using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The noise meter on the HUD: a thin bar tinted by how loud the player is, with the word and how many metres they can be
/// heard, and whether they are sneaking. Built in code under the scene's UIRoot by <see cref="Attach"/> (PlayerNoise calls
/// it); shown wherever something can hear, so during raids and near any monster, and not at a quiet home. It only reads.
/// </summary>
public sealed class NoiseMeterView : MonoBehaviour
{
    private PlayerNoise noise;
    private GameObject panel;
    private RectTransform fill;
    private Image fillImage;
    private TMP_Text label;
    private NoiseBand shownBand = (NoiseBand)(-1);
    private int shownMetres = -1;
    private bool shownSneaking;

    public static NoiseMeterView Attach(PlayerNoise noise)
    {
        if (noise == null || UIRoot.Current == null)
        {
            return null;
        }

        var root = new GameObject("Noise Meter", typeof(RectTransform));
        root.transform.SetParent(UIRoot.Current.Canvas.transform, false);
        var rect = (RectTransform)root.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        root.AddComponent<Canvas>();
        NoiseMeterView view = root.AddComponent<NoiseMeterView>();
        view.Build(noise);
        return view;
    }

    private void Update()
    {
        bool visible = noise != null && (RaidDirector.Current != null || MonsterAI.Active.Count > 0);
        if (panel.activeSelf != visible)
        {
            panel.SetActive(visible);
        }

        if (!visible)
        {
            return;
        }

        float level = noise.Level;
        fill.anchorMax = new Vector2(Mathf.Clamp01(level / PerceptionSettings.Current.NoiseMeterMax), 1f);
        NoiseBand band = noise.Band;
        int metres = Mathf.Max(1, Mathf.RoundToInt(level));
        bool sneaking = noise.IsSneaking;
        if (band == shownBand && metres == shownMetres && sneaking == shownSneaking)
        {
            return;
        }

        shownBand = band;
        shownMetres = metres;
        shownSneaking = sneaking;
        fillImage.color = ColorOf(band);
        string word = band == NoiseBand.Silent ? "Silent" : $"{band}  {metres} m";
        label.text = sneaking ? "Sneaking  ·  " + word : word;
    }

    private static Color ColorOf(NoiseBand band)
    {
        switch (band)
        {
            case NoiseBand.Deafening: return UIStyle.Current.Danger;
            case NoiseBand.Loud: return UIStyle.Current.Warning;
            case NoiseBand.Audible: return UIStyle.Current.Accent;
            default: return UIStyle.Current.Good;
        }
    }

    private void Build(PlayerNoise owner)
    {
        noise = owner;
        panel = new GameObject("Panel", typeof(RectTransform));
        var panelRect = (RectTransform)panel.transform;
        panelRect.SetParent(transform, false);
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.sizeDelta = new Vector2(220f, 3f);
        panelRect.anchoredPosition = new Vector2(0f, 208f);

        Image background = panel.AddComponent<Image>();
        background.color = UIStyle.Current.Track;
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
        UIStyle.Current.OnWorld(label);
        label.fontSize = 17f;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        label.rectTransform.sizeDelta = new Vector2(320f, 24f);
        label.rectTransform.anchoredPosition = new Vector2(0f, 14f);

        panel.SetActive(false);
    }
}
