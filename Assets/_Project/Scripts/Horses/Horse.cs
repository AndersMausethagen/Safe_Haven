using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// A horse: wild, being tamed or the player's own. It keeps its rolled <see cref="HorseStats"/> (its numbers, coat and mane,
/// sex and temperament), its name, its stamina, its bond with the player (HorseBondRules) and what it wears: a saddle, whose
/// bags are an ItemContainer as big as the saddle and as heavy as the horse can carry, and barding (horse armour, shown on it,
/// that keeps part of a blow off). A tamed horse is saved (a SaveableEntity spawned when it was tamed or born) with all of
/// that; a wild one is not and is simply met again another day. Monsters fight it as they fight the player's side; hurt, it
/// runs (a bold one turns on what hurt it), and killed it drops its gear and load where it fell. A foal grows to its full
/// size over a few days and can't be ridden or saddled until it has.
/// <para>
/// Movement and the Animator are HorseMotion's, the lasso struggle and the breaking ride HorseTaming's, the rider's side
/// PlayerRiding's, the stable's keeping and breeding HorseStable's. Interacting with a tamed horse opens its menu.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent), typeof(Damageable), typeof(Combatant))]
public sealed class Horse : MonoBehaviour, ISaveable, ICargoHold
{
    public enum Mode { Wild, Lassoed, Breaking, Tamed }

    private const float MenuRange = 3.5f;
    private const float FeedCooldownSeconds = 30f;

    [SerializeField] private HorseDefinition definition;
    [Tooltip("Where the rider sits (on the horse's back; moves with the body).")]
    [SerializeField] private Transform seat;
    [SerializeField] private Vector3 seatOffset;
    [Tooltip("The saddle on its back, shown while it wears one, and the reins and bridle that go with it.")]
    [SerializeField] private GameObject saddleModel;
    [SerializeField] private GameObject[] tack = Array.Empty<GameObject>();
    [Tooltip("The barding, shown while it wears horse armour, and its looks (by the item's Barding Look).")]
    [SerializeField] private Renderer armourModel;
    [SerializeField] private Material[] armourLooks = Array.Empty<Material>();
    [Tooltip("The mane and tail, which wear the horse's mane.")]
    [SerializeField] private Renderer[] hairParts = Array.Empty<Renderer>();
    [Tooltip("The body that is scaled down for a foal (its Model child).")]
    [SerializeField] private Transform body;

    private static readonly List<Horse> all = new List<Horse>();
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private Damageable health;
    private Combatant combatant;
    private Interactable interactable;
    private ItemDefinition saddle;
    private ItemDefinition armour;
    private string horseName = "";
    private bool restored;
    private Vector3 bodyScale = Vector3.one;
    private float lastFed = -999f;

    public static IReadOnlyList<Horse> All => all;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => all.Clear();

    public HorseDefinition Definition => definition;
    public HorseStats Stats { get; private set; }
    public Mode State { get; private set; } = Mode.Wild;
    public bool IsTamed => State == Mode.Tamed;
    public bool IsWild => State == Mode.Wild;
    public string HorseName => !string.IsNullOrWhiteSpace(horseName) ? horseName : IsTamed ? "Your horse" : definition != null ? definition.DisplayName : "Horse";
    public bool HasName => !string.IsNullOrWhiteSpace(horseName);
    public float Stamina { get; set; }
    /// <summary>Its stamina with what its bond adds.</summary>
    public float MaxStamina => Stats.maxStamina * (IsTamed ? HorseBondRules.StaminaScale(Bond, HorseSettings.Current) : 1f);
    public ItemDefinition Saddle => saddle;
    public ItemDefinition Armour => armour;
    public Damageable Health => health;
    public Combatant Combatant => combatant;
    public HorseMotion Motion { get; private set; }
    public HorseTaming Taming { get; private set; }
    public Transform Seat => seat != null ? seat : transform;
    public Vector3 SeatOffset => seatOffset;
    /// <summary>Who rides it now, or null.</summary>
    public PlayerRiding Rider { get; set; }
    public bool IsAlive => health != null && health.IsAlive;
    public bool IsGrown => Stats.IsGrown;
    public HorseTemperamentRules Temperament => HorseSettings.Current.Temperament(Stats.temperament);

