using System;
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using Godot;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes;
using STS2Agent.Models;
using STS2Agent.Services;

namespace STS2Agent;

[ModInitializer("Initialize")]
public static class STS2Agent
{
    public const string Version = "1.0.0";

    private static HttpListener? _listener;
    private static Thread? _serverThread;
    private static readonly ConcurrentQueue<RequestContext> _mainThreadQueue = new();
    private static GameStateService? _gameStateService;
    private static CardRewardService? _cardRewardService;
    private static bool _initialized;
    private static int _updateTickCounter;

    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private class RequestContext
    {
        public HttpListenerContext? Context { get; init; }
        public Func<GameStateService?, object> StateGetter { get; init; } = _ => new { error = "no getter" };
    }

    public static void Initialize()
    {
        Logger.Info("[STS2Agent] === Initialize() 开始 ===");

        try
        {
            Logger.Info("Initialize: 正在创建 GameStateService...");
            _gameStateService = new GameStateService();
            Logger.Info("Initialize: GameStateService 创建成功");

            Logger.Info("Initialize: 正在创建 CardRewardService...");
            var gameAssembly = typeof(MegaCrit.Sts2.Core.Nodes.NGame).Assembly;
            _cardRewardService = new CardRewardService(gameAssembly);
            Logger.Info("Initialize: CardRewardService 创建成功");

            _initialized = true;

            // 将 GameLoopNode 加入场景树，使其 _Process 每帧被调用
            var loopNode = new GameLoopNode();

            // NGame.Instance 在 _EnterTree() 中同步设置，mod 初始化时必然非 null
            // 使用 CallDeferred 确保节点在主线程添加
            if (NGame.Instance != null)
            {
                Logger.Info("Initialize: NGame.Instance 可用，使用 CallDeferred 添加节点");
                NGame.Instance.CallDeferred("add_child", loopNode);
                Logger.Info("Initialize: GameLoopNode 已入队等待加入场景树");
            }
            else
            {
                // 兜底：启动独立轮询线程（每 100ms 检查一次，最多等 30 秒）
                Logger.Error("Initialize: NGame.Instance 为 null，启动 fallback 轮询线程");
                StartFallbackPollingThread();
            }

            const int port = 8888;
            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://localhost:{port}/");
                _listener.Start();
                Logger.Info($"Initialize: 端口 {port} 绑定成功!");

                var debugDir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "STS2Agent");
                System.IO.Directory.CreateDirectory(debugDir);
                var portFile = System.IO.Path.Combine(debugDir, "port.txt");
                System.IO.File.WriteAllText(portFile, port.ToString());
            }
            catch (HttpListenerException ex)
            {
                Logger.Error($"Initialize: 端口绑定失败", ex);
                return;
            }

