using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Makes one creature an elite for this raid: applies its affixes, marks it with a coloured ring and its name, and pays out when the player
/// kills it. The ring and name are what make it readable from the gameplay camera above. Added by RaidElites; gone with the raid.
/// </summary>
[DisallowMultipleComponent]
[OnAFriendsCopy(CopyRule.Same, "the affixes the host's creature has, sent by name and made here")]
public sealed class EliteCreature : MonoBehaviour
{
    private const float RingThickness = 0.14f;
    private const float RingHeight = 0.08f;
    private const float LabelHeight = 2.8f;
    private const float LabelFontSize = 3.2f;
    private const float LabelRange = 16f;
    private const int RingSegments = 40;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static Mesh ringMesh;
    private static Mesh discMesh;

    private readonly List<Action> endings = new List<Action>();
    private readonly List<EliteAffixDefinition> affixes = new List<EliteAffixDefinition>();
    private readonly List<GameObject> marks = new List<GameObject>();
    private RaidDifficulty difficulty;
    private TextMeshPro label;
    private Camera viewCamera;

    public MonsterAI Ai { get; private set; }
    public Combatant Combatant { get; private set; }
    public Damageable Health { get; private set; }
    public IReadOnlyList<EliteAffixDefinition> Affixes => affixes;

    /// <summary>"Frenzied Forest-Bitten Wolf".</summary>
    public string Title { get; private set; }

    /// <summary>Each frame, for behaviours that act over time.</summary>
    public event Action<float> Ticked;

    /// <summary>Promotes a creature. <paramref name="healthScale"/> and <paramref name="powerScale"/> are the raid's own, so an affix's multiplier multiplies them.</summary>
    public static EliteCreature Make(MonsterAI ai, IReadOnlyList<EliteAffixDefinition> chosen, RaidDifficulty tier, float healthScale, float powerScale)
    {
        var elite = ai.gameObject.AddComponent<EliteCreature>();
        elite.Apply(ai, chosen, tier, healthScale, powerScale);
        return elite;
    }

    private void Apply(MonsterAI ai, IReadOnlyList<EliteAffixDefinition> chosen, RaidDifficulty tier, float healthScale, float powerScale)
    {
        Ai = ai;
        ai.IsElite = true;
        Combatant = ai.GetComponent<Combatant>();
        Health = ai.GetComponent<Damageable>();
        difficulty = tier;
        affixes.AddRange(chosen);

        float health = 1f, damage = 1f, speed = 1f, cooldowns = 1f, armor = 0f, size = 1f;
        foreach (EliteAffixDefinition affix in affixes)
        {
            health *= affix.Health;
            damage *= affix.Damage;
            speed *= affix.Speed;
            cooldowns *= affix.Cooldowns;
            armor += affix.Armor;
            size *= affix.Size;
        }

        // The raid's own bonuses are added as shares of the base value, so these are scaled by them to multiply rather than add.
        healthShare = health - 1f;
        damageShare = damage - 1f;
        armorBonus = armor;
        Rescale(healthScale, powerScale);

        Health.Heal(Health.MaxHealth);
        ai.SpeedScale = speed;
        // (the affixes' shares are kept for Rescale)
        ai.CooldownScale = cooldowns;
        transform.localScale *= size;

        BuildMarks();
        foreach (EliteAffixDefinition affix in affixes)
        {
            foreach (EliteBehaviour behaviour in affix.Behaviours)
            {
                if (behaviour != null)
                {
                    endings.Add(behaviour.Attach(this));
                }
            }
        }

        Health.Died += OnDied;
    }

    private void Update() => Ticked?.Invoke(Time.deltaTime);

    private void LateUpdate()
    {
        if (label == null)
        {
            return;
        }

        // Only near the player: a name showing through the fog of war would give away where it stands.
        Player player = Player.Current;
        bool near = player != null && (player.transform.position - transform.position).sqrMagnitude < LabelRange * LabelRange;
        label.enabled = near;
        if (near)
        {
            viewCamera = viewCamera != null ? viewCamera : Camera.main;
            if (viewCamera != null)
            {
                label.transform.rotation = viewCamera.transform.rotation;
            }
        }
    }

    private float healthShare;
    private float damageShare;
    private float armorBonus;

