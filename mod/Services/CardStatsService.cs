using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using STS2Agent.Models;

namespace STS2Agent.Services;

public class CardStatsService
{
    private readonly Dictionary<string, CardStats> _statsMap = new();
    private readonly Dictionary<string, CardStats> _zhNameMap = new();
    private bool _loaded;
    private readonly string _cacheFilePath;
    private readonly string _logFilePath;
    private readonly object _logLock = new();
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;
        private const int TTL_HOURS = 24;

    private static readonly string[] REMOTE_URLS = new[]
    {
        "https://sts2agent.oss-cn-hangzhou.aliyuncs.com/STS2/ironclad/card_stats.json",
        "https://sts2agent.oss-cn-hangzhou.aliyuncs.com/STS2/silent/card_stats.json",
        "https://sts2agent.oss-cn-hangzhou.aliyuncs.com/STS2/defect/card_stats.json",
        "https://sts2agent.oss-cn-hangzhou.aliyuncs.com/STS2/necrobinder/card_stats.json",
        "https://sts2agent.oss-cn-hangzhou.aliyuncs.com/STS2/regent/card_stats.json",
    };

    public bool IsLoaded => _loaded;

    public CardStatsService()
    {
        string modDir = GetLogDir();
        string dataDir = Path.Combine(Path.GetDirectoryName(modDir)!, "data");
        Directory.CreateDirectory(dataDir);
        _cacheFilePath = Path.Combine(dataDir, "cards_cache.json");
        _logFilePath = Path.Combine(modDir, "card_stats.log");

        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        // 配置 JSON 序列化选项：处理 snake_case 字段名
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true
        };

