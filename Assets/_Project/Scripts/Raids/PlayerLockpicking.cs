using UnityEngine;

/// <summary>
/// Runs the lock minigame on the player. When an interaction starts on a locked LootCache it holds the interaction open and
/// runs a LockSession: each Interact or Attack press tries a pin, and is heard (PlayerNoise). When the lock opens, by skill
/// or by force, the cache is unlocked and the interaction completes as usual, so the search itself stays LootCache's.
/// Walking away or being hit stops it, and the lock is back as it was. Lives on the PlayerArmature prefab, next to
/// PlayerInteractor.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInteractor), typeof(PlayerStateMachine))]
public sealed class PlayerLockpicking : MonoBehaviour
{
    private readonly System.Random random = new System.Random();
    private PlayerInteractor interactor;
    private PlayerStateMachine stateMachine;
    private PlayerNoise noise;
    private Damageable health;
    private TimingBarView barView;
    private Interactable target;
    private LootCache cache;
    private float autoTimer;
    private bool explained;

    /// <summary>The lock being picked, or null.</summary>
    public LockSession Session { get; private set; }

    private void Awake()
    {
        interactor = GetComponent<PlayerInteractor>();
        stateMachine = GetComponent<PlayerStateMachine>();
        noise = GetComponent<PlayerNoise>();
        health = GetComponent<Damageable>();
    }

    private void Start()
    {
        barView = TimingBarView.Attach(() => Session);
    }

    private void OnDestroy()
    {
        if (barView != null)
        {
            Destroy(barView.gameObject);
        }
    }

    private void OnEnable()
    {
        interactor.InteractionStarted += OnInteractionStarted;
        interactor.InteractionEnded += OnInteractionEnded;
        stateMachine.WorkPressed += OnWorkPressed;
        if (health != null)
        {
            health.Damaged += OnDamaged;
        }
    }

    private void OnDisable()
    {
        interactor.InteractionStarted -= OnInteractionStarted;
        interactor.InteractionEnded -= OnInteractionEnded;
        stateMachine.WorkPressed -= OnWorkPressed;
        if (health != null)
        {
            health.Damaged -= OnDamaged;
        }

        Session = null;
    }

    private void Update()
    {
        if (Session == null || Session.IsDone)
        {
            return;
        }

        if (!GatheringMinigameSettings.Current.AutoGather)
        {
            Session.Tick(Time.deltaTime);
            return;
        }

        autoTimer += Time.deltaTime;
        if (autoTimer >= LockpickingSettings.Current.AutoPinSeconds)
        {
            autoTimer = 0f;
            Session.Resolve(SwingResult.Normal);
            AfterPress(SwingResult.Normal);
        }
    }

    private void OnInteractionStarted(Interactable started)
    {
        Session = null;
        if (started == null || !started.TryGetComponent(out LootCache startedCache) || !startedCache.IsLocked)
        {
            return;
        }

        target = started;
        cache = startedCache;
        target.HoldOpen = true;
        autoTimer = 0f;
        LockpickingSettings settings = LockpickingSettings.Current;
        Session = new LockSession(cache.LockPins, settings.SlipsAllowed, settings.CycleSeconds, settings.NormalZoneWidth,
            settings.CriticalZoneWidth, settings.NarrowingPerPin, settings.ZoneJitter, random);
        if (!explained)
        {
            explained = true;
            GameMessages.Post("Press when the marker crosses the zone. Green is silent; a slip rattles the lock.");
        }
    }

    private void OnInteractionEnded(Interactable ended, InteractionResult result)
    {
        if (ended != target)
        {
            return;
        }

        Session = null;
        target = null;
        cache = null;
    }

    private void OnWorkPressed()
    {
        if (Session == null || Session.IsDone || GatheringMinigameSettings.Current.AutoGather)
        {
            return;
        }

        AfterPress(Session.Press());
    }

    private void OnDamaged(DamageInfo info, DamageResult result)
    {
        if (Session != null && LockpickingSettings.Current.InterruptOnHit && !info.IsOverTime)
        {
            GameMessages.Post("Lockpicking interrupted.", MessageKind.Warning);
            stateMachine.EndInteraction();
        }
    }

    private void AfterPress(SwingResult result)
    {
        LockpickingSettings settings = LockpickingSettings.Current;
        Vector3 position = target != null ? target.transform.position : transform.position;
        bool forced = Session.IsForced;
        AudioManager.Play(forced ? settings.ForcedSound : settings.SoundOf(result), position);
        if (noise != null)
        {
            noise.Make(position, forced ? settings.ForcedNoise : settings.NoiseOf(result));
        }

        if (barView != null)
        {
            barView.Flash(forced ? "Forced open!" : result == SwingResult.Critical ? "Silent" : result == SwingResult.Normal ? "Click" : "Slipped!", result);
        }

        if (Session.IsDone && target != null)
        {
            cache.Unlock(forced);
            target.Complete();
        }
    }
}
