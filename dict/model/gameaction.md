# GameAction 游戏动作

---

## 1. GameAction —— 游戏动作基类

**文件**: `src/Core/GameActions/GameAction.cs`

所有游戏动作的抽象基类。

| 字段/属性 | 类型 | 说明 |
|---|---|---|
| `OwnerId` | `ulong` (abstract) | 动作所属玩家的网络ID |
| `ActionType` | `GameActionType` (abstract) | 动作类型 |
| `State` | `GameActionState` | 当前状态 |
| `Id` | `uint?` | 动作唯一ID |
| `CompletionTask` | `Task` | 完成任务 |

| 关键方法 | 说明 |
|---|---|
| `Execute()` | 执行动作 |
| `PauseForPlayerChoice()` | 暂停等待玩家选择 |
| `ResumeAfterGatheringPlayerChoice()` | 收集选择后恢复 |
| `Cancel()` | 取消动作 |

---

## 2. PlayCardAction —— 出牌动作

**文件**: `src/Core/GameActions/PlayCardAction.cs`

| 字段/属性 | 类型 | 说明 |
|---|---|---|
| `Player` | `Player` | 出牌玩家 |
| `CardModelId` | `ModelId` | 卡牌模型ID |
| `TargetId` | `uint?` | 目标生物ID |
| `NetCombatCard` | `NetCombatCard` | 网络传输用的卡牌数据 |
| `PlayerChoiceContext` | `PlayerChoiceContext?` | 玩家选择上下文 |

---

## 3. PlayerChoiceResult —— 玩家选择结果

**文件**: `src/Core/GameActions/PlayerChoiceResult.cs`

| 工厂方法 | 说明 |
|---|---|
| `FromCanonicalCard(card)` | 从规范卡牌创建 |
| `FromMutableCombatCard(card)` | 从战斗卡牌创建 |
| `FromMutableDeckCard(card)` | 从牌组卡牌创建 |
| `FromMutableCards(cards)` | 从多个可变卡牌创建 |
| `FromPlayerId(playerId)` | 从玩家ID创建 |
| `FromIndex(index)` / `FromIndexes(indexes)` | 从索引创建 |
