using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static CodeUI;

/// <summary>
/// The main menu's Cinematics: every cinematic seen on this computer (CinematicArchive), in story order
/// (JournalSettings › Cinematics), each with its painting, its length and a Watch button. The list is the journal's own
/// page and rows (JournalContent.CinematicEntry), so both read the same. Built in code when first opened.
/// </summary>
public sealed class CinematicsScreen : MonoBehaviour
{
    private const float Width = 1280f;
    private const float Height = 760f;
    // Back sits in a bar above the list, where the journal has its tabs and Close.
    private const float TopBar = 56f;
    private const float Margin = 44f;
    private const float DetailWidth = 700f;
    private const float PictureHeight = 190f;

    private static CinematicsScreen current;

    private GameObject window;
    private HavenListPage page;
    private Action closed;

    /// <summary>True when this computer has seen any cinematic the screen lists.</summary>
    public static bool HasAny => Listed().Count > 0;

    /// <summary>Shows the cinematics seen.</summary>
    /// <param name="onClosed">Runs when the screen goes away.</param>
    public static void Open(Action onClosed = null)
    {
        if (current == null)
        {
            Create();
        }

        current.closed = onClosed;
        current.window.SetActive(true);
        current.Draw();
        current.page.ScrollToTop();
    }

    private static List<JournalCinematic> Listed()
    {
        var listed = new List<JournalCinematic>();
        JournalSettings settings = JournalSettings.Current;
        if (settings == null)
        {
            return listed;
        }

        foreach (JournalCinematic told in settings.Cinematics)
        {
            if (told.cinematic != null && CinematicArchive.HasSeen(told.cinematic))
            {
                listed.Add(told);
            }
        }

        return listed;
    }

    private static void Create()
    {
        var root = new GameObject("Cinematics Screen");
        root.SetActive(false);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 800;
        UIRoot.ConfigureScaler(root.AddComponent<CanvasScaler>());
        root.AddComponent<GraphicRaycaster>();
        current = root.AddComponent<CinematicsScreen>();
        current.Build(root.transform);
        root.SetActive(true);
    }

    private void OnEnable()
    {
        if (GameInput.Pause != null) GameInput.Pause.performed += OnBackPressed;
        if (GameInput.Cancel != null) GameInput.Cancel.performed += OnBackPressed;
    }

    private void OnDisable()
    {
        if (GameInput.Pause != null) GameInput.Pause.performed -= OnBackPressed;
        if (GameInput.Cancel != null) GameInput.Cancel.performed -= OnBackPressed;
    }

    private void OnDestroy()
    {
        if (current == this)
        {
            current = null;
        }
    }

    // While a cinematic plays the screen is hidden, and Back belongs to the cinematic (held, it skips).
    private void OnBackPressed(InputAction.CallbackContext context)
    {
        if (window.activeSelf && !CinematicScreen.IsPlaying)
        {
            Close();
        }
    }

    private void Close()
    {
        window.SetActive(false);
        Action then = closed;
        closed = null;
        then?.Invoke();
    }

    private void Build(Transform root)
    {
        RectTransform dim = Stretch("Dim", root);
        dim.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);
        window = dim.gameObject;

        RectTransform panel = Box("Panel", dim, new Vector2(0.5f, 0.5f), new Vector2(Width, Height), Vector2.zero);
        panel.gameObject.AddComponent<Image>();
        panel.gameObject.AddComponent<Outline>().effectDistance = new Vector2(1f, -1f);
        panel.gameObject.AddComponent<UIRole>().Set(UIRole.Kind.Window);
        panel.gameObject.AddComponent<FitInsideParent>();

        AddButton("Back", panel, Width - Margin - 160f, 6f, 160f, UIRole.Kind.Button, Close, out _).text = "Back";
        Hairline(panel, Margin, TopBar, Width - Margin * 2f, 1f, UIStyle.Current);
        RectTransform body = At("Body", panel, 0f, TopBar, Width, Height - TopBar);
        page = new HavenListPage(body, "Cinematics", Width, Height - TopBar, 1,
            "Nothing seen yet. A cinematic can be watched again here once it has played in the game.", Run,
            DetailWidth, PictureHeight, 16f / 9f);
    }

    private void Draw()
    {
        List<JournalCinematic> listed = Listed();
        var shelf = new HavenShelf("Seen so far");
        foreach (JournalCinematic told in listed)
        {
            shelf.Entries.Add(JournalContent.CinematicEntry(told, Watch));
        }

        int all = JournalSettings.Current != null ? JournalSettings.Current.Cinematics.Count : listed.Count;
        page.Draw(new List<HavenShelf> { shelf }, listed.Count < all ? $"{listed.Count} of {all} seen" : $"All {all} seen");
    }

    // The page's buttons only ever watch: the screen stays, hidden while the cinematic plays.
    private static void Run(HavenAction action) => action.Run?.Invoke();

    private void Watch(CinematicDefinition cinematic)
    {
        window.SetActive(false);
        CinematicScreen.Play(cinematic, () =>
        {
            if (this != null)
            {
                window.SetActive(true);
                Draw();
            }
        });
    }

    // Entering Play doesn't reload scripts in this project: what a session left in the statics goes (EngineHazardTests).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        current = null;
    }
}
