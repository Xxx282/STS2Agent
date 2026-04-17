# Potion 药水模型

---

## 1. PotionModel —— 药水模型基类

**文件**: `src/Core/Models/PotionModel.cs`

| 字段/属性 | 类型 | 说明 |
|---|---|---|
| `Title` / `Description` | `LocString` | 标题/描述 |
| `Rarity` | `PotionRarity` | 稀有度 |
| `Usage` | `PotionUsage` | 使用方式 |
| `TargetType` | `TargetType` | 目标类型 |
| `Owner` | `Player` | 持有者 |
| `DynamicVars` | `DynamicVarSet` | 动态变量 |
| `IsQueued` | `bool` | 是否正在队列中 |

| 关键方法 | 说明 |
|---|---|
| `OnUseWrapper(choiceContext, target)` | 使用药水的完整流程 |
| `OnUse(choiceContext, target)` | 实际使用逻辑（子类重写） |
| `EnqueueManualUse(target)` | 排队手动使用 |
| `Discard()` | 丢弃药水 |
| `CanBeGeneratedInCombat` | 是否能在战斗中生成 |

---

## 2. PotionRarity —— 药水稀有度

```csharp
None, Common, Uncommon, Rare
```

---

## 3. PotionUsage —— 药水使用方式

```csharp
Manual, Automatic, ManualForCombat, ManualForOutOfCombat, Passive
```
