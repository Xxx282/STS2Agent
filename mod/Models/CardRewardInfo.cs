namespace STS2Agent.Models;

public class CardRewardInfo
{
    public bool IsVisible { get; set; }
    public bool CanReroll { get; set; }
    public bool CanSkip { get; set; }
    public int CardCount { get; set; }
    public string RewardSource { get; set; } = "Unknown";
    public List<RewardCardInfo> Cards { get; set; } = new();
}

public class RewardCardInfo
{
    public string CardId { get; set; } = "";
    public string EnglishId { get; set; } = "";
    public string Name { get; set; } = "";
    public int Cost { get; set; }
    public string Rarity { get; set; } = "Unknown";
    public string Type { get; set; } = "Unknown";
    public bool IsUpgraded { get; set; }

    // 场外统计数据（可空，缓存不可用时为 null）
    public float? PickRate { get; set; }
    public float? WinRateDelta { get; set; }
    public float? SkadaScore { get; set; }
    public float? HoldStrength { get; set; }
    public int? Rank { get; set; }
    public string? DisplayNameZh { get; set; }

    // 卡牌在屏幕上的位置（用于悬停检测）
    public float? ScreenX { get; set; }
    public float? ScreenY { get; set; }
}
