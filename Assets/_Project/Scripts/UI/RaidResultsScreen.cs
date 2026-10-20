using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// The full-screen raid results, in their own small scene between the raid and home: artwork for the outcome, what was
/// brought home or lost, and a button to go on. While the player reads, the next scene loads in the background
/// (SceneTransition.Preload), so continuing is quick. Its own scene keeps the raid's objects out of memory and keeps the
/// screen free of the HUD and the world. Opened without a report (played from the editor), it shows a sample message.
/// </summary>
public sealed class RaidResultsScreen : MonoBehaviour
{
    [SerializeField] private Image artwork;
    [Tooltip("Scales the artwork to cover the screen, cropping what doesn't fit.")]
    [SerializeField] private AspectRatioFitter artworkFitter;
    [Tooltip("Faded in after the screen is shown.")]
    [SerializeField] private CanvasGroup content;
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private TMP_Text subtitleLabel;
    [Tooltip("Lists the loot and losses with rich text.")]
    [SerializeField] private TMP_Text bodyLabel;
    [SerializeField] private Button continueButton;
    [SerializeField] private TMP_Text continueLabel;

    [Header("Look")]
    [Tooltip("Shown after an extraction when the raid has no artwork of its own.")]
    [SerializeField] private Sprite extractedArtwork;
    [Tooltip("Shown after a failed raid when the raid has no artwork of its own.")]
    [SerializeField] private Sprite failedArtwork;
    [SerializeField] private Color successColor = new Color(0.55f, 0.92f, 0.55f);
    [SerializeField] private Color failureColor = new Color(1f, 0.42f, 0.36f);
    [SerializeField, Min(0f)] private float contentFadeSeconds = 0.8f;

    [Header("Where Next")]
    [Tooltip("Where the player goes when the report doesn't name a loadable scene.")]
    [SerializeField] private string fallbackScene = "SafeHaven";

    private string destination;
    private bool extracted;

    private void Awake()
    {
        continueButton.onClick.AddListener(Continue);
    }

    private void Start()
    {
        // The raid may have locked the cursor for the camera.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        RaidReport report = GameSession.TakeReport();
        if (report != null)
        {
            Show(report);
        }
        else
        {
            ShowEmpty();
        }

        content.alpha = 0f;
        SceneTransition.WhenReady(OnReady);
    }

    private void OnDestroy()
    {
        SceneTransition.CancelWhenReady(OnReady);
    }

    public void Show(RaidReport report)
    {
        extracted = report.Extracted;
        titleLabel.text = RaidReportText.Title(report);
        titleLabel.color = extracted ? successColor : failureColor;
        subtitleLabel.text = RaidReportText.Subtitle(report);
        bodyLabel.text = RaidReportText.Describe(report);
        Sprite own = report.Definition != null ? report.Definition.ResultArtwork(extracted) : null;
        SetArtwork(own != null ? own : extracted ? extractedArtwork : failedArtwork);
        destination = CanLoad(report.ReturnScene) ? report.ReturnScene : fallbackScene;
        continueLabel.text = extracted ? "Return to Safe Haven" : "Wake at the Heart Tree";
    }

    private void ShowEmpty()
    {
        extracted = true;
        titleLabel.text = "No Raid to Report";
        titleLabel.color = successColor;
        subtitleLabel.text = "This screen is shown after a raid ends.";
        bodyLabel.text = $"<color={RaidReportText.Muted}>Enter a raid through the portal in Safe Haven and extract or fall to see its results here.</color>";
        SetArtwork(extractedArtwork);
        destination = fallbackScene;
        continueLabel.text = "Continue";
    }

    private void SetArtwork(Sprite sprite)
    {
        artwork.sprite = sprite;
        artwork.enabled = sprite != null;
        if (sprite != null && artworkFitter != null)
        {
            artworkFitter.aspectRatio = sprite.rect.width / Mathf.Max(1f, sprite.rect.height);
        }
    }

    private void OnReady()
    {
        if (this == null)
        {
            return;
        }

        StartCoroutine(FadeInContent());
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(continueButton.gameObject);
        }

        // The player reads for a while, which is time enough to load home behind the scenes.
        SceneTransition.Preload(destination);
    }

    private IEnumerator FadeInContent()
    {
        for (float t = 0f; t < 1f && contentFadeSeconds > 0f; t += Time.unscaledDeltaTime / contentFadeSeconds)
        {
            content.alpha = Mathf.SmoothStep(0f, 1f, t);
            yield return null;
        }

        content.alpha = 1f;
    }

    private void Continue()
    {
        if (SceneLoader.Load(destination, extracted ? "Home to Safe Haven" : "Waking at the Heart Tree"))
        {
            continueButton.interactable = false;
        }
    }

    private static bool CanLoad(string scene) => !string.IsNullOrEmpty(scene) && Application.CanStreamedLevelBeLoaded(scene);
}
