using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// What is at work on the player, in words above the vitals: meals and tonics with their time left, poison, chill and
/// roots in their own colour (GDD §10.6 asks for distinct status icons; words until they are drawn). Builds its own label
/// on the PlayerVitalsView it sits on and only reads.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerConditionsView : MonoBehaviour
{
    private const float RefreshSeconds = 0.25f;
    private const string PanelName = "Vitals Panel";

    private const float FontSize = 19f;

    private static readonly Vector2 Offset = new Vector2(6f, 8f);
    private static Color BoonColor => UIStyle.Current.Accent;

    private readonly StringBuilder text = new StringBuilder();
    private TMP_Text label;
    private float nextRefresh;

    private void Start()
    {
        // Stacked up from the top of the vitals panel, which sits at the bottom of the screen.
        Transform panel = transform.Find(PanelName);
        var holder = new GameObject("Conditions", typeof(RectTransform));
        var labelRect = (RectTransform)holder.transform;
        labelRect.SetParent(panel != null ? panel : transform, false);
        labelRect.anchorMin = new Vector2(0f, 1f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.pivot = new Vector2(0f, 0f);
        labelRect.anchoredPosition = Offset;
        labelRect.sizeDelta = new Vector2(0f, 110f);
        label = holder.AddComponent<TextMeshProUGUI>();
        label.fontSize = FontSize;
        label.alignment = TextAlignmentOptions.BottomLeft;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
    }

    private void Update()
    {
        if (label == null || Time.unscaledTime < nextRefresh)
        {
            return;
        }

        nextRefresh = Time.unscaledTime + RefreshSeconds;
        Player player = Player.Current;
        text.Clear();
        if (player != null)
        {
            if (player.TryGetComponent(out StatusEffects effects))
            {
                foreach (StatusEffects.Active effect in effects.Current)
                {
                    Line(effect.Definition.DisplayName, effect.SecondsLeft, effect.Definition.Color);
                }
            }

            if (player.Boons != null)
            {
                List<BoonRecord> boons = player.Boons.Capture();
                foreach (BoonRecord boon in boons)
                {
                    Line(boon.Name, boon.SecondsLeft, BoonColor);
                }
            }
        }

        string shown = text.ToString();
        if (label.text != shown)
        {
            label.text = shown;
        }
    }

    private void Line(string name, float seconds, Color color)
    {
        if (text.Length > 0)
        {
            text.Append('\n');
        }

        text.Append($"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{name}  {RaidStats.FormatTime(seconds)}</color>");
    }
}
