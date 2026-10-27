using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plays a ward breach in Safe Haven (GDD §6.6–6.9): warning → preparation → defense → recovery. Once the player has
/// come home from enough raids, the breach turns Pending and the player is warned; the marked path appears. The player
/// prepares (builds a palisade, eats, repairs) and starts the defense at the Heart Tree. Enemies then come along the path,
/// hunting only the player, while buildings are protected. Defeating them all wins the breach and its reward; falling
/// ends it without other consequences, and it stays Pending to try again.
/// <para>
/// Where it belongs: one per Safe Haven scene, with a spawn point at the start of the path and a path marker object.
/// The breach's numbers and texts live in its WardBreachDefinition; its status in SettlementState (saved).
/// </para>
/// </summary>
[DisallowMultipleComponent]
public sealed class WardBreachDirector : MonoBehaviour
{
    [SerializeField] private WardBreachDefinition definition;
    [Tooltip("Where the enemies appear: the outer end of the marked path.")]
    [SerializeField] private Transform spawnPoint;
    [Tooltip("Shown while the breach is pending or under way, so the player sees where the enemies will come from.")]
    [SerializeField] private GameObject pathMarker;
    [Tooltip("Seconds after arriving in Safe Haven before the warning appears, so it isn't lost among other messages.")]
    [SerializeField, Min(0f)] private float warningDelay = 4f;
    [Tooltip("Enemies spread this far around the spawn point, in meters.")]
    [SerializeField, Min(0f)] private float spawnSpread = 2f;

    private readonly List<Damageable> attackers = new List<Damageable>();
    private readonly List<Damageable> shielded = new List<Damageable>();
    private Damageable playerHealth;
    private bool spawning;

    /// <summary>The breach director in the loaded scene, or null.</summary>
    public static WardBreachDirector Current { get; private set; }

    public WardBreachDefinition Definition => definition;
    public bool IsActive { get; private set; }
    public bool IsPending => definition != null && !IsActive && GameSession.Settlement.Breach == BreachStatus.Pending;

    private void Awake()
    {
        Current = this;
    }

    private void OnDestroy()
    {
        if (Current == this)
        {
            Current = null;
        }

        if (playerHealth != null)
        {
            playerHealth.Died -= OnPlayerDied;
        }
    }

    private void OnEnable()
    {
        GameSession.Settlement.Changed += UpdateMarker;
        UpdateMarker();
    }

    private void OnDisable()
    {
        GameSession.Settlement.Changed -= UpdateMarker;
    }

    private IEnumerator Start()
    {
        if (Player.Current != null)
        {
            playerHealth = Player.Current.Health;
            playerHealth.Died += OnPlayerDied;
        }

        yield return new WaitForSeconds(warningDelay);
        SettlementState settlement = GameSession.Settlement;
        if (definition != null && settlement.Breach == BreachStatus.Dormant && settlement.SuccessfulExtractions >= definition.AvailableAfterExtractions)
        {
            settlement.SetBreach(BreachStatus.Pending);
            GameMessages.Post(definition.WarningText, MessageKind.Warning);
        }
    }

    /// <summary>Starts the defense. Called from the Heart Tree's menu.</summary>
    public void Begin()
    {
        if (!IsPending || definition.EnemyPrefab == null || playerHealth == null || !playerHealth.IsAlive)
        {
            return;
        }

        IsActive = true;
        UpdateMarker();
        ProtectBuildings(true);
        GameMessages.Post($"{definition.DisplayName}: they are coming. Hold the clearing!", MessageKind.Warning);
        StartCoroutine(SpawnAttackers());
    }

    private IEnumerator SpawnAttackers()
    {
        spawning = true;
        for (int i = 0; i < definition.EnemyCount && IsActive; i++)
        {
            Spawn();
            if (i < definition.EnemyCount - 1)
            {
                yield return new WaitForSeconds(definition.SecondsBetweenEnemies);
            }
        }

        spawning = false;
        CheckWon();
    }