            _serverThread = new Thread(ServerLoop)
            {
                IsBackground = true,
                Name = "STS2Agent_Server"
            };
            _serverThread.Start();
            Logger.Info($"Initialize: HTTP服务器已启动 - http://localhost:{port}/");
        }
        catch (Exception ex)
        {
            Logger.Error("Initialize: 初始化失败", ex);
        }
    }

    public static void Update()
    {
        if (!_initialized)
            return;

        _updateTickCounter++;
        if (_updateTickCounter % 300 == 0)
            Logger.Info($"[心跳] tick={_updateTickCounter}, cardRewardVisible={_cardRewardService?.GetCurrentReward().IsVisible}");

        if (_gameStateService == null)
            return;

        _gameStateService.Update();
        _cardRewardService?.Update();

        int processed = 0;
        while (_mainThreadQueue.TryDequeue(out var request) && processed < 10)
        {
            processed++;
            try
            {
                var result = request!.StateGetter(_gameStateService);
                SendJson(request.Context!, result);
            }
            catch (Exception ex)
            {
                Logger.Error($"Update: 请求处理异常", ex);
                try { SendError(request!.Context!, 500, ex.Message); } catch { }
            }
        }
    }

    public static Task RunOnMainThread(Action action)
    {
        var tcs = new TaskCompletionSource<bool>();
        _mainThreadQueue.Enqueue(new RequestContext
        {
            Context = null,
            StateGetter = _ => { action(); return true; }
        });
        return tcs.Task;
    }

    public static Task<T> RunOnMainThread<T>(Func<T> func)
    {
        var tcs = new TaskCompletionSource<T>();
        _mainThreadQueue.Enqueue(new RequestContext
        {
            Context = null,
            StateGetter = _ => { tcs.SetResult(func()); return true!; }
        });
        return tcs.Task;
    }

    private static void StartFallbackPollingThread()
    {
        var thread = new Thread(() =>
        {
            Logger.Info("FallbackPolling: 线程启动，每 100ms 检查一次");
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            while (stopwatch.ElapsedMilliseconds < 30000 && !_disposed)
            {
                Thread.Sleep(100);
                if (NGame.Instance != null)
                {
                    var loopNode = new GameLoopNode();
                    NGame.Instance.CallDeferred("add_child", loopNode);
                    Logger.Info("FallbackPolling: NGame.Instance 可用，节点已通过 CallDeferred 添加");
                    stopwatch.Stop();
                    return;
                }
            }
            Logger.Error("FallbackPolling: 30秒内未找到 NGame.Instance，轮询放弃");
        })
        {
            IsBackground = true,
            Name = "STS2Agent_FallbackPolling"
        };
        thread.Start();
    }

    private static volatile bool _disposed;
    private static readonly ManualResetEventSlim _disposedEvent = new(false);

    private static void Shutdown()
    {
        Logger.Info("Shutdown: 关闭中...");
        _disposed = true;
        _disposedEvent.Set();
        _listener?.Stop();
        _listener?.Close();
        _initialized = false;
    }

    private static void ServerLoop()
    {
        Logger.Info("ServerLoop: 服务器线程启动");
        while (_listener?.IsListening == true)
        {
            try
            {
                var context = _listener.GetContext();
                ThreadPool.QueueUserWorkItem(_ => HandleRequest(context));
            }
            catch (HttpListenerException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (Exception ex)
            {
                Logger.Error("ServerLoop: 异常", ex);
            }
        }
        Logger.Info("ServerLoop: 服务器线程退出");
    }

    private static void HandleRequest(HttpListenerContext context)
    {
        try
        {
            var response = context.Response;
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

            if (context.Request.HttpMethod == "OPTIONS")
            {
                response.StatusCode = 204;
                response.Close();
                return;
            }

            string path = context.Request.Url?.AbsolutePath ?? "/";
            Logger.Info($"HandleRequest: {context.Request.HttpMethod} {path}");

            bool enqueued = false;
            switch (path)
            {
                case "/api/health":
                    enqueued = EnqueueRequest(context, svc =>
                    {
                        svc?.Update();
                        var state = svc?.GetCurrentState();
                        return new
                        {
                            status = "healthy",
                            timestamp = DateTime.UtcNow,
                            version = Version,
                            inGame = state?.InGame ?? false,
                            inCombat = state?.InCombat ?? false
                        };
                    });
                    break;

                case "/api/state":
                    enqueued = EnqueueRequest(context, svc =>
                    {
                        svc?.Update();
                        return svc?.GetCurrentState() ?? new GameState();
                    });
                    break;

                case "/api/player":
                    enqueued = EnqueueRequest(context, svc =>
                    {
                        svc?.Update();
                        var state = svc?.GetCurrentState();
                        return (object)(state?.Player ?? new PlayerState());
                    });
                    break;

                case "/api/enemies":
                    enqueued = EnqueueRequest(context, svc =>
                    {
                        svc?.Update();
                        var state = svc?.GetCurrentState();
                        return (object)(state?.Enemies ?? new List<EnemyState>());
                    });
                    break;

                case "/api/combat":
                    enqueued = EnqueueRequest(context, svc =>
                    {
                        svc?.Update();
                        var state = svc?.GetCurrentState();
                        return (object)(state?.Combat ?? new CombatState());
                    });
                    break;

                case "/api/CardReward":
                    HandleCardRewardRequest(context);
                    break;

                default:
                    SendError(context, 404, "Not Found");
                    break;
            }

            if (enqueued)
                Logger.Info($"HandleRequest: {path} 已入队");
        }
        catch (Exception ex)
        {
            Logger.Error($"HandleRequest: 请求处理失败", ex);
            try { SendError(context, 500, ex.Message); } catch { }
        }
    }

    private static bool EnqueueRequest(HttpListenerContext context, Func<GameStateService?, object> stateGetter)
    {
        if (!_initialized || _gameStateService == null)
        {
            Logger.Error("EnqueueRequest: 未初始化");
            SendError(context, 503, "Service not initialized");
            return false;
        }

        _mainThreadQueue.Enqueue(new RequestContext
        {
            Context = context,
            StateGetter = stateGetter
        });
        return true;
    }

    private static void SendJson(HttpListenerContext context, object data)
    {
        try
        {
            var json = JsonSerializer.Serialize(data, JsonOptions);
            var buffer = Encoding.UTF8.GetBytes(json);
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.ContentLength64 = buffer.Length;
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            context.Response.Close();
        }
        catch (Exception ex)
        {
            Logger.Error("SendJson: 发送响应失败", ex);
            try { context.Response.Close(); } catch { }
        }
    }

    private static void SendError(HttpListenerContext context, int statusCode, string message)
    {
        try
        {
            context.Response.StatusCode = statusCode;
            SendJson(context, new { error = message });
        }
        catch { context.Response.Close(); }
    }

    private static void HandleCardRewardRequest(HttpListenerContext context)
    {
        try
        {
            var reward = _cardRewardService?.GetCurrentReward();
            if (reward != null && reward.IsVisible)
            {
                var response = new
                {
                    hasReward = true,
                    isVisible = reward.IsVisible,
                    canReroll = reward.CanReroll,
                    canSkip = reward.CanSkip,
                    cards = reward.Cards.Select(c => new
                    {
                        cardId = c.CardId,
                        name = c.Name,
                        cost = c.Cost,
                        rarity = c.Rarity,
                        type = c.Type,
                        isUpgraded = c.IsUpgraded
                    }).ToList()
                };
                Logger.Info($"[API] /api/CardReward 返回: Cards={reward.Cards.Count}");
                SendJson(context, response);
            }
            else
            {
                SendJson(context, new { hasReward = false });
            }
        }
        catch (Exception ex)
        {
            Logger.Error("[API] /api/CardReward 处理失败", ex);
            SendError(context, 500, ex.Message);
        }
    }
}
