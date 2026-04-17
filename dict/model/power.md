# Power 能力值模型

---

## 1. PowerModel —— 能力值模型基类

**文件**: `src/Core/Models/PowerModel.cs`

| 字段/属性 | 类型 | 说明 |
|---|---|---|
| `Title` / `Description` | `LocString` | 标题/描述 |
| `Type` | `PowerType` | 类型（Buff/Debuff） |
| `StackType` | `PowerStackType` | 堆叠类型（Counter/Single） |
| `Amount` | `int` | 当前层数 |
| `AmountOnTurnStart` | `int` | 回合开始时的层数 |
| `Owner` | `Creature` | 拥有者 |
| `Applier` | `Creature?` | 施加者 |
| `Target` | `Creature?` | 目标 |
| `DynamicVars` | `DynamicVarSet` | 动态变量 |

| 关键方法 | 说明 |
|---|---|
| `ApplyInternal(owner, amount)` | 应用能力 |
| `RemoveInternal()` | 移除能力 |
| `SetAmount(amount)` | 设置层数 |
| `BeforeApplied() / AfterApplied()` | 应用前后钩子 |

---

## 2. PowerType —— 能力值类型

```csharp
None, Buff, Debuff
```

---

## 3. PowerStackType —— 能力值堆叠类型

```csharp
None, Counter,  // 可增减的数值（如 Strength）
Single           // 只存在一个实例（如 Artifact）
```
