using TMPro;
using UnityEngine;

/// <summary>
/// The floating mark over a monster that tells the player what it knows: a "?" that brightens as a suspicious monster
/// grows surer (pale while it searches for a target it lost), and a "!" when it has spotted its target. MonsterAI
/// creates it the first time the monster notices anything; look and timing are in PerceptionSettings › Presentation,
/// the font is the quest markers' so both read alike.
/// </summary>
[DisallowMultipleComponent]
public sealed class AwarenessIndicator : MonoBehaviour
{
    private MonsterAI monster;
    private TextMeshPro text;
    private float spottedUntil = -1f;

    public static AwarenessIndicator CreateFor(MonsterAI monster)
    {
        var go = new GameObject("Awareness Indicator");
        go.transform.SetParent(monster.transform, false);
        go.transform.localPosition = Vector3.up * PerceptionSettings.Current.IndicatorHeight;
        var indicator = go.AddComponent<AwarenessIndicator>();
        indicator.Build(monster);
        return indicator;
    }

    private void Build(MonsterAI owner)
    {
        monster = owner;
        QuestMarkerSettings look = QuestMarkerSettings.Current;
        text = gameObject.AddComponent<TextMeshPro>();
        if (look.Font != null)
        {
            text.font = look.Font;
        }

        if (look.FontMaterial != null)
        {
            text.fontSharedMaterial = look.FontMaterial;
        }

        text.fontSize = PerceptionSettings.Current.IndicatorFontSize;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.rectTransform.sizeDelta = new Vector2(2f, 2f);
        text.outlineWidth = look.OutlineWidth;
        text.outlineColor = Color.black;
        text.enabled = false;
    }

    /// <summary>Shows the "!" for a moment (the monster has just spotted its target).</summary>
    public void ShowSpotted()
    {
        spottedUntil = Time.time + PerceptionSettings.Current.SpottedShowSeconds;
    }

    private void LateUpdate()
    {
        PerceptionSettings rules = PerceptionSettings.Current;
        bool visible = rules.ShowIndicators && monster != null && monster.CurrentState != MonsterAI.State.Dead;
        if (visible && monster.IsRoutedNow)
        {
            // Beaten: running from its broken pack (Docs/EnemyAIReview_2026-11-19.md §3).
            Show("\u25BC", new Color(0.7f, 0.7f, 0.7f, 0.85f), 0.8f);
        }
        else if (visible && Time.time < spottedUntil)
        {
            Show("!", rules.SpottedColor);
        }
        else if (visible && monster.Senses != null
                 && (monster.CurrentState == MonsterAI.State.Investigating || monster.Senses.Stage == AwarenessStage.Suspicious))
        {
            // From the suspicious level up to spotted, the "?" grows from half to full strength.
            float sure = Mathf.InverseLerp(rules.SuspiciousAt, 1f, monster.Senses.Awareness.Level);
            Color color = monster.IsSearchingLostTarget ? rules.SearchingColor : rules.SuspiciousColor;
            color.a = Mathf.Lerp(0.5f, 1f, sure);
            Show("?", color);
        }
        else if (visible && rules.ShowStealthReadout && Player.Current != null && Player.Current.Health != null
                 && monster.NoticeLevelOf(Player.Current.Health) > NoticingShownFrom)
        {
            // Before the "?": a faint one forming over it as it notices the player, from nothing to the suspicious line.
            float filled = Mathf.Clamp01(monster.NoticeLevelOf(Player.Current.Health) / Mathf.Max(0.01f, rules.SuspiciousAt));
            Color color = rules.NoticingColor;
            color.a *= Mathf.Lerp(0.25f, 1f, filled);
            Show("?", color, Mathf.Lerp(0.55f, 0.85f, filled));
        }
        else
        {
            text.enabled = false;
            return;
        }

        Camera view = Camera.main;
        if (view != null)
        {
            transform.rotation = view.transform.rotation;
        }
    }

    // Below this the faint "?" isn't shown: a glance that came to nothing.
    private const float NoticingShownFrom = 0.03f;

    private void Show(string symbol, Color color, float size = 1f)
    {
        text.enabled = true;
        text.text = symbol;
        text.color = color;
        text.fontSize = PerceptionSettings.Current.IndicatorFontSize * size;
    }
}
