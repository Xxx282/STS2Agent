<!--
 * @Author: Mendax
 * @Date: 2026-04-16 16:45:52
 * @LastEditors: Mendax
 * @LastEditTime: 2026-04-17 16:28:22
 * @Description: 
 * @FilePath: \STS2Agent\dict\model\reward.md
-->
# Reward 奖励系统

---

## 1. Reward —— 奖励基类

**文件**: `src/Core/Rewards/Reward.cs`

| 字段/属性 | 类型 | 说明 |
|---|---|---|
| `RewardType` | `RewardType` | 奖励类型 |
| `Player` | `Player` | 持有该奖励的玩家 |
| `IsPopulated` | `bool` | 奖励数据是否已生成 |
| `Description` | `LocString` | 奖励描述 |

| 关键方法 | 说明 |
|---|---|
| `Populate()` | 异步生成奖励内容 |
| `OnSelect()` | 玩家选择时的回调 |
| `OnSkipped()` | 玩家跳过时的回调 |
| `ToSerializable() / FromSerializable()` | 存档序列化 |

---

## 2. 奖励类型

| 类型 | 说明 |
|---|---|
| `CardReward` | 卡牌奖励（默认3选1） |
| `GoldReward` | 金币奖励 |
| `PotionReward` | 药水奖励 |
| `RelicReward` | 遗物奖励 |
| `SpecialCardReward` | 特殊卡牌奖励 |
| `CardRemovalReward` | 卡牌移除奖励 |
| `LinkedRewardSet` | 链接奖励集（子奖励全部选择后才算完成） |

---

## 3. RewardsSet —— 奖励集合

**文件**: `src/Core/Rewards/RewardsSet.cs`

| 房间类型 | 奖励组成 |
|---|---|
| 普通战斗 | 金币 + 药水（概率）+ 卡牌×3 |
| 精英战斗 | 金币（满额）+ 药水 + 卡牌×3 + 遗物 |
| Boss战斗 | 金币（满额）+ 药水 + 卡牌×3 |

---

## 4. CardReward —— 卡牌奖励（运行时数据提取）

游戏中的卡牌奖励界面通过 Godot 节点树扫描实现，以下是经过实际运行验证的提取流程。

### 4.1 界面节点树结构

```
NGame.Instance
  └── NCardRewardSelectionScreen / CardRewardScreen
        └── UI/CardRow
              └── [CardHolder节点]
                    └── CardModel (具体卡牌类，如 Melancholy、Heavy)
```

### 4.2 界面节点类型（按尝试顺序）

| 节点类型 | 命名空间 |
|---|---|
| `NCardRewardSelectionScreen` | `MegaCrit.Sts2.Core.Nodes.Screens.CardSelection` |
| `CardRewardScreen` | `MegaCrit.Sts2.Core.Nodes.Screens.CardSelection` |
| `RewardScreen` | `MegaCrit.Sts2.Core.Nodes.Screens.Reward` |
| `NRewardScreen` | `MegaCrit.Sts2.Core.Nodes.Screens.Reward` |
| `RewardOverlay` | `MegaCrit.Sts2.Core.Nodes.Overlays` |
| `NRewardOverlay` | `MegaCrit.Sts2.Core.Nodes.Overlays` |

### 4.3 卡牌数据提取方式（按尝试顺序）

1. **`_cardHolders` 非公开字段** — 奖励界面的卡牌持有者列表
2. **`UI/CardRow` 节点扫描** — 遍历子节点提取卡牌
3. **`_options` 非公开字段** — 选项列表
4. **`_extraOptions` 非公开字段** — 替代选项（跳过、重抽等）
5. **扫描所有 `IEnumerable` 属性** — 兜底方案

### 4.4 卡牌奖励状态判断

| 判断方式 | 说明 |
|---|---|
| `Visible` 属性 | 检查界面可见性 |
| `Modulate.A` | Alpha 值 > 0.001 表示可见 |
| `IsNodeReady` 属性 | 节点是否已就绪 |

### 4.5 调试日志

- **卡牌提取日志**：`mods/STS2Agent/logs/card_reward.log`
- **心跳日志**（每10秒）：显示 `IsVisible`、`Cards` 数量
- **调试日志**（每600帧/约10秒）：打印卡牌对象的类型和属性结构
- **状态变化日志**：界面出现/消失时记录卡牌列表
