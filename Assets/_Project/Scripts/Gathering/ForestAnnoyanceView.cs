using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The forest annoyance meter on the raid HUD: a thin bar at the top of the screen, tinted by level, with the level's
/// name. Built in code under the scene's UIRoot by <see cref="Attach"/> (ForestAnnoyanceDriver calls it); hidden while
/// the forest is calm and the meter empty.
/// </summary>
public sealed class ForestAnnoyanceView : MonoBehaviour
{
    private static readonly Color StirringColor = new Color(0.95f, 0.8f, 0.2f, 0.95f);
    private static readonly Color AngryColor = new Color(1f, 0.45f, 0.2f, 0.95f);
    private static readonly Color CalmColor = new Color(0.55f, 0.8f, 0.55f, 0.95f);

    private ForestAnnoyanceDriver driver;
    private GameObject panel;
    private RectTransform fill;
    private Image fillImage;
    private TMP_Text label;

    public static ForestAnnoyanceView Attach(ForestAnnoyanceDriver driver)
    {
        if (driver == null || UIRoot.Current == null)
        {
            return null;
        }

        var root = new GameObject("Forest Annoyance", typeof(RectTransform));
        root.transform.SetParent(UIRoot.Current.Canvas.transform, false);
        var rect = (RectTransform)root.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        root.AddComponent<Canvas>();
        ForestAnnoyanceView view = root.AddComponent<ForestAnnoyanceView>();
        view.Build(driver);
        return view;
    }

    private void Update()
    {
        ForestAnnoyance annoyance = driver != null ? driver.Annoyance : null;
        bool visible = annoyance != null && annoyance.Value > 0f;
        panel.SetActive(visible);
        if (!visible)
        {
            return;
        }

        fill.anchorMax = new Vector2(annoyance.Fill, 1f);
        switch (annoyance.Level)
        {
            case AnnoyanceLevel.Calm:
                fillImage.color = CalmColor;
                label.text = "The forest is watching";
                break;
            case AnnoyanceLevel.Stirring:
                fillImage.color = StirringColor;
                label.text = "The forest stirs";
                break;
            default:
                fillImage.color = AngryColor;
                label.text = "The forest is angry";
                break;
        }
    }

    private void Build(ForestAnnoyanceDriver owner)
    {
        driver = owner;
        panel = new GameObject("Panel", typeof(RectTransform));
        var panelRect = (RectTransform)panel.transform;
        panelRect.SetParent(transform, false);
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.sizeDelta = new Vector2(320f, 10f);
        panelRect.anchoredPosition = new Vector2(0f, -110f);

        Image background = panel.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.6f);
        background.raycastTarget = false;

        var fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill = (RectTransform)fillObject.transform;
        fill.SetParent(panelRect, false);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(0f, 1f);
        fill.offsetMin = fill.offsetMax = Vector2.zero;
        fillImage = fillObject.GetComponent<Image>();
        fillImage.raycastTarget = false;

        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(panelRect, false);
        label = labelObject.GetComponent<TextMeshProUGUI>();
        label.fontSize = 18f;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        label.rectTransform.sizeDelta = new Vector2(320f, 24f);
        label.rectTransform.anchoredPosition = new Vector2(0f, -16f);

        panel.SetActive(false);
    }
}
