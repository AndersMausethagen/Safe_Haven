using UnityEngine;

/// <summary>
/// Plays a monster's animations from what its MonsterAI is doing. It only sets parameters and plays states its Animator Controller
/// has, so a monster with a simpler controller, or none yet, still works.
/// <para>
/// A humanoid body has its own moves, not the player's (Docs/EnemyCombatResearch.md §4.1): a call or a cast channels (the "Call" or
/// "Cast" state, or the attack's own) and lets go in its "&lt;state&gt; Release" state the attack's Strike Lead before the hit; it stands
/// ready and steps sideways while waiting its turn (Ready, MoveX, MoveZ); it stays dazed as long as it staggers (Staggered); it flinches
/// by where a blow came from, only its arms while it recovers from its own blow ("Flinch Arms" layer); a strike broken off shows; and it
/// draws its weapon when it spots someone (Draw).
/// </para>
/// <para>
/// Answering back (phase C): felled, it falls, lies ("Down") and gets up ("Get Up") as its stagger ends, as a boss does when it topples
/// (staying down while a stage's reel lengthens the topple); its weapon guard ("Guarding",
/// the "Guard" state) jolts when a blow is turned aside ("Guard Hit" on the arms layer), as its shield does ("Block Hit"); badly hurt it
/// cries out ("Roar"); an Unyielding elite doesn't flinch.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MonsterAI))]
[OnAFriendsCopy(CopyRule.Same, "plays what the copy does")]
public sealed class MonsterAnimationDriver : MonoBehaviour
{
    [Tooltip("Left empty, the first Animator on this object or its children is used.")]
    [SerializeField] private Animator animator;
    [Tooltip("Float parameter set to the movement speed in meters per second.")]
    [SerializeField] private string speedParameter = "Speed";
    [Tooltip("Trigger parameter fired when an attack starts.")]
    [SerializeField] private string attackParameter = "Attack";
    [Tooltip("Bool parameter set when the monster dies.")]
    [SerializeField] private string deadParameter = "Dead";
    [Tooltip("Trigger parameter fired when the monster staggers (broken poise, a parried attack).")]
    [SerializeField] private string staggerParameter = "Stagger";
    [SerializeField, Min(0f)] private float speedDampTime = 0.1f;
    [Tooltip("A blow worth this share of its health makes a humanoid body flinch, when it isn't busy striking or staggering.")]
    [SerializeField, Range(0f, 1f)] private float flinchShare = 0.04f;
    [Tooltip("For a body with one stride clip: the speed in m/s its feet keep up with at normal playback. Faster or slower plays it faster or slower, so the feet don't slide. Used when the controller has a StrideRate parameter.")]
    [SerializeField, Min(0.1f)] private float strideSpeed = 2f;
    [Tooltip("Swings as the player does (the Lost Ones, once settlers): a strike fitted to a player's clip (CombatClipKit.FitAttack) builds up at its " +
             "Animation Speed, strikes at Strike Speed for the Strike Lead before the blow, and plays out at Follow Through Speed. Off: one speed throughout.")]
    [SerializeField] private bool swingsLikeThePlayer;
    [Tooltip("Humanoid bodies: how fast the feet of this body's walk and run clips move at normal playback, in metres per second of a " +
             "human-sized body (the body's size is divided out). MonsterHumanoid's own KI walk and run: 1.86 and 4.08. A body whose " +
             "override plays other clips (a zombie's shamble, a treant's stride) says what they cover, so the feet keep up with the ground.")]
    [SerializeField, Min(0.1f)] private float walkClipSpeed = WalkThreshold;
    [SerializeField, Min(0.1f)] private float runClipSpeed = RunThreshold;

    /// <summary>The bool a controller has when it has a clip for grazing and sleeping.</summary>
    public const string RestingParameter = "Resting";

