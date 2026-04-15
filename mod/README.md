# STS2Agent

Slay the Spire 2 Agent - HTTP API MOD for accessing game state information.

## 功能特性

- HTTP API 服务
- 实时游戏状态采集
- 玩家状态获取 (生命值/能量/手牌等)
- 敌人状态获取 (意图/生命值等)
- 战斗事件监听

## 技术栈

- Godot .NET SDK
- .NET 9.0

## Mod 文件结构与加载机制

### Mod 组成文件

| 文件类型 | 作用 |
|---------|------|
| `.dll` 程序集 | 包含 Mod 全部代码。制作卡牌、能力、遗物等需要编写代码时使用。做皮肤 Mod 不需要代码，用不上此文件。 |
| `.pck` 资源包 | 包含游戏本地化文本和贴图纹理。本地化即"中文、英文"翻译，贴图纹理即游戏内所有图片。做皮肤 Mod 只需要这个。 |
| `.json` 清单 | 包含 Mod 的名字、版本号等信息。相当于纯文本文件，修改游戏内文本描述时也会用到它。 |

### Mod 加载流程

游戏启动时按以下步骤加载 Mod：

```bash
1. 启动检测
   查找 "Mods" 文件夹 → 递归搜索 *.pck 文件

2. 文件加载
   *.dll → 加载程序集
   *.pck → 加载资源包
   *.json → 读取验证信息

3. 初始化
   调用 Mod 入口
   Mod 加载完成
```

## API 端点

| 端点 | 方法 | 描述 |
|------|------|------|
| `/api/state` | GET | 获取完整游戏状态 |
| `/api/player` | GET | 获取玩家状态 |
| `/api/enemies` | GET | 获取敌人状态列表 |
| `/api/health` | GET | 健康检查 |

## API 响应示例

```json
GET /api/state

{
  "inCombat": true,
  "inGame": true,
  "floor": 3,
  "turn": 2,
  "player": {
    "currentHealth": 72,
    "maxHealth": 80,
    "block": 5,
    "energy": 3,
    "maxEnergy": 3,
    "hand": ["Strike", "Defend", "Bash"],
    "drawPile": [],
    "discardPile": [],
    "exhaustPile": []
  },
  "enemies": [
    {
      "id": "1",
      "name": "Slime",
      "currentHealth": 30,
      "maxHealth": 40,
      "block": 0,
      "intent": "attack",
      "intentAmount": 8
    }
  ],
  "combat": {
    "turn": 2,
    "isPlayerTurn": true,
    "canPlayCard": true,
    "canEndTurn": true
  },
  "timestamp": "2026-04-14T00:00:00Z"
}
```

## 安装

1. 确保已安装 SlaySP2Manager
2. 编译项目: `dotnet build -c Release`
3. 将输出文件放入游戏 mods 目录

## 构建

```bash
cd STS2Agent
dotnet restore
dotnet build -c Release
```

输出文件: `bin/Release/net8.0/STS2Agent.dll`

## 使用

1. 启动游戏，MOD 自动加载
2. 服务器在 `localhost:8080` 启动
3. 通过 HTTP 请求获取游戏状态

## Python 客户端示例

```python
import requests

# 获取游戏状态
state = requests.get("http://localhost:8080/api/state").json()
print(f"战斗中: {state['inCombat']}")
print(f"玩家生命: {state['player']['currentHealth']}")
print(f"手牌: {state['player']['hand']}")
```

## 开发

项目结构:
```
STS2Agent/
├── STS2Agent.cs           # 主入口
├── GameLoopNode.cs        # 游戏循环节点
├── Models/                # 数据模型
└── Services/              # 服务层
```

## 注意事项

- 游戏内端口默认 8080
- 防火墙需允许该端口通信

## 许可

MIT License
