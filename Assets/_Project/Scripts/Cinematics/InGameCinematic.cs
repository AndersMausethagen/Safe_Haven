using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A cinematic played in the game world, in the game's own camera: a list of <see cref="CameraShot"/>s that frame
/// what happens (the old troll breaking out of the trees, its roar), with black bars, the HUD put away and the player
/// held and safe until it ends. It only moves the camera; the story graph that plays it (Play Cinematic) makes things
/// happen meanwhile on its other branches (Move Object, Play Animation, Play Sound), so the same pieces stay editable
/// in the Story Graph. A scene object, so its shots can point at the scene's actors; draw and preview them by selecting
/// it (the inspector can look through each shot). For the painted 2D cinematics see <see cref="CinematicScreen"/>.
/// <para>Time is game time: a pause holds it, and it keeps step with the story's Waits.</para>
/// </summary>
[AddComponentMenu("Safe Haven/Cinematics/In-Game Cinematic")]
[DefaultExecutionOrder(-100)]
public sealed class InGameCinematic : MonoBehaviour
{
    [SerializeField] private List<CameraShot> shots = new List<CameraShot> { new CameraShot() };

    [Header("Around it")]
    [Tooltip("Seconds of gliding from the game's camera into the first shot.")]
    [SerializeField, Min(0f)] private float blendIn = 1f;
    [Tooltip("Seconds of gliding from the last shot back to the game's camera.")]
    [SerializeField, Min(0f)] private float blendOut = 1.2f;
    [Tooltip("Black bars above and below while it plays.")]
    [SerializeField] private bool letterbox = true;
    [Tooltip("Puts the HUD away while it plays (spoken lines still show).")]
    [SerializeField] private bool hideHud = true;
    [Tooltip("The player can't move or be hurt while it plays.")]
    [SerializeField] private bool holdPlayer = true;
    [Tooltip("Holding Space (or the pad's A) ends it early. The story goes on as written; only the camera comes home.")]
    [SerializeField] private bool skippable = true;
    [Tooltip("Lifts the dark (night, unexplored ground) this many metres round each shot's subject so it can be seen. 0: off.")]
    [SerializeField, Min(0f)] private float revealRadius = 18f;
    [Tooltip("Seconds over which the camera eases after its subject's small ups and downs (a giant's steps over rough " +
             "ground), so it doesn't judder with them. 0: rigid.")]
    [SerializeField, Range(0f, 1f)] private float steadiness = 0.35f;

    private const float SkipHoldSeconds = 0.8f;

    private static InGameCinematic playing;
    private Coroutine running;
    private CinematicView view;
    private readonly List<GameObject> sights = new List<GameObject>();
    private readonly HoldToSkip skip = new HoldToSkip();

    public IReadOnlyList<CameraShot> Shots => shots;
    public float BlendIn => blendIn;

    /// <summary>The one playing now, or null.</summary>
    public static InGameCinematic Playing => playing;

    /// <summary>True while any in-game cinematic has the camera.</summary>
    public static bool IsPlaying => playing != null;

    public bool IsRunning => running != null;

