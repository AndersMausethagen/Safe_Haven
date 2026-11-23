using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// A body dragged or thrown over a short time, whatever moves it otherwise: the player (CharacterController), a creature
/// (NavMeshAgent) or anything else (its transform). A bog troll's tongue drags its prey in (Attack Definition › Pull); a
/// huldra's call draws the one who hears it (Monster Attack › Foe Pull). Added to the body when first needed.
/// </summary>
[DisallowMultipleComponent]
[OnAFriendsCopy(CopyRule.Same, "a pull moves the body here; the host's poses carry it on")]
public sealed class ForcedMove : MonoBehaviour
{
    // A drag stops this far short of whoever drags, so it never pulls a body into theirs.
    private const float StopShort = 1.3f;
    private const float DragSeconds = 0.35f;

    private CharacterController controller;
    private NavMeshAgent agent;
    private Vector3 velocity;
    private float until;

    /// <summary>After a hit that got through: the attack's Pull drags the target toward the attacker (or throws it off). Not one that
    /// was dodged (the bog troll's drag and the shield rush are escaped by a dodge), nor a friend's body in the host's game.</summary>
    public static void AfterHit(AttackDefinition attack, IDamageable victim, Combatant source, in DamageResult result)
    {
        if (attack == null || Mathf.Abs(attack.Pull) < 0.01f || source == null || !result.Landed || result.Killed || result.Parried
            || !(victim is Component body) || body == null)
        {
            return;
        }

        Drag(body.transform, source.transform.position, attack.Pull, DragSeconds);
    }

    /// <summary>
    /// True for what is built, which no blow moves: a building or wall (BuildingObject) or anything struck as a structure (the user,
    /// 2026-11-23: a breach's trolls knocked the buildings out of place, their pulls and shoves dragging whatever they hit).
    /// </summary>
    public static bool IsBuilt(Transform body)
    {
        if (body == null)
        {
            return false;
        }

        if (body.GetComponentInParent<BuildingObject>() != null)
        {
            return true;
        }

        IDamageable damageable = body.GetComponentInParent<IDamageable>();
        return damageable != null && damageable.Category == TargetCategory.Structure;
    }

    /// <summary>Moves <paramref name="body"/> <paramref name="metres"/> toward <paramref name="toward"/> (negative: away)
    /// over <paramref name="seconds"/>, on the flat.</summary>
    public static void Drag(Transform body, Vector3 toward, float metres, float seconds)
    {
        if (body == null || Mathf.Abs(metres) < 0.01f || IsBuilt(body))
        {
            return;
        }

        Vector3 offset = toward - body.position;
        offset.y = 0f;
        float distance = offset.magnitude;
        if (distance < 0.01f)
        {
            return;
        }

        if (metres > 0f)
        {
            metres = Mathf.Min(metres, distance - StopShort);
            if (metres <= 0f)
            {
                return;
            }
        }

        if (!body.TryGetComponent(out ForcedMove move))
        {
            move = body.gameObject.AddComponent<ForcedMove>();
        }

        move.Begin(offset / distance * metres, Mathf.Max(0.05f, seconds));
    }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        agent = GetComponent<NavMeshAgent>();
    }

    private void Begin(Vector3 move, float seconds)
    {
        velocity = move / seconds;
        until = Time.time + seconds;
    }

    // After the body's own movement this frame, so the drag is added to it rather than undone by it.
    private void LateUpdate()
    {
        if (Time.time >= until)
        {
            return;
        }

        Vector3 step = velocity * Time.deltaTime;
        if (controller != null && controller.enabled)
        {
            controller.Move(step);
        }
        else if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.Move(step);
        }
        else
        {
            transform.position += step;
        }
    }
}
