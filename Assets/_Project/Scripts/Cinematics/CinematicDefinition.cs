using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>How a shot arrives.</summary>
public enum CinematicTransition
{
    /// <summary>At once.</summary>
    Cut = 0,
    /// <summary>Fades in over the shot before it, which keeps moving underneath.</summary>
    Dissolve = 1,
    /// <summary>The shot before fades to black, this one fades up from it.</summary>
    ThroughBlack = 2,
    /// <summary>The shot before fades to white, this one fades down from it.</summary>
    ThroughWhite = 3,
}

public enum CinematicLayerKind
{
    /// <summary>A painted layer.</summary>
    Picture = 0,
    /// <summary>Drifting specks drawn by the game: embers, snow, pollen, fireflies.</summary>
    Motes = 1,
}

/// <summary>Where the camera is. All layers fill the frame when it rests at zero.</summary>
[Serializable]
public struct CinematicPose
{
    [Tooltip("Sideways and up, in frame widths and frame heights. (0.08, 0) is 8% of the picture to the right.")]
    public Vector2 position;
    [Tooltip("Towards the picture: 0 rests, 0.1 has gone a tenth of the way to the subject, negative pulls back.")]
    [Range(-1f, 0.8f)] public float push;

    public CinematicPose(float x, float y, float push)
    {
        position = new Vector2(x, y);
        this.push = push;
    }

    public static CinematicPose Lerp(CinematicPose from, CinematicPose to, float t) => new CinematicPose
    {
        position = Vector2.LerpUnclamped(from.position, to.position, t),
        push = Mathf.LerpUnclamped(from.push, to.push, t),
    };
}

/// <summary>Specks that drift across a shot. Their paths come from their number in the row and the time, so a shot looks
/// the same every time it plays.</summary>
[Serializable]
public sealed class CinematicMotes
{
    [Min(1)] public int count = 60;
    [Tooltip("Size of a speck, smallest and largest, as a share of the frame's height.")]
    public Vector2 size = new Vector2(0.004f, 0.012f);
    [Tooltip("How far a speck travels each second, in frame widths and heights. Embers rise, snow falls.")]
    public Vector2 velocity = new Vector2(0f, 0.05f);
    [Tooltip("How far a speck strays from a straight path, as a share of the frame.")]
    [Min(0f)] public float wander = 0.02f;
    [Tooltip("How many seconds a speck takes to stray out and back.")]
    [Min(0.1f)] public float wanderSeconds = 3f;
    [Tooltip("How much a speck's brightness comes and goes: 0 steady, 1 winks right out.")]
    [Range(0f, 1f)] public float twinkle = 0.3f;
    [Tooltip("The part of the frame they live in: x, y from the bottom left, then width and height, each 0..1. They wrap round inside it.")]
    public Rect area = new Rect(0f, 0f, 1f, 1f);
    [Tooltip("How much of the top and bottom of the area a speck fades in and out over (0..0.5).")]
    [Range(0f, 0.5f)] public float edgeFade = 0.15f;
}

/// <summary>One layer of a shot: a painting cut out at some distance, or specks drawn over it.</summary>
[Serializable]
public sealed class CinematicLayer
{
    public string name;
    public CinematicLayerKind kind = CinematicLayerKind.Picture;
    [Tooltip("The painted layer. For specks: what one speck looks like (empty uses the interface's soft glow).")]
    public Sprite picture;
    [Tooltip("How far behind the subject this layer stands: 0 is the subject itself, 3 a far forest, 30 the sky, -0.4 a branch in front. Far layers move less when the camera does.")]
    [Min(-0.9f)] public float depth;
    [Tooltip("The layer's size against the frame at rest. 1 fills it exactly; a camera that moves needs more, or the layer's edge comes into view.")]
    [Min(0.05f)] public float size = 1f;
    [Tooltip("Drawn as a square as tall as its size says, not in the frame's shape: a glow that should stay round.")]
    public bool round;
    [Tooltip("Where the layer's middle sits at rest, from the middle of the frame, in frame widths and heights.")]
    public Vector2 offset;
    public Color tint = Color.white;
    [Tooltip("How visible the layer is through the shot: left edge is the shot's start, right edge its end.")]
    public AnimationCurve opacity = AnimationCurve.Constant(0f, 1f, 1f);
    [Tooltip("Firelight: how far the brightness dips at random (0 steady, 1 down to nothing).")]
    [Range(0f, 1f)] public float flicker;
    [Tooltip("How many times a second the flicker changes.")]
    [Min(0.1f)] public float flickerSpeed = 8f;
    [Tooltip("How far the layer slides each second, in frame widths and heights: mist, clouds, smoke.")]
    public Vector2 drift;
    [Tooltip("Adds its light to what is behind it instead of covering it: glows, light shafts, sparks.")]
    public bool additive;
    [Tooltip("Only for specks.")]
    public CinematicMotes motes = new CinematicMotes();
}

/// <summary>One sentence of narration: what is said, and what the subtitle reads.</summary>
[Serializable]
public sealed class CinematicLine
{
    public AudioClip voice;
    [TextArea(1, 3)] public string subtitle;
    [Tooltip("Silence before this line: after the shot starts for the first line, after the line before it for the rest.")]
    [Min(0f)] public float pauseBefore = 0.4f;
    [Tooltip("How long the line takes when it has no voice clip yet.")]
    [Min(0.1f)] public float secondsWithoutVoice = 2.5f;

