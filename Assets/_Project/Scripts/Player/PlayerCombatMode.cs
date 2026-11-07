using UnityEngine;

/// <summary>
/// Keeps the global mode in step with the player's fighting: attacking or being attacked enters GameState.Combat, and
/// only a stretch with no blows and no creature after the player (within the technique settings' Fight Hold Range)
/// returns to Exploration (StrikeRules.FightIsOver). The quiet stretch also counts from entering Combat, so a manual
/// toggle returns to Exploration when left idle. Plain C#, owned by <see cref="PlayerCombat"/>.
/// </summary>
public sealed class PlayerCombatMode
{
    // How often the creatures are asked whether one is after the player, while the quiet stretch could end the fight.
    private const float HuntCheckInterval = 0.25f;

    private readonly Transform player;
    private readonly Damageable health;
    private GameStateManager gameState;
    private float lastActivityTime;
    private float nextHuntCheck;
    private bool hunted;

    public PlayerCombatMode(Transform player, Damageable health)
    {
        this.player = player;
        this.health = health;
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
        lastActivityTime = Time.time;
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
        lastActivityTime = Time.time;
        if (gameState != null)
        {
            gameState.TrySetState(GameState.Combat);
        }
    }

    /// <summary>A blow landed: the fight goes on.</summary>
    public void NoteActivity() => lastActivityTime = Time.time;

    /// <summary>Returns to Exploration once the fight is over: <paramref name="timeout"/> seconds of quiet (0 stays in Combat).</summary>
    public void Tick(float timeout)
    {
        if (gameState != null && gameState.CurrentState == GameState.Combat
            && StrikeRules.FightIsOver(Time.time - lastActivityTime, timeout, IsHunted()))
        {
            gameState.TrySetState(GameState.Exploration);
        }
    }

    private void OnGameStateChanged(GameState previous, GameState state)
    {
        if (state == GameState.Combat)
        {
            lastActivityTime = Time.time;
        }
    }

    // Looked at a few times a second, only while the quiet stretch could end the fight.
    private bool IsHunted()
    {
        if (Time.time < nextHuntCheck)
        {
            return hunted;
        }

        nextHuntCheck = Time.time + HuntCheckInterval;
        hunted = false;
        float range = CombatTechniqueSettings.Current.FightHoldRange;
        foreach (MonsterAI monster in MonsterAI.Active)
        {
            if (monster != null && health != null && monster.IsEngaging(health)
                && (monster.transform.position - player.position).sqrMagnitude <= range * range)
            {
                hunted = true;
                break;
            }
        }

        return hunted;
    }
}
