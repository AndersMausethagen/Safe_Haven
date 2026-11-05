using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Object = UnityEngine.Object;

/// <summary>
/// A step that prepares a freshly loaded scene while the loading screen still covers it, such as generating a raid's
/// layout. Put it on any MonoBehaviour in the scene. Steps run in <see cref="SetupOrder"/> (lowest first), after every
/// object in the scene has woken up (Awake) and before anything in it starts (Start). A step may yield to spread heavy
/// work over several frames; gameplay waits for <see cref="SceneTransition.WhenReady"/> before it begins.
/// </summary>
public interface ISceneSetup
{
    int SetupOrder { get; }
    IEnumerator Setup();
}

/// <summary>
/// Changes scenes behind a loading screen: fade out, record the old scene's saveable state, load the new scene in the
/// background, let its <see cref="ISceneSetup"/> steps prepare it, then fade in. Gameplay that must not run while the
/// screen is covered (the raid clock, say) starts from <see cref="WhenReady"/>. The first scene of a play session gets
/// its setup steps too, without the fade out, so a raid scene played straight from the editor is generated the same way.
/// </summary>
public static class SceneTransition
{
    private static readonly List<Action> waitingForReady = new List<Action>();
    private static Runner runner;
    private static string preloadedScene;
    private static AsyncOperation preloaded;

    /// <summary>True while a scene is loading or being set up behind the loading screen.</summary>
    public static bool IsBusy { get; private set; }

    /// <summary>Raised when a scene is loaded and set up, as the loading screen starts to fade away.</summary>
    public static event Action<Scene> SceneReady;

    /// <summary>Loads a scene behind the loading screen. Returns false if a scene change is already under way.</summary>
    /// <param name="caption">Shown on the loading screen, e.g. "Entering the Misty Woods". Null shows "Loading".</param>
    public static bool Load(string scene, string caption = null)
    {
        if (string.IsNullOrEmpty(scene))
        {
            throw new ArgumentException("A scene name is required.", nameof(scene));
        }

        return Begin(scene, -1, caption);
    }

    public static bool Load(int buildIndex, string caption = null)
    {
        return Begin(null, buildIndex, caption);
    }

    /// <summary>
    /// Starts loading a scene in the background without showing it, so a later <see cref="Load(string,string)"/> of the
    /// same scene is near-instant. Use it where the next scene is known and the player is reading, like the raid results.
    /// While a preload waits, Unity holds back other background loads, so only preload what is about to be shown.
    /// </summary>
    public static void Preload(string scene)
    {
        if (IsBusy || preloaded != null || string.IsNullOrEmpty(scene) || !Application.CanStreamedLevelBeLoaded(scene))
        {
            return;
        }

        preloaded = SceneManager.LoadSceneAsync(scene);
        if (preloaded == null)
        {
            return;
        }

        preloaded.allowSceneActivation = false;
        preloadedScene = scene;
    }

    /// <summary>Runs the action once the current scene is set up: now if it already is, otherwise when it becomes ready.</summary>
    public static void WhenReady(Action action)
    {
        if (action == null)
        {
            return;
        }

        if (IsBusy)
        {
            waitingForReady.Add(action);
        }
        else
        {
            action();
        }
    }

    /// <summary>Removes an action passed to <see cref="WhenReady"/> that hasn't run yet, e.g. when its object is destroyed.</summary>
    public static void CancelWhenReady(Action action)
    {
        waitingForReady.Remove(action);
    }

    private static bool Begin(string scene, int buildIndex, string caption)
    {
        if (IsBusy)
        {
            Debug.LogWarning($"A scene change is already under way; ignoring the request to load '{scene ?? buildIndex.ToString()}'.");
            return false;
        }

        IsBusy = true;
        GetRunner().StartTransition(scene, buildIndex, caption);
        return true;
    }

    private static Runner GetRunner()
    {
        if (runner == null)
        {
            var host = new GameObject("Scene Transition");
            Object.DontDestroyOnLoad(host);
            runner = host.AddComponent<Runner>();
        }

        return runner;
    }

    // Scene objects have woken up but not started: the same moment a transition reaches with a newly loaded scene.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void SetUpFirstScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        List<ISceneSetup> steps = FindSetups(scene);
        if (steps.Count == 0)
        {
            return;
        }

