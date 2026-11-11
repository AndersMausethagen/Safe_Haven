using UnityEngine;

/// <summary>
/// The rider's body, from the pack's Horse AnimSet Pro rider clips on the "Riding" layer of the player's Animator
/// (BuildHorses adds it): sitting each gait the horse shows (and swimming with it), answering what the horse does (rearing,
/// bucking, striking, a shake of the head, a flinch to the side it was struck), getting on and off from the side, and the
/// saddle's weapons: a sword swung to the side the blow goes, a bow drawn forward, to either side or back. The layer blends
/// in while the player rides and out after, so on foot nothing changes. Added with PlayerRiding.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerRidingAnimation : MonoBehaviour
{
    public const string LayerName = "Riding";
    private const float BlendSpeed = 5f;

    private static readonly int GaitId = Animator.StringToHash("RideGait");
    private static readonly int SpeedId = Animator.StringToHash("RideSpeed");
    private static readonly int SwimId = Animator.StringToHash("RideSwim");
    private static readonly int RideState = Animator.StringToHash("Ride");
    private static readonly string[] BowSides = { "N", "E", "S", "W" };

    private Animator animator;
    private PlayerRiding riding;
    private PlayerCombat combat;
    private PlayerArchery archery;
    private int layer = -1;
    private float weight;
    private Horse listening;
    private bool wasAttacking;
    private bool wasDrawing;
    private int bowSide;
    private float acting;
    private float nextSync;
    private float mountingUntil;

    /// <summary>True when the player's Animator has the Riding layer: then the bones aren't bent by hand (PlayerTransportPose).</summary>
    public bool HasLayer => layer >= 0;
    /// <summary>The player's size against the clips' (a humanoid's), for the mount path.</summary>
    public float HumanScale => animator != null && animator.isHuman ? animator.humanScale : 1f;

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        riding = GetComponent<PlayerRiding>();
        combat = GetComponent<PlayerCombat>();
        archery = GetComponent<PlayerArchery>();
        layer = animator != null && animator.runtimeAnimatorController != null ? animator.GetLayerIndex(LayerName) : -1;
    }

    private void OnDisable() => Listen(null);

    /// <summary>Getting on (from the left or right of the horse) or off: the clip plays while PlayerRiding moves the body.</summary>
    public void PlayMount(bool on, bool fromRight, float seconds)
    {
        if (!HasLayer)
        {
            return;
        }

        weight = 1f;
        animator.SetLayerWeight(layer, 1f);
        Play((on ? "Mount " : "Dismount ") + (fromRight ? "Right" : "Left"), 0.08f);
        mountingUntil = Time.time + seconds;
        acting = mountingUntil;
    }

    private void Update()
    {
        if (!HasLayer)
        {
            enabled = false;
            return;
        }

        Horse horse = riding != null ? riding.Riding : null;
        Listen(horse);
        bool shown = horse != null || Time.time < mountingUntil;
        weight = Mathf.MoveTowards(weight, shown ? 1f : 0f, BlendSpeed * Time.deltaTime);
        animator.SetLayerWeight(layer, weight);
        if (horse == null)
        {
            return;
        }

        HorseMotion motion = horse.Motion;
        animator.SetFloat(GaitId, motion.Gait, 0.12f, Time.deltaTime);
        animator.SetFloat(SpeedId, motion.Animator != null ? motion.Animator.speed : 1f);
        animator.SetBool(SwimId, motion.IsSwimming);
        Weapons(horse);
        KeepStep(motion);
    }

    // A swing from the saddle goes to the side the blow goes; a bow is drawn the way it aims.
    private void Weapons(Horse horse)
    {
        bool attacking = combat != null && combat.IsAttacking;
        if (attacking && !wasAttacking && combat.Weapon != null && !combat.Weapon.IsBow)
        {
            Vector3 aim = AimFromHorse(horse);
            Play(Vector3.Dot(aim, horse.transform.right) >= 0f ? "Ride Sword Right" : "Ride Sword Left", 0.08f);
            acting = Time.time + 0.85f;
        }

        wasAttacking = attacking;
        bool drawing = archery != null && archery.IsDrawing;
        if (drawing && !wasDrawing)
        {
            bowSide = Quadrant(horse.transform, AimFromHorse(horse));
            Play($"Ride Bow {BowSides[bowSide]} Prepare", 0.12f);
            acting = float.MaxValue;
        }
        else if (!drawing && wasDrawing)
        {
            Play($"Ride Bow {BowSides[bowSide]} Release", 0.06f);
            acting = Time.time + 0.9f;
        }

        wasDrawing = drawing;
    }

    // Where the player aims, on the ground, from the horse: the swing's or the draw's direction.
    private Vector3 AimFromHorse(Horse horse)
    {
        Vector3 aim = transform.forward;
        Camera view = Camera.main;
        if (view != null && GameInput.Point != null)
        {
            Ray ray = view.ScreenPointToRay(GameInput.Point.ReadValue<Vector2>());
            var ground = new Plane(Vector3.up, horse.transform.position);
            if (ground.Raycast(ray, out float distance))
            {
                aim = ray.GetPoint(distance) - horse.transform.position;
            }
        }

        aim.y = 0f;
        return aim.sqrMagnitude > 0.01f ? aim.normalized : horse.transform.forward;
    }

    /// <summary>Which way from a horse a direction points: 0 ahead, 1 right, 2 behind, 3 left.</summary>
    public static int Quadrant(Transform horse, Vector3 direction)
    {
        float angle = Vector3.SignedAngle(horse.forward, new Vector3(direction.x, 0f, direction.z), Vector3.up);
        return angle >= -45f && angle < 45f ? 0 : angle >= 45f && angle < 135f ? 1 : angle >= -135f && angle < -45f ? 3 : 2;
    }

    // The pack's rider and horse clips are made to match: keep the rider's stride on the horse's, so the seat rises and
    // falls with the back.
    private void KeepStep(HorseMotion motion)
    {
        if (Time.time < acting || Time.time < nextSync || motion.Animator == null || motion.IsSwimming)
        {
            return;
        }

        nextSync = Time.time + 0.5f;
        AnimatorStateInfo horse = motion.Animator.GetCurrentAnimatorStateInfo(0);
        AnimatorStateInfo rider = animator.GetCurrentAnimatorStateInfo(layer);
        if (motion.Animator.IsInTransition(0) || animator.IsInTransition(layer) || rider.shortNameHash != RideState || !horse.IsName("Move"))
        {
            return;
        }

        float drift = Mathf.Abs(Mathf.Repeat(horse.normalizedTime, 1f) - Mathf.Repeat(rider.normalizedTime, 1f));
        if (Mathf.Min(drift, 1f - drift) > 0.06f)
        {
            animator.Play(RideState, layer, Mathf.Repeat(horse.normalizedTime, 1f));
        }
    }

    private void Listen(Horse horse)
    {
        if (listening == horse)
        {
            return;
        }

        if (listening != null && listening.Motion != null)
        {
            listening.Motion.Acted -= OnHorseActed;
        }

        listening = horse;
        if (listening != null && listening.Motion != null)
        {
            listening.Motion.Acted += OnHorseActed;
        }
    }

    private void OnHorseActed(HorseAct act, int which)
    {
        if (Time.time < acting && act != HorseAct.Hit && act != HorseAct.Buck && act != HorseAct.Rear && act != HorseAct.Jump)
        {
            return;
        }

        switch (act)
        {
            case HorseAct.Rear: Play("Ride Rear", 0.1f); break;
            case HorseAct.Buck: Play("Ride Buck", 0.1f); break;
            case HorseAct.Strike: Play("Ride Strike", 0.1f); break;
            case HorseAct.Fidget: Play("Ride Fidget " + which, 0.2f); break;
            case HorseAct.Hit: Play("Ride Hit " + which, 0.08f); break;
            case HorseAct.Jump: Play("Ride Jump", 0.06f); break;
        }

        acting = Time.time + 0.6f;
    }

    private void Play(string state, float fade)
    {
        int hash = Animator.StringToHash(state);
        if (animator.HasState(layer, hash))
        {
            animator.CrossFadeInFixedTime(hash, fade, layer);
        }
    }
}
