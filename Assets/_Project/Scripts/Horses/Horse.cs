using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// A horse: wild, being tamed or the player's own. It keeps its rolled <see cref="HorseStats"/>, its name, its stamina for a
/// gallop and, once saddled, the saddle's bags (an ItemContainer as big as the saddle, as heavy as the horse can carry).
/// A tamed horse is saved (a SaveableEntity spawned when it was tamed) with all of that; a wild one is not and is simply
/// met again another day. Monsters fight it as they fight the player's side; hurt, it runs (HorseMotion), and killed it
/// drops its saddle and load where it fell.
/// <para>
/// Movement and the Animator are HorseMotion's, the lasso struggle and the breaking ride HorseTaming's, the rider's side
/// PlayerRiding's. Interacting with a tamed horse opens its menu: ride, the saddle bags, the saddle, its name.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent), typeof(Damageable), typeof(Combatant))]
public sealed class Horse : MonoBehaviour, ISaveable, ICargoHold
{
    public enum Mode { Wild, Lassoed, Breaking, Tamed }

    private const float MenuRange = 3.5f;

    [SerializeField] private HorseDefinition definition;
    [Tooltip("Where the rider sits (on the horse's back; moves with the body).")]
    [SerializeField] private Transform seat;
    [SerializeField] private Vector3 seatOffset;

    private static readonly List<Horse> all = new List<Horse>();
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private Damageable health;
    private Combatant combatant;
    private Interactable interactable;
    private SaveableEntity entity;
    private ItemDefinition saddle;
    private string horseName = "";
    private bool restored;

    public static IReadOnlyList<Horse> All => all;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => all.Clear();

    public HorseDefinition Definition => definition;
    public HorseStats Stats { get; private set; }
    public Mode State { get; private set; } = Mode.Wild;
    public bool IsTamed => State == Mode.Tamed;
    public bool IsWild => State == Mode.Wild;
    public string HorseName => string.IsNullOrWhiteSpace(horseName) ? (definition != null ? definition.DisplayName : "Horse") : horseName;
    public bool HasName => !string.IsNullOrWhiteSpace(horseName);
    public float Stamina { get; set; }
    public ItemDefinition Saddle => saddle;
    public Damageable Health => health;
    public HorseMotion Motion { get; private set; }
    public HorseTaming Taming { get; private set; }
    public Transform Seat => seat != null ? seat : transform;
    public Vector3 SeatOffset => seatOffset;
    /// <summary>Who rides it now, or null.</summary>
    public PlayerRiding Rider { get; set; }
    public bool IsAlive => health != null && health.IsAlive;

    /// <summary>The saddle's bags, or null when it wears none.</summary>
    public ItemContainer Cargo { get; private set; }

    /// <summary>Raised when it is tamed, renamed, saddled or dies: the map and the menu read it again.</summary>
    public static event Action<Horse> Changed;

