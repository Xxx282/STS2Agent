namespace STS2Agent.Models;

public class GameState
{
    public bool InCombat { get; set; }
    public bool InGame { get; set; }
    public int Floor { get; set; }
    public int Turn { get; set; }
    public string GameMode { get; set; } = "Unknown";
    public PlayerState? Player { get; set; }
    public List<EnemyState> Enemies { get; set; } = new();
    public CombatState? Combat { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
