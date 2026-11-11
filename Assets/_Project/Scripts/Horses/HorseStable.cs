using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A stable (a building of the Haven): its stalls keep the player's horses safe (not fought, not hurt, not frightened,
/// through the night and through a breach) and heal them a little every in-game hour. A mare and a stallion stabled
/// together, both grown and both fond enough of the player (HorseSettings › Breeding Bond), have a foal after a few days:
/// a saved horse of the mare's kind, its numbers its parents' (HorseBreeding), born into a free stall, that grows up over
/// a few days more. A horse is stabled and led out from the stable's menu or its own (Horse); which stable keeps it is saved
/// on the horse, and on a load the stable puts it back in a stall. If the stable is pulled down, its horses wait at its door.
/// </summary>
[DisallowMultipleComponent]
public sealed class HorseStable : MonoBehaviour
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
        var page = new MenuPage { Title = "Stable", Body = $"{Occupants} of {Stalls} stalls taken. Horses here are safe, mend and rest; a mare and a stallion both fond of you may foal." };
        bool visiting = HavenVisit.IsVisiting;
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
            if (horse != null && horse.IsAlive && horse.IsTamed && !horse.IsStabled && horse.Rider == null
                && (horse.transform.position - transform.position).sqrMagnitude < LeadReach * LeadReach)
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

    private void Put(Horse horse, int stall)
    {
        kept[stall] = horse;
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
            }
        }
    }

    private void Release(Horse horse) => Release(horse, DoorPosition);

    private static void Release(Horse horse, Vector3 at)
    {
        horse.SetStabled("");
        horse.Motion.Stall(false, at, horse.transform.rotation);
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
            if (horse != null && horse.IsAlive)
            {
                horse.Health.Heal(horse.Health.MaxHealth * settings.StableHealPerHour * Mathf.Min(hours, 24f));
                horse.Stamina = horse.MaxStamina;
            }
        }

        Breed(settings);
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
            int stall = FreeStall();
            if (stall >= 0)
            {
                Put(horse, stall);
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
        foal.BeBorn(HorseBreeding.Foal(mare.Stats, sire, mare.Definition, settings, random), stall >= 0 ? Id : "");
        if (stall >= 0)
        {
            Put(foal, stall);
        }

        mare.WithFoalSince = -1f;
        mare.FoalSire = "";
        mare.LastFoaled = today;
        PlayerHorsemanship.Practise(HorseWork.Foal);
        GameMessages.Post($"{mare.HorseName} has foaled: a {foal.Stats.SexName} foal, {HorseTemperamentRules.NameOf(foal.Stats.temperament)} by nature. Give it a name.", MessageKind.Gain);
        HorseNameWindow.Ask(foal);
    }
}
