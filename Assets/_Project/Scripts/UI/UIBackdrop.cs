using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Behind a full-screen window (the Haven, skills, journal, raid planner, pause menu: one on a canvas sorted at 100 or
/// more), the game goes soft and dark: a blurred, dimmed picture of it (UIBlurFeature) laid over the HUD and the world,
/// just under the window, fading in as the window opens. A solid picture, not a translucent panel, so it stays even over
/// bright grass in the Linear blend. Side windows (the bag, a station, the stash) and dialogue leave the world in view.
/// UIWindow tells it whenever a window opens or closes.
/// </summary>
public sealed class UIBackdrop : MonoBehaviour
{
    private const int FullScreenOrder = 100;
    private const float FadeSeconds = 0.18f;

    private static UIBackdrop instance;
    private readonly List<UIWindow> open = new List<UIWindow>();
    private Canvas canvas;
    private CanvasGroup group;
    private RawImage picture;
    private float shown;

    /// <summary>A window opened or closed.</summary>
    public static void Track(UIWindow window, bool isOpen)
    {
        if (!Application.isPlaying || OrderOf(window) < FullScreenOrder || !Dims(window))
        {
            return;
        }

        if (instance == null)
        {
            var go = new GameObject("UI Backdrop");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<UIBackdrop>();
        }

        instance.open.Remove(window);
        if (isOpen)
        {
            instance.open.Add(window);
        }

        instance.Restack();
    }

    /// <summary>Whether a full-screen window darkens the game behind it. Dialogue sits on a high canvas so it shows over
    /// everything, but a conversation happens in the world: the speakers stay in view.</summary>
    public static bool Dims(UIWindow window) => window != null && window.Id != UIWindowId.Dialogue;

    private static int OrderOf(UIWindow window)
    {
        Canvas nearest = window != null ? window.GetComponentInParent<Canvas>(true) : null;
        while (nearest != null && !nearest.overrideSorting && !nearest.isRootCanvas)
        {
            nearest = nearest.transform.parent != null ? nearest.transform.parent.GetComponentInParent<Canvas>(true) : null;
        }

        return nearest != null ? nearest.sortingOrder : 0;
    }

    private void Awake()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        group = gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        group.alpha = 0f;
        var rect = (RectTransform)new GameObject("Picture", typeof(RectTransform)).transform;
        rect.SetParent(transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        picture = rect.gameObject.AddComponent<RawImage>();
        picture.raycastTarget = false;
        // Dimmed and warmed towards the panels, so the window stands clear of the world behind it.
        picture.color = new Color(0.46f, 0.43f, 0.4f, 1f);
        canvas.enabled = false;
    }

    private void Restack()
    {
        open.RemoveAll(window => window == null || !window.IsOpen);
        if (open.Count > 0)
        {
            int lowest = int.MaxValue;
            foreach (UIWindow window in open)
            {
                lowest = Mathf.Min(lowest, OrderOf(window));
            }

            canvas.sortingOrder = lowest - 1;
        }
    }

    private void LateUpdate()
    {
        open.RemoveAll(window => window == null || !window.IsOpen);
        bool want = open.Count > 0;
        shown = Mathf.MoveTowards(shown, want ? 1f : 0f, Time.unscaledDeltaTime / FadeSeconds);
        UIBlurFeature.Wanted = shown > 0f || want;
        picture.texture = UIBlurFeature.Output;
        // The blur is a frame behind the first time: wait for it rather than show an empty picture.
        bool ready = picture.texture != null;
        canvas.enabled = shown > 0f && ready;
        group.alpha = shown * shown * (3f - 2f * shown);
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
            UIBlurFeature.Wanted = false;
        }
    }
}