    /// <summary>A wild horse's trust, 0-3: barley it found on the ground or ate from the player's hand. Each step lets the
    /// player nearer and takes fight out of it on the rope. Not saved: a wild horse isn't.</summary>
    public int Trust { get; private set; }
    /// <summary>How often this wild horse has got away from the player: each time it is shyer and fights longer.</summary>
    public int Wariness { get; private set; }
    public const int MostTrust = 3;
    /// <summary>Its shy distance, times this (trust nearer, wariness farther).</summary>
    public float ShyScale => (1f - 0.18f * Trust) * (1f + 0.4f * Wariness);
    /// <summary>Its fight on the rope, times this.</summary>
    public float FightScale => (1f - 0.15f * Trust) * (1f + 0.2f * Wariness);

    /// <summary>The bond with the player, 0-100 (HorseBondRules).</summary>
    public float Bond { get; private set; }
    /// <summary>The in-game day it was last fed, groomed or ridden, for the bond's wearing down.</summary>
    public float LastCared { get; private set; }
    public float LastGroomed { get; private set; } = -99f;
    /// <summary>The save id of the stable it is kept in, or empty.</summary>
    public string StabledIn { get; private set; } = "";
    public bool IsStabled => !string.IsNullOrEmpty(StabledIn);
    /// <summary>The in-game day it was born (a foal of the player's stable), or -1 for one met wild.</summary>
    public float BornOn { get; private set; } = -1f;
    /// <summary>A mare's: the day she was paired (-1 while not with foal), the stallion's save id, and the day she last foaled.</summary>
    public float WithFoalSince { get; set; } = -1f;
    public string FoalSire { get; set; } = "";
    public float LastFoaled { get; set; } = -99f;
    /// <summary>Its save id (empty for a wild horse).</summary>
    public string SaveId => TryGetComponent(out SaveableEntity entity) ? entity.Id : "";

    /// <summary>The saddle's bags, or null when it wears none.</summary>
    public ItemContainer Cargo { get; private set; }

    /// <summary>Raised when it is tamed, renamed, saddled, born, grown or dies: the map and the menu read it again.</summary>
    public static event Action<Horse> Changed;

    private void Awake()
    {
        health = GetComponent<Damageable>();
        combatant = GetComponent<Combatant>();
        interactable = GetComponent<Interactable>();
        Motion = GetComponent<HorseMotion>();
        Taming = GetComponent<HorseTaming>();
        if (body != null)
        {
            bodyScale = body.localScale;
        }

        if (!Stats.IsRolled && definition != null)
        {
            Vector3 at = transform.position;
            Give(HorseStats.Roll(definition, new System.Random(Mathf.RoundToInt(at.x * 13f) ^ Mathf.RoundToInt(at.z * 7f) ^ Environment.TickCount)));
        }
    }

    private void OnEnable()
    {
        all.Add(this);
        health.Damaged += OnDamaged;
        health.Died += OnDied;
        if (interactable != null)
        {
            interactable.Completed.AddListener(OnUsed);
        }
    }

    private void OnDisable()
    {
        all.Remove(this);
        health.Damaged -= OnDamaged;
        health.Died -= OnDied;
        if (interactable != null)
        {
            interactable.Completed.RemoveListener(OnUsed);
        }
    }

    private void Update()
    {
        // A foal grows over its first days.
        if (BornOn < 0f || Stats.IsGrown)
        {
            return;
        }

        HorseStats stats = Stats;
        stats.growth = HorseBreeding.GrowthAfter(HorseSettings.Current.FoalSize, PlantedTrees.Today - BornOn, HorseSettings.Current);
        Stats = stats;
        ApplyGrowth();
        if (stats.IsGrown)
        {
            SetHealthBase();
            GameMessages.Post($"{HorseName} is grown: old enough to ride and to saddle.", MessageKind.Gain);
            Changed?.Invoke(this);
        }
    }

    /// <summary>Gives it these stats: its health, its stamina (full), its coat, its mane and its size.</summary>
    public void Give(HorseStats stats)
    {
        Stats = stats;
        SetHealthBase();
        Stamina = MaxStamina;
        WearCoat();
        ApplyGrowth();
    }

