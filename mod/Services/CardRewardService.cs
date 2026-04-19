using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using STS2Agent.Models;
using Godot;

namespace STS2Agent.Services;

public class CardRewardService
{
    private CardRewardInfo _currentReward = new() { IsVisible = false };
    private readonly object _lock = new();
    private Assembly? _gameAssembly;
    private Dictionary<string, Type?> _typeCache = new();
    private readonly string _logFilePath;
    private readonly object _logLock = new();
    private int _updateCounter;
    private CardStatsService? _cardStatsService;
    private string _currentCharacter = "IRONCLAD";

    // Hook 拦截相关（预留）
    // 注意：通过反射拦截 Hook 需要 IL 编织，暂时使用 Godot 节点扫描方案

    // 检测策略结果缓存，避免重复检测
    private bool _staticReflectionFailed = false;

    // 扫描优化：缓存上次扫描结果，减少扫描频率
    private int _lastExtractedCardCount;
    private int _lastLogFrame;
    private bool _lastScreenFound;
    private const int SCAN_INTERVAL_FRAMES = 10;  // 每10帧扫描一次

    // 调试日志节流
    private int _lastCardExtractLogFrame;

    public event Action<CardRewardInfo>? OnCardRewardChanged;
    public event Action<CardRewardInfo>? OnCardRewardAppeared;

    private static string GetLogDir()
    {
        string executablePath = OS.GetExecutablePath();
        string directoryName = Path.GetDirectoryName(executablePath) ?? "";
        return Path.Combine(directoryName, "mods", "STS2Agent", "logs");
    }

    public CardRewardService(Assembly gameAssembly)
    {
        _gameAssembly = gameAssembly;
        string logDir = GetLogDir();
        Directory.CreateDirectory(logDir);
        _logFilePath = Path.Combine(logDir, "card_reward.log");
        Log("CardRewardService 初始化完成");
    }

    public void SetCardStatsService(CardStatsService cardStatsService)
    {
        _cardStatsService = cardStatsService;
    }

    public void SetCharacter(string character)
    {
        _currentCharacter = character;
    }

    private void InjectStatsToReward(CardRewardInfo reward)
    {
        if (_cardStatsService == null || !reward.IsVisible || reward.Cards.Count == 0) return;
        foreach (var card in reward.Cards)
        {
            var stats = _cardStatsService.GetStats(_currentCharacter, card.CardId, card.EnglishId);
            if (stats != null)
            {
                card.PickRate = stats.PickRate;
                card.WinRateDelta = stats.WinRateDelta;
                card.SkadaScore = stats.SkadaScore;
                card.HoldStrength = stats.HoldStrength != 0 ? stats.HoldStrength : stats.WinRateDelta;
                card.Rank = stats.Rank;
                card.DisplayNameZh = stats.DisplayNameZh;
            }
        }
    }

    public CardRewardInfo GetCurrentReward()
    {
        lock (_lock)
        {
            return Clone(_currentReward);
        }
    }

