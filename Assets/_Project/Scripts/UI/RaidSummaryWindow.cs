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

    private void Awake()
    {
        continueButton.onClick.AddListener(window.Close);
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
        bodyLabel.text = RaidReportText.Describe(report);
        window.Open();
    }
}
