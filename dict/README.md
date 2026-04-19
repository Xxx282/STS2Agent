# 目录

本文档按模块拆分存储，各文件说明如下：

| 文件 | 内容 |
|---|---|---|
| [core.md](./model/core.md) | 核心基础设施（AbstractModel、ModelDb、IRunState） |
| [card.md](./model/card.md) | 卡牌系统（CardModel、CardEnergyCost、CardPile、运行时反射提取） |
| [creature.md](./model/creature.md) | 生物实体（Creature、DamageResult、MonsterModel、CharacterModel） |
| [combat.md](./model/combat.md) | 战斗系统（CombatState、CombatManager、CombatHistory） |
| [power.md](./model/power.md) | 能力值系统（PowerModel、PowerType、PowerStackType） |
| [orb.md](./model/orb.md) | 充能球系统（OrbModel、OrbQueue） |
| [relic.md](./model/relic.md) | 遗物系统（RelicModel、RelicRarity、RelicStatus） |
| [potion.md](./model/potion.md) | 药水系统（PotionModel、PotionRarity、PotionUsage） |
| [event.md](./model/event.md) | 事件系统（EventModel） |
| [encounter.md](./model/encounter.md) | 遭遇战系统（EncounterModel） |
| [run.md](./model/run.md) | 跑团状态（RunState、CombatRoom） |
| [gameaction.md](./model/gameaction.md) | 游戏动作系统（GameAction、PlayCardAction、PlayerChoiceResult） |
| [hook.md](./hook.md) | 钩子系统（Hook 完整列表） |
| [modding.md](./model/modding.md) | Mod 接口（ModManager、Mod、ModManifest、ModInitializerAttribute） |
| [reward.md](./model/reward.md) | 奖励系统（Reward 基类、卡牌奖励界面提取流程） |
| [enum.md](./model/enum.md) | 关键枚举速查（含 Godot 枚举反射提取方法） |
| [flow.md](./model/flow.md) | 数据流向总览（战斗流程、奖励流程、跑团流程） |
| [multiplayer.md](./model/multiplayer.md) | 多人游戏系统（网络同步、动作队列、玩家选择、连接管理） |
| [settings.md](./model/settings.md) | 游戏设置系统（SettingsSave、ModSettings、设置界面节点） |
| [tech.md](./tech.md) | 技术架构（引擎、项目结构、依赖库、Mod 系统、调试工具） |
