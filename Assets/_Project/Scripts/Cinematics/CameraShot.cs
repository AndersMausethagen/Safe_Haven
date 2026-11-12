using System;
using UnityEngine;

/// <summary>
/// One camera shot of an <see cref="InGameCinematic"/>: where the camera stands and what it looks at, moving from a start
/// to an end over its seconds. Positions are offsets from the shot's <see cref="subject"/> (the player when empty), in the
/// subject's own facing by default (x right, y up, z ahead of it), so "low, in front of the troll" stays in front of it
/// wherever it walks. The pose is plain maths (<see cref="Evaluate"/>), so it can be previewed in the editor and tested.
/// </summary>
[Serializable]
public sealed class CameraShot
{
    public enum Space
    {
        [Tooltip("Offsets turn with the subject: x to its right, y up, z ahead of it.")]
        SubjectFacing,
        [Tooltip("Offsets in world directions from the subject (x east, y up, z north).")]
        World,
    }

    public enum Ease
    {
        Linear,
        EaseInOut,
        EaseOut,
        EaseIn,
    }

    [Tooltip("For the designer: what the shot shows.")]
    public string name = "Shot";
    [Min(0.1f)] public float seconds = 3f;

    [Tooltip("What the shot is framed around. Empty: the player.")]
    public Transform subject;
    [Tooltip("Optional: look at this instead of the subject (the camera still stands by the subject), e.g. over the troll's shoulder at the fire.")]
    public Transform lookAt;
    [Tooltip("The point looked at, from the subject (or Look At) in the same space as the offsets: (0, 3, 0) is a big creature's chest.")]
    public Vector3 lookOffset = new Vector3(0f, 1.5f, 0f);

    public Space space = Space.SubjectFacing;
    [Tooltip("Where the camera starts, from the subject.")]
    public Vector3 startOffset = new Vector3(0f, 2f, 6f);
    [Tooltip("Where the camera ends, from the subject. The same as Start for a still shot.")]
    public Vector3 endOffset = new Vector3(0f, 2f, 5f);
    [Tooltip("On: the camera travels with the subject (a tracking shot). Off: it stays where the subject was when the shot began and only turns to keep looking (it walks past).")]
    public bool followSubject = true;

    [Range(10f, 90f)] public float startFieldOfView = 40f;
    [Range(10f, 90f)] public float endFieldOfView = 40f;
    public Ease ease = Ease.EaseInOut;

    [Tooltip("Seconds of gliding over from the previous shot. 0: a cut. (The first shot glides from the game's camera by the cinematic's own Blend In.)")]
    [Min(0f)] public float blendIn;
    [Tooltip("Keeps the camera shaking at least this much through the shot (0-1): the ground trembling under a giant.")]
    [Range(0f, 1f)] public float rumble;

    /// <summary>Where the subject stands and which way it faces, at a moment.</summary>
    public readonly struct Anchor
    {
        public readonly Vector3 Position;
        public readonly float Yaw;

        public Anchor(Vector3 position, float yaw)
        {
            Position = position;
            Yaw = yaw;
        }

        public static Anchor Of(Transform transform) => transform == null ? new Anchor(Vector3.zero, 0f) : new Anchor(transform.position, transform.eulerAngles.y);
    }

    /// <summary>The camera at a moment of the shot.</summary>
    public readonly struct Pose
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly float FieldOfView;

        public Pose(Vector3 position, Quaternion rotation, float fieldOfView)
        {
            Position = position;
            Rotation = rotation;
            FieldOfView = fieldOfView;
        }

        /// <summary>Part way from <paramref name="a"/> to <paramref name="b"/> (0..1).</summary>
        public static Pose Lerp(Pose a, Pose b, float t) => new Pose(Vector3.Lerp(a.Position, b.Position, t),
            Quaternion.Slerp(a.Rotation, b.Rotation, t), Mathf.Lerp(a.FieldOfView, b.FieldOfView, t));
    }

    /// <summary>
    /// The camera <paramref name="progress"/> (0..1) through the shot. <paramref name="stand"/> is where the camera's offsets
    /// are measured from (the subject now, or where it was when the shot began); <paramref name="looked"/> is where the
    /// look point is measured from (the subject now, or Look At).
    /// </summary>
    public Pose Evaluate(Anchor stand, Anchor looked, float progress)
    {
        float t = Eased(ease, Mathf.Clamp01(progress));
        Quaternion frame = space == Space.SubjectFacing ? Quaternion.Euler(0f, stand.Yaw, 0f) : Quaternion.identity;
        Vector3 position = stand.Position + frame * Vector3.Lerp(startOffset, endOffset, t);
        Quaternion lookFrame = space == Space.SubjectFacing ? Quaternion.Euler(0f, looked.Yaw, 0f) : Quaternion.identity;
        Vector3 point = looked.Position + lookFrame * lookOffset;
        Vector3 view = point - position;
        Quaternion rotation = view.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(view, Vector3.up) : frame;
        return new Pose(position, rotation, Mathf.Lerp(startFieldOfView, endFieldOfView, t));
    }

    public static float Eased(Ease ease, float t)
    {
        switch (ease)
        {
            case Ease.EaseInOut: return t * t * (3f - 2f * t);
            case Ease.EaseOut: return 1f - (1f - t) * (1f - t);
            case Ease.EaseIn: return t * t;
            default: return t;
        }
    }

    /// <summary>What is missing, in a sentence, or null.</summary>
    public string FindProblem()
    {
        if (seconds <= 0f) return "give it some seconds";
        if (startOffset == Vector3.zero && endOffset == Vector3.zero) return "the camera stands inside its subject: give it an offset";
        return null;
    }
}
