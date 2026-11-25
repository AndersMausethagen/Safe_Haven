using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// What is at work on the player, above the vitals, one line each with its icon: meals and tonics with their time left (their own item's
/// icon), other boons (ConditionIconSettings), and poison, chill and roots in their own colour (StatusEffectDefinition's icon; GDD §10.6).
/// A line without an icon keeps its words alone. Builds its own lines on the PlayerVitalsView it sits on and only reads.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerConditionsView : MonoBehaviour
{
    private const float RefreshSeconds = 0.25f;
    private const string PanelName = "Vitals Panel";

    private const float FontSize = 19f;
    private const float LineHeight = 26f;
    private const float IconSize = 22f;
    private const float IconGap = 6f;

    private static readonly Vector2 Offset = new Vector2(6f, 8f);
    private static Color BoonColor => UIStyle.Current.Accent;

    private sealed class Row
    {
        public GameObject Root;
        public Image Icon;
        public TMP_Text Label;
    }

    private readonly List<Row> rows = new List<Row>();
    private RectTransform holder;
    private float nextRefresh;
    private int used;

    private void Start()
    {
        // Stacked up from the top of the vitals panel, which sits at the bottom of the screen.
        Transform panel = transform.Find(PanelName);
        var go = new GameObject("Conditions", typeof(RectTransform));
        holder = (RectTransform)go.transform;
        holder.SetParent(panel != null ? panel : transform, false);
        holder.anchorMin = new Vector2(0f, 1f);
        holder.anchorMax = new Vector2(1f, 1f);
        holder.pivot = new Vector2(0f, 0f);
        holder.anchoredPosition = Offset;
        holder.sizeDelta = new Vector2(0f, 110f);
    }

    private void Update()
    {
        if (holder == null || Time.unscaledTime < nextRefresh)
        {
            return;
        }

        nextRefresh = Time.unscaledTime + RefreshSeconds;
        used = 0;
        Player player = Player.Current;
        if (player != null)
        {
            if (player.TryGetComponent(out StatusEffects effects))
            {
                foreach (StatusEffects.Active effect in effects.Current)
                {
                    Line(effect.Definition.Icon, effect.Definition.DisplayName, effect.SecondsLeft, effect.Definition.Color);
                }
            }

            if (player.Boons != null)
            {
                ConditionIconSettings icons = ConditionIconSettings.Current;
                List<BoonRecord> boons = player.Boons.Capture();
                foreach (BoonRecord boon in boons)
                {
                    Line(icons.ForBoon(boon.Id), boon.Name, boon.SecondsLeft, BoonColor);
                }
            }

            // A skill's buff (a war cry, a blessing) for as long as it lasts, with the skill's own icon (the user, 2026-11-25).
            if (player.Skills != null)
            {
                foreach (KeyValuePair<SkillDefinition, float> buff in player.Skills.ActiveBuffs)
                {
                    float left = buff.Value - Time.time;
                    if (left > 0f)
                    {
                        Line(buff.Key.Icon, buff.Key.DisplayName, left, BoonColor);
                    }
                }
            }
        }

        for (int i = used; i < rows.Count; i++)
        {
            if (rows[i].Root.activeSelf)
            {
                rows[i].Root.SetActive(false);
            }
        }
    }

    // Lines stack up from just above the vitals: status effects first, so the dangerous ones sit nearest the health bar.
    private void Line(Sprite icon, string name, float seconds, Color color)
    {
        Row row = used < rows.Count ? rows[used] : AddRow();
        used++;
        int fromBottom = used - 1;
        ((RectTransform)row.Root.transform).anchoredPosition = new Vector2(0f, fromBottom * LineHeight);
        if (!row.Root.activeSelf)
        {
            row.Root.SetActive(true);
        }

        row.Icon.sprite = icon;
        row.Icon.enabled = icon != null;
        var label = (RectTransform)row.Label.transform;
        label.anchoredPosition = new Vector2(icon != null ? IconSize + IconGap : 0f, 0f);
        string text = $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{name}  {RaidStats.FormatTime(seconds)}</color>";
        if (row.Label.text != text)
        {
            row.Label.text = text;
        }
    }

    private Row AddRow()
    {
        var root = new GameObject("Condition", typeof(RectTransform));
        var rect = (RectTransform)root.transform;
        rect.SetParent(holder, false);
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.sizeDelta = new Vector2(0f, LineHeight);

        var iconObject = new GameObject("Icon", typeof(RectTransform));
        var iconRect = (RectTransform)iconObject.transform;
        iconRect.SetParent(rect, false);
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.sizeDelta = new Vector2(IconSize, IconSize);
        var image = iconObject.AddComponent<Image>();
        image.preserveAspect = true;
        image.raycastTarget = false;

        var labelObject = new GameObject("Label", typeof(RectTransform));
        var labelRect = (RectTransform)labelObject.transform;
        labelRect.SetParent(rect, false);
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.pivot = new Vector2(0f, 0f);
        labelRect.sizeDelta = Vector2.zero;
        var label = labelObject.AddComponent<TextMeshProUGUI>();
        label.fontSize = FontSize;
        label.alignment = TextAlignmentOptions.BottomLeft;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;

        var row = new Row { Root = root, Icon = image, Label = label };
        rows.Add(row);
        return row;
    }
}
