using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// A timid animal (deer, fox): it grazes, ambles about its glade and bolts from whatever frightens it. The player is heard
/// as the birds hear them (PlayerNoise), so a run sends it off early and a crouched walk gets close; a creature coming
/// through the trees frightens it too, so animals fleeing towards the player are a warning. Purely life in the world: it
/// can't be hurt, it carries nothing and it is gone once it has fled out of sight. Made and cleared by WildlifeHerd.
/// <para>Its Animator needs a float "Speed" (metres a second) and a bool "Eating".</para>
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public sealed class Wildlife : MonoBehaviour
{
    private static readonly int SpeedId = Animator.StringToHash("Speed");
    private static readonly int EatingId = Animator.StringToHash("Eating");

    private enum State { Graze, Wander, Alert, Flee }

    [SerializeField, Min(0.1f)] private float walkSpeed = 1.1f;
    [SerializeField, Min(0.1f)] private float runSpeed = 7.5f;
    [Tooltip("Metres round its home it ambles in.")]
    [SerializeField, Min(1f)] private float wanderRadius = 9f;
    [Tooltip("How near a quiet player may come; noise adds to it (metres of startle per metre the player is heard).")]
    [SerializeField, Min(0f)] private float shyDistance = 7f;
    [SerializeField, Min(0f)] private float perHeardMetre = 0.9f;
    [Tooltip("A creature this near sends it off.")]
    [SerializeField, Min(0f)] private float creatureDistance = 14f;
    [Tooltip("The call it gives as it bolts (a deer's bark).")]
    [SerializeField] private SoundDefinition alarm;
    [Tooltip("Seconds it stands, head up, before it runs from something that isn't on top of it.")]
    [SerializeField, Min(0f)] private float alertSeconds = 0.7f;
    [Tooltip("Its winter coat where snow lies (RGB), and how far its coat turns to it (A; 0: no winter coat). Only the " +
             "body's materials (names with Main) change: a fox goes white, a deer greyer.")]
    [SerializeField] private Color winterCoat = new Color(1f, 1f, 1f, 0f);

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    public void SetWinterCoat(Color coat) => winterCoat = coat;

    // Puts on the winter coat if snow lies now (animals are made when the player comes near, so once is enough).
    private void WearCoat()
    {
        WeatherSystem weather = WeatherSystem.Active;
        float snow = weather != null ? weather.Snow : 0f;
        float amount = winterCoat.a * Mathf.Clamp01((snow - 0.2f) / 0.4f);
        if (amount <= 0.01f)
        {
            return;
        }

        var block = new MaterialPropertyBlock();
        foreach (Renderer drawn in GetComponentsInChildren<Renderer>())
        {
            Material[] materials = drawn.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null || !material.name.Contains("Main") || !material.HasProperty(BaseColorId))
                {
                    continue;
                }

                Color from = material.GetColor(BaseColorId);
                drawn.GetPropertyBlock(block, i);
                block.SetColor(BaseColorId, Color.Lerp(from, new Color(winterCoat.r, winterCoat.g, winterCoat.b, from.a), amount));
                drawn.SetPropertyBlock(block, i);
            }
        }
    }

    private NavMeshAgent agent;
    private Animator animator;
    private State state;
    private float until;
    private float nextLook;
    private Vector3 home;
    private Vector3 threat;

    /// <summary>True once it has fled out of sight: its herd removes it.</summary>
    public bool HasFled { get; private set; }

    public void Settle(Vector3 homePoint)
    {
        home = homePoint;
        Graze();
    }

    private void Awake()
    {
        WalkerFootprints.AddTo(gameObject, 0.55f, 0.14f);
        WearCoat();
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
        agent.speed = walkSpeed;
        home = transform.position;
        Graze();
    }

    private void Update()
    {
        if (Time.time >= nextLook)
        {
            nextLook = Time.time + 0.25f;
            Look();
        }

        switch (state)
        {
            case State.Graze when Time.time >= until:
                Wander();
                break;
            case State.Wander when !agent.pathPending && agent.remainingDistance < 0.4f:
                Graze();
                break;
            case State.Alert when Time.time >= until:
                Flee(threat);
                break;
            case State.Flee:
                Vector3 player = Player.Current != null ? Player.Current.transform.position : transform.position;
                if ((!agent.pathPending && agent.remainingDistance < 1f) || Time.time >= until)
                {
                    // Out of reach of the player: gone into the trees. Still close: run on.
                    if ((player - transform.position).sqrMagnitude > 30f * 30f)
                    {
                        HasFled = true;
                        gameObject.SetActive(false);
                    }
                    else
                    {
                        Flee(threat);
                    }
                }

                break;
        }

        if (animator != null)
        {
            animator.SetFloat(SpeedId, agent.velocity.magnitude);
            animator.SetBool(EatingId, state == State.Graze);
        }
    }

    private void Look()
    {
        if (state == State.Flee)
        {
            return;
        }

        Player player = Player.Current;
        if (player != null)
        {
            float heard = player.Noise != null ? player.Noise.Level : 0f;
            float startle = shyDistance + heard * perHeardMetre;
            Vector3 offset = player.transform.position - transform.position;
            offset.y = 0f;
            if (offset.sqrMagnitude < startle * startle)
            {
                Startle(player.transform.position, offset.magnitude < startle * 0.5f);
                return;
            }
        }

        foreach (MonsterAI monster in MonsterAI.Active)
        {
            if (monster == null)
            {
                continue;
            }

            Vector3 offset = monster.transform.position - transform.position;
            offset.y = 0f;
            if (offset.sqrMagnitude < creatureDistance * creatureDistance)
            {
                Startle(monster.transform.position, true);
                return;
            }
        }
    }

    private void Startle(Vector3 from, bool atOnce)
    {
        threat = from;
        if (atOnce || state == State.Alert)
        {
            if (state != State.Alert || Time.time >= until)
            {
                Flee(from);
            }

            return;
        }

        // Head up, looking at it: the moment a sneaking player has to stop or back off.
        state = State.Alert;
        until = Time.time + alertSeconds;
        agent.ResetPath();
        Face(from);
    }

    private void Graze()
    {
        state = State.Graze;
        until = Time.time + Random.Range(4f, 11f);
        agent.ResetPath();
        agent.speed = walkSpeed;
    }

    private void Wander()
    {
        Vector2 around = Random.insideUnitCircle * wanderRadius;
        if (NavMesh.SamplePosition(home + new Vector3(around.x, 0f, around.y), out NavMeshHit hit, 3f, NavMesh.AllAreas))
        {
            state = State.Wander;
            agent.speed = walkSpeed;
            agent.SetDestination(hit.position);
        }
        else
        {
            Graze();
        }
    }

    private void Flee(Vector3 from)
    {
        if (state != State.Flee && alarm != null)
        {
            AudioManager.Play(alarm, transform.position + Vector3.up);
        }

        state = State.Flee;
        until = Time.time + 6f;
        agent.speed = runSpeed;
        Vector3 away = transform.position - from;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f)
        {
            away = Random.insideUnitSphere;
            away.y = 0f;
        }

        // Away from it, a little to one side, the way a deer breaks.
        Vector3 heading = Quaternion.Euler(0f, Random.Range(-35f, 35f), 0f) * away.normalized;
        for (float distance = 28f; distance > 6f; distance -= 6f)
        {
            if (NavMesh.SamplePosition(transform.position + heading * distance, out NavMeshHit hit, 4f, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
                return;
            }
        }
    }

    private void Face(Vector3 point)
    {
        Vector3 to = point - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.LookRotation(to);
        }
    }
}
