<!--
 * @Author: Mendax
 * @Date: 2026-04-13 22:35:28
 * @LastEditors: Mendax
 * @LastEditTime: 2026-04-19 23:42:14
 * @Description: 
 * @FilePath: \STS2Agent\README.md
-->
<!--
 * @Author: Mendax
 * @Date: 2026-04-13 22:35:28
 * @LastEditors: Mendax
 * @LastEditTime: 2026-04-16 22:49:42
 * @Description: 
 * @FilePath: \STS2Agent\README.md
-->
# STS2Agent

Slay the Spire 2 智能代理 - HTTP API 接口

## 功能

- 提供 REST API 访问游戏状态
- 支持玩家、敌人、战斗状态查询
- 供 AI Agent 集成使用

## 技术栈

- Godot 4.3 引擎
- C# (.NET 8.0)

## 项目结构

```text
STS2Agent/
├── mod/                    # Mod 源码
│   ├── libs/               # 游戏 DLL 依赖
│   ├── Models/             # 数据模型
│   ├── Services/           # 服务层
│   ├── scripts/            # 辅助脚本
│   ├── publish/            # 发布输出
│   ├── STS2Agent.cs        # 主入口
│   ├── GameLoopNode.cs     # 游戏循环
│   ├── STS2Agent.csproj    # 项目配置
│   ├── STS2Agent.json      # Mod 清单
│   └── README.md           # Mod 文档
├── src/                    # 其他源码
├── .gitignore              # Git 忽略规则
├── README.md               # 项目主文档
├── requirements.txt        # Python 依赖
└── test_api.py             # 测试脚本
```

## MOD安装

1. 进入mod目录

    ```bash
    cd mod
    ```

2. 编译项目

   ```bash
   dotnet build -c Release
   ```

3. 运行部署脚本

   ```bash
   .\deploy.ps1 
   .\deploy.ps1 -Force       # 游戏运行时强制重部署
   .\deploy.ps1 -SkipBuild   # 只复制文件，不编译
   ```

## 环境配置

```bash
# 创建虚拟环境并安装依赖
uv sync

# 运行脚本
uv run python <*.py>

# 添加新依赖
uv add <package>

# 环境变量
cp .env.example .env
```

---

## [API 文档](./mod/openapi.json) 

运行端口 <http://localhost:8890>

| 端点 | 方法 | 描述 |
| ---- | ---- | ---- |
| `/api/state` | GET | 获取完整游戏状态 |
| `/api/player` | GET | 获取玩家状态 |
| `/api/enemies` | GET | 获取敌人状态列表 |
| `/api/combat` | GET | 获取战斗状态 |
| `/api/CardReward` | GET | 卡牌奖励状态（轮询） |
| `/api/health` | GET | 健康检查 |

## 待办事项

- [x] 卡牌选择数据获取的测试(2026.4.18完成)
- [x] 卡牌场外数据获取脚本测试
- [ ] 卡牌奖励界面场外信息ui设计与测试

## 感谢

- 烟汐忆梦_YM (Godot编译文件修理) <https://github.com/Yanxiyimengya/Sts2Repairer>

- STS2MCP(提供项目参考) <https://github.com/Gennadiyev/STS2MCP>
