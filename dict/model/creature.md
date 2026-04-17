# Creature 生物实体

---

## 1. Creature —— 生物实体核心类

**文件**: `src/Core/Entities/Creatures/Creature.cs`

| 字段/属性 | 类型 | 说明 |
|---|---|---|
| `Block` | `int` | 当前格挡值 |
| `CurrentHp` | `int` | 当前生命值 |
| `MaxHp` | `int` | 最大生命值 |
| `Monster` | `MonsterModel?` | 如果是怪物，对应的模型 |
| `Player` | `Player?` | 如果是玩家，对应的玩家 |
| `Side` | `CombatSide` | 阵营（Player/Enemy） |
| `CombatState` | `CombatState?` | 所在战斗状态 |
| `Powers` | `IReadOnlyList<PowerModel>` | 拥有所有能力值 |
| `IsMonster` | `bool` | 是否为怪物 |
| `IsPlayer` | `bool` | 是否为玩家 |
| `IsAlive` | `bool` | 是否存活 |
| `IsPet` | `bool` | 是否为宠物 |
| `CombatId` | `uint?` | 战斗唯一ID |

| 关键方法 | 说明 |
|---|---|
| `GainBlockInternal(amount)` | 获得格挡 |
| `LoseBlockInternal(amount)` | 失去格挡 |
| `LoseHpInternal(amount, props)` | 受到伤害 |
| `HealInternal(amount)` | 治疗 |
| `HasPower<T>()` | 是否有某能力值 |
| `GetPower<T>()` | 获取能力值 |
| `GetPowerAmount<T>()` | 获取能力值数量 |
| `ApplyPowerInternal(power)` | 应用能力值 |
| `RemovePowerInternal(power)` | 移除能力值 |
| `TakeTurn()` | 执行回合（仅敌人） |
| `StunInternal(...)` | 眩晕 |

---

## 2. DamageResult —— 伤害结果

**文件**: `src/Core/Entities/Creatures/DamageResult.cs`

| 字段/属性 | 类型 | 说明 |
|---|---|---|
| `Receiver` | `Creature` | 承受伤害的生物 |
| `Props` | `ValueProp` | 伤害属性 |
| `BlockedDamage` | `int` | 被格挡的伤害 |
| `UnblockedDamage` | `int` | 未格挡的伤害 |
| `OverkillDamage` | `int` | 溢出伤害 |
| `TotalDamage` | `int` | 总伤害 |
| `WasBlockBroken` | `bool` | 是否破盾 |
| `WasFullyBlocked` | `bool` | 是否完全格挡 |
| `WasTargetKilled` | `bool` | 是否击杀目标 |

---

## 3. MonsterModel —— 怪物模型

**文件**: `src/Core/Models/MonsterModel.cs`

| 字段/属性 | 类型 | 说明 |
|---|---|---|
| `Title` | `LocString` | 怪物名称 |
| `MinInitialHp` / `MaxInitialHp` | `int` | 初始血量范围 |
| `Creature` | `Creature?` | 关联的Creature实体 |
| `MoveStateMachine` | `MonsterMoveStateMachine?` | 行动状态机 |
| `NextMove` | `MoveState` | 下一个行动 |
| `IntendsToAttack` | `bool` | 是否意图攻击 |
| `Rng` | `Rng` | 随机数生成器 |

| 关键方法 | 说明 |
|---|---|
| `RollMove(targets)` | 根据目标掷骰决定行动 |
| `PerformMove()` | 执行怪物行动 |
| `SetUpForCombat()` | 战斗前设置 |

---

## 4. CharacterModel —— 角色模型

**文件**: `src/Core/Models/CharacterModel.cs`

| 字段/属性 | 类型 | 说明 |
|---|---|---|
| `StartingHp` | `int` | 起始HP |
| `StartingGold` | `int` | 起始金币 |
| `MaxEnergy` | `int` | 最大能量（默认3） |
| `BaseOrbSlotCount` | `int` | 充能球槽位数（默认0） |
| `CardPool` | `CardPoolModel` | 卡池 |
| `StartingDeck` | `IEnumerable<CardModel>` | 起始牌组 |
| `StartingRelics` | `IReadOnlyList<RelicModel>` | 起始遗物 |
| `StartingPotions` | `IReadOnlyList<PotionModel>` | 起始药水 |
