using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Creatures answering back (Docs/EnemyCombatResearch.md, phase C): felled when a weighty blow catches them reeling (or a charged or
/// crushing one breaks their poise); a shield that breaks under a stagger and splinters after two; the armed dead raising their weapon
/// against a flurry of light blows and answering a third into it; turning when badly hurt (fury, desperation, a cry as the cowardly run);
/// the nimble stepping aside from a bow held past its perfect release; and what elite affixes add (Unyielding, Watchful, Relentless).
/// Part of MonsterAI, in its own file.
/// </summary>
public sealed partial class MonsterAI
{
    // Its weapon guard covers this much of the front, and comes up again at the soonest this long after it went down.
    private const float WeaponGuardArc = 140f;
    private const float WeaponGuardCooldown = 4f;
    // The third light blow into a raised guard draws a quick answer.
    private const int BlowsIntoGuardToAnswer = 3;
    // A felling blow (charged, or this share of its poise in one) floors an ordinary creature.
    private const float FellingPoiseShare = 0.75f;
    // Seconds a guard broken by a heavy blow or an art leaves it reeling back.
    private const float GuardBrokenRecoil = 0.5f;
    private static readonly Color AngryEyes = new Color(1.9f, 0.55f, 0.15f, 1f);

    private bool felling;
    private int guardBreaks;
    private bool shieldBroken;
    private readonly Queue<float> lightBlows = new Queue<float>();
    private float weaponGuardUntil = float.NegativeInfinity;
    private float nextWeaponGuardAt;
    private int blowsIntoGuard;
    private bool guardAnswerDue;
    private bool remoteGuarding;
    private bool hurtTurned;
    private bool criedFleeing;
    private GameObject furyBreath;
    private float nextArrowDodgeAt;
    private bool rolledForThisDraw;
    private float markedUntil = float.NegativeInfinity;
    private bool remoteMarked;
    private GameObject markRing;

    /// <summary>Marked by a perfect release from a bow with Hunter's Mark: it takes more poise from every blow until it fades.</summary>
    public bool IsMarked => CurrentState != State.Dead && (IsRemote ? remoteMarked : Time.time < markedUntil);

    // Poise a blow does while marked, as a multiplier.
    private float MarkScale => IsMarked ? CombatTechniqueSettings.Current.HuntersMarkPoise : 1f;

    // A marking blow (HitTechnique.Marking) landed: marked anew for Hunter's Mark Seconds. The host's game marks for everyone.
    private void NoteMark(in DamageInfo damage)
    {
        if (!IsRemote && (damage.Technique & HitTechnique.Marking) != 0 && CurrentState != State.Dead)
        {
            markedUntil = Time.time + CombatTechniqueSettings.Current.HuntersMarkSeconds;
        }
    }

    // The mark's thin ring at its feet while it holds.
    private void UpdateMarkRing()
    {
        bool marked = IsMarked;
        if (marked && markRing == null)
        {
            Material material = CombatFeelSettings.Current.TelegraphMaterial;
            if (material == null)
            {
                return;
            }

            markRing = new GameObject("Hunter's Mark");
            markRing.transform.SetParent(transform, false);
            markRing.transform.localPosition = Vector3.up * 0.08f;
            float radius = TryGetComponent(out UnityEngine.AI.NavMeshAgent agent) ? agent.radius + 0.25f : 0.8f;
            markRing.transform.localScale = Vector3.one * radius / Mathf.Max(0.1f, transform.lossyScale.x);
            markRing.AddComponent<MeshFilter>().sharedMesh = AttackTelegraph.BandFor(360f, 0.08f);
            var renderer = markRing.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", CombatFeelSettings.Current.HuntersMarkColor);
            block.SetColor("_Color", CombatFeelSettings.Current.HuntersMarkColor);
            renderer.SetPropertyBlock(block);
        }
        else if (!marked && markRing != null)
        {
            Destroy(markRing);
            markRing = null;
        }
    }

    /// <summary>Raised when its raised shield takes a blow: the shield arm jolts.</summary>
    public event System.Action ShieldBlocked;

    /// <summary>Raised when its weapon guard turns a light blow aside.</summary>
    public event System.Action GuardTurnedAside;

    /// <summary>Raised when it turns, badly hurt (Monster Definition › When Hurt): a cry the body shows.</summary>
    public event System.Action TurnedHurt;

    /// <summary>True while it is down from a felling blow: its stagger is the longer Felled Seconds and it lies and gets up.</summary>
    public bool IsFelled { get; private set; }

    /// <summary>Seconds of its stagger left (0 when it isn't staggered).</summary>
    public float StaggerSecondsLeft => CurrentState == State.Staggered ? Mathf.Max(0f, staggerUntil - Time.time) : 0f;

    /// <summary>True while its weapon is raised to turn light blows aside (the armed dead, from Guard From Tier).</summary>
    public bool IsWeaponGuarding => CurrentState != State.Dead && (IsRemote ? remoteGuarding : Time.time < weaponGuardUntil);

    /// <summary>An elite's armour layer (Unyielding): nothing flinches, interrupts or staggers it until it breaks.</summary>
    public bool IsUnyielding { get; set; }

    /// <summary>Backstabs it knows about from the start and never forgets (a Watchful elite).</summary>
    public int BackstabsForeknown { get; set; }

    /// <summary>It always goes on into its follow-ups and holds its wind-ups (a Relentless elite).</summary>
    public bool Presses
    {
        get => attacks != null && attacks.AlwaysPresses;
        set
        {
            if (attacks != null)
            {
                attacks.AlwaysPresses = value;
            }
        }
    }

    /// <summary>True once it has turned, badly hurt.</summary>
    public bool HasTurnedHurt => hurtTurned;

    // Its shield's width now: none once it has splintered.
    private float ShieldArcNow => definition == null || shieldBroken ? 0f : definition.ShieldArc;

    // The raid's tier, for what creatures only learn higher up.
    private static int RaidTierNow => GameSession.ActiveRaid != null ? GameSession.ActiveRaid.Tier : 0;

    // A blow wore its poise down: a stagger when it breaks; felled when a weighty blow catches it reeling (that breaks what is left) or a
    // charged or crushing blow breaks it; a longer stagger when it breaks through a raised shield.
    private void TakeBlowOnPoise(in DamageInfo damage, DamageResult result)
    {
        if (IsUnyielding)
        {
            return;
        }

        bool reeling = IsReeling;
        bool weighty = GuardRules.FellsReeling(damage.Technique);
        float hit = damage.Poise * MarkScale;
        bool broke = poise.Hit(hit, Time.time);
        if (!broke && reeling && weighty)
        {
            poise.Break(Time.time);
            broke = true;
        }

        if (!broke)
        {
            return;
        }

        CombatTechniqueSettings rules = CombatTechniqueSettings.Current;
        if (result.Blocked && ShieldArcNow > 0f)
        {
            GuardBroken(damage.Direction);
            return;
        }

        bool fells = !IsHeavyweight && ((damage.Technique & HitTechnique.Charged) != 0 || hit >= poise.Max * FellingPoiseShare || (reeling && weighty));
        if (fells)
        {
            Fell(damage.Direction);
        }
        else
        {
            Stagger(rules.StaggerSeconds);
        }
    }

    // Floored: the longer stagger, a full recoil for a body without a clip for it.
    private void Fell(Vector3 blowDirection)
    {
        felling = true;
        Stagger(CombatTechniqueSettings.Current.FelledSeconds);
        felling = false;
        pose?.Hit(blowDirection, 1.2f, Time.time);
    }

    // Its poise broke through its raised shield: a longer stagger and the crash of it; enough of them and the shield splinters.
    private void GuardBroken(Vector3 blowDirection)
    {
        CombatTechniqueSettings rules = CombatTechniqueSettings.Current;
        Stagger(rules.StaggerSeconds * rules.GuardBrokenScale);
        AudioManager.Play(AudioManager.Sounds != null ? AudioManager.Sounds.GuardBreak : null, transform.position + Vector3.up * CenterHeight);
        guardBreaks++;
        if (!shieldBroken && guardBreaks >= rules.GuardBreaksToSplinter)
        {
            Splinter(blowDirection);
        }
    }

    // The shield splinters: it guards no more, and its model breaks apart.
    private void Splinter(Vector3 blowDirection)
    {
        shieldBroken = true;
        MonsterShieldPose shield = GetComponentInChildren<MonsterShieldPose>(true);
        if (shield != null)
        {
            shield.Splinter(blowDirection);
        }
    }

    /// <summary>True once its shield has splintered (co-op: the host's word, so friends' copies stop blocking with it too).</summary>
    public bool ShieldBroken => shieldBroken;

    // A light blow landed: enough of them in a short while and the armed dead raise their weapon to turn the next aside.
    private void NoteLightBlow(in DamageInfo damage, DamageResult result)
    {
        if (IsRemote || definition == null || definition.GuardAfterBlows <= 0 || result.Killed || IsWeaponGuarding
            || CurrentState == State.Attacking || CurrentState == State.Staggered || CurrentState == State.Hopping || CurrentState == State.Dead
            || !GuardRules.IsLight(damage.Technique))
        {
            return;
        }

        float now = Time.time;
        lightBlows.Enqueue(now);
        while (lightBlows.Count > 0 && now - lightBlows.Peek() > definition.GuardWithin)
        {
            lightBlows.Dequeue();
        }

        if (now >= nextWeaponGuardAt && GuardRules.RaisesGuard(lightBlows.Count, definition.GuardAfterBlows, RaidTierNow, definition.GuardFromTier))
        {
            weaponGuardUntil = now + definition.GuardSeconds;
            nextWeaponGuardAt = weaponGuardUntil + WeaponGuardCooldown;
            blowsIntoGuard = 0;
            lightBlows.Clear();
            movement.Stop();
        }
    }

    // Damageable asks before a hit: a light blow from the front is turned aside (part gets through); a heavy blow, an art or one from
    // its flank breaks the guard and it reels back. Null when its weapon guard isn't up.
    private GuardOutcome? WeaponGuard(in DamageInfo damage, out float letThrough)
    {
        letThrough = 1f;
        if (!IsWeaponGuarding)
        {
            return null;
        }

        bool inFront = GuardRules.ResolveShield(WeaponGuardArc, transform.forward, damage.Direction, HitTechnique.None) == GuardOutcome.Blocked;
        if (inFront && GuardRules.IsLight(damage.Technique))
        {
            letThrough = definition.GuardLetsThrough;
            GuardTurnedAside?.Invoke();
            CombatFeelSettings feel = CombatFeelSettings.Current;
            if (feel.BlockEffect != null)
            {
                PrefabPool.Spawn(feel.BlockEffect, damage.HitPoint, Quaternion.LookRotation(-damage.Direction.sqrMagnitude > 1e-4f ? -damage.Direction : transform.forward));
            }

            AudioManager.Play(AudioManager.Sounds != null ? AudioManager.Sounds.Block : null, damage.HitPoint);
            if (!IsRemote && ++blowsIntoGuard >= BlowsIntoGuardToAnswer)
            {
                guardAnswerDue = true;
            }

            return GuardOutcome.Blocked;
        }

        if (!IsRemote)
        {
            weaponGuardUntil = float.NegativeInfinity;
            AudioManager.Play(AudioManager.Sounds != null ? AudioManager.Sounds.GuardBreak : null, damage.HitPoint);
            Recoil(GuardBrokenRecoil);
        }

        return GuardOutcome.None;
    }

    // While its weapon guard is up it stands and faces its foe; the third light blow into it is answered with a quick strike (its own
    // wind-up and warning), the frame after, outside the blow's handling. True while the guard holds it.
    private bool UpdateWeaponGuard()
    {
        if (guardAnswerDue)
        {
            guardAnswerDue = false;
            weaponGuardUntil = float.NegativeInfinity;
            if (CanTarget(target))
            {
                Vector3 toTarget = FlatToTarget();
                int answer = attacks.ChooseAnswer(toTarget.magnitude);
                if (answer >= 0)
                {
                    BeginAttack(answer, toTarget);
                    return true;
                }
            }
        }

        if (!IsWeaponGuarding || !CanTarget(target))
        {
            return false;
        }

        SetState(State.Chasing);
        movement.Stop();
        movement.TurnTowards(FlatToTarget());
        return true;
    }

    // Badly hurt (Monster Definition › When Hurt), it turns once: fury or desperation, each with a cry and angry eyes. Read from its
    // health share, so a friend's copy shows it too; only the host's creature hurries its cooldowns.
    private void UpdateHurt()
    {
        if (hurtTurned || definition.HurtMood == HurtMood.None || definition.HurtBelow <= 0f || CurrentState == State.Dead
            || damageable.HealthFraction <= 0f || damageable.HealthFraction > definition.HurtBelow)
        {
            return;
        }

        hurtTurned = true;
        CryOut();
        Transform model = transform.Find("Model");
        CreatureLook.SetEyes(model != null ? model.gameObject : gameObject, AngryEyes);
        if (definition.HurtMood == HurtMood.Fury)
        {
            if (!IsRemote)
            {
                CooldownScale *= 0.85f;
            }

            GameObject breath = CombatFeelSettings.Current.FuryEffect;
            if (breath != null)
            {
                furyBreath = Instantiate(breath, transform);
                furyBreath.transform.localPosition = Vector3.up * (reelingTell != null ? reelingTell.ChestHeight + 0.4f : 1.6f);
            }
        }

        TurnedHurt?.Invoke();
    }

    private void CryOut()
    {
        AudioManager.Play(definition.HurtSound != null ? definition.HurtSound : definition.AttackSound, transform.position);
        pose?.Hit(-transform.forward, 0.5f, Time.time);
    }

    // A cowardly creature turning to run cries out as it goes, once.
    private void NoteFleeing()
    {
        if (!criedFleeing)
        {
            criedFleeing = true;
            CryOut();
        }
    }

    // The nimble step aside from a bow held on them past its perfect release: one chance per draw, never from a perfect release.
    private bool DodgesArrow()
    {
        if (definition.ArrowDodge <= 0f || targetComponent == null || !targetComponent.TryGetComponent(out PlayerArchery archery))
        {
            return false;
        }

        if (!archery.IsDrawing)
        {
            rolledForThisDraw = false;
            return false;
        }

        if (!GuardRules.SidestepsDraw(true, archery.Target == damageable, archery.Draw, archery.InPerfectWindow, rolledForThisDraw, Time.time >= nextArrowDodgeAt))
        {
            return false;
        }

        rolledForThisDraw = true;
        if (Random.value >= definition.ArrowDodge)
        {
            return false;
        }

        nextArrowDodgeAt = Time.time + CombatTechniqueSettings.Current.ArrowDodgeCooldown;
        Vector3 toFoe = FlatToTarget().normalized;
        BeginHop(Vector3.Cross(Vector3.up, toFoe) * (Random.value < 0.5f ? -1f : 1f));
        return true;
    }

    private void ClearAnswers()
    {
        if (markRing != null)
        {
            Destroy(markRing);
            markRing = null;
        }

        if (furyBreath != null)
        {
            Destroy(furyBreath);
            furyBreath = null;
        }
    }
}
