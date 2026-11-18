using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A building's level and the work on it. A building placed from the build menu starts as a construction site: a
/// scaffold stands around it and the model rises out of the ground with every hammer blow while the player holds E
/// (its BuildingDefinition's Build Work in seconds; kinds with no work stand at once). Upgrading (the build menu's
/// "Upgrade or repair", or a station's Upgrade button) pays the next level's cost and puts the scaffold up again; the
/// finished level adds visible dressing (level 2: a stone plinth and lanterns, level 3: banners and a flower box) and
/// its bonuses (a faster, bigger station, more storage, renown). A ruin placed in the scene (Starts Ruined) waits dark
/// and sunken until the player pays and repairs it.
/// <para>
/// While the first construction or a repair runs, the building's own use (station, chest) is switched off. Work done
/// is kept when the player stops, and saved. Finishing pops the building, raises dust and, for a new building, the
/// Built game event (quests). How it looks (rising, scaffold, dressing, dust): <see cref="ConstructionLook"/>, with looks and
/// sounds in ConstructionSettings. Lives on every building prefab.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BuildingObject))]
[InAFriendsHaven(HavenUse.AsksTheHost, "hammering and upgrading wait for the host's yes (HavenVisit.BuildingNeedsAsking)")]
public sealed class Construction : MonoBehaviour, ISaveable, ISharedState, IStationMenuExtension
{
    [Tooltip("A ruin waiting to be repaired: dark, sunken and tilted until the player pays its cost and hammers it up.")]
    [SerializeField] private bool startsRuined;

    private BuildingObject building;
    private ConstructionLook look;
    private Damageable health;
    private bool shownBroken;
    private Interactable siteInteractable;
    private bool built;
    private float nextBlow;

    /// <summary>Raised when work on a building is finished: built, repaired or upgraded.</summary>
    public static event Action<Construction> Finished;

    public BuildingDefinition Definition => building != null ? building.Definition : GetComponent<BuildingObject>().Definition;

    /// <summary>0 while it is first being built (or is a ruin), then 1 and up.</summary>
    public int Level { get; private set; } = 1;

    /// <summary>The level being worked towards; equal to Level when nothing is being built.</summary>
    public int Target { get; private set; } = 1;

    public float Work { get; private set; }
    public bool Paid { get; private set; } = true;
    public bool StartsRuined => startsRuined;
    public bool IsBuilding => Target > Level;
    public bool IsStanding => Level >= 1;

    /// <summary>
    /// Whether a defence (a trap, a ward totem, a watch post) does its work: it stands (not a scaffold or a ruin) and isn't broken
    /// down; being upgraded it goes on. The one rule for all three (review 2026-11-16: each had its own, and a broken trap still bit).
    /// </summary>
    public static bool DefenceWorks(Construction construction, Damageable health) =>
        (construction == null || construction.IsStanding) && (health == null || !health.IsBroken);
    public bool CanUpgrade => IsStanding && !IsBuilding && Level < Definition.MaxLevel;
    public float WorkNeeded => Target <= 1 ? (Definition.BuildWork > 0f ? Definition.BuildWork : ConstructionSettings.Current.RepairWork)
        : Definition.UpgradeTo(Target)?.work ?? 0f;
    public float Share => WorkNeeded > 0f ? Mathf.Clamp01(Work / WorkNeeded) : 1f;

    public string SaveKey => "construction";

    bool ISharedState.IsShared => true;

    int ISharedState.SharedMark => unchecked(((Level * 31 + Target) * 31 + Mathf.FloorToInt(Work)) * 2 + (Paid ? 1 : 0));

    /// <summary>The name with its level's title: "Sturdy Sawbench".</summary>
    public string Title => Level >= 2 && Definition.UpgradeTo(Level) != null ? $"{Definition.UpgradeTo(Level).title} {Definition.DisplayName}" : Definition.DisplayName;

    /// <summary>A level's bonuses added up over every level reached (2 up to Level).</summary>
    public float Sum(Func<BuildingLevel, float> bonus)
    {
        float total = 0f;
        for (int level = 2; level <= Level; level++)
        {
            BuildingLevel upgrade = Definition.UpgradeTo(level);
            if (upgrade != null)
            {
                total += bonus(upgrade);
            }
        }

        return total;
    }