    private void Spawn()
    {
        Vector3 origin = spawnPoint != null ? spawnPoint.position : transform.position;
        Vector2 offset = Random.insideUnitCircle * spawnSpread;
        Vector3 position = origin + new Vector3(offset.x, 0f, offset.y);
        // Breach enemies aren't part of the saved scene and carry no loot (the breach has its own reward).
        GameObject enemy = TransientSpawn.Spawn(definition.EnemyPrefab, position,
            spawnPoint != null ? spawnPoint.rotation : Quaternion.identity, false);

        if (enemy.TryGetComponent(out Combatant combatant))
        {
            StatSheet stats = combatant.Stats;
            stats.AddModifier(new StatModifier(StatType.MaxHealth, StatModifierOp.Percent, definition.EnemyHealthMultiplier - 1f, this));
            stats.AddModifier(new StatModifier(StatType.DamageBonus, StatModifierOp.Flat, definition.EnemyPowerMultiplier - 1f, this));
        }

        if (enemy.TryGetComponent(out Damageable damageable))
        {
            attackers.Add(damageable);
            damageable.Died += OnAttackerDied;
        }

        if (enemy.TryGetComponent(out MonsterAI ai))
        {
            ai.PlayerOnly = true;
            ai.Relentless = true;
            ai.Engage(playerHealth);
        }
    }

    private void OnAttackerDied(Damageable victim, DamageInfo killingBlow)
    {
        victim.Died -= OnAttackerDied;
        CheckWon();
    }

    private void CheckWon()
    {
        if (!IsActive || spawning)
        {
            return;
        }

        foreach (Damageable attacker in attackers)
        {
            if (attacker != null && attacker.IsAlive)
            {
                return;
            }
        }

        End();
        GameSession.Settlement.SetBreach(BreachStatus.Won);
        GameObject player = Player.Current != null ? Player.Current.gameObject : null;
        MaterialPool pool = MaterialPool.For(player);
        foreach (ResourceAmount reward in definition.Reward)
        {
            pool.Add(reward.Resource, reward.Amount);
        }

        string rewards = definition.Reward.Count > 0 ? $" Reward: {AmountText.Describe(definition.Reward)}." : "";
        GameMessages.Post(definition.WonText + rewards, MessageKind.Gain);
        if (definition.WonEvent != null)
        {
            GameEvents.Raise(definition.WonEvent, player);
        }
    }

    private void OnPlayerDied(Damageable victim, DamageInfo killingBlow)
    {
        if (!IsActive)
        {
            return;
        }

        End();
        foreach (Damageable attacker in attackers)
        {
            if (attacker != null)
            {
                Destroy(attacker.gameObject);
            }
        }

        GameMessages.Post(definition.FailedText, MessageKind.Warning);
    }

    private void End()
    {
        IsActive = false;
        spawning = false;
        StopAllCoroutines();
        ProtectBuildings(false);
        foreach (Damageable attacker in attackers)
        {
            if (attacker != null)
            {
                attacker.Died -= OnAttackerDied;
            }
        }

        attackers.Clear();
        UpdateMarker();
    }

    // Design decision: breach enemies attack only the player, so buildings can't be harmed while it lasts.
    private void ProtectBuildings(bool protect)
    {
        if (!protect)
        {
            foreach (Damageable building in shielded)
            {
                if (building != null)
                {
                    building.RemoveInvulnerability(this);
                }
            }

            shielded.Clear();
            return;
        }

        foreach (BuildingObject building in BuildingObject.Active)
        {
            if (building.TryGetComponent(out Damageable damageable))
            {
                damageable.AddInvulnerability(this);
                shielded.Add(damageable);
            }
        }
    }

    private void UpdateMarker()
    {
        if (pathMarker != null)
        {
            pathMarker.SetActive(IsActive || IsPending);
        }
    }
}
