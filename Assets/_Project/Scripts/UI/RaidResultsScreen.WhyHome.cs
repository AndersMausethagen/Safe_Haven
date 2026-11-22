using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Playtests only (Docs/RaidLoopResearch_2026-11-22.md #1): "Why home?", five pictures, one tap, logged as a WhyHome row. Seventeen
/// raids from one player couldn't separate why raids ended early; one tap a raid can. Off in a release build, like the F1 menu
/// (CheatSettings › In Release Builds), and when runs aren't logged. Over the artwork's lower left, away from the report.
/// </summary>
public sealed partial class RaidResultsScreen
{
    private static readonly (string id, string name, string icon)[] Reasons =
    {
        ("bag_full", "Bag full", "Bags"),
        ("done", "Did what I came for", "Star"),
        ("too_dangerous", "Too dangerous", "Skull"),
        ("nothing_left", "Nothing more to find", "Eye"),
        ("needed_at_home", "Needed at home", "House"),
    };

    private const float ReasonWidth = 92f;
    private const float ReasonPicture = 64f;
    private RectTransform whyHome;

    private static bool AsksWhyHome => CheatSettings.Current != null && CheatSettings.Current.IsAvailable
                                       && PlaytestSettings.Current != null && PlaytestSettings.Current.LogRuns;

    private void AskWhyHome()
    {
        if (!AsksWhyHome || whyHome != null || content == null || content.transform.parent == null)
        {
            return;
        }

        UIStyle style = UIStyle.Current;
        float width = Reasons.Length * (ReasonWidth + 8f) + 24f;
        float height = ReasonPicture + 40f + 56f;
        whyHome = CodeUI.Box("Why Home", content.transform.parent, new Vector2(0.03f, 0.07f), new Vector2(width, height), Vector2.zero);
        whyHome.pivot = Vector2.zero;
        HearthKit.Card(whyHome, "Back", 0f, 0f, width, height, 0.5f, HearthKit.Faded(style.Panel, 0.92f));
        TMP_Text title = CodeUI.Label("Title", whyHome, 12f, 8f, width - 24f, 28f, 20f, style.AccentBright, TextAlignmentOptions.Left);
        title.text = "Why home?  <size=70%><color=" + style.MutedHex + ">playtest: one tap</color></size>";
        for (int i = 0; i < Reasons.Length; i++)
        {
            (string id, string name, string icon) = Reasons[i];
            var card = new KitPictureCard(whyHome, name, ReasonWidth, ReasonPicture, 2, 13f, null, null);
            card.Root.anchoredPosition = new Vector2(12f + i * (ReasonWidth + 8f), -40f);
            card.ShowPicture(null, icon);
            card.SetName(name);
            var button = card.Root.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => ChooseWhyHome(id, name));
        }
    }

    private void ChooseWhyHome(string id, string name)
    {
        PlaytestLogger.Note(PlaytestLog.WhyHome, id, 1, name);
        if (whyHome != null)
        {
            Destroy(whyHome.gameObject);
            whyHome = null;
        }
    }
}
