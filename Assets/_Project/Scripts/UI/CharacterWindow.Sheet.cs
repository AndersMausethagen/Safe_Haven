using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The character window as a sheet (Hearthline 3, the UI kit, after the user's reference picture): wider, the player's
/// name in the display face over a portrait between the gear columns (a still, idle stand-in in the worn gear), and the stats as cards with an icon on each
/// line: Attributes, Offense, Resistances (two by two) and Gathering. The gear slots, their dragging and tooltips are the
/// window's own and unchanged; the old stats label is kept up to date but hidden. Laid out once, the first time it opens.
/// </summary>
public sealed partial class CharacterWindow
{
    private const float SheetWidth = 1120f;
    // Taller since 2026-11-27 for the Active Effects card across the foot (CharacterWindow.Effects).
    private const float SheetHeight = 800f;
    private const float StatsX = 592f;
    private const float StatsWidth = 504f;

    private bool sheetBuilt;
    private TMP_Text nameLabel;
    private TMP_Text scoreLabel;
    private KitStatRow healthRow;
    private KitStatRow strengthRow;
    private KitStatRow armorRow;
    private KitStatRow physicalPowerRow;
    private KitStatRow spellPowerRow;
    private KitStatRow physicalCritRow;
    private KitStatRow spellCritRow;
    private KitStatRow weaponRow;
    private readonly KitStatRow[] resistanceRows = new KitStatRow[4];
    private readonly KitStatRow[] toolRows = new KitStatRow[3];
    private RawImage portraitPicture;
    private CharacterPreviewStage portraitStage;
    private readonly List<GearLookDefinition> portraitLooks = new List<GearLookDefinition>();
    private readonly List<GearLookDefinition> portraitWorn = new List<GearLookDefinition>();

    private void BuildSheet()
    {
        if (sheetBuilt || UIStyle.Current.Card == null)
        {
            return;
        }

        sheetBuilt = true;
        UIStyle style = UIStyle.Current;
        var window = (RectTransform)statsLabel.transform.parent;
        window.anchorMin = window.anchorMax = window.pivot = new Vector2(0.5f, 0.5f);
        window.anchoredPosition = Vector2.zero;
        window.sizeDelta = new Vector2(SheetWidth, SheetHeight);
        statsLabel.gameObject.SetActive(false);

        // The name over all of it, in the display face; the old title gives way to it.
        Transform title = window.Find("Title");
        if (title != null)
        {
            title.gameObject.SetActive(false);
        }

        nameLabel = HearthKit.DisplayName(window, 24f, 2f, 520f, 40f);
        scoreLabel = CodeUI.Label("Gear Score", window, StatsX, 22f, StatsWidth - 60f, 26f, 19f, style.Accent);

        // The portrait between the slot columns.
        RectTransform frame = HearthKit.Card(window, "Portrait", 100f, 64f, 400f, 424f, 0f, null, 1.5f);
        frame.SetSiblingIndex(statsLabel.transform.GetSiblingIndex());
        RectTransform inner = CodeUI.Stretch("Image", frame);
        inner.offsetMin = new Vector2(5f, 5f);
        inner.offsetMax = new Vector2(-5f, -5f);
        inner.gameObject.AddComponent<RectMask2D>();
        portraitPicture = CodeUI.Stretch("Render", inner).gameObject.AddComponent<RawImage>();
        // Dragged sideways, the character turns round (the user, 2026-11-15: "let us rotate the player's model").
        portraitPicture.raycastTarget = true;
        portraitPicture.gameObject.AddComponent<DragToTurn>().Turned += degrees =>
        {
            if (portraitStage != null)
            {
                portraitStage.Turn(degrees);
            }
        };
        HearthKit.Rim(frame, 0.85f, 1.5f).transform.SetAsLastSibling();

        RectTransform attributes = HearthKit.Card(window, "Attributes", StatsX, 64f, StatsWidth, 150f);
        HearthKit.Section(attributes, "Attributes", "Star", 20f, 14f);
        healthRow = Row(attributes, 0, "Health", "Heart", bar: true);
        // Strength and Physical Power were one number shown twice: one Might row, as the damage it adds (Docs/PlayerStatsResearch.md #5).
        strengthRow = Row(attributes, 1, "Might", "Sword");
        armorRow = Row(attributes, 2, "Armor", "Barding");

        // The Fight card (Docs/PlayerStatsResearch.md #2): what the numbers come to, in blows and dodges, against the raid tier ahead.
        RectTransform offense = HearthKit.Card(window, "Fight", StatsX, 222f, StatsWidth, 208f);
        HearthKit.Section(offense, "Fight", "Sword", 20f, 14f);
        physicalPowerRow = Row(offense, 0, "Damage", "Sword");
        spellPowerRow = Row(offense, 1, "Blows", "Crit");
        physicalCritRow = Row(offense, 2, "Stamina", "Bolt");
        spellCritRow = Row(offense, 3, "Dodge", "Weight");
        weaponRow = Row(offense, 4, "Staff and abilities", "Spark");

        RectTransform resistances = HearthKit.Card(window, "Resistances", StatsX, 438f, StatsWidth, 108f);
        HearthKit.Section(resistances, "Resistances", "Barding", 20f, 14f);
        // "Holy" only ever stopped Poison: shown as Blight (the stat keeps its name in code and saves).
        string[] names = { "Physical", "Fire", "Frost", "Blight" };
        string[] icons = { "Barding", "Flame", "Snowflake", "Skull" };
        float half = (StatsWidth - 52f) * 0.5f;
        for (int i = 0; i < names.Length; i++)
        {
            resistanceRows[i] = new KitStatRow(resistances, 20f + (i % 2) * (half + 12f), 50f + (i / 2) * 26f, half, names[i], icons[i], false, 17f, labelWidth: 110f);
        }

        RectTransform gathering = HearthKit.Card(window, "Gathering", StatsX, 554f, StatsWidth, 52f);
        string[] tools = { "Axe", "Pickaxe", "Sickle" };
        float third = (StatsWidth - 40f) / 3f;
        for (int i = 0; i < tools.Length; i++)
        {
            toolRows[i] = new KitStatRow(gathering, 16f + i * third, 12f, third - 10f, tools[i], tools[i], false, 17f, labelWidth: 74f);
        }

        BuildEffects(window);

        // The hint keeps to the foot, under the Active Effects.
        Transform hint = window.Find("Hint");
        if (hint != null)
        {
            ((RectTransform)hint).anchoredPosition = new Vector2(24f, -SheetHeight + 28f);
            ((RectTransform)hint).sizeDelta = new Vector2(500f, 22f);
        }
    }

    private static readonly Color[] ResistanceColours =
    {
        new Color(0.86f, 0.72f, 0.42f), new Color(0.96f, 0.5f, 0.25f), new Color(0.55f, 0.78f, 0.95f), new Color(0.6f, 0.8f, 0.4f),
    };

    private static KitStatRow Row(RectTransform card, int index, string name, string icon, bool bar = false) =>
        new KitStatRow(card, 20f, 50f + index * 30f, StatsWidth - 40f, name, icon, bar, 18f, inline: bar, labelWidth: 160f, valueWidth: 110f);

    private void RefreshSheet(StatSheet stats, CombatRules rules, float armor)
    {
        if (!sheetBuilt)
        {
            return;
        }

        UIStyle style = UIStyle.Current;
        string muted = style.MutedHex;
        nameLabel.text = GameSession.Appearance != null ? GameSession.Appearance.DisplayName : "Character";
        scoreLabel.text = $"Gear score {GearScore.Of(combatant.gameObject)}";
        if (health != null)
        {
            healthRow.Set($"{Mathf.CeilToInt(health.CurrentHealth)} / {Mathf.RoundToInt(health.MaxHealth)}", health.HealthFraction, style.Good, style.Danger);
        }

        float might = rules.PowerFor(stats, DamageType.Physical) * rules.DamagePerPower;
        strengthRow.Set($"{Number(rules.PowerFor(stats, DamageType.Physical))} <color={muted}>(+{Mathf.RoundToInt(might * 100f)}% damage)</color>", 0f, style.Text, style.Text);
        armorRow.Set($"{Number(armor)} <color={muted}>({Mathf.RoundToInt(rules.GetArmorReduction(armor) * 100f)}% less)</color>", 0f, style.Text, style.Text);
        FightCard card = FightCard.Read(combatant.gameObject, stats, rules, health != null ? health.MaxHealth : 100f);
        physicalPowerRow.Set(card.Damage, 0f, style.Text, style.Text);
        spellPowerRow.Set(card.Blows, 0f, style.Text, style.Text);
        physicalCritRow.Set(card.Stamina, 0f, style.Text, style.Text);
        spellCritRow.Set(card.Dodge, 0f, style.Text, style.Text);
        weaponRow.Set($"{Number(rules.PowerFor(stats, DamageType.Fire))} <color={muted}>power · {Percent(stats.Get(StatType.SpellCritChance))} crit</color>", 0f, style.Text, style.Text);
        StatType[] resist = { StatType.PhysicalResistance, StatType.FireResistance, StatType.FrostResistance, StatType.HolyResistance };
        for (int i = 0; i < resist.Length; i++)
        {
            // Each element in its own colour once it guards at all; none is muted, below none is a danger (design review 2026-11-28).
            float guard = rules.ClampResistance(stats.Get(resist[i]));
            Color tone = guard < 0f ? style.Danger : guard <= 0f ? style.Muted : ResistanceColours[i];
            resistanceRows[i].Set($"<color=#{ColorUtility.ToHtmlStringRGB(tone)}>{Percent(guard)}</color>", 0f, style.Text, style.Text);
        }

        ToolType[] tools = { ToolType.Axe, ToolType.Pickaxe, ToolType.Sickle };
        for (int i = 0; i < tools.Length; i++)
        {
            int tier = equipment.Gear.ToolTier(tools[i]);
            toolRows[i].Set(tier > 0 ? $"<color=#{ColorUtility.ToHtmlStringRGB(Tiers.ColorOf(tier))}>Tier {Tiers.ToRoman(tier)}</color>" : $"<color={muted}>none</color>",
                0f, style.Text, style.Text);
        }
    }

    // The portrait: the character creation screen's stand-in, in the player's look and worn gear, before the painted
    // backdrop, playing the player's own idle (the clip of the player's Idle state) and turned by dragging it. It held still
    // after a few frames since 2026-11-14 ("no animation, just an idle pose"); on 2026-11-15 the user asked for the idle to
    // play and the model to turn. Held things (a weapon in hand, the lantern) are not shown: the stand-in wears looks only.
    private void LateUpdate()
    {
        if (!sheetBuilt || equipment == null)
        {
            return;
        }

        if (portraitStage == null)
        {
            portraitStage = CharacterPreviewStage.Create(600, 640);
            portraitPicture.texture = portraitStage.Texture;
            PortraitStage.On(portraitStage.View, "CharacterBackdrop", portraitStage.Model);
            portraitWorn.Clear();
            portraitWorn.Add(null); // never matches: dress on the first frame
        }

        DressPortrait();
    }

    private void DressPortrait()
    {
        ModularCharacter body = equipment.GetComponentInChildren<ModularCharacter>();
        CharacterAppearance look = body != null && body.Appearance != null ? body.Appearance : GameSession.Appearance;
        if (look == null)
        {
            return;
        }

        int clothes = look.clothes;
        PlayerLook.Outfit(slot =>
        {
            ItemStack stack = equipment.Gear.Get(slot);
            return stack.IsEmpty ? null : stack.Item;
        }, clothes, portraitLooks);
        if (Same(portraitLooks, portraitWorn))
        {
            return;
        }

        portraitWorn.Clear();
        portraitWorn.AddRange(portraitLooks);
        portraitStage.Show(look, portraitLooks.ToArray());
    }

    private static bool Same(List<GearLookDefinition> a, List<GearLookDefinition> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        for (int i = 0; i < a.Count; i++)
        {
            if (a[i] != b[i])
            {
                return false;
            }
        }

        return true;
    }

    private void HidePortrait()
    {
        if (portraitStage != null)
        {
            Destroy(portraitStage.gameObject);
            portraitStage = null;
        }
    }
}
