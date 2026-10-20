using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Makes hits feel like hits, game-wide: every target that is hit reacts (HitReaction: flash, punch, knockback); the
/// player's own hits and the hits the player takes also freeze the game for a moment (hit stop) and shake the camera.
/// A dodged hit shows "Dodged" over the player. Strengths come from Resources/CombatFeel.
/// <para>
/// Where it belongs: Combat. It is a persistent system started by GameSystems and listens to Damageable's game-wide
/// AnyDamaged event, so neither attackers nor targets know about it.
/// </para>
/// <para>
/// Extending: for a new kind of feedback (rumble, a hurt vignette), add it in OnAnyDamaged; for per-target behavior,
/// extend HitReaction. Keep timings in CombatFeelSettings so designers can tune them.
/// </para>
/// </summary>
public sealed class CombatFeedback : MonoBehaviour, IGameSystem
{
    private CombatFeelSettings feel;
    private float hitStopUntil = -1f;
    private Damageable watchedPlayer;

    public static CombatFeedback Instance { get; private set; }

    /// <summary>True while a hit stop slows the game.</summary>
    public bool IsHitStopped => hitStopUntil >= 0f;

    /// <summary>Called once by GameSystems when the game starts.</summary>
    public void Initialise(CombatFeelSettings settings)
    {
        Instance = this;
        feel = settings;
        Damageable.AnyDamaged += OnAnyDamaged;
    }

    private void OnDestroy()
    {
        Damageable.AnyDamaged -= OnAnyDamaged;
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
        WatchPlayer(Player.Current != null ? Player.Current.Health : null);

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
    public void HitStop(float seconds)
    {
        if (seconds <= 0f)
        {
            return;
        }

        hitStopUntil = Mathf.Max(hitStopUntil, Time.unscaledTime + seconds);
        if (!GamePause.IsTimeFrozen)
        {
            Time.timeScale = feel.HitStopTimeScale;
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
        // The pause owns time while it is frozen and restores it itself.
        if (!GamePause.IsTimeFrozen)
        {
            Time.timeScale = 1f;
        }
    }

    private void OnAnyDamaged(Damageable victim, DamageInfo damage, DamageResult result)
    {
        if (result.Damage <= 0f)
        {
            return;
        }

        HitReaction.For(victim).Play(damage, result);

        if (Player.Current != null && victim == Player.Current.Health)
        {
            HitStop(feel.HitStopWhenHurt);
            CameraShake.Add(feel.ShakeWhenHurt);
            return;
        }

        if (damage.Source != null && Player.IsPlayer(damage.Source.gameObject))
        {
            HitStop(result.Killed ? feel.HitStopOnKill : result.IsCritical ? feel.HitStopOnCrit : feel.HitStopOnHit);
            CameraShake.Add(result.Killed ? feel.ShakeOnKill : result.IsCritical ? feel.ShakeOnCrit : feel.ShakeOnHit);
        }
    }

    private void WatchPlayer(Damageable player)
    {
        if (watchedPlayer != null)
        {
            watchedPlayer.Evaded -= OnPlayerEvaded;
        }

        watchedPlayer = player;
        if (watchedPlayer != null)
        {
            watchedPlayer.Evaded += OnPlayerEvaded;
        }
    }

    private void OnPlayerEvaded(DamageInfo damage)
    {
        if (string.IsNullOrEmpty(feel.DodgedText) || watchedPlayer == null || !watchedPlayer.TryGetComponent(out DamageNumberSpawner spawner))
        {
            return;
        }

        spawner.ShowText(feel.DodgedText, feel.DodgedTextColor);
    }
}
