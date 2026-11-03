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
/// Built game event (quests). Looks and sounds: ConstructionSettings. Lives on every building prefab.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BuildingObject))]
public sealed class Construction : MonoBehaviour, ISaveable, IStationMenuExtension
{
    private const string Dressing = "Dressing Level ";
    private const string TierPart = "Tier ";

    [Tooltip("A ruin waiting to be repaired: dark, sunken and tilted until the player pays its cost and hammers it up.")]
    [SerializeField] private bool startsRuined;

    private readonly List<(Transform part, Vector3 position, Quaternion rotation)> model = new List<(Transform, Vector3, Quaternion)>();
    private readonly List<Renderer> modelRenderers = new List<Renderer>();
    private readonly Dictionary<int, Transform> tierParts = new Dictionary<int, Transform>();
    private BuildingObject building;
    private Damageable health;
    private bool shownBroken;
    private Interactable siteInteractable;
    private Transform scaffold;
    private ParticleSystem dust;
    private MaterialPropertyBlock shade;
    private bool built;
    private float nextBlow;
    private float popUntil = -1f;

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
    public bool CanUpgrade => IsStanding && !IsBuilding && Level < Definition.MaxLevel;
    public float WorkNeeded => Target <= 1 ? (Definition.BuildWork > 0f ? Definition.BuildWork : ConstructionSettings.Current.RepairWork)
        : Definition.UpgradeTo(Target)?.work ?? 0f;
    public float Share => WorkNeeded > 0f ? Mathf.Clamp01(Work / WorkNeeded) : 1f;

