# 数据流向总览

---

## 1. 游戏启动

```
游戏启动
  └── ModManager.Initialize()
        ├── 加载 mods/ 目录下的mod
        └── 加载 Steam Workshop 的mod
              └── ModInitializer.Init() 执行
```

---

## 2. 跑团流程

```
跑团开始 (RunState.CreateForNewRun())
  └── 玩家进入地图 (Map)
        ├── 普通房间 -> 战斗 -> 奖励
        ├── 精英房间 -> 战斗 -> 奖励 + 遗物
        ├── Boss房间 -> 战斗 -> 奖励
        ├── 事件房间 -> EventModel 处理选项
        ├── 商店房间 -> 购买遗物/药水/卡牌移除
        └── 休息房间 -> 恢复/升级卡牌
```

---

## 3. 战斗流程

```
战斗流程 (CombatManager)
  └── StartCombatInternal()
        ├── CombatState 创建（生物、卡牌、充能球等）
        ├── Hook.BeforeCombatStarted
        └── StartTurn()
              ├── 玩家回合: SetupPlayerTurn()
              │     ├── 重置能量
              │     ├── 抽手牌
              │     ├── Hook.BeforeSideTurnStart
              │     └── 出牌 (PlayCardAction)
              │           ├── 消耗能量/星币
              │           ├── CardModel.OnPlayWrapper()
              │           ├── Hook.BeforeCardPlayed / AfterCardPlayed
              │           └── 移动到结果堆
              └── 敌方回合: ExecuteEnemyTurn()
                    ├── MonsterModel.RollMove()
                    └── MonsterModel.PerformMove()
                          ├── 产生攻击/技能动作
                          └── Hook.BeforeAttack / AfterAttack
```

---

## 5. 多人游戏流程

### 5.1 连接建立

```
客户端加入 (JoinFlow)
  ├── 版本检查 (version mismatch → 断开)
  ├── Mod 检查 (mod mismatch → 断开)
  ├── ModelDb Hash 检查 (hash mismatch → 断开)
  └── 根据 RunSessionState 加入
        ├── InLobby → AttemptJoin → RunLobby
        ├── InLoadedLobby → AttemptLoadJoin → LoadRunLobby
        └── Running → AttemptRejoin → 恢复跑团
```

### 5.2 动作同步流程

```
玩家发起动作 (Client)
  └── ActionQueueSynchronizer.RequestEnqueue(action)
        └── SendMessage(RequestEnqueueActionMessage)
              └── Host 接收 (HandleRequestEnqueueActionMessage)
                    ├── 验证动作所有者
                    ├── 转换为 GameAction
                    └── EnqueueAction(action, senderId)
                          ├── SendMessage(ActionEnqueuedMessage) → 其他Client
                          └── ActionQueueSet.EnqueueWithoutSynchronizing(action)
                                └── GameAction.OnEnqueued(...)

Host 执行动作
  └── ActionQueueSet.GetReadyAction()
        └── GameAction.Execute()
              └── ActionQueueSynchronizer.setCombatState(...)
                    ├── PlayPhase → UnpauseAllPlayerQueues()
                    ├── EndTurnPhaseOne → StartCancellingAllPlayerDrivenCombatActions()
                    └── NotPlayPhase → PauseAllPlayerQueues()
```

### 5.3 玩家选择同步

```
玩家做出选择 (本地)
  └── PlayerChoiceSynchronizer.SyncLocalChoice(player, choiceId, result)
        └── SendMessage(PlayerChoiceMessage) → 网络

等待远程选择 (异步)
  └── PlayerChoiceSynchronizer.WaitForRemoteChoice(player, choiceId)
        └── 等待 PlayerChoiceMessage 到达
              └── OnReceivePlayerChoice(player, choiceId, result)
```

### 5.4 战斗状态同步

```
Host 广播完整战斗状态
  └── NetFullCombatState.FromRun(runState, justFinishedAction)
        ├── 序列化所有 CreatureState (HP, Block, Powers)
        ├── 序列化所有 PlayerState (Energy, Stars, Gold, Piles, Potions, Relics, Orbs)
        └── 序列化 RNG 和 ChoiceIds

Client 接收并校验
  └── ChecksumTracker 校验
        └── 不一致 → StateDivergenceException → 断开连接
```

### 5.5 多人游戏动作队列

```
ActionQueueSet (每个玩家一个队列)
  ├── ActionQueue (per player)
  │     ├── actions: List<GameAction>
  │     ├── isPaused: bool
  │     ├── isCancellingPlayCardActions: bool
  │     └── isCancellingPlayerDrivenCombatActions: bool
  └── GetReadyAction() → 按 ActionId 排序，取最小者执行
```

---

## 4. 奖励流程

```
奖励流程 (RewardsSet)
  └── Offer()
        ├── 生成奖励 (CardReward / GoldReward / PotionReward / RelicReward)
        ├── Hook.ModifyRewards 修改奖励
        └── 玩家选择
              ├── CardReward -> NCardRewardSelectionScreen
              ├── GoldReward -> PlayerCmd.GainGold
              ├── PotionReward -> PotionCmd.TryToProcure
              └── RelicReward -> RelicCmd.Obtain
                    └── Hook.AfterRewardTaken
```
