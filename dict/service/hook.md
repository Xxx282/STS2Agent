# Hook 钩子系统

---

## 1. Hook —— 静态钩子类

**文件**: `src/Core/Hooks/Hook.cs`

游戏的核心扩展系统，允许遗物、药水、能力值等在特定事件发生时执行自定义逻辑。

---

## 2. 战斗钩子（按执行时机分类）

| 钩子 | 说明 |
|---|---|
| `BeforeAttack / AfterAttack` | 攻击前后 |
| `BeforeBlockGained / AfterBlockGained` | 格挡获得前后 |
| `AfterBlockBroken / AfterBlockCleared` | 破盾/清盾后 |
| `BeforeCardAutoPlayed` | 自动出牌前 |
| `BeforeCardPlayed / AfterCardPlayed` | 卡牌打出前后 |
| `AfterCardDrawn / AfterCardDiscarded` | 抽牌/弃牌后 |
| `AfterCardExhausted` | 卡牌消耗后 |
| `AfterCardEnteredCombat` | 卡牌进入战斗时 |
| `AfterCardGeneratedForCombat` | 卡牌在战斗中生成 |
| `BeforeCardRemoved` | 卡牌移除前 |
| `BeforeCreatureDeath` | 生物死亡前 |
| `AfterCreatureDied` | 生物死亡后 |
| `BeforeDamageModified / AfterDamageModified` | 伤害修改前后 |
| `BeforeDamageReceived / AfterDamageReceived` | 伤害接收前后 |
| `BeforeDrawCard / AfterDrawCard` | 抽卡前后 |
| `BeforeEnergyGained / AfterEnergyGained` | 能量获得前后 |
| `BeforeOrbEvoked / AfterOrbEvoked` | 充能球激发前后 |
| `BeforeOrbChanneled / AfterOrbChanneled` | 充能球引导前后 |
| `BeforePowerApplied / AfterPowerApplied` | 能力应用前后 |
| `BeforePowerRemoved / AfterPowerRemoved` | 能力移除前后 |
| `AfterRelicObtained / AfterRelicRemoved` | 遗物获得/移除后 |
| `BeforeRewardTaken / AfterRewardTaken` | 奖励获取前后 |
| `BeforeTurnEnd / AfterTurnEnd` | 回合结束前后 |
| `BeforeSideTurnStart / AfterSideTurnStart` | 侧边回合开始前后 |
| `BeforeStarsGained / AfterStarsGained` | 星星获得前后 |
| `BeforeSummoned / AfterSummoned` | 召唤前后 |

---

## 3. 跑团钩子

| 钩子 | 说明 |
|---|---|
| `AfterActEntered` | 进入Act后 |
| `AfterCombatEnded` | 战斗结束后 |
| `BeforeCombatStarted` | 战斗开始前 |
| `AfterMapNodeEntered` | 进入地图节点后 |
| `AfterRestSiteEntered` | 进入休息点后 |
| `AfterShopEntered` | 进入商店后 |
| `ModifyRewards` | 修改奖励 |
