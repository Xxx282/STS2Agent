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
    public string Name { get; set; } = "";
    public int Cost { get; set; }
    public string Rarity { get; set; } = "Unknown";
    public string Type { get; set; } = "Unknown";
    public bool IsUpgraded { get; set; }
}
