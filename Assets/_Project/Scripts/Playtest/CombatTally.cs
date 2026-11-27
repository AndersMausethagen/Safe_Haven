using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Counts how the player fights, for the playtest log (PlaytestLogger writes it as Combat rows each played minute): swings by
/// kind, blows landed and whiffed, parries, blocks, broken guards, dodges and perfect dodges, flinches and the swings they cost,
/// counters, backstabs, interrupts. It only listens to the player's own events, so no fighting code knows it exists. Docs/CombatResearch.md
/// §3.3 #10: measure the loop (how much of a fight is parrying) before and after changing it.
/// </summary>
public sealed class CombatTally
{
    public const string LightSwing = "Swing.Light", HeavySwing = "Swing.Heavy", RushSwing = "Swing.Rush", ArtSwing = "Swing.Art", BashSwing = "Swing.ShieldBash",
        GuardCounterSwing = "Swing.GuardCounter";
    public const string Landed = "Landed", Whiffed = "Whiffed", Parry = "Parry", Block = "Block", GuardBroken = "GuardBroken";
    public const string Dodge = "Dodge", PerfectDodge = "PerfectDodge", Flinch = "Flinch", LostSwing = "LostSwing", Counter = "Counter";
    public const string Backstab = "Backstab", Finisher = "FinishingBlow", Interrupt = "Interrupt", Hurt = "Hurt";
    // Ranged and stealth (Docs/RangedStealthResearch.md §3.3): the perfect-release rate decides where the bow stands against a sword.
    public const string BowShot = "Bow.Shot", BowPerfect = "Bow.Perfect", BowCounter = "Bow.Counter", BowHit = "Bow.Hit";
    public const string StaffCast = "Staff.Cast", StaffHit = "Staff.Hit";
    public const string Ambush = "Ambush", AmbushKill = "Ambush.Kill", SpottedBy = "Spotted.";
    // Abilities from the bar (the user, 2026-11-25: the log had no casts at all, and their blows were counted as swings): casts in
    // all and by skill, the blows they landed and the damage those dealt, casts refused for want of a Focus circle, and the Focus
    // fighting earned (rounded down, the rest carried to the next).
    public const string SkillCast = "Skill.Cast", SkillOf = "Skill.", SkillHit = "Skill.Hit", SkillDamage = "Skill.Damage",
        SkillNoFocus = "Skill.NoFocus", FocusEarned = "Focus.Earned";
    /// <summary>Damage by skill, "SkillDamage.&lt;id&gt;" (2026-11-27): not under "Skill." so those rows stay casts by skill.</summary>
    public const string SkillDamageOf = "SkillDamage.";
    // The move inside a kind (Docs/CombatFeelAnalysis_2026-11-26.md §3.8: charged blows, follow-ups and backsteps were all counted as
    // light swings), on top of the kind's own count so Swing.Light stays comparable with older runs. And why a swing whiffed: with
    // the focused enemy within its reach (it stepped aside, or the aim was off), out of reach, or with nothing in focus.
    public const string ChargedMove = "Move.Charged", FollowUpMove = "Move.FollowUp", BackstepMove = "Move.Backstep", FinisherMove = "Move.Finisher";
    public const string WhiffInReach = "Whiffed.InReach", WhiffOutOfReach = "Whiffed.OutOfReach", WhiffNoFocus = "Whiffed.NoFocus";

    /// <summary>The move a swing of <paramref name="weapon"/> was, beyond its kind (null: none of them).</summary>
    public static string MoveName(WeaponDefinition weapon, SwingInfo swing)
    {
        AttackDefinition attack = swing.Attack;
        if (weapon == null || attack == null)
        {
            return null;
        }

        if (attack == weapon.ChargedAttack) return ChargedMove;
        if (attack == weapon.HeavyFollowUp) return FollowUpMove;
        if (attack == weapon.BackstepAttack) return BackstepMove;
        return swing.Kind == SwingKind.Light && swing.IsFinisher ? FinisherMove : null;
    }

    /// <summary>Why a swing reaching <paramref name="reach"/> m found nothing, with its focused enemy <paramref name="distance"/> m off
    /// (below 0: none in focus). A body's half width counts as within reach.</summary>
    public static string WhiffName(float reach, float distance) =>
        distance < 0f ? WhiffNoFocus : distance <= reach + 0.5f ? WhiffInReach : WhiffOutOfReach;

    private readonly Dictionary<string, int> counts = new Dictionary<string, int>();
    private PlayerCombat combat;
    private PlayerGuard guard;
    private PlayerDodge dodge;
    private Damageable health;
    private PlayerArchery archery;
    private Combatant self;
    private PlayerSkills skills;
    private PlayerFocus focus;
    private PlayerFocusTarget focusTarget;
    private float skillDamageCarry;
    private readonly Dictionary<string, float> skillDamageBy = new Dictionary<string, float>();
    private float focusCarry;

