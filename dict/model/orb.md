# Orb 充能球模型

---

## 1. OrbModel —— 充能球模型基类

**文件**: `src/Core/Models/OrbModel.cs`

| 字段/属性 | 类型 | 说明 |
|---|---|---|
| `PassiveVal` | `decimal` | 被动触发值 |
| `EvokeVal` | `decimal` | 激发触发值 |
| `Owner` | `Player` | 所属玩家 |
| `Title` / `Description` | `LocString` | 标题/描述 |

| 关键方法 | 说明 |
|---|---|
| `Passive(choiceContext, target)` | 被动触发（回合结束） |
| `Evoke(choiceContext)` | 激发触发（打出充能球卡） |

| 内置充能球 | PassiveVal | EvokeVal |
|---|---|---|
| `LightningOrb` | 3 | 8 |
| `FrostOrb` | 4（格挡） | 6（格挡） |
| `DarkOrb` | 6 | 6×层数 |
| `PlasmaOrb` | 2（能量） | 2（能量） |
| `GlassOrb` | 3 | 5 |

---

## 2. OrbQueue —— 充能球队列

**文件**: `src/Core/Entities/Orbs/OrbQueue.cs`

| 字段/属性 | 类型 | 说明 |
|---|---|---|
| `Orbs` | `IReadOnlyList<OrbModel>` | 当前所有充能球 |
| `Capacity` | `int` | 最大容量（最大10） |

| 关键方法 | 说明 |
|---|---|
| `TryEnqueue(orb)` | 尝试入队 |
| `Remove(orb)` | 移除充能球 |
| `AddCapacity(capacity)` | 增加容量 |
