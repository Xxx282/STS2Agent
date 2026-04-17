# Encounter 遭遇战模型

---

## 1. EncounterModel —— 遭遇战基础模型

**文件**: `src/Core/Models/EncounterModel.cs`

| 字段/属性 | 类型 | 说明 |
|---|---|---|
| `RoomType` | `RoomType` | 房间类型 |
| `IsWeak` | `bool` | 是否是虚弱难度 |
| `ShouldGiveRewards` | `bool` | 是否给予奖励 |
| `MinGoldReward` / `MaxGoldReward` | `int` | 金币奖励范围 |
| `Tags` | `IEnumerable<EncounterTag>` | 遭遇战标签 |
| `MonstersWithSlots` | `IReadOnlyList<(MonsterModel, string?)>` | 怪物及槽位 |

| 关键方法 | 说明 |
|---|---|
| `GenerateMonsters()` | 生成怪物（抽象方法） |
| `CreateBackground(act, rng)` | 创建战斗背景 |
