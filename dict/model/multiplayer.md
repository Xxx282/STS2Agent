# 多人游戏数据模型

---

## 1. 多人游戏类型

### 1.1 NetGameType —— 游戏网络类型

```csharp
None, Singleplayer, Host, Client, Replay
```

### 1.2 RunSessionState —— 跑团会话状态

```csharp
None, InLobby, InLoadedLobby, Running
```

| 状态 | 说明 |
|---|---|
| `None` | 无会话 |
| `InLobby` | 在大厅中（跑团尚未开始） |
| `InLoadedLobby` | 在已加载的大厅中（跑团加载中） |
| `Running` | 跑团进行中 |

---

## 2. 网络数据包结构

### 2.1 NetFullCombatState —— 完整战斗状态

**文件**: `src/Core/Entities/Multiplayer/NetFullCombatState.cs`

完整同步战斗中的所有实体状态，是多人游戏中最重要的数据结构。

#### CreatureState —— 生物状态

| 字段 | 类型 | 说明 |
|---|---|---|
| `monsterId` | `ModelId?` | 怪物模型ID |
| `playerId` | `ulong?` | 玩家网络ID |
| `currentHp` | `int` | 当前HP |
| `maxHp` | `int` | 最大HP |
| `block` | `int` | 格挡值 |
| `powers` | `List<PowerState>` | 能力值列表 |

#### PowerState —— 能力值状态

| 字段 | 类型 | 说明 |
|---|---|---|
| `id` | `ModelId` | 能力值模型ID |
| `amount` | `int` | 层数 |

#### OrbState —— 充能球状态

| 字段 | 类型 | 说明 |
|---|---|---|
| `id` | `ModelId` | 充能球模型ID |
| `passive` | `int` | 被动值 |
| `evoke` | `int` | 激发值 |

#### PlayerState —— 玩家状态

| 字段 | 类型 | 说明 |
|---|---|---|
| `playerId` | `ulong` | 玩家网络ID |
| `characterId` | `ModelId` | 角色模型ID |
| `energy` | `int` | 当前能量 |
| `stars` | `int` | 当前星星 |
| `maxStars` | `int` | 最大星星 |
| `maxPotionCount` | `int` | 最大药水数量 |
| `gold` | `int` | 金币 |
| `piles` | `List<CombatPileState>` | 牌堆状态 |
| `potions` | `List<PotionState>` | 药水列表 |
| `relics` | `List<RelicState>` | 遗物列表 |
| `orbs` | `List<OrbState>` | 充能球列表 |
| `rngSet` | `SerializablePlayerRngSet` | 玩家随机数状态 |
| `oddsSet` | `SerializablePlayerOddsSet` | 玩家概率状态 |
| `relicGrabBag` | `SerializableRelicGrabBag` | 共享遗物袋 |

#### CombatPileState —— 战斗牌堆状态

| 字段 | 类型 | 说明 |
|---|---|---|
| `pileType` | `PileType` | 堆类型 |
| `cards` | `List<CardState>` | 卡牌列表 |

#### CardState —— 卡牌状态

| 字段 | 类型 | 说明 |
|---|---|---|
| `card` | `SerializableCard` | 可序列化卡牌 |
| `affliction` | `ModelId?` | 诅咒ID |
| `afflictionCount` | `int` | 诅咒层数 |
| `keywords` | `List<CardKeyword>?` | 关键词列表 |

#### PotionState —— 药水状态

| 字段 | 类型 | 说明 |
|---|---|---|
| `id` | `ModelId` | 药水模型ID |

#### RelicState —— 遗物状态

| 字段 | 类型 | 说明 |
|---|---|---|
| `relic` | `SerializableRelic` | 可序列化遗物 |

#### NetFullCombatState 主结构

| 字段 | 类型 | 说明 |
|---|---|---|
| `Creatures` | `List<CreatureState>` | 所有生物状态 |
| `Players` | `List<PlayerState>` | 所有玩家状态 |
| `Rng` | `SerializableRunRngSet` | 跑团随机数状态 |
| `nextChoiceIds` | `List<uint>` | 下一个选择ID列表 |
| `lastExecutedHookId` | `uint?` | 最后执行的Hook ID |
| `lastExecutedActionId` | `uint?` | 最后执行的动作ID |

---

## 3. 网络卡牌结构

### 3.1 NetCombatCard —— 战斗卡牌（网络传输）

**文件**: `src/Core/Entities/Multiplayer/NetCombatCard.cs`