    /// <summary>
    /// Sets the elite's own bonuses for the raid's scaling now (RaidCreatures.Strengthen, at promotion and at every stage
    /// change): worked out once at promotion, a stage's bonus came on top of them instead of under them (×2.40 where ×2.52 was due).
    /// </summary>
    public void Rescale(float healthScale, float powerScale)
    {
        StatSheet stats = Combatant.Stats;
        stats.RemoveModifiersFrom(this);
        stats.AddModifier(new StatModifier(StatType.MaxHealth, StatModifierOp.Percent, healthShare * healthScale, this));
        stats.AddModifier(new StatModifier(StatType.DamageBonus, StatModifierOp.Flat, damageShare * powerScale, this));
        if (armorBonus > 0f)
        {
            stats.AddModifier(new StatModifier(StatType.Armor, StatModifierOp.Flat, armorBonus, this));
        }
    }

    /// <summary>
    /// Multiplies its own bonuses by more, kept through every rescale after (a stage, the dark): a Lost One's rank and scars
    /// (RaidLostOnes). Folded into its shares, so they multiply the affixes' and the raid's rather than add to them. Heals it to full.
    /// </summary>
    public void Strengthen(float health, float damage, float healthScale, float powerScale)
    {
        healthShare = (1f + healthShare) * Mathf.Max(0.1f, health) - 1f;
        damageShare = (1f + damageShare) * Mathf.Max(0f, damage) - 1f;
        Rescale(healthScale, powerScale);
        Health.Heal(Health.MaxHealth);
    }

    /// <summary>True once it carries a name of its own (<see cref="Rename"/>: a Lost One), a person's name rather than a kind of creature.</summary>
    public bool HasOwnName { get; private set; }

    /// <summary>A name of its own in place of the affixes' (a Lost One): what messages call it, and what its label reads (rich text).</summary>
    public void Rename(string title, string labelText)
    {
        Title = title;
        HasOwnName = !string.IsNullOrWhiteSpace(title);
        if (label != null)
        {
            label.text = labelText;
        }
    }

    private void BuildMarks()
    {
        var name = new System.Text.StringBuilder();
        var plain = new System.Text.StringBuilder();
        float radius = FeetRadius();
        for (int i = 0; i < affixes.Count; i++)
        {
            EliteAffixDefinition affix = affixes[i];
            name.Append($"<color=#{ColorUtility.ToHtmlStringRGB(affix.Colour)}>{affix.DisplayName}</color> ");
            plain.Append(affix.DisplayName).Append(' ');
            AddMark(RingMesh(), radius + i * RingThickness * 1.6f, affix.Colour);
        }

        // On the body too: the rings are on the ground, and easily lost in a fight.
        if (affixes.Count > 0)
        {
            Color mark = affixes[0].Colour;
            CreatureLook.Tint(gameObject, Color.Lerp(Color.white, mark, 0.3f));
            CreatureLook.SetEyes(gameObject, mark * 3f);
        }

        Title = plain + Ai.Definition.DisplayName;
        var holder = new GameObject("Elite Label");
        holder.transform.SetParent(transform, false);
        holder.transform.localPosition = Vector3.up * LabelHeight / Mathf.Max(0.1f, transform.localScale.y);
        marks.Add(holder);
        QuestMarkerSettings look = QuestMarkerSettings.Current;
        label = holder.AddComponent<TextMeshPro>();
        if (look.Font != null)
        {
            label.font = look.Font;
        }

        if (look.FontMaterial != null)
        {
            label.fontSharedMaterial = look.FontMaterial;
        }

        label.richText = true;
        label.fontSize = LabelFontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.rectTransform.sizeDelta = new Vector2(8f, 1.5f);
        label.outlineWidth = look.OutlineWidth;
        label.outlineColor = Color.black;
        label.text = name + Ai.Definition.DisplayName;
    }

    private float FeetRadius()
    {
        var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        return Mathf.Max(0.6f, agent != null ? agent.radius * 2f : 0.8f);
    }

    /// <summary>A faint disc in the first affix's colour around it, to show how far an aura reaches.</summary>
    public void AddRing(float radius, float alpha)
    {
        Color colour = affixes.Count > 0 ? affixes[0].Colour : Color.white;
        colour.a = alpha;
        AddMark(DiscMesh(), radius / Mathf.Max(0.1f, transform.localScale.x), colour);
    }

    private void AddMark(Mesh mesh, float radius, Color colour)
    {
        Material material = CombatFeelSettings.Current.TelegraphMaterial;
        if (material == null)
        {
            return;
        }

        var mark = new GameObject("Elite Ring");
        mark.transform.SetParent(transform, false);
        mark.transform.localPosition = Vector3.up * RingHeight;
        mark.transform.localScale = new Vector3(radius, 1f, radius);
        marks.Add(mark);
        mark.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = mark.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        var block = new MaterialPropertyBlock();
        block.SetColor(BaseColorId, colour);
        block.SetColor(ColorId, colour);
        renderer.SetPropertyBlock(block);
    }

