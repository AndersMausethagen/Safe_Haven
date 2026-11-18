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
/// <para>
/// A tamed horse has an owner (whoever tamed it, or whose stable bore it): theirs to name, saddle, stable and put to work;
/// a friend may borrow it to ride, feed and lead. In co-op one game runs each horse (CoopHorses: the host's, or the player
/// who has it in hand) and the others draw it as a puppet (<see cref="IsPuppet"/>): no thinking, no hurt, put where the
/// runner's game has it.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent), typeof(Damageable), typeof(Combatant))]
[InAFriendsHaven(HavenUse.ItsChoicesDecide, "the visitor's own horse is theirs; another's bags and stores ask the host")]
public sealed class Horse : MonoBehaviour, ISaveable, ICargoHold, IStatusImmunity
{
    public enum Mode { Wild, Lassoed, Breaking, Tamed }

    /// <summary>What a tamed horse does when nobody rides it: grazes about where it was left, follows the player (behind
    /// them on foot, on a lead behind the horse they ride), or stays put.</summary>
    public enum Orders { Graze, Follow, Stay, Work }

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
    // What keeps a puppet unhurt here (the stable keeps its own hold).
    private static readonly object PuppetHold = new object();
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private float roughMetres;
    private float swumMetres;
    private float rodeMetres;
    private int hurtRidden;

    private Damageable health;
    private Combatant combatant;
    private Interactable interactable;
    private ItemDefinition saddle;
    private ItemDefinition armour;
    private string horseName = "";
    private bool restored;
    private string ownerId = "";
    private string ownerName = "";
    private bool arriving;
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
    public string HorseName => !string.IsNullOrWhiteSpace(horseName) ? horseName
        : IsTamed ? (IsMine ? "Your horse" : $"{(string.IsNullOrWhiteSpace(ownerName) ? "A friend" : ownerName)}'s horse")
        : definition != null ? definition.DisplayName : "Horse";

    /// <summary>Who it belongs to: the journey that tamed it or bred it (GameSession.Playtest's id), and their name. Empty for
    /// horses from before owners: the Haven's own.</summary>
    public string OwnerId => ownerId;
    public string OwnerName => ownerName;

    /// <summary>
    /// True when it is this player's: theirs by its owner, or, with none, a horse of the Haven they are in when it is their
    /// own (not a friend's they are visiting). Its owner names it, saddles it, stables it and puts it to work; a friend
    /// borrows it.
    /// </summary>
    public bool IsMine => string.IsNullOrEmpty(ownerId) ? !HavenVisit.IsVisiting : ownerId == GameSession.Playtest.Id;

    /// <summary>Co-op: drawn here for a friend's game that runs it (CoopHorses): it neither thinks nor is hurt here.</summary>
    public bool IsPuppet { get; private set; }

    /// <summary>Co-op: how friends' games name it in their messages (CoopHorses); 0 until it is shared.</summary>
    public uint CoopKey { get; set; }
    public bool HasName => !string.IsNullOrWhiteSpace(horseName);
    public float Stamina { get; set; }
    /// <summary>Its stamina with what its bond adds.</summary>
    // The bond as the rest of the game reads it (CurrentBond, the panel): worn by idle days, whether or not the wear has been
    // written down yet. The raw Bond showed a bond the horse no longer had until something asked.
    public float MaxStamina => Stats.maxStamina * (IsTamed ? HorseBondRules.StaminaScale(BondNow, HorseSettings.Current) : 1f);

    private float BondNow
    {
        get
        {
            float idle = PlantedTrees.Today - LastCared;
            return idle >= 1f ? HorseBondRules.AfterIdle(Bond, idle, HorseSettings.Current) : Bond;
        }
    }
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

