# Relic 遗物模型

---

## 1. RelicModel —— 遗物模型基类

**文件**: `src/Core/Models/RelicModel.cs`

| 字段/属性 | 类型 | 说明 |
|---|---|---|
| `Title` / `Description` / `Flavor` | `LocString` | 标题/描述/风味文本 |
| `Rarity` | `RelicRarity` | 稀有度 |
| `Pool` | `RelicPoolModel` | 所属遗物池 |
| `Owner` | `Player` | 持有者 |
| `IsUsedUp` | `bool` | 是否已消耗 |
| `IsWax` | `bool` | 是否是蜡制版本 |
| `IsMelted` | `bool` | 是否已融化 |
| `StackCount` | `int` | 堆叠数量 |
| `DynamicVars` | `DynamicVarSet` | 动态变量 |
| `Status` | `RelicStatus` | 状态（Normal/Active/Disabled） |
| `ShowCounter` | `bool` | 是否显示计数器 |
| `DisplayAmount` | `int` | 显示数量 |

| 关键虚拟属性 | 默认值 |
|---|---|
| `IsUsedUp` | `false` |
| `HasUponPickupEffect` | `false` |
| `SpawnsPets` | `false` |
| `IsStackable` | `false` |
| `AddsPet` | `false` |
| `ShowCounter` | `false` |

| 关键方法 | 说明 |
|---|---|
| `AfterObtained()` | 获得后的行为 |
| `AfterRemoved()` | 移除后的行为 |
| `ModifyDamageAdditive() / ModifyDamageMultiplicative()` | 修改伤害 |
| `Flash(targets)` | 触发闪光效果 |

---

## 2. RelicRarity —— 遗物稀有度

```csharp
None, Starter, Common, Uncommon, Rare, Shop, Ancient, Event
```

---

## 3. RelicStatus —— 遗物状态

```csharp
Normal, Active, Disabled
```
