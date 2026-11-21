using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// The report shown on getting back to Safe Haven from a raid: whether the player made it out, how long it took, and what
/// they brought home or lost. It opens by itself when the GameSession still holds a report of the last raid, which is when the
/// full-screen results (RaidResultsScreen) were skipped.
/// </summary>
public sealed class RaidSummaryWindow : MonoBehaviour
{
    [SerializeField] private UIWindow window;
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private TMP_Text subtitleLabel;
    [Tooltip("Lists the loot and losses with rich text.")]
    [SerializeField] private TMP_Text bodyLabel;
    [SerializeField] private Button continueButton;
    [SerializeField] private Color successColor = new Color(0.55f, 0.92f, 0.55f);
    [SerializeField] private Color failureColor = new Color(1f, 0.42f, 0.36f);

    // Hearthline 3: the haul as pictures between the subtitle and the words (RaidReportCards); the panel grows to hold them.
    private const float CardSize = 64f;
    private const float ScreenMargin = 80f;
    private const float MinWords = 120f;
    // The raid's own results painting across the top, the title and subtitle on it (LocalSpawn art, as every window).
    private const float BannerHeight = 196f;
    private const float BannerKeep = 100f;
    private KitBanner banner;
    private const float Inset = 32f;
    private KitHaul first;
    private KitHaul second;
    private KitCardRow lessons;
    private RectTransform panel;
    private float panelHeight;
    private float bodyTop;
    private float bodyHeight;

    private void Awake()
    {
        continueButton.onClick.AddListener(window.Close);
    }

    private void MakeHaul()
    {
        if (first != null)
        {
            return;
        }

        panel = (RectTransform)titleLabel.transform.parent;
        panelHeight = panel.sizeDelta.y;
        bodyTop = -bodyLabel.rectTransform.offsetMax.y;
        bodyHeight = bodyLabel.rectTransform.rect.height;
        RectTransform area = CodeUI.At("Haul", panel, Inset, bodyTop - 6f, 0f, 0f);
        area.anchorMax = new Vector2(1f, 1f);
        area.sizeDelta = new Vector2(-Inset * 2f, 0f);
        first = new KitHaul(area, "First", 0f, CardSize);
        second = new KitHaul(area, "Second", 0f, CardSize);
        // What was learned, a card a skill (it was a list of lines in the words below).
        lessons = new KitCardRow(area, "Lessons", 112f, 64f);
    }

    // After the haul's area is made, so that it moves down under the painting with the divider.
    private float ShowBanner(RaidReport report)
    {
        Sprite art = report.Definition != null ? report.Definition.ResultArtwork(report.Extracted) : null;
        banner = KitBanner.Show(panel, art, BannerHeight, BannerKeep);
        if (banner == null)
        {
            return 0f;
        }

        // The title is centred over it: an even veil, not the left-hand shade alone.
        banner.Darken(0.35f);
        return banner.Growth;
    }

    private void Start()
    {
        RaidReport report = GameSession.TakeReport();
        if (report != null)
        {
            Show(report);
        }
    }

    public void Show(RaidReport report)
    {
        titleLabel.text = RaidReportText.Title(report);
        titleLabel.color = report.Extracted ? successColor : failureColor;
        subtitleLabel.text = RaidReportText.Subtitle(report);
        MakeHaul();
        float painting = ShowBanner(report);
        // The window stays on the screen: the lessons' cards only where there is room for them (else they stay in the words).
        var screen = (RectTransform)panel.GetComponentInParent<Canvas>().rootCanvas.transform;
        float room = screen.rect.height - ScreenMargin - panelHeight - painting + bodyHeight - MinWords;
        float haul = RaidReportCards.Show(report, first, second, lessons, 0f, room, out bool lessonsShown) + 10f;
        string words = RaidReportText.Describe(report, true, lessonsShown);
        bodyLabel.text = words;
        // The words take what they need, no more: a short report leaves no field of empty leather under the pictures.
        float need = words.Length > 0 ? bodyLabel.GetPreferredValues(words, bodyLabel.rectTransform.rect.width, 0f).y + 8f : 0f;
        float spare = Mathf.Max(0f, bodyHeight - need);
        panel.sizeDelta = new Vector2(panel.sizeDelta.x, panelHeight + painting + haul - spare);
        bodyLabel.rectTransform.offsetMax = new Vector2(bodyLabel.rectTransform.offsetMax.x, -(bodyTop + painting + haul));
        window.Open();
    }
}
