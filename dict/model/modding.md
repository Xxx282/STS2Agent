# Modding mod 接口

---

## 1. ModManager —— mod管理器

**文件**: `src/Core/Modding/ModManager.cs`

| 字段/属性 | 类型 | 说明 |
|---|---|---|
| `AllMods` | `IReadOnlyList<Mod>` | 所有检测到的mod |
| `LoadedMods` | `IReadOnlyList<Mod>` | 已加载的mod |
| `PlayerAgreedToModLoading` | `bool` | 玩家是否同意加载mod |

| 事件 | 触发时机 |
|---|---|
| `OnModDetected` | 检测到mod时 |
| `OnMetricsUpload` | 上传数据时 |

---

## 2. Mod —— mod实例

| 字段 | 类型 | 说明 |
|---|---|---|
| `manifest` | `ModManifest?` | mod清单 |
| `path` | `string` | mod路径 |
| `modSource` | `ModSource` | 来源（目录/Steam工坊） |
| `wasLoaded` | `bool` | 是否已加载 |
| `assembly` | `Assembly?` | 加载的程序集 |

---

## 3. ModManifest —— mod清单

**文件**: `src/Core/Modding/ModManifest.cs`

| 字段 | 类型 | 说明 |
|---|---|---|
| `id` | `string` | mod唯一标识符 |
| `name` | `string` | mod名称 |
| `version` | `string` | 版本号 |
| `author` | `string?` | 作者 |
| `description` | `string?` | 描述 |
| `dependencies` | `List<string>?` | 依赖的mod ID |
| `hasDll` | `bool` | 是否包含DLL |
| `hasPck` | `bool` | 是否包含PCK |
| `affectsGameplay` | `bool` | 是否影响游戏性 |

---

## 4. ModInitializerAttribute —— mod初始化器

**文件**: `src/Core/Modding/ModInitializerAttribute.cs`

用于标记mod的初始化类。使用 `ModInitializerAttribute` 标注的类必须在其中包含一个 `Init()` 静态方法。

```csharp
[ModInitializer]
public class MyMod
{
    public static void Init()
    {
        // 初始化mod
    }
}
```

---

## 5. ModHelper —— mod辅助工具

**文件**: `src/Core/Modding/ModHelper.cs`

提供mod开发常用的辅助功能。