    private void Awake()
    {
        health = GetComponent<Damageable>();
        combatant = GetComponent<Combatant>();
        interactable = GetComponent<Interactable>();
        entity = GetComponent<SaveableEntity>();
        Motion = GetComponent<HorseMotion>();
        Taming = GetComponent<HorseTaming>();
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

    /// <summary>Gives it these stats: its health, its stamina (full) and its coat.</summary>
    public void Give(HorseStats stats)
    {
        Stats = stats;
        combatant.Stats.SetBase(StatType.MaxHealth, stats.maxHealth);
        Stamina = stats.maxStamina;
        WearCoat();
    }

    /// <summary>Makes it the player's: called on the saved copy that takes the wild horse's place when its breaking ride ends.</summary>
    public void BecomeTamed(HorseStats stats, float healthFraction)
    {
        Give(stats);
        State = Mode.Tamed;
        health.Revive(Mathf.Clamp01(healthFraction));
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

    // ----- The saddle and its bags -----

    /// <summary>Puts a saddle from the bag on it (the one it wore goes back first). False, with a word, when it can't.</summary>
    public bool PutOnSaddle(ItemDefinition item, ItemInventory from)
    {
        if (item == null || !item.IsSaddle || from == null || !IsTamed)
        {
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

    private void OnLoadChanged() => Changed?.Invoke(this);

    // ICargoHold: the saddle bags in the cargo window.
    public string CargoTitle => $"{HorseName}'s saddle bags";
    public string CargoDescription => $"Gallops at {Stats.gallopSpeed:0.0} m/s and carries {Stats.carryWeight:0} weight easily; more slows it.";
    public float Weight => Cargo != null ? Cargo.CurrentWeight : 0f;
    public float Capacity => Stats.carryWeight;
    public bool IsOverfilled => Weight > Capacity + 0.001f;
    /// <summary>How much of its pace a load leaves it: full speed up to what it carries easily, down to 60% overloaded.</summary>
    public float SpeedScale => Weight <= Capacity * 0.6f ? 1f : Mathf.Lerp(1f, 0.6f, Mathf.InverseLerp(Capacity * 0.6f, Capacity * 1.25f, Weight));

    /// <summary>Its name and numbers, for its menu and the cargo window.</summary>
    public string StatsText()
    {
        float quality = definition != null ? Stats.Quality(definition) : 0f;
        string grade = quality >= 0.8f ? "a fine horse" : quality >= 0.5f ? "a good horse" : quality >= 0.25f ? "an ordinary horse" : "a poor horse";
        return $"{HorseName}, {grade}.\nHealth {health.CurrentHealth:0} / {health.MaxHealth:0}   Stamina {Stamina:0} / {Stats.maxStamina:0}\n" +
               $"Gallop {Stats.gallopSpeed:0.0} m/s   Carries {Stats.carryWeight:0} weight\n" +
               (saddle != null ? $"Saddle: {saddle.DisplayName} ({saddle.SaddleSlots} places)" : "No saddle: it can carry nothing yet.");
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
            GameMessages.Post(IsWild ? "It won't let you near. A lasso, thrown from close, might hold it." : "Not now: it is fighting you.", MessageKind.Info);
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
        page.Add(new MenuAction("Ride", "Space gallops, on its stamina. E gets you down.", riding != null && Rider == null && !visiting, () => riding.Mount(this), closesMenu: true));
        page.Add(new MenuAction("Saddle bags", saddle != null ? $"{Weight:0} / {Capacity:0} weight" : "It needs a saddle", saddle != null && !visiting,
            () => CargoWindow.Create().Open(this, user), closesMenu: true));
        if (items != null)
        {
            foreach (ItemDefinition carried in SaddlesIn(items))
            {
                ItemDefinition chosen = carried;
                page.Add(new MenuAction($"Put on the {chosen.DisplayName.ToLowerInvariant()}", $"{chosen.SaddleSlots} places", !visiting, () => PutOnSaddle(chosen, items)));
            }

            if (saddle != null)
            {
                page.Add(new MenuAction("Take off the saddle", Cargo != null && Cargo.CountItems() > 0 ? "Empty its bags first" : "Into your bag",
                    !visiting && (Cargo == null || Cargo.CountItems() == 0), () => TakeOffSaddle(items)));
            }
        }

        page.Add(new MenuAction("Give it a name", HasName ? $"It is called {horseName}" : "It has none yet", !visiting, () => HorseNameWindow.Ask(this), closesMenu: true));
        Haulable cart = Haulable.NearestFree(transform.position, 6f);
        if (HorseHitch.Pulling(this) != null)
        {
            page.Add(new MenuAction("Unhitch the cart", null, true, () => HorseHitch.Unhitch(this), closesMenu: true));
        }
        else if (cart != null)
        {
            page.Add(new MenuAction("Hitch the cart to it", "It pulls the cart while you ride", !visiting, () => HorseHitch.Hitch(this, cart), closesMenu: true));
        }

        return page;
    }

    private static IEnumerable<ItemDefinition> SaddlesIn(ItemInventory items)
    {
        var seen = new HashSet<ItemDefinition>();
        for (int i = 0; i < items.Container.SlotCount; i++)
        {
            ItemStack stack = items.Container.GetSlot(i);
            if (!stack.IsEmpty && stack.Item.IsSaddle && seen.Add(stack.Item))
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

        if (Taming != null && Taming.OnHurt())
        {
            return;
        }

        if (Rider == null && Motion != null)
        {
            Vector3 from = damage.Source != null ? damage.Source.transform.position : transform.position - transform.forward;
            Motion.Flee(from, definition.FleeSeconds);
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
        // Its saddle and load stay where it fell.
        if (saddle != null)
        {
            DroppedItem.Drop(transform, new ItemStack(saddle, 1));
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

    // The Main materials of the body take the horse's coat (as Wildlife's winter coat does).
    private void WearCoat()
    {
        if (definition == null || Stats.coat < 0 || Stats.coat >= definition.Coats.Length)
        {
            return;
        }

        Color coat = definition.Coats[Stats.coat];
        var block = new MaterialPropertyBlock();
        foreach (Renderer drawn in GetComponentsInChildren<Renderer>())
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
        public List<SlotSaveData> bags = new List<SlotSaveData>();
    }

    string ISaveable.SaveKey => "horse";

    public string CaptureState()
    {
        var saved = new Saved { name = horseName, stats = Stats, stamina = Stamina, saddle = saddle != null ? saddle.Id : "" };
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
        float healthFraction = health.HealthFraction;
        Give(saved.stats.IsRolled ? saved.stats : Stats);
        State = Mode.Tamed;
        horseName = saved.name ?? "";
        Stamina = Mathf.Clamp(saved.stamina, 0f, Stats.maxStamina);
        var content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        ItemDefinition worn = string.IsNullOrEmpty(saved.saddle) || content == null ? null : content.FindItem(saved.saddle);
        WearSaddle(worn);
        if (Cargo != null && saved.bags != null && content != null)
        {
            SaveMapper.ReadSlots(saved.bags, Cargo, content, new List<string>());
        }

        _ = healthFraction;
        Changed?.Invoke(this);
    }

    /// <summary>True for a tamed horse brought back from a save.</summary>
    public bool WasRestored => restored;
}