    public void Update()
    {
        _updateCounter++;

        // 心跳日志：每 10 秒（约 600 帧）输出一次
        if (_updateCounter % 600 == 1)
        {
            lock (_lock)
            {
                Log($"[心跳] Update #{_updateCounter}, 当前 IsVisible={_currentReward.IsVisible}, Cards={_currentReward.Cards.Count}");
            }
        }

        // 扫描频率控制：每 SCAN_INTERVAL_FRAMES 帧执行一次完整扫描
        // 除非有状态变化需要立即检测
        if (_updateCounter % SCAN_INTERVAL_FRAMES != 0)
        {
            return;
        }

        try
        {
            var newReward = CaptureCardRewardState();
            lock (_lock)
            {
                bool changed = HasChanged(_currentReward, newReward);
                if (changed)
                {
                    bool newAppeared = !_currentReward.IsVisible && newReward.IsVisible;
                    bool disappeared = _currentReward.IsVisible && !newReward.IsVisible;
                    _currentReward = newReward;

                    // 注入统计数据
                    InjectStatsToReward(_currentReward);

                    // 状态变化时记录日志
                    Log($"[状态变化] IsVisible={newReward.IsVisible}, Cards={newReward.Cards.Count}, Source={newReward.RewardSource}");
                    if (newReward.IsVisible && newReward.Cards.Count > 0)
                    {
                        Log($"[奖励出现] Cards={string.Join(", ", newReward.Cards.Select(c => c.Name))}");
                    }
                    else if (disappeared)
                    {
                        Log($"[奖励结束] 卡牌奖励界面已关闭");
                    }

                    OnCardRewardChanged?.Invoke(Clone(_currentReward));

                    if (newAppeared && newReward.IsVisible && newReward.Cards.Count > 0)
                    {
                        Log($"[奖励出现] 触发 OnCardRewardAppeared, Cards={newReward.Cards.Count}");
                        OnCardRewardAppeared?.Invoke(Clone(_currentReward));
                    }
                    else if (disappeared)
                    {
                        // 奖励结束后清空缓存，下次出现时重新触发
                        Log($"[奖励结束] 清空缓存");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            LogError("Update 异常", ex);
        }
    }

    private CardRewardInfo CaptureCardRewardState()
    {
        var reward = new CardRewardInfo { IsVisible = false };

        // 策略1: 静态反射 CardReward.CurrentReward
        if (!_staticReflectionFailed)
        {
            TryCaptureFromStaticReflection(ref reward);
        }

        // 策略2: Godot 节点树遍历（如果策略1失败或找到部分数据）
        if (!reward.IsVisible || reward.CardCount == 0)
        {
            TryCaptureFromGodotNodes(ref reward);
        }

        return reward;
    }

    private void TryCaptureFromStaticReflection(ref CardRewardInfo reward)
    {
        try
        {
            // 尝试多种路径查找 CardReward.CurrentReward
            var cardRewardType = GetCachedType("MegaCrit.Sts2.Core.Rewards.CardReward");
            if (cardRewardType == null)
            {
                Log("[静态反射] MegaCrit.Sts2.Core.Rewards.CardReward 类型未找到");
                _staticReflectionFailed = true;
                return;
            }

            // 尝试路径A: CardReward.CurrentReward (静态属性)
            object? currentReward = null;
            var currentRewardProp = cardRewardType.GetProperty("CurrentReward",
                BindingFlags.Public | BindingFlags.Static);
            if (currentRewardProp != null)
            {
                currentReward = currentRewardProp.GetValue(null);
            }

            // 尝试路径B: RunManager.CurrentCardReward (如果在 RunManager 上)
            if (currentReward == null)
            {
                var runManagerType = GetCachedType("MegaCrit.Sts2.Core.Runs.RunManager");
                if (runManagerType != null)
                {
                    var currentProp = runManagerType.GetProperty("CurrentCardReward",
                        BindingFlags.Public | BindingFlags.Static);
                    if (currentProp == null)
                        currentProp = runManagerType.GetProperty("CurrentReward",
                            BindingFlags.Public | BindingFlags.Static);

                    if (currentProp != null)
                    {
                        currentReward = currentProp.GetValue(null);
                    }
                }
            }

            // 尝试路径C: 通过 Player.RunState 查找当前奖励
            if (currentReward == null)
            {
                var playerType = GetCachedType("MegaCrit.Sts2.Core.Players.Player");
                if (playerType != null)
                {
                    var runStateProp = playerType.GetProperty("RunState",
                        BindingFlags.Public | BindingFlags.Instance);
                    if (runStateProp != null)
                    {
                        // 尝试获取 Player.Singleton
                        var playerSingleton = playerType.GetProperty("Singleton",
                            BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                        if (playerSingleton != null)
                        {
                            var runState = runStateProp.GetValue(playerSingleton);
                            if (runState != null)
                            {
                                // RunState 上可能有 CurrentReward 或 PendingRewards
                                var runStateType = runState.GetType();
                                var pendingProp = runStateType.GetProperty("CurrentCardReward")
                                    ?? runStateType.GetProperty("PendingRewards")
                                    ?? runStateType.GetProperty("RewardState");
                                if (pendingProp != null)
                                {
                                    currentReward = pendingProp.GetValue(runState);
                                }
                            }
                        }
                    }
                }
            }

            if (currentReward != null && cardRewardType.IsInstanceOfType(currentReward))
            {
                reward.IsVisible = true;
                reward.CanReroll = GetPropertyBool(currentReward, "CanReroll") ?? false;
                reward.CanSkip = GetPropertyBool(currentReward, "CanSkip") ?? true;
                reward.Cards = ExtractCardInfo(currentReward);
                reward.CardCount = reward.Cards.Count;
                reward.RewardSource = DetectRewardSource(currentReward);

                if (_updateCounter % 600 == 1)
                    Log($"[静态反射] 成功: Cards={reward.CardCount}, Source={reward.RewardSource}");
            }
            else
            {
                if (_updateCounter % 600 == 1)
                    Log("[静态反射] 未找到 CardReward.CurrentReward");
            }
        }
        catch (Exception ex)
        {
            LogError("[静态反射] 失败", ex);
            _staticReflectionFailed = true;
        }
    }

    private void TryCaptureFromGodotNodes(ref CardRewardInfo reward)
    {
        try
        {
            var rootType = GetCachedType("MegaCrit.Sts2.Core.Nodes.NGame");
            if (rootType == null) return;

            var instanceProp = rootType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            var root = instanceProp?.GetValue(null) as Godot.Node;
            if (root == null) return;

            // 扫描所有可能的奖励界面节点类型
            var screenTypes = new[]
            {
                "MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NCardRewardSelectionScreen",
                "MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.CardRewardScreen",
                "MegaCrit.Sts2.Core.Nodes.Screens.Reward.RewardScreen",
                "MegaCrit.Sts2.Core.Nodes.Screens.Reward.NRewardScreen",
                "MegaCrit.Sts2.Core.Nodes.Overlays.RewardOverlay",
                "MegaCrit.Sts2.Core.Nodes.Overlays.NRewardOverlay",
            };

            Godot.Node? foundNode = null;
            string foundTypeName = "";

            foreach (var typeName in screenTypes)
            {
                var screenType = GetCachedType(typeName);
                if (screenType == null) continue;

                var node = FindNodeByType(root, screenType);
                if (node != null && IsScreenVisible(node))
                {
                    foundNode = node;
                    foundTypeName = typeName;
                    break;
                }
            }

            if (foundNode == null)
            {
                if (!_lastScreenFound && _updateCounter % 600 == 1)
                {
                    Log($"[节点检测] 当前根节点: {root.Name}, 类型: {root.GetType().FullName}");
                    DumpSceneStructure(root, 0, 3);
                }
                _lastScreenFound = false;
                return;
            }

            _lastScreenFound = true;

            // 优先尝试直接从节点属性获取卡牌数据
            if (ExtractCardsFromScreen(foundNode, ref reward))
            {
                reward.IsVisible = true;
                reward.CardCount = reward.Cards.Count;
                // Godot节点路径：尝试从界面节点扫描Skip/Reroll按钮状态
                TryExtractSkipRerollFromNodes(foundNode, ref reward);
                return;
            }

            // 如果直接获取失败，尝试通过子节点搜索卡牌持有者
            if (ExtractCardsFromChildNodes(foundNode, ref reward))
            {
                reward.IsVisible = true;
                TryExtractSkipRerollFromNodes(foundNode, ref reward);
                return;
            }

            // 如果仍然失败，至少标记界面可见
            reward.IsVisible = true;
        }
        catch (Exception ex)
        {
            LogError("[节点检测] 异常", ex);
        }
    }

    // 从子节点中递归搜索卡牌数据
    private bool ExtractCardsFromChildNodes(Godot.Node parentNode, ref CardRewardInfo reward)
    {
        try
        {
            // 查找包含 "Card" 或 "Holder" 关键字的子节点
            var allChildren = new List<Godot.Node>();
            CollectAllChildren(parentNode, allChildren, 0, 10);

            foreach (var child in allChildren)
            {
                var childType = child.GetType();

                // 检查是否是卡牌持有者类型的节点
                if (childType.FullName?.Contains("CardHolder") == true ||
                    childType.FullName?.Contains("CardSlot") == true ||
                    childType.FullName?.Contains("OptionSlot") == true)
                {
                    // 尝试从持有者中提取卡牌信息（含节点坐标）
                    var cardInfo = ExtractCardFromNodeOrHolder(child, out float? sx, out float? sy);
                    if (cardInfo != null && !reward.Cards.Any(c => c.CardId == cardInfo.CardId))
                    {
                        cardInfo.ScreenX = sx;
                        cardInfo.ScreenY = sy;
                        reward.Cards.Add(cardInfo);
                    }
                }

                // 检查节点的属性是否包含 CardModel 或 CardCreationResult
                foreach (var prop in childType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (prop.PropertyType.Name.Contains("Card") &&
                        (prop.PropertyType.FullName?.Contains("CardModel") == true ||
                         prop.PropertyType.FullName?.Contains("CardCreationResult") == true))
                    {
                        try
                        {
                            var cardObj = prop.GetValue(child);
                            if (cardObj != null)
                            {
                                var cardInfo = ExtractCardFromNode(cardObj);
                                if (cardInfo != null && !reward.Cards.Any(c => c.CardId == cardInfo.CardId))
                                {
                                    reward.Cards.Add(cardInfo);
                                }
                            }
                        }
                        catch { }
                    }

                    // 检查 IEnumerable 属性
                    if (typeof(System.Collections.IEnumerable).IsAssignableFrom(prop.PropertyType) &&
                        prop.PropertyType != typeof(string) &&
                        prop.Name.Contains("Card"))
                    {
                        try
                        {
                            var list = prop.GetValue(child) as System.Collections.IEnumerable;
                            if (list != null)
                            {
                                foreach (var item in list)
                                {
                                    if (item == null) continue;
                                    var cardInfo = ExtractCardFromNode(item);
                                    if (cardInfo != null && !reward.Cards.Any(c => c.CardId == cardInfo.CardId))
                                    {
                                        reward.Cards.Add(cardInfo);
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                }
            }

            if (reward.Cards.Count > 0)
            {
                reward.CardCount = reward.Cards.Count;
                Log($"[子节点搜索] 从子节点提取到 {reward.Cards.Count} 张卡牌");
                return true;
            }
        }
        catch (Exception ex)
        {
            LogError("[子节点搜索] 异常", ex);
        }
        return false;
    }

    private void CollectAllChildren(Godot.Node node, List<Godot.Node> result, int currentDepth, int maxDepth)
    {
        if (currentDepth > maxDepth) return;
        foreach (Godot.Node child in node.GetChildren())
        {
            result.Add(child);
            CollectAllChildren(child, result, currentDepth + 1, maxDepth);
        }
    }

    private Godot.Node? FindNodeByType(Godot.Node root, Type targetType)
    {
        try
        {
            if (root.GetType() == targetType) return root;
            foreach (Godot.Node child in root.GetChildren())
            {
                var found = FindNodeByType(child, targetType);
                if (found != null) return found;
            }
        }
        catch { }
        return null;
    }

    private bool IsScreenVisible(Godot.Node node)
    {
        try
        {
            // 检查 Visible 属性
            var visibleProp = node.GetType().GetProperty("Visible",
                BindingFlags.Public | BindingFlags.Instance);
            if (visibleProp != null)
            {
                var visible = visibleProp.GetValue(node);
                if (visible is bool b && b) return true;
            }

            // 检查 ProcessMode 或 Modulate (完全透明则不可见)
            var modulate = node.GetType().GetProperty("Modulate",
                BindingFlags.Public | BindingFlags.Instance)?.GetValue(node) as Godot.Color?;
            if (modulate.HasValue && modulate.Value.A > 0.001f) return true;

            // 检查 IsNodeReady
            var ready = node.GetType().GetProperty("IsNodeReady",
                BindingFlags.Public | BindingFlags.Instance)?.GetValue(node) as bool?;
            if (ready == true) return true;

            return false;
        }
        catch { return false; }
    }

    // 从Godot节点扫描Skip/Reroll按钮状态
    private void TryExtractSkipRerollFromNodes(Godot.Node screenNode, ref CardRewardInfo reward)
    {
        try
        {
            // 收集所有子节点
            var allChildren = new List<Godot.Node>();
            CollectAllChildren(screenNode, allChildren, 0, 10);

            foreach (var child in allChildren)
            {
                var childName = child.Name?.ToString().ToLowerInvariant() ?? "";
                var childTypeName = child.GetType().Name?.ToLowerInvariant() ?? "";

                // 检查Skip按钮
                if (childName.Contains("skip") || childTypeName.Contains("skip") ||
                    childName.Contains("accept") || childTypeName.Contains("accept"))
                {
                    var disabled = IsNodeDisabled(child);
                    if (!disabled)
                    {
                        reward.CanSkip = true;
                    }
                }

                // 检查Reroll按钮
                if (childName.Contains("reroll") || childTypeName.Contains("reroll") ||
                    childName.Contains("reroll") || childTypeName.Contains("reroll"))
                {
                    var disabled = IsNodeDisabled(child);
                    if (!disabled)
                    {
                        reward.CanReroll = true;
                    }
                }
            }

            // 如果没有找到明确的Skip/Reroll节点，但界面可见且有卡牌，默认允许Skip
            if (!reward.CanSkip && reward.IsVisible && reward.Cards.Count > 0)
            {
                reward.CanSkip = true;
            }
        }
        catch (Exception ex)
        {
            LogError("[SkipReroll提取] 异常", ex);
        }
    }

    // 检查节点是否处于禁用状态
    private bool IsNodeDisabled(Godot.Node node)
    {
        try
        {
            var nodeType = node.GetType();

            // 检查 Disabled 属性
            var disabledProp = nodeType.GetProperty("Disabled",
                BindingFlags.Public | BindingFlags.Instance);
            if (disabledProp != null)
            {
                var val = disabledProp.GetValue(node);
                if (val is bool b) return b;
            }

            // 检查 ProcessMode (禁用时通常被设为 Disabled 或 Paused)
            var processMode = nodeType.GetProperty("ProcessMode",
                BindingFlags.Public | BindingFlags.Instance)?.GetValue(node);
            if (processMode != null)
            {
                var pmStr = processMode.ToString();
                if (pmStr != null && pmStr.Contains("Disabled")) return true;
            }

            // 检查 Modulate (完全透明则可能禁用)
            var modulate = nodeType.GetProperty("Modulate",
                BindingFlags.Public | BindingFlags.Instance)?.GetValue(node) as Godot.Color?;
            if (modulate.HasValue && modulate.Value.A < 0.01f) return true;
        }
        catch { }
        return false;
    }

    private bool ExtractCardsFromScreen(Godot.Node screenNode, ref CardRewardInfo reward)
    {
        try
        {
            var screenType = screenNode.GetType();

            // 方法1: _cardHolders 字段 (在屏幕初始化时的字段名)
            var cardHoldersField = screenType.GetField("_cardHolders",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (cardHoldersField != null)
            {
                var holders = cardHoldersField.GetValue(screenNode) as System.Collections.IEnumerable;
                if (holders != null)
                {
                    int count = 0;
                    foreach (var holder in holders)
                    {
                        if (holder == null) continue;
                        count++;
                        var cardInfo = ExtractCardFromHolder(holder);
                        if (cardInfo != null) reward.Cards.Add(cardInfo);
                    }
                    Log($"[界面提取] _cardHolders: 找到 {count} 个 holder, 卡牌数={reward.Cards.Count}");
                    if (reward.Cards.Count > 0) return true;
                }
            }

            // 方法2: 扫描场景树查找 CardRow 节点
            var cardRow = screenNode.GetNodeOrNull("UI/CardRow") as Godot.Node;
            if (cardRow != null)
            {
                int extractedCount = 0;
                foreach (Godot.Node child in cardRow.GetChildren())
                {
                    var cardInfo = ExtractCardFromNodeOrHolder(child, out float? sx, out float? sy);
                    if (cardInfo != null)
                    {
                        cardInfo.ScreenX = sx;
                        cardInfo.ScreenY = sy;
                        reward.Cards.Add(cardInfo);
                        extractedCount++;
                    }
                }
                // 只在提取数量变化时记录日志
                if (extractedCount != _lastExtractedCardCount || _updateCounter - _lastLogFrame > 600)
                {
                    Log($"[界面提取] CardRow扫描: 提取到 {extractedCount} 张卡牌");
                    _lastExtractedCardCount = extractedCount;
                    _lastLogFrame = _updateCounter;
                }
                if (reward.Cards.Count > 0)
                {
                    reward.CardCount = reward.Cards.Count;
                    return true;
                }
            }

            // 方法3: 直接从 _options 字段提取
            var optionsField = screenType.GetField("_options",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (optionsField != null)
            {
                var options = optionsField.GetValue(screenNode) as System.Collections.IEnumerable;
                if (options != null)
                {
                    int count = 0;
                    foreach (var opt in options)
                    {
                        if (opt == null) continue;
                        count++;
                        var cardInfo = ExtractCardFromOption(opt);
                        if (cardInfo != null) reward.Cards.Add(cardInfo);
                    }
                    if (count != _lastExtractedCardCount || _updateCounter - _lastLogFrame > 600)
                    {
                        Log($"[界面提取] _options字段: 找到 {count} 个选项, 卡牌数={reward.Cards.Count}");
                        _lastExtractedCardCount = reward.Cards.Count;
                        _lastLogFrame = _updateCounter;
                    }
                    if (reward.Cards.Count > 0) return true;
                }
            }

            // 方法4: _extraOptions 字段 (替代选项)
            var extraOptionsField = screenType.GetField("_extraOptions",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (extraOptionsField != null)
            {
                var extraOptions = extraOptionsField.GetValue(screenNode) as System.Collections.IEnumerable;
                if (extraOptions != null)
                {
                    foreach (var opt in extraOptions)
                    {
                        if (opt == null) continue;
                        var cardInfo = ExtractCardFromOption(opt);
                        if (cardInfo != null && !reward.Cards.Any(c => c.CardId == cardInfo.CardId))
                            reward.Cards.Add(cardInfo);
                    }
                }
            }

            // 方法5: 通过反射扫描所有可能的列表属性
            if (reward.Cards.Count == 0)
            {
                foreach (var prop in screenType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (!typeof(System.Collections.IEnumerable).IsAssignableFrom(prop.PropertyType))
                        continue;
                    if (prop.PropertyType == typeof(string)) continue;

                    try
                    {
                        var list = prop.GetValue(screenNode) as System.Collections.IEnumerable;
                        if (list == null) continue;

                        int itemCount = 0;
                        var foundCards = new List<RewardCardInfo>();
                        foreach (var item in list)
                        {
                            itemCount++;
                            if (itemCount > 20) break;
                            if (item == null) continue;
                            var cardInfo = ExtractCardFromOption(item) ?? ExtractCardFromNodeOrHolder(item, out _, out _);
                            if (cardInfo != null) foundCards.Add(cardInfo);
                        }
                        if (itemCount > 0 && itemCount <= 10 && foundCards.Count > 0)
                        {
                            foreach (var cardInfo in foundCards)
                            {
                                if (!reward.Cards.Any(c => c.CardId == cardInfo.CardId))
                                    reward.Cards.Add(cardInfo);
                            }
                            Log($"[界面提取] 属性 '{prop.Name}' 包含 {foundCards.Count} 张卡牌");
                        }
                    }
                    catch { }
                }
            }

            reward.CardCount = reward.Cards.Count;
            return reward.Cards.Count > 0;
        }
        catch (Exception ex)
        {
            Logger.Error("[CardRewardService] [界面提取] 异常", ex);
            return false;
        }
    }

    private RewardCardInfo? ExtractCardFromHolder(object holder)
    {
        try
        {
            var holderType = holder.GetType();

            // 查找 _card 字段
            var cardField = holderType.GetField("_card",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (cardField != null)
            {
                var card = cardField.GetValue(holder);
                if (card != null) return ExtractCardFromNode(card);
            }

            // 查找 Card 属性
            var cardProp = holderType.GetProperty("Card",
                BindingFlags.Public | BindingFlags.Instance);
            if (cardProp != null)
            {
                var card = cardProp.GetValue(holder);
                if (card != null) return ExtractCardFromNode(card);
            }

            // 查找 CardModel 属性
            var cardModelProp = holderType.GetProperty("CardModel",
                BindingFlags.Public | BindingFlags.Instance);
            if (cardModelProp != null)
            {
                var card = cardModelProp.GetValue(holder);
                if (card != null) return ExtractCardFromNode(card);
            }

            Log($"[Holder提取] 未知 holder 类型: {holderType.FullName}, 属性: {string.Join(",", holderType.GetProperties().Select(p => p.Name))}");
        }
        catch (Exception ex)
        {
            LogError("[Holder提取] 异常", ex);
        }
        return null;
    }

    private RewardCardInfo? ExtractCardFromNode(object card)
    {
        try
        {
            var cardType = card.GetType();

            string? cardId = null;
            string? name = null;
            int cost = 0;
            string rarity = "Unknown";
            string type = "Unknown";
            bool isUpgraded = false;

            // CardId / Id
            cardId = GetProperty<string>(card, "Id") ?? GetProperty<string>(card, "CardId");

            // Title: LocString -> 提取 Key
            var title = GetProperty<object>(card, "Title");
            if (title != null)
            {
                var titleType = title.GetType();
                name = titleType.GetProperty("Key", BindingFlags.Public | BindingFlags.Instance)?.GetValue(title) as string;
                if (string.IsNullOrEmpty(name))
                    name = title.ToString();
            }
            if (string.IsNullOrEmpty(name))
                name = GetProperty<string>(card, "Name") ?? cardId;

            // Cost: CardEnergyCost -> Canonical (属性名是 EnergyCost)
            var costObj = GetProperty<object>(card, "EnergyCost") ?? GetProperty<object>(card, "Cost");
            if (costObj != null)
            {
                var costType = costObj.GetType();
                var canonical = costType.GetProperty("Canonical", BindingFlags.Public | BindingFlags.Instance)?.GetValue(costObj);
                if (canonical is int c) cost = c;
                else if (canonical is Enum e) cost = Convert.ToInt32(e);
                else if (canonical != null) cost = Convert.ToInt32(canonical);
            }

            // Rarity - 尝试多个可能的属性名
            var rarityObj = GetProperty<object>(card, "Rarity");
            rarity = ExtractEnumName(rarityObj) ?? "Unknown";

            // CardType 提取：扩展搜索多种可能的属性名
            object? typeObj = null;
            var typePropNames = new[] { "CardType", "Type", "CardTypeEnum", "TypeId", "CardKind", "Kind", "Trait", "CardTrait" };
            foreach (var propName in typePropNames)
            {
                typeObj = GetProperty<object>(card, propName);
                if (typeObj != null) break;
            }

            // 如果直接属性获取失败，尝试扫描所有属性找枚举类型的
            if (typeObj == null)
            {
                typeObj = FindEnumProperty(card, new[] { "Type", "Kind" });
            }

            // 尝试从卡牌 ID 反推类型（备选方案）
            if (typeObj == null || ExtractEnumName(typeObj) == "Unknown")
            {
                var inferredType = InferCardTypeFromId(cardId);
                if (!string.IsNullOrEmpty(inferredType))
                {
                    type = inferredType;
                    // 调试日志：标注是从 ID 推断的
                    if (_updateCounter - _lastCardExtractLogFrame > 600)
                    {
                        Log($"[Card提取] {name}: 从ID推断类型={type}, cardId={cardId}");
                    }
                }
                else
                {
                    type = ExtractEnumName(typeObj) ?? "Unknown";
                }
            }
            else
            {
                type = ExtractEnumName(typeObj) ?? "Unknown";
            }

            if (_updateCounter - _lastCardExtractLogFrame > 600)
            {
                // 调试：打印 card 对象的类型和关键属性
                Log($"[Card提取] {name}: Rarity={rarity}, Type={type}");
                Log($"[Card提取] card对象类型: {card.GetType().FullName}");
                // 打印所有包含 "Type" 或 "Kind" 的属性名
                var debugProps = cardType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.Name.Contains("Type") || p.Name.Contains("Kind") || p.Name == "Trait")
                    .Select(p => $"{p.Name}({p.PropertyType.Name})")
                    .ToArray();
                if (debugProps.Length > 0)
                {
                    Log($"[Card提取] 相关属性: {string.Join(", ", debugProps)}");
                }
                else
                {
                    // 如果没有相关属性，打印所有属性名作为参考
                    var allProps = cardType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        .Take(15)
                        .Select(p => p.Name)
                        .ToArray();
                    Log($"[Card提取] card对象属性(前15): {string.Join(", ", allProps)}");
                }
                _lastCardExtractLogFrame = _updateCounter;
            }
            isUpgraded = GetPropertyBool(card, "IsUpgraded") ?? false;

            // 从卡牌类名提取英文 API ID（如 Anger -> ANGER）
            var englishId = ExtractEnglishIdFromType(cardType);

            return new RewardCardInfo
            {
                CardId = cardId ?? name ?? "Unknown",
                EnglishId = englishId ?? "",
                Name = name ?? cardId ?? "Unknown",
                Cost = cost,
                Rarity = rarity,
                Type = type,
                IsUpgraded = isUpgraded
            };
        }
        catch (Exception ex)
        {
            LogError("[Card提取] 异常", ex);
        }
        return null;
    }

    private RewardCardInfo? ExtractCardFromNodeOrHolder(Godot.Node? node, out float? screenX, out float? screenY)
    {
        screenX = null;
        screenY = null;

        if (node != null)
        {
            try
            {
                if (node is Godot.Control ctrl)
                {
                    var pos = ctrl.GetGlobalPosition();
                    var rect = ctrl.GetGlobalRect();
                    screenX = pos.X + rect.Size.X / 2;
                    screenY = pos.Y;
                }
            }
            catch { }
        }

        object? nodeOrHolder = node;
        try
        {
            var type = nodeOrHolder?.GetType();

            // 方法1: 直接检查是否是 CardModel 类型
            if (type?.FullName?.Contains("CardModel") == true)
            {
                return ExtractCardFromNode(nodeOrHolder);
            }

            // 方法2: 检查 Card / CardNode / Model 属性
            var cardProp = type?.GetProperty("Card", BindingFlags.Public | BindingFlags.Instance);
            if (cardProp != null)
            {
                var card = cardProp.GetValue(nodeOrHolder);
                if (card != null) return ExtractCardFromNode(card);
            }

            var cardNodeProp = type?.GetProperty("CardNode", BindingFlags.Public | BindingFlags.Instance);
            if (cardNodeProp != null)
            {
                var cardNode = cardNodeProp.GetValue(nodeOrHolder);
                if (cardNode != null)
                {
                    var modelProp = cardNode.GetType().GetProperty("Model", BindingFlags.Public | BindingFlags.Instance);
                    if (modelProp != null)
                    {
                        var model = modelProp.GetValue(cardNode);
                        if (model != null) return ExtractCardFromNode(model);
                    }
                    return ExtractCardFromNode(cardNode);
                }
            }

            var modelProp2 = type?.GetProperty("Model", BindingFlags.Public | BindingFlags.Instance);
            if (modelProp2 != null)
            {
                var model = modelProp2.GetValue(nodeOrHolder);
                if (model != null) return ExtractCardFromNode(model);
            }

            // 方法3: 搜索内部字段
            foreach (var field in type?.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) ?? Array.Empty<FieldInfo>())
            {
                if (field.FieldType.FullName?.Contains("CardModel") == true)
                {
                    var card = field.GetValue(nodeOrHolder);
                    if (card != null) return ExtractCardFromNode(card);
                }
            }

            if (nodeOrHolder != null)
                Log($"[OrHolder提取] 无法从 {type?.FullName} 提取卡牌");
        }
        catch (Exception ex)
        {
            LogError("[OrHolder提取] 异常", ex);
        }
        return null;
    }

    // 兼容重载：当无法获取节点时调用
    private RewardCardInfo? ExtractCardFromNodeOrHolder(object? nodeOrHolder, out float? screenX, out float? screenY)
    {
        screenX = null;
        screenY = null;
        return ExtractCardFromNodeOrHolder(nodeOrHolder as Godot.Node, out screenX, out screenY);
    }

    private RewardCardInfo? ExtractCardFromOption(object option)
    {
        // CardRewardAlternative: 奖励界面的替代选项（跳过、重抽等）
        try
        {
            var optionType = option.GetType();
            // AlternativeType 属性可以判断是否是卡牌选项
            var altType = GetProperty<object>(option, "AlternativeType");
            if (altType != null && altType.ToString() != "Card")
                return null; // 非卡牌选项跳过

            // Card 属性
            var card = GetProperty<object>(option, "Card");
            if (card != null) return ExtractCardFromNode(card);
        }
        catch { }
        return null;
    }

    private List<RewardCardInfo> ExtractCardInfo(object cardReward)
    {
        var result = new List<RewardCardInfo>();
        try
        {
            var cards = GetProperty<object>(cardReward, "Cards");
            if (cards == null) return result;

            foreach (var card in (System.Collections.IEnumerable)cards)
            {
                if (card == null) continue;
                var cardInfo = ExtractCardFromNode(card);
                if (cardInfo != null) result.Add(cardInfo);
            }
        }
        catch (Exception ex)
        {
            LogError("[CardInfo提取] 异常", ex);
        }
        return result;
    }

    private string DetectRewardSource(object cardReward)
    {
        try
        {
            var options = GetProperty<object>(cardReward, "Options");
            if (options != null)
            {
                var source = GetProperty<object>(options, "CreationSource");
                return source?.ToString() ?? "Unknown";
            }
        }
        catch { }
        return "Unknown";
    }

    private void DumpSceneStructure(Godot.Node node, int depth, int maxDepth)
    {
        if (depth > maxDepth) return;
        try
        {
            var indent = new string(' ', depth * 2);
            var nodeName = node.Name;
            var typeName = node.GetType().FullName ?? node.GetType().Name;
            Log($"[场景结构] {indent}{nodeName} [{typeName}]");

            foreach (Godot.Node child in node.GetChildren())
            {
                DumpSceneStructure(child, depth + 1, maxDepth);
            }
        }
        catch { }
    }

    private Type? GetCachedType(string fullName)
    {
        if (!_typeCache.TryGetValue(fullName, out Type? cachedType))
        {
            cachedType = _gameAssembly?.GetType(fullName);
            _typeCache[fullName] = cachedType;
        }
        return cachedType;
    }

    private object? GetProperty(object? obj, string name)
    {
        if (obj == null) return null;
        return obj.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(obj);
    }

    private T? GetProperty<T>(object? obj, string name) where T : class
    {
        var v = GetProperty(obj, name);
        return v as T;
    }

    private int? GetPropertyInt(object? obj, string name)
    {
        if (obj == null) return null;
        var v = obj.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(obj);
        if (v is int i) return i;
        return null;
    }

    private bool? GetPropertyBool(object? obj, string name)
    {
        if (obj == null) return null;
        var v = obj.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(obj);
        if (v is bool b) return b;
        return null;
    }

    // 从卡牌类的 FullName 提取英文 API ID（如 "MegaCrit.Sts2.Core.Models.Cards.Anger" -> "ANGER"）
    private string? ExtractEnglishIdFromType(Type cardType)
    {
        try
        {
            var fullName = cardType.FullName ?? "";
            // 查找 "Cards." 之后的类名部分
            var idx = fullName.LastIndexOf("Cards.");
            if (idx < 0) return null;
            var className = fullName[(idx + 6)..];
            // 移除可能的嵌套类后缀（如 "$Card`1" 或其他）
            idx = className.IndexOf('$');
            if (idx >= 0) className = className[..idx];
            return className.ToUpperInvariant();
        }
        catch
        {
            return null;
        }
    }

    private string? ExtractEnumName(object? enumObj)
    {
        if (enumObj == null) return null;
        var type = enumObj.GetType();
        // 尝试 _value 字段 (Godot 枚举格式)
        var valueField = type.GetField("_value", BindingFlags.NonPublic | BindingFlags.Instance);
        if (valueField != null)
        {
            var value = valueField.GetValue(enumObj);
            if (value != null)
            {
                // 尝试获取枚举名称
                try
                {
                    var enumType = value.GetType();
                    if (enumType.IsEnum)
                    {
                        return Enum.GetName(enumType, value);
                    }
                }
                catch { }
                return value.ToString();
            }
        }
        // 尝试 _name 字段
        var nameField = type.GetField("_name", BindingFlags.NonPublic | BindingFlags.Instance);
        if (nameField != null)
        {
            return nameField.GetValue(enumObj) as string;
        }
        // 尝试 name 属性
        var nameProp = type.GetProperty("Name", BindingFlags.Public | BindingFlags.Instance);
        if (nameProp != null)
        {
            return nameProp.GetValue(enumObj) as string;
        }
        // 直接返回 ToString
        var str = enumObj.ToString();
        return string.IsNullOrEmpty(str) ? null : str;
    }

    // 扫描对象的属性，查找类型名包含 "Enum" 的枚举属性
    private object? FindEnumProperty(object obj, string[] preferredNames)
    {
        try
        {
            var objType = obj.GetType();

            // 先尝试优先列表中的属性
            foreach (var name in preferredNames)
            {
                var prop = objType.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                if (prop != null)
                {
                    var val = prop.GetValue(obj);
                    if (val != null)
                    {
                        var valType = val.GetType();
                        // 检查是否是枚举类型（Godot枚举或标准枚举）
                        if (valType.IsEnum || valType.FullName?.Contains("Enum") == true)
                        {
                            return val;
                        }
                    }
                }
            }

            // 扫描所有属性，找任何枚举类型的值
            foreach (var prop in objType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (prop.PropertyType.IsEnum || prop.PropertyType.FullName?.Contains("Enum") == true)
                {
                    var val = prop.GetValue(obj);
                    if (val != null) return val;
                }
            }
        }
        catch { }
        return null;
    }

    // 根据卡牌ID推断卡牌类型
    // STS2的卡牌ID通常遵循命名规范，可以根据关键字推断类型
    private string? InferCardTypeFromId(string? cardId)
    {
        if (string.IsNullOrEmpty(cardId)) return null;

        var id = cardId.ToLowerInvariant();

        // 能力/Buff 类型 - 通常是持续性效果
        if (id.Contains("buff") || id.Contains("power") || id.Contains("aura") ||
            id.Contains("stance") || id.Contains("mode") || id.Contains("embrace") ||
            id.Contains("spirit") || id.Contains("form") || id.Contains("echo") ||
            id.Contains("echo") || id.Contains("wrath") || id.Contains("calm") ||
            id.Contains("divinity") || id.Contains("mantra"))
        {
            return "Power";
        }

        // 技能类型 - 非攻击的主动效果
        if (id.Contains("defend") || id.Contains("block") || id.Contains("shield") ||
            id.Contains("skill") || id.Contains("dodge") || id.Contains("evade") ||
            id.Contains("barricade") || id.Contains("armor") || id.Contains("fortify") ||
            id.Contains("brace") || id.Contains("preparation") || id.Contains("tactics") ||
            id.Contains("quick") || id.Contains(" reflexes") || id.Contains("whip"))
        {
            return "Skill";
        }

        // 攻击类型 - 造成伤害
        if (id.Contains("attack") || id.Contains("strike") || id.Contains("slash") ||
            id.Contains("hit") || id.Contains("bash") || id.Contains("punch") ||
            id.Contains("kick") || id.Contains("claw") || id.Contains("bite") ||
            id.Contains("shoot") || id.Contains("dart") || id.Contains("arrow") ||
            id.Contains("shot") || id.Contains("blast") || id.Contains("bomb") ||
            id.Contains("flame") || id.Contains("fire") || id.Contains("spark") ||
            id.Contains("shank") || id.Contains("gor") || id.Contains("sword") ||
            id.Contains("spear") || id.Contains("axe") || id.Contains("blade"))
        {
            return "Attack";
        }

        // 诅咒/状态牌
        if (id.Contains("curse") || id.Contains("status") || id.Contains("wound") ||
            id.Contains("doubt") || id.Contains("pain") || id.Contains("dreg") ||
            id.Contains("clumsy") || id.Contains("decay") || id.Contains("injury"))
        {
            return "Curse";
        }

        return null;
    }

    private bool HasChanged(CardRewardInfo a, CardRewardInfo b)
    {
        if (a.IsVisible != b.IsVisible) return true;
        // 比较 Cards.Count 而不是 CardCount，因为 CardCount 可能没有被及时更新
        if (a.Cards.Count != b.Cards.Count) return true;
        for (int i = 0; i < Math.Min(a.Cards.Count, b.Cards.Count); i++)
        {
            if (a.Cards[i].CardId != b.Cards[i].CardId) return true;
        }
        return false;
    }

    private CardRewardInfo Clone(CardRewardInfo info)
    {
        return new CardRewardInfo
        {
            IsVisible = info.IsVisible,
            CanReroll = info.CanReroll,
            CanSkip = info.CanSkip,
            CardCount = info.CardCount,
            RewardSource = info.RewardSource,
            Cards = info.Cards.Select(c => new RewardCardInfo
            {
                CardId = c.CardId,
                EnglishId = c.EnglishId,
                Name = c.Name,
                Cost = c.Cost,
                Rarity = c.Rarity,
                Type = c.Type,
                IsUpgraded = c.IsUpgraded,
                PickRate = c.PickRate,
                WinRateDelta = c.WinRateDelta,
                SkadaScore = c.SkadaScore,
                HoldStrength = c.HoldStrength,
                Rank = c.Rank,
                DisplayNameZh = c.DisplayNameZh,
                ScreenX = c.ScreenX,
                ScreenY = c.ScreenY
            }).ToList()
        };
    }

    private void Log(string message)
    {
        lock (_logLock)
        {
            try
            {
                var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [CardRewardService] {message}";
                File.AppendAllText(_logFilePath, logEntry + System.Environment.NewLine);
            }
            catch { }
        }
    }

    private void LogError(string message, Exception? ex = null)
    {
        var fullMsg = ex != null ? $"{message}: {ex.Message}" : message;
        Log($"[ERROR] {fullMsg}");
    }
}
