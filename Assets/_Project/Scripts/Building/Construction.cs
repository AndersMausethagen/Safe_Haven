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
    // Modules fitted from the Upgrade page (BuildingModules), in the order fitted; saved with the level.
    private readonly List<BuildingModuleDefinition> fitted = new List<BuildingModuleDefinition>();

    /// <summary>The modules fitted to this building (BuildingModules fits and removes them).</summary>
    public IReadOnlyList<BuildingModuleDefinition> Fitted => fitted;

    /// <summary>Raised when a module is fitted to or removed from any building.</summary>
    public static event Action<Construction> ModulesChanged;

    /// <summary>Fits <paramref name="module"/> (BuildingModules pays and checks). False when it is fitted already.</summary>
    internal bool AddModule(BuildingModuleDefinition module)
    {
        if (module == null || fitted.Contains(module))
        {
            return false;
        }

        fitted.Add(module);
        StationExtensions.Forget();
        Refresh();
        ModulesChanged?.Invoke(this);
        return true;
    }

    /// <summary>Puts the scaffold up to fit <paramref name="module"/> (BuildingModules pays and checks). False with a job under way already.</summary>
    internal bool BeginFitting(BuildingModuleDefinition module)
    {
        if (module == null || IsBuilding || IsFitting || fitted.Contains(module))
        {
            return false;
        }

        fitting = module;
        Work = 0f;
        Refresh();
        ModulesChanged?.Invoke(this);
        return true;
    }

    // The hammering is done: the module is fitted and counts from now (a Fit Module objective counts it here).
    private void FinishFitting(GameObject by)
    {
        BuildingModuleDefinition module = fitting;
        fitting = null;
        Work = 0f;
        AddModule(module);
        if (!Application.isPlaying)
        {
            return;
        }

        look.Celebrate();
        GameMessages.Post($"The {module.DisplayName} is fitted to the {Definition.DisplayName}: {module.Effect()}.", MessageKind.Gain);
        GameObject builder = by != null ? by : Player.Current != null ? Player.Current.gameObject : null;
        GameEvents.Raise(new GameEvent(GameEventKind.ModuleFitted, module, 1, builder, detail: Definition));
    }

    /// <summary>Takes <paramref name="module"/> off. False when it wasn't fitted.</summary>
    internal bool RemoveModule(BuildingModuleDefinition module)
    {
        if (!fitted.Remove(module))
        {
            return false;
        }

        StationExtensions.Forget();
        Refresh();
        ModulesChanged?.Invoke(this);
        return true;
    }

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
    /// The module being fitted (paid for or taken from store, its scaffold up), or null. Fitting is hammered like a tier (the research's
    /// decision 2): the module counts once the hammering is done; the station keeps working meanwhile. One job at a time: a tier or a module.
    /// </summary>
    public BuildingModuleDefinition Fitting => fitting;
    public bool IsFitting => fitting != null;
    private BuildingModuleDefinition fitting;

    /// <summary>
    /// Whether a defence (a trap, a ward totem, a watch post) does its work: it stands (not a scaffold or a ruin) and isn't broken
    /// down; being upgraded it goes on. The one rule for all three (review 2026-11-16: each had its own, and a broken trap still bit).
    /// </summary>
    public static bool DefenceWorks(Construction construction, Damageable health) =>
        (construction == null || construction.IsStanding) && (health == null || !health.IsBroken);
    public bool CanUpgrade => IsStanding && !IsBuilding && !IsFitting && Level < Definition.MaxLevel;
    public float WorkNeeded => !IsBuilding && IsFitting ? fitting.Work
        : Target <= 1 ? (Definition.BuildWork > 0f ? Definition.BuildWork : ConstructionSettings.Current.RepairWork)
        : Definition.UpgradeTo(Target)?.work ?? 0f;
    public float Share => WorkNeeded > 0f ? Mathf.Clamp01(Work / WorkNeeded) : 1f;

    public string SaveKey => "construction";

    bool ISharedState.IsShared => true;

    int ISharedState.SharedMark => unchecked(((((Level * 31 + Target) * 31 + Mathf.FloorToInt(Work)) * 2 + (Paid ? 1 : 0)) * 37 + ModuleMark()) * 41
        + (fitting != null ? fitting.Id.GetHashCode() : 0));

    private int ModuleMark()
    {
        int mark = fitted.Count;
        foreach (BuildingModuleDefinition module in fitted)
        {
            mark = unchecked(mark * 31 + (module != null ? module.Id.GetHashCode() : 0));
        }

        return mark;
    }

    /// <summary>Adds the modules counting now to <paramref name="into"/>: fitted, standing, a Staffed one only while someone works here.</summary>
    public void AddCounting(List<BuildingModuleDefinition> into)
    {
        if (fitted.Count == 0 || !IsStanding)
        {
            return;
        }

        bool staffed = IsStaffed;
        foreach (BuildingModuleDefinition module in fitted)
        {
            if (module != null && (!module.Staffed || staffed))
            {
                into.Add(module);
            }
        }
    }

    /// <summary>
    /// True while someone works here: a station's worker, or for a building that isn't a station (the Purification Altar) a resident
    /// whose job's workplace is this kind of building. A Staffed module counts only then.
    /// </summary>
    public bool IsStaffed
    {
        get
        {
            if (TryGetComponent(out CraftingStation station))
            {
                return station.HasWorker;
            }

            foreach (ResidentRecord record in GameSession.Here.Residents)
            {
                if (record.Job != null && record.Job.Workplace == Definition && !record.WorkPaused)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>The name with its level's title: "Sturdy Sawbench".</summary>
    public string Title => Level >= 2 && Definition.UpgradeTo(Level) != null ? $"{Definition.UpgradeTo(Level).title} {Definition.DisplayName}" : Definition.DisplayName;

    /// <summary>A bonus added up over every level reached (2 up to Level) and every module fitted (a Staffed one only while someone
    /// works here). A scaffold or a ruin gets nothing from its modules.</summary>
    public float Sum(Func<BuildingBonus, float> bonus)
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

        if (fitted.Count > 0 && IsStanding)
        {
            bool staffed = IsStaffed;
            foreach (BuildingModuleDefinition module in fitted)
            {
                if (module != null && (!module.Staffed || staffed))
                {
                    total += bonus(module.Bonus);
                }
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
        IsPlan = false;
        Refresh();
    }

    /// <summary>
    /// A plan (the Keeper's view, Docs/ColonyModeResearch_2026-11-21.md §4.5): the scaffold laid out before its materials are home. It
    /// costs nothing yet; its materials are wanted at home (WantedAtHome), it is paid from the chests the moment they hold it all
    /// (BuildPlans), or when the player starts hammering it with what they carry, and then built like any scaffold.
    /// </summary>
    public void BeginPlan()
    {
        Level = 0;
        Target = 1;
        Work = 0f;
        Paid = false;
        IsPlan = true;
        Refresh();
    }

    /// <summary>True for a plan whose materials aren't paid yet (BeginPlan).</summary>
    public bool IsPlan { get; private set; }

    /// <summary>Pays a plan from <paramref name="pool"/> when it holds the whole cost; true when it is paid now.</summary>
    public bool TryPayPlan(MaterialPool pool)
    {
        if (!IsPlan || Paid || pool == null)
        {
            return false;
        }

        Cost(out IReadOnlyList<ResourceAmount> resources, out IReadOnlyList<ItemAmount> items);
        if (!pool.TrySpend(resources, items))
        {
            return false;
        }

        Paid = true;
        IsPlan = false;
        Refresh();
        return true;
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
        bool modules = IsStanding && !IsBuilding && BuildingModules.TakesModules(Definition);
        if ((CanUpgrade || modules) && user != null && user.TryGetComponent(out BuildPlacer builder) && builder.AllowsChanges)
        {
            string label = CanUpgrade ? $"Upgrade to Tier {Tiers.ToRoman(Level + 1)}…" : "Upgrade: modules…";
            string detail = CanUpgrade ? (modules ? "The next tier, and the modules it can take" : "What the next tier costs and brings") : "Fit or remove modules";
            page.Add(new MenuAction(label, detail, true, () => ConstructionUpgradeOffer.Show(GetComponent<BuildingObject>(), user)));
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
            GameMessages.Post(IsBuilding ? "It is already being worked on."
                : IsFitting ? $"Finish fitting the {fitting.DisplayName} first: hold {{key:Hammer}} at the {Definition.DisplayName}."
                : $"The {Title} can't be improved further.", MessageKind.Warning);
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
        if (!IsBuilding && !IsFitting)
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
            health.Damaged += OnDamaged;
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

    // Work added by builders this frame, shown in Update (ShowRise moves every part and tints every renderer).
    private bool riseDirty;

    private void Update()
    {
        look.Tick();
        if (riseDirty)
        {
            riseDirty = false;
            ShowRise();
        }

        if (siteInteractable == null || !siteInteractable.IsInUse)
        {
            return;
        }

        Work = Mathf.Min(WorkNeeded, Work + Time.deltaTime * workSpeed);
        // Builders hammering beside the player (ResidentLife: the Carpenter and Mason at home) add their own work (AddWork): the
        // player's bar is what is left at their own pace, so it shortens as the others help, as work adds up in Palworld.
        siteInteractable.Duration = siteInteractable.Progress * siteInteractable.Duration + Mathf.Max(0f, WorkNeeded - Work) / workSpeed;
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
            GameMessages.Post(IsPlan ? $"Building the {Definition.DisplayName}." : $"Repairing the {Definition.DisplayName}.");
            IsPlan = false;
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
        if ((!IsBuilding && !IsFitting) || !Paid || seconds <= 0f)
        {
            return 0f;
        }

        // The player hammering it finishes it themselves (their Construction practice, their last blow): helpers bring the work to its
        // end, never past it, and the player's shortened bar (Update) completes it the next frame.
        bool playerHammers = siteInteractable != null && siteInteractable.IsInUse && Player.IsPlayer(siteInteractable.Interactor);
        float used = Mathf.Min(seconds, WorkNeeded - Work);
        Work += used;
        if (!playerHammers && Work + 0.01f >= WorkNeeded)
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
            // Shown once a frame however many hands are at it (Update), not once a builder.
            riseDirty = true;
            // A builder's blows are heard and seen too, not only the player's.
            if (Application.isPlaying && Time.time >= nextBlow)
            {
                nextBlow = Time.time + ConstructionSettings.Current.BlowInterval;
                look.HammerBlow();
            }
        }

        return used;
    }

    /// <summary>Seconds of hammering still needed to finish what is under way (0 when nothing is).</summary>
    public float WorkLeft => IsBuilding || IsFitting ? Mathf.Max(0f, WorkNeeded - Work) : 0f;

    /// <summary>True while Finished is raised for work others did (ResidentBuilders): it trains nobody's Construction, as
    /// workers' orders train nobody's crafts. The player got full Construction practice for buildings finished while away.</summary>
    public bool FinishedByOthers { get; private set; }

    /// <summary>Ends the work at once (tests, editor tools, and when the hammering is done).</summary>
    public void Finish(GameObject by = null)
    {
        if (!IsBuilding)
        {
            if (IsFitting)
            {
                FinishFitting(by);
            }

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
        if (!first)
        {
            // Before the Haven notes this tier (HavenProgress), so a module known already (another of the kind at this tier, a spare,
            // a quest) isn't announced again.
            BuildingModules.AnnounceLearned(Definition.UpgradeTo(Level)?.unlocksModules, null);
        }
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
            health.Damaged -= OnDamaged;
        }
    }

    private static bool turningBack;

    // Entering Play doesn't reload scripts in this project, so statics are cleared when it starts.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetTurningBack() => turningBack = false;

    // Hag's Knot (turnBackShare): a share of the harm comes back on whoever struck, never on the player's side. A blow turned back is
    // never turned again.
    private void OnDamaged(DamageInfo hit, DamageResult result)
    {
        if (turningBack || hit.Source == null || result.HealthLost <= 0f || !IsStanding)
        {
            return;
        }

        float share = Sum(bonus => bonus.turnBackShare);
        if (share <= 0f || !hit.Source.TryGetComponent(out Damageable striker) || !striker.IsAlive
            || FactionRelations.SideOf(hit.Source.gameObject)?.IsPlayerSide() == true)
        {
            return;
        }

        Vector3 at = striker.transform.position + Vector3.up;
        float back = result.HealthLost * share;
        try
        {
            turningBack = true;
            striker.ApplyDamage(new DamageInfo(GetComponent<Combatant>(), null, back, DamageType.Physical, false, at, at - transform.position, 0f, HitTechnique.None));
            ModuleCues.Fire(this, "A blow turned back on the striker", "Skull");
        }
        finally
        {
            turningBack = false;
        }
    }

    private float appliedHealthShare;

    // Iron Bands (healthShare): more health while standing, kept as a share as it changes (Damageable keeps health's share of the most).
    // Changed only when the share does: taking it off and putting it back on every Refresh moved health each time.
    private void ApplyHealthShare()
    {
        float share = Sum(bonus => bonus.healthShare);
        if (Mathf.Approximately(share, appliedHealthShare) || health == null || !TryGetComponent(out Combatant fighter) || fighter.Stats == null)
        {
            return;
        }

        appliedHealthShare = share;
        fighter.Stats.RemoveModifiersFrom(this);
        if (share > 0f)
        {
            fighter.Stats.AddModifier(new StatModifier(StatType.MaxHealth, StatModifierOp.Percent, share, this));
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
        look.ShowScaffold(IsBuilding || IsFitting);

        if (siteInteractable != null)
        {
            siteInteractable.gameObject.SetActive(IsBuilding || IsFitting);
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

        ApplyHealthShare();
        ModuleDressing.Show(this);

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

    // "Level,Target,Work,Paid" and, with modules fitted, ",<their ids joined by |>", and with one being fitted, ",<its id>" after that (the
    // fitted part may be empty): a building without modules saves as it always did.
    public string CaptureState()
    {
        // Paid: 1, a plan waiting for its materials: 2 (older saves read either as before).
        string state = string.Join(",", Level, Target, Work.ToString(System.Globalization.CultureInfo.InvariantCulture), Paid ? 1 : IsPlan ? 2 : 0);
        string ids = string.Join("|", fitted.ConvertAll(module => module != null ? module.Id : ""));
        return fitting != null ? state + "," + ids + "," + fitting.Id : fitted.Count == 0 ? state : state + "," + ids;
    }

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
        IsPlan = parts[3] == "2";
        fitted.Clear();
        fitting = parts.Length >= 6 && !string.IsNullOrEmpty(parts[5]) ? Resources.Load<ContentDatabase>(ContentDatabase.ResourceName)?.FindModule(parts[5]) : null;
        if (parts.Length >= 5 && !string.IsNullOrEmpty(parts[4]))
        {
            ContentDatabase content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
            foreach (string id in parts[4].Split('|'))
            {
                BuildingModuleDefinition module = content != null ? content.FindModule(id) : null;
                if (module != null && !fitted.Contains(module))
                {
                    fitted.Add(module);
                }
            }

            StationExtensions.Forget();
        }

        Refresh();
    }
}
