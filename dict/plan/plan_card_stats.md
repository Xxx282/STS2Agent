# 卡牌场外数据缓存方案

**目标**：将 sts2log.com 的卡牌社区统计数据（pick_rate、win_rate_delta、skada_score）接入 C# mod，使 AI 在卡牌奖励界面能参考社区胜率做决策。离线可用。

---

## 架构

```
Python 端                          C# 端
────────                          ─────
sts2log.com ──抓取──▶ OSS          游戏启动
                        │          │
                        ▼          ▼
              STS2/{char}/card_stats.json   CardStatsService
                        │          │   ├─ 缓存有效（<24h）──直接加载
                        │          │   ├─ 缓存过期──联网拉取──合并──写入缓存
                        │          │   └─ 联网失败──fallback到过期缓存
                        │          │
              data_url.txt ◀──────┘   (C# 不写 URL，Python 写入)
```

**原则**：
- Python 负责数据生产和上传
- C# 只负责消费（拉取 + 查询）
- 本地缓存永久兜底

---

## OSS 结构

```
STS2/ironclad/card_stats.json
STS2/silent/card_stats.json
STS2/defect/card_stats.json
STS2/necrobinder/card_stats.json
STS2/regent/card_stats.json
```

**角色映射**：
```python
CHAR_TO_FOLDER = {
    "IRONCLAD": "ironclad",
    "SILENT": "silent",
    "DEFECT": "defect",
    "NECROBINDER": "necrobinder",
    "REGENT": "regent",
}
```

**单角色 JSON 格式**：
```json
{
  "version": 1,
  "updated_at": "2026-04-17T12:00:00+08:00",
  "characters": ["IRONCLAD"],
  "data": {
    "IRONCLAD": [
      {
        "card_id": "OFFERING",
        "pick_rate": 63.25,
        "win_rate_delta": 29.86,
        "skada_score": 1104.7,
        "rank": 1,
        "confidence": "high",
        "display_name": { "en": "Offering", "zh": "祭品" }
      }
    ]
  }
}
```

**data_url.txt**（每行一个 URL）：
```
https://bucket.oss-cn-hangzhou.aliyuncs.com/STS2/ironclad/card_stats.json
https://bucket.oss-cn-hangzhou.aliyuncs.com/STS2/silent/card_stats.json
https://bucket.oss-cn-hangzhou.aliyuncs.com/STS2/defect/card_stats.json
https://bucket.oss-cn-hangzhou.aliyuncs.com/STS2/necrobinder/card_stats.json
https://bucket.oss-cn-hangzhou.aliyuncs.com/STS2/regent/card_stats.json
```

---

## 关键文件

| 操作 | 文件路径 | 说明 |
|------|----------|------|
| 新建 | `src/Data/scraper.py` | 爬虫核心，HMAC 签名请求 |
| 新建 | `src/Data/uploader.py` | OSS 上传，支持动态 object_key |
| 新建 | `src/Data/cli.py` | CLI 入口 |
| 新建 | `mod/Services/CardStatsService.cs` | 拉取 + 缓存 + 查询 |
| 新建 | `mod/Models/CardStats.cs` | 数据模型 |
| 修改 | `mod/Models/CardRewardInfo.cs` | 增加场外字段 |
| 修改 | `mod/STS2Agent.cs` | 接入 CardStatsService |

---

## CLI 命令

```bash
python -m src.Data.cli                      # 抓取 + 上传（完整流程）
python -m src.Data.cli --scrape-only        # 仅抓取
python -m src.Data.cli --upload-only        # 仅上传
python -m src.Data.cli --char ironclad      # 单角色抓取
python -m src.Data.cli --url                # 输出所有 URL
```

**环境变量**（`.env`）：
```
OSS_ENDPOINT=https://oss-cn-hangzhou.aliyuncs.com
OSS_BUCKET=your-bucket
OSS_KEY=your-access-key-id
OSS_SECRET=your-access-key-secret
DATA_URL_FILE=./data_url.txt
```

---

## 测试验证

### 第一阶段：Python 端

**1.1 配置环境变量**

创建 `.env` 文件或设置环境变量，确保 OSS 配置正确。

**1.2 运行完整流程**

```bash
python -m src.Data.cli
```

**1.3 验证 OSS**

- 登录阿里云 OSS 控制台，确认 5 个 JSON 文件已上传
- 确认文件内容包含 `data` 字段

**1.4 验证 data_url.txt**

```bash
cat data_url.txt
```

应包含 5 行 URL。

**1.5 验证单角色抓取**