    // Everything the killer earned on top of the creature's own drops. A killer who isn't the player earns nothing.
    private void OnDied(Damageable victim, DamageInfo blow)
    {
        foreach (GameObject mark in marks)
        {
            if (mark != null)
            {
                mark.SetActive(false);
            }
        }

        // A companion's finishing blow pays the player, as an ordinary creature's drop does (LootOnDeath.TakerOf).
        Combatant taker = LootOnDeath.TakerOf(blow.Source);
        if (difficulty == null || taker == null || !Player.IsPlayer(taker.gameObject))
        {
            return;
        }

        GameObject killer = taker.gameObject;
        // As its creature's own loot does: on the body, with its title, when the kill was made near the player (LootOnDeath.LeavesOnBody).
        if (LootOnDeath.LeavesOnBody(BodyLootSettings.Current.LootFromBodies, true, taker, true, LootOnDeath.StruckNearPlayer(blow.Source)))
        {
            List<ItemAmount> rolled = EliteLoot(difficulty).Roll(difficulty.tier, 1f);
            rolled.RemoveAll(drop => !difficulty.Allows(drop.Item));
            // Now and then lore lies on its body with the rest (SkillLore; this branch returned before the roll, bug hunt 2026-11-27).
            if (UnityEngine.Random.value < SkillLoreSettings.Current.EliteChance(difficulty.tier))
            {
                ItemDefinition lore = SkillLore.Pick(killer, new System.Random(UnityEngine.Random.Range(0, int.MaxValue)));
                if (lore != null)
                {
                    rolled.Add(new ItemAmount(lore, 1));
                    SkillLore.NoteFound(lore, "on " + Title);
                }
            }

            bool relic = difficulty.eliteRelicChance > 0f && UnityEngine.Random.value < difficulty.eliteRelicChance;
            LootableRemains body = LootableRemains.Leave(victim, Title, rolled, EliteLoot(difficulty).RollResources(difficulty.tier, 1f), relic,
                difficulty.tier, PickInscription(killer, difficulty.tier));
            body?.Rename(Title);
            GameMessages.Post($"{Title} slain.", MessageKind.Gain);
            return;
        }

        var bag = killer.GetComponentInParent<ItemInventory>();
        if (bag != null)
        {
            List<ItemAmount> items = EliteLoot(difficulty).Roll(difficulty.tier, 1f);
            items.RemoveAll(drop => !difficulty.Allows(drop.Item));
            ItemGrantResult result = bag.Give(items, ArrivalCause.Found);
            if (result.HasOverflow)
            {
                GameMessages.Post("Bag full, left behind: " + AmountText.Describe(result.Overflow), MessageKind.Warning);
                LootGrant.LeftBehind(killer, result.Overflow);
            }
        }

        List<ResourceAmount> overflow = LootGrant.GiveResources(killer, EliteLoot(difficulty).RollResources(difficulty.tier, 1f));
        if (overflow.Count > 0)
        {
            GameMessages.Post("Can't carry more: " + AmountText.Describe(overflow) + " left behind.", MessageKind.Warning);
            LootGrant.LeftBehind(killer, overflow);
        }

        GameMessages.Post($"{Title} slain.", MessageKind.Gain);
        if (difficulty.eliteRelicChance > 0f && UnityEngine.Random.value < difficulty.eliteRelicChance)
        {
            RelicSettings.Grant(killer, new System.Random(UnityEngine.Random.Range(0, int.MaxValue)), difficulty.tier);
        }

        GiveInscription(killer, difficulty.tier);
        // Now and then an elite carries lore: an ability to take home (SkillLore).
        SkillLore.Roll(killer, SkillLoreSettings.Current.EliteChance(difficulty.tier), Title);
    }

    // The raid's elite table (rows by tier) when it has one, else the tier's own list.
    private static ItemDropTable EliteLoot(RaidDifficulty difficulty)
    {
        LootTable table = RaidDirector.Current != null ? RaidDirector.Current.Session.Definition.EliteLoot : null;
        return table != null ? table.Drops : difficulty.eliteLoot;
    }

    // The mark's own enchantment, one not yet known first; when its marks' are all known, another elite one of the tier, else a copy.
    private void GiveInscription(GameObject killer, int tier)
    {
        EnchantmentDefinition found = PickInscription(killer, tier);
        if (found != null)
        {
            Inscriptions.Grant(killer, found, Title);
        }
    }

