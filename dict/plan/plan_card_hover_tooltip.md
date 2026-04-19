# 卡牌奖励数据页悬停显示方案

## 1. 目标

将卡牌奖励数据页面从"常驻显示所有卡牌"改为"鼠标悬停在卡牌上时，在该卡牌上方显示单张卡数据"。

## 2. 现状分析

### 当前实现

- `CardRewardService`：通过 Godot 节点树扫描提取卡牌奖励界面中的卡牌数据（EnglishId、Rank、PickRate、SkadaScore 等）
- `CardStatsOverlayNode`：常驻面板，位于屏幕右边缘，始终显示全部 3 张卡的数据
- `CardStatsService`：提供 Skada 数据查询

### 游戏 UI 节点结构（来源：dict/model/reward.md 4.1 节）

```
NGame.Instance
  └── NCardRewardSelectionScreen / CardRewardScreen
        └── UI/CardRow
              └── [CardHolder 节点]  ← 游戏内实际卡牌显示节点
                    └── CardModel (Anger, Rampage...)
```

## 3. 架构设计

### 3.1 核心组件

| 组件 | 类型 | 职责 |
|---|---|---|
| `CardHoverService` | 服务 | 扫描游戏 UI 树，检测鼠标悬停在哪个卡牌上 |
| `CardTooltipNode` | UI 节点 | 跟随卡牌位置的悬浮数据框，显示单张卡数据 |
| `CardRewardInfo` | 模型 | 增加 `ScreenX`/`ScreenY` 字段，存储卡牌屏幕坐标 |
| `CardStatsOverlayNode` | UI 节点 | 修改为悬停模式，不再常驻显示 |

### 3.2 数据流

```
CardRewardService.Update()
  └── 扫描 CardHolder 节点
        └── 获取节点 GlobalPosition → CardRewardInfo
              └── CardHoverService 订阅
                    └── 检测悬停变化
                          └── CardTooltipNode.ShowAt(card, x, y) / Hide()
```

### 3.3 悬停检测原理

在 Godot 4 中，无法直接监听游戏内部节点的鼠标事件。因此采用主动检测方案：

1. **每帧检测**：`CardHoverService._Process()` 中调用 `GetGlobalMousePosition()` 获取鼠标屏幕坐标
2. **节点范围检测**：遍历当前可见的 `CardHolder` 节点列表，对每个节点调用 `GetGlobalRect()` 检查鼠标是否在矩形范围内
3. **状态变更通知**：当悬停卡牌发生变化时，通过事件通知 `CardTooltipNode` 更新显示

## 4. 详细实现

### 4.1 修改 CardRewardInfo

在 `RewardCardInfo` 中增加可选坐标字段：

```csharp
public class RewardCardInfo
{
    // ... 现有字段 ...
    public float? ScreenX { get; set; }  // 卡牌中心屏幕 X 坐标
    public float? ScreenY { get; set; }  // 卡牌中心屏幕 Y 坐标
}
```

### 4.2 修改 CardRewardService

在 `ExtractCardsFromChildNodes` 中，获取每个 `CardHolder` 节点的 `GlobalPosition`：

```csharp
// 检测 CardHolder 节点时获取坐标
if (childType.FullName?.Contains("CardHolder") == true)
{
    // 尝试获取节点位置
    float? screenX = null;
    float? screenY = null;
    if (child is Godot.Control ctrl)
    {
        var pos = ctrl.GetGlobalPosition();
        var rect = ctrl.GetGlobalRect();
        screenX = pos.X + rect.Size.X / 2;
        screenY = pos.Y;
    }

    var cardInfo = ExtractCardFromNodeOrHolder(child);
    if (cardInfo != null)
    {
        cardInfo.ScreenX = screenX;
        cardInfo.ScreenY = screenY;
        if (!reward.Cards.Any(c => c.CardId == cardInfo.CardId))
        {
            reward.Cards.Add(cardInfo);
        }
    }
}
```

### 4.3 新建 CardHoverService

