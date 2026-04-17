# Run 跑团状态

---

## 1. RunState —— 跑团状态

**文件**: `src/Core/Runs/RunState.cs`

| 字段/属性 | 类型 | 说明 |
|---|---|---|
| `Players` | `IReadOnlyList<Player>` | 所有玩家 |
| `Acts` | `IReadOnlyList<ActModel>` | 所有Act模型 |
| `CurrentActIndex` | `int` | 当前Act索引（0/1/2） |
| `Map` | `ActMap` | 当前地图 |
| `CurrentMapCoord` | `MapCoord?` | 当前所在坐标 |
| `CurrentMapPoint` | `MapPoint?` | 当前所在地图节点 |
| `ActFloor` | `int` | 当前Act的层数 |
| `TotalFloor` | `int` | 总层数 |
| `AscensionLevel` | `int` | 进阶等级 |
| `Rng` | `RunRngSet` | 跑团随机数集 |
| `Modifiers` | `IReadOnlyList<ModifierModel>` | 跑团修改器 |
| `IsGameOver` | `bool` | 游戏是否结束 |

---

## 2. CombatRoom —— 战斗房间

**文件**: `src/Core/Rooms/CombatRoom.cs`

| 字段/属性 | 类型 | 说明 |
|---|---|---|
| `Encounter` | `EncounterModel` | 关联的遭遇战 |
| `CombatState` | `CombatState` | 完整的战斗状态 |
| `ExtraRewards` | `IReadOnlyDictionary<Player, List<Reward>>` | 额外奖励 |
