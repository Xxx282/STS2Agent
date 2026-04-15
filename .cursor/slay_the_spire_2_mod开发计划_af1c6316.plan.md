---
name: Slay the Spire 2 Mod开发计划
overview: 基于已分析的源码结构，制定完整的Mod开发流程，包括环境搭建、内容添加、Hook使用、打包发布等全流程
todos:
  - id: setup-env
    content: 准备开发环境 - 安装.NET 9 SDK和创建Class Library项目
    status: pending
  - id: create-manifest
    content: 创建Mod清单文件(manifest.json)模板
    status: pending
  - id: implement-hooks
    content: 实现Mod初始化和Hook系统示例代码
    status: pending
  - id: create-card-example
    content: 创建自定义卡牌(CardModel)示例类
    status: pending
  - id: create-relic-example
    content: 创建自定义遗物(RelicModel)示例类
    status: pending
  - id: harmony-patch-example
    content: 编写Harmony补丁示例（可选方式）
    status: pending
  - id: localization-example
    content: 添加本地化支持示例
    status: pending
  - id: project-structure
    content: 创建完整的Mod项目结构和构建脚本
    status: pending
  - id: write-readme
    content: 编写README和Mod使用说明文档
    status: pending
  - id: troubleshoot-guide
    content: 提供SlaysP2Manager导入失败排查指南
    status: pending
isProject: false
---

# Slay the Spire 2 Mod开发计划

## 一、Mod系统核心架构分析

### 1.1 加载机制

- **位置**: `src/Core/Modding/ModManager.cs`
- **检测目录**: 游戏目录下的`mods`文件夹（`{exePath}/mods`）
- **支持来源**: 
  - 本地mods目录
  - Steam创意工坊（通过Steamworks API）
- **加载顺序**: 基于依赖关系拓扑排序 + 用户手动排序
- **加载内容**: 
  - `.dll`文件（C#程序集，通过AssemblyLoadContext加载）
  - `.pck`文件（Godot资源包，通过ProjectSettings.LoadResourcePack加载）

### 1.2 Mod清单（Manifest）结构

文件：`src/Core/Modding/ModManifest.cs`

必需字段：

- `id` (string, 唯一标识符，必需)
- `name` (string, 显示名称)
- `author` (string)
- `description` (string)
- `version` (string)
- `has_pck` (bool, 是否包含PCK资源包)
- `has_dll` (bool, 是否包含DLL程序集)
- `dependencies` (List<string>, 依赖的其他mod ID列表)
- `affectsGameplay` (bool, 默认true，是否影响游戏玩法)

### 1.3 Mod初始化方式

文件：`src/Core/Modding/ModManager.cs` (372-487行)

**方式1 - ModInitializerAttribute**（推荐用于新mod）:

- 在类上标记`[ModInitializer("MethodName")]`
- ModManager会自动查找并调用指定的静态/实例方法
- 适合注册内容到游戏池

**方式2 - Harmony PatchAll**（兼容旧mod）:

- 如果没有ModInitializerAttribute，自动调用`Harmony.PatchAll(assembly)`
- 适用于纯补丁型mod，不添加新内容

### 1.4 Hook系统

文件：`src/Core/Hooks/Hook.cs` + `src/Core/Models/AbstractModel.cs`

**Hook类型**：

- **异步事件钩子**（After/Before + 游戏事件）：60+个钩子
  - 战斗相关：`BeforeCardPlayed`, `AfterDamageGiven`, `BeforeAttack`等
  - 卡牌相关：`AfterCardDrawn`, `AfterCardExhausted`, `AfterCardGeneratedForCombat`
  - 玩家相关：`AfterPlayerTurnStart`, `AfterEnergySpent`, `AfterGoldGained`
  - 地图相关：`AfterMapGenerated`, `BeforeRoomEntered`, `AfterRewardTaken`
  - 等等...

- **修改型钩子**（ModifyXX）: 修改游戏数值
  - `ModifyDamage` / `ModifyDamageAdditive` / `ModifyDamageMultiplicative`
  - `ModifyBlock` / `ModifyHealAmount`
  - `ModifyCardRewardOptions` / `ModifyCardRewardCreationOptions`
  - `ModifyEnergyCostInCombat` / `ModifyStarCost`
  - `ModifyMerchantPrice` / `ModifyMerchantCardPool`
  - 等等...

- **条件钩子**（ShouldXX）: 控制游戏逻辑是否执行
  - `ShouldPlay` / `ShouldDraw` / `ShouldDie`
  - `ShouldAllowTargeting` / `ShouldAllowHitting`
  - `ShouldProcurePotion` / `ShouldGainStars`
  - 等等...

## 二、Mod开发步骤

### 步骤1：准备开发环境

1. 安装.NET 9 SDK
2. 创建Class Library项目（.NET 9）
3. 添加NuGet包引用：

   - `0Harmony` (与游戏版本匹配，位于游戏目录的`data_sts2_windows_x86_64/0Harmony.dll`)
   - `GodotSharp` (可选，如果需要Godot API)

4. 项目配置：

   - `EnableDynamicLoading=true`
   - `Nullable=enable`
   - `AllowUnsafeBlocks=true`

### 步骤2：创建Mod清单文件

在mod根目录创建`{mod_id}.json`：

```json
{
  "id": "com.yourname.modname",
  "name": "My Awesome Mod",
  "author": "Your Name",
  "description": "Description of what this mod does",
  "version": "1.0.0",
  "has_pck": false,
  "has_dll": true,
  "dependencies": [],
  "affectsGameplay": true
}
```

### 步骤3：实现Mod逻辑（两种方式）

**方式A - 使用ModInitializer（推荐）**:

```csharp
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Hooks;

[ModInitializer("Initialize")]
public static class MyModInitializer
{
    public static void Initialize()
    {
        // 注册内容到游戏池
        ModHelper.AddModelToPool<CardPoolModel, MyCustomCard>();
        ModHelper.AddModelToPool<RelicPoolModel, MyCustomRelic>();
        
        // 其他初始化逻辑
        Log.Info("MyMod initialized!");
    }
}

// 自定义卡牌示例
public class MyCustomCard : CardModel
{
    public override Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        // 卡牌使用后的逻辑
        return Task.CompletedTask;
    }
    
    // 重写其他Hook方法...
}
```

**方式B - 使用Harmony补丁**:

```csharp
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;

[HarmonyPatch(typeof(Player), "GainGold")]
public static class Player_GainGold_Patch
{
    static void Prefix(Player __instance, decimal amount)
    {
        // 在原方法前执行
    }
    
    static void Postfix(Player __instance, decimal amount)
    {
        // 在原方法后执行
    }
}
```

### 步骤4：使用Hook系统

继承`AbstractModel`并重写需要的Hook方法：

```csharp
public class MyModBehavior : AbstractModel
{
    // 事件钩子
    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        // 卡牌使用后逻辑
        await DoSomething();
    }
    
    // 修改数值
    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, 
        ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        return 2m; // 增加2点伤害
    }
    
    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, 
        ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        return 1.5m; // 1.5倍伤害
    }
    
    // 条件控制
    public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType)
    {
        return false; // 禁止自动打牌
    }
}
```

### 步骤5：添加新内容

使用`ModHelper.AddModelToPool<T>`注册新内容：

```csharp
// 在Initialize方法中
ModHelper.AddModelToPool<CardPoolModel, MyNewCard>();
ModHelper.AddModelToPool<RelicPoolModel, MyNewRelic>();
ModHelper.AddModelToPool<PotionPoolModel, MyNewPotion>();
ModHelper.AddModelToPool<AfflictionPoolModel, MyNewAffliction>();
// 等等...
```

**注意**：新内容类必须继承对应的Model基类（`CardModel`, `RelicModel`等），并在构造函数中调用`base()`自动注册到ModelDb。

### 步骤6：本地化

在mod的PCK包中或资源路径下添加本地化文件：

```
res://{mod_id}/localization/{language}/{file}.translation
```

或使用Godot的本地化系统。

### 步骤7：资源文件（可选）

如果需要自定义图形/音效：

1. 创建Godot PCK包包含资源
2. 在代码中使用`ResourceLoader.Load`加载
3. 设置`has_pck: true`在manifest中

### 步骤8：依赖管理

在manifest中声明依赖：

```json
"dependencies": ["base.mod.id", "another.mod.id"]
```

ModManager会自动按依赖顺序加载（被依赖的先加载）。

### 步骤9：测试与调试

1. 将mod文件（`.dll` + `.json`）复制到游戏目录的`mods/`子文件夹
2. 启动游戏，首次会显示mod同意警告
3. 在设置中查看mod列表，启用/禁用
4. 查看游戏日志（`user://logs/`）调试信息
5. 开发时可在代码中使用`Log.Info()`, `Log.Error()`输出日志

### 步骤10：打包发布

1. 压缩mod文件夹（包含`.dll`, `.json`, 以及可选的`.pck`）
2. 上传到Steam创意工坊或mod网站
3. 确保manifest中的id唯一（推荐使用反向域名格式）

## 三、Mod示例模板结构

```
MyMod/
├── manifest.json          # Mod清单
├── MyMod.dll             # 编译后的DLL
├── MyMod.pck             # (可选) 资源包
└── README.md            # 说明文档
```

## 四、注意事项

1. **热加载限制**：游戏不支持运行时加载/卸载mod，必须重启
2. **ID唯一性**：每个mod的ID必须全局唯一，冲突会导致加载失败
3. **依赖循环检测**：ModManager会自动检测并报错循环依赖
4. **线程安全**：Hook方法是异步的，注意线程安全
5. **性能考虑**：避免在频繁调用的hook中执行耗时操作
6. **兼容性**：使用Hook而非直接修改原代码，提高兼容性
7. **Steam创意工坊**：需要处理Steamworks回调（ModManager已内置）

## 五、调试SlaysP2Manager导入失败问题

如果第三方Mod管理器（如StS2ModManager）显示"未发现mod"：

**可能原因**：

1. **目录结构错误**：mod必须放在`{游戏目录}/mods/`下，而非其他位置
2. **文件名不匹配**：manifest文件名必须与mod id一致（`{mod_id}.json`）
3. **manifest字段缺失**：缺少`id`字段或`has_dll`/`has_pck`设置不正确
4. **DLL/PCK缺失**：manifest声明的文件不存在于mod目录
5. **JSON格式错误**：manifest文件JSON解析失败
6. **依赖缺失**：声明了依赖但依赖mod未安装

**排查步骤**：

1. 检查游戏目录下是否存在`mods/`文件夹
2. 验证manifest JSON格式（使用JSON验证工具）
3. 确认`id`字段与文件名一致
4. 确认`has_dll`为true时，`{mod_id}.dll`存在
5. 查看游戏日志：`%APPDATA%/SlayTheSpire2/logs/`或游戏安装目录的日志
6. 检查mod文件夹权限（读取权限）

**日志位置**：

- Windows: `%APPDATA%/SlayTheSpire2/`
- Steam: `{Steam}/steamapps/common/Slay the Spire 2/`

---

这个计划涵盖了从环境准备到mod发布的完整流程。您希望我详细展开哪个部分，或者开始创建mod项目模板？