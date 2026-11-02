using UnityEngine;

/// <summary>
/// Plays a monster's animations from what its MonsterAI is doing. It only sets parameters its Animator Controller has,
/// so a monster with a simpler controller, or none yet, still works.
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

    private const float StrikeFade = 0.1f;
    private const float FlinchFade = 0.06f;
    private static readonly int ActionSpeedHash = Animator.StringToHash("ActionSpeed");
    private static readonly int HitStateHash = Animator.StringToHash("Hit");

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
        if (health != null)
        {
            health.Damaged += OnDamaged;
        }
    }

    private void OnDisable()
    {
        ai.AttackStarted -= OnAttackStarted;
        ai.StateChanged -= OnStateChanged;
        ai.Staggered -= OnStaggered;
        if (health != null)
        {
            health.Damaged -= OnDamaged;
        }
    }

    private void Update()
    {
        // Story movement owns locomotion while the AI is disabled.
        if (ai.enabled && hasSpeed)
        {
            animator.SetFloat(speedHash, ai.Speed, speedDampTime, Time.deltaTime);
        }
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
        }
    }

    // A strike with a state of its own plays it, fitted so the blow lands at its Hit Delay (Set Up Combat Animations works the speed
    // out); any other plays the controller's one Attack state.
    private void OnAttackStarted()
    {
        AttackDefinition attack = ai.CurrentAttack;
        if (animator != null && animator.isHuman && attack != null && !string.IsNullOrEmpty(attack.AnimationState))
        {
            int state = Animator.StringToHash(attack.AnimationState);
            if (animator.HasState(0, state))
            {
                animator.SetFloat(ActionSpeedHash, attack.AnimationSpeed);
                animator.CrossFadeInFixedTime(state, StrikeFade, 0, 0f);
                return;
            }
        }

        if (hasAttack)
        {
            animator.SetFloat(ActionSpeedHash, 1f);
            animator.SetTrigger(attackHash);
        }
    }

    private void OnDamaged(DamageInfo damage, DamageResult result)
    {
        bool flinches = animator != null && animator.isHuman && animator.enabled && result.Damage > 0f && !result.Killed && !result.Blocked
            && !damage.IsOverTime && result.Damage >= health.MaxHealth * flinchShare
            && ai.CurrentState != MonsterAI.State.Attacking && ai.CurrentState != MonsterAI.State.Staggered;
        if (flinches && animator.HasState(0, HitStateHash))
        {
            animator.CrossFadeInFixedTime(HitStateHash, FlinchFade, 0, 0f);
        }
    }

    private void OnStaggered(float seconds)
    {
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
    }
}