| 字段 | 类型 | 说明 |
|---|---|---|
| `CombatCardIndex` | `uint` | 战斗中卡牌的唯一索引（16位） |

通过 `NetCombatCardDb` 进行双向转换：
- `FromModel(CardModel)` → `NetCombatCard`
- `ToCardModel()` → `CardModel`

### 3.2 NetDeckCard —— 牌组卡牌（网络传输）

**文件**: `src/Core/Entities/Multiplayer/NetDeckCard.cs`

| 字段 | 类型 | 说明 |
|---|---|---|
| `DeckIndex` | `uint` | 在牌组中的索引（16位） |

### 3.3 NetPlayerChoiceResult —— 玩家选择结果

**文件**: `src/Core/Entities/Multiplayer/NetPlayerChoiceResult.cs`

| 字段 | 类型 | 说明 |
|---|---|---|
| `type` | `PlayerChoiceType` | 选择类型 |
| `canonicalCards` | `List<CardModel>?` | 规范卡牌列表 |
| `combatCards` | `List<NetCombatCard>?` | 战斗卡牌列表 |
| `deckCards` | `List<NetDeckCard>?` | 牌组卡牌列表 |
| `mutableCards` | `List<SerializableCard>?` | 可变卡牌列表 |
| `mutableCardOwner` | `ulong?` | 可变卡牌所属玩家ID |
| `indexes` | `List<int>?` | 索引列表 |
| `playerId` | `ulong?` | 玩家ID |

---

## 4. 玩家选择相关

### 4.1 PlayerChoiceType —— 玩家选择类型

```csharp
None, CanonicalCard, CombatCard, DeckCard, MutableCard, Player, Index
```

### 4.2 PlayerChoiceOptions —— 玩家选择选项

```csharp
[Flags]
None = 0,
CancelPlayCardActions = 1
```

### 4.3 NetScreenType —— 网络屏幕类型

```csharp
None, Room, Map, Settings, Compendium, DeckView,
CardPile, SimpleCardsView, CardSelection, GameOver,
PauseMenu, Rewards, Feedback, SharedRelicPicking,
RemotePlayerExpandedState
```

### 4.4 ReactionType —— 反应类型

```csharp
None, Exclamation, Skull, ThumbDown, SadSlime,
QuestionMark, Heart, ThumbUp, HappyCultist
```

---

## 5. 错误和状态

### 5.1 NetError —— 网络错误

```csharp
None, Quit, QuitGameOver, HostAbandoned, Kicked,
InvalidJoin, CancelledJoin, LobbyFull, RunInProgress,
NotInSaveGame, VersionMismatch, JoinBlockedByUser,
StateDivergence, HandshakeTimeout, ModMismatch,
NoInternet, Timeout, InternalError, UnknownNetworkError,
TryAgainLater, FailedToHost
```

### 5.2 ConnectionFailureReason —— 连接失败原因

```csharp
None, LobbyFull, NotInSaveGame, RunInProgress,
VersionMismatch, ModMismatch
```

### 5.3 ActionSynchronizerCombatState —— 动作同步战斗状态

```csharp
NotInCombat, PlayPhase, EndTurnPhaseOne, NotPlayPhase
```

| 状态 | 说明 |
|---|---|
| `NotInCombat` | 不在战斗中 |
| `PlayPhase` | 出牌阶段 |
| `EndTurnPhaseOne` | 结束回合阶段1 |
| `NotPlayPhase` | 非出牌阶段（敌方回合等） |

### 5.4 GameActionType —— 游戏动作类型

```csharp
None, Combat, CombatPlayPhaseOnly, NonCombat, Any
```

| 类型 | 说明 |
|---|---|
| `Combat` | 战斗动作（任意阶段） |
| `CombatPlayPhaseOnly` | 仅出牌阶段可执行（如出牌） |
| `NonCombat` | 非战斗动作 |
| `Any` | 任意类型 |

---

## 6. 大厅相关

### 6.1 LobbyPlayer —— 大厅玩家

**文件**: `src/Core/Entities/Multiplayer/LobbyPlayer.cs`

| 字段 | 类型 | 说明 |
|---|---|---|
| `id` | `ulong` | 玩家网络ID |
| `slotId` | `int` | 槽位ID（2位） |
| `character` | `CharacterModel` | 角色模型 |
| `unlockState` | `SerializableUnlockState` | 解锁状态 |
| `maxMultiplayerAscensionUnlocked` | `int` | 最高多人进阶等级 |
| `isReady` | `bool` | 是否准备 |

