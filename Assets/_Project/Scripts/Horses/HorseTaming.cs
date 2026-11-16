using UnityEngine;

/// <summary>
/// Taming a wild horse, after the lasso has caught it (HorseLasso):
/// <list type="number">
/// <item>The struggle. It whinnies and pulls this way and that on the rope; the player, feet planted, taps E or left click
/// and leans away from it with the movement keys (which spends their stamina) to hold it. Strain on the rope builds while
/// it pulls and every tap and every moment of leaning takes some off and tires it (HorseTamingRules); at full strain the
/// rope slips and it is gone. A player who neither taps nor leans is dragged along behind it, and the camera shakes with
/// the rope. Worn out, it stands, head low, for a few seconds: the player walks up to it and gets on (E, from beside it).</item>
/// <item>The breaking ride. With the player on its back it veers off and bucks every few seconds, and the player steers
/// against it to keep their balance (lost, they are thrown); and it asks for barley every 10-30 seconds (E feeds it from
/// the bag); left unfed past its patience, or got off of, it throws the rider and bolts. Fed as often as it needs (3-5
/// times, its own number, more or fewer by its temperament), it is tamed: a saved copy of the horse takes its place under
/// the rider, and it is given a name.</item>
/// </list>
/// The whole of it is a fight for the music (AudioManager.KeepCombat).
/// Its temperament (HorseSettings › Temperament) sets how hard it pulls, how much fight it has and how hard it bucks;
/// Horsemanship's Hold on the Rope makes each tap and lean count for more. The HUD (HorseHud) shows the rope's strain and
/// the horse's strength, the rider's balance and the feeding.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Horse))]
public sealed class HorseTaming : MonoBehaviour
{
    public enum Phase { None, Struggle, GivenIn, Breaking }

    private const float RopeLength = 6.5f;
    // Walked this far from a spent horse, the rope slips.
    private const float SlackReach = 11f;
    private const float ResistSeconds = 0.5f;
    // The tell before each pull, and how soon after the pull starts a lean against it counts as catching it.
    private const float TellSeconds = 0.55f;
    private const float CatchWindow = 0.7f;
    private const float BalkSeconds = 3.5f;
    private const float CoaxSeconds = 1.2f;
    private const float LassoSnapChance = 0.25f;
    private const float RearWindow = 0.9f;

    private Horse horse;
    private Player handler;
    private Phase phase;
    private float strain;
    private float struggle;
    private Vector3 lungeTo;
    private float lungeUntil;
    private float mountUntil;
    private int fed;
    private float nextAsk;
    private float askUntil;
    private bool asking;
    private System.Random random;
    private LineRenderer rope;
    private float balance = 1f;
    private Vector3 veer;
    private float veerUntil;
    private float nextVeer;
    private float lastResisted;
    private float tellUntil;
    private bool caught;
    private float spinRate;
    private float balkUntil;
    private float coaxed;
    private float rearUntil;

