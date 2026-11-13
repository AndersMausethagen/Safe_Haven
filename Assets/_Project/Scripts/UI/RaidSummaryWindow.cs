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
    private const float Inset = 32f;
    private KitHaul first;
    private KitHaul second;
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
        float haul = RaidReportCards.Show(report, first, second, 0f) + 10f;
        string words = RaidReportText.Describe(report, true);
        bodyLabel.text = words;
        // The words take what they need, no more: a short report leaves no field of empty leather under the pictures.
        float need = words.Length > 0 ? bodyLabel.GetPreferredValues(words, bodyLabel.rectTransform.rect.width, 0f).y + 8f : 0f;
        float spare = Mathf.Max(0f, bodyHeight - need);
        panel.sizeDelta = new Vector2(panel.sizeDelta.x, panelHeight + haul - spare);
        bodyLabel.rectTransform.offsetMax = new Vector2(bodyLabel.rectTransform.offsetMax.x, -(bodyTop + haul));
        window.Open();
    }
}