### 6.2 NetClientData —— 网络客户端数据

| 字段 | 类型 | 说明 |
|---|---|---|
| `peerId` | `ulong` | 对等方ID |
| `readyForBroadcasting` | `bool` | 是否准备广播 |

---

## 7. 校验和同步

### 7.1 NetChecksumData —— 校验和数据

| 字段 | 类型 | 说明 |
|---|---|---|
| `id` | `uint` | 校验点ID |
| `checksum` | `uint` | 校验和值 |

### 7.2 StateDivergenceException —— 状态分歧异常

当客户端和服务器状态不一致时抛出。

---

## 8. 动作队列同步

### 8.1 ActionQueueSet —— 动作队列集合

**文件**: `src/Core/GameActions/Multiplayer/ActionQueueSet.cs`

管理所有玩家的动作队列，支持暂停、恢复、取消等操作。

| 字段/属性 | 类型 | 说明 |
|---|---|---|
| `IsEmpty` | `bool` | 所有队列是否为空 |
| `NextActionId` | `uint` | 下一个动作ID |

| 关键方法 | 说明 |
|---|---|
| `EnqueueWithoutSynchronizing(action)` | 入队（不同步） |
| `GetReadyAction()` | 获取就绪动作（按ID排序） |
| `PauseActionForPlayerChoice(action, options)` | 暂停等待玩家选择 |
| `PauseAllPlayerQueues()` | 暂停所有玩家队列 |
| `UnpauseAllPlayerQueues()` | 恢复所有玩家队列 |
| `CombatStarted() / CombatEnded()` | 战斗状态变更 |

### 8.2 ActionQueueSynchronizer —— 动作队列同步器

**文件**: `src/Core/GameActions/Multiplayer/ActionQueueSynchronizer.cs`

管理 Host/Client 之间的动作同步。

| 字段/属性 | 类型 | 说明 |
|---|---|---|
| `CombatState` | `ActionSynchronizerCombatState` | 当前战斗状态 |
| `NextHookId` | `uint` | 下一个Hook ID |

| 关键方法 | 说明 |
|---|---|
| `RequestEnqueue(action)` | 请求入队（Client → Host） |
| `GenerateHookAction(ownerId, type)` | 生成Hook动作 |
| `RequestResumeActionAfterPlayerChoice(action)` | 请求在选择后恢复动作 |

### 8.3 PlayerChoiceSynchronizer —— 玩家选择同步器

**文件**: `src/Core/GameActions/Multiplayer/PlayerChoiceSynchronizer.cs`

管理玩家选择的同步和等待。

| 字段/属性 | 类型 | 说明 |
|---|---|---|
| `ChoiceIds` | `IReadOnlyList<uint>` | 每个玩家的选择ID计数器 |

| 关键方法 | 说明 |
|---|---|
| `ReserveChoiceId(player)` | 为玩家预留选择ID |
| `SyncLocalChoice(player, choiceId, result)` | 同步本地选择 |
| `WaitForRemoteChoice(player, choiceId)` | 等待远程玩家选择 |

---

## 9. 网络消息类型

### 9.1 INetAction —— 网络动作接口

```csharp
public interface INetAction : IPacketSerializable
{
    GameAction ToGameAction(Player player);
}
```

所有网络同步的动作都实现此接口。

### 9.2 INetActionSubtypes —— 网络动作子类型

包含所有内置的 NetAction 子类，用于动作序列化。

### 9.3 ActionTypes —— 动作类型缓存

```csharp
ActionTypes.TypeToId<T>();     // 获取动作类型ID
action.ToId();                // 获取动作实例的ID
ActionTypes.TryGetActionType(id, out type); // 通过ID获取类型
```

---

## 10. 连接管理

### 10.1 JoinFlow —— 加入流程

**文件**: `src/Core/Multiplayer/Game/JoinFlow.cs`

处理客户端加入服务器的完整流程。

| 方法 | 说明 |
|---|---|
| `Begin(initializer, sceneTree)` | 开始加入流程 |

**加入流程步骤：**
1. 连接初始化
2. 版本检查
3. Mod 检查
4. ModelDb Hash 检查
5. 根据 `RunSessionState` 执行不同加入方式：
   - `InLobby` → `AttemptJoin`
   - `InLoadedLobby` → `AttemptLoadJoin`
   - `Running` → `AttemptRejoin`

### 10.2 连接初始化器

