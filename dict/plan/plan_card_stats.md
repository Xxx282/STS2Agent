# 卡牌场外数据缓存方案

**目标**：将 sts2log.com 的卡牌社区统计数据（pick_rate、win_rate_delta、skada_score 等）接入 C# mod，使 AI 在卡牌奖励界面能参考社区胜率数据做决策。

**离线可用**：游戏内断网时用本地缓存兜底，不强制要求网络。

---

## 架构总览

```
┌─────────────────────────────────────────────────────────┐
│  STS2Data 项目（独立维护）                                │
│  ┌──────────────┐    ┌──────────────┐    ┌───────────┐ │
│  │ Python 爬虫  │───▶│ 数据处理过滤  │───▶│ 上传到    │ │
│  │ sts2log.com │    │              │    │ OSS / CDN │ │
│  └──────────────┘    └──────────────┘    └───────────┘ │
└─────────────────────────────────────────────────────────┘
                                                      │
                                                      ▼ 远程 JSON URL
┌─────────────────────────────────────────────────────────┐
│  STS2Agent 项目（C# Mod）                               │
│                                                         │
│  CardStatsService.Initialize()                          │
│    ├─ 本地缓存有效（< 24h）──▶ 直接加载到内存            │
│    ├─ 本地缓存过期 ──▶ 联网拉取远程 JSON ──▶ 写入本地     │
│    └─ 联网失败 ──▶ fallback 到过期本地缓存               │
│                                                         │
│  /api/CardReward                                        │
│    └─ 附加场外统计数据字段 ──▶ AI Client                  │
└─────────────────────────────────────────────────────────┘
```

**核心原则**：
- STS2Data 和 STS2Agent 是**两个独立项目**，分开部署和维护
- 数据更新只依赖 STS2Data，STS2Agent 只消费
- 离线必须可用：本地缓存永久兜底

---

## 第一步：新建独立数据项目 STS2Data

### 项目结构

```
STS2Data/
├── pyproject.toml
├── src/
│   ├── __init__.py
│   ├── scraper.py      # 爬虫核心（从 test/test_cards_scraper.py 迁移）
│   ├── uploader.py     # OSS/CDN 上传逻辑
│   └── cli.py          # CLI 入口
├── .github/
│   └── workflows/
│       └── daily-scrape.yml  # GitHub Actions 定时任务（可选）
└── README.md
```

### `src/scraper.py`（迁移 + 重构）

将 `test/test_cards_scraper.py` 中的 `STS2CardScraper` 迁移至此，保留核心逻辑：

- HMAC-SHA256 签名请求（`x-skada-t` / `x-skada-s` header）
- 分页抓取（IRONCLAD / SILENT / DEFECT / AWAKENEDONE）
- `fetch_api(char, page)` / `scrape(char)` 方法
- `ScraperResult` dataclass 返回类型

### `src/uploader.py`

使用阿里云 OSS，通过环境变量配置：

```python
# 环境变量
OSS_ENDPOINT=xxx        # OSS endpoint，如 https://oss-cn-hangzhou.aliyuncs.com
OSS_BUCKET=xxx          # Bucket 名称
OSS_KEY=xxx             # AccessKey ID
OSS_SECRET=xxx          # AccessKey Secret
OSS_OBJECT_KEY=xxx      # 上传后的对象路径，如 cards_stats.json
```

上传后返回**公开访问 URL**，并将该 URL 写入配置文件供 C# 端读取：

```python
# 配置文件路径（由 --config 参数指定，默认 ./config.txt）
https://<bucket>.<endpoint>/<object_key>
```

上传流程：

1. 读取环境变量，初始化 `oss2.Bucket`
2. 调用 `bucket.put_object_from_file(object_key, local_file)`
3. 拼接公开 URL
4. 可选写入配置文件：`open(config_path, "w").write(url)`

### `src/cli.py`

```bash
python -m src.cli                      # 抓取 + 上传（完整流程）
python -m src.cli --scrape-only        # 仅抓取，不上传
python -m src.cli --upload-only        # 仅上传本地数据文件
python -m src.cli --url                # 输出当前数据 URL
```

返回码：0=成功，1=网络错误，2=无需刷新，3=参数错误。

### 定时任务（可选）

通过 GitHub Actions 每日自动触发，触发后执行 `python -m src.cli` 将数据上传到 OSS：

```yaml
# .github/workflows/daily-scrape.yml
on:
  schedule:
    - cron: '0 4 * * *'   # 每天 UTC 4:00（约北京时间 12:00）
  workflow_dispatch:        # 支持手动触发

env:
  OSS_ENDPOINT: ${{ secrets.OSS_ENDPOINT }}
  OSS_BUCKET: ${{ secrets.OSS_BUCKET }}
  OSS_KEY: ${{ secrets.OSS_KEY }}
  OSS_SECRET: ${{ secrets.OSS_SECRET }}
  OSS_OBJECT_KEY: card_stats.json
```

---

## 第二步：设计 JSON 缓存结构

### 远程 JSON（STS2Data 上传）

```json
{
  "version": 1,
  "updated_at": "2026-04-17T12:00:00+08:00",
  "characters": ["IRONCLAD", "SILENT", "DEFECT", "AWAKENEDONE"],
  "data": {
    "IRONCLAD": [
      {
        "card_id": "OFFERING",
        "pick_rate": 63.25,
        "win_rate_delta": 29.86,
        "skada_score": 1104.7,
        "rank": 70,
        "confidence": "high",
        "display_name": { "en": "Offering", "zh": "祭品" }
      }
    ],
    "SILENT": [],
    "DEFECT": [],
    "AWAKENEDONE": []
  }
}
```

