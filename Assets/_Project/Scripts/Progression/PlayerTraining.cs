using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Watches the player for practice and reports it to ExperienceTracker, with the amounts in LevelingSettings:
/// <list type="bullet">
/// <item>Melee, Archery and Runecraft: every swing, arrow or spell that hurts a monster pays the share of the monster's
/// worth it took off its health; parries and perfect dodges pay Melee a little, a few times per monster, and every spell
/// cast pays Runecraft a little. Other blows (traps) give Insight.</item>
/// <item>Sneaking: the first blow on an unaware monster, sneaking within sight of a monster that hasn't spotted you (a
/// few times per monster), a monster that hunted you losing track, and every pin set silently in a lock.</item>
/// </list>
/// What it remembers per monster lasts as long as the player object (one scene), so nothing carries over. Added by
/// Player.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerTraining : MonoBehaviour
{
    private const float SlipCheckSeconds = 0.5f;

    // Kept by the monster itself (or, for guards, whoever struck): they last no longer than the scene.
    private readonly HashSet<Object> ambushed = new HashSet<Object>();
    private readonly HashSet<Object> lostMe = new HashSet<Object>();
    private readonly Dictionary<Object, int> guardRewards = new Dictionary<Object, int>();
    private readonly Dictionary<Object, float> slipTime = new Dictionary<Object, float>();
    private readonly Dictionary<Object, int> slipRewards = new Dictionary<Object, int>();
    private Combatant combatant;
    private PlayerCombat combat;
    private PlayerGuard guard;
    private PlayerStealth stealth;
    private PlayerLockpicking lockpicking;
    private PlayerSkills spells;
    private float nextSlipCheck;

    public static PlayerTraining AddTo(GameObject player) =>
        player.TryGetComponent(out PlayerTraining existing) ? existing : player.AddComponent<PlayerTraining>();

    private void Awake()
    {
        combatant = GetComponent<Combatant>();
        combat = GetComponent<PlayerCombat>();
        guard = GetComponent<PlayerGuard>();
        stealth = GetComponent<PlayerStealth>();
        lockpicking = GetComponent<PlayerLockpicking>();
        spells = GetComponent<PlayerSkills>();
    }

    private void OnEnable()
    {
        if (combatant != null) combatant.DamageDealt += OnDamageDealt;
        if (combat != null) combat.PerfectDodge += OnGuarded;
        if (guard != null) guard.Parried += OnGuarded;
        if (lockpicking != null) lockpicking.PinPressed += OnPinPressed;
        if (spells != null) spells.SkillCast += OnSpellCast;
        MonsterAI.LostTrackOfPlayer += OnLostTrack;
    }

    private void OnDisable()
    {
        if (combatant != null) combatant.DamageDealt -= OnDamageDealt;
        if (combat != null) combat.PerfectDodge -= OnGuarded;
        if (guard != null) guard.Parried -= OnGuarded;
        if (lockpicking != null) lockpicking.PinPressed -= OnPinPressed;
        if (spells != null) spells.SkillCast -= OnSpellCast;
        MonsterAI.LostTrackOfPlayer -= OnLostTrack;
    }

    private static LevelingSettings Rules => LevelingSettings.Current;

    // Sneaking near a monster that hasn't spotted you, checked a couple of times a second.
    private void Update()
    {
        if (stealth == null || !stealth.IsSneaking || Time.time < nextSlipCheck)
        {
            return;
        }

        nextSlipCheck = Time.time + SlipCheckSeconds;
        LevelingSettings rules = Rules;
        Vector3 here = transform.position;
        IReadOnlyList<MonsterAI> monsters = MonsterAI.Active;
        for (int i = 0; i < monsters.Count; i++)
        {
            MonsterAI monster = monsters[i];
            if (monster == null || monster.Definition == null || monster.IsHuntingPlayerSide)
            {
                continue;
            }

            float sight = monster.Definition.DetectionRange;
            if ((monster.transform.position - here).sqrMagnitude > sight * sight)
            {
                continue;
            }

            slipTime.TryGetValue(monster, out float seconds);
            seconds += SlipCheckSeconds;
            if (seconds < rules.SlipSeconds)
            {
                slipTime[monster] = seconds;
                continue;
            }

            slipTime[monster] = 0f;
            if (Count(slipRewards, monster, rules.SlipsPerFoe))
            {
                Sneak(rules.SlipExperience);
            }
        }
    }

    private void OnDamageDealt(IDamageable target, DamageInfo damage, DamageResult result)
    {
        if (result.HealthLost <= 0f || !(target is Damageable body) || !body.TryGetComponent(out MonsterAI monster))
        {
            return;
        }

        float worth = ExperienceTracker.MonsterWorth(monster.Definition, body.MaxHealth) * result.HealthLost / Mathf.Max(1f, body.MaxHealth);
        int tier = ExperienceTracker.PlaceTier;
        if ((damage.Technique & HitTechnique.Melee) != 0)
        {
            ExperienceTracker.Practise(ProficiencyTraining.Melee, tier, worth);
        }
        else if ((damage.Technique & HitTechnique.Shot) != 0)
        {
            ExperienceTracker.Practise(ProficiencyTraining.Archery, tier, worth);
        }
        else if ((damage.Technique & HitTechnique.Spell) != 0)
        {
            ExperienceTracker.Practise(ProficiencyTraining.Spells, tier, worth);
        }
        else
        {
            ExperienceTracker.GrantInsightShare(worth * (ExperienceTracker.OnRaid ? Rules.RaidMultiplier : 1f));
        }

        if (monster.LastHitAmbushed && ambushed.Add(monster))
        {
            Sneak(Rules.AmbushExperience);
        }
    }

    // A parry or a perfect dodge: a few per monster.
    private void OnGuarded(DamageInfo damage)
    {
        if (damage.Source != null && Count(guardRewards, damage.Source, Rules.GuardRewardsPerFoe))
        {
            int tier = ExperienceTracker.PlaceTier;
            ExperienceTracker.Practise(ProficiencyTraining.Melee, tier, Rules.TierExperience(tier, Rules.GuardExperience));
        }
    }

    private void OnLostTrack(MonsterAI monster)
    {
        if (monster != null && lostMe.Add(monster))
        {
            Sneak(Rules.LostTrackExperience);
        }
    }

    // Every spell cast trains Runecraft a little, so healing and warding count as well as hurting.
    private void OnSpellCast(int slot, SkillDefinition spell)
    {
        int tier = ExperienceTracker.PlaceTier;
        ExperienceTracker.Practise(ProficiencyTraining.Spells, tier, Rules.TierExperience(tier, Rules.CastExperience));
    }

    private void OnPinPressed(SwingResult result)
    {
        if (result == SwingResult.Critical)
        {
            Sneak(Rules.SilentPinExperience);
        }
    }

    private static void Sneak(int tierOneAmount)
    {
        int tier = ExperienceTracker.PlaceTier;
        ExperienceTracker.Practise(ProficiencyTraining.Sneaking, tier, Rules.TierExperience(tier, tierOneAmount));
    }

    // True (and counted) while this monster has paid fewer than limit times.
    private static bool Count(Dictionary<Object, int> counts, Object who, int limit)
    {
        counts.TryGetValue(who, out int paid);
        if (paid >= limit)
        {
            return false;
        }

        counts[who] = paid + 1;
        return true;
    }
}
