using UnityEngine;

/// <summary>
/// Fishing with a rod in hand. Hold attack to raise the rod and swing it back (longer casts further), let go to throw:
/// the float leaves the tip at the throw's flick and flies towards the cursor; a cast into water takes one bait from the
/// bag (FishingSettings lists what works and how well). The float bobs until a fish bites: it goes under with a splash and
/// the player has a moment to strike (attack). Then the fight (<see cref="FishFight"/>): hold attack to reel in, let go to
/// give line when the fish runs, and lean the rod against the run with the movement keys; a tired fish comes to the bank
/// and is landed, swung out on the line if it is light, lifted up with both hands if it is heavy. The view comes down
/// close on the player and the float while a fish is on. Guard (right mouse) or a step away takes the line in while
/// waiting. In still water a bite can instead be the Nøkken (NokkenEncounter). FishingHud shows it all; the poses are
/// PlayerAnimationController's (<see cref="FishingPose"/>); the float, the line and the landed fish are a
/// <see cref="FishingLine"/>'s. Lives on the player beside PlayerArchery; the rod is a weapon of kind Fishing Rod, so the
/// attack and guard buttons are this component's while it is held.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerFishing : MonoBehaviour, IPlayerActivity
{
    // Casting and Landing came later: the numbers are sent to friends in co-op, so the older ones keep theirs.
    public enum Phase { None, Ready, Charging, Waiting, Bite, Reeling, Grabbed, Casting, Landing }

    // How far the body turns with the rod when it is leaned to a side, in degrees.
    private const float LeanDegrees = 28f;
    // Seconds to give up waiting for the throw's flick (an animator without the fishing states).
    private const float CastTimeout = 1.6f;

    private PlayerStateMachine stateMachine;
    private PlayerCombat combat;
    private PlayerMovement movement;
    private PlayerInteractor interactor;
    private PlayerAnimationController poses;
    private ItemInventory items;
    private Damageable health;
    private FishingLine look;
    private FishingCast cast;
    private float chargeStarted;
    private float castCharge;
    private float castReleasedAt;
    private float biteAt;
    private float strikeUntil;
    private bool held;
    private bool still;
    private FishingSettings.Fish hooked;
    private float kilograms;
    // The hooked fish's length, and how much the bait on the line eases a big one (FishSizes).
    private float centimetres;
    private float baitEase;
    private float startDistance;
    private float rodSide;
    private Vector3 fishAt;
    private FishFight.Mood lastMood;
    private float nextReelSound;
    private float nextStrainSound;
    private float nextSplash;
    private float landingStarted;
    private bool landed;
    private bool landingLetGo;
    private float landingLetGoAt = -1f;
    private float heldUpAt = -1f;
    private bool haul;
    private Transform closeUp;

    public static PlayerFishing Current { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Current = null;

    public Phase State { get; private set; }

    /// <summary>The fight with the fish on the line, while reeling; else null.</summary>
    public FishFight Fight { get; private set; }
    public FishingSettings.Fish Hooked => hooked;
    public Transform Bobber => look?.Bobber;

    /// <summary>Where the hooked fish is on the water.</summary>
    public Vector3 FishPosition => fishAt;

    /// <summary>True while the reel is being turned (attack held).</summary>
    public bool ReelHeld => Held && State == Phase.Reeling;

    /// <summary>Probes: stands in for the attack button while reeling (the input system ignores the mouse while the editor
    /// window is unfocused), and for the movement keys' lean of the rod (-1 to 1). Null: the real input.</summary>
    public bool? HeldForTests { get; set; }
    public float? RodSideForTests { get; set; }

    private bool Held => HeldForTests ?? held;

    /// <summary>Where the rod is leaned, -1 (left of the line) to 1 (right).</summary>
    public float RodSide => rodSide;

    /// <summary>The way to lean the rod now to turn the fish (flat, world space), or zero when the side doesn't matter.</summary>
    public Vector3 CounterDirection => Fight != null && Fight.Counter != 0 ? RightOfLine() * Fight.Counter : Vector3.zero;

    /// <summary>True while a line is in the water: cast, bitten, being reeled in or landed.</summary>
    public bool LineOut => State == Phase.Waiting || State == Phase.Bite || State == Phase.Reeling || State == Phase.Landing;

    /// <summary>Where the last line was cast (co-op tells friends, so they see the float).</summary>
    public FishingCast LastCast => cast;

    /// <summary>How far the catch is reeled in, 0 to 1; 0 when not reeling.</summary>
    public float ReelCatch => State == Phase.Landing ? 1f
        : Fight != null ? Mathf.Clamp01(1f - (Fight.Distance - FishingSettings.Current.Fight.landDistance) / Mathf.Max(0.5f, startDistance - FishingSettings.Current.Fight.landDistance))
        : 0f;

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
        interactor = GetComponent<PlayerInteractor>();
        poses = GetComponent<PlayerAnimationController>();
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
        PlayerInterrupts.For(gameObject).Add(this);
    }

    // Killed with a line out: the line goes with them. The fight ticked on while they lay there and woke them rooted, facing
    // the water, at the portal.
    /// <summary>Death alone ends it (PlayerInterrupts tells every activity of a death).</summary>
    public PlayerInterrupt EndsOn => PlayerInterrupt.None;

    public void Interrupt(PlayerInterrupt reason) => EndLine(null);

    private void OnDisable()
    {
        stateMachine.AttackRequested -= OnAttackPressed;
        stateMachine.AttackReleased -= OnAttackReleased;
        stateMachine.GuardRequested -= OnGuard;
        if (TryGetComponent(out PlayerInterrupts interrupts))
        {
            interrupts.Remove(this);
        }

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
                if (interactor != null && interactor.Current != null)
                {
                    return;
                }

                // Not from a seat (the saddle, a boat) or with a cart in hand: the cast and the fight root the angler where they stand.
                if (movement != null && movement.IsSeated
                    || (TryGetComponent(out PlayerTransport transport) && transport.Using != null))
                {
                    GameMessages.Post("Stand on your own feet to fish.", MessageKind.Warning);
                    return;
                }

                if (FindBait(out _, out _) < 0)
                {
                    GameMessages.Post("You need bait: grubs from rotten wood, or berries.", MessageKind.Warning);
                    return;
                }

                State = Phase.Charging;
                chargeStarted = Time.time;
                if (poses != null)
                {
                    poses.BeginCast();
                }

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
            Release();
        }
    }

    private void OnGuard(bool raised)
    {
        // A fish on the line is only lost by the fight itself, never by a stray right click.
        if (raised && (State == Phase.Waiting || State == Phase.Bite))
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

        if (Rod == null || interactor != null && interactor.Current != null)
        {
            if (State != Phase.None && State != Phase.Ready)
            {
                EndLine(null);
            }

            State = Rod != null ? Phase.Ready : Phase.None;
            return;
        }

        if (State == Phase.None)
        {
            State = Phase.Ready;
        }

        // The poses are full-body: the feet stay put from the cast until the line is in.
        movement.SetSpeedScale(this, State == Phase.Ready ? 1f : 0f);
        switch (State)
        {
            case Phase.Charging:
                FaceCursor();
                if (poses != null)
                {
                    poses.HoldCast(true, FishingSettings.Current.CastHoldPoint);
                }

                break;
            case Phase.Casting:
                if (poses != null)
                {
                    poses.HoldCast(false, FishingSettings.Current.CastHoldPoint);
                }

                if (poses == null || poses.CastFlicked(FishingSettings.Current.CastReleasePoint) || Time.time - castReleasedAt > CastTimeout)
                {
                    Throw();
                }

                break;
            case Phase.Waiting:
                if (Stepping())
                {
                    EndLine(null);
                    return;
                }

                if (Time.time >= biteAt)
                {
                    Bite();
                }

                break;
            case Phase.Bite:
                if (Stepping())
                {
                    EndLine("You stepped off. It got away with the bait.");
                    return;
                }

                if (Time.time > strikeUntil)
                {
                    EndLine("It got away with the bait.");
                }

                break;
            case Phase.Reeling:
                UpdateFight(Time.deltaTime);
                return;
            case Phase.Landing:
                UpdateLanding();
                return;
        }

        if (State == Phase.Waiting || State == Phase.Bite)
        {
            look.Tick(cast, State, 0f);
        }
    }

    private bool Stepping() => movement.MoveKeys.sqrMagnitude > 0.25f;

    // Let go of a charged cast: where it will land is settled now, and the rod throws it at the flick.
    private void Release()
    {
        FishingSettings settings = FishingSettings.Current;
        castCharge = Charge;
        float distance = Mathf.Lerp(settings.MinimumCast, settings.MaximumCast, castCharge);
        if (!WaterAlong(AimDirection(), distance, out _, out _, out _))
        {
            GameMessages.Post("Cast into the water.", MessageKind.Warning);
            EndLine(null);
            return;
        }

        State = Phase.Casting;
        castReleasedAt = Time.time;
    }

    private void Throw()
    {
        // The first cast opens the fishing tutorial, bait or no bait: it says where bait comes from.
        JournalNotices.Tried(ProficiencyTraining.Fishing);
        FishingSettings settings = FishingSettings.Current;
        Vector3 direction = Flat(transform.forward).normalized;
        float distance = Mathf.Lerp(settings.MinimumCast, settings.MaximumCast, castCharge);
        WaterAlong(direction, distance, out Vector3 target, out float height, out WaterSurface water);
        int slot = FindBait(out float lure, out baitEase);
        if (water == null || slot < 0)
        {
            GameMessages.Post(water == null ? "Cast into the water." : "You need bait.", MessageKind.Warning);
            EndLine(null);
            return;
        }

        items.Container.RemoveAt(slot, 1);
        AudioManager.Play(settings.CastSound, transform.position);
        cast = new FishingCast(transform.position, new Vector3(target.x, height, target.z), height, Time.time + FishingLine.FlightSeconds);
        still = water.IsStill;
        // The Nøkk's air (SpiritBonds): the water's own spirit sends fish to the line.
        float favour = SpiritBonds.HasGift(SpiritBonds.NokkGift) ? 1.5f : 1f;
        float quick = Techniques.Has(PerkTechnique.QuickBite) ? TechniqueSettings.Current.QuickBite : 1f;
        if (quick > 1f)
        {
            Techniques.Used(PerkTechnique.QuickBite);
        }

        biteAt = cast.LandsAt + Random.Range(settings.BiteWait.x, settings.BiteWait.y) / Mathf.Max(0.1f, lure * favour * quick);
        State = Phase.Waiting;
        // A fish in sight near the float comes to it (and what is right under it scatters).
        if (FishInTheWater.Current != null)
        {
            FishInTheWater.Current.Attract(new Vector3(target.x, height, target.z), baitEase);
        }

        look.Show();
        look.Tick(cast, State, 0f);
        // The float stays in the picture while it is out, so a bite is seen wherever the line was cast.
        closeUp = look.Bobber;
        if (PlayerCameraController.Current != null)
        {
            PlayerCameraController.Current.KeepInView(closeUp);
        }
    }

    private void Bite()
    {
        FishingSettings settings = FishingSettings.Current;
        State = Phase.Bite;
        strikeUntil = Time.time + settings.StrikeWindow;
        AudioManager.Play(settings.BiteSound, cast.Landing);
        look.Splash(cast.Landing);
        CameraShake.Add(0.08f);
        ControllerRumble.Pulse(ControllerSettings.Current.BitePulse);
    }

    private void Strike()
    {
        FishingSettings settings = FishingSettings.Current;
        if (still && !SpiritBonds.HasGift(SpiritBonds.NokkGift) && Random.value < settings.NokkenChance)
        {
            State = Phase.Grabbed;
            look.Hide();
            if (poses != null)
            {
                poses.EndFishing();
            }

            movement.SetSpeedScale(this, 1f);
            NokkenEncounter.Begin(this, cast.Landing, cast.WaterHeight, () =>
            {
                State = Rod != null ? Phase.Ready : Phase.None;
            });
            return;
        }

        // The fish that came to the float when there was one in sight (FishInTheWater); else one of the water's, sized now.
        if (FishInTheWater.Current != null && FishInTheWater.Current.TakeBiter(cast.Landing, out FishingSettings.Fish biter, out float biterLength))
        {
            hooked = biter;
            centimetres = biterLength;
        }
        else
        {
            hooked = settings.PickFish(still, Random.value);
            centimetres = hooked != null ? FishSizes.Roll(Random.value, Random.value, hooked.lengthScale) : 0f;
        }

        if (hooked == null)
        {
            EndLine("Nothing worth keeping bites here.");
            return;
        }

        kilograms = FishSizes.Kilograms(hooked, centimetres);
        float size = Mathf.InverseLerp(hooked.size.x, hooked.size.y, kilograms);
        // The bigger it is the harder it fights; good bait takes some of that out of it.
        float heft = FishSizes.Eased(FishSizes.Difficulty(centimetres), baitEase);
        // The Fishing skill (and its perks) is line control: the line holds longer and fish tire sooner.
        float control = TryGetComponent(out Combatant self) ? Mathf.Max(0.2f, 1f + self.Stats.Get(StatType.ReelZone)) : 1f;
        startDistance = Flat(cast.Landing - transform.position).magnitude;
        Fight = new FishFight(settings.Fight, hooked.fight, size, control, startDistance, Random.Range(0, int.MaxValue), heft);
        fishAt = cast.Landing;
        rodSide = 0f;
        lastMood = Fight.Current;
        State = Phase.Reeling;
        movement.FacingLocked = true;
        if (poses != null)
        {
            poses.SetFishingPose(FishingPose.Fighting);
        }

        AudioManager.Play(settings.SplashSound, cast.Landing);
        look.Splash(cast.Landing, 0f);
        CameraShake.Add(0.12f);
        closeUp = look.Bobber;
        if (PlayerCameraController.Current != null)
        {
            PlayerCameraController.Current.BeginCloseUp(closeUp);
        }
    }

    private void UpdateFight(float seconds)
    {
        FishingSettings settings = FishingSettings.Current;
        rodSide = Mathf.MoveTowards(rodSide, RodInput(), 5f * seconds);
        float before = Fight.Distance;
        Fight.Tick(seconds, Held, rodSide);
        PlaceFish(before);
        FishFight.Mood mood = Fight.Current;
        bool thrashing = mood == FishFight.Mood.Running || mood == FishFight.Mood.Warning;
        look.Hooked(fishAt, Fight.Tension, thrashing);

        // The body turns with the rod: towards the fish, leaned to the side the rod is held.
        Vector3 toFish = Flat(fishAt - transform.position);
        if (toFish.sqrMagnitude > 0.01f)
        {
            Quaternion facing = Quaternion.LookRotation(toFish.normalized) * Quaternion.Euler(0f, rodSide * LeanDegrees, 0f);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, 240f * seconds);
        }

        if (poses != null)
        {
            poses.SetFishingPose(FishingPose.Fighting, mood == FishFight.Mood.Running ? 1.35f : Held ? 1f : 0.7f);
        }

        if (mood != lastMood)
        {
            if (mood == FishFight.Mood.Running)
            {
                AudioManager.Play(settings.SplashSound, fishAt);
                look.Splash(fishAt, 0f);
                CameraShake.Add(0.06f);
                nextSplash = Time.time + 0.9f;
            }

            lastMood = mood;
        }

        if (mood == FishFight.Mood.Running && Time.time >= nextSplash)
        {
            look.Splash(fishAt);
            nextSplash = Time.time + Random.Range(0.6f, 1.1f);
        }

        if (Held && Fight.Distance < before && Time.time >= nextReelSound)
        {
            AudioManager.Play(settings.ReelSound, transform.position);
            nextReelSound = Time.time + 0.42f;
        }

        if (Fight.Tension >= 0.92f && Time.time >= nextStrainSound)
        {
            AudioManager.Play(settings.StrainSound, transform.position);
            nextStrainSound = Time.time + 0.55f;
        }

        switch (Fight.Result)
        {
            case FishFight.Outcome.Landed:
                BeginLanding();
                break;
            case FishFight.Outcome.Snapped:
                AudioManager.Play(settings.SnapSound, transform.position);
                CameraShake.Add(0.1f);
                EndLine($"The line snapped! The {hooked.item.DisplayName} got away.");
                break;
            case FishFight.Outcome.ThrewHook:
                look.Splash(fishAt, 0f);
                EndLine($"The line went slack and the {hooked.item.DisplayName} threw the hook.");
                break;
            case FishFight.Outcome.Spooled:
                EndLine($"The {hooked.item.DisplayName} ran off with all your line, and got away.");
                break;
        }
    }

    // Where the rod is pushed with the movement keys, -1 (left of the line, as the angler looks at the fish) to 1.
    private float RodInput()
    {
        if (RodSideForTests.HasValue)
        {
            return Mathf.Clamp(RodSideForTests.Value, -1f, 1f);
        }

        Vector2 keys = movement.MoveKeys;
        Camera view = Camera.main;
        if (keys.sqrMagnitude < 0.01f || view == null)
        {
            return 0f;
        }

        Vector3 right = Flat(view.transform.right).normalized;
        Vector3 forward = Flat(view.transform.forward).normalized;
        Vector3 pushed = right * keys.x + forward * keys.y;
        return Mathf.Clamp(Vector3.Dot(pushed, RightOfLine()) * 1.4f, -1f, 1f);
    }

    private Vector3 RightOfLine()
    {
        Vector3 line = Flat(fishAt - transform.position);
        line = line.sqrMagnitude > 0.01f ? line.normalized : Flat(transform.forward).normalized;
        return Vector3.Cross(Vector3.up, line);
    }

    // Where a cast this way lands: the furthest open water along it, no further than it was thrown (a long throw over a
    // narrow pond drops in the water, not on the far bank). False when there is none.
    private bool WaterAlong(Vector3 direction, float distance, out Vector3 landing, out float height, out WaterSurface water)
    {
        for (float at = distance; at >= 1.5f; at -= 0.5f)
        {
            Vector3 point = transform.position + direction * at;
            water = WaterSurface.OpenAt(new Vector3(point.x, transform.position.y + 1f, point.z), 6f, out height);
            if (water != null)
            {
                landing = new Vector3(point.x, height, point.z);
                return true;
            }
        }

        landing = default;
        height = 0f;
        water = null;
        return false;
    }

    // The fish on the water: off the line of the cast by the fight's angle, as far as the line out, and kept on the water
    // (a run towards the bank swings back in where the water is narrow). Brought in to where the shore comes up through
    // the water, it is landed there.
    private void PlaceFish(float before)
    {
        Vector3 from = cast.From;
        Vector3 straight = Flat(cast.Landing - from);
        straight = straight.sqrMagnitude > 0.01f ? straight.normalized : Flat(transform.forward).normalized;
        float angle = Fight.Angle;
        for (int i = 3; i >= 0; i--)
        {
            float tried = angle * i / 3f;
            Vector3 at = from + Quaternion.AngleAxis(tried, Vector3.up) * straight * Fight.Distance;
            if (WaterSurface.OpenAt(new Vector3(at.x, cast.WaterHeight, at.z), 1f, out float height) != null)
            {
                fishAt = new Vector3(at.x, height, at.z);
                if (i < 3)
                {
                    Fight.Confine(Mathf.Abs(tried));
                }

                return;
            }
        }

        // No water there. Running out (a long cast lands by the far bank), the fish meets that bank and stays where it was;
        // only one brought in to the angler's own shore is landed.
        if (Fight.Distance > before)
        {
            Fight.HoldAt(before);
            return;
        }

        Fight.Beach();
    }

    private void BeginLanding()
    {
        FishingSettings settings = FishingSettings.Current;
        State = Phase.Landing;
        haul = kilograms >= settings.HaulKilograms;
        landingStarted = Time.time;
        landed = false;
        landingLetGo = false;
        landingLetGoAt = -1f;
        heldUpAt = -1f;
        if (poses != null)
        {
            poses.SetFishingPose(haul ? FishingPose.Haul : FishingPose.Catch);
        }

        AudioManager.Play(settings.SplashSound, fishAt);
        look.Splash(fishAt, 0f);
    }

    private void UpdateLanding()
    {
        FishingSettings settings = FishingSettings.Current;
        float since = Time.time - landingStarted;
        float share = poses != null ? poses.LandingShare : -1f;
        float lift = share >= 0f ? Mathf.InverseLerp(0.15f, settings.LandLiftPoint, share) : Mathf.Clamp01(since / 0.8f);
        if (share < 0f && landingLetGo)
        {
            // Past the clip (going back to the ready pose): the fish is in hand.
            lift = 1f;
        }

        look.Landing(lift, centimetres / 100f, hooked.colour);
        if (!landed && lift >= 0.6f)
        {
            landed = true;
            look.Splash(fishAt, 0f);
            Keep();
        }

        // A light fish's swing out plays to the end and the rod comes down by itself; a heavy one is held up a moment
        // first. Both go back to the ready pose when they are let go.
        if (haul && share >= 1f && heldUpAt < 0f)
        {
            heldUpAt = Time.time;
        }

        bool shown = haul ? heldUpAt >= 0f && Time.time - heldUpAt >= settings.HaulHoldSeconds : share >= 0f;
        if (!landingLetGo && (shown || since > 5f))
        {
            landingLetGo = true;
            landingLetGoAt = Time.time;
            if (poses != null)
            {
                poses.EndFishing();
            }
        }

        bool done = landingLetGo && (haul ? Time.time - landingLetGoAt > 0.55f : share < 0f || share >= 0.93f);
        if (done || since > 8f)
        {
            if (!landed)
            {
                Keep();
            }

            EndLine(null);
        }
    }

    // The fish goes in the bag (or back in the water when the bag is full).
    private void Keep()
    {
        landed = true;
        FishingSettings settings = FishingSettings.Current;
        ItemDefinition fish = hooked.item;
        // Its length goes with it (the bag shows it), and the journal keeps the biggest of each kind. The fish fought comes
        // first: with room for one only, the Double Catch's extra must not take the place of a trophy.
        if (items.Give(fish, 1, centimetres, ArrivalCause.Caught) < 1)
        {
            GameMessages.Post($"No room for the {fish.DisplayName}. You let it go.", MessageKind.Warning);
        }
        else
        {
            bool record = GameSession.Progress.Journal.NoteCatch(fish.Id, centimetres, out float before);
            GameMessages.Post($"Caught a {FishSizes.Describe(centimetres)} {fish.DisplayName}! ({kilograms:0.0} kg)"
                              + (record && before > 0f ? $" Your biggest yet (the last was {FishSizes.Describe(before)})." : ""), MessageKind.Gain);
            GameEvents.Raise(new GameEvent(GameEventKind.FishCaught, fish, 1, gameObject));
            int tier = Mathf.Max(1, fish.Tier);
            ExperienceTracker.Practise(ProficiencyTraining.Fishing, tier, LevelingSettings.Current.TierExperience(tier) * LevelingSettings.Current.CatchWork);

            // Double Catch: now and then a second one comes up on the line, a catch like the first.
            float second = FishSizes.Roll(Random.value, Random.value, hooked.lengthScale);
            if (Techniques.Has(PerkTechnique.DoubleCatch) && Random.value < TechniqueSettings.Current.DoubleCatchChance
                && items.Give(fish, 1, second, ArrivalCause.Caught) > 0)
            {
                GameMessages.Post($"Double Catch: two {fish.DisplayName} on the one line!", MessageKind.Gain);
                Techniques.Used(PerkTechnique.DoubleCatch);
                GameSession.Progress.Journal.NoteCatch(fish.Id, second, out _);
                GameEvents.Raise(new GameEvent(GameEventKind.FishCaught, fish, 1, gameObject));
            }
        }

        AudioManager.Play(settings.CatchSound, transform.position);
    }

    /// <summary>How long a fish of this weight is, in metres (a 1 kg trout about 40 cm).</summary>
    public static float Length(float kilograms) => 0.42f * Mathf.Pow(Mathf.Max(0.05f, kilograms), 1f / 3f);

    /// <summary>Takes the line in: the float and line go, the fight ends and the rod comes down. A message says why, when given.</summary>
    public void EndLine(string message)
    {
        look?.Hide();
        if (FishInTheWater.Current != null)
        {
            FishInTheWater.Current.Release();
        }

        Fight = null;
        hooked = null;
        if (closeUp != null)
        {
            if (PlayerCameraController.Current != null)
            {
                PlayerCameraController.Current.EndCloseUp(closeUp);
            }

            closeUp = null;
        }

        if (movement != null)
        {
            movement.SetSpeedScale(this, 1f);
            movement.FacingLocked = false;
        }

        if (poses != null)
        {
            poses.EndFishing();
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

    // The first bait in the bag that FishingSettings knows, how well it lures and how much it eases a big fish. -1 when there is none.
    private int FindBait(out float lure, out float ease)
    {
        lure = 1f;
        ease = 0f;
        ItemContainer bag = items.Container;
        foreach (FishingSettings.Bait bait in FishingSettings.Current.BaitList)
        {
            for (int i = 0; i < bag.SlotCount; i++)
            {
                ItemStack stack = bag.GetSlot(i);
                if (!stack.IsEmpty && stack.Item == bait.item)
                {
                    lure = bait.lure;
                    ease = bait.ease;
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

    private void OnDestroy() => look?.Destroy();

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
}
