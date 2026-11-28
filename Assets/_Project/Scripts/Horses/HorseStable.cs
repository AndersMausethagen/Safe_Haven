using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A stable (a building of the Haven): its stalls keep the player's horses safe (not fought, not hurt, not frightened,
/// through the night and through a breach) and heal them a little every in-game hour. A mare and a stallion stabled
/// together, both grown and both fond enough of the player (HorseSettings › Breeding Bond), have a foal after a few days:
/// a saved horse of the mare's kind, its numbers its parents' (HorseBreeding), born into a free stall, that grows up over
/// a few days more. A horse is stabled and led out from the stable's menu or its own (Horse); which stable keeps it is saved
/// on the horse, and on a load the stable puts it back in a stall. If the stable is pulled down, its horses wait at its door.
/// <para>
/// Its feed trough (four places, filled from the bag in the cargo window, saved with the stable): every few hours each
/// stabled horse eats a meal from it (barley, a turnip, bread), and while fed it mends twice as fast and grows fonder.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[InAFriendsHaven(HavenUse.ItsChoicesDecide, "the stable's stalls are the host's")]
public sealed class HorseStable : MonoBehaviour, ISaveable, ICargoHold
{
    /// <summary>How near a horse must stand to a stable to be led into it.</summary>
    public const float LeadReach = 16f;

    [Tooltip("Where horses stand, facing out (made by BuildHorses). Their number is the stable's.")]
    [SerializeField] private Transform[] stalls = System.Array.Empty<Transform>();
    [Tooltip("Where a horse led out waits.")]
    [SerializeField] private Transform door;

    private static readonly List<HorseStable> all = new List<HorseStable>();
    private readonly Horse[] kept = new Horse[8];
    private SaveableEntity entity;
    private Interactable interactable;
    private float nextCheck;
    private float lastHour = -1f;
    private readonly System.Random random = new System.Random();
    private readonly ItemContainer trough = new ItemContainer(4, 1);
    // The in-game hour each stall's horse is fed until.
    private readonly float[] fedUntil = new float[8];
    // Who stood in each stall when saved (their save ids): a loaded horse goes back to its own stall, with its meal.
    private string[] savedStalls;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => all.Clear();