    // ----- Starting work -----

    /// <summary>A building just placed from the build menu (already paid for): a scaffold to hammer up.</summary>
    public void BeginBuilding()
    {
        Level = 0;
        Target = 1;
        Work = 0f;
        Paid = true;
        Refresh();
    }

    /// <summary>The cost of the work waiting to be paid: the next level's, or the building's own for a ruin.</summary>
    public void Cost(out IReadOnlyList<ResourceAmount> resources, out IReadOnlyList<ItemAmount> items)
    {
        int next = IsBuilding ? Target : Level + 1;
        if (next <= 1)
        {
            resources = Definition.ConstructionCost;
            items = Definition.ConstructionItems;
            return;
        }

        BuildingLevel level = Definition.UpgradeTo(next);
        resources = level != null ? level.essence : (IReadOnlyList<ResourceAmount>)Array.Empty<ResourceAmount>();
        items = level != null ? level.cost : new List<ItemAmount>();
    }

    /// <summary>A station's window is where the player sees what a higher tier would make, so the way up starts there too.</summary>
    public void AddActions(MenuPage page, GameObject user)
    {
        if (CanUpgrade && user != null && user.TryGetComponent(out BuildPlacer builder) && builder.AllowsChanges)
        {
            page.Add(new MenuAction($"Upgrade to Tier {Tiers.ToRoman(Level + 1)}…", "What the next tier costs and brings", true,
                () => ConstructionUpgradeOffer.Show(GetComponent<BuildingObject>(), user)));
        }
    }

    /// <summary>Pays for the next level and puts the scaffold up. False (with a message) when it can't.</summary>
    public bool TryOrderUpgrade(GameObject payer)
    {
        // A friend's Haven: their buildings go up a level with their yes.
        if (HavenVisit.BuildingNeedsAsking())
        {
            return false;
        }

        if (!CanUpgrade)
        {
            GameMessages.Post(IsBuilding ? "It is already being worked on." : $"The {Title} can't be improved further.", MessageKind.Warning);
            return false;
        }

        BuildingLevel wanted = Definition.UpgradeTo(Level + 1);
        if (!BuildingUnlocks.IsOpen(wanted))
        {
            GameMessages.Post(BuildingUnlocks.Missing(wanted), MessageKind.Warning);
            return false;
        }

        Cost(out IReadOnlyList<ResourceAmount> resources, out IReadOnlyList<ItemAmount> items);
        if (!MaterialPool.For(payer).TrySpend(resources, items))
        {
            MaterialPool.For(payer).CanAfford(resources, items, out string missing);
            GameMessages.Post($"Not enough for the upgrade: {missing}", MessageKind.Warning);
            return false;
        }

        Target = Level + 1;
        Work = 0f;
        Paid = true;
        Refresh();
        GameMessages.Post($"The scaffold is up. Hold {{key:Hammer}} at the {Definition.DisplayName} to build.");
        return true;
    }

    /// <summary>Whether the player can work now; a ruin not paid for yet needs its cost at hand.</summary>
    public bool CanWork(GameObject user, out string reason)
    {
        reason = null;
        if (!IsBuilding)
        {
            reason = "Nothing to build here.";
            return false;
        }

        // A friend's scaffold is theirs to raise unless they said yes to building (as placing, moving and upgrading are): a guest
        // finished it unasked, and the host's game never heard it was done (homes, yards and Build objectives stayed behind).
        if (Player.IsPlayer(user) && HavenVisit.IsVisiting && !HavenVisit.MayBuild)
        {
            reason = HavenVisit.Refusal;
            return false;
        }

        if (Paid)
        {
            return true;
        }

        Cost(out IReadOnlyList<ResourceAmount> resources, out IReadOnlyList<ItemAmount> items);
        if (!MaterialPool.For(user).CanAfford(resources, items, out string missing))
        {
            reason = $"Repairing it takes {AmountText.Describe(items)}{(resources.Count > 0 ? ", " + AmountText.Describe(resources) : "")}. Missing {missing}";
            return false;
        }

        return true;
    }

