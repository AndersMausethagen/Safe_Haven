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
public sealed partial class RaidResultsScreen : MonoBehaviour
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
    private ScrollRect reportScroll;
    // Hearthline 3: the haul as pictures between the subtitle and the report (RaidReportCards), which gives up the room.
    private const float CardSize = 72f;
    private KitHaul first;
    private KitHaul second;
    private KitCardRow lessons;
    private RectTransform reportRect;
    private float reportTop;
    private float reportHeight;
    private const float MinReportHeight = 220f;

    private void Awake()
    {
        Dress();
        continueButton.onClick.AddListener(Continue);
        reportScroll = bodyLabel.GetComponentInParent<ScrollRect>();
        if (reportScroll != null && reportScroll.verticalScrollbar == null)
        {
            reportScroll.verticalScrollbar = ScrollBar((RectTransform)reportScroll.transform);
            reportScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }
    }

    // Hearthline 3: the report's panel in the kit's look, as every window: the leather ground and scroll corners, the title
    // carved in the display face with its rule, the subtitle in the title face, and the gold button.
    private void Dress()
    {
        UIStyle style = UIStyle.Current;
        // One line, never broken in a word ("Extracte / d!"): the carved face is wide. Not past 64 either, where the
        // face's atlas shows specks of the letters beside each one.
        titleLabel.textWrappingMode = TextWrappingModes.NoWrap;
        titleLabel.fontSizeMax = Mathf.Min(titleLabel.fontSize, 64f);
        titleLabel.fontSizeMin = 36f;
        titleLabel.enableAutoSizing = true;
        // Not bold: the carved face is thickened by widening its letters' field, which with its shadow reaches past the
        // atlas's padding and picks up bits of the next letters there.
        titleLabel.fontStyle = FontStyles.Normal;
        content.gameObject.AddComponent<UIRole>().Set(UIRole.Kind.Window);
        style.Title(subtitleLabel);
        subtitleLabel.fontStyle = FontStyles.Normal;
        subtitleLabel.color = style.Muted;
        continueButton.gameObject.AddComponent<UIRole>().Set(UIRole.Kind.PrimaryButton);
    }

    // The carved title takes its colour as a tint over the face's own gold: a hint of green for a way home, red for a fall.
    private Color TitleColour(bool home) => home ? Color.Lerp(Color.white, successColor, 0.3f) : failureColor;

    // A thin bar beside the report: a long one (a rescue, a haul and what held the player back) runs past the panel, and
    // without it nothing says so. Built here, from the UI style, like the Options screen's.
    private static Scrollbar ScrollBar(RectTransform view)
    {
        var track = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        var rect = (RectTransform)track.transform;
        rect.SetParent(view, false);
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.offsetMin = new Vector2(12f, 0f);
        rect.offsetMax = new Vector2(18f, 0f);
        track.GetComponent<Image>().color = UIStyle.Current.Inset;

        var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        var handleRect = (RectTransform)handle.transform;
        handleRect.SetParent(rect, false);
        handleRect.sizeDelta = Vector2.zero;
        var image = handle.GetComponent<Image>();
        image.color = UIStyle.Current.Rim;

        var bar = track.GetComponent<Scrollbar>();
        bar.handleRect = handleRect;
        bar.targetGraphic = image;
        bar.direction = Scrollbar.Direction.BottomToTop;
        return bar;
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
        ReleaseMotion();
        SceneTransition.CancelWhenReady(OnReady);
    }

    public void Show(RaidReport report)
    {
        extracted = report.Extracted;
        titleLabel.text = RaidReportText.Title(report);
        titleLabel.color = TitleColour(extracted);
        subtitleLabel.text = RaidReportText.Subtitle(report);
        if (MakeHaul())
        {
            // The report keeps at least Min Report Height under the pictures: the lessons go back into the words before it is squeezed.
            float haul = RaidReportCards.Show(report, first, second, lessons, 0f, reportHeight - MinReportHeight, out bool lessonsShown) + 12f;
            reportRect.offsetMax = new Vector2(reportRect.offsetMax.x, -(reportTop + haul));
            bodyLabel.text = RaidReportText.Describe(report, true, lessonsShown);
        }
        else
        {
            bodyLabel.text = RaidReportText.Describe(report);
        }

        ToTop();
        Sprite own = report.Definition != null ? report.Definition.ResultArtwork(extracted) : null;
        SetArtwork(own != null ? own : extracted ? extractedArtwork : failedArtwork);
        PlayScene(report.Definition, extracted);
        destination = CanLoad(report.ReturnScene) ? report.ReturnScene : fallbackScene;
        continueLabel.text = extracted ? "Return to Safe Haven" : "Wake at the portal";
        AskWhyHome();
    }

    // Under the subtitle, as wide as the report; false where the screen has no scrolling report to make room in.
    private bool MakeHaul()
    {
        if (first != null)
        {
            return true;
        }

        if (reportScroll == null)
        {
            return false;
        }

        reportRect = (RectTransform)reportScroll.transform;
        reportTop = -reportRect.offsetMax.y;
        reportHeight = reportRect.rect.height;
        RectTransform area = CodeUI.At("Haul", reportRect.parent, reportRect.offsetMin.x, reportTop - 8f, 0f, 0f);
        area.anchorMax = new Vector2(1f, 1f);
        area.sizeDelta = new Vector2(reportRect.offsetMax.x - reportRect.offsetMin.x, 0f);
        first = new KitHaul(area, "First", 0f, CardSize);
        second = new KitHaul(area, "Second", 0f, CardSize);
        // What was learned, a card a skill (it was a list of lines in the words below).
        lessons = new KitCardRow(area, "Lessons", 112f, 64f);
        return true;
    }

    // The report opens at its start, with the bar sized to the new text.
    private void ToTop()
    {
        if (reportScroll == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(reportScroll.content);
        reportScroll.verticalNormalizedPosition = 1f;
    }

    private void ShowEmpty()
    {
        extracted = true;
        titleLabel.text = "No Raid to Report";
        titleLabel.color = TitleColour(true);
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
        if (sprite != null)
        {
            Cover().AspectRatio = sprite.rect.width / Mathf.Max(1f, sprite.rect.height);
        }
    }

    // ArtCover, not the scene's envelope fitter: on a super-ultrawide screen the envelope cut away over half the picture
    // (and the motion drawn over it), zoomed right in. ArtCover crops at most a fifth, then shows black at the sides.
    private ArtCover Cover()
    {
        if (artworkFitter != null)
        {
            artworkFitter.enabled = false;
        }

        return artwork.TryGetComponent(out ArtCover cover) ? cover : artwork.gameObject.AddComponent<ArtCover>();
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
        // The window's dress (UIRole) sets the carved face's own colour as it starts, which may be after the report was
        // shown: the tint goes on a frame later, while the content is still unseen.
        yield return null;
        titleLabel.color = TitleColour(extracted);
        SeeThrough();
        for (float t = 0f; t < 1f && contentFadeSeconds > 0f; t += Time.unscaledDeltaTime / contentFadeSeconds)
        {
            content.alpha = Mathf.SmoothStep(0f, 1f, t);
            yield return null;
        }

        content.alpha = 1f;
    }

    private void Continue()
    {
        // Off while the way home loads, and back on if it breaks off: the results scene has no other way out.
        continueButton.interactable = false;
        SceneLoader.Load(destination, extracted ? "Home to Safe Haven" : "Waking at the portal", ifItFails: () =>
        {
            if (continueButton != null)
            {
                continueButton.interactable = true;
            }
        });
    }

    private static bool CanLoad(string scene) => !string.IsNullOrEmpty(scene) && Application.CanStreamedLevelBeLoaded(scene);
}