    // A foal has half a grown horse's health, growing with it.
    private void SetHealthBase() =>
        combatant.Stats.SetBase(StatType.MaxHealth, Stats.maxHealth * (Stats.IsGrown ? 1f : Mathf.Lerp(0.5f, 1f, Stats.growth)));

    private void ApplyGrowth()
    {
        if (body != null)
        {
            body.localScale = bodyScale * (Stats.IsGrown ? 1f : Mathf.Max(0.3f, Stats.growth));
        }
    }

    /// <summary>Makes it the player's: called on the saved copy that takes the wild horse's place when its breaking ride ends.</summary>
    public void BecomeTamed(HorseStats stats, float healthFraction)
    {
        State = Mode.Tamed;
        Bond = Mathf.Max(Bond, HorseSettings.Current.BondWhenTamed);
        LastCared = PlantedTrees.Today;
        Give(stats);
        health.SetHealthFraction(healthFraction);
        Changed?.Invoke(this);
    }

    /// <summary>A foal born in the player's stable: theirs from the start, a little fond of them already.</summary>
    public void BeBorn(HorseStats stats, string stable)
    {
        State = Mode.Tamed;
        BornOn = PlantedTrees.Today;
        Bond = 30f;
        LastCared = BornOn;
        Give(stats);
        health.SetHealthFraction(1f);
        if (!string.IsNullOrEmpty(stable))
        {
            SetStabled(stable);
        }

        Changed?.Invoke(this);
    }

    /// <summary>While HorseTaming has it on a rope or under a rider for the first time.</summary>
    public void SetTamingState(Mode mode)
    {
        if (State != Mode.Tamed)
        {
            State = mode;
        }
    }

    public void Rename(string name)
    {
        horseName = CharacterAppearance.CleanName(name);
        Changed?.Invoke(this);
    }

    // ----- The stable -----

    /// <summary>Kept in a stable (its save id), or let out of it (empty): safe there, not fought, not frightened.</summary>
    public void SetStabled(string stable)
    {
        StabledIn = stable ?? "";
        if (IsStabled)
        {
            health.AddInvulnerability(this);
            combatant.SetFaction(Faction.Neutral);
        }
        else
        {
            health.RemoveInvulnerability(this);
            combatant.SetFaction(Faction.Ally);
        }
    }

    // ----- A wild horse's trust -----

    /// <summary>It ate barley the player left for it, or from their hand: a little calmer.</summary>
    public void Calm()
    {
        if (!IsWild)
        {
            return;
        }

        Trust = Mathf.Min(MostTrust, Trust + 1);
        Motion.PlayEat();
        Player player = Player.Current;
        if (player != null && (player.transform.position - transform.position).sqrMagnitude < 35f * 35f)
        {
            GameMessages.Post(Trust >= MostTrust ? "The horse eats, and lets you be. It trusts you as far as a wild one can." : $"The horse eats, and is a little calmer ({Trust} of {MostTrust}).", MessageKind.Gain);
        }
    }

    /// <summary>Eats food from a crouched player's hand (the best in their bag): it trusts them more.</summary>
    public bool TakeFromHand(Player player)
    {
        ItemInventory items = player != null ? player.GetComponent<ItemInventory>() : null;
        ItemDefinition treat = TreatIn(items);
        if (treat == null || !IsWild)
        {
            return false;
        }

        if (items.Container.Remove(treat, 1) < 1 && (items.SafePocket == null || items.SafePocket.Remove(treat, 1) < 1))
        {
            return false;
        }

        Calm();
        return true;
    }

    /// <summary>It got away from the player: warier from now on.</summary>
    public void GotAway()
    {
        Wariness++;
        Trust = 0;
    }

    // ----- Bond -----

    /// <summary>The bond grows (feeding, grooming, riding), and it counts as cared for today.</summary>
    public void Befriend(float amount)
    {
        if (!IsTamed)
        {
            return;
        }

        WearBond();
        Bond = HorseBondRules.Add(Bond, amount * (1f + PlayerHorsemanship.BondGain()));
        LastCared = PlantedTrees.Today;
    }

    // Whole days without care wear the bond down; counted when it is next looked at.
    private void WearBond()
    {
        float idle = PlantedTrees.Today - LastCared;
        if (idle >= 1f)
        {
            Bond = HorseBondRules.AfterIdle(Bond, idle, HorseSettings.Current);
            LastCared += Mathf.Floor(idle);
        }
    }

