using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// What a track follows (Docs/RaidEventsPlan_2026-11-26.md E3 Track: the White Hart): an animal that walks on ahead to each sign and waits
/// there, out of reach, until the player comes close, then goes on. It leaves its own prints (WalkerFootprints). Can be tinted (a white
/// hart from a stag). On the root of the leader's prefab, beside a NavMeshAgent and an Animator with a float "Speed"; no other AI.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public sealed class TrackLeader : MonoBehaviour
{
    private static readonly int SpeedId = Animator.StringToHash("Speed");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    [SerializeField, Min(0.5f)] private float walkSpeed = 1.6f;
    [Tooltip("When the player is this close as it sets off, it runs.")]
    [SerializeField, Min(0.5f)] private float runSpeed = 5.5f;
    [SerializeField, Min(0f)] private float runWhenWithin = 7f;
    [Tooltip("Its coat (RGB) and how far it turns to it (A; 0: its own).")]
    [SerializeField] private Color tint = new Color(1f, 1f, 1f, 0f);
    [Tooltip("Prints left as it walks: spacing and size (0: none).")]
    [SerializeField, Min(0f)] private float printSpacing = 0.55f;
    [SerializeField, Min(0f)] private float printSize = 0.14f;

    private NavMeshAgent agent;
    private Animator animator;
    private bool hasSpeed;

    /// <summary>True while it is on its way to a sign.</summary>
    public bool IsMoving => agent != null && agent.isOnNavMesh && (agent.pathPending || agent.remainingDistance > agent.stoppingDistance + 0.2f);

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                hasSpeed |= parameter.nameHash == SpeedId && parameter.type == AnimatorControllerParameterType.Float;
            }
        }

        if (printSpacing > 0f && printSize > 0f)
        {
            WalkerFootprints.AddTo(gameObject, printSpacing, printSize);
        }

        Tint();
    }

    /// <summary>Goes on to <paramref name="point"/>; runs if the player is close.</summary>
    public void GoTo(Vector3 point, Vector3 player)
    {
        if (agent == null || !agent.isOnNavMesh)
        {
            return;
        }

        Vector3 offset = player - transform.position;
        offset.y = 0f;
        agent.speed = offset.magnitude <= runWhenWithin ? runSpeed : walkSpeed;
        agent.SetDestination(point);
    }

    private void Update()
    {
        if (hasSpeed && agent != null)
        {
            animator.SetFloat(SpeedId, agent.velocity.magnitude, 0.1f, Time.deltaTime);
        }
    }

    private void Tint()
    {
        if (tint.a <= 0.01f)
        {
            return;
        }

        var block = new MaterialPropertyBlock();
        foreach (Renderer drawn in GetComponentsInChildren<Renderer>())
        {
            Material[] materials = drawn.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == null || !materials[i].HasProperty(BaseColorId))
                {
                    continue;
                }

                Color from = materials[i].GetColor(BaseColorId);
                drawn.GetPropertyBlock(block, i);
                block.SetColor(BaseColorId, Color.Lerp(from, new Color(tint.r, tint.g, tint.b, from.a), tint.a));
                drawn.SetPropertyBlock(block, i);
            }
        }
    }
}