    /// <summary>Seconds from the first shot to the end of the last (without the blend out).</summary>
    public float Seconds
    {
        get
        {
            float total = 0f;
            foreach (CameraShot shot in shots) total += shot != null ? shot.seconds : 0f;
            return total;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => playing = null;

    /// <summary>Ends whatever cinematic is playing at once (a scene change, the prologue's ending taking over).</summary>
    public static void StopAll()
    {
        if (playing != null)
        {
            playing.Stop();
        }
    }

    /// <summary>Plays it from the first shot; one already playing (this or another) ends first.</summary>
    public void Play()
    {
        if (!isActiveAndEnabled || shots.Count == 0)
        {
            return;
        }

        StopAll();
        running = StartCoroutine(Run());
    }

    /// <summary>Moves on from the shot playing (or a held one) to the next, or to shot <paramref name="number"/> (1 is the
    /// first). Past the last shot it ends, gliding back to the game's camera.</summary>
    public void Cue(int number = 0)
    {
        if (running == null)
        {
            return;
        }

        cueTo = number > 0 ? Mathf.Clamp(number - 1, 0, shots.Count) : -1;
        cued = true;
    }

    /// <summary>Ends it now, gliding back to the game's camera over Blend Out.</summary>
    public void Finish()
    {
        if (running != null)
        {
            StopCoroutine(running);
            running = null;
            Release(blendOut);
        }
    }

    public void Stop()
    {
        if (running != null)
        {
            StopCoroutine(running);
            running = null;
        }

        Release(0f);
    }

    private void OnDisable() => Stop();

    // The shot playing and how far into it: Run moves time on, LateUpdate places the camera from it (after the actors have
    // moved this frame, and before Cinemachine reads the camera, so a tracking shot doesn't trail a frame behind).
    private int shotIndex = -1;
    private float shotTime;
    private bool shotCut;
    private Transform shotSubject;
    private CameraShot.Anchor shotStand;
    private bool shotStandPending;
    private bool cued;
    private int cueTo = -1;
    private CameraShot.Pose? glideFrom;
    private CameraShot.Pose? lastPose;
    // The subject as the camera follows it: eased, its height and turning more than its going.
    private Vector3 steadyAt;
    private Vector3 steadyVelocity;
    private float steadyYaw;
    private float steadyYawVelocity;
    private float steadyLift;
    private float steadyLiftVelocity;
    private bool steadySet;

    private IEnumerator Run()
    {
        playing = this;
        skip.Reset();
        view = CinematicView.Begin(letterbox, hideHud, holdPlayer, skippable, blendIn);
        Reveal();
        lastPose = null;

        cueTo = -1;
        for (int i = 0; i < shots.Count; i++)
        {
            CameraShot shot = shots[i];
            if (shot == null || shot.seconds <= 0f)
            {
                continue;
            }

            shotIndex = i;
            cued = false;
            shotTime = 0f;
            shotSubject = SubjectOf(shot);
            // Read where the subject stands at this frame's end, not now: the story's other branches (a Move Object
            // turning it the way it will go) start in the same frame, after this one.
            shotStandPending = true;
            steadySet = false;
            glideFrom = i > 0 && shot.blendIn > 0f ? lastPose : null;
            shotCut = lastPose.HasValue && !glideFrom.HasValue;
            // A held shot stays on its end until the story moves on (Cinematic Shot); a cue ends any shot at once.
            while (!cued && (shotTime < shot.seconds || shot.holdUntilCued))
            {
                yield return null;
                shotTime = Mathf.Min(shotTime + Time.deltaTime, shot.seconds);
                if (skippable && view != null && view.ReadSkip(skip, SkipHoldSeconds))
                {
                    i = shots.Count;
                    break;
                }
            }

            if (cueTo >= 0)
            {
                i = cueTo - 1;
                cueTo = -1;
            }
        }

        shotIndex = -1;
        running = null;
        Release(blendOut);
    }

    private void LateUpdate()
    {
        if (running != null)
        {
            PlaceCamera();
        }
    }

    private void PlaceCamera()
    {
        if (view == null || shotIndex < 0 || shotIndex >= shots.Count)
        {
            return;
        }

        CameraShot shot = shots[shotIndex];
        if (shotStandPending)
        {
            shotStand = CameraShot.Anchor.Of(shotSubject);
            shotStandPending = false;
        }

        CameraShot.Pose pose = PoseOf(shot, shotSubject, shotStand, shotTime / shot.seconds);
        if (glideFrom.HasValue && shotTime < shot.blendIn)
        {
            pose = CameraShot.Pose.Lerp(glideFrom.Value, pose, CameraShot.Eased(CameraShot.Ease.EaseInOut, shotTime / shot.blendIn));
        }

        view.Show(pose, shotCut);
        shotCut = false;
        if (shot.rumble > 0f)
        {
            CameraShake.AtLeast(shot.rumble);
        }

        lastPose = pose;
    }

    // The pose at a moment, kept above the ground so a low shot on a slope doesn't look out from under the earth.
    private CameraShot.Pose PoseOf(CameraShot shot, Transform subject, CameraShot.Anchor fixedStand, float progress)
    {
        CameraShot.Anchor now = Steady(CameraShot.Anchor.Of(subject));
        CameraShot.Anchor stand = shot.followSubject ? now : fixedStand;
        CameraShot.Anchor looked = shot.lookAt != null ? CameraShot.Anchor.Of(shot.lookAt) : now;
        CameraShot.Pose pose = shot.Evaluate(stand, looked, progress);
        Vector3 position = pose.Position;
        // Lifted over rising ground, eased too, or the camera would bump over every hummock under it.
        float lift = Mathf.Max(0f, GroundUnder(position) + 0.4f - position.y);
        steadyLift = steadiness > 0f && steadySet ? Mathf.SmoothDamp(steadyLift, lift, ref steadyLiftVelocity, steadiness) : lift;
        steadyLift = Mathf.Max(steadyLift, lift - 0.25f);
        if (steadyLift > 0f)
        {
            position.y += steadyLift;
            return new CameraShot.Pose(position, pose.Rotation, pose.FieldOfView);
        }

        return pose;
    }

    // The subject eased: its height and facing over Steadiness, where it goes over a fifth of that.
    private CameraShot.Anchor Steady(CameraShot.Anchor now)
    {
        float step = Time.deltaTime;
        if (!steadySet || steadiness <= 0f || step <= 0f)
        {
            steadyAt = now.Position;
            steadyYaw = now.Yaw;
            steadyVelocity = Vector3.zero;
            steadyYawVelocity = 0f;
            steadyLift = 0f;
            steadyLiftVelocity = 0f;
            steadySet = true;
            return now;
        }

        var flat = new Vector2(steadyAt.x, steadyAt.z);
        var flatVelocity = new Vector2(steadyVelocity.x, steadyVelocity.z);
        flat = Vector2.SmoothDamp(flat, new Vector2(now.Position.x, now.Position.z), ref flatVelocity, steadiness * 0.2f);
        float height = Mathf.SmoothDamp(steadyAt.y, now.Position.y, ref steadyVelocity.y, steadiness);
        steadyAt = new Vector3(flat.x, height, flat.y);
        steadyVelocity = new Vector3(flatVelocity.x, steadyVelocity.y, flatVelocity.y);
        steadyYaw = Mathf.SmoothDampAngle(steadyYaw, now.Yaw, ref steadyYawVelocity, steadiness);
        return new CameraShot.Anchor(steadyAt, steadyYaw);
    }

    private static float GroundUnder(Vector3 point)
    {
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            Vector3 local = point - terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            if (local.x >= 0f && local.z >= 0f && local.x <= size.x && local.z <= size.z)
            {
                return terrain.SampleHeight(point) + terrain.transform.position.y;
            }
        }

        return float.NegativeInfinity;
    }

    private static Transform SubjectOf(CameraShot shot)
    {
        if (shot.subject != null)
        {
            return shot.subject;
        }

        return Player.Current != null ? Player.Current.transform : null;
    }

    // Light enough to see each subject by, for as long as it plays.
    private void Reveal()
    {
        if (revealRadius <= 0f)
        {
            return;
        }

        var lit = new HashSet<Transform>();
        foreach (CameraShot shot in shots)
        {
            foreach (Transform each in new[] { shot != null ? shot.subject : null, shot != null ? shot.lookAt : null })
            {
                if (each == null || !lit.Add(each))
                {
                    continue;
                }

                var sight = new GameObject("Cinematic Sight");
                sight.transform.SetParent(each, false);
                sight.AddComponent<VisibilitySource>().Configure(revealRadius, 0.4f, true, revealRadius, false);
                sights.Add(sight);
            }
        }
    }

    private void Release(float glideOut)
    {
        foreach (GameObject sight in sights)
        {
            if (sight != null) Destroy(sight);
        }

        sights.Clear();
        shotIndex = -1;
        if (view != null) view.End(glideOut);
        view = null;
        if (playing == this)
        {
            playing = null;
        }
    }

    private void OnDrawGizmosSelected()
    {
        foreach (CameraShot shot in shots)
        {
            if (shot == null || shot.subject == null)
            {
                continue;
            }

            CameraShot.Anchor at = CameraShot.Anchor.Of(shot.subject);
            CameraShot.Anchor looked = shot.lookAt != null ? CameraShot.Anchor.Of(shot.lookAt) : at;
            CameraShot.Pose start = shot.Evaluate(at, looked, 0f);
            CameraShot.Pose end = shot.Evaluate(at, looked, 1f);
            Gizmos.color = new Color(1f, 0.75f, 0.3f);
            Gizmos.DrawLine(start.Position, end.Position);
            Gizmos.DrawWireSphere(start.Position, 0.25f);
            Gizmos.DrawSphere(end.Position, 0.18f);
            Gizmos.color = new Color(1f, 0.75f, 0.3f, 0.4f);
            Gizmos.DrawLine(end.Position, end.Position + end.Rotation * Vector3.forward * 4f);
        }
    }

    /// <summary>What is missing, in a sentence each.</summary>
    public IEnumerable<string> FindProblems()
    {
        if (shots.Count == 0) yield return "add a shot";
        for (int i = 0; i < shots.Count; i++)
        {
            string problem = shots[i]?.FindProblem();
            if (problem != null) yield return $"shot {i + 1}: {problem}";
        }
    }
}