    public string SaveKey => "construction";

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
                () => builder.ShowUpgrade(GetComponent<BuildingObject>())));
        }
    }

    /// <summary>Pays for the next level and puts the scaffold up. False (with a message) when it can't.</summary>
    public bool TryOrderUpgrade(GameObject payer)
    {
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
        GameMessages.Post($"The scaffold is up. Hold E at the {Definition.DisplayName} to build.");
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

        shade = new MaterialPropertyBlock();
        foreach (Transform child in transform)
        {
            model.Add((child, child.localPosition, child.localRotation));
        }

        foreach (Transform part in GetComponentsInChildren<Transform>(true))
        {
            if (part.name.StartsWith(TierPart) && int.TryParse(part.name.Substring(TierPart.Length), out int tier))
            {
                tierParts[tier] = part;
            }
        }

        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (!(renderer is ParticleSystemRenderer))
            {
                modelRenderers.Add(renderer);
            }
        }

        if (startsRuined)
        {
            Level = 0;
            Target = 1;
            Paid = false;
        }

        CreateSite();
        dust = CreateDust(transform);
    }

    private void Start()
    {
        built = true;
        Refresh();
    }

    private void Update()
    {
        if (popUntil >= 0f)
        {
            float t = 1f - Mathf.Clamp01((popUntil - Time.time) / 0.45f);
            float pop = ConstructionSettings.Current.FinishPop;
            transform.localScale = Vector3.one * Mathf.LerpUnclamped(pop, 1f, EaseOutBack(t));
            if (t >= 1f)
            {
                popUntil = -1f;
                transform.localScale = Vector3.one;
            }
        }

        if (siteInteractable == null || !siteInteractable.IsInUse)
        {
            return;
        }

        Work = Mathf.Min(WorkNeeded, Work + Time.deltaTime);
        ShowRise();
        if (Time.time >= nextBlow)
        {
            nextBlow = Time.time + ConstructionSettings.Current.BlowInterval;
            Blow();
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

        siteInteractable.Duration = Mathf.Max(0.1f, WorkNeeded - Work);
        nextBlow = Time.time + 0.15f;
    }

    private void OnSiteCompleted()
    {
        if (!Paid || Work + 0.05f < WorkNeeded && siteInteractable.Progress < 1f)
        {
            return;
        }

        Finish(siteInteractable.Interactor);
    }

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

        popUntil = Time.time + 0.45f;
        Burst(transform.position + Vector3.up * 0.3f, 40, BoundsRadius);
        CameraShake.Add(ConstructionSettings.Current.FinishShake);
        AudioManager.Play(ConstructionSettings.Current.FinishSound, transform.position);
        GameMessages.Post(first ? $"The {Definition.DisplayName} stands!" : $"The {Title} is finished!", MessageKind.Gain);
        Teach(by, first ? Definition.TeachesRecipes : Definition.UpgradeTo(Level)?.teachesRecipes);
        if (first)
        {
            GameEvents.Raise(new GameEvent(GameEventKind.Built, Definition, 1, by != null ? by : Player.Current != null ? Player.Current.gameObject : null));
        }

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

    private void Blow()
    {
        ConstructionSettings settings = ConstructionSettings.Current;
        AudioManager.Play(settings.HammerSound, transform.position);
        CameraShake.Add(settings.BlowShake);
        Bounds bounds = building.LocalBounds;
        Vector3 local = new Vector3(UnityEngine.Random.Range(bounds.min.x, bounds.max.x), UnityEngine.Random.Range(0.2f, Mathf.Max(0.4f, bounds.max.y * 0.6f)),
            UnityEngine.Random.value < 0.5f ? bounds.min.z : bounds.max.z);
        Burst(transform.TransformPoint(local), 10, 0.3f);
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
        if (scaffold != null)
        {
            scaffold.gameObject.SetActive(IsBuilding);
        }

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
        Shade(rising ? Mathf.Lerp(ConstructionSettings.Current.RuinShade, 1f, Share) : shownBroken ? ConstructionSettings.Current.RuinShade : 1f);
        // A broken wall stops no one: the way through it opens.
        if (Definition.PlaceInLines && TryGetComponent(out UnityEngine.AI.NavMeshObstacle wall))
        {
            wall.enabled = !shownBroken;
        }

        for (int level = 2; level <= Mathf.Max(Definition.MaxLevel, 3); level++)
        {
            // A model made for its tiers shows them itself: the parts of each tier are switched on as it is reached.
            if (tierParts.TryGetValue(level, out Transform authored))
            {
                authored.gameObject.SetActive(Level >= level);
                continue;
            }

            Transform dressing = transform.Find(Dressing + level);
            if (Level >= level && dressing == null && built)
            {
                dressing = BuildDressing(level);
            }

            if (dressing != null)
            {
                dressing.gameObject.SetActive(Level >= level);
            }
        }
    }

    // A building going up rises out of the ground; a ruin also leans until it is repaired.
    private void ShowRise()
    {
        bool rising = IsBuilding && Level == 0;
        float height = Mathf.Max(0.5f, building.LocalBounds.size.y);
        float sink = rising ? (1f - Share) * height * (startsRuined ? 0.4f : 0.85f) : 0f;
        Quaternion lean = rising && startsRuined ? Quaternion.Euler(Mathf.Lerp(7f, 0f, Share), 0f, Mathf.Lerp(-4f, 0f, Share)) : Quaternion.identity;
        foreach ((Transform part, Vector3 position, Quaternion rotation) in model)
        {
            if (part != null)
            {
                part.localPosition = position + Vector3.down * sink;
                part.localRotation = lean * rotation;
            }
        }

        if (rising)
        {
            Shade(Mathf.Lerp(ConstructionSettings.Current.RuinShade, 1f, Share));
        }
    }

    private void Shade(float brightness)
    {
        foreach (Renderer renderer in modelRenderers)
        {
            if (renderer == null || renderer.sharedMaterial == null)
            {
                continue;
            }

            if (brightness >= 0.999f)
            {
                renderer.SetPropertyBlock(null);
                continue;
            }

            Material material = renderer.sharedMaterial;
            Color colour = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : Color.white;
            shade.Clear();
            shade.SetColor("_BaseColor", new Color(colour.r * brightness, colour.g * brightness, colour.b * brightness, colour.a));
            renderer.SetPropertyBlock(shade);
        }
    }

    private float BoundsRadius => Mathf.Max(building.LocalBounds.extents.x, building.LocalBounds.extents.z) + 0.5f;

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
        siteInteractable.Configure(InteractionAnimation.Hammer, 1f, 1.6f, false);
        siteInteractable.Started.AddListener(OnSiteStarted);
        siteInteractable.Completed.AddListener(OnSiteCompleted);
        site.SetActive(false);
        scaffold = BuildScaffold(bounds);
        scaffold.gameObject.SetActive(false);
    }

    // Posts at the corners, two rings of beams, a brace and a plank walk at the front.
    private Transform BuildScaffold(Bounds bounds)
    {
        Material wood = ConstructionSettings.Current.ScaffoldMaterial;
        var root = new GameObject("Scaffold").transform;
        root.SetParent(transform, false);
        float height = bounds.size.y + 0.4f;
        Vector3 min = bounds.min - new Vector3(0.35f, 0f, 0.35f);
        Vector3 max = bounds.max + new Vector3(0.35f, 0f, 0.35f);
        for (int corner = 0; corner < 4; corner++)
        {
            float x = (corner & 1) == 0 ? min.x : max.x;
            float z = (corner & 2) == 0 ? min.z : max.z;
            Piece(root, "Post", new Vector3(x, height * 0.5f, z), new Vector3(0.12f, height, 0.12f), Quaternion.identity, wood);
        }

        foreach (float y in new[] { height * 0.45f, height * 0.92f })
        {
            Piece(root, "Beam", new Vector3((min.x + max.x) * 0.5f, y, min.z), new Vector3(max.x - min.x, 0.09f, 0.09f), Quaternion.identity, wood);
            Piece(root, "Beam", new Vector3((min.x + max.x) * 0.5f, y, max.z), new Vector3(max.x - min.x, 0.09f, 0.09f), Quaternion.identity, wood);
            Piece(root, "Beam", new Vector3(min.x, y, (min.z + max.z) * 0.5f), new Vector3(0.09f, 0.09f, max.z - min.z), Quaternion.identity, wood);
            Piece(root, "Beam", new Vector3(max.x, y, (min.z + max.z) * 0.5f), new Vector3(0.09f, 0.09f, max.z - min.z), Quaternion.identity, wood);
        }

        float span = max.x - min.x;
        float angle = Mathf.Atan2(height * 0.45f, span) * Mathf.Rad2Deg;
        Piece(root, "Brace", new Vector3((min.x + max.x) * 0.5f, height * 0.23f, max.z), new Vector3(Mathf.Sqrt(span * span + height * height * 0.2f), 0.07f, 0.07f),
            Quaternion.Euler(0f, 0f, angle), wood);
        Piece(root, "Planks", new Vector3((min.x + max.x) * 0.5f, height * 0.45f + 0.05f, max.z + 0.2f), new Vector3(span, 0.05f, 0.45f), Quaternion.identity, wood);
        return root;
    }

    // Level 2: a stone plinth round the base and two lanterns at the front. Level 3: banners at the back and a flower box.
    private Transform BuildDressing(int level)
    {
        ConstructionSettings settings = ConstructionSettings.Current;
        Bounds bounds = building.LocalBounds;
        var root = new GameObject(Dressing + level).transform;
        root.SetParent(transform, false);
        var random = new System.Random(Definition.Id.GetHashCode() * 31 + level);
        float Range(float a, float b) => a + (float)random.NextDouble() * (b - a);
        Vector3 min = bounds.min - new Vector3(0.25f, 0f, 0.25f);
        Vector3 max = bounds.max + new Vector3(0.25f, 0f, 0.25f);

        if (level == 2)
        {
            float perimeter = 2f * ((max.x - min.x) + (max.z - min.z));
            int stones = Mathf.Clamp(Mathf.RoundToInt(perimeter / 0.55f), 8, 40);
            for (int i = 0; i < stones; i++)
            {
                Vector3 at = Perimeter(min, max, i / (float)stones);
                float size = Range(0.32f, 0.48f);
                Piece(root, "Plinth Stone", new Vector3(at.x, size * 0.45f, at.z), new Vector3(size, size * 0.95f, size * Range(0.8f, 1.2f)),
                    Quaternion.Euler(Range(-6f, 6f), Range(0f, 90f), Range(-6f, 6f)), settings.StoneMaterial);
            }

            foreach (float x in new[] { min.x - 0.2f, max.x + 0.2f })
            {
                Lantern(root, new Vector3(x, 0f, max.z + 0.2f), settings);
            }
        }
        else
        {
            foreach (float x in new[] { min.x - 0.15f, max.x + 0.15f })
            {
                float pole = Mathf.Max(2.4f, bounds.size.y + 0.8f);
                Piece(root, "Banner Pole", new Vector3(x, pole * 0.5f, min.z - 0.15f), new Vector3(0.08f, pole, 0.08f), Quaternion.identity, settings.ScaffoldMaterial);
                Piece(root, "Banner", new Vector3(x + (x < 0f ? 0.32f : -0.32f), pole - 0.6f, min.z - 0.15f), new Vector3(0.55f, 0.95f, 0.03f), Quaternion.identity, settings.ClothMaterial);
            }

            float boxX = Range(min.x + 0.3f, max.x - 0.9f);
            Piece(root, "Flower Box", new Vector3(boxX + 0.3f, 0.15f, max.z + 0.35f), new Vector3(0.7f, 0.3f, 0.28f), Quaternion.identity, settings.ScaffoldMaterial);
            for (int i = 0; i < 5; i++)
            {
                Transform bloom = Piece(root, "Bloom", new Vector3(boxX + 0.06f + i * 0.12f, 0.36f, max.z + 0.35f + Range(-0.06f, 0.06f)),
                    Vector3.one * Range(0.1f, 0.15f), Quaternion.identity, settings.ClothMaterial, PrimitiveType.Sphere);
                bloom.GetComponent<Renderer>().SetPropertyBlock(Tint(Color.HSVToRGB(Range(0f, 1f) < 0.5f ? Range(0.95f, 1f) : Range(0.1f, 0.16f), 0.6f, 1f)));
            }
        }

        return root;
    }

    private static MaterialPropertyBlock Tint(Color colour)
    {
        var block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", colour);
        return block;
    }

    private static void Lantern(Transform parent, Vector3 at, ConstructionSettings settings)
    {
        Piece(parent, "Lantern Post", at + Vector3.up * 0.8f, new Vector3(0.09f, 1.6f, 0.09f), Quaternion.identity, settings.ScaffoldMaterial);
        Transform lamp = Piece(parent, "Lantern", at + Vector3.up * 1.62f, new Vector3(0.2f, 0.26f, 0.2f), Quaternion.identity, settings.LanternMaterial);
        var light = new GameObject("Lantern Light").AddComponent<Light>();
        light.transform.SetParent(lamp, false);
        light.type = LightType.Point;
        light.color = settings.LanternLight;
        light.intensity = 1.6f;
        light.range = 5f;
        light.shadows = LightShadows.None;
    }

    // A point on the rectangle's edge, 0..1 round it.
    private static Vector3 Perimeter(Vector3 min, Vector3 max, float t)
    {
        float w = max.x - min.x;
        float d = max.z - min.z;
        float along = t * 2f * (w + d);
        if (along < w) return new Vector3(min.x + along, 0f, min.z);
        along -= w;
        if (along < d) return new Vector3(max.x, 0f, min.z + along);
        along -= d;
        if (along < w) return new Vector3(max.x - along, 0f, max.z);
        along -= w;
        return new Vector3(min.x, 0f, max.z - along);
    }

    private static Transform Piece(Transform parent, string name, Vector3 position, Vector3 size, Quaternion rotation, Material material,
        PrimitiveType shape = PrimitiveType.Cube)
    {
        GameObject piece = GameObject.CreatePrimitive(shape);
        piece.name = name;
        // At once, so nothing that gathers colliders in Awake (stations, nodes) keeps a dead one.
        UnityEngine.Object.DestroyImmediate(piece.GetComponent<Collider>());
        piece.transform.SetParent(parent, false);
        piece.transform.localPosition = position;
        piece.transform.localRotation = rotation;
        piece.transform.localScale = size;
        if (material != null)
        {
            piece.GetComponent<Renderer>().sharedMaterial = material;
        }

        return piece.transform;
    }

    private static ParticleSystem CreateDust(Transform parent)
    {
        var go = new GameObject("Construction Dust");
        go.transform.SetParent(parent, false);
        ParticleSystem particles = go.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = particles.main;
        main.playOnAwake = false;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.55f, 0.48f, 0.38f, 0.7f), new Color(0.75f, 0.68f, 0.55f, 0.5f));
        main.gravityModifier = -0.05f;
        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        ParticleSystem.ColorOverLifetimeModule fade = particles.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) });
        fade.color = gradient;
        Material material = ConstructionSettings.Current.DustMaterial;
        if (material != null)
        {
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
        }

        return particles;
    }

    private void Burst(Vector3 at, int count, float radius)
    {
        if (dust == null)
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            Vector2 ring = UnityEngine.Random.insideUnitCircle.normalized * radius * UnityEngine.Random.Range(0.6f, 1f);
            var emit = new ParticleSystem.EmitParams
            {
                position = at + new Vector3(ring.x, UnityEngine.Random.Range(0f, 0.3f), ring.y),
                velocity = new Vector3(ring.x, 0.8f, ring.y).normalized * UnityEngine.Random.Range(0.6f, 1.8f),
                applyShapeToPosition = false,
            };
            dust.Emit(emit, 1);
        }
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
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
