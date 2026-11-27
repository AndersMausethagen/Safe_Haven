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

    // The layout of 2026-11-26 (the user: "veldig mye tekst i høyre del av skjermen hvor man må scrolle ... infoen om hva man har fått
    // med seg kan vises øverst til venstre ... annen informasjon på høyre side ... flere bilder og del opp tekst med ikoner"; Tarkov's
    // status word, Risk of Rain's icon grid, Darkest Dungeon's kept-before-lost): the outcome and the haul as pictures in a panel of
    // their own at the top left, what was learned under it; on the right, short facts with pictures and icons (RaidReportFacts); the
    // button at the bottom right. The words wall (RaidReportText.Describe) is no longer shown here.
    private const float CardSize = 72f;
    private const float PanelInset = 36f;
    private const float TitleHeight = 76f;
    private const float SubtitleHeight = 40f;
    private const float FactsBottom = 132f;
    private const float WhyHomeRoom = 150f;
    private KitHaul first;
    private KitHaul second;
    private KitCardRow lessons;
    private KitFactList facts;
    private RectTransform haulPanel;
    private RectTransform haulArea;
    private CanvasGroup haulGroup;
    private RectTransform reportRect;

    private void Awake()
    {
        reportScroll = bodyLabel.GetComponentInParent<ScrollRect>();
        Dress();
        continueButton.onClick.AddListener(Continue);
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
        // Without a window round it, the title is carved here, as the window's dressing did (UIRole).
        if (reportScroll != null)
        {
            style.Display(titleLabel);
            HearthlineArt.AddTitleRule(titleLabel, style);
        }

        // One line, never broken in a word ("Extracte / d!"): the carved face is wide. Not past 64 either, where the
        // face's atlas shows specks of the letters beside each one.
        titleLabel.textWrappingMode = TextWrappingModes.NoWrap;
        titleLabel.fontSizeMax = Mathf.Min(titleLabel.fontSize, 64f);
        titleLabel.fontSizeMin = 36f;
        titleLabel.enableAutoSizing = true;
        // Not bold: the carved face is thickened by widening its letters' field, which with its shadow reaches past the
        // atlas's padding and picks up bits of the next letters there.
        titleLabel.fontStyle = FontStyles.Normal;
        // No panel behind the facts (the user, 2026-11-27: "Remove the half-transparent bar around the extraction HUD, just keep the
        // icons and information"): the words and pictures stand on the artwork, darkened at the sides (Shade). The old words-only
        // report (a scene without the scroll) keeps its window.
        if (reportScroll == null)
        {
            content.gameObject.AddComponent<UIRole>().Set(UIRole.Kind.Window);
        }
        else if (content.TryGetComponent(out Image ground))
        {
            ground.enabled = false;
        }

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

    // The report shown, and the screen's size it was laid out for: the cards are placed in pixels, so a window resized (or the
    // resolution changed) lays them out again.
    private RaidReport shownReport;
    private Vector2 laidOutFor;

    private void LateUpdate()
    {
        if (shownReport == null || first == null)
        {
            return;
        }

        Vector2 size = Screen().rect.size;
        if ((size - laidOutFor).sqrMagnitude > 1f)
        {
            LayOut(shownReport);
        }
    }

    private void LayOut(RaidReport report)
    {
        laidOutFor = Screen().rect.size;
        // The haul and what was learned at the top left, as tall as they need, never past the screen's foot.
        float room = Screen().rect.height * 0.9f - HaulTop - PanelInset;
        float haul = RaidReportCards.Show(report, first, second, lessons, 0f, room, out _, 3);
        FitHaulPanel(haul);
        ShowFacts(RaidReportFacts.For(report, line => RaidReportFacts.EventPicture(report.Definition, line)));
        ShadowWords(haulPanel, titleLabel);
        ShadowWords(content.transform, titleLabel);
    }

    // A soft dark shadow under every word laid on the artwork, so it reads on a bright patch (a portal's light) with no panel behind
    // it. Each label gets its own copy of its face's material, once. Not the carved title: its face, widened, already reaches past its
    // atlas's padding (Dress), and a dilated shadow picked up specks of the next letters.
    private static void ShadowWords(Transform root, TMP_Text except)
    {
        if (root == null)
        {
            return;
        }

        foreach (TMP_Text words in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (words == except)
            {
                continue;
            }

            Material face = words.fontMaterial;
            if (face == null || face.IsKeywordEnabled(ShaderUtilities.Keyword_Underlay) || !face.HasProperty(ShaderUtilities.ID_UnderlayColor))
            {
                continue;
            }

            face.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            face.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.85f));
            face.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.35f);
            face.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.65f);
            words.UpdateMeshPadding();
        }
    }

    public void Show(RaidReport report)
    {
        extracted = report.Extracted;
        titleLabel.text = RaidReportText.Title(report);
        titleLabel.color = TitleColour(extracted);
        subtitleLabel.text = RaidReportText.Subtitle(report);
        if (BuildLayout())
        {
            shownReport = report;
            LayOut(report);
        }
        else
        {
            bodyLabel.text = RaidReportText.Describe(report);
        }

        Sprite own = report.Definition != null ? report.Definition.ResultArtwork(extracted) : null;
        SetArtwork(own != null ? own : extracted ? extractedArtwork : failedArtwork);
        PlayScene(report.Definition, extracted);
        destination = CanLoad(report.ReturnScene) ? report.ReturnScene : fallbackScene;
        continueLabel.text = extracted ? "Return to Safe Haven" : "Wake at the portal";
        AskWhyHome();
    }

    private float HaulTop => PanelInset + TitleHeight + SubtitleHeight + 10f;

    private RectTransform Screen() => (RectTransform)content.transform.parent;

    // The haul's panel at the top left (the title, the subtitle and the pictures moved into it) and the facts on the right in the
    // report's scroll, which now only scrolls when they run past the screen. False where the scene has no report to lay out.
    private bool BuildLayout()
    {
        if (first != null)
        {
            return true;
        }

        if (reportScroll == null)
        {
            return false;
        }

        RectTransform canvas = Screen();
        var right = (RectTransform)content.transform;
        right.anchorMin = new Vector2(0.6f, 0.05f);
        right.anchorMax = new Vector2(0.978f, 0.95f);
        right.offsetMin = right.offsetMax = Vector2.zero;

        haulPanel = CodeUI.Box("Haul Panel", canvas, new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
        haulPanel.anchorMin = new Vector2(0.022f, 1f);
        haulPanel.anchorMax = new Vector2(0.58f, 1f);
        haulPanel.pivot = new Vector2(0.5f, 1f);
        haulPanel.anchoredPosition = new Vector2(0f, -canvas.rect.height * 0.05f);
        haulPanel.sizeDelta = new Vector2(0f, 400f);
        haulPanel.SetSiblingIndex(content.transform.GetSiblingIndex());
        // No face of its own either: the haul's pictures and words on the artwork (see Dress).
        Shade(canvas, haulPanel.GetSiblingIndex());
        haulGroup = haulPanel.gameObject.AddComponent<CanvasGroup>();
        haulGroup.alpha = 0f;

        Place(titleLabel.rectTransform, PanelInset - 4f, TitleHeight);
        Place(subtitleLabel.rectTransform, PanelInset + TitleHeight, SubtitleHeight);
        titleLabel.alignment = TextAlignmentOptions.Left;
        subtitleLabel.alignment = TextAlignmentOptions.Left;

        haulArea = CodeUI.At("Haul", haulPanel, PanelInset, HaulTop, 0f, 0f);
        haulArea.anchorMax = new Vector2(1f, 1f);
        haulArea.sizeDelta = new Vector2(-PanelInset * 2f, 0f);
        first = new KitHaul(haulArea, "First", 0f, CardSize);
        second = new KitHaul(haulArea, "Second", 0f, CardSize);
        // What was learned, a card a skill, and the one who went along.
        lessons = new KitCardRow(haulArea, "Lessons", 112f, 64f);

        // The facts take the report's place, in its scroll, from the panel's top down to the button.
        reportRect = (RectTransform)reportScroll.transform;
        reportRect.anchorMin = Vector2.zero;
        reportRect.anchorMax = Vector2.one;
        reportRect.offsetMin = new Vector2(PanelInset, FactsBottom);
        reportRect.offsetMax = new Vector2(-PanelInset, -PanelInset);
        bodyLabel.gameObject.SetActive(false);
        Transform viewport = reportScroll.viewport != null ? reportScroll.viewport : reportRect;
        facts = new KitFactList((RectTransform)viewport, "Facts");
        reportScroll.content = facts.Root;
        reportScroll.horizontal = false;
        return true;
    }

    // The artwork darkened toward both sides, softly, with no edge anywhere: the words and pictures read on it without a panel. A
    // texture made here (a few bytes), stretched over the screen, behind the haul and the facts.
    private static void Shade(RectTransform canvas, int siblingIndex)
    {
        // Made once for the game's run: every results screen shares it.
        if (shadeTexture == null)
        {
            const int Width = 128;
            shadeTexture = new Texture2D(Width, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Results Shade" };
            for (int x = 0; x < Width; x++)
            {
                // 0 at the middle, rising smoothly to its darkest at either side.
                float fromMiddle = Mathf.Abs(x / (Width - 1f) - 0.5f) * 2f;
                shadeTexture.SetPixel(x, 0, new Color(0f, 0f, 0f, ShadeDarkest * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.02f, 0.5f, fromMiddle))));
            }

            shadeTexture.Apply();
        }

        RectTransform shade = CodeUI.Stretch("Shade", canvas);
        shade.SetSiblingIndex(siblingIndex);
        var image = shade.gameObject.AddComponent<RawImage>();
        image.texture = shadeTexture;
        image.raycastTarget = false;
    }

    private const float ShadeDarkest = 0.7f;
    private static Texture2D shadeTexture;

    // Moves a label to the haul's panel, across its width under the panel's top.
    private void Place(RectTransform label, float top, float height)
    {
        label.SetParent(haulPanel, false);
        label.anchorMin = new Vector2(0f, 1f);
        label.anchorMax = new Vector2(1f, 1f);
        label.pivot = new Vector2(0.5f, 1f);
        label.anchoredPosition = new Vector2(0f, -top);
        label.sizeDelta = new Vector2(-PanelInset * 2f, height);
    }

    private void FitHaulPanel(float haul)
    {
        float height = HaulTop + Mathf.Max(0f, haul) + PanelInset;
        haulPanel.sizeDelta = new Vector2(0f, Mathf.Min(height, Screen().rect.height * 0.9f));
    }

    private void ShowFacts(System.Collections.Generic.List<ReportFact> shown)
    {
        if (shown.Count == 0)
        {
            shown.Add(new ReportFact { Group = RaidReportFacts.OnTheWay, Icon = "Path", Title = "A quiet raid", Line = "You went, you took what you could, and you came back.", Tone = FactTone.Plain });
        }

        Canvas.ForceUpdateCanvases();
        facts.Show(shown, ((RectTransform)facts.Root.parent).rect.width - 12f);
        reportScroll.verticalNormalizedPosition = 1f;
    }

    private void ShowEmpty()
    {
        extracted = true;
        titleLabel.text = "No Raid to Report";
        titleLabel.color = TitleColour(true);
        subtitleLabel.text = "This screen is shown after a raid ends.";
        if (BuildLayout())
        {
            first.Hide();
            second.Hide();
            lessons.Hide();
            FitHaulPanel(0f);
            ShowFacts(new System.Collections.Generic.List<ReportFact>
            {
                new ReportFact { Group = RaidReportFacts.OnTheWay, Icon = "Info", Title = "No raid to report", Line = "Enter a raid through the portal and extract or fall to see its results here." },
            });
        }
        else
        {
            bodyLabel.text = $"<color={RaidReportText.Muted}>Enter a raid through the portal in Safe Haven and extract or fall to see its results here.</color>";
        }

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
        // What came home first, then the rest a beat later (the haul is the news; the facts explain it).
        const float RightAfter = 0.35f;
        float span = contentFadeSeconds + RightAfter;
        for (float t = 0f; t < span && contentFadeSeconds > 0f; t += Time.unscaledDeltaTime)
        {
            if (haulGroup != null)
            {
                haulGroup.alpha = Mathf.SmoothStep(0f, 1f, t / contentFadeSeconds);
            }

            content.alpha = Mathf.SmoothStep(0f, 1f, (t - (haulGroup != null ? RightAfter : 0f)) / contentFadeSeconds);
            yield return null;
        }

        content.alpha = 1f;
        if (haulGroup != null)
        {
            haulGroup.alpha = 1f;
        }
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
