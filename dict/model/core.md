# 核心基础设施

本文档基于游戏源码 `C:\Users\18200\Desktop\Slay the Spire 2\src` 编写。

---

## 1. AbstractModel —— 所有数据模型的基类

**文件**: `src/Core/Models/AbstractModel.cs`

所有数据模型的抽象基类，提供统一标识与生命周期管理。

| 字段/属性 | 类型 | 说明 |
|---|---|---|
| `Id` | `ModelId` | 模型的唯一标识（由 `ModelDb` 自动分配） |
| `IsMutable` | `bool` | 区分规范实例（canonical）和可变实例（mutable） |
| `IsCanonical` | `bool` | 只读属性的快捷方式 |
| `CategorySortingId` | `int` | 网络序列化的分类排序ID |
| `EntrySortingId` | `int` | 网络序列化的条目排序ID |

| 关键方法 | 说明 |
|---|---|
| `AssertMutable()` | 确保对象处于可修改状态，否则抛异常 |
| `MutableClone()` | 深拷贝创建可变副本 |
| `ToMutable()` | 转换为可变实例 |

---

## 2. ModelDb —— 模型数据库

**文件**: `src/Core/Models/ModelDb.cs`

全局模型数据库，提供模型的注册、查找和实例化。

```csharp
// 获取规范实例
T canonical = ModelDb.GetById<T>(modelId);

// 获取ID
ModelId id = ModelDb.GetId<T>();
```

---

## 3. IRunState —— 跑团状态接口

**文件**: `src/Core/Runs/RunState.cs`

定义跑团状态的接口，包含：

- `Players` — 所有玩家列表
- `Map` — 当前地图
- `ActFloor` / `TotalFloor` — 层数
- `Rng` — 随机数生成器
- `IterateHookListeners()` — 遍历所有Hook监听器