        IsBusy = true;
        GetRunner().StartFirstScene(scene, steps);
    }

    private static List<ISceneSetup> FindSetups(Scene scene)
    {
        var steps = new List<ISceneSetup>();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return steps;
        }

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour is ISceneSetup step && behaviour.isActiveAndEnabled)
                {
                    steps.Add(step);
                }
            }
        }

        // A stable sort keeps hierarchy order between steps of the same order.
        var ordered = new List<ISceneSetup>(steps.Count);
        ordered.AddRange(steps);
        ordered.Sort((a, b) =>
        {
            int order = a.SetupOrder.CompareTo(b.SetupOrder);
            return order != 0 ? order : steps.IndexOf(a).CompareTo(steps.IndexOf(b));
        });
        return ordered;
    }

    private static AsyncOperation TakePreloaded(string scene)
    {
        AsyncOperation operation = preloaded;
        string name = preloadedScene;
        preloaded = null;
        preloadedScene = null;
        if (operation == null)
        {
            return null;
        }

        if (name == scene)
        {
            return operation;
        }

        // A different scene is waiting. It can't be cancelled, and it blocks other loads, so it is let through first.
        Debug.LogWarning($"'{name}' was preloaded but '{scene}' was requested; loading both.");
        operation.allowSceneActivation = true;
        return null;
    }

    private static void MarkReady(Scene scene)
    {
        IsBusy = false;
        var actions = new List<Action>(waitingForReady);
        waitingForReady.Clear();
        foreach (Action action in actions)
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        try
        {
            SceneReady?.Invoke(scene);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    // Entering play mode doesn't reload scripts in this project (Enter Play Mode Options), so statics are reset here.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        waitingForReady.Clear();
        SceneReady = null;
        runner = null;
        preloaded = null;
        preloadedScene = null;
        IsBusy = false;
    }

    /// <summary>Hosts the transition coroutines and the loading screen. Lives across scene loads.</summary>
    private sealed class Runner : MonoBehaviour
    {
        // Loading the scene is most of the bar; the setup steps fill the rest.
        private const float LoadShare = 0.85f;

        private LoadingScreen screen;
        private PlayerInputHandler stoppedInput;
        private float setupProgress;

        private void Awake()
        {
            screen = LoadingScreen.Create(transform, SceneTransitionSettings.Current);
        }

        public void StartTransition(string scene, int buildIndex, string caption)
        {
            StartCoroutine(Transition(scene, buildIndex, caption));
        }

        public void StartFirstScene(Scene scene, List<ISceneSetup> steps)
        {
            StartCoroutine(FirstScene(scene, steps));
        }

        private IEnumerator FirstScene(Scene scene, List<ISceneSetup> steps)
        {
            SceneTransitionSettings settings = SceneTransitionSettings.Current;
            screen.Show(null, scene.name, settings);
            screen.Alpha = 1f;
            yield return RunSetups(steps);
            MarkReady(scene);
            yield return Fade(0f, settings.FadeInSeconds);
            screen.Hide();
        }

        private IEnumerator Transition(string scene, int buildIndex, string caption)
        {
            SceneTransitionSettings settings = SceneTransitionSettings.Current;
            StopPlayer();
            string destination = scene ?? System.IO.Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(buildIndex));
            screen.Show(caption, destination, settings);
            screen.Progress = 0f;
            yield return Fade(1f, settings.FadeOutSeconds);
            float shownAt = Time.realtimeSinceStartup;

            // Recorded while the old scene is still whole, before Unity starts switching its objects off.
            SceneLoader.BeginLeaving();

            AsyncOperation operation = scene != null ? TakePreloaded(scene) : null;
            if (operation == null)
            {
                operation = scene != null ? SceneManager.LoadSceneAsync(scene) : SceneManager.LoadSceneAsync(buildIndex);
            }

            if (operation == null)
            {
                Debug.LogError($"Could not load '{scene ?? buildIndex.ToString()}'. Is it in Build Settings?");
                SceneLoader.CancelLeaving();
                ResumePlayer();
                yield return Fade(0f, settings.FadeInSeconds);
                screen.Hide();
                MarkReady(SceneManager.GetActiveScene());
                yield break;
            }

            operation.allowSceneActivation = false;
            // Unity holds a scene at 90% until it is allowed to activate.
            while (operation.progress < 0.9f)
            {
                screen.Progress = operation.progress / 0.9f * LoadShare;
                yield return null;
            }

            screen.Progress = LoadShare;
            while (Time.realtimeSinceStartup - shownAt < settings.MinimumLoadingSeconds)
            {
                yield return null;
            }

            // The setup steps start from sceneLoaded: after the new scene's Awake, before its Start.
            Scene loaded = default;
            bool setupsDone = false;
            void OnLoaded(Scene newScene, LoadSceneMode mode)
            {
                if (mode != LoadSceneMode.Single)
                {
                    return;
                }

                SceneManager.sceneLoaded -= OnLoaded;
                loaded = newScene;
                StartCoroutine(SetUp(FindSetups(newScene), () => setupsDone = true));
            }

            SceneManager.sceneLoaded += OnLoaded;
            operation.allowSceneActivation = true;
            while (!operation.isDone || !setupsDone)
            {
                screen.Progress = LoadShare + (1f - LoadShare) * setupProgress;
                yield return null;
            }

            screen.Progress = 1f;
            MarkReady(loaded);
            yield return Fade(0f, settings.FadeInSeconds);
            screen.Hide();
        }

        private IEnumerator SetUp(List<ISceneSetup> steps, Action done)
        {
            yield return RunSetups(steps);
            done();
        }

        // Steps are driven by hand rather than as nested coroutines, so a step that doesn't yield finishes this frame and
        // the next one starts straight away, all before the scene's Start.
        private IEnumerator RunSetups(List<ISceneSetup> steps)
        {
            setupProgress = 0f;
            for (int i = 0; i < steps.Count; i++)
            {
                IEnumerator routine;
                try
                {
                    routine = steps[i].Setup();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, steps[i] as Object);
                    continue;
                }

                while (routine != null)
                {
                    object current;
                    try
                    {
                        if (!routine.MoveNext())
                        {
                            break;
                        }

                        current = routine.Current;
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception, steps[i] as Object);
                        break;
                    }

                    yield return current;
                }

                setupProgress = (i + 1f) / steps.Count;
            }

            setupProgress = 1f;
        }

        private IEnumerator Fade(float target, float seconds)
        {
            float start = screen.Alpha;
            if (seconds <= 0f)
            {
                screen.Alpha = target;
                yield break;
            }

            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / seconds)
            {
                screen.Alpha = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }

            screen.Alpha = target;
        }

        // The player stays in the old scene until the new one is shown, so keep them still meanwhile.
        private void StopPlayer()
        {
            stoppedInput = null;
            if (Player.Current != null && Player.Current.TryGetComponent(out PlayerInputHandler input) && input.enabled)
            {
                input.enabled = false;
                stoppedInput = input;
            }
        }

        // Only what the transition stopped itself: a player stopped for a reason of its own, such as a raid ending, stays stopped.
        private void ResumePlayer()
        {
            if (stoppedInput != null)
            {
                stoppedInput.enabled = true;
                stoppedInput = null;
            }
        }
    }

    /// <summary>The full-screen loading overlay, built in code so no scene or prefab has to carry it.</summary>
    private sealed class LoadingScreen
    {
        private GameObject root;
        private CanvasGroup group;
        private Image background;
        private Image art;
        private Image shade;
        private TMP_Text caption;
        private TMP_Text tip;
        private string lastTip;
        private RectTransform barFill;
        private Image barFillImage;

        public float Alpha
        {
            get => group.alpha;
            set => group.alpha = value;
        }

        public float Progress
        {
            set => barFill.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);
        }

        public static LoadingScreen Create(Transform parent, SceneTransitionSettings settings)
        {
            var screen = new LoadingScreen();
            screen.Build(parent);
            screen.root.SetActive(false);
            return screen;
        }

        public void Show(string text, string destination, SceneTransitionSettings settings)
        {
            background.color = settings.BackgroundColor;
            Sprite picture = settings.ArtFor(destination);
            art.sprite = picture;
            art.enabled = picture != null;
            shade.enabled = picture != null;
            shade.color = settings.BackgroundColor;
            if (art.enabled)
            {
                art.GetComponent<AspectRatioFitter>().aspectRatio = picture.rect.width / Mathf.Max(1f, picture.rect.height);
            }

            barFillImage.color = settings.AccentColor;
            caption.text = string.IsNullOrEmpty(text) ? "Loading" : text;
            tip.text = GameInput.WithRebinds(PickTip(settings.TipsFor(destination)));
            group.alpha = 0f;
            root.SetActive(true);
        }

        // Never the same one twice running, when there is another to say.
        private string PickTip(string[] tips)
        {
            if (tips == null || tips.Length == 0)
            {
                return "";
            }

            int index = UnityEngine.Random.Range(0, tips.Length);
            if (tips[index] == lastTip && tips.Length > 1)
            {
                index = (index + 1) % tips.Length;
            }

            lastTip = tips[index];
            return lastTip;
        }

        public void Hide()
        {
            root.SetActive(false);
        }

        private void Build(Transform parent)
        {
            root = new GameObject("Loading Screen", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            UIRoot.ConfigureScaler(root.AddComponent<CanvasScaler>());
            // Blocks clicks on whatever is underneath while visible.
            root.AddComponent<GraphicRaycaster>();
            group = root.AddComponent<CanvasGroup>();

            background = Stretch(new GameObject("Background", typeof(RectTransform), typeof(Image)), root.transform).GetComponent<Image>();

            GameObject artObject = Stretch(new GameObject("Art", typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter)), root.transform);
            art = artObject.GetComponent<Image>();
            art.raycastTarget = false;
            artObject.GetComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;

            // The picture sinks into the background colour at the bottom, where the words have to be read.
            GameObject shadeObject = Stretch(new GameObject("Shade", typeof(RectTransform), typeof(Image)), root.transform);
            shade = shadeObject.GetComponent<Image>();
            shade.raycastTarget = false;
            shade.sprite = RisingFade();
            ((RectTransform)shadeObject.transform).anchorMax = new Vector2(1f, ShadeHeight);

            UIStyle style = UIStyle.Current;
            HearthlineArt.AddIcon(root.transform, "Hearthmark", style.Hearthmark, Vector2.zero, Vector2.one * 54f,
                new Vector2(68f, 168f), style.Accent, true);
            caption = Text("Caption", root.transform, 44f, FontStyles.SmallCaps, new Vector2(120f, 150f), TextAlignmentOptions.BottomLeft);
            style.OnWorld(caption, true);
            caption.color = style.Text;
            tip = Text("Tip", root.transform, 24f, FontStyles.Normal, new Vector2(120f, 100f), TextAlignmentOptions.BottomLeft);
            style.OnWorld(tip);
            tip.color = style.Text * new Color(1f, 1f, 1f, 0.85f);

            var bar = new GameObject("Bar", typeof(RectTransform), typeof(Image));
            bar.transform.SetParent(root.transform, false);
            var barRect = (RectTransform)bar.transform;
            barRect.anchorMin = new Vector2(0f, 0f);
            barRect.anchorMax = new Vector2(1f, 0f);
            barRect.offsetMin = new Vector2(120f, 64f);
            barRect.offsetMax = new Vector2(-120f, 70f);
            bar.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.12f);

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(bar.transform, false);
            barFill = (RectTransform)fill.transform;
            barFill.anchorMin = Vector2.zero;
            barFill.anchorMax = new Vector2(0f, 1f);
            barFill.offsetMin = Vector2.zero;
            barFill.offsetMax = Vector2.zero;
            barFillImage = fill.GetComponent<Image>();
        }

        private const float ShadeHeight = 0.55f;

        // White, solid at the bottom edge and gone at the top, for an Image to tint.
        private static Sprite RisingFade()
        {
            const int Height = 64;
            var texture = new Texture2D(1, Height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < Height; y++)
            {
                float up = y / (Height - 1f);
                // Solid behind the words, then gone by the top.
                float gone = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.22f, 1f, up));
                texture.SetPixel(0, y, new Color(1f, 1f, 1f, 0.96f * (1f - gone)));
            }

            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, 1f, Height), new Vector2(0.5f, 0.5f));
        }

        private static GameObject Stretch(GameObject child, Transform parent)
        {
            child.transform.SetParent(parent, false);
            var rect = (RectTransform)child.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return child;
        }

        private static TMP_Text Text(string name, Transform parent, float size, FontStyles style, Vector2 bottomLeft, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.raycastTarget = false;
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.offsetMin = bottomLeft;
            rect.offsetMax = new Vector2(-bottomLeft.x, bottomLeft.y + size * 1.4f);
            return text;
        }
    }
}
