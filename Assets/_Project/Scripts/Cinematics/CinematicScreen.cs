using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Counts how long the skip key has been held. Letting go starts over.</summary>
public sealed class HoldToSkip
{
    private float held;

    /// <summary>How far the hold has come, 0..1.</summary>
    public float Progress { get; private set; }

    /// <summary>Call every frame. True once the key has been held for the whole time.</summary>
    public bool Step(bool isHeld, float deltaTime, float holdSeconds)
    {
        held = isHeld ? held + deltaTime : 0f;
        Progress = Mathf.Clamp01(held / Mathf.Max(0.01f, holdSeconds));
        return Progress >= 1f;
    }

    public void Reset()
    {
        held = 0f;
        Progress = 0f;
    }
}

/// <summary>
/// Plays a <see cref="CinematicDefinition"/> over whatever is on screen: the layered paintings in a 16:9 frame, the
/// narration, its subtitles, sounds and music, and a hold-to-skip. Built in code when first used, like the other
/// full-screen screens. While it plays the game is paused and the scene's own music is held back; both come back when
/// it ends, and then the caller's <c>finished</c> runs (once, whether it played out or was skipped).
/// <para>
/// It runs on real time, not game time. A stalled frame doesn't count for more than a tenth of a second: the first
/// scene loads in the background meanwhile and can hold a frame for seconds, and jumping ahead by that much would
/// skip a line. The cinematic waits instead.
/// </para>
/// </summary>
public sealed class CinematicScreen : MonoBehaviour
{
    private const float FrameAspect = 16f / 9f;
    // A screen wider than the paintings crops this much of their height at most; past that, black at the sides.
    private const float WidestCrop = 0.2f;
    private const float SkipFadeSeconds = 0.5f;
    private const float HintSeconds = 3f;
    private const float DuckSeconds = 0.35f;
    private const float LongestStep = 0.1f;
    private const string AdditiveMaterialPath = "Cinematics/Cinematic Additive";

    private sealed class LayerView
    {
        public CinematicLayer Layer;
        public RectTransform Rect;
        public Graphic Graphic;
        public CinematicMotesGraphic Motes;
    }

    private sealed class ShotView
    {
        public GameObject Root;
        public CanvasGroup Group;
        public readonly List<LayerView> Layers = new List<LayerView>();
    }

    private sealed class Loop
    {
        public int Shot;
        public CinematicSound Sound;
        public AudioSource Source;
    }

    private static CinematicScreen current;
    private static Material additive;

    private readonly Dictionary<int, ShotView> shotViews = new Dictionary<int, ShotView>();
    private readonly List<AudioSource> musicSources = new List<AudioSource>();
    private readonly List<Loop> loops = new List<Loop>();
    private readonly List<bool> soundStarted = new List<bool>();
    private readonly HoldToSkip skip = new HoldToSkip();
    private CinematicDefinition definition;
    private CinematicTimeline timeline;
    private Action finished;
    private RectTransform stage;
    private Image curtain;
    private TMP_Text subtitle;
    private CanvasGroup hint;
    private RectTransform hintFill;
    private AudioSource voice;
    private AudioSource effects;
    private float time;
    private float duck = 1f;
    private float skippedAt = -1f;
    private float hintUntil;
    private int nextLine;
    private string shownSubtitle;

    /// <summary>True while a cinematic is on screen.</summary>
    public static bool IsPlaying => current != null && current.definition != null;

    /// <summary>Seconds into the cinematic being played (0 when none is).</summary>
    public static float PlayTime => IsPlaying ? current.time : 0f;

    /// <summary>Plays a cinematic, then calls <paramref name="onFinished"/>. With nothing to play it is called at once.</summary>
    public static void Play(CinematicDefinition cinematic, Action onFinished = null)
    {
        if (cinematic == null || cinematic.Shots.Count == 0 || IsPlaying)
        {
            onFinished?.Invoke();
            return;
        }

        if (current == null)
        {
            Create();
        }

        current.Begin(cinematic, onFinished);
    }