    /// <summary>The bond as it stands today (worn down by days of no care).</summary>
    public float CurrentBond()
    {
        WearBond();
        return Bond;
    }

    /// <summary>Ridden this far (metres): the bond grows with the road.</summary>
    public void Ridden(float metres) => Befriend(metres / 1000f * HorseSettings.Current.BondPerKilometre);

    /// <summary>Eats a treat from the player's bag (the best it has: bread before barley). False, with a word, when there is
    /// none or it isn't hungry yet.</summary>
    public bool Feed(ItemInventory from)
    {
        ItemDefinition treat = TreatIn(from);
        if (treat == null)
        {
            GameMessages.Post("You have nothing it would eat: barley, a turnip or bread.", MessageKind.Warning);
            return false;
        }

        if (Time.time - lastFed < FeedCooldownSeconds)
        {
            GameMessages.Post($"{HorseName} isn't hungry yet.", MessageKind.Info);
            return false;
        }

        if (from.Container.Remove(treat, 1) < 1 && (from.SafePocket == null || from.SafePocket.Remove(treat, 1) < 1))
        {
            return false;
        }

        lastFed = Time.time;
        Befriend(treat.HorseTreat);
        Stamina = Mathf.Min(MaxStamina, Stamina + MaxStamina * 0.25f);
        health.Heal(health.MaxHealth * 0.05f);
        Motion.PlayEat();
        PlayerHorsemanship.Practise(HorseWork.Feed);
        GameMessages.Post($"{HorseName} eats the {treat.DisplayName.ToLowerInvariant()} from your hand.", MessageKind.Gain);
        return true;
    }

    /// <summary>The food in a bag a horse likes best (bread before barley), or null.</summary>
    public static ItemDefinition TreatIn(ItemInventory from)
    {
        ItemDefinition best = null;
        if (from == null)
        {
            return null;
        }

        foreach (ItemContainer container in new[] { from.Container, from.SafePocket })
        {
            for (int i = 0; container != null && i < container.SlotCount; i++)
            {
                ItemStack stack = container.GetSlot(i);
                if (!stack.IsEmpty && stack.Item.IsHorseTreat && (best == null || stack.Item.HorseTreat > best.HorseTreat))
                {
                    best = stack.Item;
                }
            }
        }

        return best;
    }

    /// <summary>A brush down: once each in-game day it raises the bond.</summary>
    public void Groom()
    {
        float today = PlantedTrees.Today;
        if (today - LastGroomed < 1f)
        {
            GameMessages.Post($"{HorseName} has been groomed today. Tomorrow again.", MessageKind.Info);
            return;
        }

        LastGroomed = today;
        Befriend(HorseSettings.Current.BondPerGroom);
        Motion.PlayFidget();
        PlayerHorsemanship.Practise(HorseWork.Groom);
        GameMessages.Post($"You brush {HorseName} down. It leans into it.", MessageKind.Gain);
    }

    // ----- The saddle and its bags, the barding -----

    /// <summary>Puts a saddle from the bag on it (the one it wore goes back first). False, with a word, when it can't.</summary>
    public bool PutOnSaddle(ItemDefinition item, ItemInventory from)
    {
        if (item == null || !item.IsSaddle || from == null || !IsTamed)
        {
            return false;
        }

        if (!IsGrown)
        {
            GameMessages.Post($"{HorseName} is too young for a saddle.", MessageKind.Warning);
            return false;
        }

        if (saddle != null && !TakeOffSaddle(from))
        {
            return false;
        }

        if (from.Container.Remove(item, 1) < 1)
        {
            return false;
        }

        WearSaddle(item);
        GameMessages.Post($"{HorseName} wears the {item.DisplayName.ToLowerInvariant()}: {item.SaddleSlots} places in its bags.", MessageKind.Info);
        return true;
    }

