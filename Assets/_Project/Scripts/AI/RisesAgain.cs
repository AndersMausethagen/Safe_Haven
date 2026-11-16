using UnityEngine;

/// <summary>
/// The haugbui's way (the Draug): struck down by anything but fire or a finishing blow, it does not die. It falls
/// reeling, and a few seconds later stands again with part of its strength. While it lies there, only a finishing blow (it
/// is staggered, so one can be lined up) or fire ends it; ordinary blows do nothing. It rises once: the second time it
/// falls, it stays down. On the creature's root, beside its Damageable.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Damageable))]
public sealed class RisesAgain : MonoBehaviour
{
    [Tooltip("Share of its health it stands up with.")]
    [SerializeField, Range(0.05f, 1f)] private float riseWith = 0.35f;
    [Tooltip("Seconds it lies reeling before it rises: the time to finish it.")]
    [SerializeField, Min(0.5f)] private float liesFor = 4f;
    [Tooltip("Said when it falls, after its name.")]
    [SerializeField] private string fallText = "falls, but the dead do not stay down. Finish it, or burn it!";
    [Tooltip("Said when it stands again, after its name.")]
    [SerializeField] private string riseText = "rises again!";

    private Damageable health;
    private MonsterAI ai;
    private bool holding;
    private bool down;
    private bool risen;
    private float riseAt;

    private void Awake()
    {
        health = GetComponent<Damageable>();
        ai = GetComponent<MonsterAI>();
    }

    private void OnEnable()
    {
        health.Damaged += OnDamaged;
        // In a friend's game (co-op) the host's game decides whether it rises; its copy here only shows it.
        if (ai == null || !ai.IsRemote)
        {
            health.HoldFloor(this, 1f);
            holding = true;
        }
    }

    private void OnDisable()
    {
        health.Damaged -= OnDamaged;
        Release();
    }

    private void Release()
    {
        if (holding)
        {
            health.ReleaseFloor(this);
            holding = false;
        }
    }

    private void OnDamaged(DamageInfo damage, DamageResult result)
    {
        if (!holding || !health.IsAlive || health.CurrentHealth > 1.01f)
        {
            return;
        }

        bool burning = TryGetComponent(out Burning fire) && fire.IsBurning;
        if (damage.Type == DamageType.Fire || burning || (damage.Technique & HitTechnique.Finisher) != 0)
        {
            Release();
            // With the blow itself, so it dies of it: the finisher's kill, the fire's, the cut that severs.
            health.Finish(damage);
            return;
        }

        if (down)
        {
            return;
        }

        down = true;
        riseAt = Time.time + liesFor;
        if (ai != null)
        {
            ai.StaggerAtLeast(liesFor);
            GameMessages.Post($"{ai.Definition.DisplayName} {fallText}", MessageKind.Warning);
        }
    }

    private void Update()
    {
        if (!down || Time.time < riseAt)
        {
            return;
        }

        down = false;
        if (!health.IsAlive)
        {
            return;
        }

        risen = true;
        Release();
        health.SetHealthFraction(riseWith);
        if (ai != null)
        {
            GameMessages.Post($"{ai.Definition.DisplayName} {riseText}", MessageKind.Warning);
        }

        enabled = !risen;
    }
}