    private static readonly int RestingHash = Animator.StringToHash(RestingParameter);
    private static readonly int StrideRateHash = Animator.StringToHash("StrideRate");
    // Humanoid locomotion (technical animation audit, 2026-11-20): Speed and MoveX/MoveZ are fed per body size (a Tusse at 0.44 of a man
    // glided 2.5 times over the ground, Brukam at 2.3 trod a treadmill), Locomotion's thresholds are its clips' feet speeds, and
    // LocomotionRate / StrafeRate play the stride faster or slower where the clips alone can't keep up.
    private const float WalkThreshold = 1.86f;
    private const float RunThreshold = 4.08f;
    private const float StrafeRingForward = 4.74f;
    private const float StrafeRingBack = 2.37f;
    private const float SlowestStride = 0.5f;
    private const float FastestStride = 2.2f;
    // A small body may stride faster still (a Tusse at 0.39 of a man, chasing at 4.4 m/s, glided at 2.2: its legs cover 3.5 m/s):
    // the cap grows as the body shrinks, FastestStride / sqrt(size), to MostStride.
    private const float MostStride = 3.5f;
    private float Fastest => bodyScale >= 1f ? FastestStride : Mathf.Min(MostStride, FastestStride / Mathf.Sqrt(Mathf.Max(0.05f, bodyScale)));
    private static readonly int LocomotionRateHash = Animator.StringToHash("LocomotionRate");
    private static readonly int StrafeRateHash = Animator.StringToHash("StrafeRate");
    private bool hasLocomotionRate;
    private bool hasStrafeRate;
    private float bodyScale = 1f;
    // Charges move the body with NavMeshAgent.Move, which the agent's velocity (ai.Speed) doesn't count: a charging boar slid along in
    // its idle. The speed fed is also what the body actually covered (as KnightSteed does).
    private const float MostMeasuredSpeed = 15f;
    private Vector3 speedProbe;
    private bool speedProbed;
    private int strikeStateHash;
    private bool hasResting;
    private bool hasStride;

    private const float StrikeFade = 0.1f;
    private const float FlinchFade = 0.06f;
    private const float HopFade = 0.05f;
    private const float HopPlayback = 1.5f;
    // A held wind-up all but stops the clip, without it looking frozen.
    private const float HeldPlayback = 0.04f;
    private const float FinishFade = 0.14f;
    private float holdEndsAt = -1f;
    private float holdSlowsAt;
    private static readonly int ActionSpeedHash = Animator.StringToHash("ActionSpeed");
    private static readonly int HitStateHash = Animator.StringToHash("Hit");
    private static readonly int HopStateHash = Animator.StringToHash("Hop");
    private static readonly int HeavyHitStateHash = Animator.StringToHash("Hit Heavy");
    private static readonly int KnockdownStateHash = Animator.StringToHash("Knockdown");
    // Reactions by the blow's weight (No Rest for the Wicked 1.0: "hit reactions depend on how much poise damage an attack deals"): a
    // light flinch, a heavy stagger back for a weighty blow, a knockdown when a charged or crushing blow breaks its poise.
    private const float HeavyHitPoiseShare = 0.3f;
    private const HitTechnique Weighty = HitTechnique.Heavy | HitTechnique.Charged | HitTechnique.Counter | HitTechnique.GuardCounter | HitTechnique.Finisher;
    private float lastBlowAt = -1f;
    private bool lastBlowFells;

    // States and parameters of its own moves (all optional).
    private static readonly int LocomotionStateHash = Animator.StringToHash("Locomotion");
    private static readonly int DrawStateHash = Animator.StringToHash("Draw");
    private static readonly int FlinchStateHash = Animator.StringToHash("Flinch");
    private static readonly int StaggeredHash = Animator.StringToHash("Staggered");
    private static readonly int ReadyHash = Animator.StringToHash("Ready");
    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveZHash = Animator.StringToHash("MoveZ");
    private static readonly int GuardingHash = Animator.StringToHash("Guarding");
    private static readonly int GetUpStateHash = Animator.StringToHash("Get Up");
    private static readonly int GuardHitStateHash = Animator.StringToHash("Guard Hit");
    private static readonly int BlockHitStateHash = Animator.StringToHash("Block Hit");
    private static readonly int RoarStateHash = Animator.StringToHash("Roar");
    // How long "Get Up" takes as played; it starts this long before the felled stagger ends, so it stands as the creature acts again.
    private const float GetUpSeconds = 1.0f;
    private bool hasGuarding;
    private float getUpAt = -1f;
    private int interruptedFrame = -1;
    private const string FlinchLayerName = "Flinch Arms";
    // A call or cast without a Strike Lead of its own lets go this long before the hit.
    private const float DefaultReleaseLead = 0.15f;
    private const float ReleaseFade = 0.08f;
    // A blow from further round than this is from behind: the body tips forward with it (MonsterPose), no flinch back.
    private const float FromBehindDegrees = 110f;
    private const float MoveDampTime = 0.15f;
    private bool hasStaggered;
    private bool hasReady;
    private bool hasMove;
    private bool hasMoveX;
    private bool hasMoveZ;
    private int flinchLayer = -1;
    private int releaseHash;
    private float releaseAt = -1f;
    // A swing fitted as the player's are (CombatClipKit.FitAttack), on a body that swings like the player (swingsLikeThePlayer): built
    // up at its Animation Speed, struck at Strike Speed for the Strike Lead before the blow, played out at Follow Through Speed, so the
    // blade meets the target as the hit lands.
    private float strikePhaseAt = -1f, followPhaseAt = -1f;
    private float strikePhaseSpeed = 1f, followPhaseSpeed = 1f;
    private Vector3 lastPosition;

