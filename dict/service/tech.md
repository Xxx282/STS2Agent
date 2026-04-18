# Slay the Spire 2 技术架构

本文档描述 Slay the Spire 2 的技术栈和开发工具。

---

## 1. 引擎 —— Godot Engine 4.5

Slay the Spire 2 使用 **Godot 4.5**（.NET Edition）作为游戏引擎，这是一个重要的技术升级，与初代使用的 Java/LibGDX 不同。

| 项目 | 配置 |
|---|---|
| 引擎版本 | Godot 4.5.1 |
| 渲染驱动 | DirectX 12 (d3d12) |
| 脚本语言 | C# + GDScript |
| .NET 版本 | .NET 9.0 |
| C# 语言版本 | 13.0 |
| 平台支持 | Windows、Mac、Mobile |
| 物理引擎 | Dummy（禁用，使用自定义方案） |

### 自动加载节点 (Autoload)

| 节点 | 用途 |
|---|---|
| `SentryInit` | 错误监控和上报 |
| `OneTimeInitialization` | 一次性初始化逻辑 |
| `AssetLoader` | 资源加载管理 |
| `DevConsole` | 开发者控制台 |
| `CommandHistory` | 命令历史记录 |
| `MemoryMonitor` | 内存监控 |
| `FmodManager` | FMOD 音频管理 |

---

## 2. 项目结构

```
Slay the Spire 2/
├── project.godot           # Godot 项目配置
├── sts2.csproj            # .NET 项目文件
├── packages.lock.json      # NuGet 包依赖
├── addons/                # 插件目录
│   ├── atlas_generator/   # 精灵图集合并工具
│   ├── dev_tools/        # 开发工具
│   ├── fmod/             # FMOD 音频集成
│   ├── megacontentcreator/ # 内容创建工具
│   ├── mega_text/        # 文本处理
│   └── sentry/           # 错误监控
├── scenes/               # Godot 场景文件 (.tscn)
├── src/                  # C# 源代码
│   ├── Core/            # 核心游戏逻辑
│   └── GameInfo/        # 游戏信息
├── animations/          # 动画资源
├── banks/               # FMOD 音频库
├── fonts/              # 字体资源
├── images/             # 图片资源
├── localization/      # 本地化文件
├── materials/          # 材质资源
├── models/            # 3D 模型
├── shaders/            # 着色器
└── themes/            # UI 主题
```

---

## 3. 核心依赖库

### NuGet 包

| 包名 | 版本 | 用途 |
|---|---|---|
| `Godot.SourceGenerators` | 4.5.1 | Godot C# 代码生成器 |
| `GodotSharp` | 4.5.1 | Godot C# 核心库 |
| `GodotSharpEditor` | 4.5.1 | Godot 编辑器集成 |
| `JetBrains.Annotations` | 2023.3.0 | 代码注解 |
| `MonoMod.Backports` | 1.1.2 | MonoMod 补丁支持 |
| `Sentry` | 5.0.0 | 错误追踪和监控 |
| `SmartFormat` | 3.3.0 | 字符串格式化 |
| `System.IO.Hashing` | 9.0.0 | 哈希计算 |
| `Vortice.DXGI` | 3.6.2 | DirectX 诊断接口 |

### 第三方集成

| 库名 | 用途 |
|---|---|
| `Steamworks.NET` | Steam 平台集成 |
| `0Harmony` (Harmony) | IL 补丁（Mod 系统） |
| `FMOD` | 游戏音频引擎 |

---

## 4. Mod 系统

### 4.1 Mod 加载机制

游戏支持两种 Mod 加载方式：

1. **本地目录**: `mods/` 文件夹
2. **Steam Workshop**: Steam 工坊订阅

### 4.2 Mod 结构

```
mods/
└── [mod_id]/
    ├── manifest.json      # Mod 清单
    ├── [mod_id].dll      # C# 代码（可选）
    └── [mod_id].pck      # Godot 资源包（可选）
```

### 4.3 Mod 清单 (manifest.json)

| 字段 | 类型 | 说明 |
|---|---|---|
| `id` | `string` | Mod 唯一标识符 |
| `name` | `string` | Mod 名称 |
| `version` | `string` | 版本号 |
| `author` | `string?` | 作者 |
| `description` | `string?` | 描述 |
| `dependencies` | `List<string>?` | 依赖的 Mod ID |
| `hasDll` | `bool` | 是否包含 C# 代码 |
| `hasPck` | `bool` | 是否包含资源包 |
| `affectsGameplay` | `bool` | 是否影响游戏性 |

### 4.4 Mod 初始化

**方式一**：使用 `ModInitializerAttribute`

```csharp
[ModInitializer]
public class MyMod
{
    public static void Init()
    {
        // 初始化代码
    }
}
```

**方式二**：使用 Harmony 进行 IL 补丁

```csharp
[HarmonyPatch(typeof(TargetClass), "TargetMethod")]
public class MyPatch
{
    public static void Postfix(ref int __result)
    {
        __result += 10;
    }
}
```

### 4.5 ModManager

**文件**: `src/Core/Modding/ModManager.cs`

| 属性/事件 | 说明 |
|---|---|
| `AllMods` | 所有检测到的 Mod |
| `LoadedMods` | 已加载的 Mod |
| `PlayerAgreedToModLoading` | 玩家是否同意加载 Mod |
| `OnModDetected` | 检测到 Mod 时触发 |
| `OnMetricsUpload` | 上传数据时触发 |

