using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What a slain creature carried, waiting on its body (BodyLootSettings, the user 2026-11-24): its items, its resources (hides, Cursed
/// Essence) and, on a raid, a relic or inscription it may hold. LootOnDeath rolls it as the creature dies, exactly as it used to roll
/// the bag's share, and leaves it here. The body glows faintly (an outline of copies, as the hover outline) and glitters while it holds
/// anything; Interact opens the loot window (LootWindow): take a stack, a resource, or all. A relic or inscription is found on opening,
/// with its own words and sound (RelicSettings.Grant, Inscriptions.Grant). Emptied, the body sinks as any corpse; left alone past
/// Lootable Seconds it fades with what it held (a LootLeft row in the playtest log). Its loot point follows the corpse until it lies
/// still (DeathRagdoll keeps a body with loot from sinking or being cleared by the corpse limit). A creature with no corpse (a Marsh
/// Light, one gone in the cap) leaves the glitter where it fell. Personal: each game in co-op rolls and keeps its own.
/// </summary>
[DisallowMultipleComponent]
public sealed class LootableRemains : MonoBehaviour, IInteractionVerb, IInteractionName
{
    private const int Columns = 5;
    private const float FollowEvery = 0.4f;
    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly List<LootableRemains> all = new List<LootableRemains>();
    private static Material glowMaterial;
    private static int glowFrame = -1;
    private static readonly int OutlinePixelsId = Shader.PropertyToID("_OutlinePixels");

    private readonly List<ResourceAmount> resources = new List<ResourceAmount>();
    private Damageable victim;
    private DeathRagdoll corpse;
    private int corpseLooks;
    private Interactable interactable;
    private SphereCollider point;
    private ParticleSystem sparkles;
    private List<OutlineCopies.Entry> glow;
    private float expiresAt;
    private float nextFollow;
    private bool relic;
    private int tier;
    private EnchantmentDefinition inscription;
    private bool opened;
    private bool finished;

    /// <summary>Every body with loot in the scene.</summary>
    public static IReadOnlyList<LootableRemains> All => all;

    /// <summary>The items it holds, as the window shows them.</summary>
    public ItemContainer Items { get; private set; }

    /// <summary>The resources it holds (hides, Cursed Essence).</summary>
    public IReadOnlyList<ResourceAmount> Resources => resources;

    /// <summary>The creature's name: the loot window's title and the prompt's.</summary>
    public string Title { get; private set; }

    public string Verb => "Loot";
    public string InteractionName => Title;

    /// <summary>True when nothing is left: no items, no resources, nothing still to be found.</summary>
    public bool IsEmpty => Items.CountItems() == 0 && resources.Count == 0 && !relic && inscription == null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        all.Clear();
        glowMaterial = null;
        glowFrame = -1;
    }

    /// <summary>Leaves loot on <paramref name="body"/>'s body: what it carried, and on a raid a relic (<paramref name="withRelic"/>, rolled at
    /// <paramref name="raidTier"/>) and an inscription. Null when there is nothing to leave.</summary>
    public static LootableRemains Leave(Damageable body, string title, IReadOnlyList<ItemAmount> items, IReadOnlyList<ResourceAmount> rolled,
        bool withRelic, int raidTier, EnchantmentDefinition withInscription)
    {
        int stacks = 0;
        foreach (ItemAmount item in items)
        {
            stacks += item.Item != null && item.Amount > 0 ? 1 : 0;
        }

        bool anyResource = false;
        foreach (ResourceAmount resource in rolled)
        {
            anyResource |= resource.Resource != null && resource.Amount > 0;
        }

        if (body == null || (stacks == 0 && !anyResource && !withRelic && withInscription == null))
        {
            return null;
        }

        var go = new GameObject($"Loot ({title})");
        go.transform.position = body.transform.position + Vector3.up * 0.4f;
        go.layer = LayerMask.NameToLayer("Ignore Raycast");
        var remains = go.AddComponent<LootableRemains>();
        remains.victim = body;
        remains.Title = title;
        remains.relic = withRelic;
        remains.tier = raidTier;
        remains.inscription = withInscription;
        // Filled into a roomy scratch grid first (an item can need several stacks), then kept in rows just enough for it.
        var scratch = new ItemContainer(Columns, Mathf.Max(2, stacks * 2 / Columns + 2));
        foreach (ItemAmount item in items)
        {
            if (item.Item != null && item.Amount > 0)
            {
                scratch.Add(item.Item, item.Amount);
            }
        }

        int used = 0;
        for (int i = 0; i < scratch.SlotCount; i++)
        {
            used += scratch.GetSlot(i).IsEmpty ? 0 : 1;
        }

        remains.Items = new ItemContainer(Columns, Mathf.Max(1, Mathf.CeilToInt(used / (float)Columns)));
        for (int i = 0; i < scratch.SlotCount; i++)
        {
            if (!scratch.GetSlot(i).IsEmpty)
            {
                remains.Items.AddStack(scratch.GetSlot(i));
            }
        }

        foreach (ResourceAmount resource in rolled)
        {
            if (resource.Resource != null && resource.Amount > 0)
            {
                remains.resources.Add(resource);
            }
        }

        remains.Build();
        return remains;
    }

    private void Build()
    {
        BodyLootSettings settings = BodyLootSettings.Current;
        expiresAt = Time.time + settings.LootableSeconds;
        point = gameObject.AddComponent<SphereCollider>();
        point.isTrigger = true;
        point.radius = 0.5f;
        interactable = gameObject.AddComponent<Interactable>();
        interactable.Configure(InteractionAnimation.None, 0f, settings.Reach, false, InteractionKind.Use);
        interactable.Completed.AddListener(Open);
        BuildSparkles(settings);
    }

    private void OnEnable() => all.Add(this);

    private void OnDisable() => all.Remove(this);

    private void Open()
    {
        GameObject looter = interactable.Interactor != null ? interactable.Interactor : Player.Current != null ? Player.Current.gameObject : null;
        if (looter == null)
        {
            return;
        }

        FindOnOpening(looter);
        if (IsEmpty)
        {
            Finish();
            return;
        }

        LootWindow.Show(this, looter);
    }

    // A relic or an inscription is found as the body is searched, with its own words and sound.
    private void FindOnOpening(GameObject looter)
    {
        if (opened)
        {
            return;
        }

        opened = true;
        if (relic)
        {
            relic = false;
            RelicSettings.Grant(looter, new System.Random(Random.Range(0, int.MaxValue)), tier);
        }

        if (inscription != null)
        {
            global::Inscriptions.Grant(looter, inscription, Title);
            inscription = null;
        }
    }

    /// <summary>Takes the stack in <paramref name="index"/> into <paramref name="looter"/>'s bag, as much as fits. True when any went.</summary>
    public bool Take(int index, GameObject looter)
    {
        ItemInventory bag = looter != null ? looter.GetComponent<ItemInventory>() : null;
        ItemStack stack = Items.IsValid(index) ? Items.GetSlot(index) : ItemStack.Empty;
        if (bag == null || stack.IsEmpty)
        {
            return false;
        }

        int given = bag.Give(stack.Item, stack.Count, ArrivalCause.Found);
        if (given > 0)
        {
            Items.RemoveAt(index, given);
        }

        AfterTaking();
        return given > 0;
    }

    /// <summary>Takes resource <paramref name="index"/> to <paramref name="looter"/>'s bars, as much as they hold. True when any went.</summary>
    public bool TakeResource(int index, GameObject looter)
    {
        if (looter == null || index < 0 || index >= resources.Count)
        {
            return false;
        }

        ResourceAmount wanted = resources[index];
        List<ResourceAmount> left = LootGrant.GiveResources(looter, new List<ResourceAmount> { wanted });
        int kept = left.Count > 0 ? left[0].Amount : 0;
        if (kept > 0)
        {
            resources[index] = new ResourceAmount(wanted.Resource, kept);
        }
        else
        {
            resources.RemoveAt(index);
        }

        AfterTaking();
        return kept < wanted.Amount;
    }

    /// <summary>Takes everything that fits. Returns how many stacks and resources stayed for want of room.</summary>
    public int TakeAll(GameObject looter)
    {
        FindOnOpening(looter);
        for (int i = 0; i < Items.SlotCount; i++)
        {
            if (!Items.GetSlot(i).IsEmpty)
            {
                Take(i, looter);
            }
        }

        for (int i = resources.Count - 1; i >= 0 && this != null; i--)
        {
            TakeResource(i, looter);
        }

        int stayed = Items.CountItems() + resources.Count;
        if (stayed > 0)
        {
            GameMessages.Post("No room for the rest: it stays on the body.", MessageKind.Warning);
        }

        AfterTaking();
        return stayed;
    }

    private void AfterTaking()
    {
        if (IsEmpty)
        {
            finished = true;
            Finish();
        }
    }

    // Emptied or faded: the body sinks as any corpse, and the glow and glitter go with this.
    private void Finish()
    {
        if (this != null && gameObject != null)
        {
            if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
            else
            {
                DestroyImmediate(gameObject);
            }
        }
    }

    private void Update()
    {
        if (Time.time >= expiresAt)
        {
            PlaytestLogger.Note(PlaytestLog.LootLeft, Title, Items.CountItems() + resources.Count, "faded");
            finished = true;
            Finish();
            return;
        }

        // The corpse is made in the same moment as the loot, in no set order: looked for over the first frames.
        if (corpse == null && corpseLooks < 5 && victim != null)
        {
            corpseLooks++;
            corpse = DeathRagdoll.Of(victim);
            if (corpse != null)
            {
                corpse.HoldsLoot = true;
            }
        }

        // One shared glow, breathing: set once a frame whichever body gets here first.
        if (glowMaterial != null && glowFrame != Time.frameCount)
        {
            glowFrame = Time.frameCount;
            glowMaterial.SetColor(OutlineColorId, BodyLootSettings.Current.GlowAt(Time.time));
        }

        if (Time.time >= nextFollow)
        {
            nextFollow = Time.time + FollowEvery;
            Follow();
        }
    }

    // Onto the body as it falls and settles (the loot point, the glitter's box), then the glow once it lies there.
    private void Follow()
    {
        Transform body = corpse != null ? corpse.transform : null;
        if (body == null || !BoundsOf(body, out Bounds bounds))
        {
            return;
        }

        Vector3 centre = bounds.center;
        if ((centre - transform.position).sqrMagnitude > 0.09f)
        {
            transform.position = centre;
            // Filed again under its new place (InteractableGrid files by position).
            interactable.Configure(InteractionAnimation.None, 0f, BodyLootSettings.Current.Reach, false, InteractionKind.Use);
        }

        if (sparkles != null)
        {
            ParticleSystem.ShapeModule shape = sparkles.shape;
            shape.scale = Vector3.Max(bounds.size, Vector3.one * 0.3f);
            ParticleSystem.EmissionModule emission = sparkles.emission;
            emission.rateOverTime = Mathf.Min(BodyLootSettings.Current.SparklesPerSecond, 2f + bounds.size.x * bounds.size.z * 4f);
        }

        if (glow == null)
        {
            Outline(body);
        }
    }

    private static bool BoundsOf(Transform root, out Bounds bounds)
    {
        bounds = default;
        bool any = false;
        foreach (Renderer part in root.GetComponentsInChildren<Renderer>())
        {
            if (!part.enabled || OutlineCopies.IsCopy(part) || part is ParticleSystemRenderer)
            {
                continue;
            }

            if (any)
            {
                bounds.Encapsulate(part.bounds);
            }
            else
            {
                bounds = part.bounds;
                any = true;
            }
        }

        return any;
    }

    // The faint outline, made from the player's hover outline in the settings' colour (NeededNearby does it so too).
    private void Outline(Transform body)
    {
        PlayerInteractionOutline hover = Player.Current != null ? Player.Current.GetComponent<PlayerInteractionOutline>() : null;
        if (hover == null || hover.OutlineMaterial == null || hover.MaskMaterial == null)
        {
            glow = new List<OutlineCopies.Entry>();
            return;
        }

        if (glowMaterial == null)
        {
            glowMaterial = new Material(hover.OutlineMaterial) { name = "Loot Glow", hideFlags = HideFlags.DontSave };
            glowMaterial.SetColor(OutlineColorId, BodyLootSettings.Current.Glow);
            glowMaterial.SetFloat(OutlinePixelsId, BodyLootSettings.Current.GlowPixels);
        }

        // Made hidden (OutlineCopies): shown at once, for as long as it holds loot.
        glow = OutlineCopies.Create(body, glowMaterial, hover.MaskMaterial, "Loot Glow", "Loot Glow Mask");
        OutlineCopies.SetShown(glow, true);
    }

    private void BuildSparkles(BodyLootSettings settings)
    {
        if (settings.SparkleMaterial == null)
        {
            return;
        }

        var holder = new GameObject("Glitter");
        holder.transform.SetParent(transform, false);
        sparkles = holder.AddComponent<ParticleSystem>();
        sparkles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = sparkles.main;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = settings.SparkleSeconds;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(settings.SparkleSize.x, settings.SparkleSize.y);
        main.startColor = settings.SparkleColour;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 40;
        main.playOnAwake = false;
        ParticleSystem.EmissionModule emission = sparkles.emission;
        emission.rateOverTime = Mathf.Min(settings.SparklesPerSecond, 4f);
        ParticleSystem.ShapeModule shape = sparkles.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.6f, 0.4f, 0.8f);
        ParticleSystem.VelocityOverLifetimeModule rise = sparkles.velocityOverLifetime;
        rise.enabled = true;
        rise.space = ParticleSystemSimulationSpace.World;
        rise.x = new ParticleSystem.MinMaxCurve(0f);
        rise.y = new ParticleSystem.MinMaxCurve(settings.SparkleRise * 0.6f, settings.SparkleRise);
        rise.z = new ParticleSystem.MinMaxCurve(0f);
        // A glint: in quickly, out softly.
        ParticleSystem.ColorOverLifetimeModule fade = sparkles.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
        fade.color = gradient;
        ParticleSystem.SizeOverLifetimeModule twinkle = sparkles.sizeOverLifetime;
        twinkle.enabled = true;
        twinkle.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.3f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0.2f)));
        var renderer = holder.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = settings.SparkleMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        sparkles.Play();
    }

    private void OnDestroy()
    {
        // Left on the body when the raid or the scene ended: what players walk past is worth knowing.
        if (!finished && Items != null && !IsEmpty)
        {
            PlaytestLogger.Note(PlaytestLog.LootLeft, Title, Items.CountItems() + resources.Count, "left");
        }

        if (glow != null)
        {
            OutlineCopies.Destroy(glow);
            glow = null;
        }

        if (corpse != null)
        {
            corpse.HoldsLoot = false;
        }

        if (LootWindow.Current != null && LootWindow.Current.IsShowing(this))
        {
            LootWindow.Current.Close();
        }
    }
}