保留字段：核心统计（pick_rate, win_rate_delta, skada_score, rank, confidence）+ display_name
丢弃冗余字段：character、card_pool、card_pool_name、seen 等游戏内已有的数据

### 本地缓存文件

路径：`%LOCALAPPDATA%/STS2Agent/cards_cache.json`
远程 URL 配置文件：`%LOCALAPPDATA%/STS2Agent/card_stats_url.txt`

---

## 第三步：C# CardStatsService

### 新建 `mod/Services/CardStatsService.cs`

**职责**：远程拉取数据 + 本地缓存 + 内存查询。

```csharp
public class CardStatsService
{
    // 启动时（Initialize）中：
    // 1. 检查本地缓存是否在 TTL（24h）内
    //    - 有效 -> 直接从本地文件加载到内存
    //    - 无效或不存在 -> 尝试联网拉取
    // 2. 联网拉取：
    //    - 读取 card_stats_url.txt 获取远程 URL
    //    - HttpClient.GetAsync(url) -> 写入本地文件 -> 加载到内存
    //    - 网络失败 -> fallback 到本地缓存（即使过期）
    // 3. 内存结构：Dictionary<string, CardStats>  // key = "CHARACTER:CardId"

    public CardStats? GetStats(string character, string cardId);
    public bool IsLoaded { get; }
}
```

**CardStats 模型**：

```csharp
public class CardStats
{
    public string CardId { get; set; }
    public string Character { get; set; }
    public float PickRate { get; set; }
    public float WinRateDelta { get; set; }
    public float SkadaScore { get; set; }
    public int Rank { get; set; }
    public string Confidence { get; set; }
    public string DisplayNameZh { get; set; }
}
```

**TTL 策略**：启动时检查，本地文件 `updated_at` 超过 24h 则尝试联网刷新。游戏内不主动刷新，下次启动再检。

**离线兜底**：网络失败时，即使本地缓存过期也照常使用，确保游戏内断网不报错。

### 注册到 `STS2Agent.Initialize()`

```csharp
_cardStatsService = new CardStatsService();
```

### 日志

写入 `mods/STS2Agent/logs/card_stats.log`：

- 缓存命中（本地有效）
- 远程拉取成功
- 远程拉取失败 + fallback 到本地
- 内存中无数据（首次运行）
- 查询未命中的卡牌（正常现象，新卡）

---

## 第四步：修改 `/api/CardReward` 返回附加数据

### 4.1 修改 `mod/Models/CardRewardInfo.cs`

在 `RewardCardInfo` 中增加场外数据字段（可空，网络/缓存不可用时为 null）：

```csharp
public class RewardCardInfo
{
    // 现有字段...

    // 场外统计数据
    public float? PickRate { get; set; }
    public float? WinRateDelta { get; set; }
    public float? SkadaScore { get; set; }
    public int? Rank { get; set; }
    public string? Confidence { get; set; }
    public string? DisplayNameZh { get; set; }
}
```

### 4.2 修改 `STS2Agent.cs` 的 `HandleCardRewardRequest`

```csharp
private static void HandleCardRewardRequest(HttpListenerContext context)
{
    var reward = _cardRewardService?.GetCurrentReward();
    if (reward != null && reward.IsVisible)
    {
        var character = DetectCharacter(reward);
        var stats = _cardStatsService;

        var response = new
        {
            hasReward = true,
            isVisible = reward.IsVisible,
            canReroll = reward.CanReroll,
            canSkip = reward.CanSkip,
            cards = reward.Cards.Select(c =>
            {
                var s = stats?.GetStats(character, c.CardId);
                return new {
                    cardId = c.CardId,
                    name = c.Name,
                    cost = c.Cost,
                    rarity = c.Rarity,
                    type = c.Type,
                    isUpgraded = c.IsUpgraded,
                    // 场外数据（可空）
                    pickRate = s?.PickRate,
                    winRateDelta = s?.WinRateDelta,
                    skadaScore = s?.SkadaScore,
                    rank = s?.Rank,
                    confidence = s?.Confidence,
                    displayNameZh = s?.DisplayNameZh,
                };
            }).ToList()
        };
        SendJson(context, response);
    }
    else
    {
        SendJson(context, new { hasReward = false });
    }
}
```

**当前角色检测**：通过 `GameStateService` 获取当前 `PlayerState`，从  `PlayerState` 中推断当前角色。

---

## 第五步：测试验证

1. 运行 STS2Data 的爬虫 + 上传，验证 OSS/CDN 上有数据
2. 在 STS2Agent 机器上手动删除本地缓存，启动游戏，验证远程拉取成功
3. 断网场景下启动游戏，验证 fallback 到过期本地缓存
4. 触发卡牌奖励界面，`GET http://localhost:8888/api/CardReward` 验证响应中包含场外字段

---

## 关键文件清单

| 操作 | STS2Data 项目 | STS2Agent 项目 |
|------|---------------|---------------|
| 新建 | `STS2Data/pyproject.toml` | - |
| 新建 | `STS2Data/src/scraper.py` | - |
| 新建 | `STS2Data/src/uploader.py` | - |
| 新建 | `STS2Data/src/cli.py` | - |
| 新建 | `STS2Data/.github/workflows/daily-scrape.yml` | - |
| 新建 | - | `mod/Services/CardStatsService.cs` |
| 修改 | - | `mod/Models/CardRewardInfo.cs` |
| 修改 | - | `mod/STS2Agent.cs` |
| 不改 | `test/test_cards_scraper.py` | - |

---

## 依赖说明

| 依赖 | 用途 | 安装 |
|------|------|------|
| oss2 | 阿里云 OSS 上传 | `pip install oss2` |
| HttpClient（C# 内置） | 远程拉取 JSON | .NET 内置 |
| System.Text.Json（C# 内置） | JSON 解析 | .NET 内置 |