    private Damageable health;
    private MonsterAI ai;
    private int speedHash;
    private int attackHash;
    private int deadHash;
    private int staggerHash;
    private bool hasStagger;
    private bool hasSpeed;
    private bool hasAttack;
    private bool hasDead;
    private bool holding;

    private void Awake()
    {
        ai = GetComponent<MonsterAI>();
        health = GetComponent<Damageable>();
        animator = animator != null ? animator : GetComponentInChildren<Animator>();
        CacheParameters();
    }

    // A humanoid's clips move a human-sized body: its own size scales what they cover. Read in Start, after every Awake has sized
    // the body (a pack leader is made 1.1 times bigger in MonsterPack.Awake, in no set order with this one).
    private void Start()
    {
        bodyScale = animator != null && animator.isHuman ? Mathf.Max(0.05f, animator.humanScale * animator.transform.lossyScale.y) : 1f;
    }

    private void OnEnable()
    {
        ai.AttackStarted += OnAttackStarted;
        ai.StateChanged += OnStateChanged;
        ai.Staggered += OnStaggered;
        ai.HeldForFinish += OnHeldForFinish;
        ai.Interrupted += OnInterrupted;
        ai.Wavered += OnWavered;
        ai.ShieldBlocked += OnShieldBlocked;
        ai.GuardTurnedAside += OnGuardTurnedAside;
        ai.TurnedHurt += OnTurnedHurt;
        ai.DrawStarted += PlayDraw;
        if (health != null)
        {
            health.Damaged += OnDamaged;
        }

        lastPosition = transform.position;
    }

    private void OnDisable()
    {
        ai.AttackStarted -= OnAttackStarted;
        ai.StateChanged -= OnStateChanged;
        ai.Staggered -= OnStaggered;
        ai.HeldForFinish -= OnHeldForFinish;
        ai.Interrupted -= OnInterrupted;
        ai.Wavered -= OnWavered;
        ai.ShieldBlocked -= OnShieldBlocked;
        ai.GuardTurnedAside -= OnGuardTurnedAside;
        ai.TurnedHurt -= OnTurnedHurt;
        ai.DrawStarted -= PlayDraw;
        if (health != null)
        {
            health.Damaged -= OnDamaged;
        }

        getUpAt = -1f;
    }

    private void Update()
    {
        // The body went to a corpse (DeathRagdoll.TakeBody) and the corpse is gone before this creature's own delayed destroy.
        if (animator == null)
        {
            enabled = false;
            return;
        }

        // Story movement owns locomotion while the AI is disabled.
        float speed = MovedSpeed();
        UpdateStrikeTail(speed);
        if (ai.enabled && hasSpeed)
        {
            animator.SetFloat(speedHash, speed / bodyScale, speedDampTime, Time.deltaTime);
        }

        if (hasLocomotionRate && ai.enabled)
        {
            animator.SetFloat(LocomotionRateHash, LocomotionRate(speed / bodyScale));
        }

        if (hasStride && ai.enabled)
        {
            animator.SetFloat(StrideRateHash, speed > 0.2f ? Mathf.Clamp(speed / strideSpeed, 0.5f, 2.5f) : 1f);
        }

        if (hasResting)
        {
            animator.SetBool(RestingHash, ai.IsResting);
        }

        if (holdEndsAt >= 0f)
        {
            bool over = Time.time >= holdEndsAt;
            animator.speed = over || Time.time < holdSlowsAt ? 1f : HeldPlayback;
            holdEndsAt = over ? -1f : holdEndsAt;
        }

        if (animator != null && ai.IsHoldingStrike != holding)
        {
            holding = !holding;
            animator.speed = holding ? HeldPlayback : 1f;
        }

        UpdateOwnMoves();
    }