```csharp
public partial class CardHoverService
{
    private CardRewardService _cardRewardService;
    private string? _hoveredCardId;
    private int _updateCounter;

    public event Action<RewardCardInfo>? OnCardHovered;
    public event Action? OnCardUnhovered;

    public void _Process(double delta)
    {
        _updateCounter++;
        if (_updateCounter % 3 != 0) return;  // 每3帧检测一次

        var reward = _cardRewardService.GetCurrentReward();
        if (!reward.IsVisible || reward.Cards.Count == 0) return;

        var mousePos = GetViewport().GetMousePosition();

        string? newHovered = null;
        foreach (var card in reward.Cards)
        {
            if (card.ScreenX.HasValue && card.ScreenY.HasValue)
            {
                var cardCenterX = card.ScreenX.Value;
                var cardCenterY = card.ScreenY.Value;
                // 扩大检测区域，卡牌宽度约200px
                if (Mathf.Abs(mousePos.X - cardCenterX) < 120 &&
                    Mathf.Abs(mousePos.Y - cardCenterY) < 150)
                {
                    newHovered = card.CardId;
                    break;
                }
            }
        }

        if (newHovered != _hoveredCardId)
        {
            _hoveredCardId = newHovered;
            if (newHovered != null)
            {
                var card = reward.Cards.First(c => c.CardId == newHovered);
                OnCardHovered?.Invoke(card);
            }
            else
            {
                OnCardUnhovered?.Invoke();
            }
        }
    }
}
```

### 4.4 新建 CardTooltipNode

悬浮提示框设计：

- **位置**：跟随当前悬停卡牌的坐标，显示在卡牌**上方**（`ScreenY - 偏移量`）
- **尺寸**：280×180px（紧凑显示单张卡数据）
- **背景**：半透明深色圆角面板
- **内容**：卡牌名称 + 标签行 + SkadaScore 进度条
- **动画**：淡入淡出（FadeDuration = 0.2s）

```csharp
public partial class CardTooltipNode : Control
{
    // 锚点设为居中顶部，随位置参数更新 OffsetLeft/OffsetRight
    public void ShowAt(RewardCardInfo card, float anchorX, float anchorY)
    {
        // anchorX, anchorY = 悬停卡牌的屏幕中心坐标
        // 显示在卡牌上方
        GlobalPosition = new Vector2(anchorX - TooltipWidth / 2, anchorY - TooltipHeight - 20);
        RefreshContent(card);
        ShowWithFade();
    }
}
```

### 4.5 修改 CardStatsOverlayNode

将当前常驻面板模式改为备选：

- 方案 A（推荐）：**完全移除常驻面板**，仅保留 `CardTooltipNode` 悬浮显示
- 方案 B：保留常驻面板作为"总览模式"，通过快捷键切换显示模式

## 5. 界面布局

```
┌─────────────────────────────────────────────────────────┐
│                    游戏卡牌奖励界面                       │
│    ┌─────────┐    ┌─────────┐    ┌─────────┐           │
│    │  卡牌1  │    │  卡牌2  │    │  卡牌3  │           │
│    └────┬────┘    └────┬────┘    └────┬────┘           │
│         │              │              │                │
│         ▼ 鼠标悬停     │              │                │
│    ┌────────────┐     │              │                │
│    │ 暴走        │     │              │                │
│    │ #99 13.3%  │     │              │                │
│    │ [████░░]    │     │              │                │
│    └────────────┘     │              │                │
│  （悬浮在卡牌1上方）    │              │                │
└─────────────────────────────────────────────────────────┘
```

## 6. 依赖关系

```
dict/model/reward.md    ← 卡牌奖励界面节点结构
dict/service/tech.md    ← Godot 4.5 技术约束
dict/model/flow.md      ← 奖励流程

STS2Agent.cs            ← 注册服务
  ├── CardRewardService ← 提供卡牌数据 + CardHolder 节点扫描
  ├── CardHoverService  ← 悬停检测（新建）
  └── CardTooltipNode   ← 悬浮框 UI（新建）
```

## 7. 风险与备选方案

### 风险 1：CardHolder 节点位置获取失败

游戏版本更新可能导致节点类型名变化，导致无法获取 `GlobalPosition`。

**备选**：使用 `GetViewport().GetMousePosition()` 相对坐标 + 预定义的卡牌位置偏移（基于屏幕分辨率归一化）。

### 风险 2：鼠标检测延迟

`_Process` 每帧检测有 1-3 帧延迟。

**备选**：在 `CardRewardService` 中使用更快的检测频率，或通过 `_Input` 事件监听。

### 风险 3：多屏幕/不同分辨率

悬浮框位置计算需要考虑视口偏移。

**备选**：所有坐标计算使用 `GetViewport().GetVisibleRect()` 归一化。

## 8. 实现顺序

1. 修改 `CardRewardInfo.cs` 增加坐标字段
2. 新建 `CardHoverService.cs`
3. 新建 `CardTooltipNode.cs`
4. 修改 `CardRewardService.cs` 提取 CardHolder 节点坐标
5. 修改 `CardStatsOverlayNode.cs` 移除/精简常驻逻辑
6. 修改 `STS2Agent.cs` 注册新服务和节点
7. 编译验证
8. 实际游戏测试
