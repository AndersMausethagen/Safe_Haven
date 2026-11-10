using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Where a cinematic's layers stand for a camera pose. The camera rests one unit from the subject; a layer at depth d
/// stands 1 + d away and fills the frame from there. Moving the camera sideways slides near layers further than far
/// ones, and pushing in grows them faster, which is all the depth there is.
/// </summary>
public static class CinematicCamera
{
    private const float NearestDistance = 0.05f;

    /// <summary>How much larger than at rest a layer at this depth is drawn, and how far it has slid, in frame widths and heights.</summary>
    public static void Place(float depth, CinematicPose pose, out float scale, out Vector2 shift)
    {
        float atRest = 1f + depth;
        float now = Mathf.Max(NearestDistance, atRest - pose.push);
        scale = atRest / now;
        shift = -pose.position / now;
    }

    /// <summary>The middle of a layer on screen (from the middle of the frame) and its size against the frame.</summary>
    public static void Place(CinematicLayer layer, CinematicPose pose, out Vector2 centre, out float size)
    {
        Place(layer.depth, pose, out float scale, out Vector2 shift);
        centre = layer.offset * scale + shift;
        size = layer.size * scale;
    }

    /// <summary>Whether the layer hides the whole frame at this pose.</summary>
    public static bool Covers(CinematicLayer layer, CinematicPose pose)
    {
        Place(layer, pose, out Vector2 centre, out float size);
        return size + 0.0001f >= 1f + 2f * Mathf.Max(Mathf.Abs(centre.x), Mathf.Abs(centre.y));
    }

    /// <summary>The smallest size (against the frame) at which a layer at this depth hides the frame all through a move.</summary>
    public static float SizeToCover(float depth, Vector2 offset, CinematicPose from, CinematicPose to)
    {
        const int Samples = 8;
        float needed = 1f;
        for (int i = 0; i <= Samples; i++)
        {
            CinematicPose pose = CinematicPose.Lerp(from, to, i / (float)Samples);
            Place(depth, pose, out float scale, out Vector2 shift);
            Vector2 centre = offset * scale + shift;
            needed = Mathf.Max(needed, (1f + 2f * Mathf.Max(Mathf.Abs(centre.x), Mathf.Abs(centre.y))) / scale);
        }

        return needed;
    }
}

/// <summary>
/// When everything in a cinematic happens: each shot starts when the one before it has said its lines, so the timing
/// follows the voice clips. Plain arithmetic over a <see cref="CinematicDefinition"/>, which it never changes; the
/// screen that plays the cinematic asks it what to show at a given second.
/// </summary>
public sealed class CinematicTimeline
{
    /// <summary>A spoken line and when it is said, in seconds from the cinematic's start.</summary>
    public readonly struct SpokenLine
    {
        public readonly int Shot;
        public readonly CinematicLine Line;
        public readonly float Start;
        public readonly float End;

        public SpokenLine(int shot, CinematicLine line, float start, float end)
        {
            Shot = shot;
            Line = line;
            Start = start;
            End = end;
        }
    }

    private readonly CinematicDefinition definition;
    private readonly List<float> starts = new List<float>();
    private readonly List<SpokenLine> lines = new List<SpokenLine>();

    public CinematicTimeline(CinematicDefinition definition)
    {
        this.definition = definition;
        float at = 0f;
        for (int i = 0; i < definition.Shots.Count; i++)
        {
            CinematicShot shot = definition.Shots[i];
            starts.Add(at);
            float lineAt = at;
            foreach (CinematicLine line in shot.lines)
            {
                lineAt += line.pauseBefore;
                lines.Add(new SpokenLine(i, line, lineAt, lineAt + line.Seconds));
                lineAt += line.Seconds;
            }

            at += shot.Seconds;
        }

        Seconds = at;
    }

    /// <summary>How long the whole cinematic lasts.</summary>
    public float Seconds { get; }
    public int ShotCount => starts.Count;
    public IReadOnlyList<SpokenLine> Lines => lines;

    public float ShotStart(int shot) => starts[shot];
    public float ShotEnd(int shot) => shot + 1 < starts.Count ? starts[shot + 1] : Seconds;

    /// <summary>The shot whose turn it is at this time.</summary>
    public int ShotAt(float time)
    {
        for (int i = starts.Count - 1; i > 0; i--)
        {
            if (time >= starts[i])
            {
                return i;
            }
        }

        return 0;
    }

    /// <summary>How far through a shot the time is: 0 at its start, 1 at its end, and past 1 while the next shot dissolves in over it.</summary>
    public float Progress(int shot, float time)
    {
        float length = ShotEnd(shot) - starts[shot];
        return length > 0f ? (time - starts[shot]) / length : 1f;
    }