    // The channel's release on time, the ready stance and its side steps, and the daze held while it staggers.
    private void UpdateOwnMoves()
    {
        if (animator == null || !animator.enabled)
        {
            return;
        }

        UpdateSwingPhases();
        if (releaseAt >= 0f)
        {
            if (ai.IsHoldingStrike)
            {
                releaseAt += Time.deltaTime;
            }
            else if (Time.time >= releaseAt)
            {
                releaseAt = -1f;
                if (ai.CurrentState == MonsterAI.State.Attacking)
                {
                    animator.CrossFadeInFixedTime(releaseHash, ReleaseFade, 0, 0f);
                }
            }
        }

        if (hasStaggered)
        {
            animator.SetBool(StaggeredHash, ai.CurrentState == MonsterAI.State.Staggered);
        }

        if (hasGuarding)
        {
            animator.SetBool(GuardingHash, ai.IsWeaponGuarding);
        }

        if (getUpAt >= 0f && Time.time >= getUpAt)
        {
            // A topple made longer while it lies (a boss's stage reel lands in it): it stays down until near the new end.
            float later = Time.time + ai.StaggerSecondsLeft - GetUpSeconds;
            if (ai.IsToppled && later > Time.time + 0.05f)
            {
                getUpAt = later;
            }
            else
            {
                getUpAt = -1f;
                if (ai.CurrentState == MonsterAI.State.Staggered && animator.isHuman && animator.HasState(0, GetUpStateHash))
                {
                    animator.CrossFadeInFixedTime(GetUpStateHash, 0.2f, 0, 0f);
                }
            }
        }

        if (hasReady)
        {
            // Facing one way and moving another (circling, a caster's relocation) is a strafe, not a forward walk slid sideways.
            animator.SetBool(ReadyHash, ai.IsWaitingTurn || (ai.IsStrafing && ai.CurrentState == MonsterAI.State.Chasing));
        }

        if (hasMove && Time.deltaTime > 0f)
        {
            // From how it actually moved, so a friend's copy (moved by the host's poses) steps the same way.
            Vector3 local = transform.InverseTransformDirection((transform.position - lastPosition) / Time.deltaTime) / bodyScale;
            animator.SetFloat(MoveXHash, local.x, MoveDampTime, Time.deltaTime);
            animator.SetFloat(MoveZHash, local.z, MoveDampTime, Time.deltaTime);
            if (hasStrafeRate)
            {
                animator.SetFloat(StrafeRateHash, StrafeRate(new Vector2(local.x, local.z)));
            }
        }

        lastPosition = transform.position;
    }

    private float MovedSpeed()
    {
        Vector3 here = transform.position;
        float measured = 0f;
        if (speedProbed && Time.deltaTime > 0f)
        {
            Vector3 step = here - speedProbe;
            step.y = 0f;
            measured = Mathf.Min(MostMeasuredSpeed, step.magnitude / Time.deltaTime);
        }

        speedProbe = here;
        speedProbed = true;
        return Mathf.Max(ai.Speed, measured);
    }

    // Locomotion blends idle, walk (at WalkThreshold) and run (at RunThreshold): the feet cover what the blended clips do, scaled by
    // this rate, so it is the speed over what they cover. Below the walk, idle and walk share the stride, so the walk clip's own speed
    // against its threshold is the rate; past the run, the run is played faster.
    private float LocomotionRate(float speed)
    {
        if (speed <= WalkThreshold)
        {
            // From the idle (rate 1: its breathing at its own pace) to the walk's own rate at the walk threshold.
            return Mathf.Clamp(Mathf.Lerp(1f, WalkThreshold / walkClipSpeed, speed / WalkThreshold), SlowestStride, Fastest);
        }

        float feet = speed < RunThreshold
            ? Mathf.Lerp(walkClipSpeed, runClipSpeed, (speed - WalkThreshold) / (RunThreshold - WalkThreshold))
            : runClipSpeed;
        return Mathf.Clamp(speed / feet, SlowestStride, Fastest);
    }

