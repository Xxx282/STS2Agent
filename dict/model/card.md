# Card 卡牌模型

---

## 1. CardModel —— 卡牌基础模型

**文件**: `src/Core/Models/CardModel.cs`

继承自 `AbstractModel`，是所有卡牌的抽象基类。

| 字段/属性 | 类型 | 说明 |
|---|---|---|
| `Id` | `ModelId` | 卡牌唯一标识 |
| `Title` | `LocString` | 卡牌标题（需提取 `.Key` 获取本地化 Key） |
| `Description` | `LocString` | 卡牌描述文本 |
| `Type` | `CardType` | 卡牌类型（枚举） |
| `Rarity` | `CardRarity` | 稀有度（枚举） |
| `Pool` | `CardPoolModel` | 所属卡池 |
| `Owner` | `Player` | 当前持有该牌的玩家 |
| `Pile` | `CardPile?` | 卡片当前所在的牌堆 |
| `EnergyCost` | `CardEnergyCost` | 能量费用（支持临时修改） |
| `BaseStarCost` | `int` | 基础星币费用 |
| `CurrentStarCost` | `int` | 当前星币费用 |
| `TemporaryStarCosts` | `List<TemporaryCardCost>` | 临时星币费用 |
| `Keywords` | `IReadOnlySet<CardKeyword>` | 卡牌关键词 |
| `Tags` | `IReadOnlySet<CardTag>` | 标签 |
| `DynamicVars` | `DynamicVarSet` | 动态变量（描述中的数值替换） |
| `Enchantment` | `EnchantmentModel?` | 附魔效果 |
| `Affliction` | `AfflictionModel?` | 诅咒效果 |
| `CurrentUpgradeLevel` | `int` | 当前升级等级 |
| `MaxUpgradeLevel` | `int` | 最大可升级次数 |
| `IsUpgraded` | `bool` | 是否已升级 |
| `CloneOf` | `CardModel?` | 克隆来源 |
| `IsDupe` | `bool` | 是否是复制品 |
| `ExhaustOnNextPlay` | `bool` | 下次使用时消耗 |
| `FloorAddedToDeck` | `int?` | 添加到牌组的层数 |
| `CardScope` | `ICardScope?` | 提供上下文（战斗/跑团状态） |

| 关键方法 | 说明 |
|---|---|
| `CanPlay()` | 检查卡牌是否可以打出 |
| `IsValidTarget(target)` | 验证目标是否合法 |
| `SpendResources()` | 消耗能量和星币 |
| `OnPlayWrapper(...)` | 打出卡牌的完整流程 |
| `CreateClone()` | 创建克隆卡牌 |
| `CreateDupe()` | 创建复制品 |
| `UpgradeInternal()` / `DowngradeInternal()` | 升级/降级卡牌 |

---

## 1.1 运行时卡牌对象类型（反射发现）

游戏中每个卡牌不是统一的 `CardModel` 类型，而是每个卡牌有独立的类型（如 `Melancholy`、`Heavy` 等），继承自基类 `CardModel`。

**卡牌对象类型示例（反射验证）：**

| 中文名 | 英文类名 | 命名空间 |
|---|---|---|
| 忧郁 | `Melancholy` | `MegaCrit.Sts2.Core.Models.Cards.Melancholy` |
| 重压 | `Heavy` | `MegaCrit.Sts2.Core.Models.Cards.Heavy` |
| 预借时间 | `PrestigeTime` | `MegaCrit.Sts2.Core.Models.Cards.PrestigeTime` |

**已验证的关键属性（通过反射验证）：**

| 属性名 | 类型 | 说明 |
|---|---|---|
| `Type` | `CardType` (枚举) | 卡牌类型：Attack / Skill / Power / Curse |
| `TargetType` | `TargetType` (枚举) | 目标类型 |
| `OrbEvokeType` | `OrbEvokeType` (枚举) | 充能球触发类型 |
| `UpgradePreviewType` | `CardUpgradePreviewType` (枚举) | 升级预览类型 |
| `Rarity` | `CardRarity` (枚举) | 稀有度 |
| `Title` | `LocString` | 本地化标题（需提取 `.Key` 属性获取本地化 Key） |
| `Cost` | `CardEnergyCost` | 能量费用（需提取 `.Canonical` 获取费用值） |
| `Id` | `string` | 卡牌 ID（类名，如 "Heavy"） |
| `IsUpgraded` | `bool` | 是否已升级 |

**从卡牌对象提取数据的反射调用示例：**

