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
    private CombatScreenEffects screen;
    private ComboHud comboHud;

    public static CombatFeedback Instance { get; private set; }

    /// <summary>True while a hit stop slows the game.</summary>
    public bool IsHitStopped => hitStopUntil >= 0f;

    private static AudioSettingsDefinition Sounds => AudioManager.Sounds;

    /// <summary>Called once by GameSystems when the game starts.</summary>
    public void Initialise(CombatFeelSettings settings)
    {
        Instance = this;
        feel = settings;
        Damageable.AnyDamaged += OnAnyDamaged;
        Damageable.AnyGuarded += OnAnyGuarded;
        MonsterAI.AnyStaggered += OnAnyStaggered;
    }

    private void OnDestroy()
    {
        Damageable.AnyDamaged -= OnAnyDamaged;
        Damageable.AnyGuarded -= OnAnyGuarded;
        MonsterAI.AnyStaggered -= OnAnyStaggered;
        WatchPlayer(null);
        EndHitStop();
        if (Instance == this)
        {
            Instance = null;
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

    /// <summary>Slows the game to <paramref name="scale"/> for <paramref name="seconds"/> of real time. Overlapping requests
    /// keep the slowest and the longest.</summary>
    public void SlowTime(float scale, float seconds)
    {
        if (seconds <= 0f)
        {
            return;
        }

        hitStopScale = hitStopUntil >= 0f ? Mathf.Min(hitStopScale, scale) : scale;
        hitStopUntil = Mathf.Max(hitStopUntil, Time.unscaledTime + seconds);
        if (!GamePause.IsTimeFrozen)
        {
            Time.timeScale = hitStopScale;
        }
    }

    private void Update()
    {
        if (hitStopUntil >= 0f && Time.unscaledTime >= hitStopUntil)
        {
            EndHitStop();
        }
    }

    private void EndHitStop()
    {
        if (hitStopUntil < 0f)
        {
            return;
        }

        hitStopUntil = -1f;
        hitStopScale = 1f;
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
        bool strong = result.IsCritical || (damage.Technique & (HitTechnique.Heavy | HitTechnique.Finisher | HitTechnique.Counter)) != 0;
        if (!result.Blocked)
        {
            GameObject effect = structure ? feel.StructureHitEffect : strong ? feel.CriticalHitEffect : feel.HitEffect;
            Spawn(effect, damage.HitPoint, damage.Direction);
            AudioManager.Play(HitSoundFor(damage, structure), damage.HitPoint);
            if (strong && !structure)
            {
                AudioManager.Play(Sounds != null ? Sounds.CriticalHit : null, damage.HitPoint);
            }

            if (victim.TryGetComponent(out IEventSubject known) && known.EventSubject is ICombatSounds sounds)
            {
                AudioManager.Play(sounds.HurtSound, victim.transform.position);
            }
        }

        if (Player.Current != null && victim == Player.Current.Health)
        {
            if (!result.Blocked)
            {
                HitStop(feel.HitStopWhenHurt);
                CameraShake.Add(feel.ShakeWhenHurt);
                AudioManager.Play(Sounds != null ? Sounds.PlayerHurt : null, victim.transform.position);
            }

            return;
        }

        if (damage.Source != null && Player.IsPlayer(damage.Source.gameObject))
        {
            float extra = (damage.Technique & (HitTechnique.Heavy | HitTechnique.Counter)) != 0 ? 1.5f
                : (damage.Technique & HitTechnique.Finisher) != 0 ? 1.25f : 1f;
            HitStop((result.Killed ? feel.HitStopOnKill : strong ? feel.HitStopOnCrit : feel.HitStopOnHit) * extra);
            CameraShake.Add((result.Killed ? feel.ShakeOnKill : strong ? feel.ShakeOnCrit : feel.ShakeOnHit) * extra);
        }
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
                HitStop(feel.HitStopOnHit);
                CameraShake.Add(feel.ShakeOnHit);
                break;
            case GuardOutcome.Parried:
                Spawn(feel.ParryEffect, point, -damage.Direction);
                AudioManager.Play(Sounds != null ? Sounds.Parry : null, point);
                CombatTechniqueSettings rules = CombatTechniqueSettings.Current;
                SlowTime(rules.SlowMotionScale, rules.SlowMotionSeconds);
                CameraShake.Add(feel.ShakeOnCrit);
                Callout(victim, feel.ParryText);
                screen?.Flash(feel.TechniqueFlashColor, 1f);
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
        }

        if (playerDodge != null)
        {
            playerDodge.Dodged -= OnDodged;
        }

        if (playerStamina != null)
        {
            playerStamina.SpendRefused -= OnStaminaRefused;
        }

        watchedPlayer = player != null ? player.Health : null;
        playerCombat = player != null ? player.GetComponent<PlayerCombat>() : null;
        playerDodge = player != null ? player.GetComponent<PlayerDodge>() : null;
        playerStamina = player != null ? player.Stamina : null;

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
        AudioManager.Play(sound, playerCombat.transform.position + Vector3.up);
    }

    // The arc sweeps through the swing's reach as it connects (or whiffs). Chain steps alternate sides; heavy swings
    // come down steeply.
    private void OnSwingLanded(SwingInfo swing, int hits)
    {
        AttackDefinition attack = swing.Attack;
        if (attack == null || swing.Kind == SwingKind.ShieldBash || feel.SlashMaterial == null)
        {
            return;
        }

        bool reverse = swing.Kind == SwingKind.Light && swing.Step % 2 == 1;
        float tilt = swing.Kind == SwingKind.Heavy ? 70f : swing.IsFinisher ? 0f : reverse ? -14f : 14f;
        Color color = attack.SlashColor;
        if (swing.IsCounter)
        {
            color = Color.Lerp(color, new Color(0.55f, 0.85f, 1f, color.a), 0.6f);
        }

        color *= 1f + swing.Rhythm * 0.15f;
        Vector3 origin = playerCombat.transform.position + Vector3.up * feel.SlashHeight;
        SlashEffect.Play(feel.SlashMaterial, origin, swing.Direction, attack.Range + 0.2f, Mathf.Min(attack.Arc, 220f), color,
            feel.SlashSeconds * (swing.Kind == SwingKind.Heavy ? 1.3f : 1f), tilt, reverse);
    }

    private void OnPerfectDodge(DamageInfo damage)
    {
        CombatTechniqueSettings rules = CombatTechniqueSettings.Current;
        SlowTime(rules.SlowMotionScale, rules.SlowMotionSeconds);
        Spawn(feel.PerfectDodgeEffect, playerCombat.transform.position + Vector3.up * 0.05f, Vector3.forward);
        AudioManager.Play(Sounds != null ? Sounds.PerfectDodge : null, playerCombat.transform.position);
        Callout(playerCombat, feel.PerfectDodgeText);
        screen?.Flash(feel.TechniqueFlashColor, 0.8f);
    }

    private void OnCounterReady()
    {
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
