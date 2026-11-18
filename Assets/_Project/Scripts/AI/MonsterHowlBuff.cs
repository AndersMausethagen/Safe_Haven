using UnityEngine;

/// <summary>
/// One timed call per recipient; another call refreshes it, never adds a second modifier. A howl makes it hit harder; a
/// troll-wife's salve mends it over the time; a fossegrim's tune hurries its feet and its blows; stone skin hardens it
/// against every kind of harm but its weakness (Monster Attack › Howl).
/// </summary>
[RequireComponent(typeof(Combatant))]
public sealed class MonsterHowlBuff : MonoBehaviour
{
    private Combatant combatant;
    private Damageable health;
    private MonsterAI ai;
    private float expiresAt;
    private float healPerSecond;
    // What it multiplied into the creature's own speed and waits, taken back out when it ends (an elite's frenzy stays).
    private float speedApplied = 1f;
    private float waitApplied = 1f;

    public float Bonus { get; private set; }
    public bool IsActive => Time.time < expiresAt && (Bonus > 0f || healPerSecond > 0f || speedApplied != 1f || waitApplied != 1f || Toughness > 0f);
    public float Toughness { get; private set; }

    /// <summary>Which call this is: a plain rallying howl (damage only) shares one with every other, so howls refresh
    /// rather than stack, as they always have; a salve, a tune or stone skin each keep their own beside it.</summary>
    public object Key { get; private set; } = Rally;

    private static readonly LeaseKey Rally = new LeaseKey("a rally call");

    /// <summary>The buff on <paramref name="creature"/> for <paramref name="call"/>, added if it has none yet.</summary>
    public static MonsterHowlBuff For(GameObject creature, MonsterAttack call)
    {
        object key = KeyOf(call);
        foreach (MonsterHowlBuff buff in creature.GetComponents<MonsterHowlBuff>())
        {
            if (buff.Key == key)
            {
                return buff;
            }
        }

        MonsterHowlBuff added = creature.AddComponent<MonsterHowlBuff>();
        added.Key = key;
        return added;
    }

    private static object KeyOf(MonsterAttack call) =>
        call == null || (call.healShare <= 0f && call.speedBonus <= 0f && call.hasteBonus <= 0f && call.toughness <= 0f) ? Rally : call;

    private void Awake()
    {
        combatant = GetComponent<Combatant>();
        health = GetComponent<Damageable>();
        ai = GetComponent<MonsterAI>();
    }

    /// <summary>A plain howl: hits harder by <paramref name="bonus"/> for <paramref name="seconds"/>.</summary>
    public void Refresh(float bonus, float seconds) => Apply(bonus, 0f, 0f, 0f, 0f, seconds);

    /// <summary>Everything the call gives (damage, mending, speed, haste, toughness), for its Buff Seconds.</summary>
    public void Refresh(MonsterAttack call) =>
        Apply(call.damageBonus, call.healShare, call.speedBonus, call.hasteBonus, call.toughness, call.buffSeconds);

    private void Apply(float bonus, float healShare, float speedBonus, float hasteBonus, float toughness, float seconds)
    {
        // Plain howls refresh rather than stack. A weaker call while a stronger one holds is let be: the stronger runs its own time,
        // and the weaker (a Rallying elite's aura, 1.5 s every second) takes over when it is done. The same call again keeps the later
        // end. Before, the aura cut a kin's 10 s call short and lowered a boss's bigger call to its own; then, keeping the stronger
        // bonus with the aura's renewed time, a big call near an elite never ended.
        if (Key == Rally && IsActive)
        {
            if (bonus < Bonus - 0.0001f)
            {
                return;
            }

            if (bonus <= Bonus + 0.0001f)
            {
                seconds = Mathf.Max(seconds, expiresAt - Time.time);
            }
        }

        Clear();
        Bonus = bonus;
        Toughness = toughness;
        expiresAt = Time.time + seconds;
        if (bonus > 0f)
        {
            combatant.Stats.AddModifier(new StatModifier(StatType.DamageBonus, StatModifierOp.Flat, bonus, this));
        }

        if (toughness > 0f)
        {
            // Every resistance but the three kinds of blow: a stone troll's weakness to blunt stays a weakness.
            foreach (StatType resistance in new[] { StatType.PhysicalResistance, StatType.FireResistance, StatType.FrostResistance, StatType.HolyResistance })
            {
                combatant.Stats.AddModifier(new StatModifier(resistance, StatModifierOp.Flat, toughness, this));
            }
        }

        healPerSecond = health != null && healShare > 0f ? healShare * health.MaxHealth / Mathf.Max(0.1f, seconds) : 0f;
        if (ai != null)
        {
            speedApplied = 1f + speedBonus;
            waitApplied = 1f - hasteBonus;
            ai.SpeedScale *= speedApplied;
            ai.CooldownScale *= waitApplied;
        }
    }

    private void Update()
    {
        if (Time.time >= expiresAt)
        {
            if (Bonus > 0f || healPerSecond > 0f || speedApplied != 1f || waitApplied != 1f || Toughness > 0f)
            {
                Clear();
            }

            return;
        }

        if (healPerSecond > 0f && health != null && health.IsAlive)
        {
            health.Heal(healPerSecond * Time.deltaTime);
        }
    }

    private void OnDisable() => Clear();

    private void Clear()
    {
        if (combatant != null)
        {
            combatant.Stats.RemoveModifiersFrom(this);
        }

        if (ai != null && (speedApplied != 1f || waitApplied != 1f))
        {
            ai.SpeedScale /= speedApplied;
            ai.CooldownScale /= Mathf.Max(0.05f, waitApplied);
        }

        speedApplied = 1f;
        waitApplied = 1f;
        healPerSecond = 0f;
        Bonus = 0f;
        Toughness = 0f;
    }
}
