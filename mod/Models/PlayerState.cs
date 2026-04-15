namespace STS2Agent.Models;

public class PlayerState
{
    public int CurrentHealth { get; set; }
    public int MaxHealth { get; set; }
    public int Block { get; set; }
    public int Energy { get; set; }
    public int MaxEnergy { get; set; }
    public int Stars { get; set; }
    public int Gold { get; set; }
    public int PotionCount { get; set; }

    public int OrbCount { get; set; }
    public int OrbCapacity { get; set; }
    public List<OrbInfo> Orbs { get; set; } = new();

    public List<string> Hand { get; set; } = new();
    public List<string> DrawPile { get; set; } = new();
    public List<string> DiscardPile { get; set; } = new();
    public List<string> ExhaustPile { get; set; } = new();
    public List<string> Powers { get; set; } = new();
}

public class OrbInfo
{
    public string Name { get; set; } = string.Empty;
}