    /// <summary>What it does while nobody rides it.</summary>
    public Orders Order { get; private set; } = Orders.Graze;
    /// <summary>Put to work: which job (HorseSettings › Jobs) and where; -1 while it isn't.</summary>
    public int Job { get; private set; } = -1;
    public Vector3 WorkAt { get; private set; }

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
        // Where it is and what it holds go to friends live, from whichever game runs it (CoopHorses), not through HavenSync.
        if (TryGetComponent(out SaveableEntity entity))
        {
            entity.SyncedElsewhere = true;
        }

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
        if (arriving && restored)
        {
            ArriveHome();
        }

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
        BelongTo(GameSession.Playtest.Id, GameSession.Appearance != null ? GameSession.Appearance.DisplayName : "");
        Bond = Mathf.Max(Bond, HorseSettings.Current.BondWhenTamed);
        LastCared = PlantedTrees.Today;
        Give(stats);
        health.SetHealthFraction(healthFraction);
        Changed?.Invoke(this);
    }

    /// <summary>A foal born in the player's stable: its mother's owner's from the start (the player's, for one of theirs), a little
    /// fond of them already.</summary>
    public void BeBorn(HorseStats stats, string stable, Horse mother = null)
    {
        State = Mode.Tamed;
        if (mother != null && !string.IsNullOrEmpty(mother.OwnerId))
        {
            BelongTo(mother.OwnerId, mother.OwnerName);
        }
        else
        {
            BelongTo(GameSession.Playtest.Id, GameSession.Appearance != null ? GameSession.Appearance.DisplayName : "");
        }
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

    private void BelongTo(string id, string name)
    {
        ownerId = id ?? "";
        ownerName = name ?? "";
    }

    /// <summary>
    /// Co-op: drawn for a friend's game that runs it (on), or run here again (off). A puppet doesn't think, move by itself,
    /// fight the rope or take hurt; CoopHorses puts it where the runner's game has it and mirrors its animation.
    /// </summary>
    public void SetPuppet(bool puppet)
    {
        if (IsPuppet == puppet)
        {
            return;
        }

        IsPuppet = puppet;
        if (puppet)
        {
            if (Taming != null)
            {
                Taming.Abandon();
            }

            health.AddInvulnerability(PuppetHold);
        }
        else
        {
            health.RemoveInvulnerability(PuppetHold);
        }

        Motion.SetPuppet(puppet);
    }

    /// <summary>Co-op: it died in the game that runs it. Here it only falls (its gear dropped there).</summary>
    public void DieAsPuppet()
    {
        if (IsPuppet && health.IsAlive)
        {
            health.Finish(null);
        }
    }

    /// <summary>
    /// Co-op: the game that keeps it lets go of an order that was about them (follow me): it grazes where it is, quietly.
    /// </summary>
    public void ForgetFollowing()
    {
        if (Order == Orders.Follow)
        {
            Order = Orders.Graze;
        }
    }

    // Brought home from a friend's Haven (HavenVisit): it comes to graze near the portal, wherever the save had it.
    private void ArriveHome()
    {
        arriving = false;
        Vector3 centre = HomePortal.Current != null ? HomePortal.Current.transform.position : transform.position;
        for (int attempt = 0; attempt < 12; attempt++)
        {
            Vector2 step = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(9f, 16f);
            if (NavMesh.SamplePosition(centre + new Vector3(step.x, 0f, step.y), out NavMeshHit hit, 4f, NavMesh.AllAreas))
            {
                if (TryGetComponent(out NavMeshAgent agent) && agent.enabled)
                {
                    agent.Warp(hit.position);
                }
                else
                {
                    transform.position = hit.position;
                }

                break;
            }
        }

        Order = Orders.Graze;
        GameMessages.Post($"{HorseName} came home with you from your friend's Haven.", MessageKind.Gain);
    }

    // ----- Co-op: what friends' games need of it -----

    [Serializable]
    private sealed class Shared
    {
        public int mode;
        public int trust;
        public int wariness;
        public HorseStats stats;
        public string saved;
    }

    /// <summary>Co-op: everything a friend's copy needs to be this horse (wild or tamed), as text.</summary>
    public string CaptureShared() => JsonUtility.ToJson(new Shared
    {
        mode = (int)State, trust = Trust, wariness = Wariness, stats = Stats, saved = IsTamed ? CaptureState() : null,
    });

    /// <summary>Co-op: becomes the horse a friend's game described (<see cref="CaptureShared"/>).</summary>
    public void RestoreShared(string text)
    {
        Shared shared = string.IsNullOrEmpty(text) ? null : JsonUtility.FromJson<Shared>(text);
        if (shared == null)
        {
            return;
        }

        if (shared.mode == (int)Mode.Tamed && !string.IsNullOrEmpty(shared.saved))
        {
            RestoreState(shared.saved);
            return;
        }

        if (shared.stats.IsRolled && !shared.stats.Equals(Stats))
        {
            Give(shared.stats);
        }

        State = (Mode)Mathf.Clamp(shared.mode, 0, 2);
        Trust = Mathf.Clamp(shared.trust, 0, MostTrust);
        Wariness = Mathf.Max(0, shared.wariness);
    }

    /// <summary>
    /// Co-op: a number that changes whenever a friend would see it differently: its state, name, owner, coat and size, gear
    /// and load, orders, stable, and bond and trust in whole steps. Stamina, health and the metres it has gone are left out
    /// (they change all the time, and travel with its every pose).
    /// </summary>
    public int ShareMark()
    {
        unchecked
        {
            int mark = (int)State * 31 + Trust * 7 + Wariness;
            mark = mark * 31 + (horseName ?? "").GetHashCode();
            mark = mark * 31 + ownerId.GetHashCode();
            mark = mark * 31 + Stats.coat * 97 + Stats.mane * 13 + Stats.rareCoat * 7 + (int)Stats.traits + (int)Stats.temperament * 3;
            mark = mark * 31 + Mathf.RoundToInt(Stats.growth * 50f);
            mark = mark * 31 + (saddle != null ? saddle.Id.GetHashCode() : 0);
            mark = mark * 31 + (armour != null ? armour.Id.GetHashCode() : 0);
            mark = mark * 31 + Mathf.FloorToInt(Bond);
            mark = mark * 31 + (StabledIn ?? "").GetHashCode();
            mark = mark * 31 + (int)Order * 5 + Job;
            for (int i = 0; Cargo != null && i < Cargo.SlotCount; i++)
            {
                ItemStack stack = Cargo.GetSlot(i);
                mark = mark * 31 + (stack.IsEmpty ? 0 : stack.Item.Id.GetHashCode() + stack.Count * 7919);
            }

            return mark;
        }
    }

    /// <summary>Co-op, pure: whose a saved horse's state (its "horse" component) says it is. Empty for none.</summary>
    public static string OwnerIn(string state)
    {
        Saved saved = string.IsNullOrEmpty(state) ? null : JsonUtility.FromJson<Saved>(state);
        return saved != null ? saved.owner ?? "" : "";
    }

    /// <summary>Co-op, pure: the same saved state, marked to come home (it grazes near the portal when next loaded).</summary>
    public static string ComingHome(string state)
    {
        Saved saved = string.IsNullOrEmpty(state) ? null : JsonUtility.FromJson<Saved>(state);
        if (saved == null)
        {
            return state;
        }

        saved.arriving = true;
        saved.stabledIn = "";
        saved.order = (int)Orders.Graze;
        saved.job = -1;
        return JsonUtility.ToJson(saved);
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

    // ----- Orders -----

    public void Command(Orders order)
    {
        Order = order;
        if (order != Orders.Work)
        {
            Job = -1;
        }

        GameMessages.Post(order == Orders.Follow ? $"{HorseName} follows you." : order == Orders.Stay ? $"{HorseName} stays here." : $"{HorseName} grazes where it is.", MessageKind.Info);
    }

    /// <summary>Puts it to work beside a station (ploughing by the farm plots, hauling for the sawbench).</summary>
    public void PutToWork(int job, Vector3 at)
    {
        Order = Orders.Work;
        Job = job;
        WorkAt = at;
        var jobs = HorseSettings.Current.Jobs;
        if (job >= 0 && job < jobs.Count)
        {
            GameMessages.Post($"{HorseName} is put to work: {jobs[job].name.ToLowerInvariant()}. Stations of its kind nearby work {jobs[job].bonus * 100f:0}% faster.", MessageKind.Gain);
        }
    }

    // ----- Status effects a breed shrugs off -----

    public bool Resists(StatusEffectDefinition effect)
    {
        if (effect == null || definition == null)
        {
            return false;
        }

        string name = (effect.Id + " " + effect.DisplayName).ToLowerInvariant();
        return definition.Has(HorseAbility.PoisonProof) && (name.Contains("poison") || name.Contains("rot") || name.Contains("venom"))
            || definition.Has(HorseAbility.ColdProof) && (name.Contains("chill") || name.Contains("frost") || name.Contains("cold"));
    }

    // ----- Traits from use -----

    /// <summary>The way it went this frame, for what it learns by it: rough ground, swimming, distance.</summary>
    public void Travelled(float metres, bool rough, bool swimming)
    {
        if (!IsTamed || metres <= 0f)
        {
            return;
        }

        HorseSettings settings = HorseSettings.Current;
        rodeMetres += metres;
        if (rough)
        {
            roughMetres += metres;
        }

        if (swimming)
        {
            swumMetres += metres;
        }

        if (roughMetres >= settings.SureFootedMetres)
        {
            Learn(HorseTrait.SureFooted, "sure-footed: rough ground no longer slows it");
        }

        if (swumMetres >= settings.SwimmerMetres)
        {
            Learn(HorseTrait.Swimmer, "a strong swimmer: it swims faster and tires less");
        }

        if (rodeMetres >= settings.EnduringMetres)
        {
            Learn(HorseTrait.Enduring, "enduring: its breath comes back sooner and a gallop costs it less");
        }
    }

    /// <summary>It learns a trait (once): told to the player, and passed on to its foals half the time.</summary>
    public void Learn(HorseTrait trait, string what)
    {
        if (Stats.Has(trait))
        {
            return;
        }

        HorseStats stats = Stats;
        stats.traits |= trait;
        Stats = stats;
        GameMessages.Post($"{HorseName} has grown {what}.", MessageKind.Gain);
        Changed?.Invoke(this);
    }

    /// <summary>The names of what it has learned, for its panel.</summary>
    public string TraitNames()
    {
        var names = new List<string>();
        if (Stats.Has(HorseTrait.SureFooted)) names.Add("Sure-footed");
        if (Stats.Has(HorseTrait.Brave)) names.Add("Brave");
        if (Stats.Has(HorseTrait.Swimmer)) names.Add("Swimmer");
        if (Stats.Has(HorseTrait.Enduring)) names.Add("Enduring");
        return names.Count > 0 ? string.Join(", ", names) : "";
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
        // Its bond is with its owner: a friend who borrows it feeds and rides it, but it isn't theirs to win.
        if (IsTamed && !IsMine && amount > 0f)
        {
            return;
        }

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
    public string CargoArt => HorseSettings.Current.SaddlebagsArt;
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

        // In co-op, whoever has it in hand runs it: asking for its menu takes it in hand (or says who has it).
        if (CoopHorses.TakeInHand(this))
        {
            HorsePanelWindow.Show(this, user, MenuRange);
        }
    }

    /// <summary>What can be done with it, by <paramref name="user"/>: its window's buttons (HorsePanelWindow), each tagged
    /// with where it goes (MenuAction.Group: Main, Care, Gear, Name) and its icon.</summary>
    public MenuPage MenuFor(GameObject user)
    {
        var page = new MenuPage { Title = HorseName, Body = StatsText() };
        var riding = PlayerRiding.For(user);
        ItemInventory items = user.GetComponent<ItemInventory>();
        // A visitor rides, feeds, grooms and leads a friend's horse; its owner names, saddles, stables and puts it to work, and
        // stabling and jobs are the Haven's (the host's) to decide.
        bool visiting = HavenVisit.IsVisiting;
        bool mine = IsMine;
        bool ownersOnly = !visiting && mine;
        string theirs = $"{(string.IsNullOrWhiteSpace(ownerName) ? "Its owner" : ownerName)}'s to decide";
        if (!mine)
        {
            page.Title = $"{HorseName} <size=70%>({(string.IsNullOrWhiteSpace(ownerName) ? "a friend" : ownerName)}'s)</size>";
        }

        void Add(string group, string icon, MenuAction action)
        {
            action.Group = group;
            action.Icon = icon;
            page.Add(action);
        }

        Add("Main", "HorseHead", new MenuAction("Ride", !IsGrown ? "Too young to ride" : IsStabled ? "Lead it out first" : $"Tap {GameInput.KeyLabel(GameInput.HorseGallop)} to canter, hold it to gallop; {GameInput.KeyLabel(GameInput.Dodge)} jumps. {GameInput.KeyLabel(GameInput.Whistle)} gets you down.",
            riding != null && Rider == null && IsGrown && !IsStabled, () => riding.Mount(this), closesMenu: true));
        Add("Care", "Wheat", new MenuAction("Feed it", "Barley, a turnip or bread from your bag", items != null, () => Feed(items)));
        Add("Care", "Brush", new MenuAction("Groom it", PlantedTrees.Today - LastGroomed < 1f ? "Groomed today" : "Once a day, for its bond", true, Groom));
        // A friend's bags are theirs; a visitor uses the host's horses' bags as they would the stash, with the host's yes.
        bool bags = mine || (visiting && HavenVisit.StashAllowed);
        Add("Gear", "Bags", new MenuAction("Saddle bags", saddle == null ? "It needs a saddle" : bags ? $"{Weight:0} / {Capacity:0} weight" : visiting ? "Ask to use the stores first" : theirs,
            saddle != null && (bags || visiting), () => { if (bags || !HavenVisit.StoresNeedAsking()) CargoWindow.Create().Open(this, user); }, closesMenu: true));
        if (items != null)
        {
            foreach (ItemDefinition carried in GearIn(items))
            {
                ItemDefinition chosen = carried;
                string detail = chosen.IsSaddle ? $"{chosen.SaddleSlots} places" : $"Keeps {chosen.HorseArmour * 100f:0}% of a blow off";
                Add("Gear", chosen.IsSaddle ? "Saddle" : "Barding", new MenuAction($"Put on the {chosen.DisplayName.ToLowerInvariant()}", !mine ? theirs : IsGrown ? detail : "Too young for it", mine && IsGrown,
                    () => { if (chosen.IsSaddle) PutOnSaddle(chosen, items); else PutOnArmour(chosen, items); }));
            }

            if (saddle != null)
            {
                Add("Gear", "Saddle", new MenuAction("Take off the saddle", !mine ? theirs : Cargo != null && Cargo.CountItems() > 0 ? "Empty its bags first" : "Into your bag",
                    mine && (Cargo == null || Cargo.CountItems() == 0), () => TakeOffSaddle(items)));
            }

            if (armour != null)
            {
                Add("Gear", "Barding", new MenuAction("Take off the barding", mine ? "Into your bag" : theirs, mine, () => TakeOffArmour(items)));
            }
        }

        if (HorseTravel.InRaid == this)
        {
            // In a raid: tied at the portal it came through, it is safe and comes home whatever happens.
            Add("Main", "Stay", HorseTravel.IsTied
                ? new MenuAction("Untie it", "It can be hurt again, and must be near you when you extract", true, () => HorseTravel.Tie(this, false), closesMenu: true)
                : new MenuAction("Tie it at the portal", HorseTravel.CanTieHere(this) ? "Safe here, and home whatever happens" : "Lead it back to the portal first",
                    HorseTravel.CanTieHere(this) && Rider == null, () => HorseTravel.Tie(this, true), closesMenu: true));
        }
        else if (IsStabled)
        {
            Add("Main", "Stable", new MenuAction("Lead it out", ownersOnly ? "It waits by the stable door" : theirs, ownersOnly, () => HorseStable.LeadOut(this), closesMenu: true));
        }
        else
        {
            HorseStable stable = HorseStable.Nearest(transform.position, HorseStable.LeadReach);
            if (stable != null)
            {
                Add("Main", "Stable", new MenuAction("Stable it", !ownersOnly ? theirs : stable.HasRoom ? $"Safe at night, healing; a mare and a stallion may foal ({stable.Occupants} of {stable.Stalls} stalls)" : "The stable is full",
                    ownersOnly && stable.HasRoom && Rider == null, () => stable.Keep(this), closesMenu: true));
            }
        }

        if (!IsStabled && IsGrown)
        {
            var jobs = HorseSettings.Current.Jobs;
            if (Order == Orders.Work)
            {
                Add("Care", "Gear", new MenuAction("Stop working", !ownersOnly ? theirs : Job >= 0 && Job < jobs.Count ? jobs[Job].name : null, ownersOnly, () => Command(Orders.Graze), closesMenu: true));
            }
            else
            {
                for (int i = 0; i < jobs.Count; i++)
                {
                    int chosen = i;
                    CraftingStation station = HorseJobs.NearestStation(jobs[i].station, transform.position, 30f);
                    if (station != null)
                    {
                        Add("Care", "Gear", new MenuAction(jobs[i].name, ownersOnly ? $"{jobs[i].description} (+{jobs[i].bonus * 100f:0}%)" : theirs, ownersOnly,
                            () => PutToWork(chosen, station.transform.position), closesMenu: true));
                    }
                }
            }
        }

        if (!IsStabled)
        {
            Add("Main", "Lead", new MenuAction(Order == Orders.Follow ? "Stop following" : "Follow me",
                Order == Orders.Follow ? "It grazes where it is" : "Behind you on foot; on a lead behind the horse you ride", IsGrown,
                () => Command(Order == Orders.Follow ? Orders.Graze : Orders.Follow), closesMenu: true));
            Add("Main", Order == Orders.Stay ? "Graze" : "Stay", new MenuAction(Order == Orders.Stay ? "Let it graze" : "Stay here", Order == Orders.Stay ? "It wanders a little" : "It waits right here",
                true, () => Command(Order == Orders.Stay ? Orders.Graze : Orders.Stay), closesMenu: true));
        }

        Add("Name", "Pencil", new MenuAction("Give it a name", !mine ? theirs : HasName ? $"It is called {horseName}" : "It has none yet", mine, () => HorseNameWindow.Ask(this), closesMenu: true));
        Haulable cart = Haulable.NearestFree(transform.position, 6f);
        if (HorseHitch.Pulling(this) != null)
        {
            Add("Gear", "Wheel", new MenuAction("Unhitch the cart", "It stops pulling", true, () => HorseHitch.Unhitch(this), closesMenu: true));
        }
        else if (cart != null && IsGrown && !IsStabled)
        {
            Add("Gear", "Wheel", new MenuAction("Hitch the cart to it", ownersOnly ? "It pulls the cart while you ride" : theirs, ownersOnly, () => HorseHitch.Hitch(this, cart), closesMenu: true));
        }

        return page;
    }

    /// <summary>How far it has come towards learning a trait (0 to 1; 1 once learned): rough ground for Sure-footed, swims
    /// for Swimmer, the road for Enduring, blows taken under a rider for Brave.</summary>
    public float TraitProgress(HorseTrait trait)
    {
        if (Stats.Has(trait))
        {
            return 1f;
        }

        HorseSettings settings = HorseSettings.Current;
        switch (trait)
        {
            case HorseTrait.SureFooted: return Mathf.Clamp01(roughMetres / Mathf.Max(1f, settings.SureFootedMetres));
            case HorseTrait.Swimmer: return Mathf.Clamp01(swumMetres / Mathf.Max(1f, settings.SwimmerMetres));
            case HorseTrait.Enduring: return Mathf.Clamp01(rodeMetres / Mathf.Max(1f, settings.EnduringMetres));
            case HorseTrait.Brave: return Mathf.Clamp01(hurtRidden / (float)Mathf.Max(1, settings.BraveHits));
            default: return 0f;
        }
    }

    /// <summary>How far it has been ridden and led since it was tamed (metres).</summary>
    public float RiddenMetres => rodeMetres;

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
        if (Rider != null && IsTamed && ++hurtRidden >= settings.BraveHits)
        {
            Learn(HorseTrait.Brave, "brave: it no longer panics, throws you or shies from monsters");
        }

        if (Rider != null && Stats.Has(HorseTrait.Brave))
        {
            // A brave horse takes the blow and keeps going.
        }
        else if (Rider != null)
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
        // A friend's game runs it: its gear fell there (and comes to this game as the dropped items it left).
        if (IsPuppet)
        {
            if (Motion != null)
            {
                Motion.Die();
            }

            Changed?.Invoke(this);
            return;
        }

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

        if (Motion != null)
        {
            Motion.Die();
        }

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

        RareCoat? rare = definition.RareCoatOf(Stats.rareCoat);
        if (rare.HasValue && rare.Value.mane >= 0 && rare.Value.mane < manes.Length && manes[rare.Value.mane] != null)
        {
            foreach (Renderer hair in hairParts)
            {
                if (hair != null)
                {
                    Material[] worn = hair.sharedMaterials;
                    for (int i = 0; i < worn.Length; i++)
                    {
                        worn[i] = manes[rare.Value.mane];
                    }

                    hair.sharedMaterials = worn;
                }
            }
        }

        if (!rare.HasValue && (Stats.coat < 0 || Stats.coat >= definition.Coats.Length))
        {
            return;
        }

        Color coat = rare.HasValue ? rare.Value.tint : definition.Coats[Stats.coat];
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
                if (rare.HasValue && rare.Value.pattern != null)
                {
                    block.SetTexture(BaseMapId, rare.Value.pattern);
                }

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
        public int order;
        public float roughMetres;
        public float swumMetres;
        public float rodeMetres;
        public int hurtRidden;
        public int job = -1;
        public Vector3 workAt;
        public string owner = "";
        public string ownerName = "";
        public bool arriving;
        /// <summary>Its health as a share of its maximum; -1 in saves from before (2026-11-16).</summary>
        public float healthShare = -1f;
    }

    string ISaveable.SaveKey => "horse";

    public string CaptureState()
    {
        var saved = new Saved
        {
            name = horseName, stats = Stats, stamina = Stamina, saddle = saddle != null ? saddle.Id : "",
            armour = armour != null ? armour.Id : "", bond = Bond, lastCared = LastCared, lastGroomed = LastGroomed, stabledIn = StabledIn,
            bornOn = BornOn, withFoalSince = WithFoalSince, foalSire = FoalSire, lastFoaled = LastFoaled,
            order = (int)Order, job = Job, workAt = WorkAt, roughMetres = roughMetres, swumMetres = swumMetres, rodeMetres = rodeMetres, hurtRidden = hurtRidden,
            owner = ownerId, ownerName = ownerName, arriving = arriving,
            healthShare = health != null ? health.HealthFraction : -1f,
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

        // Its health is its Damageable's to save, but the Damageable may restore first, against the maximum of the stats Awake
        // rolled at random: Give then rescales it by saved/rolled. The saved share is put back after the saved stats.
        Give(stats);
        if (saved.healthShare >= 0f && health != null)
        {
            health.SetHealthFraction(saved.healthShare);
        }
        horseName = saved.name ?? "";
        Stamina = Mathf.Clamp(saved.stamina, 0f, MaxStamina);
        LastGroomed = saved.lastGroomed;
        WithFoalSince = saved.withFoalSince;
        FoalSire = saved.foalSire ?? "";
        LastFoaled = saved.lastFoaled;
        Order = (Orders)Mathf.Clamp(saved.order, 0, 3);
        Job = saved.job;
        WorkAt = saved.workAt;
        if (Order == Orders.Work && Job < 0)
        {
            Order = Orders.Graze;
        }
        BelongTo(saved.owner, saved.ownerName);
        arriving = saved.arriving;
        roughMetres = saved.roughMetres;
        swumMetres = saved.swumMetres;
        rodeMetres = saved.rodeMetres;
        hurtRidden = saved.hurtRidden;
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
