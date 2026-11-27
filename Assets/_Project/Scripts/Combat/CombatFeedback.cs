using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Makes hits feel like hits, game-wide: every target that is hit reacts (HitReaction: flash, punch, knockback) with a
/// spark effect and a sound where it was struck; the player's own hits and the hits the player takes also freeze the
/// game for a moment (hit stop) and shake the camera, harder for heavy swings, finishers and counters. Blocks, parries,
/// broken guards, staggers and perfect dodges each get their own effect, sound and callout; parries and perfect dodges
/// also slow time for a moment. The player's swings draw a slash arc and whoosh, and the screen edges flash when hurt
/// (CombatScreenEffects) while a small readout shows the combo chain (ComboHud). Strengths and effects come from
/// Resources/CombatFeel, sounds from Audio Settings (falling back from the attack to the weapon to the game-wide one).
/// <para>
/// Where it belongs: Combat. It is a persistent system started by GameSystems and listens to Damageable's and
/// MonsterAI's game-wide events and the player's PlayerCombat and PlayerGuard, so none of them know about it.
/// </para>
/// <para>
/// Extending: for a new kind of feedback (rumble), add it in the matching handler; for per-target behavior, extend
/// HitReaction. Keep timings in CombatFeelSettings so designers can tune them.
/// </para>
/// </summary>
public sealed class CombatFeedback : MonoBehaviour, IGameSystem
{
    private CombatFeelSettings feel;
    private float hitStopUntil = -1f;
    private float hitStopScale = 1f;
    private Damageable watchedPlayer;
    private PlayerCombat playerCombat;
    private PlayerDodge playerDodge;
    private PlayerStamina playerStamina;
    private PlayerArchery playerArchery;
    private CombatScreenEffects screen;
    private ComboHud comboHud;
    // The player's swing whoosh, waiting for the moment the weapon moves (Combat Feel › Swing Sound Lead), and which swing it is for.
    private SoundDefinition pendingSwingSound;
    private float pendingSwingStart;
    // A slow that waits for the stop running now (the last kill of a fight), real seconds; below 0 none.
    private float thenScale = -1f;
    private float thenSeconds;
    // The fight under way (§8): the foes the player struck or was struck by, since its first blow; a gap of quiet starts another.
    private readonly System.Collections.Generic.HashSet<Damageable> fightFoes = new System.Collections.Generic.HashSet<Damageable>();
    private float fightBeganAt;
    private float lastFightBlowAt = float.NegativeInfinity;
    private const float FightGapSeconds = 5f;
    // The riser under a heavy held to charge (§11), on a voice of its own so it can stop the moment the charge does.
    private AudioSource chargeVoice;

