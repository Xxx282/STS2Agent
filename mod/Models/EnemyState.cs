namespace STS2Agent.Models;

public class EnemyState
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int CurrentHealth { get; set; }
    public int MaxHealth { get; set; }
    public int Block { get; set; }
    public string Intent { get; set; } = "Unknown";
    public int IntentAmount { get; set; }
    public List<string> Powers { get; set; } = new();
    public int MoveHistory { get; set; }
}
