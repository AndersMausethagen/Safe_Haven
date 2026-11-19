using UnityEngine;

/// <summary>
/// Keeps the global mode in step with the player's fighting: attacking or being attacked enters GameState.Combat, and
/// only a stretch with no blows and no creature after the player's side nearby (within the technique settings' Fight
/// Hold Range: after the player, a companion, or in co-op a friend fighting beside them) returns to Exploration
/// (StrikeRules.FightIsOver). The quiet stretch also counts from entering Combat, so a manual
/// toggle returns to Exploration when left idle. Plain C#, owned by <see cref="PlayerCombat"/>.
/// </summary>
public sealed class PlayerCombatMode
{
    // How often the creatures are asked whether one is after the player, while the quiet stretch could end the fight.
    private const float HuntCheckInterval = 0.25f;

    private readonly Transform player;
    private readonly System.Func<float> clock;
    private readonly System.Func<Vector3, float, bool> huntedNear;
    private GameStateManager gameState;
    private float lastActivityTime;
    private float nextHuntCheck;
    private bool hunted;

    /// <param name="clock">The time now: Time.time unless given another (tests).</param>
    /// <param name="huntedNear">Whether a creature near a point (within a range) is after the player's side:
    /// MonsterAI.AnyHuntingPlayerSideWithin unless given another (tests).</param>
    public PlayerCombatMode(Transform player, System.Func<float> clock = null, System.Func<Vector3, float, bool> huntedNear = null)
    {
        this.player = player;
        this.clock = clock ?? (() => Time.time);
        this.huntedNear = huntedNear ?? MonsterAI.AnyHuntingPlayerSideWithin;
    }

    /// <summary>Starts following the game's mode (PlayerCombat.OnEnable).</summary>
    public void Attach(Object context)
    {
        gameState = GameStateManager.Current;
        if (gameState == null)
        {
            Debug.LogWarning("PlayerCombat found no GameStateManager; attacking won't change the game mode.", context);
            return;
        }

        gameState.GameStateChanged += OnGameStateChanged;
        lastActivityTime = clock();
    }

    public void Detach()
    {
        if (gameState != null)
        {
            gameState.GameStateChanged -= OnGameStateChanged;
            gameState = null;
        }
    }

    /// <summary>Enters (or stays in) GameState.Combat: the player attacked or was attacked.</summary>
    public void Enter()
    {
        lastActivityTime = clock();
        if (gameState != null)
        {
            gameState.TrySetState(GameState.Combat);
        }
    }

    /// <summary>The fight is won (CombatFeedback: the last foe near fell): back to Exploration now, not after the quiet stretch.</summary>
    public void EndNow()
    {
        if (gameState != null && gameState.CurrentState == GameState.Combat)
        {
            gameState.TrySetState(GameState.Exploration);
        }
    }

    /// <summary>A blow landed: the fight goes on.</summary>
    public void NoteActivity() => lastActivityTime = clock();

    /// <summary>Returns to Exploration once the fight is over: <paramref name="timeout"/> seconds of quiet (0 stays in Combat).</summary>
    public void Tick(float timeout)
    {
        if (gameState != null && gameState.CurrentState == GameState.Combat
            && StrikeRules.FightIsOver(clock() - lastActivityTime, timeout, IsHunted()))
        {
            gameState.TrySetState(GameState.Exploration);
        }
    }

    private void OnGameStateChanged(GameState previous, GameState state)
    {
        if (state == GameState.Combat)
        {
            lastActivityTime = clock();
        }
    }

    // Looked at a few times a second, only while the quiet stretch could end the fight.
    private bool IsHunted()
    {
        if (clock() < nextHuntCheck)
        {
            return hunted;
        }

        nextHuntCheck = clock() + HuntCheckInterval;
        hunted = huntedNear(player.position, CombatTechniqueSettings.Current.FightHoldRange);
        return hunted;
    }
}
