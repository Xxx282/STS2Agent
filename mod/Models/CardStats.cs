namespace STS2Agent.Models;

public class CardStats
{
    public string CardId { get; set; } = "";
    public string Character { get; set; } = "";
    public float PickRate { get; set; }
    public float WinRateDelta { get; set; }
    public float SkadaScore { get; set; }
    public int Rank { get; set; }
    public string Confidence { get; set; } = "low";
    public DisplayName? DisplayName { get; set; }

    // 持有实力：牌组里有vs没有的胜率差（逆方差加权）
    // 远程数据没有时 fallback 到 WinRateDelta（选卡建议）
    public float HoldStrength { get; set; }

    public string DisplayNameZh => DisplayName?.Zh ?? "";
    public string DisplayNameEn => DisplayName?.En ?? "";
}

public class DisplayName
{
    public string Zh { get; set; } = "";
    public string En { get; set; } = "";
}

public class CardStatsResponse
{
    public int Version { get; set; }
    public string UpdatedAt { get; set; } = "";
    public List<string> Characters { get; set; } = new();
    public Dictionary<string, List<CardStats>> Data { get; set; } = new();
}