    // An inscription of its own affixes it may carry, at the tier's chance; null when it carries none.
    private EnchantmentDefinition PickInscription(GameObject killer, int tier)
    {
        if (UnityEngine.Random.value >= EssenceSettings.Current.EliteInscriptionChance(tier))
        {
            return null;
        }

        var own = new List<EnchantmentDefinition>();
        foreach (EliteAffixDefinition affix in Affixes)
        {
            if (affix != null && affix.Inscription != null)
            {
                own.Add(affix.Inscription);
            }
        }

        var random = new System.Random(UnityEngine.Random.Range(0, int.MaxValue));
        return Inscriptions.Pick(own, killer, random, copies: false)
               ?? Inscriptions.Pick(Inscriptions.Pool(EnchantmentSource.Elite, tier), killer, random, copies: false)
               ?? (own.Count > 0 ? own[random.Next(own.Count)] : null);
    }

    private void OnDestroy()
    {
        if (Health != null)
        {
            Health.Died -= OnDied;
        }

        foreach (Action ending in endings)
        {
            ending?.Invoke();
        }

        endings.Clear();
    }

    // Shared by every elite, and kept for the session: small, and asked for again next raid.
    private static Mesh RingMesh()
    {
        if (ringMesh != null)
        {
            return ringMesh;
        }

        var vertices = new Vector3[(RingSegments + 1) * 2];
        var triangles = new int[RingSegments * 6];
        for (int i = 0; i <= RingSegments; i++)
        {
            float angle = i * Mathf.PI * 2f / RingSegments;
            var out_ = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            vertices[i * 2] = out_;
            vertices[i * 2 + 1] = out_ * (1f - RingThickness);
        }

        for (int i = 0; i < RingSegments; i++)
        {
            int at = i * 6, a = i * 2;
            triangles[at] = a;
            triangles[at + 1] = a + 2;
            triangles[at + 2] = a + 1;
            triangles[at + 3] = a + 1;
            triangles[at + 4] = a + 2;
            triangles[at + 5] = a + 3;
        }

        ringMesh = new Mesh { name = "Elite Ring", vertices = vertices, triangles = triangles };
        ringMesh.RecalculateNormals();
        ringMesh.RecalculateBounds();
        return ringMesh;
    }

    private static Mesh DiscMesh()
    {
        if (discMesh == null)
        {
            discMesh = AttackTelegraph.FanFor(360f);
        }

        return discMesh;
    }
}

/// <summary>
/// The burst an elite leaves when it dies: a warning that grows on the ground and then the blow, outliving the body if it has to. The player can
/// always step out, and only what the creature's side is hostile to is hurt.
/// </summary>
public sealed class EliteBurst : MonoBehaviour
{
    private Combatant source;
    private AttackDefinition attack;
    private float damage;
    private float delay;
    private float started;
    private int layers;
    private Transform fill;

    public static void Begin(EliteCreature elite, AttackDefinition attack, float damage, float delay)
    {
        var holder = new GameObject("Elite Burst");
        holder.transform.position = elite.transform.position + Vector3.up * 0.1f;
        var burst = holder.AddComponent<EliteBurst>();
        burst.source = elite.Combatant;
        burst.attack = attack;
        burst.damage = damage;
        burst.delay = delay;
        burst.started = Time.time;
        burst.layers = elite.Ai.TargetLayers;
        burst.Build(elite.Affixes.Count > 0 ? elite.Affixes[0].Colour : Color.red);
    }

    private void Build(Color colour)
    {
        Material material = CombatFeelSettings.Current.TelegraphMaterial;
        if (material == null)
        {
            return;
        }

        float range = attack.Range;
        Part(AttackTelegraph.FanFor(360f), range, new Color(colour.r, colour.g, colour.b, 0.22f), material);
        fill = Part(AttackTelegraph.FanFor(360f), 0.01f, new Color(colour.r, colour.g, colour.b, 0.55f), material);
    }

    private Transform Part(Mesh mesh, float radius, Color colour, Material material)
    {
        var part = new GameObject("Part");
        part.transform.SetParent(transform, false);
        part.transform.localScale = new Vector3(radius, 1f, radius);
        part.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = part.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", colour);
        block.SetColor("_Color", colour);
        renderer.SetPropertyBlock(block);
        return part.transform;
    }

    private void Update()
    {
        float progress = (Time.time - started) / delay;
        if (fill != null)
        {
            float radius = Mathf.Clamp01(progress) * attack.Range;
            fill.localScale = new Vector3(radius, 1f, radius);
        }

        if (progress < 1f)
        {
            return;
        }

        if (source != null)
        {
            AttackResolver.Execute(source, attack, damage, transform.position, Vector3.forward, layers);
        }

        Destroy(gameObject);
    }
}