    /// <summary>Takes the saddle off into the bag. Refused while its bags hold anything, or the bag has no room.</summary>
    public bool TakeOffSaddle(ItemInventory to)
    {
        if (saddle == null)
        {
            return true;
        }

        if (Cargo != null && Cargo.CountItems() > 0)
        {
            GameMessages.Post("Empty the saddle bags first.", MessageKind.Warning);
            return false;
        }

        if (to == null || to.Container.Add(saddle, 1) < 1)
        {
            GameMessages.Post("There is no room in your bag for the saddle.", MessageKind.Warning);
            return false;
        }

        WearSaddle(null);
        return true;
    }

    private void WearSaddle(ItemDefinition item)
    {
        saddle = item;
        if (saddleModel != null)
        {
            saddleModel.SetActive(item != null);
        }

        foreach (GameObject part in tack)
        {
            if (part != null)
            {
                part.SetActive(item != null || definition.AlwaysArmoured);
            }
        }

        if (Cargo != null)
        {
            Cargo.WeightChanged -= OnLoadChanged;
        }

        Cargo = item != null ? new ItemContainer(Mathf.Min(item.SaddleSlots, 4), Mathf.CeilToInt(item.SaddleSlots / 4f)) : null;
        if (Cargo != null)
        {
            Cargo.SetWeightCapacity(Stats.carryWeight * 1.25f);
            Cargo.WeightChanged += OnLoadChanged;
        }

        Changed?.Invoke(this);
    }

    /// <summary>Puts barding from the bag on it (what it wore goes back first).</summary>
    public bool PutOnArmour(ItemDefinition item, ItemInventory from)
    {
        if (item == null || !item.IsBarding || from == null || !IsTamed)
        {
            return false;
        }

        if (!IsGrown)
        {
            GameMessages.Post($"{HorseName} is too young for barding.", MessageKind.Warning);
            return false;
        }

        if (armour != null && !TakeOffArmour(from))
        {
            return false;
        }

        if (from.Container.Remove(item, 1) < 1)
        {
            return false;
        }

        WearArmour(item);
        GameMessages.Post($"{HorseName} wears the {item.DisplayName.ToLowerInvariant()}.", MessageKind.Info);
        return true;
    }

    public bool TakeOffArmour(ItemInventory to)
    {
        if (armour == null)
        {
            return true;
        }

        if (to == null || to.Container.Add(armour, 1) < 1)
        {
            GameMessages.Post("There is no room in your bag for the barding.", MessageKind.Warning);
            return false;
        }

        WearArmour(null);
        return true;
    }

    private void WearArmour(ItemDefinition item)
    {
        armour = item;
        combatant.Stats.RemoveModifiersFrom(this);
        if (item != null)
        {
            combatant.Stats.AddModifier(new StatModifier(StatType.PhysicalResistance, StatModifierOp.Flat, item.HorseArmour, this));
        }

        if (armourModel != null && !definition.AlwaysArmoured)
        {
            armourModel.gameObject.SetActive(item != null);
        }

        if (armourModel != null && item != null && armourLooks.Length > 0)
        {
            Material look = armourLooks[Mathf.Clamp(item.BardingLook, 0, armourLooks.Length - 1)];
            Material[] worn = armourModel.sharedMaterials;
            for (int i = 0; i < worn.Length; i++)
            {
                worn[i] = look;
            }

            armourModel.sharedMaterials = worn;
        }

        Changed?.Invoke(this);
    }

    private void OnLoadChanged() => Changed?.Invoke(this);

    // ICargoHold: the saddle bags in the cargo window.
    public string CargoTitle => $"{HorseName}'s saddle bags";
    public string CargoDescription => $"Gallops at {Stats.gallopSpeed:0.0} m/s and carries {Stats.carryWeight:0} weight easily; more slows it.";
    public float Weight => Cargo != null ? Cargo.CurrentWeight : 0f;
    public float Capacity => Stats.carryWeight;
    public bool IsOverfilled => Weight > Capacity + 0.001f;
    /// <summary>How much of its pace a load leaves it: full speed up to what it carries easily, down to 60% overloaded.</summary>
    public float SpeedScale => Weight <= Capacity * 0.6f ? 1f : Mathf.Lerp(1f, 0.6f, Mathf.InverseLerp(Capacity * 0.6f, Capacity * 1.25f, Weight));