    /// <summary>Ends the cinematic now, as if it had played out. For tests and for leaving the game.</summary>
    public static void Stop()
    {
        if (IsPlaying)
        {
            current.Finish();
        }
    }

    /// <summary>Jumps to a time in the cinematic being played: for looking at one shot without sitting through the rest.</summary>
    public static void Seek(float seconds)
    {
        if (IsPlaying)
        {
            current.JumpTo(seconds);
        }
    }

    // Entering Play doesn't reload scripts in this project, so statics are cleared when it starts.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        current = null;
        additive = null;
    }

    private static void Create()
    {
        var root = new GameObject("Cinematic Screen");
        DontDestroyOnLoad(root);
        root.SetActive(false);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Over the menus and the character screen, under the loading screen.
        canvas.sortingOrder = 900;
        UIRoot.ConfigureScaler(root.AddComponent<CanvasScaler>());
        root.AddComponent<GraphicRaycaster>();
        current = root.AddComponent<CinematicScreen>();
        current.Build(root.transform);
    }

    private void Build(Transform root)
    {
        UIStyle style = UIStyle.Current;
        // Black behind the frame, and nothing under the screen can be clicked.
        Image backdrop = CodeUI.Stretch("Backdrop", root).gameObject.AddComponent<Image>();
        backdrop.color = Color.black;

        stage = CodeUI.Box("Frame", root, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        var cover = stage.gameObject.AddComponent<ArtCover>();
        cover.AspectRatio = FrameAspect;
        cover.MaxCrop = WidestCrop;
        stage.gameObject.AddComponent<RectMask2D>();

        curtain = CodeUI.Stretch("Curtain", root).gameObject.AddComponent<Image>();
        curtain.raycastTarget = false;
        curtain.color = Color.clear;

        RectTransform words = CodeUI.Box("Subtitle", root, new Vector2(0.5f, 0f), new Vector2(1300f, 120f), new Vector2(0f, 110f));
        subtitle = words.gameObject.AddComponent<TextMeshProUGUI>();
        subtitle.fontSize = 32f;
        subtitle.alignment = TextAlignmentOptions.Bottom;
        subtitle.textWrappingMode = TextWrappingModes.Normal;
        subtitle.raycastTarget = false;
        subtitle.color = style != null ? style.Text : Color.white;
        if (style != null)
        {
            // The shadowed face made for words that sit straight on a picture.
            style.OnWorld(subtitle);
        }

        RectTransform corner = CodeUI.Box("Skip Hint", root, new Vector2(1f, 0f), new Vector2(260f, 40f), new Vector2(-170f, 48f));
        hint = corner.gameObject.AddComponent<CanvasGroup>();
        hint.alpha = 0f;
        hint.blocksRaycasts = false;
        Color muted = style != null ? style.Muted : Color.gray;
        TMP_Text label = CodeUI.Label("Label", corner, 0f, 0f, 260f, 28f, 18f, muted, TextAlignmentOptions.Right);
        label.text = "Hold Esc or Space to skip";
        if (style != null)
        {
            style.OnWorld(label);
        }

        Image track = CodeUI.At("Track", corner, 60f, 32f, 200f, 3f).gameObject.AddComponent<Image>();
        track.color = style != null ? style.Track : new Color(1f, 1f, 1f, 0.2f);
        track.raycastTarget = false;
        hintFill = CodeUI.Stretch("Fill", track.transform);
        hintFill.anchorMax = new Vector2(0f, 1f);
        Image fill = hintFill.gameObject.AddComponent<Image>();
        fill.color = style != null ? style.Accent : Color.white;
        fill.raycastTarget = false;

        voice = NewSource("Voice");
        effects = NewSource("Effects");
    }

    private AudioSource NewSource(string sourceName)
    {
        var host = new GameObject(sourceName);
        host.transform.SetParent(transform, false);
        var source = host.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        // The game is paused while this plays, and a paused game hushes the world's sounds.
        source.ignoreListenerPause = true;
        return source;
    }

    private void Begin(CinematicDefinition cinematic, Action onFinished)
    {
        definition = cinematic;
        timeline = new CinematicTimeline(cinematic);
        finished = onFinished;
        time = 0f;
        duck = 1f;
        skippedAt = -1f;
        hintUntil = 0f;
        nextLine = 0;
        shownSubtitle = null;
        skip.Reset();
        subtitle.text = "";
        hint.alpha = 0f;

        foreach (CinematicMusic cue in cinematic.Music)
        {
            AudioSource source = NewSource("Music");
            source.clip = cue.clip;
            source.loop = true;
            source.volume = 0f;
            musicSources.Add(source);
        }

        soundStarted.Clear();
        foreach (CinematicShot shot in cinematic.Shots)
        {
            foreach (CinematicSound _ in shot.sounds)
            {
                soundStarted.Add(false);
            }
        }

        GamePause.Request(this);
        AudioManager.HoldMusic(this);
        gameObject.SetActive(true);
        // The frame has no size until the canvas has been laid out once.
        Canvas.ForceUpdateCanvases();
        Show();
    }

    private void Update()
    {
        if (definition == null)
        {
            return;
        }

        float step = Mathf.Min(Time.unscaledDeltaTime, LongestStep);
        time += step;
        ReadSkip(step);
        if (time >= timeline.Seconds || (skippedAt >= 0f && time >= skippedAt + SkipFadeSeconds))
        {
            Finish();
            return;
        }

        Show();
        Sound(step);
    }

    private void JumpTo(float seconds)
    {
        time = Mathf.Clamp(seconds, 0f, timeline.Seconds - 0.05f);
        voice.Stop();
        nextLine = 0;
        while (nextLine < timeline.Lines.Count && timeline.Lines[nextLine].End <= time)
        {
            nextLine++;
        }

        for (int i = 0; i < soundStarted.Count; i++)
        {
            soundStarted[i] = false;
        }

        ClearLoops();
        Show();
    }

    // ----- Skip -----

    private void ReadSkip(float step)
    {
        if (skippedAt >= 0f)
        {
            return;
        }

        bool held = Pressed(GameInput.Pause) || Pressed(GameInput.Cancel) || Pressed(GameInput.Submit)
            || (Keyboard.current != null && Keyboard.current.spaceKey.isPressed);
        bool anything = held || (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
            || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);
        if (anything)
        {
            hintUntil = time + HintSeconds;
        }

        if (skip.Step(held, step, definition.SkipHoldSeconds))
        {
            skippedAt = time;
        }

        hint.alpha = Mathf.MoveTowards(hint.alpha, time < hintUntil ? 1f : 0f, step * 4f);
        hintFill.anchorMax = new Vector2(skip.Progress, 1f);
    }

    private static bool Pressed(InputAction action) => action != null && action.enabled && action.IsPressed();

    // How much of everything is left while a skip fades it out.
    private float SkipFade => skippedAt < 0f ? 1f : 1f - Mathf.Clamp01((time - skippedAt) / SkipFadeSeconds);

    // ----- Picture -----

    private void Show()
    {
        Vector2 frame = stage.rect.size;
        for (int i = 0; i < timeline.ShotCount; i++)
        {
            float visibility = timeline.Visibility(i, time);
            if (visibility <= 0f)
            {
                if (shotViews.TryGetValue(i, out ShotView gone))
                {
                    Destroy(gone.Root);
                    shotViews.Remove(i);
                }

                continue;
            }

            if (!shotViews.TryGetValue(i, out ShotView view))
            {
                view = BuildShot(i);
                shotViews[i] = view;
            }

            view.Group.alpha = visibility;
            Place(view, i, frame);
        }

        Color over = timeline.Curtain(time);
        float skipping = 1f - SkipFade;
        curtain.color = skipping > over.a ? new Color(0f, 0f, 0f, skipping) : over;

        string words = skippedAt >= 0f ? "" : timeline.SubtitleAt(time, GameOptions.SubtitleHold);
        if (words != shownSubtitle)
        {
            shownSubtitle = words;
            subtitle.text = words;
        }
    }

    private ShotView BuildShot(int index)
    {
        CinematicShot shot = definition.Shots[index];
        var view = new ShotView();
        RectTransform root = CodeUI.Stretch(string.IsNullOrEmpty(shot.name) ? $"Shot {index + 1}" : shot.name, stage);
        view.Root = root.gameObject;
        view.Group = view.Root.AddComponent<CanvasGroup>();
        view.Group.blocksRaycasts = false;
        // A later shot dissolves in over the one before it.
        int after = 0;
        foreach (KeyValuePair<int, ShotView> other in shotViews)
        {
            if (other.Key < index)
            {
                after = Mathf.Max(after, other.Value.Root.transform.GetSiblingIndex() + 1);
            }
        }

        root.SetSiblingIndex(after);
        foreach (CinematicLayer layer in shot.layers)
        {
            RectTransform rect = CodeUI.Box(string.IsNullOrEmpty(layer.name) ? "Layer" : layer.name, root, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var made = new LayerView { Layer = layer, Rect = rect };
            if (layer.kind == CinematicLayerKind.Motes)
            {
                made.Motes = rect.gameObject.AddComponent<CinematicMotesGraphic>();
                Sprite speck = layer.picture != null ? layer.picture : UIStyle.Current != null ? UIStyle.Current.Glow : null;
                made.Motes.Show(layer.motes, speck);
                made.Graphic = made.Motes;
            }
            else
            {
                var image = rect.gameObject.AddComponent<Image>();
                image.sprite = layer.picture;
                image.enabled = layer.picture != null;
                made.Graphic = image;
            }

            made.Graphic.raycastTarget = false;
            if (layer.additive && Additive != null)
            {
                made.Graphic.material = Additive;
            }

            view.Layers.Add(made);
        }

        return view;
    }

    private static Material Additive
    {
        get
        {
            if (additive == null)
            {
                additive = Resources.Load<Material>(AdditiveMaterialPath);
            }

            return additive;
        }
    }

    private void Place(ShotView view, int index, Vector2 frame)
    {
        CinematicPose pose = timeline.Camera(index, time);
        float progress = Mathf.Clamp01(timeline.Progress(index, time));
        float seconds = time - timeline.ShotStart(index);
        foreach (LayerView layer in view.Layers)
        {
            CinematicCamera.Place(layer.Layer, pose, out Vector2 centre, out float size);
            centre += layer.Layer.drift * seconds;
            layer.Rect.anchoredPosition = Vector2.Scale(centre, frame);
            layer.Rect.sizeDelta = layer.Layer.round ? new Vector2(frame.y, frame.y) * size : frame * size;
            Color tint = layer.Layer.tint;
            tint.a *= Mathf.Clamp01(layer.Layer.opacity.Evaluate(progress)) * Flicker(layer.Layer, seconds);
            layer.Graphic.color = tint;
            if (layer.Motes != null)
            {
                layer.Motes.SetTime(seconds);
            }
        }
    }

    /// <summary>How bright a flickering layer is at this second of its shot: 1 at most, down by its flicker at least.</summary>
    public static float Flicker(CinematicLayer layer, float seconds)
    {
        if (layer.flicker <= 0f)
        {
            return 1f;
        }

        // Two rates laid over each other, so it never settles into a beat.
        float t = seconds * layer.flickerSpeed;
        float noise = 0.6f * Mathf.PerlinNoise(t, 3.7f) + 0.4f * Mathf.PerlinNoise(t * 2.3f, 11.1f);
        return 1f - layer.flicker * Mathf.Clamp01(noise);
    }

    // ----- Sound -----

    private void Sound(float step)
    {
        float fade = SkipFade;
        float voiceVolume = AudioManager.ChannelVolume(AudioChannel.Voice) * fade;
        float effectsVolume = AudioManager.ChannelVolume(AudioChannel.Effects) * fade;
        float musicVolume = AudioManager.ChannelVolume(AudioChannel.Music) * fade;

        while (skippedAt < 0f && nextLine < timeline.Lines.Count && time >= timeline.Lines[nextLine].Start)
        {
            CinematicTimeline.SpokenLine line = timeline.Lines[nextLine++];
            if (line.Line.voice != null && time < line.End)
            {
                voice.clip = line.Line.voice;
                voice.time = Mathf.Clamp(time - line.Start, 0f, Mathf.Max(0f, line.Line.voice.length - 0.01f));
                voice.Play();
            }
        }

        voice.volume = voiceVolume;

        float duckTo = timeline.SpeakingAt(time) >= 0 ? definition.MusicWhileSpeaking : 1f;
        duck = Mathf.MoveTowards(duck, duckTo, step / DuckSeconds);
        for (int i = 0; i < musicSources.Count; i++)
        {
            CinematicMusic cue = definition.Music[i];
            AudioSource source = musicSources[i];
            float level = timeline.MusicLevel(cue, time);
            if (level > 0f && !source.isPlaying && cue.clip != null)
            {
                source.time = Mathf.Repeat(cue.startAt + (time - timeline.MusicStart(cue)), cue.clip.length);
                source.Play();
            }
            else if (level <= 0f && source.isPlaying)
            {
                source.Stop();
            }

            source.volume = AudioManager.EqualPower(level > 0f ? level / Mathf.Max(0.0001f, cue.volume) : 0f) * cue.volume * duck * musicVolume;
        }

        int number = 0;
        for (int i = 0; i < timeline.ShotCount; i++)
        {
            foreach (CinematicSound sound in definition.Shots[i].sounds)
            {
                float start = timeline.SoundStart(i, sound);
                if (!soundStarted[number] && sound.clip != null && time >= start && time < start + sound.clip.length && skippedAt < 0f)
                {
                    soundStarted[number] = true;
                    if (sound.loop)
                    {
                        AudioSource source = NewSource("Loop");
                        source.clip = sound.clip;
                        source.loop = true;
                        source.volume = 0f;
                        source.Play();
                        loops.Add(new Loop { Shot = i, Sound = sound, Source = source });
                    }
                    else
                    {
                        effects.PlayOneShot(sound.clip, sound.volume);
                    }
                }

                number++;
            }
        }

        effects.volume = effectsVolume;
        for (int i = loops.Count - 1; i >= 0; i--)
        {
            Loop loop = loops[i];
            float level = timeline.LoopLevel(loop.Shot, loop.Sound, time);
            if (level <= 0f && time > timeline.SoundStart(loop.Shot, loop.Sound))
            {
                Destroy(loop.Source.gameObject);
                loops.RemoveAt(i);
                continue;
            }

            loop.Source.volume = level * effectsVolume;
        }
    }

    private void ClearLoops()
    {
        foreach (Loop loop in loops)
        {
            Destroy(loop.Source.gameObject);
        }

        loops.Clear();
        effects.Stop();
    }

    // ----- End -----

    private void Finish()
    {
        Action done = finished;
        finished = null;
        definition = null;
        timeline = null;
        voice.Stop();
        ClearLoops();
        foreach (AudioSource source in musicSources)
        {
            Destroy(source.gameObject);
        }

        musicSources.Clear();
        foreach (ShotView view in shotViews.Values)
        {
            Destroy(view.Root);
        }

        shotViews.Clear();
        GamePause.Release(this);
        AudioManager.ReleaseMusic(this);
        gameObject.SetActive(false);
        done?.Invoke();
    }

    private void OnDestroy()
    {
        if (definition != null)
        {
            GamePause.Release(this);
            AudioManager.ReleaseMusic(this);
        }

        if (current == this)
        {
            current = null;
        }
    }
}