    public float Seconds => voice != null ? voice.length : secondsWithoutVoice;
}

/// <summary>A sound in a shot.</summary>
[Serializable]
public sealed class CinematicSound
{
    public AudioClip clip;
    [Tooltip("Seconds after the shot starts.")]
    [Min(0f)] public float at;
    [Range(0f, 1f)] public float volume = 0.7f;
    [Tooltip("Keeps going to the end of the shot (and through the dissolve after it): a fire, wind, birds.")]
    public bool loop;
    [Tooltip("Seconds it takes to come in and to go out.")]
    [Min(0f)] public float fade = 0.5f;
}

/// <summary>A painting, how the camera moves over it, and what is said meanwhile. A shot lasts as long as its lines and
/// pauses take, so recording a line again never breaks the timing.</summary>
[Serializable]
public sealed class CinematicShot
{
    public string name;
    public CinematicTransition arrives = CinematicTransition.Dissolve;
    [Tooltip("How long the arrival takes. Not used by a cut.")]
    [Min(0f)] public float arriveSeconds = 1.2f;
    [Tooltip("Back to front.")]
    public List<CinematicLayer> layers = new List<CinematicLayer>();
    public CinematicPose cameraFrom;
    public CinematicPose cameraTo;
    [Tooltip("Starts and stops the camera gently. Off, it moves at one speed, which reads better through a dissolve.")]
    public bool easeCamera;
    public List<CinematicLine> lines = new List<CinematicLine>();
    [Tooltip("Seconds the shot holds after its last line (or altogether, if nothing is said).")]
    [Min(0f)] public float holdAfter = 1f;
    public List<CinematicSound> sounds = new List<CinematicSound>();

    /// <summary>How long the shot lasts: its pauses, its lines and the hold after them.</summary>
    public float Seconds
    {
        get
        {
            float seconds = holdAfter;
            foreach (CinematicLine line in lines)
            {
                seconds += line.pauseBefore + line.Seconds;
            }

            return seconds;
        }
    }
}

/// <summary>A stretch of music under some of the shots.</summary>
[Serializable]
public sealed class CinematicMusic
{
    public AudioClip clip;
    [Tooltip("The shot it comes in with (the first shot is 0).")]
    [Min(0)] public int fromShot;
    [Tooltip("Seconds into that shot.")]
    public float delay;
    [Tooltip("The last shot it plays under. It fades out over that shot's end.")]
    [Min(0)] public int toShot;
    [Tooltip("Where in the track to start, in seconds.")]
    [Min(0f)] public float startAt;
    [Range(0f, 1f)] public float volume = 0.6f;
    [Min(0f)] public float fadeIn = 2f;
    [Min(0f)] public float fadeOut = 2f;
}

/// <summary>
/// A narrated sequence of painted shots: the game's backstory at the start of a new game. Each shot is a picture cut
/// into layers at different distances, a camera move over them, the lines spoken and the sounds heard. Authored data
/// only: <see cref="CinematicScreen"/> plays it and <see cref="CinematicTimeline"/> works out its timing, and neither
/// changes this asset.
/// </summary>
[CreateAssetMenu(fileName = "Cinematic", menuName = "Safe Haven/Story/Cinematic", order = 30)]
public sealed class CinematicDefinition : ScriptableObject
{
    [SerializeField] private string title = "The Old Story";
    [SerializeField] private List<CinematicShot> shots = new List<CinematicShot>();
    [SerializeField] private List<CinematicMusic> music = new List<CinematicMusic>();
    [Tooltip("How far the music drops while a line is spoken (1 not at all).")]
    [SerializeField, Range(0.1f, 1f)] private float musicWhileSpeaking = 0.55f;
    [Tooltip("Seconds the last shot takes to fade to black at the end.")]
    [SerializeField, Min(0f)] private float endFadeSeconds = 1.5f;
    [Tooltip("Seconds a subtitle stays after its line, when the next one doesn't replace it first.")]
    [SerializeField, Min(0f)] private float subtitleLinger = 0.6f;
    [Tooltip("Seconds the skip key must be held.")]
    [SerializeField, Min(0.1f)] private float skipHoldSeconds = 1f;

    public string Title => title;
    public IReadOnlyList<CinematicShot> Shots => shots;
    public IReadOnlyList<CinematicMusic> Music => music;
    public float MusicWhileSpeaking => musicWhileSpeaking;
    public float EndFadeSeconds => endFadeSeconds;
    public float SubtitleLinger => subtitleLinger;
    public float SkipHoldSeconds => skipHoldSeconds;

#if UNITY_EDITOR
    /// <summary>Replaces the shots and the music. For the editor script that writes this asset, and for tests that
    /// make one of their own: nothing may change a cinematic while the game runs.</summary>
    public void Author(string newTitle, List<CinematicShot> newShots, List<CinematicMusic> newMusic = null)
    {
        title = newTitle;
        shots = newShots ?? new List<CinematicShot>();
        music = newMusic ?? new List<CinematicMusic>();
    }
#endif
}