    /// <summary>Its name and numbers, for its menu.</summary>
    public string StatsText()
    {
        float quality = definition != null ? Stats.Quality(definition) : 0f;
        string grade = quality >= 0.8f ? "a fine" : quality >= 0.5f ? "a good" : quality >= 0.25f ? "an ordinary" : "a poor";
        string nature = HorseTemperamentRules.NameOf(Stats.temperament);
        string age = Stats.IsGrown ? "" : $", a foal {Stats.growth * 100f:0}% grown";
        string bred = Stats.generation > 0 ? $" Bred in your stable (generation {Stats.generation})." : "";
        string foaling = WithFoalSince >= 0f ? " With foal." : "";
        return $"{HorseName}, {grade} {Stats.SexName}{age}, {nature} by nature.{bred}{foaling}\n" +
               $"Health {health.CurrentHealth:0} / {health.MaxHealth:0}   Stamina {Stamina:0} / {MaxStamina:0}   Bond {CurrentBond():0} / 100\n" +
               $"Gallop {Stats.gallopSpeed:0.0} m/s   Carries {Stats.carryWeight:0} weight\n" +
               (saddle != null ? $"Saddle: {saddle.DisplayName} ({saddle.SaddleSlots} places)" : "No saddle: it can carry nothing yet.") +
               (armour != null ? $"   Barding: {armour.DisplayName}" : "");
    }

    // ----- The menu -----

    private void OnUsed()
    {
        GameObject user = interactable.Interactor;
        if (user == null)
        {
            return;
        }

        if (!IsTamed)
        {
            GameMessages.Post(IsWild ? "It won't let you near. Leave barley on the ground for it, or crouch still with some in your bag; then a lasso, thrown from close, might hold it."
                : "Not now: it is fighting you.", MessageKind.Info);
            return;
        }

        if (ActionMenuWindow.Current != null)
        {
            ActionMenuWindow.Current.Open(() => Page(user), user, transform, MenuRange);
        }
    }

    private MenuPage Page(GameObject user)
    {
        var page = new MenuPage { Title = HorseName, Body = StatsText() };
        var riding = PlayerRiding.For(user);
        ItemInventory items = user.GetComponent<ItemInventory>();
        bool visiting = HavenVisit.IsVisiting;
        page.Add(new MenuAction("Ride", !IsGrown ? "Too young to ride" : IsStabled ? "Lead it out first" : $"Space gallops, on its stamina. {GameInput.KeyLabel(GameInput.Whistle)} gets you down.",
            riding != null && Rider == null && !visiting && IsGrown && !IsStabled, () => riding.Mount(this), closesMenu: true));
        page.Add(new MenuAction("Feed it", "Barley, a turnip or bread from your bag", items != null && !visiting, () => Feed(items)));
        page.Add(new MenuAction("Groom it", PlantedTrees.Today - LastGroomed < 1f ? "Groomed today" : "Once a day, for its bond", !visiting, Groom));
        page.Add(new MenuAction("Saddle bags", saddle != null ? $"{Weight:0} / {Capacity:0} weight" : "It needs a saddle", saddle != null && !visiting,
            () => CargoWindow.Create().Open(this, user), closesMenu: true));
        if (items != null)
        {
            foreach (ItemDefinition carried in GearIn(items))
            {
                ItemDefinition chosen = carried;
                string detail = chosen.IsSaddle ? $"{chosen.SaddleSlots} places" : $"Keeps {chosen.HorseArmour * 100f:0}% of a blow off";
                page.Add(new MenuAction($"Put on the {chosen.DisplayName.ToLowerInvariant()}", IsGrown ? detail : "Too young for it", !visiting && IsGrown,
                    () => { if (chosen.IsSaddle) PutOnSaddle(chosen, items); else PutOnArmour(chosen, items); }));
            }

            if (saddle != null)
            {
                page.Add(new MenuAction("Take off the saddle", Cargo != null && Cargo.CountItems() > 0 ? "Empty its bags first" : "Into your bag",
                    !visiting && (Cargo == null || Cargo.CountItems() == 0), () => TakeOffSaddle(items)));
            }

            if (armour != null)
            {
                page.Add(new MenuAction("Take off the barding", "Into your bag", !visiting, () => TakeOffArmour(items)));
            }
        }

        if (IsStabled)
        {
            page.Add(new MenuAction("Lead it out of the stable", "It waits by the door", !visiting, () => HorseStable.LeadOut(this), closesMenu: true));
        }
        else
        {
            HorseStable stable = HorseStable.Nearest(transform.position, HorseStable.LeadReach);
            if (stable != null)
            {
                page.Add(new MenuAction($"Stable it ({stable.Occupants} of {stable.Stalls} stalls)", stable.HasRoom ? "Safe at night, healing; a mare and a stallion may foal" : "The stable is full",
                    !visiting && stable.HasRoom && Rider == null, () => stable.Keep(this), closesMenu: true));
            }
        }

        page.Add(new MenuAction("Give it a name", HasName ? $"It is called {horseName}" : "It has none yet", !visiting, () => HorseNameWindow.Ask(this), closesMenu: true));
        Haulable cart = Haulable.NearestFree(transform.position, 6f);
        if (HorseHitch.Pulling(this) != null)
        {
            page.Add(new MenuAction("Unhitch the cart", null, true, () => HorseHitch.Unhitch(this), closesMenu: true));
        }
        else if (cart != null && IsGrown && !IsStabled)
        {
            page.Add(new MenuAction("Hitch the cart to it", "It pulls the cart while you ride", !visiting, () => HorseHitch.Hitch(this, cart), closesMenu: true));
        }

        return page;
    }