    /// <summary>The weapon the player held when the last count was made (the rows' detail).</summary>
    public string Weapon { get; private set; } = "";

    public IReadOnlyDictionary<string, int> Counts => counts;

    public void Note(string what, int amount = 1)
    {
        if (amount <= 0 || string.IsNullOrEmpty(what))
        {
            return;
        }

        counts.TryGetValue(what, out int sum);
        counts[what] = sum + amount;
        Weapon = combat != null && combat.Weapon != null ? combat.Weapon.name : "";
    }

    public void Clear()
    {
        counts.Clear();
        skillDamageCarry = 0f;
        skillDamageBy.Clear();
        focusCarry = 0f;
    }

    /// <summary>Listens to this player (null stops listening).</summary>
    public void Follow(GameObject player)
    {
        Unfollow();
        if (player == null)
        {
            return;
        }

        combat = player.GetComponent<PlayerCombat>();
        guard = player.GetComponent<PlayerGuard>();
        dodge = player.GetComponent<PlayerDodge>();
        health = player.GetComponent<Damageable>();
        archery = player.GetComponent<PlayerArchery>();
        self = player.GetComponent<Combatant>();
        skills = player.GetComponent<PlayerSkills>();
        focus = player.GetComponent<PlayerFocus>();
        focusTarget = player.GetComponent<PlayerFocusTarget>();
        if (archery != null)
        {
            archery.Shot += OnShot;
        }

        if (skills != null)
        {
            skills.SkillCast += OnSkillCast;
            skills.CastRefused += OnCastRefused;
        }

        if (focus != null)
        {
            focus.Earned += OnFocusEarned;
        }

        Damageable.AnyDamaged += OnAnyDamaged;
        MonsterAI.SpottedPlayer += OnSpotted;
        if (combat != null)
        {
            combat.SwingStarted += OnSwingStarted;
            combat.SwingLanded += OnSwingLanded;
            combat.Flinched += OnFlinched;
            combat.PerfectDodge += OnPerfectDodge;
            combat.Backstabbed += OnBackstabbed;
            combat.FinishStarted += OnFinishStarted;
        }

        if (guard != null)
        {
            guard.Parried += OnParried;
            guard.Blocked += OnBlocked;
            guard.Broken += OnBroken;
        }

        if (dodge != null)
        {
            dodge.Dodged += OnDodged;
        }

        if (health != null)
        {
            health.Damaged += OnHurt;
        }

        MonsterAI.AnyInterrupted += OnInterrupted;
    }

    public void Unfollow()
    {
        if (archery != null)
        {
            archery.Shot -= OnShot;
        }

        if (skills != null)
        {
            skills.SkillCast -= OnSkillCast;
            skills.CastRefused -= OnCastRefused;
        }

        if (focus != null)
        {
            focus.Earned -= OnFocusEarned;
        }

        Damageable.AnyDamaged -= OnAnyDamaged;
        MonsterAI.SpottedPlayer -= OnSpotted;
        archery = null;
        self = null;
        skills = null;
        focus = null;
        focusTarget = null;
        if (combat != null)
        {
            combat.SwingStarted -= OnSwingStarted;
            combat.SwingLanded -= OnSwingLanded;
            combat.Flinched -= OnFlinched;
            combat.PerfectDodge -= OnPerfectDodge;
            combat.Backstabbed -= OnBackstabbed;
            combat.FinishStarted -= OnFinishStarted;
        }

        if (guard != null)
        {
            guard.Parried -= OnParried;
            guard.Blocked -= OnBlocked;
            guard.Broken -= OnBroken;
        }

        if (dodge != null)
        {
            dodge.Dodged -= OnDodged;
        }

        if (health != null)
        {
            health.Damaged -= OnHurt;
        }

        MonsterAI.AnyInterrupted -= OnInterrupted;
        combat = null;
        guard = null;
        dodge = null;
        health = null;
    }

    /// <summary>The count a swing of this kind is kept under.</summary>
    public static string SwingName(SwingKind kind) => kind switch
    {
        SwingKind.Heavy => HeavySwing,
        SwingKind.Rush => RushSwing,
        SwingKind.Art => ArtSwing,
        SwingKind.ShieldBash => BashSwing,
        SwingKind.GuardCounter => GuardCounterSwing,
        _ => LightSwing,
    };

    private void OnShot(ArrowShot shot)
    {
        Note(BowShot);
        if (shot.Perfect) Note(BowPerfect);
        if (shot.Counter) Note(BowCounter);
    }