    /// <summary>The horse the local player is taming, for the HUD; null when none.</summary>
    public static HorseTaming Active { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Active = null;

    public Phase Current => phase;
    public Horse Horse => horse;
    public bool IsHeld => phase == Phase.Struggle || phase == Phase.GivenIn;
    public bool IsBreaking => phase == Phase.Breaking;
    public float Strain => strain;
    /// <summary>What strength it has left to fight with, 0-1.</summary>
    public float Strength => FullStruggle > 0f ? struggle / FullStruggle : 0f;
    private float FullStruggle => horse.Definition.StruggleStamina * horse.Temperament.fight * horse.FightScale;
    /// <summary>How hard the player leans on the rope now, 0-1 (for the HUD).</summary>
    public float Lean { get; private set; }
    /// <summary>True while it drags a player who isn't holding against it.</summary>
    public bool IsDragging { get; private set; }
    /// <summary>True while it throws its head before a pull: the way it is about to go (lean the other way).</summary>
    public bool IsTelling => phase == Phase.Struggle && Time.time < tellUntil;
    /// <summary>The way it pulls (or is about to), on the ground from the player.</summary>
    public Vector3 PullDirection { get; private set; }
    /// <summary>When the player last caught a pull with a timed lean (the HUD shows it a moment).</summary>
    public float CaughtAt { get; private set; } = -99f;
    /// <summary>A stubborn horse on its breaking ride has planted its feet: the rider pushes it on (forward).</summary>
    public bool IsBalking => phase == Phase.Breaking && Time.time < balkUntil;
    /// <summary>A bold horse rears under the rider: they lean forward (the movement keys ahead) or slide off.</summary>
    public bool IsRearing => phase == Phase.Breaking && Time.time < rearUntil;
    /// <summary>The rider's balance on the breaking ride, 0-1: at 0 they are thrown.</summary>
    public float Balance => balance;
    /// <summary>Where it is veering off to now (its length is how hard), zero between veers; HorseMotion adds it to the rider's steering.</summary>
    public Vector3 Veer => phase == Phase.Breaking && Time.time < veerUntil ? veer : Vector3.zero;
    public float MountLeft => Mathf.Max(0f, mountUntil - Time.time);
    public int Fed => fed;
    public int Needed => HorseTamingRules.FeedsNeeded(horse.Stats, horse.Temperament.feeds);
    public bool IsAsking => asking;
    /// <summary>How much of its patience is left while it asks for barley, 0-1.</summary>
    public float PatienceLeft => asking ? Mathf.Clamp01((askUntil - Time.time) / horse.Definition.FeedWindow) : 0f;

    private void Awake()
    {
        horse = GetComponent<Horse>();
        random = new System.Random(GetEntityId().GetHashCode() ^ System.Environment.TickCount);
    }

    private void OnDisable()
    {
        if (phase != Phase.None)
        {
            Abandon();
        }
    }

    /// <summary>The lasso has caught it: the struggle begins.</summary>
    public void BeginStruggle(Player player)
    {
        handler = player;
        phase = Phase.Struggle;
        strain = 0.25f;
        lastResisted = Time.time;
        struggle = FullStruggle;
        horse.SetTamingState(Horse.Mode.Lassoed);
        horse.Motion.Hold(true);
        PlayerRiding.For(player.gameObject).HoldRope(this);
        Active = this;
        HorseHud.Ensure();
        Frame(0.8f, true);
        ShowRope(true);
        NextLunge();
        Whinny();
        GameMessages.Post($"It fights the rope! Tap {GameInput.KeyLabel(GameInput.Interact)} or left click, and lean away from it with the movement keys.", MessageKind.Info);
    }

    private void Update()
    {
        if (phase == Phase.None)
        {
            return;
        }

        if (handler == null || !horse.IsAlive || (handler.TryGetComponent(out Damageable life) && !life.IsAlive))
        {
            Escape(null);
            return;
        }

        // A window over the taming (the bag, the map) holds it (the user, 2026-11-16): its HUD is hidden and its keys can't be
        // pressed, and strain, patience and balance ran on, the horse slipping away unseen.
        if (UIStateManager.Current != null && UIStateManager.Current.IsAnyWindowOpen)
        {
            Hold(Time.deltaTime);
            return;
        }

        AudioManager.KeepCombat();
        switch (phase)
        {
            case Phase.Struggle:
                UpdateStruggle();
                break;
            case Phase.GivenIn:
                horse.Motion.Lunge(transform.position, 0f);
                IsDragging = false;
                Vector3 away = handler.transform.position - transform.position;
                away.y = 0f;
                if (away.magnitude > SlackReach)
                {
                    Escape("You let the rope run out too far. It slips free and is gone.");
                    return;
                }

                if (Time.time > mountUntil)
                {
                    phase = Phase.Struggle;
                    struggle = FullStruggle * 0.4f;
                    strain = Mathf.Max(strain, 0.3f);
                    PlayerRiding.For(handler.gameObject).TautenRope();
                    NextLunge();
                    Whinny();
                    GameMessages.Post("It has its breath back, and fights again!", MessageKind.Warning);
                }

                break;
            case Phase.Breaking:
                UpdateBreaking();
                break;
        }

        UpdateRope();
    }

    // Every clock of the taming moves on by what was held, so nothing comes due while it waits.
    private void Hold(float seconds)
    {
        Later(ref lungeUntil, seconds);
        Later(ref mountUntil, seconds);
        Later(ref nextAsk, seconds);
        Later(ref askUntil, seconds);
        Later(ref veerUntil, seconds);
        Later(ref nextVeer, seconds);
        Later(ref lastResisted, seconds);
        Later(ref tellUntil, seconds);
        Later(ref balkUntil, seconds);
        Later(ref rearUntil, seconds);
    }

    // 0 is "not set" for some (a rear not under way): left so.
    private static void Later(ref float time, float seconds)
    {
        if (time > 0f)
        {
            time += seconds;
        }
    }

    private void UpdateStruggle()
    {
        float taps = 0f;
        Lean = 0f;
        if (!GamePause.IsPaused && (UIStateManager.Current == null || !UIStateManager.Current.IsAnyWindowOpen))
        {
            if (GameInput.Interact != null && GameInput.Interact.WasPressedThisFrame()) taps++;
            if (GameInput.Attack != null && GameInput.Attack.WasPressedThisFrame()) taps++;
            if (taps > 0f)
            {
                lastResisted = Time.time;
            }

            // Leaning against the way it pulls holds as a few taps a second would, while the player has the stamina for it;
            // leaning with it gives it rope. Caught just as the pull starts, after its tell, a lean holds it hard.
            PlayerMovement feet = handler.GetComponent<PlayerMovement>();
            PlayerStamina breath = handler.Stamina;
            if (feet != null && breath != null && breath.Current > 0.5f)
            {
                HorseSettings settings = HorseSettings.Current;
                Lean = HorseTamingRules.Lean(feet.MovementInput, -PullDirection);
                if (Lean > 0f)
                {
                    if (Lean > 0.15f)
                    {
                        lastResisted = Time.time;
                    }

                    taps += Lean * settings.LeanTapsPerSecond * Time.deltaTime;
                    breath.Drain(Lean * settings.LeanStaminaPerSecond * Time.deltaTime);
                }

                if (!IsTelling && !caught && HorseTamingRules.CaughtPull(Time.time - tellUntil, Lean, CatchWindow))
                {
                    caught = true;
                    CaughtAt = Time.time;
                    strain = Mathf.Max(0f, strain - 0.2f);
                    struggle = Mathf.Max(0f, struggle - horse.Definition.TapDrain * 4f * (1f + PlayerHorsemanship.TamingHold()));
                    CameraShake.Add(0.08f);
                }

                if (HorseTamingRules.LeansWith(feet.MovementInput, PullDirection))
                {
                    strain = Mathf.Min(1f, strain + 0.3f * Time.deltaTime);
                }
            }
        }

        float quality = horse.Stats.Quality(horse.Definition);
        // During its tell it only gathers itself: the pull comes after.
        float pulling = horse.Temperament.pull * (IsTelling ? 0.3f : 1f);
        HorseTamingRules.Struggle(ref strain, ref struggle, taps, Time.deltaTime, quality, horse.Definition, pulling,
            1f + PlayerHorsemanship.TamingHold());
        if (HorseTamingRules.Slipped(strain))
        {
            Escape("It tears the rope from your hands and is gone.");
            return;
        }

        if (HorseTamingRules.GivenIn(struggle))
        {
            phase = Phase.GivenIn;
            IsDragging = false;
            mountUntil = Time.time + horse.Definition.MountSeconds;
            PlayerRiding.For(handler.gameObject).SlackRope();
            GameMessages.Post($"It is spent and stands, head low. Walk up beside it and get on: {GameInput.KeyLabel(GameInput.Interact)}.", MessageKind.Gain);
            return;
        }

        // The rope shakes the camera as it strains; and a player who holds nothing against it is dragged along.
        HorseSettings rules = HorseSettings.Current;
        CameraShake.AtLeast(rules.RopeShake * strain);
        Vector3 pull = transform.position - handler.transform.position;
        pull.y = 0f;
        IsDragging = Time.time - lastResisted > ResistSeconds && pull.magnitude > RopeLength * 0.8f;
        if (IsDragging && handler.TryGetComponent(out PlayerMovement dragged))
        {
            dragged.Lunge(pull.normalized * rules.DragSpeed * Mathf.Lerp(0.45f, 1f, strain), 0.12f);
            CameraShake.AtLeast(rules.RopeShake * 1.4f);
        }

        if (Time.time >= lungeUntil)
        {
            NextLunge();
        }

        // It can't go further than the rope: the lunge ends on the circle round the player. Its tell: it stands and turns
        // its head and body the way it is about to go.
        Vector3 holder = handler.transform.position;
        Vector3 offset = lungeTo - holder;
        offset.y = 0f;
        PullDirection = offset.sqrMagnitude > 0.01f ? offset.normalized : PullDirection;
        if (IsTelling)
        {
            horse.Motion.Lunge(transform.position, 0f);
            Vector3 face = lungeTo - transform.position;
            face.y = 0f;
            if (face.sqrMagnitude > 0.01f)
            {
                horse.Motion.TurnToward(Quaternion.LookRotation(face.normalized, Vector3.up));
            }

            return;
        }

        Vector3 point = holder + Vector3.ClampMagnitude(offset, RopeLength);
        horse.Motion.Lunge(point, horse.Definition.TrotSpeed * (0.6f + 0.6f * strain));
    }

    // A new direction to fight in: mostly away from the player, sometimes across.
    private void NextLunge()
    {
        Vector3 away = transform.position - handler.transform.position;
        away.y = 0f;
        away = away.sqrMagnitude > 0.01f ? away.normalized : transform.forward;
        float turn = (float)(random.NextDouble() * 160.0 - 80.0);
        lungeTo = transform.position + Quaternion.Euler(0f, turn, 0f) * away * 8f;
        Vector2 seconds = horse.Definition.LungeSeconds;
        tellUntil = Time.time + TellSeconds;
        caught = false;
        lungeUntil = tellUntil + Mathf.Lerp(seconds.x, seconds.y, (float)random.NextDouble());
        horse.Motion.PlayHeadToss(lungeTo - transform.position);
        CameraShake.Add(HorseSettings.Current.RopeJolt * 0.5f);
        if (random.NextDouble() < 0.35)
        {
            horse.Motion.PlayRear();
            CameraShake.Add(HorseSettings.Current.RopeJolt * 0.5f);
            Whinny();
        }
    }

    private void UpdateBreaking()
    {
        // Nothing asks or bucks while the rider is still climbing on.
        PlayerRiding rider = horse.Rider;
        if (rider == null || rider.IsMounting)
        {
            nextAsk += Time.deltaTime;
            nextVeer += Time.deltaTime;
            return;
        }

        UpdateBalance();
        if (phase != Phase.Breaking)
        {
            return;
        }

        if (!asking && Time.time >= nextAsk)
        {
            asking = true;
            askUntil = Time.time + horse.Definition.FeedWindow;
            Whinny();
            ItemDefinition barley = horse.Definition.Barley;
            GameMessages.Post($"It tosses its head and wants {(barley != null ? barley.DisplayName.ToLowerInvariant() : "food")}: {GameInput.KeyLabel(GameInput.Interact)}.", MessageKind.Info);
        }

        if (asking && Time.time > askUntil)
        {
            Escape("It wanted barley. It throws you off and bolts.");
        }
    }

    /// <summary>E from the player handling it. True when the press was the taming's (mounting, feeding, a tap); false
    /// lets the rider get down (which ends a breaking ride).</summary>
    public bool OnHandlerInteract()
    {
        switch (phase)
        {
            case Phase.Struggle:
                return true;
            case Phase.GivenIn:
                if (PlayerRiding.For(handler.gameObject).CanMount(horse, out string why))
                {
                    Mount();
                }
                else if (!string.IsNullOrEmpty(why))
                {
                    GameMessages.Post(why, MessageKind.Info);
                }

                return true;
            case Phase.Breaking:
                if (asking)
                {
                    Feed();
                    return true;
                }

                return false;
            default:
                return false;
        }
    }

    // Every few seconds it veers off to one side, or bucks and veers; the rider steers against it to keep their seat.
    private void UpdateBalance()
    {
        HorseSettings settings = HorseSettings.Current;
        float bucking = horse.Temperament.bucking;
        bool sure = Techniques.Has(PerkTechnique.SureSeat);
        PlayerMovement feet = handler.GetComponent<PlayerMovement>();
        bool free = UIStateManager.Current == null || !UIStateManager.Current.IsAnyWindowOpen;
        Vector3 input = feet != null && free ? feet.MovementInput : Vector3.zero;
        float ahead = input.sqrMagnitude > 0.01f ? Vector3.Dot(input.normalized, transform.forward) : 0f;
        if (Time.time >= nextVeer && !IsBalking && !IsRearing)
        {
            Vector2 seconds = settings.BuckSeconds;
            nextVeer = Time.time + Mathf.Lerp(seconds.x, seconds.y, (float)random.NextDouble()) / Mathf.Max(0.3f, bucking);
            NextTrick(settings, bucking, sure);
        }

        // A skittish horse spins: the veer goes round, and the rider must keep steering against it.
        if (spinRate != 0f && Time.time < veerUntil)
        {
            veer = Quaternion.Euler(0f, spinRate * Time.deltaTime, 0f) * veer;
        }

        // A stubborn one balks: pushed on (forward) long enough it walks on; left to stand, it bucks.
        if (IsBalking)
        {
            coaxed = ahead > 0.6f ? coaxed + Time.deltaTime : Mathf.Max(0f, coaxed - Time.deltaTime * 0.5f);
            if (coaxed >= CoaxSeconds)
            {
                balkUntil = 0f;
                GameMessages.Post("It gives in and walks on.", MessageKind.Gain);
            }
            else if (Time.time >= balkUntil - 0.05f)
            {
                balkUntil = 0f;
                Buck(settings, bucking, sure, 0.5f);
            }
        }

        // A bold one rears: leaning forward keeps the seat; sitting back, the rider slides off its rump.
        if (IsRearing && ahead > 0.6f)
        {
            rearUntil = 0f;
            GameMessages.Post("You lean into its neck and hold on.", MessageKind.Gain);
        }
        else if (rearUntil > 0f && Time.time >= rearUntil)
        {
            rearUntil = 0f;
            balance = Mathf.Max(0f, balance - settings.BuckBalance * 1.2f * (sure ? 0.5f : 1f));
        }

        float loss = settings.BuckBalance * (sure ? 0.5f : 1f);
        balance = HorseTamingRules.Balance(balance, Veer, input, Time.deltaTime, loss, settings.SteadyPerSecond, bucking);
        if (balance <= 0f)
        {
            Escape("It bucks you clean off and bolts.");
        }
    }

    // What it does next to throw the rider, by its nature: every horse veers and bucks; a skittish one spins round, a
    // stubborn one plants its feet, a bold one rears.
    private void NextTrick(HorseSettings settings, float bucking, bool sure)
    {
        veerUntil = Time.time + Mathf.Lerp(1f, 1.7f, (float)random.NextDouble());
        Vector3 side = transform.right * (random.NextDouble() < 0.5 ? -1f : 1f);
        float calm = horse.Stats.temperament == HorseTemperament.Calm ? 0.7f : 1f;
        veer = (side * 0.85f + transform.forward * 0.25f) * Mathf.Clamp(bucking, 0.4f, 1.2f) * calm;
        spinRate = 0f;
        double roll = random.NextDouble();
        switch (horse.Stats.temperament)
        {
            case HorseTemperament.Skittish when roll < 0.45:
                spinRate = (random.NextDouble() < 0.5 ? -1f : 1f) * 200f;
                veerUntil = Time.time + 1.8f;
                Whinny();
                return;
            case HorseTemperament.Stubborn when roll < 0.4:
                veerUntil = 0f;
                balkUntil = Time.time + BalkSeconds;
                coaxed = 0f;
                GameMessages.Post("It plants its feet and won't go on. Push it forward.", MessageKind.Warning);
                return;
            case HorseTemperament.Bold when roll < 0.4:
                veerUntil = 0f;
                rearUntil = Time.time + RearWindow;
                horse.Motion.PlayRear();
                GameMessages.Post("It rears! Lean forward.", MessageKind.Warning);
                return;
        }

        if (roll > 0.6)
        {
            Buck(settings, bucking, sure, 0.5f);
        }
    }

    private void Buck(HorseSettings settings, float bucking, bool sure, float share)
    {
        horse.Motion.PlayBuck();
        if (sure)
        {
            Techniques.Used(PerkTechnique.SureSeat);
        }

        balance = Mathf.Max(0f, balance - settings.BuckBalance * share * bucking * (sure ? 0.5f : 1f));
        Whinny();
    }

    private void Mount()
    {
        ShowRope(false);
        Frame(0.85f, false);
        balance = 1f;
        veer = Vector3.zero;
        nextVeer = Time.time + 2f;
        phase = Phase.Breaking;
        horse.SetTamingState(Horse.Mode.Breaking);
        fed = 0;
        asking = false;
        nextAsk = Time.time + HorseTamingRules.NextAsk(horse.Definition, random);
        PlayerRiding riding = PlayerRiding.For(handler.gameObject);
        riding.ReleaseRope();
        riding.Mount(horse);
        // The saddle can still refuse (someone already on it, a friend holding it): left Breaking with no rider, it pushed its
        // timers on for ever.
        if (riding.Riding != horse)
        {
            Escape("It shies away before you can get on.");
            return;
        }

        ItemInventory items = handler.GetComponent<ItemInventory>();
        ItemDefinition barley = horse.Definition.Barley;
        int carried = items != null && barley != null ? items.CountCarried(barley) : 0;
        GameMessages.Post($"Stay on! Steer against it when it veers. It will want {(barley != null ? barley.DisplayName.ToLowerInvariant() : "food")} now and then" +
                          (carried < Needed ? $", and you carry {carried}." : "."), carried < Needed ? MessageKind.Warning : MessageKind.Info);
    }

    private void Feed()
    {
        ItemInventory items = handler.GetComponent<ItemInventory>();
        ItemDefinition barley = horse.Definition.Barley;
        if (items == null || barley == null || items.CountCarried(barley) <= 0)
        {
            GameMessages.Post($"You have no {(barley != null ? barley.DisplayName.ToLowerInvariant() : "food")} for it!", MessageKind.Warning);
            return;
        }

        if (items.Container.Remove(barley, 1) < 1 && (items.SafePocket == null || items.SafePocket.Remove(barley, 1) < 1))
        {
            return;
        }

        fed++;
        asking = false;
        if (HorseTamingRules.IsBroken(fed, horse.Stats, horse.Temperament.feeds))
        {
            Tame();
            return;
        }

        nextAsk = Time.time + HorseTamingRules.NextAsk(horse.Definition, random);
        GameMessages.Post($"It takes the barley from your hand and calms a little ({fed} of {Needed}).", MessageKind.Gain);
    }

    /// <summary>The rider got off during the breaking ride: it bolts.</summary>
    public void OnRiderLeft()
    {
        if (phase == Phase.Breaking)
        {
            Escape("The moment you are off, it bolts.");
        }
    }

    /// <summary>Hurt while held or ridden for the first time: it pulls all the harder. True when taming took the blow.</summary>
    public bool OnHurt()
    {
        if (phase == Phase.Struggle)
        {
            strain = Mathf.Min(1f, strain + 0.2f);
            return true;
        }

        return phase != Phase.None;
    }

    // The breaking ride is over: the horse is the player's. A wild horse has no save id, so a saved copy of it takes its
    // place under the rider, with its numbers, its health and its stamina.
    private void Tame()
    {
        Horse wild = horse;
        PlayerRiding rider = wild.Rider;
        Frame(1f, false);
        phase = Phase.None;
        asking = false;
        Active = null;
        SaveableEntity prefab = wild.Definition.Prefab != null ? wild.Definition.Prefab.GetComponent<SaveableEntity>() : null;
        if (prefab == null)
        {
            Debug.LogError("The horse's prefab has no SaveableEntity: a tamed horse can't be kept.", wild.Definition);
            return;
        }

        SaveableEntity copy = SaveableEntity.Spawn(prefab, transform.position, transform.rotation);
        Horse tamed = copy.GetComponent<Horse>();
        tamed.BecomeTamed(wild.Stats, wild.Health.HealthFraction);
        tamed.Stamina = wild.Stamina;
        // In co-op the wild horse's copies go from friends' games, and the tamed one is in this player's hand.
        CoopHorses.Replaced(wild, tamed);
        if (rider != null)
        {
            rider.SwapTo(tamed);
        }

        Destroy(gameObject);
        tamed.Motion.PlayRear();
        PlayerHorsemanship.Practise(HorseWork.Tame);
        GameMessages.Post($"It stands quiet under you. It is yours, a {HorseTemperamentRules.NameOf(tamed.Stats.temperament)} {tamed.Stats.SexName}: give it a name.", MessageKind.Gain);
        HorseNameWindow.Offer(tamed);
    }

    /// <summary>It gets away: off the rope or out from under the rider, and runs. Null says nothing.</summary>
    private void Escape(string message)
    {
        Phase was = phase;
        Player from = handler;
        bool thrown = horse.Rider != null;
        Stop();
        if (horse.Rider != null)
        {
            horse.Rider.Dismount(thrown: true);
        }

        // It got away: warier of this player; a slipped rope may snap the lasso; a rider thrown off lands hard.
        if (message != null && from != null && was != Phase.None)
        {
            horse.GotAway();
            if (was == Phase.Struggle && horse.Definition.Lasso != null && random.NextDouble() < LassoSnapChance
                && from.TryGetComponent(out ItemInventory items) && items.Container.Remove(horse.Definition.Lasso, 1) > 0)
            {
                message += " The lasso snaps.";
            }

            if (thrown && from.TryGetComponent(out Damageable body) && body.IsAlive)
            {
                body.ApplyDamage(new DamageInfo(null, null, body.MaxHealth * 0.06f, DamageType.Physical, false, from.transform.position + Vector3.up,
                    Vector3.down, 0f, HitTechnique.None));
                if (from.TryGetComponent(out PlayerCombat combat))
                {
                    combat.Rebuff(1f);
                }
            }
        }

        horse.SetTamingState(Horse.Mode.Wild);
        horse.Motion.Hold(false);
        if (from != null)
        {
            horse.Motion.Flee(from.transform.position, 14f);
        }

        Whinny();
        if (message != null && was != Phase.None)
        {
            GameMessages.Post(message, MessageKind.Warning);
        }
    }

    /// <summary>Lets go without a word (it died, or the scene goes).</summary>
    public void Abandon()
    {
        Stop();
        if (horse.Rider != null && horse.IsAlive)
        {
            horse.Rider.Dismount(thrown: false);
        }
    }

    private void Stop()
    {
        PlayerRiding holding = handler != null ? PlayerRiding.For(handler.gameObject) : null;
        if (holding != null)
        {
            holding.ReleaseRope();
        }

        ShowRope(false);
        Frame(1f, false);
        phase = Phase.None;
        asking = false;
        handler = null;
        if (Active == this)
        {
            Active = null;
        }
    }

    // The camera comes in a little while taming (and keeps the horse in the picture while it fights the rope).
    private void Frame(float closeness, bool keepInView)
    {
        PlayerCameraController view = PlayerCameraController.Current;
        if (view == null)
        {
            return;
        }

        view.SetCloseness(this, closeness);
        if (keepInView)
        {
            view.KeepInView(transform);
        }
        else
        {
            view.EndCloseUp(transform);
        }
    }

    private void Whinny()
    {
        if (horse.Definition.Whinny != null)
        {
            AudioManager.Play(horse.Definition.Whinny, transform.position);
        }
    }

    // ----- The rope, from the player's hand to the horse's neck -----

    private void ShowRope(bool shown)
    {
        if (shown && rope == null)
        {
            var holder = new GameObject("Lasso Rope");
            holder.transform.SetParent(transform, false);
            rope = holder.AddComponent<LineRenderer>();
            rope.positionCount = 12;
            rope.widthMultiplier = 0.06f;
            rope.useWorldSpace = true;
            rope.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetColor("_BaseColor", new Color(0.72f, 0.6f, 0.4f));
            rope.sharedMaterial = material;
        }

        if (rope != null)
        {
            rope.enabled = shown;
        }
    }

    private void UpdateRope()
    {
        if (rope == null || !rope.enabled || handler == null)
        {
            return;
        }

        Vector3 hand = handler.transform.position + Vector3.up * 1.2f + handler.transform.forward * 0.35f;
        Vector3 neck = horse.Seat.position + transform.forward * 0.9f + Vector3.up * 0.25f;
        float sag = Mathf.Lerp(0.8f, 0.02f, strain);
        for (int i = 0; i < rope.positionCount; i++)
        {
            float t = i / (rope.positionCount - 1f);
            rope.SetPosition(i, Vector3.Lerp(hand, neck, t) + Vector3.down * (sag * 4f * t * (1f - t)));
        }
    }
}