    // ----- Unity -----

    private void Awake()
    {
        building = GetComponent<BuildingObject>();
        health = GetComponent<Damageable>();
        if (health != null)
        {
            health.HealthChanged += OnHealthChanged;
        }

        look = new ConstructionLook(this, building);

        if (startsRuined)
        {
            Level = 0;
            Target = 1;
            Paid = false;
        }

        CreateSite();
        look.BuildSite();
    }

    // How fast whoever is hammering works: 1 is the building's Build Work in seconds (OnSiteStarted).
    private float workSpeed = 1f;

    private void Start()
    {
        built = true;
        Refresh();
    }

    private void Update()
    {
        look.Tick();

        if (siteInteractable == null || !siteInteractable.IsInUse)
        {
            return;
        }

        Work = Mathf.Min(WorkNeeded, Work + Time.deltaTime * workSpeed);
        ShowRise();
        if (Time.time >= nextBlow)
        {
            nextBlow = Time.time + ConstructionSettings.Current.BlowInterval;
            look.HammerBlow();
        }
    }

    // ----- Work -----

    private void OnSiteStarted()
    {
        if (!Paid)
        {
            Cost(out IReadOnlyList<ResourceAmount> resources, out IReadOnlyList<ItemAmount> items);
            if (!MaterialPool.For(siteInteractable.Interactor).TrySpend(resources, items))
            {
                siteInteractable.Complete();
                return;
            }

            Paid = true;
            GameMessages.Post($"Repairing the {Definition.DisplayName}.");
        }

        // The Construction skill: a beginner hammers slower, a master faster.
        workSpeed = siteInteractable.Interactor != null && siteInteractable.Interactor.TryGetComponent(out Combatant builder)
            ? Mathf.Max(0.25f, 1f + builder.Stats.Get(StatType.BuildSpeed))
            : 1f;
        if (Player.IsPlayer(siteInteractable.Interactor) && Techniques.Has(PerkTechnique.RaiseInADay))
        {
            workSpeed *= TechniqueSettings.Current.RaiseSpeed;
            Techniques.Used(PerkTechnique.RaiseInADay);
        }
        siteInteractable.Duration = Mathf.Max(0.1f, (WorkNeeded - Work) / workSpeed);
        nextBlow = Time.time + 0.15f;
    }

    // Completed (not cancelled) and paid for means the hammering ran its time, which was set to the work left (OnSiteStarted);
    // the only early Complete is an unpaid start. Work counted in this Update can lag the interaction's own clock by a frame (no
    // set order between them), and Progress reads 0 here (End resets it first), so checking Work let a fast builder's last
    // frame end the hammering with nothing standing.
    private void OnSiteCompleted()
    {
        if (!Paid)
        {
            return;
        }

        Finish(siteInteractable.Interactor);
    }

    /// <summary>
    /// Work done on it by someone else (the Carpenter and Mason while the player raids: ResidentBuilders): adds seconds of
    /// hammering, finishing it when it is done. Returns the seconds it used. Nothing for a ruin not yet paid for.
    /// </summary>
    public float AddWork(float seconds)
    {
        if (!IsBuilding || !Paid || seconds <= 0f)
        {
            return 0f;
        }

        float used = Mathf.Min(seconds, WorkNeeded - Work);
        Work += used;
        if (Work + 0.01f >= WorkNeeded)
        {
            FinishedByOthers = true;
            try
            {
                Finish();
            }
            finally
            {
                FinishedByOthers = false;
            }
        }
        else
        {
            ShowRise();
        }

        return used;
    }

    /// <summary>True while Finished is raised for work others did (ResidentBuilders): it trains nobody's Construction, as
    /// workers' orders train nobody's crafts. The player got full Construction practice for buildings finished while away.</summary>
    public bool FinishedByOthers { get; private set; }

