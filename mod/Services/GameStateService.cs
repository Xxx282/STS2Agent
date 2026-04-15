using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using STS2Agent.Models;
using Godot;

namespace STS2Agent.Services;

public class GameStateService
{
    private GameState _currentState = new();
    private readonly object _lock = new();
    private Assembly? _gameAssembly;
    private Dictionary<string, Type?> _typeCache = new();
    private readonly string _logFilePath;
    private readonly object _logLock = new();

    private FieldInfo? _runManagerStateField;
    private PropertyInfo? _runManagerIsInProgressProp;
    private PropertyInfo? _combatManagerIsInProgressProp;
    private bool _reflectionInitialized;

    public event Action<GameState>? OnStateChanged;

    private void Log(string message)
    {
        lock (_logLock)
        {
            try
            {
                var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [GameStateService] {message}";
                File.AppendAllText(_logFilePath, logEntry + System.Environment.NewLine);
            }
            catch { }
        }
    }

    private void LogError(string message, Exception? ex = null)
    {
        var fullMsg = ex != null ? $"{message}: {ex.Message}\n{ex.StackTrace}" : message;
        Log($"[ERROR] {fullMsg}");
    }

    public GameState GetCurrentState()
    {
        lock (_lock)
        {
            return CloneState(_currentState);
        }
    }

    public GameStateService()
    {
        var debugDir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "STS2Agent");
        System.IO.Directory.CreateDirectory(debugDir);
        _logFilePath = System.IO.Path.Combine(debugDir, "debug.log");