    private static IEnumerable<ItemDefinition> GearIn(ItemInventory items)
    {
        var seen = new HashSet<ItemDefinition>();
        for (int i = 0; i < items.Container.SlotCount; i++)
        {
            ItemStack stack = items.Container.GetSlot(i);
            if (!stack.IsEmpty && (stack.Item.IsSaddle || stack.Item.IsBarding) && seen.Add(stack.Item))
            {
                yield return stack.Item;
            }
        }
    }

    // ----- Hurt and killed -----

    private void OnDamaged(DamageInfo damage, DamageResult result)
    {
        if (!health.IsAlive)
        {
            return;
        }

        Vector3 from = damage.Source != null ? damage.Source.transform.position : transform.position - transform.forward;
        Motion.PlayHit(from);
        if (Taming != null && Taming.OnHurt())
        {
            return;
        }

        HorseSettings settings = HorseSettings.Current;
        float panic = definition.FleeSeconds * Temperament.panic * (IsTamed ? HorseBondRules.PanicScale(CurrentBond(), settings) : 1f);
        if (Rider != null)
        {
            // A horse that hardly knows its rider throws them now and then; a bold one hardly ever; never a Sure Seat.
            bool throws = HorseBondRules.Throws(CurrentBond(), Stats.temperament, settings, UnityEngine.Random.value);
            if (throws && Techniques.Has(PerkTechnique.SureSeat))
            {
                Techniques.Used(PerkTechnique.SureSeat);
                throws = false;
            }

            if (throws)
            {
                GameMessages.Post($"{HorseName} panics and throws you!", MessageKind.Warning);
                Rider.Dismount(thrown: true);
                Motion.Flee(from, panic);
            }
        }
        else if (Stats.temperament == HorseTemperament.Bold && damage.Source != null && IsGrown)
        {
            // A bold horse turns on what hurt it.
            Motion.Strike(damage.Source);
        }
        else
        {
            Motion.Flee(from, panic);
        }

        if (definition.Whinny != null)
        {
            AudioManager.Play(definition.Whinny, transform.position);
        }
    }

    private void OnDied(Damageable victim, DamageInfo killingBlow)
    {
        if (Rider != null)
        {
            Rider.Dismount(thrown: true);
        }

        if (Taming != null)
        {
            Taming.Abandon();
        }

        HorseHitch.Unhitch(this);
        // Its gear and its load stay where it fell.
        foreach (ItemDefinition worn in new[] { saddle, armour })
        {
            if (worn != null)
            {
                DroppedItem.Drop(transform, new ItemStack(worn, 1));
            }
        }

        for (int i = 0; Cargo != null && i < Cargo.SlotCount; i++)
        {
            ItemStack stack = Cargo.GetSlot(i);
            if (!stack.IsEmpty)
            {
                DroppedItem.Drop(transform, stack);
            }
        }

        Cargo?.Clear();
        if (IsTamed)
        {
            GameMessages.Post($"{HorseName} is dead.", MessageKind.Warning);
        }

        Motion?.Die();
        Changed?.Invoke(this);
    }