    /// <summary>Ends the work at once (tests, editor tools, and when the hammering is done).</summary>
    public void Finish(GameObject by = null)
    {
        if (!IsBuilding)
        {
            return;
        }

        bool first = Level == 0;
        Level = Target;
        Work = 0f;
        Refresh();
        if (!Application.isPlaying)
        {
            Finished?.Invoke(this);
            return;
        }

        look.Celebrate();
        GameMessages.Post(first ? $"The {Definition.DisplayName} stands!" : $"The {Title} is finished!", MessageKind.Gain);
        Teach(by, first ? Definition.TeachesRecipes : Definition.UpgradeTo(Level)?.teachesRecipes);
        GameObject builder = by != null ? by : Player.Current != null ? Player.Current.gameObject : null;
        GameEvents.Raise(first ? new GameEvent(GameEventKind.Built, Definition, 1, builder) : new GameEvent(GameEventKind.Upgraded, Definition, Level, builder));

        Finished?.Invoke(this);
    }

    private static void Teach(GameObject by, IEnumerable<RecipeDefinition> recipes)
    {
        GameObject learner = by != null ? by : Player.Current != null ? Player.Current.gameObject : null;
        if (recipes != null && learner != null && learner.TryGetComponent(out PlayerCrafting crafting))
        {
            crafting.Unlock(recipes);
        }
    }

    // ----- Looks -----

    private void OnDestroy()
    {
        if (health != null)
        {
            health.HealthChanged -= OnHealthChanged;
        }
    }

    private void OnHealthChanged(float current, float maximum)
    {
        bool broken = health.IsBroken;
        if (broken != shownBroken)
        {
            shownBroken = broken;
            Refresh();
        }
    }

    private void Refresh()
    {
        if (building == null)
        {
            return;
        }

        bool rising = IsBuilding && Level == 0;
        look.ShowScaffold(IsBuilding);

        if (siteInteractable != null)
        {
            siteInteractable.gameObject.SetActive(IsBuilding);
        }

        foreach (Interactable own in GetComponents<Interactable>())
        {
            own.enabled = !IsBuilding;
        }

        if (TryGetComponent(out CraftingStation station))
        {
            station.Usable = IsStanding;
        }

        foreach (VisibilitySource light in GetComponentsInChildren<VisibilitySource>(true))
        {
            light.ExtraRadius = Sum(level => level.extraLight);
        }

        ShowRise();
        look.Shade(rising ? Mathf.Lerp(ConstructionSettings.Current.RuinShade, 1f, Share) : shownBroken ? ConstructionSettings.Current.RuinShade : 1f);
        // A broken wall stops no one: the way through it opens.
        if (Definition.PlaceInLines && TryGetComponent(out UnityEngine.AI.NavMeshObstacle wall))
        {
            wall.enabled = !shownBroken;
        }

        look.ShowLevels(Level, Definition.MaxLevel, built);
    }

    private void ShowRise() => look.ShowRise(IsBuilding && Level == 0, Share, startsRuined);

    private void CreateSite()
    {
        Bounds bounds = building.LocalBounds;
        var site = new GameObject(Definition != null ? Definition.DisplayName : "Construction Site");
        site.transform.SetParent(transform, false);
        var trigger = site.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = bounds.center;
        trigger.size = bounds.size + new Vector3(0.6f, 0f, 0.6f);
        site.AddComponent<ConstructionSite>().Owner = this;
        siteInteractable = site.AddComponent<Interactable>();
        // Reach from the edge of the site's box (ConstructionSite is its interaction shape).
        siteInteractable.Configure(InteractionAnimation.Hammer, 1f, 2.2f, false);
        siteInteractable.Started.AddListener(OnSiteStarted);
        siteInteractable.Completed.AddListener(OnSiteCompleted);
        site.SetActive(false);
    }

    // ----- Saving -----

    public string CaptureState() =>
        string.Join(",", Level, Target, Work.ToString(System.Globalization.CultureInfo.InvariantCulture), Paid ? 1 : 0);

    public void RestoreState(string state)
    {
        string[] parts = string.IsNullOrEmpty(state) ? Array.Empty<string>() : state.Split(',');
        if (parts.Length < 4)
        {
            return;
        }

        Level = int.Parse(parts[0]);
        Target = Mathf.Max(Level, int.Parse(parts[1]));
        Work = float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture);
        Paid = parts[3] == "1";
        Refresh();
    }
}
