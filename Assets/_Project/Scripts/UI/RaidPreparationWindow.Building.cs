using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static CodeUI;

/// <summary>
/// Building the window in code when it wakes: the panel, both columns, the figures, the footer and the hover tooltip,
/// and the rows that are added as they are needed (tabs, tiers, rumours, finds, runes). Part of RaidPreparationWindow,
/// in its own file.
/// </summary>
public sealed partial class RaidPreparationWindow
{
    private void Build()
    {
        UIStyle style = UIStyle.Current;
        panel = Box("Panel", content, new Vector2(0.5f, 0.5f), new Vector2(Width, Height), new Vector2(0f, Lift));
        panel.gameObject.AddComponent<Image>();
        panel.gameObject.AddComponent<Outline>().effectDistance = new Vector2(1f, -1f);
        panel.gameObject.AddComponent<UIRole>().Set(UIRole.Kind.Window);

        // The chosen place's painting across the header, fading into the panel to the left and below so the title stays readable.
        // Inset from the top and right edges so the panel's carved corners stay in view.
        placeArt = At("Place Art", panel, 0f, FrameInset, Width - FrameInset, HeaderHeight - FrameInset).gameObject.AddComponent<RawImage>();
        placeArt.raycastTarget = false;
        placeArt.enabled = false;
        var fade = At("Place Fade", panel, 0f, FrameInset, Width - FrameInset, HeaderHeight - FrameInset).gameObject.AddComponent<RawImage>();
        fade.texture = HeaderFade();
        fade.color = style.Panel;
        fade.raycastTarget = false;
        // Behind everything else on the panel, the carved corners included.
        fade.transform.SetAsFirstSibling();
        placeArt.transform.SetAsFirstSibling();

        TMP_Text kicker = Label("Kicker", panel, Margin, 30f, 500f, 24f, 17f, style.Accent);
        kicker.text = "THE PORTAL";
        kicker.characterSpacing = 12f;
        titleLabel = Label("Title", panel, Margin, 52f, 760f, 56f, 42f, style.Text);
        style.Title(titleLabel);
        descriptionLabel = Paragraph("Description", panel, Margin, 108f, Width - Margin * 2f, 50f, 18f, style.Muted);
        Hairline(panel, Margin, 160f, Width - Margin * 2f, 1f, style);

        BuildBriefing(style);
        Hairline(panel, RightColumn - 20f, BodyTop, 1f, BodyBottom - BodyTop, style);
        BuildPreparation(style);
        lockedNote = Paragraph("Locked Note", panel, RightColumn, BodyTop + 4f, ColumnWidth, 200f, 22f, style.Muted);
        style.Title(lockedNote);
        lockedNote.color = style.Muted;
        BuildStats(style);
        BuildFooter(style);
        BuildTooltip(style);
    }

    private const float HeaderHeight = 160f;
    private const float FrameInset = 38f;