        Log("=== CardStatsService 初始化 ===");
        Initialize();
    }

    private void Initialize()
    {
        // 1. 检查本地缓存是否有效
        if (IsCacheValid())
        {
            Log("缓存命中（本地有效），从本地加载");
            LoadFromLocalCache();
            return;
        }

        // 2. 本地缓存无效，尝试联网拉取
        Log("本地缓存已过期或不存在，尝试联网拉取");
        if (TryFetchRemote())
        {
            return;
        }

        // 3. 联网失败，fallback 到本地缓存（即使过期）
        Log("联网失败，fallback 到过期本地缓存");
        LoadFromLocalCache();
    }

    private bool IsCacheValid()
    {
        if (!File.Exists(_cacheFilePath))
        {
            Log("本地缓存文件不存在");
            return false;
        }

        try
        {
            var json = File.ReadAllText(_cacheFilePath);
            var response = JsonSerializer.Deserialize<CardStatsResponse>(json, _jsonOptions);
            if (response == null || string.IsNullOrEmpty(response.UpdatedAt))
            {
                return false;
            }

            if (!DateTime.TryParse(response.UpdatedAt, out var updatedAt))
            {
                return false;
            }

            var age = DateTime.UtcNow - updatedAt.ToUniversalTime();
            var isValid = age.TotalHours < TTL_HOURS;
            Log($"缓存年龄: {age.TotalHours:F1} 小时, 有效: {isValid}");
            return isValid;
        }
        catch (Exception ex)
        {
            LogError("检查缓存有效性失败", ex);
            return false;
        }
    }

    private void LoadFromLocalCache()
    {
        try
        {
            if (!File.Exists(_cacheFilePath))
            {
                Log("无本地缓存文件");
                return;
            }

            var json = File.ReadAllText(_cacheFilePath);
            var response = JsonSerializer.Deserialize<CardStatsResponse>(json, _jsonOptions);
            if (response != null)
            {
                LoadResponse(response);
                _loaded = true;
                Log($"从本地缓存加载完成，共 {response.Data.Values.Sum(d => d.Count)} 张卡牌");
            }
        }
        catch (Exception ex)
        {
            LogError("从本地缓存加载失败", ex);
        }
    }

    private bool TryFetchRemote()
    {
        try
        {
            Log($"从 {REMOTE_URLS.Length} 个 URL 拉取数据...");

            var allResponses = new List<CardStatsResponse>();
            var failedUrls = new List<string>();

            foreach (var remoteUrl in REMOTE_URLS)
            {
                try
                {
                    Log($"正在拉取: {remoteUrl}");
                    var json = _httpClient.GetStringAsync(remoteUrl).GetAwaiter().GetResult();
                    var response = JsonSerializer.Deserialize<CardStatsResponse>(json, _jsonOptions);
                    if (response != null)
                    {
                        allResponses.Add(response);
                    }
                }
                catch (Exception ex)
                {
                    LogError($"拉取失败: {remoteUrl}", ex);
                    failedUrls.Add(remoteUrl);
                }
            }

            if (allResponses.Count == 0)
            {
                Log("所有远程 URL 拉取均失败");
                return false;
            }

            // 合并所有响应
            var merged = MergeResponses(allResponses);

            // 保存合并后的缓存
            var mergedJson = JsonSerializer.Serialize(merged, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_cacheFilePath, mergedJson);
            Log($"远程数据已保存到本地缓存（共 {allResponses.Count} 个文件合并）");

            LoadResponse(merged);
            _loaded = true;
            Log($"远程拉取成功，共 {_statsMap.Count} 张卡牌");

            if (failedUrls.Count > 0)
            {
                Log($"部分 URL 拉取失败: {string.Join(", ", failedUrls)}");
            }

            return true;
        }
        catch (Exception ex)
        {
            LogError("远程拉取失败", ex);
            return false;
        }
    }

    private CardStatsResponse MergeResponses(List<CardStatsResponse> responses)
    {
        var merged = new CardStatsResponse
        {
            Version = 1,
            UpdatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss+08:00"),
            Characters = new List<string>(),
            Data = new Dictionary<string, List<CardStats>>()
        };

        foreach (var resp in responses)
        {
            if (resp.Data == null) continue;

            foreach (var kvp in resp.Data)
            {
                if (!merged.Data.ContainsKey(kvp.Key))
                {
                    merged.Data[kvp.Key] = new List<CardStats>();
                    merged.Characters.Add(kvp.Key);
                }
                merged.Data[kvp.Key].AddRange(kvp.Value);
            }
        }

        // 对每个角色按 skada_score 降序排列并重新计算 rank
        foreach (var kvp in merged.Data)
        {
            var sorted = kvp.Value.OrderByDescending(c => c.SkadaScore).ToList();
            for (int i = 0; i < sorted.Count; i++)
            {
                sorted[i].Rank = i + 1;
            }
            kvp.Value.Clear();
            kvp.Value.AddRange(sorted);
        }

        return merged;
    }

    private void LoadResponse(CardStatsResponse response)
    {
        _statsMap.Clear();
        foreach (var kvp in response.Data)
        {
            var character = kvp.Key;
            foreach (var card in kvp.Value)
            {
                card.Character = character;
                var key = $"{character}:{card.CardId}";
                _statsMap[key] = card;
                // 添加中文名索引
                if (!string.IsNullOrEmpty(card.DisplayNameZh))
                {
                    var zhKey = $"{character}:{card.DisplayNameZh}";
                    _zhNameMap[zhKey] = card;
                }
            }
        }
    }

    public CardStats? GetStats(string character, string cardId)
    {
        if (!_loaded)
        {
            LogWarning($"未加载数据，查询失败: {character}:{cardId}");
            return null;
        }

        // 1. 先尝试直接用 cardId 匹配
        var key = $"{character}:{cardId}";
        if (_statsMap.TryGetValue(key, out var stats))
        {
            return stats;
        }

        // 2. 尝试用中文名匹配（游戏返回中文卡牌名）
        var zhKey = $"{character}:{cardId}";
        if (_zhNameMap.TryGetValue(zhKey, out stats))
        {
            return stats;
        }

        // 查询未命中（正常现象，新卡牌）
        if (_statsMap.Count > 0)
        {
            Log($"查询未命中: {key}");
        }
        return null;
    }

    private static string GetLogDir()
    {
        string executablePath = Godot.OS.GetExecutablePath();
        string directoryName = Path.GetDirectoryName(executablePath) ?? "";
        return Path.Combine(directoryName, "mods", "STS2Agent", "logs");
    }

    private void Log(string message)
    {
        lock (_logLock)
        {
            try
            {
                var logEntry = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [CardStatsService] {message}";
                File.AppendAllText(_logFilePath, logEntry + Environment.NewLine);
            }
            catch { }
        }
    }

    private void LogWarning(string message)
    {
        Log($"[WARN] {message}");
    }

    private void LogError(string message, Exception? ex = null)
    {
        var fullMsg = ex != null ? $"{message}: {ex.Message}" : message;
        Log($"[ERROR] {fullMsg}");
    }
}
