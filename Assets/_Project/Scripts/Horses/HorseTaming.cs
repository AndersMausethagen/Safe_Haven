using UnityEngine;

/// <summary>
/// Taming a wild horse, after the lasso has caught it (HorseLasso):
/// <list type="number">
/// <item>The struggle. It whinnies and pulls this way and that on the rope; the player, feet planted, taps E or left click
/// and leans away from it with the movement keys (which spends their stamina) to hold it. Strain on the rope builds while
/// it pulls and every tap and every moment of leaning takes some off and tires it (HorseTamingRules); at full strain the
/// rope slips and it is gone, worn out it stands, head low, for a few seconds to be mounted (E).</item>
/// <item>The breaking ride. With the player on its back it veers off and bucks every few seconds, and the player steers
/// against it to keep their balance (lost, they are thrown); and it asks for barley every 10-30 seconds (E feeds it from
/// the bag); left unfed past its patience, or got off of, it throws the rider and bolts. Fed as often as it needs (3-5
/// times, its own number, more or fewer by its temperament), it is tamed: a saved copy of the horse takes its place under
/// the rider, and it is given a name.</item>
/// </list>
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
    private float FullStruggle => horse.Definition.StruggleStamina * horse.Temperament.fight;
    /// <summary>How hard the player leans on the rope now, 0-1 (for the HUD).</summary>
    public float Lean { get; private set; }
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
        struggle = FullStruggle;
        horse.SetTamingState(Horse.Mode.Lassoed);
        horse.Motion.Hold(true);
        PlayerRiding.For(player.gameObject).HoldRope(this);
        Active = this;
        HorseHud.Ensure();
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

        switch (phase)
        {
            case Phase.Struggle:
                UpdateStruggle();
                break;
            case Phase.GivenIn:
                horse.Motion.Lunge(transform.position, 0f);
                if (Time.time > mountUntil)
                {
                    phase = Phase.Struggle;
                    struggle = FullStruggle * 0.4f;
                    strain = Mathf.Max(strain, 0.3f);
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

    private void UpdateStruggle()
    {
        float taps = 0f;
        Lean = 0f;
        if (!GamePause.IsPaused && (UIStateManager.Current == null || !UIStateManager.Current.IsAnyWindowOpen))
        {
            if (GameInput.Interact != null && GameInput.Interact.WasPressedThisFrame()) taps++;
            if (GameInput.Attack != null && GameInput.Attack.WasPressedThisFrame()) taps++;
            // Leaning away from it holds as a few taps a second would, while the player has the stamina for it.
            PlayerMovement feet = handler.GetComponent<PlayerMovement>();
            PlayerStamina breath = handler.Stamina;
            if (feet != null && breath != null && breath.Current > 0.5f)
            {
                HorseSettings settings = HorseSettings.Current;
                Lean = HorseTamingRules.Lean(feet.MovementInput, handler.transform.position - transform.position);
                if (Lean > 0f)
                {
                    taps += Lean * settings.LeanTapsPerSecond * Time.deltaTime;
                    breath.Drain(Lean * settings.LeanStaminaPerSecond * Time.deltaTime);
                }
            }
        }

        float quality = horse.Stats.Quality(horse.Definition);
        HorseTamingRules.Struggle(ref strain, ref struggle, taps, Time.deltaTime, quality, horse.Definition, horse.Temperament.pull,
            1f + PlayerHorsemanship.TamingHold());
        if (HorseTamingRules.Slipped(strain))
        {
            Escape("It tears the rope from your hands and is gone.");
            return;
        }

        if (HorseTamingRules.GivenIn(struggle))
        {
            phase = Phase.GivenIn;
            mountUntil = Time.time + horse.Definition.MountSeconds;
            GameMessages.Post($"It is spent and stands, head low. Get on its back: {GameInput.KeyLabel(GameInput.Interact)}.", MessageKind.Gain);
            return;
        }

        if (Time.time >= lungeUntil)
        {
            NextLunge();
        }

        // It can't go further than the rope: the lunge ends on the circle round the player.
        Vector3 holder = handler.transform.position;
        Vector3 offset = lungeTo - holder;
        offset.y = 0f;
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
        lungeUntil = Time.time + Mathf.Lerp(seconds.x, seconds.y, (float)random.NextDouble());
        if (random.NextDouble() < 0.35)
        {
            horse.Motion.PlayRear();
            Whinny();
        }
    }

    private void UpdateBreaking()
    {
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
                Mount();
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
        if (Time.time >= nextVeer)
        {
            Vector2 seconds = settings.BuckSeconds;
            nextVeer = Time.time + Mathf.Lerp(seconds.x, seconds.y, (float)random.NextDouble()) / Mathf.Max(0.3f, bucking);
            veerUntil = Time.time + Mathf.Lerp(1f, 1.7f, (float)random.NextDouble());
            Vector3 side = transform.right * (random.NextDouble() < 0.5 ? -1f : 1f);
            veer = (side * 0.85f + transform.forward * 0.25f) * Mathf.Clamp(bucking, 0.4f, 1.2f);
            if (random.NextDouble() < 0.4)
            {
                horse.Motion.PlayBuck();
                bool sure = Techniques.Has(PerkTechnique.SureSeat);
                if (sure)
                {
                    Techniques.Used(PerkTechnique.SureSeat);
                }

                balance = Mathf.Max(0f, balance - settings.BuckBalance * 0.5f * bucking * (sure ? 0.5f : 1f));
                Whinny();
            }
        }

        PlayerMovement feet = handler.GetComponent<PlayerMovement>();
        bool free = UIStateManager.Current == null || !UIStateManager.Current.IsAnyWindowOpen;
        Vector3 input = feet != null && free ? feet.MovementInput : Vector3.zero;
        float loss = settings.BuckBalance * (Techniques.Has(PerkTechnique.SureSeat) ? 0.5f : 1f);
        balance = HorseTamingRules.Balance(balance, Veer, input, Time.deltaTime, loss, settings.SteadyPerSecond, bucking);
        if (balance <= 0f)
        {
            Escape("It bucks you clean off and bolts.");
        }
    }

    private void Mount()
    {
        ShowRope(false);
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
        if (rider != null)
        {
            rider.SwapTo(tamed);
        }

        Destroy(gameObject);
        tamed.Motion.PlayRear();
        PlayerHorsemanship.Practise(HorseWork.Tame);
        GameMessages.Post($"It stands quiet under you. It is yours, a {HorseTemperamentRules.NameOf(tamed.Stats.temperament)} {tamed.Stats.SexName}: give it a name.", MessageKind.Gain);
        HorseNameWindow.Ask(tamed);
    }

    /// <summary>It gets away: off the rope or out from under the rider, and runs. Null says nothing.</summary>
    private void Escape(string message)
    {
        Phase was = phase;
        Player from = handler;
        Stop();
        if (horse.Rider != null)
        {
            horse.Rider.Dismount(thrown: true);
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
        phase = Phase.None;
        asking = false;
        handler = null;
        if (Active == this)
        {
            Active = null;
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