```csharp
// 获取 Title 本地化 Key
var title = GetProperty(card, "Title");
var titleKey = title.GetType().GetProperty("Key",
    BindingFlags.Public | BindingFlags.Instance)?.GetValue(title) as string;

// 获取能量费用
var costObj = GetProperty(card, "Cost");
var cost = costObj.GetType().GetProperty("Canonical",
    BindingFlags.Public | BindingFlags.Instance)?.GetValue(costObj) as int? ?? 0;

// 获取卡牌类型
var typeObj = GetProperty(card, "Type");
var cardType = ExtractEnumName(typeObj); // 返回 "Skill", "Attack" 等
```

---

## 1.2 Godot 枚举提取方法

Godot 枚举使用特殊的包装类型，不能直接用 `ToString()` 获取名称。

**正确的提取步骤：**

```csharp
private string? ExtractEnumName(object? enumObj)
{
    if (enumObj == null) return null;

    // 1. 尝试 _value 字段（Godot 枚举标准格式）
    var valueField = enumObj.GetType().GetField("_value",
        BindingFlags.NonPublic | BindingFlags.Instance);
    if (valueField != null)
    {
        var value = valueField.GetValue(enumObj);
        if (value != null)
        {
            // 尝试从枚举值获取名称
            if (value.GetType().IsEnum)
                return Enum.GetName(value.GetType(), value);
            return value.ToString();
        }
    }

    // 2. 尝试 _name 字段
    var nameField = enumObj.GetType().GetField("_name",
        BindingFlags.NonPublic | BindingFlags.Instance);
    if (nameField != null)
        return nameField.GetValue(enumObj) as string;

    // 3. 尝试 Name 属性
    var nameProp = enumObj.GetType().GetProperty("Name",
        BindingFlags.Public | BindingFlags.Instance);
    if (nameProp != null)
        return nameProp.GetValue(enumObj) as string;

    // 4. 直接 ToString
    return enumObj.ToString();
}
```

---

## 2. CardEnergyCost —— 卡牌能量消耗

**文件**: `src/Core/Entities/Cards/CardEnergyCost.cs`

| 字段/属性 | 类型 | 说明 |
|---|---|---|
| `Canonical` | `int` | 基础能量消耗 |
| `CostsX` | `bool` | 是否为 X 费卡牌 |
| `CapturedXValue` | `int` | X 值 |

| 临时费用方法 | 说明 |
|---|---|
| `SetUntilPlayed(cost)` | 直到打出时有效 |
| `SetThisTurn(cost)` | 本回合有效 |
| `SetThisCombat(cost)` | 本场战斗有效 |
| `AddUntilPlayed(amount)` | 相对增加，直到打出 |

---

## 3. CardPile —— 卡牌堆

**文件**: `src/Core/Entities/Cards/CardPile.cs`

| 字段/属性 | 类型 | 说明 |
|---|---|---|
| `Type` | `PileType` | 堆类型 |
| `Cards` | `IReadOnlyList<CardModel>` | 堆中的卡牌列表 |
| `IsEmpty` | `bool` | 是否为空 |

| 关键方法 | 说明 |
|---|---|
| `AddInternal(card, index, silent)` | 添加卡牌 |
| `RemoveInternal(card, silent)` | 移除卡牌 |
| `MoveToBottomInternal(card)` | 移到堆底 |
| `MoveToTopInternal(card)` | 移到堆顶 |

---

## 4. PlayerCombatState —— 玩家战斗状态

**文件**: `src/Core/Entities/Players/PlayerCombatState.cs`

| 字段/属性 | 类型 | 说明 |
|---|---|---|
| `Hand` | `CardPile` | 手牌堆 |
| `DrawPile` | `CardPile` | 抽牌堆 |
| `DiscardPile` | `CardPile` | 弃牌堆 |
| `ExhaustPile` | `CardPile` | 消耗堆 |
| `PlayPile` | `CardPile` | 出牌堆 |
| `Energy` | `int` | 当前剩余能量 |
| `MaxEnergy` | `int` | 最大能量 |
| `Stars` | `int` | 当前星星数量 |
| `OrbQueue` | `OrbQueue` | 充能球队列 |

| 关键方法 | 说明 |
|---|---|
| `ResetEnergy()` | 重置能量到最大值 |
| `LoseEnergy(amount)` | 消耗能量 |
| `GainEnergy(amount)` | 获取能量 |
| `HasEnoughResourcesFor(card, out reason)` | 检查是否能打某张卡 |
| `EndOfTurnCleanup()` | 回合结束清理 |