    // The Ready tree's rings: walk strafes (about 0.8 m/s, 1.333 s cycles) and run strafes (4.74 forward and to the sides, 2.37 back,
    // 0.667 s cycles). Between them the blend's stride and cycle are both weighted, so its feet cover stride / cycle, less than the
    // straight line between the rings' speeds (a Draug circling at 1.85 covered 1.4): the rate makes up the difference.
    private const float StrafeWalkRing = 0.8f;
    private const float StrafeWalkCycle = 1.333f;
    private const float StrafeRunCycle = 0.667f;

    private float StrafeRate(Vector2 local)
    {
        float speed = local.magnitude;
        if (speed <= StrafeWalkRing)
        {
            return 1f;
        }

        float run = Mathf.Lerp(StrafeRingForward, StrafeRingBack, Mathf.Clamp01(-local.y / speed));
        float w = Mathf.Clamp01((speed - StrafeWalkRing) / Mathf.Max(0.1f, run - StrafeWalkRing));
        float stride = Mathf.Lerp(StrafeWalkRing * StrafeWalkCycle, run * StrafeRunCycle, w);
        float cycle = Mathf.Lerp(StrafeWalkCycle, StrafeRunCycle, w);
        return Mathf.Clamp(speed / (stride / cycle), SlowestStride, Fastest);
    }

    private void CacheParameters()
    {
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return;
        }