        Log("=== GameStateService init ===");
        try
        {
            var assembly = GameAssembly;
            InitializeReflection(assembly);
            Log($"GameStateService ready, Assembly={assembly?.GetName()?.Name ?? "null"}");
        }
        catch (Exception ex)
        {
            LogError("GameStateService init failed", ex);
        }
    }

    private void InitializeReflection(Assembly assembly)
    {
        if (_reflectionInitialized) return;

        var runManagerType = GetCachedType("MegaCrit.Sts2.Core.Runs.RunManager");
        if (runManagerType != null)
        {
            _runManagerIsInProgressProp = runManagerType.GetProperty("IsInProgress", BindingFlags.Public | BindingFlags.Static);
            _runManagerStateField = runManagerType.GetField("State", BindingFlags.NonPublic | BindingFlags.Instance);
            Log($"Init: RunManager.IsInProgress={_runManagerIsInProgressProp != null}, State={_runManagerStateField != null}");
        }

        var combatManagerType = GetCachedType("MegaCrit.Sts2.Core.Combat.CombatManager");
        if (combatManagerType != null)
        {
            _combatManagerIsInProgressProp = combatManagerType.GetProperty("IsInProgress", BindingFlags.Public | BindingFlags.Static);
            Log($"Init: CombatManager.IsInProgress={_combatManagerIsInProgressProp != null}");
        }

        _reflectionInitialized = true;
    }

    public void Update()
    {
        try
        {
            var newState = CaptureGameState();
            lock (_lock)
            {
                if (HasStateChanged(_currentState, newState))
                {
                    _currentState = newState;
                    OnStateChanged?.Invoke(CloneState(_currentState));
                }
            }
        }
        catch (Exception ex)
        {
            LogError("Update failed", ex);
        }
    }

    private GameState CaptureGameState()
    {
        var inGame = InGame();
        var inCombat = InCombat();
        var floor = GetFloor();
        var turn = GetTurn();

        var state = new GameState
        {
            InGame = inGame,
            InCombat = inCombat,
            Floor = floor,
            Turn = turn,
            Timestamp = DateTime.UtcNow
        };

        if (state.InGame)
        {
            object? combatState = inCombat ? GetCombatStateRaw() : null;
            state.Player = GetPlayerState(combatState);
            state.Enemies = GetEnemyStates(combatState);
            if (state.InCombat)
            {
                state.Combat = GetCombatState();
            }
        }

        return state;
    }

    private Assembly GameAssembly
    {
        get
        {
            if (_gameAssembly == null)
            {
                var currentAssemblies = AppDomain.CurrentDomain.GetAssemblies();
                _gameAssembly = currentAssemblies.FirstOrDefault(a => a.GetName().Name == "sts2");
                if (_gameAssembly == null)
                {
                    try
                    {
                        var dllPath = System.IO.Path.Combine("libs", "sts2.dll");
                        if (File.Exists(dllPath))
                            _gameAssembly = Assembly.LoadFrom(dllPath);
                        else
                            _gameAssembly = Assembly.LoadFrom("libs/sts2.dll");
                    }
                    catch (Exception ex)
                    {
                        LogError("Failed to load sts2.dll", ex);
                        _gameAssembly = Assembly.GetExecutingAssembly();
                    }
                }
            }
            return _gameAssembly;
        }
    }

    private Type? GetCachedType(string fullName)
    {
        if (!_typeCache.TryGetValue(fullName, out Type? cachedType))
        {
            cachedType = GameAssembly?.GetType(fullName);
            _typeCache[fullName] = cachedType;
            if (cachedType == null)
                Log($"Type not found: {fullName}");
        }
        return cachedType;
    }

    private object? GetSingleton(string typeName)
    {
        try
        {
            var type = GetCachedType(typeName);
            if (type == null) return null;
            var prop = type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            return prop?.GetValue(null);
        }
        catch (Exception ex)
        {
            Log($"GetSingleton({typeName}): {ex.Message}");
            return null;
        }
    }

    private object? GetProperty(object? obj, string name)
    {
        if (obj == null) return null;
        return obj.GetType().GetProperty(name)?.GetValue(obj);
    }

    private T? GetProperty<T>(object? obj, string name)
    {
        var v = GetProperty(obj, name);
        return v is T t ? t : default;
    }

    private object? GetField(object? obj, string name)
    {
        if (obj == null) return null;
        return obj.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(obj);
    }

    private object? CallMethod(object? obj, string name)
    {
        if (obj == null) return null;
        return obj.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(obj, null);
    }

    private bool InGame()
    {
        try
        {
            var rm = GetSingleton("MegaCrit.Sts2.Core.Runs.RunManager");
            if (rm == null) return false;
            if (_runManagerIsInProgressProp != null)
                return _runManagerIsInProgressProp.GetValue(null) is bool b && b;
            return GetProperty<bool?>(rm, "IsInProgress") ?? false;
        }
        catch (Exception ex)
        {
            LogError("InGame failed", ex);
            return false;
        }
    }

    private bool InCombat()
    {
        try
        {
            var cm = GetSingleton("MegaCrit.Sts2.Core.Combat.CombatManager");
            if (cm == null) return false;
            if (_combatManagerIsInProgressProp != null)
                return _combatManagerIsInProgressProp.GetValue(null) is bool b && b;
            return GetProperty<bool?>(cm, "IsInProgress") ?? false;
        }
        catch (Exception ex)
        {
            LogError("InCombat failed", ex);
            return false;
        }
    }

    private int GetFloor()
    {
        try
        {
            var rm = GetSingleton("MegaCrit.Sts2.Core.Runs.RunManager");
            if (rm == null) return 0;
            var rs = GetField(rm, "State");
            if (rs == null) return 0;
            // ActFloor: ?? act ????????????? row+1?
            // TotalFloor: ????????
            var actFloor = GetProperty<int?>(rs, "ActFloor") ?? 0;
            var totalFloor = GetProperty<int?>(rs, "TotalFloor") ?? 0;
            // ?????????TotalFloor ????? 1 ???
            return Math.Max(actFloor, totalFloor);
        }
        catch (Exception ex)
        {
            LogError("GetFloor failed", ex);
            return 0;
        }
    }

    /// ???? CombatState ???? GetPlayerState / GetEnemyStates ??
    private object? GetCombatStateRaw()
    {
        try
        {
            var cm = GetSingleton("MegaCrit.Sts2.Core.Combat.CombatManager");
            if (cm == null) return null;
            // DebugOnlyGetState() ?? CombatState?
            return CallMethod(cm, "DebugOnlyGetState");
        }
        catch (Exception ex)
        {
            LogError("GetCombatStateRaw failed", ex);
            return null;
        }
    }

    private int GetTurn()
    {
        var cs = GetCombatStateRaw();
        if (cs == null) return 0;
        return GetProperty<int?>(cs, "RoundNumber") ?? 0;
    }

    private CombatState GetCombatState()
    {
        var combat = new CombatState();
        try
        {
            var cs = GetCombatStateRaw();
            if (cs == null) return combat;
            combat.Turn = GetProperty<int?>(cs, "RoundNumber") ?? 0;

            // CombatState.CurrentSide -- CombatSide enum (Player=0, Enemy=1)
            var currentSide = GetProperty<object>(cs, "CurrentSide");
            combat.IsPlayerTurn = currentSide?.ToString() == "Player";
            combat.CanPlayCard = combat.IsPlayerTurn;
            combat.CanEndTurn = combat.IsPlayerTurn;
        }
        catch (Exception ex)
        {
            LogError("GetCombatState failed", ex);
        }
        return combat;
    }

    private PlayerState GetPlayerState(object? combatState)
    {
        var player = new PlayerState();
        try
        {
            // 获取 PlayerCombatState 引用
            object? playerRef = null;
            object? pcs = null;

            if (combatState != null)
            {
                var playerCreatures = GetProperty<object>(combatState, "PlayerCreatures");
                if (playerCreatures != null)
                {
                    foreach (var creature in (System.Collections.IEnumerable)playerCreatures)
                    {
                        if (creature == null) continue;
                        // Creature.CurrentHp / MaxHp / Block
                        player.CurrentHealth = GetProperty<int?>(creature, "CurrentHp") ?? 0;
                        player.MaxHealth = GetProperty<int?>(creature, "MaxHp") ?? 0;
                        player.Block = GetProperty<int?>(creature, "Block") ?? 0;

                        // Creature.Powers
                        player.Powers = GetCreaturePowers(creature);

                        // PlayerCombatState -> Energy / Hand / DrawPile / DiscardPile / ExhaustPile / Stars / Orbs
                        playerRef = GetProperty<object>(creature, "Player");
                        pcs = GetProperty<object>(playerRef, "PlayerCombatState");
                        if (pcs != null)
                        {
                            player.Energy = GetProperty<int?>(pcs, "Energy") ?? 0;
                            player.MaxEnergy = GetProperty<int?>(pcs, "MaxEnergy") ?? 3;
                            player.Stars = GetProperty<int?>(pcs, "Stars") ?? 0;
                            player.Hand = GetCardNamesFromPile(pcs, "Hand");
                            player.DrawPile = GetCardNamesFromPile(pcs, "DrawPile");
                            player.DiscardPile = GetCardNamesFromPile(pcs, "DiscardPile");
                            player.ExhaustPile = GetCardNamesFromPile(pcs, "ExhaustPile");

                            // OrbQueue -> Orbs / Capacity
                            var orbQueue = GetProperty<object>(pcs, "OrbQueue");
                            if (orbQueue != null)
                            {
                                player.OrbCount = GetProperty<int?>(orbQueue, "Count") ?? 0;
                                player.OrbCapacity = GetProperty<int?>(orbQueue, "Capacity") ?? 0;
                                player.Orbs = GetOrbInfos(orbQueue);
                            }
                        }
                        break;
                    }
                }
            }

            // 从 RunState.Players[0] 获取 Gold / PotionCount (非战斗时也获取)
            var rm = GetSingleton("MegaCrit.Sts2.Core.Runs.RunManager");
            var rs = GetField(rm, "State");
            var players = GetProperty<object>(rs, "Players");
            if (players != null)
            {
                foreach (var p in (System.Collections.IEnumerable)players)
                {
                    if (p == null) continue;
                    // 如果 combatState 为空或 pcs 为 null，从这里获取生命值
                    if (combatState == null || pcs == null)
                    {
                        var creature = GetProperty<object>(p, "Creature");
                        if (creature != null)
                        {
                            player.CurrentHealth = GetProperty<int?>(creature, "CurrentHp") ?? 0;
                            player.MaxHealth = GetProperty<int?>(creature, "MaxHp") ?? 0;
                            player.Block = GetProperty<int?>(creature, "Block") ?? 0;
                        }
                    }
                    // Gold / PotionCount
                    player.Gold = GetProperty<int?>(p, "Gold") ?? 0;
                    player.PotionCount = GetProperty<int?>(p, "MaxPotionCount") ?? 0;
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            LogError("GetPlayerState failed", ex);
        }
        return player;
    }

    private List<OrbInfo> GetOrbInfos(object? orbQueue)
    {
        var orbs = new List<OrbInfo>();
        if (orbQueue == null) return orbs;
        var orbsObj = GetProperty<object>(orbQueue, "Orbs");
        if (orbsObj == null) return orbs;

        foreach (var orb in (System.Collections.IEnumerable)orbsObj)
        {
            if (orb == null) continue;
            var name = GetProperty<string?>(orb, "Name");
            if (!string.IsNullOrEmpty(name))
            {
                orbs.Add(new OrbInfo { Name = name });
            }
        }
        return orbs;
    }

    private List<string> GetCardNamesFromPile(object? playerCombatState, string pileName)
    {
        if (playerCombatState == null) return new List<string>();
        var pile = GetProperty<object>(playerCombatState, pileName);
        if (pile == null) return new List<string>();

        // CardPile.Cards 本身就是 IReadOnlyList<CardModel>，直接迭代
        var cards = new List<string>();
        foreach (var card in (System.Collections.IEnumerable)pile)
        {
            if (card == null) continue;
            var name = GetProperty<string?>(card, "Name");
            if (!string.IsNullOrEmpty(name)) cards.Add(name);
        }
        return cards;
    }

    private List<string> GetCreaturePowers(object? creature)
    {
        var powers = new List<string>();
        if (creature == null) return powers;
        var powersObj = GetProperty<object>(creature, "Powers");
        if (powersObj == null) return powers;
        foreach (var power in (System.Collections.IEnumerable)powersObj)
        {
            if (power == null) continue;
            var name = GetProperty<string?>(power, "Name");
            if (!string.IsNullOrEmpty(name)) powers.Add(name);
        }
        return powers;
    }

    private List<EnemyState> GetEnemyStates(object? combatState)
    {
        var enemies = new List<EnemyState>();
        if (combatState == null) return enemies;

        try
        {
            var enemiesObj = GetProperty<object>(combatState, "Enemies");
            if (enemiesObj == null) return enemies;

            foreach (var creature in (System.Collections.IEnumerable)enemiesObj)
            {
                if (creature == null) continue;
                var enemy = new EnemyState();

                // Creature.Name -- computed string??????
                enemy.Name = GetProperty<string?>(creature, "Name") ?? "Unknown";

                // Creature.CombatId -- uint?
                var combatId = GetProperty<uint?>(creature, "CombatId");
                enemy.Id = combatId.HasValue ? combatId.Value.ToString() : Guid.NewGuid().ToString();

                // Creature.CurrentHp / MaxHp / Block
                enemy.CurrentHealth = GetProperty<int?>(creature, "CurrentHp") ?? 0;
                enemy.MaxHealth = GetProperty<int?>(creature, "MaxHp") ?? 0;
                enemy.Block = GetProperty<int?>(creature, "Block") ?? 0;

                // Creature.Powers
                enemy.Powers = GetCreaturePowers(creature);

                // ?????Creature.Monster.NextMove.Intents
                var monster = GetProperty<object>(creature, "Monster");
                if (monster != null)
                {
                    var nextMove = GetProperty<object>(monster, "NextMove");
                    if (nextMove != null)
                    {
                        var intents = GetProperty<object>(nextMove, "Intents");
                        if (intents != null)
                        {
                            foreach (var intent in (System.Collections.IEnumerable)intents)
                            {
                                if (intent == null) continue;
                                // IntentType enum: Attack, Defense, Buff, etc.
                                var intentType = GetProperty<object>(intent, "IntentType");
                                enemy.Intent = intentType?.ToString() ?? "Unknown";
                                // AbstractIntent ? Damage / Value ??
                                enemy.IntentAmount = GetProperty<int?>(intent, "Damage")
                                    ?? GetProperty<int?>(intent, "Value")
                                    ?? 0;
                                break; // ????? intent
                            }
                        }
                    }
                }

                enemies.Add(enemy);
            }
        }
        catch (Exception ex)
        {
            LogError("GetEnemyStates failed", ex);
        }
        return enemies;
    }

    private bool HasStateChanged(GameState a, GameState b)
    {
        if (a.InCombat != b.InCombat) return true;
        if (a.Floor != b.Floor) return true;
        if (a.Turn != b.Turn) return true;
        if (a.Player?.CurrentHealth != b.Player?.CurrentHealth) return true;
        if (a.Player?.Energy != b.Player?.Energy) return true;
        return false;
    }

    private GameState CloneState(GameState s)
    {
        return new GameState
        {
            InCombat = s.InCombat,
            InGame = s.InGame,
            Floor = s.Floor,
            Turn = s.Turn,
            GameMode = s.GameMode,
            Timestamp = s.Timestamp,
            Player = s.Player != null ? new PlayerState
            {
                CurrentHealth = s.Player.CurrentHealth,
                MaxHealth = s.Player.MaxHealth,
                Block = s.Player.Block,
                Energy = s.Player.Energy,
                MaxEnergy = s.Player.MaxEnergy,
                Stars = s.Player.Stars,
                Gold = s.Player.Gold,
                PotionCount = s.Player.PotionCount,
                OrbCount = s.Player.OrbCount,
                OrbCapacity = s.Player.OrbCapacity,
                Orbs = s.Player.Orbs.Select(o => new OrbInfo { Name = o.Name }).ToList(),
                Hand = new List<string>(s.Player.Hand),
                DrawPile = new List<string>(s.Player.DrawPile),
                DiscardPile = new List<string>(s.Player.DiscardPile),
                ExhaustPile = new List<string>(s.Player.ExhaustPile),
                Powers = new List<string>(s.Player.Powers)
            } : null,
            Enemies = s.Enemies.Select(e => new EnemyState
            {
                Id = e.Id,
                Name = e.Name,
                CurrentHealth = e.CurrentHealth,
                MaxHealth = e.MaxHealth,
                Block = e.Block,
                Intent = e.Intent,
                IntentAmount = e.IntentAmount,
                Powers = new List<string>(e.Powers)
            }).ToList(),
            Combat = s.Combat != null ? new CombatState
            {
                Turn = s.Combat.Turn,
                Phase = s.Combat.Phase,
                IsPlayerTurn = s.Combat.IsPlayerTurn,
                CanPlayCard = s.Combat.CanPlayCard,
                CanEndTurn = s.Combat.CanEndTurn
            } : null
        };
    }
}
