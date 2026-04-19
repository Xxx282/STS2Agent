namespace STS2Agent.Models;

public class RewardCardInfo
{
    public string Name { get; set; } = "";
    public string? DisplayNameZh { get; set; }
    public int? Rank { get; set; }
    public float? PickRate { get; set; }
    public float? WinRateDelta { get; set; }
    public float? SkadaScore { get; set; }
    public string? Confidence { get; set; }
    public string? CardType { get; set; }
    public string? Cost { get; set; }
    public string? Description { get; set; }
}
