# 关键枚举速查

---

## 1. CardType —— 卡牌类型

```csharp
None, Attack, Skill, Power, Status, Curse, Quest
```

| 枚举值 | 说明 |
|---|---|
| `None` | 无类型 |
| `Attack` | 攻击牌 |
| `Skill` | 技能牌 |
| `Power` | 能力牌 |
| `Status` | 状态牌 |
| `Curse` | 诅咒牌 |
| `Quest` | 任务牌 |

---

## 2. CardRarity —— 卡牌稀有度

```csharp
None, Basic, Common, Uncommon, Rare, Ancient, Event, Token, Status, Curse, Quest
```

| 枚举值 | 说明 |
|---|---|
| `None` | 无稀有度 |
| `Basic` | 基础 |
| `Common` | 普通 |
| `Uncommon` | 非普通（稀有） |
| `Rare` | 稀有 |
| `Ancient` | 远古 |
| `Event` | 事件 |
| `Token` | 临时牌 |
| `Status` | 状态 |
| `Curse` | 诅咒 |
| `Quest` | 任务 |

---

## 3. CardKeyword —— 卡牌关键词

```csharp
None, Exhaust, Ethereal, Innate, Unplayable, Retain, Sly, Eternal
```

| 枚举值 | 说明 |
|---|---|
| `None` | 无关键词 |
| `Exhaust` | 消耗（打出后进入耗尽堆） |
| `Ethereal` | 虚无（本回合未打出则进入弃牌堆） |
| `Innate` | 天赋（起始手牌必定包含） |
| `Unplayable` | 不可打出 |
| `Retain` | 保留（回合结束不移至弃牌堆） |
| `Sly` | 隐秘（不进入弃牌堆） |
| `Eternal` | 永恒（永不洗入抽牌堆） |

---

## 4. TargetType —— 目标类型

```csharp
None, Self, AnyEnemy, AllEnemies, RandomEnemy,
AnyPlayer, AnyAlly, AllAllies, TargetedNoCreature, Osty
```

| 枚举值 | 说明 |
|---|---|
| `None` | 无目标 |
| `Self` | 自身 |
| `AnyEnemy` | 任意敌人 |
| `AllEnemies` | 所有敌人 |
| `RandomEnemy` | 随机敌人 |
| `AnyPlayer` | 任意玩家 |
| `AnyAlly` | 任意友方 |
| `AllAllies` | 所有友方 |
| `TargetedNoCreature` | 指定非生物目标 |
| `Osty` | Osty（特殊目标类型） |

---

## 5. PileType —— 牌堆类型

```csharp
None, Draw, Hand, Discard, Exhaust, Play, Deck
```

| 枚举值 | 说明 |
|---|---|
| `None` | 无 |
| `Draw` | 抽牌堆 |
| `Hand` | 手牌堆 |
| `Discard` | 弃牌堆 |
| `Exhaust` | 消耗堆 |
| `Play` | 出牌堆 |
| `Deck` | 牌组（整套牌） |

---

## 6. CombatSide —— 战斗阵营

```csharp
None, Player, Enemy
```

---

## 7. RoomType —— 房间类型

```csharp
None, Map, Monster, Elite, Boss, Event, RestSite, Shop,
TreasureRoom, AncientEvent, CardRewardSelection, RewardScreen
```

| 枚举值 | 说明 |
|---|---|
| `None` | 无 |
| `Map` | 地图 |
| `Monster` | 怪物战斗 |
| `Elite` | 精英战斗 |
| `Boss` | Boss 战斗 |
| `Event` | 事件 |
| `RestSite` | 休息点 |
| `Shop` | 商店 |
| `TreasureRoom` | 宝藏房间 |
| `AncientEvent` | 远古事件 |
| `CardRewardSelection` | 卡牌奖励选择 |
| `RewardScreen` | 奖励界面 |

---

## 8. PlayerChoiceType —— 玩家选择类型

```csharp
None, CanonicalCard, CombatCard, DeckCard, MutableCard, Player, Index
```

| 枚举值 | 说明 |
|---|---|
| `None` | 无 |
| `CanonicalCard` | 规范卡牌 |
| `CombatCard` | 战斗卡牌 |
| `DeckCard` | 牌组卡牌 |
| `MutableCard` | 可变卡牌 |
| `Player` | 玩家 |
| `Index` | 索引 |

---

## 9. PlayerChoiceContext —— 玩家选择上下文

在钩子和动作中传递，提供当前操作的上下文信息。

---

## 10. ModelId —— 模型标识符

```csharp
// ModelDb.GetId<T>() 获取某类型的ID
// ModelDb.GetById<T>(id) 通过ID获取模型
```

---

## 11. CardUpgradePreviewType —— 卡牌升级预览类型

通过反射验证存在于运行时卡牌对象上。

---

## 12. OrbEvokeType —— 充能球触发类型

通过反射验证存在于运行时卡牌对象上。

---

## 13. Godot 枚举的反射提取

Godot 枚举使用特殊的包装类型（如 `CardType` 包装在 Godot 类型系统中），**不能**直接用 `ToString()` 获取枚举名称。

必须通过以下步骤：

1. 获取枚举对象的 `_value` 非公开字段
2. 从 `_value` 中提取底层数值
3. 用 `Enum.GetName(enumType, value)` 获取枚举名称字符串

```csharp
// 示例：提取 CardType 枚举值
var typeObj = GetProperty(card, "Type");
var valueField = typeObj.GetType().GetField("_value",
    BindingFlags.NonPublic | BindingFlags.Instance);
var value = valueField.GetValue(typeObj);
var name = Enum.GetName(value.GetType(), value); // 返回 "Skill", "Attack" 等
```

（详细提取代码见 [card.md](./card.md) 第 1.2 节）
