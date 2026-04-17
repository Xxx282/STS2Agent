# Event 事件模型

---

## 1. EventModel —— 事件基础模型

**文件**: `src/Core/Models/EventModel.cs`

| 字段/属性 | 类型 | 说明 |
|---|---|---|
| `Owner` | `Player?` | 触发事件的玩家 |
| `Description` | `LocString?` | 当前页面描述 |
| `CurrentOptions` | `IReadOnlyList<EventOption>` | 当前可选选项 |
| `IsFinished` | `bool` | 事件是否结束 |
| `DynamicVars` | `DynamicVarSet` | 动态变量 |
| `Rng` | `Rng` | 事件专用随机数生成器 |
| `LayoutType` | `EventLayoutType` | 布局类型 |

| 关键方法 | 说明 |
|---|---|
| `BeginEvent(player, isPreFinished)` | 开始事件 |
| `GenerateInitialOptions()` | 生成初始选项（抽象方法） |
| `SetEventState(description, options)` | 设置事件状态 |
