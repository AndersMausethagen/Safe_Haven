using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Puts idle creatures far from the player to sleep: their AI, NavMeshAgent and animation stop running until the player
/// comes back within wake distance, something hits them, or a noise (WorldNoise) reaches them. The distances are global raid rules (RaidGlobalSettings). Lives next to the RaidDirector, which adds it when missing.
/// Creatures that are chasing, fighting, returning or roaming are never put to sleep, nor those marked KeepAwake.
/// </summary>
[RequireComponent(typeof(RaidDirector))]
public sealed class CreatureSleep : MonoBehaviour
{
    private const float CheckInterval = 0.5f;

    private sealed class Sleeper
    {
        public MonsterAI Ai;
        public NavMeshAgent Agent;
        public Animator Animator;
        public MonsterAnimationDriver Driver;
        public Damageable Damageable;
        public System.Action<DamageInfo, DamageResult> OnDamaged;
    }

    private readonly Dictionary<MonsterAI, Sleeper> sleepers = new Dictionary<MonsterAI, Sleeper>();
    private readonly List<MonsterAI> scratch = new List<MonsterAI>();
    private readonly List<Vector3> players = new List<Vector3>();
    private RaidDirector director;
    private float nextCheck;

    public int SleepingCount => sleepers.Count;

    public bool IsAsleep(MonsterAI ai) => ai != null && sleepers.ContainsKey(ai);

    /// <summary>Wakes <paramref name="ai"/> if this has it asleep: what wakes a creature itself (a boss rising) asks here, as its body
    /// (agent, animator) is this one's to switch. A boss woken by setting its AI on stood up frozen, its agent and animation still off.</summary>
    public void WakeNow(MonsterAI ai) => Wake(ai, null);

    private void Awake()
    {
        director = GetComponent<RaidDirector>();
    }

    private void OnEnable()
    {
        WorldNoise.Emitted += OnNoise;
    }

    private void OnDisable()
    {
        WorldNoise.Emitted -= OnNoise;
    }

    private void OnNoise(Vector3 position, float radius)
    {
        WakeWithin(position, radius);
    }

    private void OnDestroy()
    {
        foreach (Sleeper sleeper in sleepers.Values)
        {
            if (sleeper.Damageable != null)
            {
                sleeper.Damageable.Damaged -= sleeper.OnDamaged;
            }
        }
        sleepers.Clear();
    }

    private void Update()
    {
        if (Time.time < nextCheck || director == null || director.Player == null)
        {
            return;
        }
        nextCheck = Time.time + CheckInterval;

        // Every player up on this raid, friends too (RaidPlayers): by the host's own alone, a guest exploring apart walked past
        // camps asleep around them.
        RaidPlayers.Positions(director.Player, players);
        RaidGlobalSettings rules = RaidGlobalSettings.Current;
        float sleepSqr = rules.SleepDistance * rules.SleepDistance;
        float wakeSqr = rules.WakeDistance * rules.WakeDistance;

        scratch.Clear();
        scratch.AddRange(sleepers.Keys);
        foreach (MonsterAI ai in scratch)
        {
            if (ai == null)
            {
                sleepers.Remove(ai);
            }
            else if (ai.KeepAwake || RaidPlayers.NearestFlatSqr(players, ai.transform.position) < wakeSqr)
            {
                Wake(ai, null);
            }
        }

        foreach (MonsterAI ai in MonsterAI.All)
        {
            if (ai != null && ai.enabled && !sleepers.ContainsKey(ai) && ai.CurrentState == MonsterAI.State.Idle
                && !ai.IsRoaming && !ai.KeepAwake && RaidPlayers.NearestFlatSqr(players, ai.transform.position) > sleepSqr)
            {
                Sleep(ai);
            }
        }
    }

    private void Sleep(MonsterAI ai)
    {
        var sleeper = new Sleeper
        {
            Ai = ai,
            Agent = ai.GetComponent<NavMeshAgent>(),
            Animator = ai.GetComponentInChildren<Animator>(),
            Driver = ai.GetComponentInChildren<MonsterAnimationDriver>(),
            Damageable = ai.GetComponent<Damageable>(),
        };
        sleeper.OnDamaged = (damage, result) => Wake(ai, damage.Source);

        // The AI goes first so it never runs a frame with its agent switched off.
        ai.enabled = false;
        SetEnabled(sleeper.Driver, false);
        SetEnabled(sleeper.Animator, false);
        SetEnabled(sleeper.Agent, false);
        if (sleeper.Damageable != null)
        {
            sleeper.Damageable.Damaged += sleeper.OnDamaged;
        }
        sleepers.Add(ai, sleeper);
    }

    /// <summary>Wakes every sleeper within the radius (a loud noise) and returns how many woke.</summary>
    public int WakeWithin(Vector3 position, float radius)
    {
        float radiusSqr = radius * radius;
        scratch.Clear();
        foreach (MonsterAI ai in sleepers.Keys)
        {
            if (ai != null && FlatSqr(ai.transform.position - position) <= radiusSqr)
            {
                scratch.Add(ai);
            }
        }
        foreach (MonsterAI ai in scratch)
        {
            Wake(ai, null);
        }
        return scratch.Count;
    }

    private void Wake(MonsterAI ai, Combatant attacker)
    {
        if (!sleepers.TryGetValue(ai, out Sleeper sleeper))
        {
            return;
        }
        sleepers.Remove(ai);
        if (sleeper.Damageable != null)
        {
            sleeper.Damageable.Damaged -= sleeper.OnDamaged;
        }
        if (ai == null)
        {
            return;
        }

        SetEnabled(sleeper.Agent, true);
        SetEnabled(sleeper.Animator, true);
        SetEnabled(sleeper.Driver, true);
        ai.enabled = true;

        // The AI missed this hit while it was switched off, so pass the attacker on directly.
        if (attacker != null && ai.CurrentState != MonsterAI.State.Dead)
        {
            IDamageable target = attacker.GetComponent<IDamageable>();
            if (target != null)
            {
                ai.Engage(target);
            }
        }
    }

    private static void SetEnabled(Behaviour behaviour, bool value)
    {
        if (behaviour != null)
        {
            behaviour.enabled = value;
        }
    }

    private static float FlatSqr(Vector3 offset)
    {
        offset.y = 0f;
        return offset.sqrMagnitude;
    }
}