    public static CombatFeedback Current { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Current = null;

    /// <summary>True while a hit stop slows the game.</summary>
    public bool IsHitStopped => hitStopUntil >= 0f;

    private static AudioSettingsDefinition Sounds => AudioManager.Sounds;

    /// <summary>Called once by GameSystems when the game starts.</summary>
    public void Initialise(CombatFeelSettings settings)
    {
        Current = this;
        feel = settings;
        Damageable.AnyDamaged += OnAnyDamaged;
        Damageable.AnyGuarded += OnAnyGuarded;
        MonsterAI.AnyStaggered += OnAnyStaggered;
        MonsterAI.AnyInterrupted += OnAnyInterrupted;
    }

    private void OnDestroy()
    {
        Damageable.AnyDamaged -= OnAnyDamaged;
        Damageable.AnyGuarded -= OnAnyGuarded;
        MonsterAI.AnyStaggered -= OnAnyStaggered;
        MonsterAI.AnyInterrupted -= OnAnyInterrupted;
        WatchPlayer(null);
        EndHitStop();
        if (Current == this)
        {
            Current = null;
        }
    }

    public void OnSceneEntered(Scene scene)
    {
        EndHitStop();
        WatchPlayer(Player.Current);

        // Every scene with a player camera gets the shake; scenes can also have it set up already.
        foreach (CinemachineCamera vcam in FindObjectsByType<CinemachineCamera>())
        {
            if (!vcam.TryGetComponent(out CameraShake _))
            {
                vcam.gameObject.AddComponent<CameraShake>();
            }
        }
    }

    /// <summary>Nearly freezes the game for a moment of real time. Overlapping requests keep the longest.</summary>
    public void HitStop(float seconds) => SlowTime(feel.HitStopTimeScale, seconds);

    /// <summary>Slows the game to <paramref name="scale"/> for <paramref name="seconds"/> of real time. Overlapping requests keep
    /// the slowest for as long as it asked: a short stop inside a longer, gentler slow runs on its own and the slow takes up again
    /// after it for what is left of it, and a gentler, longer slow asked during a stop follows the stop.</summary>
    public void SlowTime(float scale, float seconds)
    {
        // Hosting friends in co-op, time is theirs too (GamePause.TimeIsShared): the camera shake carries the blow alone.
        if (seconds <= 0f || !GameOptions.HitStop.On || GamePause.TimeIsShared)
        {
            return;
        }

        float until = Time.unscaledTime + seconds;
        if (hitStopUntil < 0f)
        {
            hitStopScale = scale;
            hitStopUntil = until;
        }
        else if (scale < hitStopScale && until < hitStopUntil)
        {
            // Keeping the slowest and the latest end made a hurt during an ultimate's slow beat, or a skill's stop during the last
            // kill's slow, freeze the whole rest of the slow (review 2026-11-27).
            WaitAfterTheStop(hitStopScale, hitStopUntil - until);
            hitStopScale = scale;
            hitStopUntil = until;
        }
        else if (scale > hitStopScale && until > hitStopUntil)
        {
            WaitAfterTheStop(scale, until - hitStopUntil);
            return;
        }
        else
        {
            hitStopScale = Mathf.Min(hitStopScale, scale);
            hitStopUntil = Mathf.Max(hitStopUntil, until);
        }

        if (!GamePause.IsTimeFrozen)
        {
            Time.timeScale = hitStopScale;
        }
    }

    /// <summary>Slows the game once the stop running now has ended (at once when none runs): a beat after a blow, not a longer freeze.</summary>
    public void SlowTimeAfterStop(float scale, float seconds)
    {
        if (hitStopUntil < 0f)
        {
            SlowTime(scale, seconds);
            return;
        }

        WaitAfterTheStop(scale, seconds);
    }

    // One slow waits at most: the slowest and the longest of those asked (the last one asked used to win).
    private void WaitAfterTheStop(float scale, float seconds)
    {
        if (seconds <= 0f)
        {
            return;
        }

        bool none = thenScale < 0f;
        thenScale = none ? scale : Mathf.Min(thenScale, scale);
        thenSeconds = none ? seconds : Mathf.Max(thenSeconds, seconds);
    }

    private void Update()
    {
        EndStopsDueBy(Time.unscaledTime);
        PlayPendingSwingSound();
        UpdateChargeSound();
    }

    /// <summary>Ends the stop or slow due by <paramref name="now"/> (real time) and starts the slow waiting after it. Update's own;
    /// public for tests, which hand it a later time.</summary>
    public void EndStopsDueBy(float now)
    {
        if (hitStopUntil >= 0f && now >= hitStopUntil)
        {
            float then = thenScale;
            float thenFor = thenSeconds;
            EndHitStop();
            if (then > 0f)
            {
                SlowTime(then, thenFor);
            }
        }
    }

    private void UpdateChargeSound()
    {
        bool charging = playerCombat != null && playerCombat.IsCharging && feel.ChargeRiseSound != null && feel.ChargeRiseSound.HasClips;
        if (!charging)
        {
            if (chargeVoice != null && chargeVoice.isPlaying)
            {
                chargeVoice.Stop();
            }

            return;
        }

        if (chargeVoice == null)
        {
            chargeVoice = gameObject.AddComponent<AudioSource>();
            chargeVoice.playOnAwake = false;
        }

        if (!chargeVoice.isPlaying && playerCombat.Charge < 0.5f)
        {
            AudioManager.Configure(chargeVoice, feel.ChargeRiseSound);
            chargeVoice.clip = feel.ChargeRiseSound.PickClip();
            chargeVoice.loop = false;
            // The player's own, so flat, but part of the world: it waits with it through a pause.
            chargeVoice.spatialBlend = 0f;
            chargeVoice.ignoreListenerPause = false;
            // From where the charge stands, if it began during a stop or a pause.
            chargeVoice.time = Mathf.Min(chargeVoice.clip.length - 0.01f, playerCombat.Charge * CombatTechniqueSettings.Current.HeavyChargeSeconds);
            chargeVoice.Play();
        }
    }

    // The whoosh comes Swing Sound Lead before the blow, timed from the swing's own clock (PlayerCombat.TimeToHit), so a swing that
    // stands still at the top of its wind-up (a heavy charging, a light one waiting to see whether its press is held) whooshes only
    // once it moves on, and a charged attack let go from the hold, with less than the lead left, whooshes at once. Only if that swing
    // is still winding up: a swing dodged out of, lost to a flinch or turned heavy stays silent.
    private void PlayPendingSwingSound()
    {
        if (pendingSwingSound == null)
        {
            return;
        }

        if (playerCombat == null || !playerCombat.IsWindingUp || !Mathf.Approximately(playerCombat.SwingStartTime, pendingSwingStart))
        {
            pendingSwingSound = null;
            return;
        }

        if (playerCombat.IsHoldingSwing || playerCombat.TimeToHit > feel.SwingSoundLead)
        {
            return;
        }

        AudioManager.Play(pendingSwingSound, playerCombat.transform.position + Vector3.up);
        pendingSwingSound = null;
    }

    private void EndHitStop()
    {
        if (hitStopUntil < 0f)
        {
            return;
        }

        hitStopUntil = -1f;
        hitStopScale = 1f;
        thenScale = -1f;
        // The pause owns time while it is frozen and restores it itself.
        if (!GamePause.IsTimeFrozen)
        {
            Time.timeScale = 1f;
        }
    }

    // ----- Hits -----

    private void OnAnyDamaged(Damageable victim, DamageInfo damage, DamageResult result)
    {
        // Damage over time (burning) shows only its numbers: no sparks, flinch, hit stop or shake every tick.
        if (result.Damage <= 0f || (damage.Technique & HitTechnique.OverTime) != 0)
        {
            return;
        }

        HitReaction.For(victim).Play(damage, result);
        bool structure = victim.Category == TargetCategory.Structure;
        // The guard counter ("a hard answer") is a strong blow too: it landed like a light one (Docs/CombatFeelReview_2026-11-19.md §7).
        bool strong = result.IsCritical
            || (damage.Technique & (HitTechnique.Heavy | HitTechnique.Finisher | HitTechnique.Counter | HitTechnique.Backstab | HitTechnique.WeaponArt
                | HitTechnique.GuardCounter)) != 0;
        float impact = damage.Attack != null ? damage.Attack.Impact : 1f;
        float force = StrikeRules.HitForce(result.Damage / victim.MaxHealth, impact, strong, result.Killed);
        if (!result.Blocked)
        {
            bool bled = Bleed(victim, damage, result, force);
            if (result.Killed && DeathRagdoll.Play(victim, damage, force) == null && bled && victim.TryGetComponent(out MonsterAI fallen))
            {
                // Nothing left lying (no ragdoll), but it still bled out where it fell.
                BloodEffects.PoolUnder(victim.transform.position, 1.6f, fallen.Definition != null ? fallen.Definition.BloodColor : DefaultBlood);
            }

            // Steel on flesh throws blood, not sparks; only a strong blow keeps its flash. A weapon's own effect (a fist's puff) stays.
            GameObject weaponEffect = PlayerWeaponHitEffect(damage);
            GameObject effect = structure ? feel.StructureHitEffect : strong ? feel.CriticalHitEffect
                : weaponEffect != null ? weaponEffect : bled ? null : feel.HitEffect;
            Spawn(effect, damage.HitPoint, damage.Direction);
            // Stone throws grit and sparks, plate rings, bark splinters (HitMaterials): on top of the usual hit.
            if (!structure)
            {
                HitMaterials.Struck(victim, damage);
            }

            Vector3 heardAt = HeardAt(damage);
            AudioManager.Play(HitSoundFor(damage, structure), heardAt);
            if (strong && !structure)
            {
                AudioManager.Play(Sounds != null ? Sounds.CriticalHit : null, heardAt);
            }

            if (bled && result.Killed)
            {
                AudioManager.Play(feel.BloodKillSound, heardAt);
            }

            if ((damage.Technique & HitTechnique.Backstab) != 0)
            {
                AudioManager.Play(feel.BackstabSound, heardAt);
                SlowTime(CombatTechniqueSettings.Current.SlowMotionScale, feel.BackstabSlowSeconds);
                if (screen != null)
                {
                    screen.Flash(feel.TechniqueFlashColor, 0.6f);
                }
            }

            // The killing blow has its death cry instead.
            if (!result.Killed && victim.TryGetComponent(out IEventSubject known) && known.EventSubject is ICombatSounds sounds)
            {
                AudioManager.Play(sounds.HurtSound, victim.transform.position);
            }
        }

        if (Player.Current != null && victim == Player.Current.Health)
        {
            NoteFoe(damage.Source != null ? damage.Source.GetComponent<Damageable>() : null);
            if (!result.Blocked)
            {
                // By the blow's own Impact: a rootling's grasp and a troll's stomp landed alike, and a wolf's nip shook the screen harder
                // than the player's own kills (Docs/CombatFeelReview_2026-11-19.md §3).
                HitStop(feel.HitStopWhenHurt * impact);
                CameraShake.Add(feel.ShakeWhenHurt * impact);
                if (impact >= feel.HeavyImpact)
                {
                    CameraShake.Kick(damage.Direction, feel.KickPerImpact * impact);
                }
                if (playerCombat != null && playerCombat.IsPushingThrough)
                {
                    // Taken on the swing: it goes on.
                    Spawn(feel.BlockEffect, damage.HitPoint, -damage.Direction);
                }

                AudioManager.Play(Sounds != null ? Sounds.PlayerHurt : null, victim.transform.position);
            }

            return;
        }

        if (damage.Source != null && Player.IsPlayer(damage.Source.gameObject))
        {
            float technique = (damage.Technique & (HitTechnique.Backstab | HitTechnique.WeaponArt)) != 0 ? 1.75f
                : (damage.Technique & (HitTechnique.Heavy | HitTechnique.Counter | HitTechnique.GuardCounter)) != 0 ? 1.5f
                : (damage.Technique & HitTechnique.Finisher) != 0 ? 1.25f : 1f;
            // A dagger's nick and a warhammer's blow don't land alike; the attack's Impact says how they differ.
            float extra = technique * impact;
            // A kill takes the kill's stop and at most a finisher's technique on top: an art's or a backstab's kill multiplied twice
            // (455 ms per body for the hammer art).
            float stop = result.Killed ? feel.HitStopOnKill * Mathf.Min(technique, 1.25f) * impact
                : (strong ? feel.HitStopOnCrit : feel.HitStopOnHit) * extra;
            stop = Mathf.Min(stop, result.Killed ? feel.KillStopCeiling : feel.StopCeiling);
            // The blow that toppled a boss (MonsterAI heard it first) holds twice as long: the moment the work paid off.
            if (!result.Killed && victim.TryGetComponent(out MonsterAI toppled) && toppled.ToppledThisFrame)
            {
                stop *= 2f;
            }

            float trauma = (result.Killed ? feel.ShakeOnKill : strong ? feel.ShakeOnCrit : feel.ShakeOnHit) * extra;
            // A big blow pushes the frame along itself, once a swing, and rattles it less (§15).
            if ((damage.Technique & (HitTechnique.Heavy | HitTechnique.Finisher | HitTechnique.WeaponArt)) != 0)
            {
                trauma *= feel.KickShakeShare;
                if (!InTheSameSwing(damage.Technique))
                {
                    CameraShake.Kick(damage.Direction, feel.KickPerImpact * impact);
                }
            }

            stop = SwingStop(stop, damage.Technique, ref trauma);
            HitStop(stop);
            NoteFoe(victim);
            if (result.Killed)
            {
                GreatBlow(victim, damage);
                FightWon(victim);
            }

            CameraShake.Add(trauma);
            // The struck body shudders through the moment; hosting co-op, time can't stop, so its animation holds and the swing slows.
            if (!result.Killed && GameOptions.HitStop.On)
            {
                bool shared = GamePause.TimeIsShared;
                HitShake.Play(victim, stop, feel.VictimShake * Mathf.Min(extra, 2f), shared);
                if (shared && playerCombat != null)
                {
                    HitShake.Slow(playerCombat.GetComponentInChildren<Animator>(), stop, 0.15f);
                }
            }
        }
    }

    // One stop per swing (Docs/CombatFeelReview_2026-11-19.md §1): each body a sweep reached started a freeze of its own after the last
    // had ended, so a pack strobed (up to 4 × 455 ms for the hammer art). The first body of a swing (or of a cast's fan) gets its stop;
    // each further one lengthens that stop by Extra Target Share of it, to at most Most Per Swing, and adds that share of its shake.
    // Arrows and thrown weapons land seconds apart and keep one each. Returns the stop to ask for now.
    private float swingKey = float.NaN;
    private float swingStopBegan;
    private float swingFirstStop;
    private int swingTargets;
    private const float SwingMergeSeconds = 0.4f;

    // A further body of the swing whose first already had its stop.
    private bool InTheSameSwing(HitTechnique technique) => playerCombat != null && (technique & (HitTechnique.Melee | HitTechnique.Spell)) != 0
        && Mathf.Approximately(playerCombat.SwingStartTime, swingKey) && Time.unscaledTime - swingStopBegan < SwingMergeSeconds;

    private float SwingStop(float stop, HitTechnique technique, ref float trauma)
    {
        bool swing = playerCombat != null && (technique & (HitTechnique.Melee | HitTechnique.Spell)) != 0;
        if (!swing)
        {
            return stop;
        }

        float key = playerCombat.SwingStartTime;
        if (Mathf.Approximately(key, swingKey) && Time.unscaledTime - swingStopBegan < SwingMergeSeconds)
        {
            swingTargets++;
            float merged = swingFirstStop * Mathf.Min(1f + feel.ExtraTargetShare * swingTargets, feel.MostPerSwing);
            trauma *= feel.ExtraTargetShare;
            return swingStopBegan + merged - Time.unscaledTime;
        }

        swingKey = key;
        swingStopBegan = Time.unscaledTime;
        swingFirstStop = stop;
        swingTargets = 0;
        return stop;
    }

    private void NoteFoe(Damageable foe)
    {
        if (foe == null || !foe.TryGetComponent(out MonsterAI _))
        {
            return;
        }

        if (Time.time - lastFightBlowAt > FightGapSeconds)
        {
            fightFoes.Clear();
            fightBeganAt = Time.time;
        }

        lastFightBlowAt = Time.time;
        fightFoes.Add(foe);
    }

    /// <summary>Whether a player's kill ends a fight worth marking (§8): <paramref name="foes"/> struck, or it lasted
    /// <paramref name="seconds"/>, and nothing else hostile hunts the player's side near.</summary>
    public static bool EndsAFight(int foes, float seconds, bool othersHunting, CombatFeelSettings feel) =>
        !othersHunting && (foes >= feel.FightWonFoes || seconds >= feel.FightWonSeconds);

    // The blow that ends the fight (Docs/CombatFeelReview_2026-11-19.md §8): nothing marked the moment the danger ended, and the fight
    // stayed on for the quiet stretch after it. A boss has its own stop; wildlife isn't a fight.
    private void FightWon(Damageable victim)
    {
        if (!victim.TryGetComponent(out MonsterAI monster) || victim.TryGetComponent(out BossEncounter _) || !victim.Faction.WillAttack(Faction.Player))
        {
            return;
        }

        Vector3 at = Player.Current != null ? Player.Current.transform.position : victim.transform.position;
        bool others = MonsterAI.AnyHuntingPlayerSideWithin(at, CombatTechniqueSettings.Current.FightHoldRange, monster);
        if (!EndsAFight(fightFoes.Count, Time.time - fightBeganAt, others, feel))
        {
            return;
        }

        fightFoes.Clear();
        lastFightBlowAt = float.NegativeInfinity;
        SlowTimeAfterStop(feel.LastKillSlowScale, feel.LastKillSlowSeconds);
        AudioManager.Play(feel.FightWonSound, at);
        if (playerCombat != null)
        {
            playerCombat.EndFight();
        }
    }

    // The "boss stop" (Docs/LookAndFeelResearch_2026-11-18.md V6): the blow that fells a boss holds far longer than any other and the
    // picture streaks out from it; an elite felled by a heavy blow or a finisher gets half the streak and no longer hold.
    private void GreatBlow(Damageable victim, in DamageInfo damage)
    {
        if (victim.TryGetComponent(out BossEncounter _))
        {
            SlowTime(feel.BossStopTimeScale, feel.BossStopSeconds);
            ImpactBlur.Play(damage.HitPoint, feel.ImpactBlurStrength, feel.ImpactBlurSeconds);
        }
        else if ((damage.Technique & (HitTechnique.Heavy | HitTechnique.Finisher)) != 0 && victim.TryGetComponent(out MonsterAI felled) && felled.IsElite)
        {
            ImpactBlur.Play(damage.HitPoint, feel.ImpactBlurStrength * 0.5f, feel.ImpactBlurSeconds * 0.75f);
        }
    }

    // What bleeds sprays the way the blow went and the way the blade was sweeping, as hard as it struck. Structures don't, nor
    // what its definition says has no blood in it.
    private static bool Bleed(Damageable victim, in DamageInfo damage, in DamageResult result, float force)
    {
        if (victim.Category != TargetCategory.Creature || !GameOptions.Blood.On)
        {
            return false;
        }

        Color blood = DefaultBlood;
        if (victim.TryGetComponent(out MonsterAI monster) && monster.Definition != null)
        {
            if (!monster.Definition.Bleeds)
            {
                return false;
            }

            blood = monster.Definition.BloodColor;
        }

        AttackDefinition attack = damage.Attack;
        Vector3 way = StrikeRules.SprayDirection(damage.Direction, attack != null ? attack.Arc : 0f, attack != null ? attack.SlashSweep : SlashSweep.RightToLeft, 0.3f);
        BloodEffects.Spray(damage.HitPoint, way, force, blood);
        if ((damage.Technique & HitTechnique.Backstab) != 0 && !result.Killed)
        {
            // Run through: as much again, out the front.
            BloodEffects.Spray(damage.HitPoint, way, 1f, blood);
        }

        if (result.Killed)
        {
            // A killing blow opens it up: as hard as blood can fly, whatever the blow, and a splash where it stood.
            BloodEffects.Spray(damage.HitPoint, way, 1f, blood);
            BloodEffects.Pool(victim.transform.position, 1.1f, blood);
        }

        return true;
    }

    private static readonly Color DefaultBlood = new Color(0.32f, 0.015f, 0.02f, 0.95f);

    // Where the player's own blow is heard from: a hit past the range of the sound would tell the shooter nothing, so a far arrow is
    // brought near enough to hear. Everyone else's blows stay where they are.
    private Vector3 HeardAt(in DamageInfo damage)
    {
        float within = feel.HitHeardWithin;
        if (within <= 0f || playerCombat == null || damage.Source == null || damage.Source.gameObject != playerCombat.gameObject)
        {
            return damage.HitPoint;
        }

        Vector3 from = playerCombat.transform.position;
        Vector3 offset = damage.HitPoint - from;
        return offset.sqrMagnitude <= within * within ? damage.HitPoint : from + offset.normalized * within;
    }

    // The player's weapon's own hit effect, when the hit is the player's.
    private GameObject PlayerWeaponHitEffect(in DamageInfo damage)
    {
        bool players = playerCombat != null && damage.Source != null && damage.Source.gameObject == playerCombat.gameObject;
        return players && playerCombat.Weapon != null ? playerCombat.Weapon.HitEffect : null;
    }

    // The attack's own sound, else the weapon's (for the player), else the game-wide one.
    private SoundDefinition HitSoundFor(in DamageInfo damage, bool structure)
    {
        if (damage.Attack != null && damage.Attack.HitSound != null)
        {
            return damage.Attack.HitSound;
        }

        if (playerCombat != null && damage.Source != null && damage.Source.gameObject == playerCombat.gameObject
            && playerCombat.Weapon != null && playerCombat.Weapon.HitSound != null && !structure)
        {
            return playerCombat.Weapon.HitSound;
        }

        return Sounds == null ? null : structure ? Sounds.StructureHit : Sounds.Hit;
    }

    private void OnAnyGuarded(Damageable victim, DamageInfo damage, GuardOutcome outcome)
    {
        Vector3 point = damage.HitPoint;
        switch (outcome)
        {
            case GuardOutcome.Blocked:
                Spawn(feel.BlockEffect, point, -damage.Direction);
                AudioManager.Play(Sounds != null ? Sounds.Block : null, point);
                // Only a block the player is in stops time: a companion's block across the clearing froze the player's fight too.
                bool playersBlock = Player.Current != null && (victim == Player.Current.Health
                    || (damage.Source != null && Player.IsPlayer(damage.Source.gameObject)));
                if (playersBlock)
                {
                    HitStop(feel.HitStopOnHit);
                    CameraShake.Add(feel.ShakeOnHit);
                }

                break;
            case GuardOutcome.Parried:
                bool playerParried = Player.Current != null && victim == Player.Current.Health;
                if (playerParried)
                {
                    ControllerRumble.Pulse(ControllerSettings.Current.ParryPulse);
                }

                Spawn(feel.ParryEffect, point, -damage.Direction);
                AudioManager.Play(Sounds != null ? Sounds.Parry : null, point);
                CombatTechniqueSettings rules = CombatTechniqueSettings.Current;
                SlowTime(rules.SlowMotionScale, rules.SlowMotionSeconds);
                // The player's parry lands as hard as a heavy blow; a monster's parry of the player keeps the small hitch above.
                if (playerParried)
                {
                    HitStop(rules.ParryStop);
                }
                CameraShake.Add(feel.ShakeOnCrit);
                Callout(victim, feel.ParryText);
                // The flash is the player's reward; a monster's parry is the player's bad luck.
                if (screen != null && Player.Current != null && victim == Player.Current.Health)
                {
                    screen.Flash(feel.TechniqueFlashColor, 1f);
                }
                break;
            case GuardOutcome.GuardBroken:
                Spawn(feel.BlockEffect, point, -damage.Direction);
                AudioManager.Play(Sounds != null ? Sounds.GuardBreak : null, point);
                CameraShake.Add(feel.ShakeWhenHurt);
                Callout(victim, feel.GuardBrokenText);
                break;
        }
    }

    private void OnAnyStaggered(MonsterAI monster, float seconds)
    {
        Spawn(feel.StaggerEffect, monster.transform.position + Vector3.up * 0.05f, monster.transform.forward);
        AudioManager.Play(Sounds != null ? Sounds.Stagger : null, monster.transform.position);
        Callout(monster, feel.StaggerText);
        if (Player.Current != null && (monster.transform.position - Player.Current.transform.position).sqrMagnitude < BackstabMarkerRange * BackstabMarkerRange)
        {
            BackstabMarker.Show(monster);
        }
    }

    private const float BackstabMarkerRange = 12f;

    // A wind-up broken off by a blow (No Rest for the Wicked's Crucible: a spark on interrupt): sparks, a clang and a beat of hit stop.
    private void OnAnyInterrupted(MonsterAI monster, Combatant by)
    {
        Vector3 chest = monster.transform.position + Vector3.up * 1.2f;
        Spawn(feel.ParryEffect, chest, monster.transform.forward);
        AudioManager.Play(Sounds != null ? Sounds.Interrupt : null, chest);
        Callout(monster, feel.InterruptText);
        if (by != null && Player.IsPlayer(by.gameObject))
        {
            HitStop(feel.HitStopOnCrit);
        }
    }

    // A weapon's signature at work (a hammer's crush, a spear stopping a charge, an axe through a shield): a strong flash and the stagger's sound.
    private void OnSignatureUsed(WeaponSignature signature, MonsterAI monster)
    {
        if (monster == null)
        {
            return;
        }

        Vector3 chest = monster.transform.position + Vector3.up * 1.2f;
        Spawn(feel.CriticalHitEffect, chest, monster.transform.forward);
        AudioManager.Play(Sounds != null ? Sounds.GuardBreak : null, chest);
        CameraShake.Add(feel.ShakeOnCrit);
    }

    // ----- The player -----

    private void WatchPlayer(Player player)
    {
        if (watchedPlayer != null)
        {
            watchedPlayer.Evaded -= OnPlayerEvaded;
        }

        if (playerCombat != null)
        {
            playerCombat.SwingStarted -= OnSwingStarted;
            playerCombat.SwingLanded -= OnSwingLanded;
            playerCombat.PerfectDodge -= OnPerfectDodge;
            playerCombat.CounterReady -= OnCounterReady;
            playerCombat.ChargeFull -= OnChargeFull;
            playerCombat.SignatureUsed -= OnSignatureUsed;
            playerCombat.ArtNotReady -= OnArtNotReady;
        }

        if (playerDodge != null)
        {
            playerDodge.Dodged -= OnDodged;
        }

        if (playerStamina != null)
        {
            playerStamina.SpendRefused -= OnStaminaRefused;
        }

        if (playerArchery != null)
        {
            playerArchery.FullDraw -= OnFullDraw;
            playerArchery.Shot -= OnArrowLoosed;
        }

        watchedPlayer = player != null ? player.Health : null;
        playerCombat = player != null ? player.GetComponent<PlayerCombat>() : null;
        playerDodge = player != null ? player.GetComponent<PlayerDodge>() : null;
        playerStamina = player != null ? player.Stamina : null;
        playerArchery = player != null ? player.GetComponent<PlayerArchery>() : null;
        if (playerArchery != null)
        {
            playerArchery.FullDraw += OnFullDraw;
            playerArchery.Shot += OnArrowLoosed;
        }

        if (watchedPlayer != null)
        {
            watchedPlayer.Evaded += OnPlayerEvaded;
        }

        if (playerCombat != null)
        {
            playerCombat.SwingStarted += OnSwingStarted;
            playerCombat.SwingLanded += OnSwingLanded;
            playerCombat.PerfectDodge += OnPerfectDodge;
            playerCombat.CounterReady += OnCounterReady;
            playerCombat.ChargeFull += OnChargeFull;
            playerCombat.SignatureUsed += OnSignatureUsed;
            playerCombat.ArtNotReady += OnArtNotReady;
        }

        if (playerDodge != null)
        {
            playerDodge.Dodged += OnDodged;
        }

        if (playerStamina != null)
        {
            playerStamina.SpendRefused += OnStaminaRefused;
        }

        if (player == null)
        {
            return;
        }

        // Scene objects: made fresh for each scene's player.
        screen = CombatScreenEffects.Create();
        screen.Watch(watchedPlayer);
        if (playerCombat != null && CombatTechniqueSettings.Current.ShowComboHud)
        {
            comboHud = ComboHud.Create(playerCombat);
        }

        if (player.TryGetComponent(out PlayerArchery archery))
        {
            QuiverHud.Create(archery);
        }
    }

    private void OnSwingStarted(SwingInfo swing)
    {
        SoundDefinition sound = swing.Attack != null && swing.Attack.SwingSound != null ? swing.Attack.SwingSound
            : playerCombat.Weapon != null && playerCombat.Weapon.SwingSound != null && swing.Kind != SwingKind.Heavy ? playerCombat.Weapon.SwingSound
            : Sounds == null ? null : swing.Kind == SwingKind.Heavy ? Sounds.HeavySwing : Sounds.Swing;
        if (swing.Attack == null)
        {
            AudioManager.Play(sound, playerCombat.transform.position + Vector3.up);
            return;
        }

        pendingSwingSound = sound;
        pendingSwingStart = playerCombat.SwingStartTime;
        PlayPendingSwingSound();
    }

    // The arc sweeps through the swing's reach as it connects (or whiffs), the way the weapon moves in the animation
    // (the attack's Slash Sweep and Tilt, measured from its clip).
    private void OnSwingLanded(SwingInfo swing, int hits)
    {
        AttackDefinition attack = swing.Attack;
        if (!feel.SlashArcs || attack == null || swing.Kind == SwingKind.ShieldBash || feel.SlashMaterial == null)
        {
            return;
        }

        bool reverse = attack.SlashSweep == SlashSweep.RightToLeft;
        float tilt = attack.SlashTilt;
        Color color = attack.SlashColor;
        if (swing.IsCounter)
        {
            color = Color.Lerp(color, new Color(0.55f, 0.85f, 1f, color.a), 0.6f);
        }

        float brightness = feel.SlashBrightness * (1f + swing.Rhythm * 0.15f);
        color = new Color(color.r * brightness, color.g * brightness, color.b * brightness, color.a);
        Vector3 origin = playerCombat.transform.position + Vector3.up * feel.SlashHeight;
        SlashEffect.Play(feel.SlashMaterial, origin, swing.Direction, attack.Range + 0.2f, Mathf.Min(attack.Arc, 220f), color,
            feel.SlashSeconds * (swing.Kind == SwingKind.Heavy ? 1.3f : 1f), tilt, reverse);
    }

    private void OnPerfectDodge(DamageInfo damage)
    {
        CombatTechniqueSettings rules = CombatTechniqueSettings.Current;
        SlowTime(rules.SlowMotionScale, rules.SlowMotionSeconds);
        // A beat of weight, as a parry: it landed lighter than a jab.
        HitStop(rules.PerfectDodgeStop);
        Spawn(feel.PerfectDodgeEffect, playerCombat.transform.position + Vector3.up * 0.05f, Vector3.forward);
        AudioManager.Play(Sounds != null ? Sounds.PerfectDodge : null, playerCombat.transform.position);
        Callout(playerCombat, feel.PerfectDodgeText);
        if (screen != null)
        {
            screen.Flash(feel.TechniqueFlashColor, 0.8f);
        }
    }

    private void OnCounterReady()
    {
        AudioManager.Play(Sounds != null ? Sounds.CounterReady : null, playerCombat.transform.position);
    }

    // The creak and glint that say the draw is full: now, for a perfect release.
    private void OnFullDraw()
    {
        WeaponDefinition bow = playerCombat != null ? playerCombat.Weapon : null;
        Vector3 at = playerArchery.transform.position + Vector3.up * 1.4f;
        Spawn(feel.ParryCueEffect, at + playerArchery.transform.forward * 0.3f, Vector3.forward);
        AudioManager.Play(bow != null ? bow.FullDrawSound : null, at);
    }

    // A loosed bow kicks with the strength of its draw; a perfect release also gives a beat of weight, as a parry does.
    private void OnArrowLoosed(ArrowShot shot)
    {
        CameraShake.Add(Mathf.Lerp(feel.ShotKickMin, feel.ShotKickMax, shot.Draw));
        if (shot.Perfect)
        {
            CombatTechniqueSettings rules = CombatTechniqueSettings.Current;
            SlowTime(rules.SlowMotionScale, rules.SlowMotionSeconds);
            if (screen != null)
            {
                screen.Flash(feel.TechniqueFlashColor, 0.5f);
            }
        }
    }

    // The glint and chime that say a held heavy swing can go no further.
    private void OnChargeFull()
    {
        Spawn(feel.ParryCueEffect, playerCombat.transform.position + Vector3.up * 1.5f, Vector3.forward);
        AudioManager.Play(Sounds != null ? Sounds.CounterReady : null, playerCombat.transform.position);
    }

    private void OnDodged(Vector3 direction)
    {
        AudioManager.Play(Sounds != null ? Sounds.Dodge : null, playerDodge.transform.position);
    }

    private void OnStaminaRefused()
    {
        AudioManager.Play(Sounds != null ? Sounds.OutOfStamina : null, playerStamina.transform.position);
    }

    // An art asked for while it is still cooling down: the same short refusal, so the press isn't simply lost.
    private void OnArtNotReady()
    {
        AudioManager.Play(Sounds != null ? Sounds.OutOfStamina : null, playerCombat.transform.position);
    }

    // A dodge that avoided a hit some other way (not during its own invulnerability) still says so.
    private void OnPlayerEvaded(DamageInfo damage)
    {
        if (playerDodge != null && playerDodge.IsDodging)
        {
            return; // a perfect dodge: PlayerCombat reports it
        }

        Callout(watchedPlayer, feel.DodgedText, feel.DodgedTextColor);
    }

    // ----- Helpers -----

    private void Callout(Component target, string text) => Callout(target, text, feel.TechniqueTextColor);

    private static void Callout(Component target, string text, Color color)
    {
        if (target != null && !string.IsNullOrEmpty(text) && target.TryGetComponent(out DamageNumberSpawner spawner))
        {
            spawner.ShowText(text, color);
        }
    }

    private static void Spawn(GameObject effect, Vector3 position, Vector3 direction)
    {
        if (effect == null)
        {
            return;
        }

        direction.y = 0f;
        Quaternion rotation = direction.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(direction.normalized, Vector3.up) : Quaternion.identity;
        PrefabPool.Spawn(effect, position, rotation);
    }
}