        speedHash = Animator.StringToHash(speedParameter);
        attackHash = Animator.StringToHash(attackParameter);
        deadHash = Animator.StringToHash(deadParameter);
        staggerHash = Animator.StringToHash(staggerParameter);

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            hasSpeed |= parameter.nameHash == speedHash && parameter.type == AnimatorControllerParameterType.Float;
            hasAttack |= parameter.nameHash == attackHash && parameter.type == AnimatorControllerParameterType.Trigger;
            hasDead |= parameter.nameHash == deadHash && parameter.type == AnimatorControllerParameterType.Bool;
            hasStagger |= parameter.nameHash == staggerHash && parameter.type == AnimatorControllerParameterType.Trigger;
            hasResting |= parameter.nameHash == RestingHash && parameter.type == AnimatorControllerParameterType.Bool;
            hasStride |= parameter.nameHash == StrideRateHash && parameter.type == AnimatorControllerParameterType.Float;
            hasStaggered |= parameter.nameHash == StaggeredHash && parameter.type == AnimatorControllerParameterType.Bool;
            hasReady |= parameter.nameHash == ReadyHash && parameter.type == AnimatorControllerParameterType.Bool;
            hasMoveX |= parameter.nameHash == MoveXHash && parameter.type == AnimatorControllerParameterType.Float;
            hasMoveZ |= parameter.nameHash == MoveZHash && parameter.type == AnimatorControllerParameterType.Float;
            hasGuarding |= parameter.nameHash == GuardingHash && parameter.type == AnimatorControllerParameterType.Bool;
        }

        hasMove = hasMoveX && hasMoveZ;
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            hasLocomotionRate |= parameter.nameHash == LocomotionRateHash && parameter.type == AnimatorControllerParameterType.Float;
            hasStrafeRate |= parameter.nameHash == StrafeRateHash && parameter.type == AnimatorControllerParameterType.Float;
        }
        flinchLayer = animator.GetLayerIndex(FlinchLayerName);
    }

    // A strike with a state of its own plays it, fitted so the blow lands at its Hit Delay (the attack's Animation Speed); one without
    // plays the body's own move for its kind (a call, a cast, a throw) when it has one, else the controller's one Attack state. A state
    // with a "<state> Release" channels until the attack's Strike Lead before the hit, then lets go.
    private void OnAttackStarted()
    {
        releaseAt = -1f;
        strikePhaseAt = followPhaseAt = -1f;
        AttackDefinition attack = ai.CurrentAttack;
        if (animator != null && animator.isHuman && attack != null)
        {
            bool own = !string.IsNullOrEmpty(attack.AnimationState);
            string name = own ? attack.AnimationState : KindState(ai.ActiveAction);
            int state = name != null ? Animator.StringToHash(name) : 0;
            if (name != null && animator.HasState(0, state))
            {
                animator.SetFloat(ActionSpeedHash, own ? attack.AnimationSpeed : 1f);
                animator.CrossFadeInFixedTime(state, StrikeFade, 0, 0f);
                strikeStateHash = state;
                int release = Animator.StringToHash(name + " Release");
                if (animator.HasState(0, release))
                {
                    releaseHash = release;
                    float lead = attack.StrikeLead > 0f ? attack.StrikeLead : DefaultReleaseLead;
                    releaseAt = Time.time + Mathf.Max(0f, attack.HitDelay - lead);
                }
                else if (swingsLikeThePlayer && own && attack.StrikeLead > 0f && attack.StrikeSpeed > 0f)
                {
                    strikePhaseSpeed = attack.StrikeSpeed;
                    followPhaseSpeed = attack.FollowThroughSpeed > 0f ? attack.FollowThroughSpeed : 1f;
                    strikePhaseAt = Time.time + Mathf.Max(0f, attack.HitDelay - attack.StrikeLead);
                    followPhaseAt = Time.time + attack.HitDelay;
                }

                return;
            }
        }

        // A howl or a summon with no call to play stands and calls (the bite at the air read wrong, audit 2026-11-20).
        MonsterAttack calling = ai.ActiveAction;
        if (calling != null && (calling.kind == MonsterAttackKind.Howl || calling.kind == MonsterAttackKind.Summon)
            && (attack == null || string.IsNullOrEmpty(attack.AnimationState)))
        {
            return;
        }

        if (hasAttack)
        {
            animator.SetFloat(ActionSpeedHash, 1f);
            animator.SetTrigger(attackHash);
            strikeStateHash = AttackStateHash;
        }
    }

    private static readonly int AttackStateHash = Animator.StringToHash("Attack");

    // The strike's quicker part, then its follow-through, on time; a held wind-up (holdChance) pushes both back as it pushes the hit.
    private void UpdateSwingPhases()
    {
        if (strikePhaseAt < 0f && followPhaseAt < 0f)
        {
            return;
        }

        if (ai.IsHoldingStrike)
        {
            strikePhaseAt = strikePhaseAt >= 0f ? strikePhaseAt + Time.deltaTime : strikePhaseAt;
            followPhaseAt = followPhaseAt >= 0f ? followPhaseAt + Time.deltaTime : followPhaseAt;
            return;
        }

        if (strikePhaseAt >= 0f && Time.time >= strikePhaseAt)
        {
            strikePhaseAt = -1f;
            animator.SetFloat(ActionSpeedHash, strikePhaseSpeed);
        }

        if (followPhaseAt >= 0f && Time.time >= followPhaseAt)
        {
            followPhaseAt = -1f;
            animator.SetFloat(ActionSpeedHash, followPhaseSpeed);
        }
    }

    // The body's own move for an action that names none: a howl or a summon calls, a shot casts, a lob is thrown.
    private static string KindState(MonsterAttack action)
    {
        if (action == null)
        {
            return null;
        }

        switch (action.kind)
        {
            case MonsterAttackKind.Howl:
            case MonsterAttackKind.Summon:
                return "Call";
            case MonsterAttackKind.Projectile:
                return "Cast";
            case MonsterAttackKind.Lob:
                return "Throw";
            default:
                return null;
        }
    }

    // A blow or a parry broke its strike off before it landed: it reels out of the swing, so the interrupt is seen.
    private void OnInterrupted()
    {
        releaseAt = -1f;
        strikePhaseAt = followPhaseAt = -1f;
        interruptedFrame = Time.frameCount;
        if (animator == null || !animator.isHuman || !animator.enabled)
        {
            return;
        }

        int state = animator.HasState(0, HeavyHitStateHash) ? HeavyHitStateHash : HitStateHash;
        if (animator.HasState(0, state))
        {
            animator.CrossFadeInFixedTime(state, FlinchFade, 0, 0f);
        }
    }

    // Shaken by a brutal kill nearby: its arms flinch while its legs carry on.
    private void OnWavered() => FlinchArms();

    private bool FlinchArms() => PlayOnArms(FlinchStateHash);

    // Its raised shield took a blow, or its weapon guard turned one aside: the arm jolts.
    private void OnShieldBlocked() => PlayOnArms(BlockHitStateHash);

    private void OnGuardTurnedAside() => PlayOnArms(GuardHitStateHash);

    // Spotted someone while unaware: it readies its weapon while MonsterAI holds it still.
    private void PlayDraw()
    {
        if (animator != null && animator.isHuman && animator.enabled && animator.HasState(0, DrawStateHash))
        {
            animator.CrossFadeInFixedTime(DrawStateHash, StrikeFade, 0, 0f);
        }
    }

    // Badly hurt, it cries out (a roar, a call), unless busy striking or reeling.
    private void OnTurnedHurt()
    {
        if (animator != null && animator.isHuman && animator.enabled && animator.HasState(0, RoarStateHash)
            && ai.CurrentState != MonsterAI.State.Attacking && ai.CurrentState != MonsterAI.State.Staggered)
        {
            animator.SetFloat(ActionSpeedHash, 1.4f);
            animator.CrossFadeInFixedTime(RoarStateHash, StrikeFade, 0, 0f);
        }
    }

    private bool PlayOnArms(int state)
    {
        if (animator == null || !animator.isHuman || !animator.enabled || flinchLayer < 0 || !animator.HasState(flinchLayer, state))
        {
            return false;
        }

        animator.CrossFadeInFixedTime(state, FlinchFade, flinchLayer, 0f);
        return true;
    }

    private void OnDamaged(DamageInfo damage, DamageResult result)
    {
        float maxPoise = ai.Poise != null ? ai.Poise.Max : 30f;
        bool weighty = (damage.Technique & Weighty) != 0 || damage.Poise >= maxPoise * HeavyHitPoiseShare;
        lastBlowAt = Time.time;
        lastBlowFells = (damage.Technique & HitTechnique.Charged) != 0 || damage.Poise >= maxPoise * 0.75f;
        // A blow that staggered it already (the AI heard it first): a felling blow knocks it down. Never a great beast, boss or elite,
        // which only stagger (MonsterAI.TakeBlowOnPoise) unless they topple, and a topple is already down (OnStaggered: IsFelled).
        if (ai.CurrentState == MonsterAI.State.Staggered && lastBlowFells && !result.Killed && !ai.IsHeavyweight)
        {
            KnockDown(ai.StaggerSecondsLeft);
            return;
        }

        // An Unyielding elite doesn't flinch; a raised weapon guard shows its own jolt; a strike just broken off shows its reel.
        if (ai.IsUnyielding || ai.IsWeaponGuarding || interruptedFrame == Time.frameCount)
        {
            return;
        }

        bool felt = animator != null && animator.isHuman && animator.enabled && result.Damage > 0f && !result.Killed && !result.Blocked
            && !damage.IsOverTime && (result.Damage >= health.MaxHealth * flinchShare || weighty);
        if (!felt || ai.CurrentState == MonsterAI.State.Staggered)
        {
            return;
        }

        // Struck while it recovers from its own blow: only its arms flinch, a punish seen that changes nothing.
        if (ai.CurrentState == MonsterAI.State.Attacking)
        {
            if (ai.TryGetOpening(out _, out _))
            {
                FlinchArms();
            }

            return;
        }

        // From behind, the blow tips it forward (MonsterPose); a flinch back from the front would read wrong.
        Vector3 cameFrom = -damage.Direction;
        cameFrom.y = 0f;
        if (cameFrom.sqrMagnitude > 0.0001f && Vector3.Angle(transform.forward, cameFrom) > FromBehindDegrees)
        {
            return;
        }

        int state = weighty && animator.HasState(0, HeavyHitStateHash) ? HeavyHitStateHash : HitStateHash;
        if (animator.HasState(0, state))
        {
            animator.CrossFadeInFixedTime(state, FlinchFade, 0, 0f);
        }
    }

    // Down: it falls, lies while it staggers (the controller's "Down") and gets up a moment before the stagger ends.
    private void KnockDown(float seconds)
    {
        if (animator != null && animator.isHuman && animator.enabled && animator.HasState(0, KnockdownStateHash))
        {
            animator.CrossFadeInFixedTime(KnockdownStateHash, FlinchFade, 0, 0f);
            lastBlowFells = false;
            getUpAt = animator.HasState(0, GetUpStateHash) ? Time.time + Mathf.Max(0.5f, seconds - GetUpSeconds) : -1f;
        }
    }

    // Up off the ground and at the start of its hit, barely moving, so the blade finds a body standing where it should; then it plays out.
    private void OnHeldForFinish(float bladeIn)
    {
        if (animator != null && animator.isHuman && animator.HasState(0, HitStateHash))
        {
            animator.CrossFadeInFixedTime(HitStateHash, FinishFade, 0, 0f);
            animator.speed = 1f;
            holdSlowsAt = Time.time + FinishFade;
            holdEndsAt = Time.time + bladeIn;
        }
    }

    private void OnStaggered(float seconds)
    {
        // Felled (MonsterAI.IsFelled: caught reeling by a weighty blow, or a charged or crushing one broke it): down, not just reeling.
        // A great beast, boss or elite goes down only when the AI says so: a boss's topple (Monster Definition › Topple Seconds), never a
        // plain stagger after a weighty blow, which would lie it down for a stagger's length.
        if (ai.IsFelled || (!ai.IsHeavyweight && lastBlowFells && Time.time - lastBlowAt < 0.05f))
        {
            KnockDown(seconds);
            return;
        }

        if (hasStagger)
        {
            if (hasAttack) animator.ResetTrigger(attackHash);
            animator.SetTrigger(staggerHash);
        }
    }

    // The AI is free again before many strike clips end (Lost Rush: free at 1.3 s, clip to 5 s; stomps 2.4 s against 3.2 s), and a body
    // moving off while the clip plays out glides in its strike pose: the legs take over as it recovers. A stagger, a hop or death
    // play their own.
    private const float StrikeTailFade = 0.25f;

    private void HandStrikeTailToLegs(bool body, MonsterAI.State current)
    {
        int strike = strikeStateHash;
        strikeStateHash = 0;
        if (!body || strike == 0 || current == MonsterAI.State.Staggered || current == MonsterAI.State.Dead
            || current == MonsterAI.State.Hopping || current == MonsterAI.State.Attacking || !animator.HasState(0, LocomotionStateHash))
        {
            return;
        }

        // Standing, the follow-through plays out (a slam lands its weight); the legs take over once it moves (UpdateStrikeTail).
        tailStrikeHash = strike;
    }

    private int tailStrikeHash;
    private const float TailMovingSpeed = 0.3f;

    private void UpdateStrikeTail(float speed)
    {
        if (tailStrikeHash == 0 || animator == null || !animator.enabled)
        {
            return;
        }

        AnimatorStateInfo now = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
        bool inTail = now.shortNameHash == tailStrikeHash || (releaseHash != 0 && now.shortNameHash == releaseHash);
        if (!inTail || ai.CurrentState == MonsterAI.State.Attacking)
        {
            tailStrikeHash = 0;
            return;
        }

        if (speed > TailMovingSpeed || ai.IsWaitingTurn || ai.IsStrafing)
        {
            tailStrikeHash = 0;
            animator.CrossFadeInFixedTime(LocomotionStateHash, StrikeTailFade, 0, 0f);
        }
    }

    private void OnStateChanged(MonsterAI.State previous, MonsterAI.State current)
    {
        if (current == MonsterAI.State.Dead && hasDead)
        {
            animator.SetBool(deadHash, true);
        }

        bool body = animator != null && animator.isHuman && animator.enabled;
        if (previous == MonsterAI.State.Attacking)
        {
            strikePhaseAt = followPhaseAt = -1f;
            HandStrikeTailToLegs(animator != null && animator.enabled, current);
        }

        // A channel broken off before it let go (a rout, a recoil) mustn't loop on.
        if (previous == MonsterAI.State.Attacking && releaseAt >= 0f)
        {
            releaseAt = -1f;
            if (body && current == MonsterAI.State.Chasing && animator.HasState(0, LocomotionStateHash))
            {
                animator.CrossFadeInFixedTime(LocomotionStateHash, StrikeFade, 0, 0f);
            }
        }

        // A friend's copy draws when the host's creature goes from idle to the chase (the host's own draw comes from MonsterAI.DrawStarted;
        // a search for a lost target, through Investigating, isn't a first sighting).
        if (ai.IsRemote && current == MonsterAI.State.Chasing && previous == MonsterAI.State.Idle
            && ai.Definition != null && ai.Definition.SpottedPause > 0f)
        {
            PlayDraw();
        }

        if (current == MonsterAI.State.Hopping && animator != null && animator.isHuman && animator.HasState(0, HopStateHash))
        {
            animator.SetFloat(ActionSpeedHash, HopPlayback);
            animator.CrossFadeInFixedTime(HopStateHash, HopFade, 0, 0f);
        }
    }
}
