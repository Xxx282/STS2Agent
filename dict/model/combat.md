# Combat 战斗系统

---

## 1. CombatState —— 战斗状态

**文件**: `src/Core/Combat/CombatState.cs`

战斗的**核心数据容器**，管理一场战斗中所有的实体状态。

| 字段/属性 | 类型 | 说明 |
|---|---|---|
| `Allies` | `IReadOnlyList<Creature>` | 玩家方生物列表 |
| `Enemies` | `IReadOnlyList<Creature>` | 敌方生物列表 |
| `Creatures` | `IReadOnlyList<Creature>` | 所有生物 |
| `RoundNumber` | `int` | 当前回合数 |
| `CurrentSide` | `CombatSide` | 当前行动方 |
| `Encounter` | `EncounterModel?` | 遭遇配置 |
| `Modifiers` | `IReadOnlyList<ModifierModel>` | 战斗modifier |

| 关键方法 | 说明 |
|---|---|
| `CreateCard<T>()` | 从 ModelDb 创建卡牌 |
| `AddCard() / RemoveCard()` | 卡牌生命周期管理 |
| `CreateCreature()` | 创建怪物，自动设置HP缩放 |
| `AddCreature() / RemoveCreature()` | 生物增删 |
| `GetCreature()` / `GetCreatureAsync()` | 按 CombatId 查找生物 |
| `GetOpponentsOf() / GetTeammatesOf()` | 获取对手/队友 |
| `IterateHookListeners()` | 遍历所有可触发Hook的实体 |

---

## 2. CombatManager —— 战斗管理器

**文件**: `src/Core/Combat/CombatManager.cs`

单例类，是战斗系统的中枢，负责管理战斗生命周期、回合流转。

| 属性 | 类型 | 说明 |
|---|---|---|
| `Instance` | `static CombatManager` | 单例实例 |
| `IsPaused` | `bool` | 战斗是否暂停 |
| `IsInProgress` | `bool` | 战斗是否进行中 |
| `IsEnemyTurnStarted` | `bool` | 敌方回合是否已开始 |
| `RoundNumber` | `int` | 当前回合数 |
| `History` | `CombatHistory` | 战斗历史记录 |
| `StateTracker` | `CombatStateTracker` | 状态变更追踪器 |

| 关键事件 | 触发时机 |
|---|---|
| `CombatSetUp` | 战斗设置完成 |
| `CombatEnded` | 战斗结束 |
| `CreaturesChanged` | 生物列表变更 |
| `TurnStarted` / `TurnEnded` | 回合开始/结束 |

| 关键方法 | 说明 |
|---|---|
| `StartCombatInternal()` | 开始战斗 |
| `StartTurn()` | 开始回合 |
| `SetupPlayerTurn(player)` | 设置玩家回合（重置能量、抽牌） |
| `ExecuteEnemyTurn()` | 执行敌方回合 |
| `EndPlayerTurnPhaseOneInternal()` | 结束回合阶段1（处理Ethereal） |
| `EndPlayerTurnPhaseTwoInternal()` | 结束回合阶段2（弃置手牌） |
| `CheckWinCondition()` | 判断是否胜利 |
| `LoseCombat()` | 战斗失败 |

---

## 3. CombatHistory —— 战斗历史

**文件**: `src/Core/Combat/History/CombatHistory.cs`

记录所有战斗事件条目，提供 `Changed` 事件供追踪器订阅。

| 关键方法 | 说明 |
|---|---|
| `CardPlayStarted() / CardPlayFinished()` | 记录卡牌开始使用/使用完成 |
| `CardDrawn() / CardDiscarded() / CardExhausted()` | 记录抽牌/弃牌/消耗 |
| `CreatureAttacked()` | 记录生物攻击 |
| `DamageReceived()` | 记录生物受到伤害 |
| `BlockGained()` | 记录获得护甲 |
| `EnergySpent()` | 记录能量消耗 |
| `OrbChanneled()` | 记录引导Orb |
| `PotionUsed()` | 记录药水使用 |
| `PowerReceived()` | 记录获得能力Power |
