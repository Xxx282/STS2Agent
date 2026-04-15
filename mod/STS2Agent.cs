using System;
using System.Collections.Concurrent;
using System.IO;
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
    private static bool _initialized;
    private static readonly string _logFilePath;
    private static readonly object _logLock = new();

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

    static STS2Agent()
    {
        var debugDir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "STS2Agent");
        System.IO.Directory.CreateDirectory(debugDir);
        _logFilePath = System.IO.Path.Combine(debugDir, "debug.log");
    }

    internal static void Log(string message)
    {
        lock (_logLock)
        {
            try
            {
                var logEntry = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [STS2Agent] {message}";
                File.AppendAllText(_logFilePath, logEntry + System.Environment.NewLine);
            }
            catch
            {
                // 忽略日志写入失败
            }
        }
    }

    internal static void LogError(string message, Exception? ex = null)
    {
        var fullMsg = ex != null ? $"{message}: {ex.Message}\n{ex.StackTrace}" : message;
        Log($"[ERROR] {fullMsg}");
    }

    public static void Initialize()
    {
        Log("=== Initialize() 开始 ===");

        try
        {
            Log("Initialize: 正在创建 GameStateService...");
            _gameStateService = new GameStateService();
            _initialized = true;
            Log("Initialize: GameStateService 创建成功");

            // 将 GameLoopNode 加入场景树，使其 _Process 每帧被调用
            var loopNode = new GameLoopNode();

            // NGame.Instance 在 _EnterTree() 中同步设置，mod 初始化时必然非 null
            // 使用 CallDeferred 确保节点在主线程添加
            if (NGame.Instance != null)
            {
                Log("Initialize: NGame.Instance 可用，使用 CallDeferred 添加节点");
                NGame.Instance.CallDeferred("add_child", loopNode);
                Log("Initialize: GameLoopNode 已入队等待加入场景树");
            }
            else
            {
                // 兜底：启动独立轮询线程（每 100ms 检查一次，最多等 30 秒）
                LogError("Initialize: NGame.Instance 为 null，启动 fallback 轮询线程");
                StartFallbackPollingThread();
            }

            const int port = 8888;
            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://localhost:{port}/");
                _listener.Start();
                Log($"Initialize: 端口 {port} 绑定成功!");

                var debugDir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "STS2Agent");
                var portFile = System.IO.Path.Combine(debugDir, "port.txt");
                System.IO.File.WriteAllText(portFile, port.ToString());
            }
            catch (HttpListenerException ex)
            {
                LogError($"Initialize: 端口绑定失败", ex);
                GD.PrintErr($"[STS2Agent] 无法绑定端口 {port}: {ex.Message}");
                return;
            }

            _serverThread = new Thread(ServerLoop)
            {
                IsBackground = true,
                Name = "STS2Agent_Server"
            };
            _serverThread.Start();
            Log($"Initialize: HTTP服务器已启动 - http://localhost:{port}/");
            GD.Print($"[STS2Agent] HTTP 服务器已启动 - http://localhost:{port}/");
        }
        catch (Exception ex)
        {
            LogError("Initialize: 初始化失败", ex);
            GD.PrintErr($"[STS2Agent] 初始化失败: {ex.Message}");
        }
    }

    public static void Update()
    {
        if (!_initialized)
            return;

        if (_gameStateService == null)
        {
            LogError("Update: _gameStateService 为 null");
            return;
        }

        int processed = 0;
        while (_mainThreadQueue.TryDequeue(out var request) && processed < 10)
        {
            processed++;
            try
            {
                Log($"Update: 处理请求 {request?.Context?.Request?.Url?.AbsolutePath}");
                var result = request!.StateGetter(_gameStateService);
                SendJson(request.Context!, result);
                Log($"Update: 请求处理完成");
            }
            catch (Exception ex)
            {
                LogError($"Update: 请求处理异常", ex);
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
            Log("FallbackPolling: 线程启动，每 100ms 检查一次");
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            while (stopwatch.ElapsedMilliseconds < 30000 && !_disposed)
            {
                Thread.Sleep(100);
                if (NGame.Instance != null)
                {
                    var loopNode = new GameLoopNode();
                    NGame.Instance.CallDeferred("add_child", loopNode);
                    Log("FallbackPolling: NGame.Instance 可用，节点已通过 CallDeferred 添加");
                    stopwatch.Stop();
                    return;
                }
            }
            LogError("FallbackPolling: 30秒内未找到 NGame.Instance，轮询放弃");
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
        Log("Shutdown: 关闭中...");
        _disposed = true;
        _disposedEvent.Set();
        _listener?.Stop();
        _listener?.Close();
        _initialized = false;
    }

    private static void ServerLoop()
    {
        Log("ServerLoop: 服务器线程启动");
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
                LogError("ServerLoop: 异常", ex);
            }
        }
        Log("ServerLoop: 服务器线程退出");
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
            Log($"HandleRequest: {context.Request.HttpMethod} {path}");

            bool enqueued = false;
            switch (path)
            {
                case "/":
                    SendHtml(context);
                    break;

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

                default:
                    SendError(context, 404, "Not Found");
                    break;
            }

            if (enqueued)
                Log($"HandleRequest: {path} 已入队");
        }
        catch (Exception ex)
        {
            LogError("HandleRequest: 请求处理失败", ex);
            try { SendError(context, 500, ex.Message); } catch { }
        }
    }

    private static bool EnqueueRequest(HttpListenerContext context, Func<GameStateService?, object> stateGetter)
    {
        if (!_initialized || _gameStateService == null)
        {
            LogError("EnqueueRequest: 未初始化");
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
            LogError("SendJson: 发送响应失败", ex);
            try { context.Response.Close(); } catch { }
        }
    }

    private static void SendHtml(HttpListenerContext context)
    {
        try
        {
            var html = @"<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <title>STS2Agent API</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 40px; background: #1a1a2e; color: #eee; }
        h1 { color: #00d4ff; }
        ul { line-height: 1.8; }
        a { color: #00d4ff; }
        .endpoint { background: #16213e; padding: 10px; border-radius: 5px; margin: 5px 0; }
    </style>
</head>
<body>
    <h1>STS2Agent API</h1>
    <p>Slay the Spire 2 智能代理接口</p>
    <div class=""endpoint""><strong>GET</strong> <a href=""/api/health"">/api/health</a> - 健康检查</div>
    <div class=""endpoint""><strong>GET</strong> <a href=""/api/state"">/api/state</a> - 完整游戏状态</div>
    <div class=""endpoint""><strong>GET</strong> <a href=""/api/player"">/api/player</a> - 玩家状态</div>
    <div class=""endpoint""><strong>GET</strong> <a href=""/api/enemies"">/api/enemies</a> - 敌人状态</div>
    <div class=""endpoint""><strong>GET</strong> <a href=""/api/combat"">/api/combat</a> - 战斗状态</div>
</body>
</html>";
            var buffer = Encoding.UTF8.GetBytes(html);
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.ContentLength64 = buffer.Length;
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            context.Response.Close();
        }
        catch
        {
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
}