    // The player's arrows and bolts that struck, and blows from the shadows (each creature knows, after the blow, whether it was caught unaware).
    private void OnAnyDamaged(Damageable victim, DamageInfo damage, DamageResult result)
    {
        if (self == null || damage.Source != self || result.Damage <= 0f || damage.IsOverTime)
        {
            return;
        }

        if ((damage.Technique & HitTechnique.Shot) != 0) Note(BowHit);
        if ((damage.Technique & HitTechnique.Ability) != 0)
        {
            Note(SkillHit);
            skillDamageCarry += result.Damage;
            int whole = Mathf.FloorToInt(skillDamageCarry);
            skillDamageCarry -= whole;
            Note(SkillDamage, whole);
            // And by skill (Docs/SkillChoiceResearch_2026-11-27.md 3.7: a skill doing over 40% of skill damage is a default, not a choice).
            SkillDefinition skill = SkillOfAttack(damage.Attack);
            if (skill != null)
            {
                skillDamageBy.TryGetValue(skill.Id, out float carried);
                carried += result.Damage;
                int part = Mathf.FloorToInt(carried);
                skillDamageBy[skill.Id] = carried - part;
                Note(SkillDamageOf + skill.Id, part);
            }
        }
        else if ((damage.Technique & HitTechnique.Spell) != 0) Note(StaffHit);
        if (victim.TryGetComponent(out MonsterAI monster) && monster.LastHitAmbushed)
        {
            Note(result.Killed ? AmbushKill : Ambush);
        }
    }

    // The known skill whose blow this was, by its attack; null for none (an art, a staff's bolt).
    private SkillDefinition SkillOfAttack(AttackDefinition attack)
    {
        if (attack == null || skills == null)
        {
            return null;
        }

        foreach (SkillDefinition skill in skills.KnownSkills)
        {
            if (skill != null && skill.Attack == attack)
            {
                return skill;
            }
        }

        return null;
    }

    private void OnSpotted(MonsterAI monster, string how) => Note(SpottedBy + how);

    private void OnSkillCast(int slot, SkillDefinition skill)
    {
        Note(SkillCast);
        Note(SkillOf + skill.Id);
    }

    private void OnCastRefused(int slot, SkillCastResult why)
    {
        if (why == SkillCastResult.NoFocus) Note(SkillNoFocus);
    }

    private void OnFocusEarned(float amount)
    {
        focusCarry += amount;
        int whole = Mathf.FloorToInt(focusCarry);
        focusCarry -= whole;
        Note(FocusEarned, whole);
    }

    private void OnSwingStarted(SwingInfo swing)
    {
        Note(SwingName(swing.Kind));
        Note(MoveName(combat != null ? combat.Weapon : null, swing));
        if (swing.Attack != null && swing.Attack.CastProjectile != null)
        {
            Note(StaffCast);
        }

        if (swing.IsCounter)
        {
            Note(Counter);
        }
    }

    private void OnSwingLanded(SwingInfo swing, int hits)
    {
        if (swing.Attack != null && (swing.Attack.Motion == ArtMotion.Throw || swing.Attack.CastProjectile != null))
        {
            return;
        }

        if (hits > 0)
        {
            Note(Landed, hits);
            return;
        }

        Note(Whiffed);
        Damageable focused = focusTarget != null ? focusTarget.Target : null;
        float distance = -1f;
        if (focused != null && combat != null)
        {
            Vector3 apart = focused.transform.position - combat.transform.position;
            apart.y = 0f;
            distance = apart.magnitude;
        }

        Note(WhiffName(swing.Attack != null ? swing.Attack.Range : 0f, distance));
    }

    private void OnFlinched(float seconds, bool lostSwing)
    {
        Note(Flinch);
        if (lostSwing) Note(LostSwing);
    }

    private void OnPerfectDodge(DamageInfo damage) => Note(PerfectDodge);
    private void OnBackstabbed(IDamageable target) => Note(Backstab);
    private void OnFinishStarted(MonsterAI monster) => Note(Finisher);
    private void OnParried(DamageInfo damage) => Note(Parry);
    private void OnBlocked(DamageInfo damage) => Note(Block);
    private void OnBroken(float seconds) => Note(GuardBroken);
    private void OnDodged(Vector3 direction) => Note(Dodge);

    private void OnHurt(DamageInfo damage, DamageResult result)
    {
        if (!result.Blocked && result.Damage > 0f && !damage.IsOverTime)
        {
            Note(Hurt);
        }
    }

    // Only the player's own blows count: a companion's hit cancelling a wind-up is not the player trading.
    private void OnInterrupted(MonsterAI monster, Combatant by)
    {
        if (combat != null && by != null && by.gameObject == combat.gameObject)
        {
            Note(Interrupt);
        }
    }
}
