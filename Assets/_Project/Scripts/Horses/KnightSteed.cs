using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// The horse under a mounted monster (the undead knights): it has no will of its own. Its Animator (the Horse controller)
/// is driven from how the monster moves - the gait by speed, the turn by how fast it turns, played as fast as the speed
/// needs, as HorseMotion does a living horse's - and it flinches when the monster is struck and falls when it dies.
/// Hoofbeats on every beat near the player.
/// </summary>
[DisallowMultipleComponent]
public sealed class KnightSteed : MonoBehaviour
{
    private static readonly int GaitId = Animator.StringToHash("Gait");
    private static readonly int TurnId = Animator.StringToHash("Turn");
    private static readonly int DeadId = Animator.StringToHash("Dead");
    private static readonly int DeathKindId = Animator.StringToHash("DeathKind");
    private static readonly int HitId = Animator.StringToHash("Hit");
    private static readonly int HitSideId = Animator.StringToHash("HitSide");
    private static readonly float[] HoofBeats = { 0.28f, 0.34f, 0.2f, 0.12f, 0.1f };

    [SerializeField] private Animator steed;
    [Tooltip("Metres a second each gait's clip shows (copied from the living horse by BuildUndeadKnights).")]
    [SerializeField] private float[] gaitSpeeds = { 1.33f, 3.13f, 4.85f, 7.32f, 10.23f };
    [SerializeField, Min(10f)] private float turnInPlaceRate = 84f;
    [SerializeField] private SoundDefinition hooves;

    private NavMeshAgent agent;
    private Damageable health;
    private float lastYaw;
    private float turnRate;
    private Vector3 lastPlace;
    private float measured;
    private float nextHoof;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<Damageable>();
        lastYaw = transform.eulerAngles.y;
        lastPlace = transform.position;
        HoofPrints.AddTo(gameObject);
    }

    private void OnEnable()
    {
        if (health != null)
        {
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
        }
    }

    private void Update()
    {
        if (steed == null || health != null && !health.IsAlive)
        {
            return;
        }

        // By how fast it really goes: a knight in a friend's game is a puppet with no agent driving it (CoopCreatures).
        Vector3 place = transform.position;
        Vector3 step = place - lastPlace;
        step.y = 0f;
        lastPlace = place;
        float went = Time.deltaTime > 0f && step.sqrMagnitude < 25f ? step.magnitude / Time.deltaTime : measured;
        measured = Mathf.Lerp(measured, went, 1f - Mathf.Exp(-10f * Time.deltaTime));
        float speed = agent != null && agent.enabled && agent.updatePosition ? Mathf.Max(agent.velocity.magnitude, measured) : measured;
        int gait = HorseMotion.GaitFor(speed, gaitSpeeds);
        float yaw = transform.eulerAngles.y;
        float rate = Time.deltaTime > 0f ? Mathf.DeltaAngle(lastYaw, yaw) / Time.deltaTime : 0f;
        lastYaw = yaw;
        turnRate = Mathf.Lerp(turnRate, rate, 1f - Mathf.Exp(-12f * Time.deltaTime));
        steed.SetFloat(GaitId, gait, 0.1f, Time.deltaTime);
        steed.SetFloat(TurnId, Mathf.Clamp(turnRate / turnInPlaceRate, -1f, 1f), 0.08f, Time.deltaTime);
        steed.speed = gait == 0 ? 1f : Mathf.Clamp(speed / gaitSpeeds[gait - 1], 0.35f, 1.5f);

        Player player = Player.Current;
        if (gait > 0 && hooves != null && player != null && (player.transform.position - transform.position).sqrMagnitude < 40f * 40f)
        {
            nextHoof -= Time.deltaTime * steed.speed;
            if (nextHoof <= 0f)
            {
                nextHoof = HoofBeats[Mathf.Clamp(gait - 1, 0, HoofBeats.Length - 1)];
                AudioManager.Play(hooves, transform.position);
            }
        }
    }

    private void OnDamaged(DamageInfo damage, DamageResult result)
    {
        if (steed == null || health == null || !health.IsAlive)
        {
            return;
        }

        Vector3 from = damage.Source != null ? damage.Source.transform.position : transform.position - transform.forward;
        steed.SetInteger(HitSideId, HorseMotion.HitSide(transform.forward, from - transform.position));
        steed.SetTrigger(HitId);
    }

    private void OnDied(Damageable victim, DamageInfo killingBlow)
    {
        if (steed == null)
        {
            return;
        }

        steed.speed = 1f;
        steed.SetInteger(DeathKindId, agent != null && agent.velocity.magnitude > 6f ? 4 : Random.Range(0, 4));
        steed.SetBool(DeadId, true);
    }
}
