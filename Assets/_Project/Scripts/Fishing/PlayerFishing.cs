using UnityEngine;

/// <summary>
/// Fishing with a rod in hand, as in Palworld. Hold attack to swing back (longer is further), let go to cast towards the
/// cursor; a cast into water takes one bait from the bag (FishingSettings lists what works and how well). The float bobs
/// until a fish bites: it goes under with a splash and the player has a moment to strike (attack). Then the reeling game
/// (ReelSession): hold attack to lift the catch zone, let go to drop it, and keep it on the fish until it is landed.
/// Guard (right mouse) reels in at any time; walking away slackens the line. In still water a bite can instead be the
/// Nøkken (NokkenEncounter). FishingHud shows it all. Lives on the player beside PlayerArchery; the rod is a weapon of
/// kind Fishing Rod, so the attack and guard buttons are this component's while it is held. The float and the line are
/// a <see cref="FishingLine"/>'s.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerFishing : MonoBehaviour
{
    public enum Phase { None, Ready, Charging, Waiting, Bite, Reeling, Grabbed }

    private const float SlackDistance = 3.5f;

    private PlayerStateMachine stateMachine;
    private PlayerCombat combat;
    private PlayerMovement movement;
    private ItemInventory items;
    private Damageable health;
    private FishingLine look;
    private Vector3 castFrom;
    private Vector3 landing;
    private float waterHeight;
    private float chargeStarted;
    private float biteAt;
    private float strikeUntil;
    private float castLanding;
    private bool held;
    private bool still;
    private FishingSettings.Fish hooked;

    public static PlayerFishing Current { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Current = null;

    public Phase State { get; private set; }
    public ReelSession Reel { get; private set; }
    public FishingSettings.Fish Hooked => hooked;
    public Transform Bobber => look?.Bobber;
    public WeaponDefinition Rod => combat != null && combat.Weapon != null && combat.Weapon.IsFishingRod ? combat.Weapon : null;

    /// <summary>0..1 while swinging back for a cast.</summary>
    public float Charge => State == Phase.Charging ? Mathf.Clamp01((Time.time - chargeStarted) / FishingSettings.Current.ChargeSeconds) : 0f;

    /// <summary>Seconds left to strike while a fish has the bait.</summary>
    public float StrikeLeft => State == Phase.Bite ? Mathf.Max(0f, strikeUntil - Time.time) : 0f;

    private void Awake()
    {
        stateMachine = GetComponent<PlayerStateMachine>();
        combat = GetComponent<PlayerCombat>();
        movement = GetComponent<PlayerMovement>();
        items = GetComponent<ItemInventory>();
        health = GetComponent<Damageable>();
        look = new FishingLine(transform);
    }

    private void OnEnable()
    {
        Current = this;
        stateMachine.AttackRequested += OnAttackPressed;
        stateMachine.AttackReleased += OnAttackReleased;
        stateMachine.GuardRequested += OnGuard;
    }

    private void OnDisable()
    {
        stateMachine.AttackRequested -= OnAttackPressed;
        stateMachine.AttackReleased -= OnAttackReleased;
        stateMachine.GuardRequested -= OnGuard;
        EndLine(null);
        if (Current == this)
        {
            Current = null;
        }
    }

    private void Start()
    {
        if (GetComponent<Player>() == Player.Current)
        {
            FishingHud.Create();
        }
    }

    // ----- Input -----

    private void OnAttackPressed()
    {
        held = true;
        if (Rod == null || health != null && !health.IsAlive)
        {
            return;
        }

        switch (State)
        {
            case Phase.None:
            case Phase.Ready:
                if (FindBait(out _) < 0)
                {
                    GameMessages.Post("You need bait: grubs from rotten wood, or berries.", MessageKind.Warning);
                    return;
                }

                State = Phase.Charging;
                chargeStarted = Time.time;
                break;
            case Phase.Waiting:
                EndLine("You reel in. The bait is gone.");
                break;
            case Phase.Bite:
                Strike();
                break;
        }
    }

    private void OnAttackReleased()
    {
        held = false;
        if (State == Phase.Charging)
        {
            Cast();
        }
    }

    private void OnGuard(bool raised)
    {
        if (raised && State != Phase.None && State != Phase.Ready && State != Phase.Grabbed)
        {
            EndLine("You reel in.");
        }
    }

    // ----- Fishing -----

    private void Update()
    {
        if (State == Phase.Grabbed)
        {
            return;
        }

        if (Rod == null)
        {
            if (State != Phase.None)
            {
                EndLine(null);
                State = Phase.None;
            }

            return;
        }

        if (State == Phase.None)
        {
            State = Phase.Ready;
        }

        movement.SetSpeedScale(this, State == Phase.Charging ? 0.3f : State == Phase.Reeling ? 0f : State == Phase.Ready ? 1f : 0.5f);
        switch (State)
        {
            case Phase.Charging:
                FaceCursor();
                break;
            case Phase.Waiting:
                if (Flat(transform.position - castFrom).magnitude > SlackDistance)
                {
                    EndLine("You walked off. The line went slack.");
                    return;
                }

                if (Time.time >= biteAt)
                {
                    Bite();
                }

                break;
            case Phase.Bite:
                if (Time.time > strikeUntil)
                {
                    EndLine("It got away with the bait.");
                }

                break;
            case Phase.Reeling:
                Reel.Tick(Time.deltaTime, held);
                if (Reel.Caught)
                {
                    Land();
                }
                else if (Reel.Escaped)
                {
                    EndLine($"The {hooked.item.DisplayName} got away!");
                }

                break;
        }

        AnimateBobber();
    }

    private void Cast()
    {
        FishingSettings settings = FishingSettings.Current;
        float charge = Charge;
        Vector3 direction = AimDirection();
        float distance = Mathf.Lerp(settings.MinimumCast, settings.MaximumCast, charge);
        Vector3 target = transform.position + direction * distance;
        WaterSurface water = WaterSurface.At(new Vector3(target.x, transform.position.y + 1f, target.z), 6f, out float height);
        if (water == null)
        {
            State = Phase.Ready;
            GameMessages.Post("Cast into the water.", MessageKind.Warning);
            return;
        }

        int slot = FindBait(out float lure);
        if (slot < 0)
        {
            State = Phase.Ready;
            return;
        }

        items.Container.RemoveAt(slot, 1);
        AudioManager.Play(settings.CastSound, transform.position);
        castFrom = transform.position;
        still = water.IsStill;
        waterHeight = height;
        landing = new Vector3(target.x, height, target.z);
        castLanding = Time.time + FishingLine.FlightSeconds;
        // The Nøkk's air (SpiritBonds): the water's own spirit sends fish to the line.
        float favour = SpiritBonds.HasGift(SpiritBonds.NokkGift) ? 1.5f : 1f;
        biteAt = castLanding + Random.Range(settings.BiteWait.x, settings.BiteWait.y) / Mathf.Max(0.1f, lure * favour);
        State = Phase.Waiting;
        ShowLine();
    }

    private void Bite()
    {
        FishingSettings settings = FishingSettings.Current;
        State = Phase.Bite;
        strikeUntil = Time.time + settings.StrikeWindow;
        AudioManager.Play(settings.BiteSound, landing);
        CameraShake.Add(0.08f);
    }

    private void Strike()
    {
        FishingSettings settings = FishingSettings.Current;
        if (still && !SpiritBonds.HasGift(SpiritBonds.NokkGift) && Random.value < settings.NokkenChance)
        {
            State = Phase.Grabbed;
            HideLine();
            movement.SetSpeedScale(this, 1f);
            NokkenEncounter.Begin(this, landing, waterHeight, () =>
            {
                State = Rod != null ? Phase.Ready : Phase.None;
            });
            return;
        }

        hooked = settings.PickFish(still, Random.value);
        if (hooked == null)
        {
            EndLine("Nothing worth keeping bites here.");
            return;
        }

        Reel = new ReelSession(hooked.fight, settings.ZoneSize, settings.ZoneLift, settings.ZoneFall, settings.CatchFill, settings.CatchDrain,
            Random.Range(0, int.MaxValue));
        State = Phase.Reeling;
    }

    private void Land()
    {
        FishingSettings settings = FishingSettings.Current;
        ItemDefinition fish = hooked.item;
        float kilograms = Random.Range(hooked.size.x, hooked.size.y);
        if (items.Give(fish, 1) < 1)
        {
            GameMessages.Post($"No room for the {fish.DisplayName}. You let it go.", MessageKind.Warning);
        }
        else
        {
            GameMessages.Post($"Caught a {fish.DisplayName}! ({kilograms:0.0} kg)", MessageKind.Gain);
            GameEvents.Raise(new GameEvent(GameEventKind.FishCaught, fish, 1, gameObject));
            ExperienceTracker.Grant(LevelingSettings.Current.CatchExperience);
        }

        AudioManager.Play(settings.CatchSound, transform.position);
        EndLine(null);
    }

    /// <summary>Takes the line in: the float and line go, the reeling ends. A message says why, when given.</summary>
    public void EndLine(string message)
    {
        HideLine();
        Reel = null;
        hooked = null;
        if (movement != null)
        {
            movement.SetSpeedScale(this, 1f);
        }

        if (State != Phase.Grabbed)
        {
            State = Rod != null ? Phase.Ready : Phase.None;
        }

        if (!string.IsNullOrEmpty(message))
        {
            GameMessages.Post(message);
        }
    }

    // The first bait in the bag that FishingSettings knows, and how well it lures. -1 when there is none.
    private int FindBait(out float lure)
    {
        lure = 1f;
        ItemContainer bag = items.Container;
        foreach (FishingSettings.Bait bait in FishingSettings.Current.BaitList)
        {
            for (int i = 0; i < bag.SlotCount; i++)
            {
                ItemStack stack = bag.GetSlot(i);
                if (!stack.IsEmpty && stack.Item == bait.item)
                {
                    lure = bait.lure;
                    return i;
                }
            }
        }

        return -1;
    }

    private Vector3 AimDirection()
    {
        Camera view = Camera.main;
        if (view != null && CameraPointer.TryGetPointOnPlane(view, new Plane(Vector3.up, transform.position), out Vector3 point))
        {
            Vector3 toward = Flat(point - transform.position);
            if (toward.sqrMagnitude > 0.25f)
            {
                return toward.normalized;
            }
        }

        return Flat(transform.forward).normalized;
    }

    private void FaceCursor()
    {
        Vector3 direction = AimDirection();
        if (direction.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 12f);
        }
    }

    // ----- The float and the line -----

    private void ShowLine()
    {
        look.Show();
        AnimateBobber();
    }

    private void HideLine() => look.Hide();

    private void AnimateBobber() => look.Tick(landing, castLanding, State, Reel, castFrom, waterHeight);

    private void OnDestroy() => look?.Destroy();

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
}