---

## 5. 调试和开发工具

### 5.1 开发者控制台

通过 `DevConsole` 节点访问，支持多种调试命令。

### 5.2 常用调试命令

| 命令 | 功能 |
|---|---|
| `Fight [encounter_id]` | 开始战斗 |
| `Event [event_id]` | 触发事件 |
| `Gold [amount]` | 设置金币 |
| `Heal [amount]` | 治疗 |
| `Kill [target]` | 击杀目标 |
| `Energy [amount]` | 设置能量 |
| `Enchant [card_id]` | 附魔卡牌 |
| `GodMode` | 上帝模式 |

### 5.3 快捷键

| 快捷键 | 功能 |
|---|---|
| `I` | 即时模式 (Instant Mode) |
| `D` | 查看牌组 |
| `M` | 查看地图 |
| `[` / `]` | Controller 设置标签页 |
| `P` | 切换粒子计数器 |
| `V` | 反应轮盘 |

---

## 6. 存档系统

### 6.1 存档位置

```
%APPDATA%/SlayTheSpire2/
```

在 `project.godot` 中配置：
```ini
config/use_custom_user_dir=true
config/custom_user_dir_name="SlayTheSpire2"
```

### 6.2 存档格式

使用 JSON 格式，包含以下主要部分：

| 存档部分 | 说明 |
|---|---|
| `RunState` | 跑团状态 |
| `SerializableRun` | 可序列化的跑团数据 |
| `Settings` | 玩家设置 |

---

## 7. 多人游戏

游戏支持多人合作模式，通过 Steamworks.NET 和 ENet 实现 P2P/客户端-服务器网络架构。

### 7.1 网络架构

```
NetGameType
├── Singleplayer  # 单人模式
├── Host          # 主机（Host）
├── Client        # 客户端
└── Replay        # 回放模式
```

| 组件 | 文件 | 说明 |
|---|---|---|
| `ENetHost` | `Transport/ENet/ENetHost.cs` | ENet 网络传输层 |
| `NetClientGameService` | 多人游戏服务 | 客户端网络服务 |
| `JoinFlow` | `Multiplayer/Game/JoinFlow.cs` | 加入流程管理 |

### 7.2 连接流程

```
客户端发起连接
  ├── 版本检查 (version mismatch → NetError.VersionMismatch)
  ├── Mod 检查 (mod mismatch → NetError.ModMismatch)
  ├── ModelDb Hash 检查 (hash mismatch → 断开)
  └── 根据 RunSessionState 加入
```

### 7.3 数据同步机制

| 同步类型 | 说明 |
|---|---|
| **动作同步** | 通过 `INetAction` 接口序列化游戏动作 |
| **状态同步** | `NetFullCombatState` 完整战斗状态同步 |
| **校验和同步** | `ChecksumTracker` 检测状态分歧 |
| **玩家选择同步** | `PlayerChoiceSynchronizer` 管理选择广播 |

### 7.4 动作队列同步

| 类 | 说明 |
|---|---|
| `ActionQueueSet` | 管理所有玩家的动作队列 |
| `ActionQueueSynchronizer` | Host/Client 动作同步 |
| `PlayerChoiceSynchronizer` | 玩家选择同步 |

### 7.5 网络数据包

**关键结构：**
- `NetFullCombatState` — 完整战斗状态
- `NetCombatCard` — 战斗卡牌（16位索引）
- `NetDeckCard` — 牌组卡牌（16位索引）
- `NetPlayerChoiceResult` — 玩家选择结果
- `PacketWriter/PacketReader` — 位级二进制序列化

**位级压缩示例：**
```csharp
WriteUInt(CombatCardIndex, 16);  // 16位压缩
WriteInt(energy, 4);             // 能量用4位
```

---

## 8. 资源管理

### 8.1 资源类型

| 类型 | 格式 | 说明 |
|---|---|---|
| 场景 | `.tscn` | Godot 场景文件 |
| 脚本 | `.gd` / `.cs` | GDScript / C# 代码 |
| 图片 | `.png` / Atlas | 纹理和精灵图集 |
| 音频 | `.tres` | Godot 音频资源 |
| 字体 | `.ttf` / `.otf` | 字体文件 |
| 着色器 | `.gdshader` | Godot 着色器 |
| 本地化 | JSON | 多语言文本 |

### 8.2 本地化

使用 `LocString` 类管理本地化文本，支持动态变量替换。

---

## 9. 命名空间约定

```csharp
MegaCrit.Sts2.Core.功能模块.具体类
```

示例：
- `MegaCrit.Sts2.Core.Models.CardModel`
- `MegaCrit.Sts2.Core.Combat.CombatManager`
- `MegaCrit.Sts2.Core.Entities.Players.Player`

---

## 10. 与初代的技术对比

| 方面 | Slay the Spire | Slay the Spire 2 |
|---|---|---|
| 引擎 | LibGDX (Java) | Godot 4.5 (C#) |
| 脚本语言 | Java | C# + GDScript |
| Mod 支持 | Steam Workshop / Basemod | 原生 Mod API + Harmony |
| 存档格式 | 自定义二进制 | JSON |
| 多人支持 | 无 | 原生多人合作 |
| 运行时 | JVM | .NET 9.0 |
