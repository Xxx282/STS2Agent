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
using STS2Agent.UI;

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
    private static CardStatsService? _cardStatsService;
    private static CardHoverService? _cardHoverService;
    private static CardTooltipNode? _cardTooltip;
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
        if (_initialized)
        {
            Logger.Warn("[STS2Agent] Initialize() 已被调用，跳过重复初始化");
            return;
        }

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

            Logger.Info("Initialize: 正在创建 CardStatsService...");
            _cardStatsService = new CardStatsService();
            Logger.Info($"Initialize: CardStatsService 创建成功, IsLoaded={_cardStatsService.IsLoaded}");

            // 创建悬停检测服务和悬浮提示框
            Logger.Info("Initialize: 正在创建 CardHoverService...");
            _cardHoverService = new CardHoverService();
            Logger.Info("Initialize: 正在创建 CardTooltipNode...");
            _cardTooltip = new CardTooltipNode();

            // 将 CardStatsService 注入 CardRewardService，使其在提取数据时自动注入 stats
            if (_cardStatsService != null)
            {
                _cardRewardService!.SetCardStatsService(_cardStatsService!);
            }

            if (_cardRewardService != null)
            {
                _cardRewardService.OnCardRewardAppeared += OnCardRewardAppeared;
                _cardRewardService.OnCardRewardChanged += OnCardRewardChanged;
                Logger.Info("Initialize: 卡牌奖励事件订阅完成");
            }

            _initialized = true;

            // 将 GameLoopNode 加入场景树，使其 _Process 每帧被调用
            var loopNode = new GameLoopNode();

            // NGame.Instance 在 _EnterTree() 中同步设置，mod 初始化时必然非 null
            // 使用 CallDeferred 确保节点在主线程添加
            if (NGame.Instance != null)
            {
                Logger.Info("Initialize: NGame.Instance 可用，使用 CallDeferred 添加节点");
                NGame.Instance.CallDeferred("add_child", loopNode);
                NGame.Instance.CallDeferred("add_child", _cardTooltip);
                NGame.Instance.CallDeferred("add_child", _cardHoverService);
                Logger.Info("Initialize: GameLoopNode、CardTooltipNode、CardHoverService 已入队等待加入场景树");
            }
            else
            {
                // 兜底：启动独立轮询线程（每 100ms 检查一次，最多等 30 秒）
                Logger.Error("Initialize: NGame.Instance 为 null，启动 fallback 轮询线程");
                StartFallbackPollingThread();
            }

            // 初始化悬停服务并订阅事件
            if (_cardHoverService != null)
            {
                _cardHoverService.Initialize(_cardRewardService!);
                _cardHoverService.OnCardHovered += OnCardHovered;
                _cardHoverService.OnCardUnhovered += OnCardUnhovered;
                Logger.Info("Initialize: 悬停服务事件订阅完成");
            }

            const int port = 8890;
            int boundPort = port;
            Logger.Info($"Initialize: 准备绑定端口 {port}，当前进程ID={System.Diagnostics.Process.GetCurrentProcess().Id}");

            // 尝试注册 URL 保留权限（需要管理员权限，非管理员会静默跳过）
            TryRegisterUrlAcl(port);
            TryRegisterUrlAcl(8891);
            TryRegisterUrlAcl(8892);

            for (int attempt = 0; attempt < 3; attempt++)
            {
                int tryPort = attempt == 0 ? port : (attempt == 1 ? 8891 : 8892);
                Logger.Info($"Initialize: 尝试绑定端口 {tryPort}...");
                try
                {
                    _listener = new HttpListener();
                    _listener.Prefixes.Add($"http://localhost:{tryPort}/");
                    _listener.Start();
                    boundPort = tryPort;
                    Logger.Info($"Initialize: 端口 {boundPort} 绑定成功!");
                    break;
                }
                catch (HttpListenerException ex)
                {
                    Logger.Warn($"Initialize: 端口 {tryPort} 绑定失败 (code={ex.ErrorCode}): {ex.Message}");
                    if (ex.ErrorCode == 5 || ex.ErrorCode == 32 || ex.ErrorCode == 183)
                    {
                        // 权限不足或进程占用，尝试 netsh 注册
                        if (TryRegisterUrlAcl(tryPort))
                        {
                            try
                            {
                                _listener = new HttpListener();
                                _listener.Prefixes.Add($"http://localhost:{tryPort}/");
                                _listener.Start();
                                boundPort = tryPort;
                                Logger.Info($"Initialize: urlacl 注册后端口 {boundPort} 绑定成功!");
                                break;
                            }
                            catch (HttpListenerException ex2)
                            {
                                Logger.Warn($"Initialize: urlacl 注册后仍然绑定失败 (code={ex2.ErrorCode}): {ex2.Message}");
                            }
                        }
                    }
                    if (_listener != null) { try { _listener.Close(); } catch { } _listener = null; }
                    if (attempt == 2) Logger.Error($"Initialize: 所有端口绑定均失败，HTTP 服务不可用", ex);
                }
            }

            try
            {
                var debugDir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "STS2Agent");
                System.IO.Directory.CreateDirectory(debugDir);
                var portFile = System.IO.Path.Combine(debugDir, "port.txt");
                System.IO.File.WriteAllText(portFile, boundPort.ToString());
            } catch { }

            _serverThread = new Thread(ServerLoop)
            {
                IsBackground = true,
                Name = "STS2Agent_Server"
            };
            _serverThread.Start();
            Logger.Info($"Initialize: HTTP服务器已启动 - http://localhost:{boundPort}/");
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
                var character = DetectCurrentCharacter();
                var response = new
                {
                    hasReward = true,
                    isVisible = reward.IsVisible,
                    canReroll = reward.CanReroll,
                    canSkip = reward.CanSkip,
                    cards = reward.Cards.Select(c =>
                    {
                        var s = _cardStatsService?.GetStats(character, c.CardId, c.EnglishId);
                        return new
                        {
                            name = c.Name,
                            cost = c.Cost,
                            rarity = c.Rarity,
                            type = c.Type,
                            isUpgraded = c.IsUpgraded,
                            pickRate = s?.PickRate,
                            winRateDelta = s?.WinRateDelta,
                            skadaScore = s?.SkadaScore,
                            rank = s?.Rank,
                            confidence = s?.Confidence,
                        };
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

    private static string DetectCurrentCharacter()
    {
        try
        {
            var state = _gameStateService?.GetCurrentState();
            var player = state?.Player;
            if (player == null) return "IRONCLAD";

            // 通过手中卡牌 ID 推断角色
            // IRONCLAD: Strike_IR, Defend_IR, Bash_IR 等后缀
            // SILENT: Strike_Watcher, Defend_Watcher, Slash_Watcher 等
            // DEFECT: Strike_Defect, Defend_Defect, Zap_Defect 等
            var hand = player.Hand;
            if (hand.Count > 0)
            {
                foreach (var card in hand)
                {
                    if (card.Contains("Watcher")) return "WATCHER";
                    if (card.Contains("Defect")) return "DEFECT";
                    if (card.Contains("Ironclad")) return "IRONCLAD";
                    if (card.Contains("Silent")) return "SILENT";
                    if (card.Contains("_IR")) return "IRONCLAD";
                    if (card.Contains("_SW")) return "SILENT";
                    if (card.Contains("_DF")) return "DEFECT";
                    if (card.Contains("_AW")) return "AWAKENEDONE";
                }
            }

            // 通过弃牌堆/抽牌堆推断
            var drawPile = player.DrawPile;
            foreach (var card in drawPile)
            {
                if (card.Contains("Watcher")) return "WATCHER";
                if (card.Contains("Defect")) return "DEFECT";
                if (card.Contains("_DF")) return "DEFECT";
                if (card.Contains("_AW")) return "AWAKENEDONE";
            }

            // 备用：通过玩家拥有金币数量推断（仅供参考）
            // 新手角色通常是默认角色
            return "IRONCLAD";
        }
        catch
        {
            return "IRONCLAD";
        }
    }

    private static void OnCardRewardAppeared(CardRewardInfo reward)
    {
        try
        {
            Logger.Info($"[CardReward] OnCardRewardAppeared 收到，Cards={reward.Cards.Count}");
            var character = DetectCurrentCharacter();
            _cardRewardService?.SetCharacter(character);
            Logger.Info($"[CardReward] 当前角色={character}");
        }
        catch (Exception ex)
        {
            Logger.Error("[CardReward] OnCardRewardAppeared 异常", ex);
        }
    }

    private static void OnCardRewardChanged(CardRewardInfo reward)
    {
        try
        {
            if (!reward.IsVisible || reward.Cards.Count == 0)
                return;
            var character = DetectCurrentCharacter();
            _cardRewardService?.SetCharacter(character);
        }
        catch (Exception ex)
        {
            Logger.Error("[CardReward] OnCardRewardChanged 异常", ex);
        }
    }

    private static void OnCardHovered(RewardCardInfo card)
    {
        try
        {
            if (card.ScreenX.HasValue && card.ScreenY.HasValue)
            {
                _cardTooltip?.ShowAt(card, card.ScreenX.Value, card.ScreenY.Value);
            }
        }
        catch (Exception ex)
        {
            Logger.Error("[Hover] OnCardHovered 异常", ex);
        }
    }

    private static void OnCardUnhovered()
    {
        try
        {
            _cardTooltip?.Hide();
        }
        catch (Exception ex)
        {
            Logger.Error("[Hover] OnCardUnhovered 异常", ex);
        }
    }

    private static bool TryRegisterUrlAcl(int port)
    {
        var url = $"http://localhost:{port}/";
        try
        {
            // 先检查是否已有保留权限
            var checkPsi = new System.Diagnostics.ProcessStartInfo("netsh", $"http show urlacl url={url}");
            checkPsi.UseShellExecute = false;
            checkPsi.RedirectStandardOutput = true;
            checkPsi.CreateNoWindow = true;
            var checkProc = System.Diagnostics.Process.Start(checkPsi);
            var checkOutput = checkProc!.StandardOutput.ReadToEnd();
            checkProc.WaitForExit();
            if (checkOutput.Contains("ERROR:") || checkProc.ExitCode != 0)
            {
                // 无保留权限，尝试添加
                var addPsi = new System.Diagnostics.ProcessStartInfo("netsh", $"http add urlacl url={url} user=\"Everyone\"");
                addPsi.UseShellExecute = false;
                addPsi.RedirectStandardOutput = true;
                addPsi.CreateNoWindow = true;
                var addProc = System.Diagnostics.Process.Start(addPsi);
                var addOutput = addProc!.StandardOutput.ReadToEnd();
                addProc.WaitForExit();
                if (addProc.ExitCode == 0)
                {
                    Logger.Info($"Initialize: urlacl 注册成功 (Everyone) -> {url}");
                    return true;
                }
                else
                {
                    Logger.Warn($"Initialize: urlacl 注册失败: {addOutput.Trim()}");
                    return false;
                }
            }
            else
            {
                Logger.Info($"Initialize: urlacl 已存在 -> {url}");
                return true;
            }
        }
        catch (Exception ex)
        {
            Logger.Info($"Initialize: urlacl 操作异常: {ex.Message}");
            return false;
        }
    }
}
