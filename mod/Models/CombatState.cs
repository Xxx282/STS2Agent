namespace STS2Agent.Models;

public class CombatState
{
    public int Turn { get; set; }
    public string Phase { get; set; } = "Unknown";
    public bool IsPlayerTurn { get; set; }
    public bool CanPlayCard { get; set; }
    public bool CanEndTurn { get; set; }
    public int CardsPlayedThisTurn { get; set; }
    public int DamageDealtThisCombat { get; set; }
    public int DamageTakenThisCombat { get; set; }
}