    // The Main materials of the body take its coat (as Wildlife's winter coat does); the hair its mane.
    private void WearCoat()
    {
        if (definition == null)
        {
            return;
        }

        Material[] manes = definition.Manes;
        if (Stats.mane >= 0 && Stats.mane < manes.Length && manes[Stats.mane] != null)
        {
            foreach (Renderer hair in hairParts)
            {
                if (hair == null)
                {
                    continue;
                }

                Material[] worn = hair.sharedMaterials;
                for (int i = 0; i < worn.Length; i++)
                {
                    worn[i] = manes[Stats.mane];
                }

                hair.sharedMaterials = worn;
            }
        }

        if (Stats.coat < 0 || Stats.coat >= definition.Coats.Length)
        {
            return;
        }

        Color coat = definition.Coats[Stats.coat];
        var block = new MaterialPropertyBlock();
        foreach (Renderer drawn in GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = drawn.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null || !material.name.Contains("Main") || !material.HasProperty(BaseColorId))
                {
                    continue;
                }

                drawn.GetPropertyBlock(block, i);
                Color own = material.GetColor(BaseColorId);
                float shade = material.name.Contains("Dark") ? 0.65f : material.name.Contains("Light") ? 1.3f : 1f;
                block.SetColor(BaseColorId, Color.Lerp(own, coat * shade, 0.85f));
                drawn.SetPropertyBlock(block, i);
            }
        }
    }

    // ----- Saving (tamed horses only: a wild one has no id) -----

    [Serializable]
    private sealed class Saved
    {
        public string name;
        public HorseStats stats;
        public float stamina;
        public string saddle;
        public string armour;
        public List<SlotSaveData> bags = new List<SlotSaveData>();
        public float bond;
        public float lastCared;
        public float lastGroomed = -99f;
        public string stabledIn = "";
        public float bornOn = -1f;
        public float withFoalSince = -1f;
        public string foalSire = "";
        public float lastFoaled = -99f;
    }

    string ISaveable.SaveKey => "horse";

    public string CaptureState()
    {
        var saved = new Saved
        {
            name = horseName, stats = Stats, stamina = Stamina, saddle = saddle != null ? saddle.Id : "",
            armour = armour != null ? armour.Id : "", bond = Bond, lastCared = LastCared, lastGroomed = LastGroomed, stabledIn = StabledIn,
            bornOn = BornOn, withFoalSince = WithFoalSince, foalSire = FoalSire, lastFoaled = LastFoaled,
        };
        if (Cargo != null)
        {
            saved.bags = SaveMapper.ToSlots(Cargo);
        }

        return JsonUtility.ToJson(saved);
    }

    public void RestoreState(string state)
    {
        Saved saved = JsonUtility.FromJson<Saved>(state);
        if (saved == null)
        {
            return;
        }

        restored = true;
        State = Mode.Tamed;
        BornOn = saved.bornOn;
        Bond = saved.bond;
        LastCared = saved.lastCared;
        HorseStats stats = saved.stats.IsRolled ? saved.stats : Stats;
        // Saves from before foals and temperaments: a grown horse.
        if (stats.growth <= 0f)
        {
            stats.growth = 1f;
        }

        // Its health is its Damageable's to save.
        Give(stats);
        horseName = saved.name ?? "";
        Stamina = Mathf.Clamp(saved.stamina, 0f, MaxStamina);
        LastGroomed = saved.lastGroomed;
        WithFoalSince = saved.withFoalSince;
        FoalSire = saved.foalSire ?? "";
        LastFoaled = saved.lastFoaled;
        SetStabled(saved.stabledIn);
        var content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        ItemDefinition Find(string id) => string.IsNullOrEmpty(id) || content == null ? null : content.FindItem(id);
        WearSaddle(Find(saved.saddle));
        WearArmour(Find(saved.armour));
        if (Cargo != null && saved.bags != null && content != null)
        {
            SaveMapper.ReadSlots(saved.bags, Cargo, content, new List<string>());
        }

        Changed?.Invoke(this);
    }

    /// <summary>True for a tamed horse brought back from a save.</summary>
    public bool WasRestored => restored;
}
