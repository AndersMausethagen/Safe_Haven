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
/// layout. Put it on any MonoBehaviour in the scene, or add it while the scene is being set up (SceneTransition.AddSetup).
/// Steps run in <see cref="SetupOrder"/> (lowest first), after every
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
    // The steps of the scene being set up now and the one running (AddSetup); null between setups.
    private static List<ISceneSetup> runningSteps;
    private static int runningStep;
    private static Runner runner;
    private static string preloadedScene;
    private static AsyncOperation preloaded;

    /// <summary>True while a scene is loading or being set up behind the loading screen.</summary>
    public static bool IsBusy { get; private set; }

    // Counts the loads begun: a transition whose scene is ready leaves the screen to a newer one begun meanwhile.
    private static int generation;

    /// <summary>Raised when a scene is loaded and set up, as the loading screen starts to fade away.</summary>
    public static event Action<Scene> SceneReady;

    /// <summary>Raised when a scene change broke off before its scene was ready and the game goes on where it stands: what got
    /// ready for the trip (a load's flag, the pause menu's leaving) undoes itself, or every later load was refused and the menu
    /// stayed frozen.</summary>
    public static event Action Abandoned;

    /// <summary>Loads a scene behind the loading screen. Returns false if a scene change is already under way.</summary>
    /// <param name="caption">Shown on the loading screen, e.g. "Entering the Misty Woods". Null shows "Loading".</param>
    /// <param name="ifItFails">Called however this load fails: refused now, or broken off before its scene is ready (SceneLoader.Load).</param>
    /// <param name="asItCommits">Called once the load can no longer break off, just before the new scene comes in and the old one stops:
    /// what the trip swaps in (a loaded save) goes in then, not while the old scene still runs on it.</param>
    public static bool Load(string scene, string caption = null, Action ifItFails = null, Action asItCommits = null)
    {
        if (string.IsNullOrEmpty(scene))
        {
            throw new ArgumentException("A scene name is required.", nameof(scene));
        }

        return Begin(scene, -1, caption, ifItFails, asItCommits);
    }

    public static bool Load(int buildIndex, string caption = null, Action ifItFails = null, Action asItCommits = null)
    {
        return Begin(null, buildIndex, caption, ifItFails, asItCommits);
    }

    // What the load under way undoes if it breaks off (its caller's ifItFails), until its scene is ready.
    private static Action failing;

    // What the load under way swaps in once it can't break off (its caller's asItCommits). A save loaded at once was played on by the old
    // scene for the whole loading screen: the Haven left behind raised the loaded Haven's rank, moved settlers in and finished its rank
    // quests (review 2026-11-27).
    private static Action committing;

    // A load broke off before its scene was ready: the game goes on where it stands, and what got ready for it undoes itself.
    private static void BrokeOff()
    {
        Action failed = failing;
        failing = null;
        committing = null;
        MarkReady(SceneManager.GetActiveScene());
        try
        {
            failed?.Invoke();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }

        Abandoned?.Invoke();
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

    /// <summary>
    /// Adds a step to the scene being set up now, behind the loading screen, to run after every step still to come of its
    /// <see cref="ISceneSetup.SetupOrder"/> or lower: for work that must see what later steps place, from an object that isn't
    /// in the scene (GroundLightMap draws the cover again once caches and resource nodes are in). Returns false when no scene
    /// is being set up, so there is nothing still to come.
    /// </summary>
    public static bool AddSetup(ISceneSetup step)
    {
        if (step == null || !IsBusy || runningSteps == null)
        {
            return false;
        }

        int at = runningSteps.Count;
        for (int i = runningStep + 1; i < runningSteps.Count; i++)
        {
            if (runningSteps[i].SetupOrder > step.SetupOrder)
            {
                at = i;
                break;
            }
        }

        runningSteps.Insert(at, step);
        return true;
    }

    private static bool Begin(string scene, int buildIndex, string caption, Action ifItFails, Action asItCommits)
    {
        if (IsBusy)
        {
            Debug.LogWarning($"A scene change is already under way; ignoring the request to load '{scene ?? buildIndex.ToString()}'.");
            ifItFails?.Invoke();
            return false;
        }

        IsBusy = true;
        generation++;
        failing = ifItFails;
        committing = asItCommits;
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
        generation++;
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
        failing = null;
        committing = null;
        runningSteps = null;
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
        runningSteps = null;
        SceneReady = null;
        runner = null;
        preloaded = null;
        preloadedScene = null;
        IsBusy = false;
        failing = null;
        committing = null;
        runningStep = 0;
    }

    /// <summary>Hosts the transition coroutines and the loading screen. Lives across scene loads.</summary>
    private sealed class Runner : MonoBehaviour
    {
        // Loading the scene is most of the bar; the setup steps fill the rest.
        private const float LoadShare = 0.85f;

        private LoadingScreen screen;
        private GameObject stoppedPlayer;
        private float setupProgress;

        private void Awake()
        {
            screen = LoadingScreen.Create(transform, SceneTransitionSettings.Current);
        }

        public void StartTransition(string scene, int buildIndex, string caption)
        {
            StartCoroutine(Guarded(Transition(scene, buildIndex, caption)));
        }

        public void StartFirstScene(Scene scene, List<ISceneSetup> steps)
        {
            StartCoroutine(Guarded(FirstScene(scene, steps)));
        }

        // Runs a transition so that, should it end without making its scene ready (an exception outside the guarded setup steps:
        // the screen, the shader warm-up, a setting), the game isn't left busy for good: every later load refused, the loading
        // screen up and the player stopped. It gives up on that load and lets the game go on where it stands.
        private IEnumerator Guarded(IEnumerator work)
        {
            int started = generation;
            try
            {
                while (work.MoveNext())
                {
                    yield return work.Current;
                }
            }
            finally
            {
                if (IsBusy && generation == started)
                {
                    Debug.LogError("A scene change broke off before its scene was ready; the game goes on where it is.");
                    SceneLoader.CancelLeaving();
                    ResumePlayer();
                    screen.Hide();
                    BrokeOff();
                }
            }
        }

        private IEnumerator FirstScene(Scene scene, List<ISceneSetup> steps)
        {
            SceneTransitionSettings settings = SceneTransitionSettings.Current;
            screen.Show(null, scene.name, settings);
            screen.Alpha = 1f;
            yield return RunSetups(steps);
            int mine = generation;
            MarkReady(scene);
            if (generation != mine)
            {
                yield break;
            }

            yield return Fade(0f, settings.FadeInSeconds);
            if (generation == mine)
            {
                screen.Hide();
            }
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
                // Broken off like any other (it never said so: a load's hold on the controls, the pause menu's leaving, stayed).
                BrokeOff();
                yield break;
            }

            operation.allowSceneActivation = false;
            // Unity holds a scene at 90% until it is allowed to activate.
            while (operation.progress < 0.9f)
            {
                screen.Progress = operation.progress / 0.9f * LoadShare;
                ShaderWarmup.Step();
                yield return null;
            }

            screen.Progress = LoadShare;
            // The first load of a session also compiles the game's shaders here, not mid-play (ShaderWarmup).
            while (Time.realtimeSinceStartup - shownAt < settings.MinimumLoadingSeconds || !ShaderWarmup.Done)
            {
                ShaderWarmup.Step();
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
            // From here the load can't break off: what it swaps in goes in now, before the new scene's Awake reads it.
            Action commit = committing;
            committing = null;
            if (commit != null)
            {
                try
                {
                    commit();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }

            operation.allowSceneActivation = true;
            while (!operation.isDone || !setupsDone)
            {
                screen.Progress = LoadShare + (1f - LoadShare) * setupProgress;
                yield return null;
            }

            screen.Progress = 1f;
            int mine = generation;
            MarkReady(loaded);
            // A load begun from a ready callback (a co-op visit ending) has the screen now: this one mustn't fade it out or hide it
            // in the middle of the new load.
            if (generation != mine)
            {
                yield break;
            }

            yield return Fade(0f, settings.FadeInSeconds);
            if (generation == mine)
            {
                screen.Hide();
            }
        }

        private IEnumerator SetUp(List<ISceneSetup> steps, Action done)
        {
            yield return RunSetups(steps);
            done();
        }

        // Steps are driven by hand rather than as nested coroutines, so a step that doesn't yield finishes this frame and
        // the next one starts straight away, all before the scene's Start. A step may add another as it runs (AddSetup).
        // What a step yields that is itself a routine (yield return Another()) is driven by hand too: left to Unity as a nested
        // coroutine, an exception thrown in it stopped the whole chain, setup never finished and the loading screen stayed up.
        private IEnumerator RunSetups(List<ISceneSetup> steps)
        {
            setupProgress = 0f;
            runningSteps = steps;
            var nested = new Stack<IEnumerator>();
            for (int i = 0; i < steps.Count; i++)
            {
                runningStep = i;
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

                nested.Clear();
                if (routine != null)
                {
                    nested.Push(routine);
                }

                while (nested.Count > 0)
                {
                    IEnumerator running = nested.Peek();
                    object current;
                    try
                    {
                        if (!running.MoveNext())
                        {
                            nested.Pop();
                            continue;
                        }

                        current = running.Current;
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception, steps[i] as Object);
                        // The step ends: what it was waiting in is abandoned, its finally blocks run (a flag it holds is let go).
                        nested.Pop();
                        DisposeAll(nested, steps[i] as Object);
                        break;
                    }

                    if (current is IEnumerator inner && !(current is CustomYieldInstruction))
                    {
                        nested.Push(inner);
                        continue;
                    }

                    yield return current;
                }

                setupProgress = (i + 1f) / steps.Count;
            }

            if (runningSteps == steps)
            {
                runningSteps = null;
            }

            setupProgress = 1f;
        }

        // Abandons the routines a failed step was nested in, innermost first: each one's finally blocks run as it is disposed.
        private static void DisposeAll(Stack<IEnumerator> routines, Object context)
        {
            while (routines.Count > 0)
            {
                try
                {
                    (routines.Pop() as IDisposable)?.Dispose();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, context);
                }
            }
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
            stoppedPlayer = Player.Current != null ? Player.Current.gameObject : null;
            PlayerControl.Hold(stoppedPlayer, this);
        }

        // Only the transition's own hold: a player stopped for a reason of its own, such as a raid ending, stays stopped.
        private void ResumePlayer()
        {
            if (stoppedPlayer != null)
            {
                PlayerControl.Release(stoppedPlayer, this);
                stoppedPlayer = null;
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
            set => KitBar.Drive(barFill, value);
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
                art.GetComponent<ArtCover>().AspectRatio = picture.rect.width / Mathf.Max(1f, picture.rect.height);
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

            GameObject artObject = Stretch(new GameObject("Art", typeof(RectTransform), typeof(Image), typeof(ArtCover)), root.transform);
            art = artObject.GetComponent<Image>();
            art.raycastTarget = false;

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