```bash
python -m src.Data.cli --char ironclad --scrape-only
```

---

### 第二阶段：C# 端

**2.1 首次启动（冷启动）**

启动游戏，观察日志：

```
%LOCALAPPDATA%/STS2Agent/logs/card_stats.log
```

**2.2 验证缓存文件**

```bash
# 检查本地缓存是否存在
cat "%LOCALAPPDATA%/STS2Agent/cards_cache.json" | head -50
```

确认：
- `version` 字段为 1
- `updated_at` 为当前时间
- `characters` 包含所有 5 个角色
- 每个角色的 `data` 数组包含卡牌

**2.3 缓存命中测试**

保持游戏运行，关闭后重新启动。

预期日志：
```
[INFO] === CardStatsService 初始化 ===
[INFO] 本地缓存文件存在
[INFO] 缓存年龄: 0.1 小时, 有效: True
[INFO] 从本地缓存加载完成，共 XXX 张卡牌
```

**2.4 缓存过期测试**

手动修改缓存文件中的 `updated_at` 为 25 小时前：
```bash
# 临时修改缓存为过期状态
$json = Get-Content "$env:LOCALAPPDATA\STS2Agent\cards_cache.json" -Raw | ConvertFrom-Json
$json.updated_at = (Get-Date).AddHours(-25).ToString("yyyy-MM-ddTHH:mm:ss+08:00")
$json | ConvertTo-Json -Depth 10 | Set-Content "$env:LOCALAPPDATA\STS2Agent\cards_cache.json"
```

重新启动游戏，预期日志：
```
[INFO] 缓存年龄: 25.0 小时, 有效: False
[INFO] 本地缓存已过期或不存在，尝试联网拉取
[INFO] 正在拉取: ...
[INFO] 远程拉取成功
```

**2.5 离线 fallback 测试**

1. 断网（或修改 `data_url.txt` 指向无效 URL）
2. 删除 `%LOCALAPPDATA%/STS2Agent/cards_cache.json`
3. 启动游戏

预期日志：
```
[INFO] 本地缓存已过期或不存在，尝试联网拉取
[ERROR] 远程拉取失败: ...
[INFO] 联网失败，fallback 到过期本地缓存
[INFO] 从本地缓存加载完成，共 XXX 张卡牌
```

---

### 第三阶段：API 集成

**3.1 触发卡牌奖励界面**

在游戏中进入卡牌奖励界面（打完一场战斗后）。

**3.2 验证 API 响应**

```bash
curl http://localhost:8888/api/CardReward
```

预期响应包含 `cards` 数组，每个卡牌有场外字段：
```json
{
  "hasReward": true,
  "isVisible": true,
  "canReroll": true,
  "canSkip": true,
  "cards": [
    {
      "cardId": "STRIKE",
      "name": "打击",
      "cost": 1,
      "rarity": "starter",
      "type": "attack",
      "isUpgraded": false,
      "pickRate": 42.5,
      "winRateDelta": -3.2,
      "skadaScore": 85.3,
      "rank": 35,
      "confidence": "high",
      "displayNameZh": "打击"
    }
  ]
}
```

**3.3 验证 null 情况**

对于新卡牌（OSS 中不存在的卡牌），场外字段应为 `null`：
```json
{
  "cardId": "NEW_CARD_2026",
  "pickRate": null,
  "winRateDelta": null,
  "skadaScore": null,
  "rank": null,
  "confidence": null,
  "displayNameZh": null
}
```

**3.4 AI Client 验证**

确认 AI Client 收到的请求中包含 `pickRate`、`winRateDelta`、`skadaScore` 等字段。

---

## 常见问题

| 问题 | 原因 | 解决 |
|------|------|------|
| `OSS upload failed` | OSS 配置错误或权限不足 | 检查 OSS_ENDPOINT、BUCKET、KEY、SECRET |
| `远程拉取失败` | 网络问题或 URL 错误 | 检查 data_url.txt 是否正确 |
| 卡牌数据为空 | 缓存文件损坏 | 删除缓存重启游戏 |
| 部分 URL 拉取失败 | 单个 OSS 文件不存在 | 重新运行 `python -m src.Data.cli` |

---

## 日志路径

| 类型 | 路径 |
|------|------|
| CardStatsService 日志 | `%LOCALAPPDATA%/STS2Agent/logs/card_stats.log` |
| 本地缓存 | `%LOCALAPPDATA%/STS2Agent/cards_cache.json` |
| URL 配置 | `data_url.txt`（Python 执行目录） |