    // Clear at the top right, solid from the left edge to the title's end and behind the description: alpha only, tinted the panel's colour.
    private static Texture2D HeaderFade()
    {
        const int width = 128;
        const int height = 32;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Raid Header Fade" };
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float across = Mathf.InverseLerp(0.75f, 0.32f, x / (width - 1f));
                // Solid under the description line (the lower third), clearing towards the top.
                float down = Mathf.InverseLerp(0.7f, 0.36f, y / (height - 1f));
                float alpha = Mathf.SmoothStep(0f, 1f, Mathf.Max(across, down));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Max(alpha, 0.18f)));
            }
        }

        texture.Apply();
        return texture;
    }

    // The middle band of the painting that fits the header's shape.
    private void ShowPlace(Sprite art)
    {
        placeArt.enabled = art != null;
        if (art == null)
        {
            return;
        }

        Texture texture = art.texture;
        Rect rect = art.textureRect;
        float band = Mathf.Min(1f, (HeaderHeight - FrameInset) / (Width - FrameInset) * rect.width / rect.height);
        placeArt.texture = texture;
        placeArt.uvRect = new Rect(rect.x / texture.width, (rect.y + rect.height * (0.55f - band * 0.5f)) / texture.height,
            rect.width / texture.width, rect.height * band / texture.height);
    }

    private void BuildBriefing(UIStyle style)
    {
        RectTransform part = At("Briefing", panel, 0f, 0f, Width, Height);
        briefing = part.gameObject;
        thisRunHeading = Heading("This run", part, Margin, 0f, ColumnWidth, style);
        hourChip = new Chip("Hour", part, style);
        moodChip = new Chip("Forest", part, style);
        Hover(hourChip.Root, () => tooltip.Show("The hour", style.Accent, hourDetail));
        Hover(moodChip.Root, () => tooltip.Show("The forest", style.Accent, moodDetail));
        dangersHeading = Heading("Known dangers", part, Margin, 0f, ColumnWidth, style);
        dangersLabel = Paragraph("Dangers", part, Margin, 0f, ColumnWidth, 30f, 18f, style.Text);
        findsHeading = Heading("Likely finds", part, Margin, 0f, ColumnWidth, style);
    }

    private void BuildPreparation(UIStyle style)
    {
        RectTransform part = At("Preparation", panel, 0f, 0f, Width, Height);
        preparation = part.gameObject;
        tierHeading = Heading("Difficulty", part, RightColumn, 0f, ColumnWidth, style);
        gearLabel = Label("Gear", part, RightColumn, 0f, ColumnWidth, 46f, 18f, style.Muted, TextAlignmentOptions.MidlineRight);
        tierBriefing = Paragraph("Tier Briefing", part, RightColumn, 0f, ColumnWidth, 30f, 17f, style.Muted);

        sparksHeading = Heading("Extra time", part, RightColumn, 0f, ColumnWidth, style);
        sparksRowRect = At("Sparks", part, RightColumn, 0f, ColumnWidth, 46f);
        sparksRow = sparksRowRect.gameObject;
        AddButton("Fewer", sparksRowRect, 0f, 0f, 46f, UIRole.Kind.Button, () => Change(() => plan.StepSparks(-1)), out fewerSparks).text = "−";
        sparksValue = Label("Value", sparksRowRect, 50f, 0f, 76f, 46f, 20f, style.Text, TextAlignmentOptions.Center);
        AddButton("More", sparksRowRect, 130f, 0f, 46f, UIRole.Kind.Button, () => Change(() => plan.StepSparks(1)), out moreSparks).text = "+";
        sparksAdded = Label("Added", sparksRowRect, 190f, 0f, 100f, 46f, 20f, style.Good);
        RectTransform owned = At("Owned", sparksRowRect, ColumnWidth - 210f, 0f, 210f, 46f);
        owned.gameObject.AddComponent<Image>().color = Color.clear;
        sparksIcon = At("Icon", owned, 0f, 8f, 30f, 30f).gameObject.AddComponent<Image>();
        sparksIcon.raycastTarget = false;
        sparksOwned = Label("Label", owned, 38f, 0f, 172f, 46f, 18f, style.Text);
        Hover(owned.gameObject, () => tooltip.ShowDefinition(SettlementSettings.Current.PortalSpark));

        sizeHeading = Heading("Size", part, RightColumn, 0f, ColumnWidth, style);
        sizeRowRect = At("Size", part, RightColumn, 0f, ColumnWidth, 46f);
        sizeRow = sizeRowRect.gameObject;
        AddButton("Change", sizeRowRect, 0f, 0f, 150f, UIRole.Kind.Button, () => Change(plan.ToggleSize), out Button sizeButton);
        sizeName = sizeButton.GetComponentInChildren<TMP_Text>();
        sizeDetail = Label("Detail", sizeRowRect, 164f, 0f, ColumnWidth - 164f, 46f, 16f, style.Muted);

        companionHeading = Heading("Companion", part, RightColumn, 0f, ColumnWidth, style);
        companionRowRect = At("Companion", part, RightColumn, 0f, ColumnWidth, 46f);
        companionRow = companionRowRect.gameObject;
        AddButton("Previous", companionRowRect, 0f, 0f, 46f, UIRole.Kind.Button, () => Change(() => plan.StepCompanion(-1)), out Button previous).text = "<";
        companionName = Label("Name", companionRowRect, 50f, 0f, 230f, 46f, 20f, style.Text, TextAlignmentOptions.Center);
        AddButton("Next", companionRowRect, 284f, 0f, 46f, UIRole.Kind.Button, () => Change(() => plan.StepCompanion(1)), out Button next).text = ">";
        companionBond = Label("Bond", companionRowRect, 340f, 0f, 216f, 46f, 17f, style.Muted, TextAlignmentOptions.MidlineRight);
        companionRisk = Paragraph("Risk", part, RightColumn, 0f, ColumnWidth, 26f, 15f, style.Muted);

        modifiersHeading = Heading("Raid modifiers", part, RightColumn, 0f, ColumnWidth, style);
        modifiersNote = Label("Note", part, RightColumn, 0f, ColumnWidth, 26f, 16f, style.Muted, TextAlignmentOptions.MidlineRight);
        modifiersNote.text = "One of each is used up on entry";
        noModifiers = Paragraph("None", part, RightColumn, 0f, ColumnWidth, 30f, 17f, style.Muted);
        modifierView = At("Modifier List", part, RightColumn, 0f, ColumnWidth, 150f);
        modifierView.gameObject.AddComponent<Image>().color = Color.clear;
        modifierView.gameObject.AddComponent<RectMask2D>();
        modifierContent = At("Content", modifierView, 0f, 0f, ColumnWidth, 0f);
        var scroll = modifierView.gameObject.AddComponent<ScrollRect>();
        scroll.content = modifierContent;
        scroll.viewport = modifierView;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;
    }

    private void BuildStats(UIStyle style)
    {
        RectTransform part = At("Rules", panel, 0f, 0f, Width, Height);
        stats = part.gameObject;
        Hairline(part, Margin, StatsTop, Width - Margin * 2f, 1f, style);
        float width = (Width - Margin * 2f) / figures.Length;
        for (int i = 0; i < figures.Length; i++)
        {
            float x = Margin + i * width;
            figures[i] = Label(FigureNames[i] + " Value", part, x, StatsTop + 6f, width, 40f, 28f, style.Text);
            // Hearthline 3: each figure's name with its icon.
            TMP_Text name = Label(FigureNames[i] + " Label", part, x, StatsTop + 44f, width, 24f, 16f, style.Muted);
            name.richText = true;
            name.text = HearthKit.InlineIcon(FigureIcons[i]) + " " + FigureNames[i];
        }

        Hairline(part, Margin, FooterTop - 8f, Width - Margin * 2f, 1f, style);
    }

    private void BuildFooter(UIStyle style)
    {
        const float enter = 190f;
        const float close = 130f;
        const float gap = 12f;
        float buttons = FooterTop + 14f;
        float closeAt = Width - Margin - enter - gap - close;
        riskLabel = Paragraph("Risk", panel, Margin, FooterTop, closeAt - Margin - 24f, 72f, 17f, style.Muted);
        riskLabel.raycastTarget = true;
        Hover(riskLabel.gameObject, () =>
        {
            if (riskDetail != null)
            {
                tooltip.Show("What can be lost", style.Warning, riskDetail);
            }
        });
        AddButton("Close Button", panel, closeAt, buttons, close, UIRole.Kind.Button, Close, out _).text = "Close";
        enterLabel = AddButton("Enter Button", panel, Width - Margin - enter, buttons, enter, UIRole.Kind.PrimaryButton, Enter, out enterButton);
    }

    // Laid over the content, not the panel, so it stays at full size when a small screen shrinks the panel.
    private void BuildTooltip(UIStyle style)
    {
        var holder = new GameObject("Tooltip Holder", typeof(RectTransform));
        holder.SetActive(false);
        holder.transform.SetParent(content, false);
        RectTransform tip = Box("Tooltip", content, new Vector2(0.5f, 0.5f), new Vector2(380f, 100f), Vector2.zero);
        tip.pivot = new Vector2(0f, 1f);
        tip.gameObject.AddComponent<Image>().raycastTarget = false;
        tip.gameObject.AddComponent<Outline>().effectDistance = new Vector2(1f, -1f);
        tip.gameObject.AddComponent<UIRole>().Set(UIRole.Kind.Window);
        var layout = tip.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(14, 14, 10, 12);
        layout.spacing = 4f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        tip.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        TMP_Text title = Paragraph("Title", tip, 0f, 0f, 352f, 28f, 19f, style.Text);
        style.Title(title);
        TMP_Text body = Paragraph("Body", tip, 0f, 0f, 352f, 28f, 16f, style.Text);
        tooltip = holder.AddComponent<ItemTooltipView>();
        tooltip.Configure(tip, title, body);
        holder.SetActive(true);
    }

    private void Hover(GameObject target, Action entered)
    {
        var hover = target.AddComponent<UIHover>();
        hover.Entered = entered;
        hover.Exited = () =>
        {
            if (tooltip != null)
            {
                tooltip.Hide();
            }
        };
    }

    private Tab AddTab(int index)
    {
        UIStyle style = UIStyle.Current;
        const float width = 168f;
        // Hearthline 3: a framed tab as everywhere else (KitTabs), a little see-through over the painting; the one shown in
        // its bright rim.
        RectTransform rect = At($"Destination {index}", panel, 0f, 48f, width - 8f, 40f);
        Image fill = HearthKit.Sliced(rect.gameObject.AddComponent<Image>(), style.Card, 1.4f);
        fill.color = HearthKit.Faded(style.Inset, 0.9f);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = fill;
        HearthKit.Tint(button);
        UIHighlight.On(rect.gameObject);
        button.onClick.AddListener(() => Change(() => plan.PickDestination(index)));
        HearthKit.Rim(rect, 0.3f, 1.4f);
        TMP_Text label = Label("Label", rect, 0f, 0f, width - 8f, 40f, 19f, style.Text, TextAlignmentOptions.Center);
        style.Title(label);
        Image mark = HearthKit.Rim(rect, 1f, 1.4f);
        return new Tab { Rect = rect, Label = label, Mark = mark.gameObject };
    }

    private TierButton AddTierButton(int index)
    {
        UIStyle style = UIStyle.Current;
        const float width = 64f;
        var entry = new TierButton();
        entry.Label = AddButton($"Tier {index}", preparation.transform, RightColumn + index * (width + 6f), 0f, width, UIRole.Kind.Button, () => Change(() => plan.PickTier(entry.Tier)), out entry.Button);
        RectTransform mark = At("Mark", entry.Button.transform, 0f, 44f, width, 2f);
        var line = mark.gameObject.AddComponent<Image>();
        line.color = style.Accent;
        line.raycastTarget = false;
        entry.Mark = mark.gameObject;
        Hover(entry.Button.gameObject, () => tooltip.Show($"Tier {Tiers.ToRoman(entry.Tier.tier)}", Tiers.ColorOf(entry.Tier.tier), DescribeTierNeeds(entry.Tier)));
        return entry;
    }

    private static string DescribeTierNeeds(RaidDifficulty tier) =>
        (RaidLauncher.CheckWard(tier) is string ward ? ward + " " : "") + (tier.minimumGearScore > 0
            ? $"Needs a gear score of {tier.minimumGearScore}. {tier.recommendedGearScore} is recommended."
            : $"Open to anyone. A gear score of {tier.recommendedGearScore} is recommended.");

    private RumourLine AddRumourLine()
    {
        UIStyle style = UIStyle.Current;
        RectTransform rect = At("Rumour", briefing.transform, Margin, 0f, ColumnWidth, 30f);
        var mark = At("Mark", rect, 1f, 9f, 7f, 7f).gameObject.AddComponent<Image>();
        mark.color = style.Accent;
        mark.raycastTarget = false;
        mark.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
        TMP_Text label = Paragraph("Label", rect, 16f, 0f, ColumnWidth - 16f, 30f, 18f, style.Text);
        return new RumourLine { Rect = rect, Label = label };
    }

    private FindSlot AddFindSlot()
    {
        RectTransform rect = At("Find", briefing.transform, Margin, 0f, FindSize, FindSize);
        rect.gameObject.AddComponent<Image>();
        rect.gameObject.AddComponent<Outline>().effectDistance = new Vector2(1f, -1f);
        rect.gameObject.AddComponent<UIRole>().Set(UIRole.Kind.Slot);
        var icon = At("Icon", rect, 4f, 4f, FindSize - 8f, FindSize - 8f).gameObject.AddComponent<Image>();
        icon.raycastTarget = false;
        var slot = new FindSlot { Rect = rect, Icon = icon };
        Hover(rect.gameObject, () => tooltip.ShowDefinition(slot.Definition));
        return slot;
    }

    private ModifierRow AddModifierRow(int index)
    {
        UIStyle style = UIStyle.Current;
        RectTransform rect = At($"Modifier {index}", modifierContent, 0f, index * RowHeight, ColumnWidth, RowHeight - 4f);
        var row = new ModifierRow { Root = rect.gameObject };
        var image = row.Root.AddComponent<Image>();
        row.Button = row.Root.AddComponent<Button>();
        row.Button.targetGraphic = image;
        row.Button.onClick.AddListener(() => Toggle(row.Item));
        var mark = At("Mark", rect, 0f, 0f, 3f, RowHeight - 4f).gameObject.AddComponent<Image>();
        mark.color = style.Accent;
        mark.raycastTarget = false;
        row.Mark = mark.gameObject;
        row.Icon = At("Icon", rect, 12f, 6f, 40f, 40f).gameObject.AddComponent<Image>();
        row.Icon.raycastTarget = false;
        row.Name = Label("Name", rect, 64f, 3f, ColumnWidth - 150f, 26f, 18f, style.Text);
        row.Effect = Label("Effect", rect, 64f, 27f, ColumnWidth - 150f, 22f, 15f, style.Muted);
        row.State = Label("State", rect, ColumnWidth - 86f, 0f, 74f, RowHeight - 4f, 17f, style.Text, TextAlignmentOptions.MidlineRight);
        return row;
    }
}
