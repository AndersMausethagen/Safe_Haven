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
    [SerializeField, Min(0f)] private float speedDampTime = 0.1f;

    private MonsterAI ai;
    private int speedHash;
    private int attackHash;
    private int deadHash;
    private bool hasSpeed;
    private bool hasAttack;
    private bool hasDead;

    private void Awake()
    {
        ai = GetComponent<MonsterAI>();
        animator = animator != null ? animator : GetComponentInChildren<Animator>();
        CacheParameters();
    }

    private void OnEnable()
    {
        ai.AttackStarted += OnAttackStarted;
        ai.StateChanged += OnStateChanged;
    }

    private void OnDisable()
    {
        ai.AttackStarted -= OnAttackStarted;
        ai.StateChanged -= OnStateChanged;
    }

    private void Update()
    {
        if (hasSpeed)
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

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            hasSpeed |= parameter.nameHash == speedHash && parameter.type == AnimatorControllerParameterType.Float;
            hasAttack |= parameter.nameHash == attackHash && parameter.type == AnimatorControllerParameterType.Trigger;
            hasDead |= parameter.nameHash == deadHash && parameter.type == AnimatorControllerParameterType.Bool;
        }
    }

    private void OnAttackStarted()
    {
        if (hasAttack)
        {
            animator.SetTrigger(attackHash);
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
