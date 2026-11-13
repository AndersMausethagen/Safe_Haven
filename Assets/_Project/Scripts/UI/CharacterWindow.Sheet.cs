using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The character window as a sheet (Hearthline 3, the UI kit, after the user's reference picture): wider, the player's
/// name in the display face over a live portrait between the gear columns, and the stats as cards with an icon on each
/// line: Attributes, Offense, Resistances (two by two) and Gathering. The gear slots, their dragging and tooltips are the
/// window's own and unchanged; the old stats label is kept up to date but hidden. Laid out once, the first time it opens.
/// </summary>
public sealed partial class CharacterWindow
{
    private const float SheetWidth = 1120f;
    private const float SheetHeight = 640f;
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
    private RenderTexture portraitTexture;
    private Camera portraitCamera;

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
        var picture = CodeUI.Stretch("Render", inner).gameObject.AddComponent<RawImage>();
        portraitTexture = new RenderTexture(600, 640, 24) { name = "Character Portrait" };
        picture.texture = portraitTexture;
        picture.raycastTarget = false;
        HearthKit.Rim(frame, 0.85f, 1.5f).transform.SetAsLastSibling();

        RectTransform attributes = HearthKit.Card(window, "Attributes", StatsX, 64f, StatsWidth, 150f);
        HearthKit.Section(attributes, "Attributes", "Star", 20f, 14f);
        healthRow = Row(attributes, 0, "Health", "Heart", bar: true);
        strengthRow = Row(attributes, 1, "Strength", "Weight");
        armorRow = Row(attributes, 2, "Armor", "Barding");

        RectTransform offense = HearthKit.Card(window, "Offense", StatsX, 222f, StatsWidth, 208f);
        HearthKit.Section(offense, "Offense", "Sword", 20f, 14f);
        physicalPowerRow = Row(offense, 0, "Physical power", "Sword");
        spellPowerRow = Row(offense, 1, "Spell power", "Spark");
        physicalCritRow = Row(offense, 2, "Physical crit", "Crit");
        spellCritRow = Row(offense, 3, "Spell crit", "Crit");
        weaponRow = Row(offense, 4, "Weapon", "Sword");

        RectTransform resistances = HearthKit.Card(window, "Resistances", StatsX, 438f, StatsWidth, 108f);
        HearthKit.Section(resistances, "Resistances", "Barding", 20f, 14f);
        string[] names = { "Physical", "Fire", "Frost", "Holy" };
        string[] icons = { "Barding", "Flame", "Snowflake", "Sun" };
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

        // The hint keeps to the foot, under the portrait.
        Transform hint = window.Find("Hint");
        if (hint != null)
        {
            ((RectTransform)hint).anchoredPosition = new Vector2(24f, -SheetHeight + 40f);
            ((RectTransform)hint).sizeDelta = new Vector2(500f, 22f);
        }
    }

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

        strengthRow.Set(Number(stats.Get(StatType.Strength)), 0f, style.Text, style.Text);
        armorRow.Set($"{Number(armor)} <color={muted}>({Mathf.RoundToInt(rules.GetArmorReduction(armor) * 100f)}% less)</color>", 0f, style.Text, style.Text);
        physicalPowerRow.Set(Number(rules.PowerFor(stats, DamageType.Physical)), 0f, style.Text, style.Text);
        spellPowerRow.Set(Number(rules.PowerFor(stats, DamageType.Fire)), 0f, style.Text, style.Text);
        physicalCritRow.Set($"{Percent(stats.Get(StatType.PhysicalCritChance))} <color={muted}>×{1f + stats.Get(StatType.PhysicalCritDamage):0.##}</color>", 0f, style.Text, style.Text);
        spellCritRow.Set($"{Percent(stats.Get(StatType.SpellCritChance))} <color={muted}>×{1f + stats.Get(StatType.SpellCritDamage):0.##}</color>", 0f, style.Text, style.Text);
        bool armed = !equipment.MainHand.IsEmpty && !equipment.MainHand.IsBroken;
        weaponRow.Set(armed ? $"{equipment.MainHand.Item.DisplayName} <color={muted}>{Mathf.RoundToInt(equipment.MainHand.Item.Weapon.Damage)}</color>" : $"<color={muted}>Unarmed</color>",
            0f, style.Text, style.Text);
        StatType[] resist = { StatType.PhysicalResistance, StatType.FireResistance, StatType.FrostResistance, StatType.HolyResistance };
        for (int i = 0; i < resist.Length; i++)
        {
            resistanceRows[i].Set(Percent(rules.ClampResistance(stats.Get(resist[i]))), 0f, style.Text, style.Text);
        }

        ToolType[] tools = { ToolType.Axe, ToolType.Pickaxe, ToolType.Sickle };
        for (int i = 0; i < tools.Length; i++)
        {
            int tier = equipment.Gear.ToolTier(tools[i]);
            toolRows[i].Set(tier > 0 ? $"<color=#{ColorUtility.ToHtmlStringRGB(Tiers.ColorOf(tier))}>Tier {Tiers.ToRoman(tier)}</color>" : $"<color={muted}>none</color>",
                0f, style.Text, style.Text);
        }
    }

    // The live portrait: a camera before the player, at chest height, only while the window is open.
    private void LateUpdate()
    {
        if (!sheetBuilt || equipment == null)
        {
            return;
        }

        if (portraitCamera == null)
        {
            var holder = new GameObject("Character Portrait Camera");
            holder.transform.SetParent(transform, false);
            portraitCamera = holder.AddComponent<Camera>();
            portraitCamera.targetTexture = portraitTexture;
            portraitCamera.fieldOfView = 30f;
            portraitCamera.nearClipPlane = 0.2f;
            portraitCamera.farClipPlane = 80f;
        }

        // Hearthline 3: the player before a painted backdrop, not the world behind them (PortraitStage).
        PortraitStage.On(portraitCamera, "CharacterBackdrop", equipment.transform);

        portraitCamera.enabled = true;
        Transform body = equipment.transform;
        Vector3 look = body.position + Vector3.up * 1.05f;
        portraitCamera.transform.position = look + body.forward * 3.6f + body.right * 0.6f + Vector3.up * 0.25f;
        portraitCamera.transform.LookAt(look);
    }

    private void HidePortrait()
    {
        if (portraitCamera != null)
        {
            portraitCamera.enabled = false;
        }
    }
}