    /// <summary>How visible a shot is: 0 hidden, 1 fully there. A shot stays under the dissolve of the one after it.</summary>
    public float Visibility(int shot, float time)
    {
        if (time < starts[shot] || time >= StaysUntil(shot))
        {
            return 0f;
        }

        CinematicShot arriving = definition.Shots[shot];
        if (arriving.arrives == CinematicTransition.Dissolve && shot > 0 && arriving.arriveSeconds > 0f)
        {
            return Mathf.Clamp01((time - starts[shot]) / arriving.arriveSeconds);
        }

        return 1f;
    }

    /// <summary>When a shot leaves the screen: its end, or the end of the next shot's dissolve over it.</summary>
    public float StaysUntil(int shot)
    {
        float end = ShotEnd(shot);
        if (shot + 1 < starts.Count)
        {
            CinematicShot next = definition.Shots[shot + 1];
            if (next.arrives == CinematicTransition.Dissolve)
            {
                end += next.arriveSeconds;
            }
        }

        return end;
    }

    public CinematicPose Camera(int shot, float time)
    {
        CinematicShot moving = definition.Shots[shot];
        float progress = Progress(shot, time);
        if (moving.easeCamera)
        {
            progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress));
        }

        return CinematicPose.Lerp(moving.cameraFrom, moving.cameraTo, progress);
    }

    /// <summary>The black or white drawn over everything: the first shot coming up, a shot arriving through black or white, and the end.</summary>
    public Color Curtain(float time)
    {
        Color curtain = new Color(0f, 0f, 0f, 0f);
        for (int i = 0; i < starts.Count; i++)
        {
            CinematicShot shot = definition.Shots[i];
            bool white = shot.arrives == CinematicTransition.ThroughWhite;
            if ((!white && shot.arrives != CinematicTransition.ThroughBlack) || shot.arriveSeconds <= 0f)
            {
                continue;
            }

            float half = shot.arriveSeconds * 0.5f;
            float alpha = 1f - Mathf.Abs(time - starts[i]) / half;
            if (alpha > curtain.a)
            {
                curtain = white ? new Color(1f, 1f, 1f, alpha) : new Color(0f, 0f, 0f, alpha);
            }
        }

        if (definition.EndFadeSeconds > 0f)
        {
            float alpha = 1f - (Seconds - time) / definition.EndFadeSeconds;
            if (alpha > curtain.a)
            {
                curtain = new Color(0f, 0f, 0f, alpha);
            }
        }

        curtain.a = Mathf.Clamp01(curtain.a);
        return curtain;
    }

    /// <summary>The line being spoken at this time, or -1 between lines.</summary>
    public int SpeakingAt(float time)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            if (time >= lines[i].Start && time < lines[i].End)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>The subtitle to show: the line being spoken, or the last one for a little after it ends.</summary>
    /// <param name="linger">How much longer than usual a subtitle stays (the player's reading speed).</param>
    public string SubtitleAt(float time, float linger = 1f)
    {
        for (int i = lines.Count - 1; i >= 0; i--)
        {
            if (time >= lines[i].Start)
            {
                return time < lines[i].End + definition.SubtitleLinger * linger ? lines[i].Line.subtitle : "";
            }
        }

        return "";
    }

    /// <summary>When a piece of music comes in.</summary>
    public float MusicStart(CinematicMusic cue) => starts[Mathf.Clamp(cue.fromShot, 0, starts.Count - 1)] + cue.delay;

    /// <summary>When a piece of music has faded out.</summary>
    public float MusicEnd(CinematicMusic cue) => ShotEnd(Mathf.Clamp(cue.toShot, 0, starts.Count - 1));

    /// <summary>How loud a piece of music is at this time, before the player's volume and before it dips under a voice.</summary>
    public float MusicLevel(CinematicMusic cue, float time)
    {
        float start = MusicStart(cue);
        float end = MusicEnd(cue);
        if (time < start || time >= end)
        {
            return 0f;
        }

        float rise = cue.fadeIn > 0f ? Mathf.Clamp01((time - start) / cue.fadeIn) : 1f;
        float fall = cue.fadeOut > 0f ? Mathf.Clamp01((end - time) / cue.fadeOut) : 1f;
        return cue.volume * Mathf.Min(rise, fall);
    }

    /// <summary>When a sound in a shot starts.</summary>
    public float SoundStart(int shot, CinematicSound sound) => starts[shot] + sound.at;

    /// <summary>How loud a looping sound is at this time: it comes in at its start and goes out as its shot leaves the screen.</summary>
    public float LoopLevel(int shot, CinematicSound sound, float time)
    {
        float start = SoundStart(shot, sound);
        float end = StaysUntil(shot);
        if (time < start || time >= end)
        {
            return 0f;
        }

        float rise = sound.fade > 0f ? Mathf.Clamp01((time - start) / sound.fade) : 1f;
        float fall = sound.fade > 0f ? Mathf.Clamp01((end - time) / sound.fade) : 1f;
        return sound.volume * Mathf.Min(rise, fall);
    }
}