| 类 | 说明 |
|---|---|
| `SteamClientConnectionInitializer` | Steam 平台连接 |
| `ENetClientConnectionInitializer` | ENet 连接 |

---

## 11. 数据包序列化

### 11.1 PacketWriter —— 数据包写入器

**文件**: `src/Core/Multiplayer/Serialization/PacketWriter.cs`

使用位级压缩的二进制序列化器。

| 方法 | 说明 |
|---|---|
| `WriteBool(bool)` | 写入布尔值 |
| `WriteInt(int, bits)` | 写入整数（可变位数） |
| `WriteUInt(uint, bits)` | 写入无符号整数 |
| `WriteFloat(float, QuantizeParams?)` | 写入浮点数（可选量化） |
| `WriteString(string)` | 写入字符串 |
| `WriteList<T>(list, lengthBits)` | 写入列表 |
| `WriteEnum<T>(val)` | 写入枚举 |
| `WriteModel(model)` | 写入模型引用 |

### 11.2 QuantizeParams —— 量化参数

| 字段 | 类型 | 说明 |
|---|---|---|
| `min` | `float` | 最小值 |
| `max` | `float` | 最大值 |
| `bits` | `int` | 位数 |

用于将浮点数量化为固定位数，节省带宽。

---

## 12. 多人游戏目录结构

```
src/Core/Multiplayer/
├── Connection/
│   ├── IClientConnectionInitializer.cs
│   ├── ENetClientConnectionInitializer.cs
│   ├── SteamClientConnectionInitializer.cs
│   └── ClientConnectionFailedException.cs
├── Game/
│   ├── NetGameType.cs
│   ├── JoinFlow.cs
│   ├── JoinResult.cs
│   ├── RunLocationTargetedMessageBuffer.cs
│   ├── RewardSynchronizer.cs
│   ├── PlayerChoiceSynchronizer.cs
│   ├── ReactionSynchronizer.cs
│   ├── MapVote.cs
│   ├── MapSelectionSynchronizer.cs
│   ├── EventSynchronizer.cs
│   ├── ActChangeSynchronizer.cs
│   ├── FlavorSynchronizer.cs
│   ├── ChecksumTracker.cs
│   ├── StateDivergenceException.cs
│   ├── Lobby/
│   │   ├── RunLobby.cs
│   │   ├── StartRunLobby.cs
│   │   ├── LoadRunLobby.cs
│   │   └── IRunLobbyListener.cs
│   ├── PeerInput/
│   │   ├── PeerInputSynchronizer.cs
│   │   ├── ScreenStateTracker.cs
│   │   ├── NetCursorHelper.cs
│   │   ├── HoveredModelTracker.cs
│   │   └── NetMapDrawingEvent.cs
│   └── Replay/
│       ├── CombatReplay.cs
│       └── CombatReplayEvent.cs
├── Serialization/
│   ├── PacketWriter.cs
│   ├── PacketReader.cs
│   ├── QuantizeParams.cs
│   ├── NetTypeCache.cs
│   └── IPacketSerializable.cs
├── Transport/
│   └── ENet/
│       ├── ENetHost.cs
│       ├── ENetPacket.cs
│       ├── ENetPacketType.cs
│       └── ENetUtil.cs
└── Messages/
    ├── Game/
    │   ├── Sync/
    │   ├── Checksums/
    │   └── Flavor/

src/Core/Entities/Multiplayer/
├── NetFullCombatState.cs
├── NetCombatCard.cs
├── NetDeckCard.cs
├── NetPlayerChoiceResult.cs
├── NetClientData.cs
├── NetChecksumData.cs
├── NetError.cs
├── NetErrorInfo.cs
├── LobbyPlayer.cs
├── RunSessionState.cs
├── ReactionType.cs
├── NetScreenType.cs
├── PlayerChoiceOptions.cs
├── GameActionType.cs
├── ActionSynchronizerCombatState.cs
└── ConnectionFailureReason.cs

src/Core/GameActions/Multiplayer/
├── INetAction.cs
├── INetActionSubtypes.cs
├── ActionQueueSet.cs
├── ActionQueueSynchronizer.cs
├── PlayerChoiceSynchronizer.cs
├── ActionTypes.cs
├── PlayerChoiceContext.cs
├── BlockingPlayerChoiceContext.cs
├── ThrowingPlayerChoiceContext.cs
├── HookPlayerChoiceContext.cs
├── GameActionPlayerChoiceContext.cs
└── NetCombatCardDb.cs
```
