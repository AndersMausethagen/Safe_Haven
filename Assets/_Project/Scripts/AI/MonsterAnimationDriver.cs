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
/// Answering back (phase C): felled, it falls, lies ("Down") and gets up ("Get Up") as its stagger ends; its weapon guard ("Guarding",
/// the "Guard" state) jolts when a blow is turned aside ("Guard Hit" on the arms layer), as its shield does ("Block Hit"); badly hurt it
/// cries out ("Roar"); an Unyielding elite doesn't flinch.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MonsterAI))]
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

    /// <summary>The bool a controller has when it has a clip for grazing and sleeping.</summary>
    public const string RestingParameter = "Resting";

    private static readonly int RestingHash = Animator.StringToHash(RestingParameter);
    private static readonly int StrideRateHash = Animator.StringToHash("StrideRate");
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
        if (ai.enabled && hasSpeed)
        {
            animator.SetFloat(speedHash, ai.Speed, speedDampTime, Time.deltaTime);
        }

        if (hasStride && ai.enabled)
        {
            animator.SetFloat(StrideRateHash, ai.Speed > 0.2f ? Mathf.Clamp(ai.Speed / strideSpeed, 0.5f, 2.5f) : 1f);
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
            getUpAt = -1f;
            if (ai.CurrentState == MonsterAI.State.Staggered && animator.isHuman && animator.HasState(0, GetUpStateHash))
            {
                animator.CrossFadeInFixedTime(GetUpStateHash, 0.2f, 0, 0f);
            }
        }

        if (hasReady)
        {
            animator.SetBool(ReadyHash, ai.IsWaitingTurn);
        }

        if (hasMove && Time.deltaTime > 0f)
        {
            // From how it actually moved, so a friend's copy (moved by the host's poses) steps the same way.
            Vector3 local = transform.InverseTransformDirection((transform.position - lastPosition) / Time.deltaTime);
            animator.SetFloat(MoveXHash, local.x, MoveDampTime, Time.deltaTime);
            animator.SetFloat(MoveZHash, local.z, MoveDampTime, Time.deltaTime);
        }

        lastPosition = transform.position;
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
        flinchLayer = animator.GetLayerIndex(FlinchLayerName);
    }

    // A strike with a state of its own plays it, fitted so the blow lands at its Hit Delay (the attack's Animation Speed); one without
    // plays the body's own move for its kind (a call, a cast, a throw) when it has one, else the controller's one Attack state. A state
    // with a "<state> Release" channels until the attack's Strike Lead before the hit, then lets go.
    private void OnAttackStarted()
    {
        releaseAt = -1f;
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
                int release = Animator.StringToHash(name + " Release");
                if (animator.HasState(0, release))
                {
                    releaseHash = release;
                    float lead = attack.StrikeLead > 0f ? attack.StrikeLead : DefaultReleaseLead;
                    releaseAt = Time.time + Mathf.Max(0f, attack.HitDelay - lead);
                }

                return;
            }
        }

        if (hasAttack)
        {
            animator.SetFloat(ActionSpeedHash, 1f);
            animator.SetTrigger(attackHash);
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
        // which only stagger (MonsterAI.TakeBlowOnPoise).
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
        if (ai.IsFelled || (lastBlowFells && Time.time - lastBlowAt < 0.05f))
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

    private void OnStateChanged(MonsterAI.State previous, MonsterAI.State current)
    {
        if (current == MonsterAI.State.Dead && hasDead)
        {
            animator.SetBool(deadHash, true);
        }

        bool body = animator != null && animator.isHuman && animator.enabled;
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