    public string Id => entity != null ? entity.Id : "";
    public int Stalls => Mathf.Min(stalls.Length, kept.Length);
    public int Occupants
    {
        get
        {
            int count = 0;
            for (int i = 0; i < Stalls; i++)
            {
                if (kept[i] != null)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public bool HasRoom => FreeStall() >= 0;
    private Vector3 DoorPosition => door != null ? door.position : transform.position + transform.forward * 4f;

    private void Awake()
    {
        entity = GetComponent<SaveableEntity>();
        interactable = GetComponent<Interactable>();
    }

    private void OnEnable()
    {
        all.Add(this);
        if (interactable != null)
        {
            interactable.Completed.AddListener(OnUsed);
        }
    }

    private void OnDisable()
    {
        all.Remove(this);
        if (interactable != null)
        {
            interactable.Completed.RemoveListener(OnUsed);
        }

        // Pulled down (or the scene goes): its horses wait at the door. A scene unloading takes them with it anyway.
        if (!gameObject.scene.isLoaded)
        {
            return;
        }

        for (int i = 0; i < kept.Length; i++)
        {
            if (kept[i] != null)
            {
                Release(kept[i]);
                kept[i] = null;
            }
        }
    }

    // The stable's menu: each horse in it to lead out, each of the player's horses near it to lead in.
    private void OnUsed()
    {
        GameObject user = interactable.Interactor;
        if (user != null && ActionMenuWindow.Current != null)
        {
            ActionMenuWindow.Current.Open(Page, user, transform, 6f);
        }
    }

    private MenuPage Page()
    {
        TakeInLoaded();
        // Who tends them each morning (Stablehands), or that someone could.
        float tends = 0f;
        ResidentRecord hand = !HavenVisit.IsVisiting ? Stablehands.BestOf(GameSession.Home, out tends) : null;
        string tended = hand != null ? $" {hand.Resident.DisplayName} tends them each morning: +{tends:0.#} bond a day, and none lost while you are away."
            : !HavenVisit.IsVisiting && Stablehands.Known(GameSession.Home) ? " A Stablehand would tend them each morning (Who does what)." : "";
        // Its painting over the menu (the UX pass of 2026-11-28: the stable was a title, a paragraph and two buttons).
        var page = new MenuPage { Title = "Stable", Body = $"{Occupants} of {Stalls} stalls taken. Horses here are safe, mend and rest; a mare and a stallion both fond of you may foal.{tended}",
            Picture = Resources.Load<Sprite>("WindowArt/Stable") };
        bool visiting = HavenVisit.IsVisiting;
        GameObject user = interactable.Interactor;
        page.Add(new MenuAction($"Feed trough ({Meals()} meals)", "Fed horses mend twice as fast and grow fonder", !visiting && user != null,
            () => CargoWindow.Create().Open(this, user), closesMenu: true));
        for (int i = 0; i < Stalls; i++)
        {
            Horse horse = kept[i];
            if (horse != null && horse.IsAlive)
            {
                string foaling = horse.WithFoalSince >= 0f ? ", in foal" : "";
                page.Add(new MenuAction($"Lead out {horse.HorseName}", $"{horse.Stats.SexName}{foaling}, bond {horse.CurrentBond():0}", !visiting,
                    () => LeadOut(horse), closesMenu: true));
            }
        }

        foreach (Horse horse in Horse.All)
        {
            if (horse != null && horse.IsAlive && horse.IsTamed && !horse.IsStabled && horse.Rider == null && horse.IsMine
                && CoopHorses.HeldByFriend(horse) == null && (horse.transform.position - transform.position).sqrMagnitude < LeadReach * LeadReach)
            {
                Horse chosen = horse;
                page.Add(new MenuAction($"Stable {horse.HorseName}", HasRoom ? "Into a free stall" : "Every stall is taken", !visiting && HasRoom,
                    () => Keep(chosen), closesMenu: true));
            }
        }

        return page;
    }

    /// <summary>The stable within <paramref name="reach"/> of a point, nearest first; null when none.</summary>
    public static HorseStable Nearest(Vector3 point, float reach)
    {
        HorseStable best = null;
        float bestDistance = reach * reach;
        foreach (HorseStable stable in all)
        {
            if (stable == null)
            {
                continue;
            }

            float distance = (stable.transform.position - point).sqrMagnitude;
            if (distance <= bestDistance)
            {
                best = stable;
                bestDistance = distance;
            }
        }

        return best;
    }

    private static HorseStable WithId(string id)
    {
        foreach (HorseStable stable in all)
        {
            if (stable != null && stable.Id == id)
            {
                return stable;
            }
        }

        return null;
    }

    /// <summary>Leads a horse into a free stall. False, with a word, when there is none.</summary>
    public bool Keep(Horse horse)
    {
        if (horse == null || !horse.IsTamed || horse.Rider != null)
        {
            return false;
        }

        int stall = FreeStall();
        if (stall < 0)
        {
            GameMessages.Post("Every stall is taken.", MessageKind.Warning);
            return false;
        }

        HorseHitch.Unhitch(horse);
        Put(horse, stall);
        GameMessages.Post($"{horse.HorseName} is in the stable: safe, fed and resting.", MessageKind.Info);
        return true;
    }

    /// <summary>Lets a stabled horse out to wait by its stable's door.</summary>
    public static void LeadOut(Horse horse)
    {
        if (horse == null || !horse.IsStabled)
        {
            return;
        }

        HorseStable stable = WithId(horse.StabledIn);
        if (stable != null)
        {
            stable.Forget(horse);
        }

        Release(horse, stable != null ? stable.DoorPosition : horse.transform.position + horse.transform.forward * 3f);
    }

    private void Put(Horse horse, int stall, bool keepMeal = false)
    {
        kept[stall] = horse;
        if (!keepMeal)
        {
            fedUntil[stall] = 0f; // a meal is the horse's that ate it, not the stall's
        }

        horse.SetStabled(Id);
        horse.Motion.Stall(true, stalls[stall].position, stalls[stall].rotation);
    }

    private void Forget(Horse horse)
    {
        for (int i = 0; i < kept.Length; i++)
        {
            if (kept[i] == horse)
            {
                kept[i] = null;
                fedUntil[i] = 0f;
            }
        }
    }

    private void Release(Horse horse) => Release(horse, DoorPosition);

    private static void Release(Horse horse, Vector3 at)
    {
        horse.SetStabled("");
        horse.Motion.Stall(false, at, horse.transform.rotation);
    }

    // The stall the save had this horse in, while it is free.
    private int OwnStall(Horse horse)
    {
        string id = horse.SaveId;
        for (int i = 0; savedStalls != null && !string.IsNullOrEmpty(id) && i < savedStalls.Length && i < Stalls; i++)
        {
            if (savedStalls[i] == id && (kept[i] == null || !kept[i].IsAlive || kept[i].StabledIn != Id))
            {
                kept[i] = null;
                return i;
            }
        }

        return -1;
    }

    private int FreeStall()
    {
        for (int i = 0; i < Stalls; i++)
        {
            if (kept[i] == null || !kept[i].IsAlive || kept[i].StabledIn != Id)
            {
                kept[i] = null;
                return i;
            }
        }

        return -1;
    }

    private void Update()
    {
        if (Time.time < nextCheck || string.IsNullOrEmpty(Id))
        {
            return;
        }

        nextCheck = Time.time + 2f;
        TakeInLoaded();
        // The stable's days (meals, foaling) run in its owner's game: on a guest's they ran too, foaling the host's mare a second
        // time for the guest and taking the trough's meals twice.
        if (HavenVisit.IsVisiting)
        {
            return;
        }

        float hour = PlantedTrees.Today * 24f;
        if (lastHour < 0f)
        {
            lastHour = hour;
            return;
        }

        float hours = hour - lastHour;
        if (hours < 1f)
        {
            return;
        }

        lastHour = hour;
        HorseSettings settings = HorseSettings.Current;
        for (int i = 0; i < Stalls; i++)
        {
            Horse horse = kept[i];
            if (horse == null || !horse.IsAlive)
            {
                continue;
            }

            // A meal from the trough when the last one has worn off: while fed, it mends twice as fast and grows fonder, for the
            // hours a meal covered (coming home after a day, one meal gave the doubling for the whole day).
            float elapsed = Mathf.Min(hours, 24f);
            float covered = Mathf.Clamp(fedUntil[i] - (hour - elapsed), 0f, elapsed);
            if (fedUntil[i] <= hour)
            {
                ItemDefinition meal = TakeMeal();
                if (meal != null)
                {
                    fedUntil[i] = hour + settings.TroughMealHours;
                    horse.Befriend(meal.HorseTreat * 0.5f);
                    covered = Mathf.Max(covered, Mathf.Min(1f, elapsed)); // it ate as the last meal wore off, within the hour
                }
            }

            float mend = settings.StableHealPerHour * (elapsed + covered);
            horse.Health.Heal(horse.Health.MaxHealth * mend);
            horse.Stamina = horse.MaxStamina;
            if (covered > 0f)
            {
                horse.Befriend(settings.TroughBondPerHour * covered);
            }
        }

        Breed(settings);
    }

    private ItemDefinition TakeMeal()
    {
        for (int i = 0; i < trough.SlotCount; i++)
        {
            ItemStack stack = trough.GetSlot(i);
            if (!stack.IsEmpty && stack.Item.IsHorseTreat && trough.Remove(stack.Item, 1) > 0)
            {
                return stack.Item;
            }
        }

        return null;
    }

    private int Meals()
    {
        int count = 0;
        for (int i = 0; i < trough.SlotCount; i++)
        {
            ItemStack stack = trough.GetSlot(i);
            if (!stack.IsEmpty && stack.Item.IsHorseTreat)
            {
                count += stack.Count;
            }
        }

        return count;
    }

    // ICargoHold: the trough in the cargo window.
    public ItemContainer Cargo => trough;
    public string CargoTitle => "Feed trough";
    public string CargoArt => HorseSettings.Current.TroughArt;
    public string CargoDescription => $"Barley, turnips or bread. Every {HorseSettings.Current.TroughMealHours:0} hours each stabled horse eats a meal, and while fed it mends twice as fast and grows fonder.";
    public float Weight => trough.CurrentWeight;
    public float Capacity => 1000f;
    public bool IsOverfilled => false;
    public float SpeedScale => 1f;

    // Saving: the trough's food, the hour last looked at and each stall's meal, so the hours away on a raid mend the horses
    // kept here (a stall's meal isn't eaten again just for the Haven loading).
    [System.Serializable]
    private sealed class Saved
    {
        public System.Collections.Generic.List<SlotSaveData> trough = new System.Collections.Generic.List<SlotSaveData>();
        public float lastHour = -1f;
        public float[] fedUntil;
        /// <summary>Who stood in each stall (save ids; empty for none).</summary>
        public string[] horses;
    }

    string ISaveable.SaveKey => "stable";

    public string CaptureState() => JsonUtility.ToJson(new Saved
    {
        trough = SaveMapper.ToSlots(trough), lastHour = lastHour, fedUntil = (float[])fedUntil.Clone(), horses = StallIds(),
    });

    private string[] StallIds()
    {
        var ids = new string[kept.Length];
        for (int i = 0; i < kept.Length; i++)
        {
            ids[i] = kept[i] != null ? kept[i].SaveId : "";
        }

        return ids;
    }

    public void RestoreState(string state)
    {
        Saved saved = JsonUtility.FromJson<Saved>(state);
        var content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        trough.Clear();
        if (saved != null && saved.trough != null && content != null)
        {
            SaveMapper.ReadSlots(saved.trough, trough, content, new System.Collections.Generic.List<string>());
        }

        if (saved != null)
        {
            lastHour = saved.lastHour;
            for (int i = 0; saved.fedUntil != null && i < saved.fedUntil.Length && i < fedUntil.Length; i++)
            {
                fedUntil[i] = saved.fedUntil[i];
            }

            savedStalls = saved.horses;
        }
    }

    // Horses saved as stabled here come back from the load in the open: each goes back to a stall.
    private void TakeInLoaded()
    {
        foreach (Horse horse in Horse.All)
        {
            if (horse == null || !horse.IsAlive || horse.StabledIn != Id || horse.Motion.IsStalled)
            {
                continue;
            }

            Forget(horse);
            // Its own stall, as saved, keeps its meal (Put clears a meal for a horse new to the stall: on every load each horse lost
            // the hours it had been fed).
            int own = OwnStall(horse);
            int stall = own >= 0 ? own : FreeStall();
            if (stall >= 0)
            {
                Put(horse, stall, keepMeal: stall == own);
            }
            else
            {
                Release(horse);
            }
        }
    }

    // A mare and a stallion, both grown and fond of the player: a foal is on the way, and after Days To Foal it is born.
    private void Breed(HorseSettings settings)
    {
        float today = PlantedTrees.Today;
        for (int i = 0; i < Stalls; i++)
        {
            Horse mare = kept[i];
            if (mare == null || !mare.IsAlive || mare.Stats.sex != HorseSex.Mare)
            {
                continue;
            }

            if (mare.WithFoalSince >= 0f)
            {
                if (today - mare.WithFoalSince >= settings.DaysToFoal)
                {
                    Foal(mare, settings, today);
                }

                continue;
            }

            Horse stallion = FondStallion(mare, settings);
            if (stallion != null && today - mare.LastFoaled >= settings.DaysBetweenFoals && mare.CurrentBond() >= settings.BreedingBond)
            {
                mare.WithFoalSince = today;
                mare.FoalSire = stallion.SaveId;
                GameMessages.Post($"{mare.HorseName} is in foal to {stallion.HorseName}. In {settings.DaysToFoal:0} days the stable will be one horse fuller.", MessageKind.Gain);
            }
        }
    }

    private Horse FondStallion(Horse mare, HorseSettings settings)
    {
        for (int i = 0; i < Stalls; i++)
        {
            Horse other = kept[i];
            if (other != null && other != mare && other.IsAlive && HorseBreeding.CanBreed(mare.Stats, other.Stats) && other.CurrentBond() >= settings.BreedingBond)
            {
                return other;
            }
        }

        return null;
    }

    private void Foal(Horse mare, HorseSettings settings, float today)
    {
        HorseStats sire = mare.Stats;
        foreach (Horse horse in Horse.All)
        {
            if (horse != null && horse.SaveId == mare.FoalSire)
            {
                sire = horse.Stats;
            }
        }

        SaveableEntity prefab = mare.Definition.Prefab != null ? mare.Definition.Prefab.GetComponent<SaveableEntity>() : null;
        if (prefab == null)
        {
            return;
        }

        int stall = FreeStall();
        Vector3 at = stall >= 0 ? stalls[stall].position : DoorPosition;
        SaveableEntity made = SaveableEntity.Spawn(prefab, at, stall >= 0 ? stalls[stall].rotation : transform.rotation);
        Horse foal = made.GetComponent<Horse>();
        foal.BeBorn(HorseBreeding.Foal(mare.Stats, sire, mare.Definition, settings, random), stall >= 0 ? Id : "", mare);
        if (stall >= 0)
        {
            Put(foal, stall);
        }

        mare.WithFoalSince = -1f;
        mare.FoalSire = "";
        mare.LastFoaled = today;
        PlayerHorsemanship.Practise(HorseWork.Foal);
        GameMessages.Post($"{mare.HorseName} has foaled: a {foal.Stats.SexName} foal, {HorseTemperamentRules.NameOf(foal.Stats.temperament)} by nature. Give it a name.", MessageKind.Gain);
        HorseNameWindow.Offer(foal);
    }
}
