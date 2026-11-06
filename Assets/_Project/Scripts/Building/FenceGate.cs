using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// A gate in a fence that opens for the Haven's own people: the player, residents and companions walking up to it swing
/// it open away from them, and it closes once nobody is by it. Creatures it stays shut to, so they have to break it
/// (it is a building with its own health). The leaf is a child that turns on its hinge (its pivot), with its own
/// collider; its NavMeshObstacle doesn't carve, so residents still plan their way through it.
/// <para>Where it belongs: on a gate building's prefab (the Skigard Gate, AgentScripts/BuildSkigard.cs).</para>
/// </summary>
[DisallowMultipleComponent]
public sealed class FenceGate : MonoBehaviour
{
    private static readonly Collider[] Near = new Collider[16];

    [SerializeField] private Transform leaf;
    [Tooltip("Degrees it swings open.")]
    [SerializeField, Range(30f, 140f)] private float openAngle = 100f;
    [Tooltip("Metres from the gate's middle someone of the Haven opens it at.")]
    [SerializeField, Min(0.5f)] private float openWithin = 2.6f;
    [Tooltip("Degrees per second it swings.")]
    [SerializeField, Min(10f)] private float swingSpeed = 220f;
    [SerializeField] private SoundDefinition swingSound;

    private float angle;
    private float wanted;
    private float nextLook;

    public bool IsOpen => Mathf.Abs(wanted) > 0.01f;

    public void Configure(Transform swings, float degrees, SoundDefinition sound)
    {
        leaf = swings;
        openAngle = degrees;
        swingSound = sound;
    }

    /// <summary>Which way the gate swings for someone at <paramref name="localSide"/> (their local z): away from them.</summary>
    public static float SwingFor(float localSide, float degrees) => localSide >= 0f ? -degrees : degrees;

    private void Update()
    {
        if (leaf == null)
        {
            return;
        }

        if (Time.time >= nextLook)
        {
            nextLook = Time.time + 0.15f;
            float next = Look();
            if (!Mathf.Approximately(next, wanted))
            {
                if (Mathf.Approximately(wanted, 0f) || Mathf.Approximately(next, 0f))
                {
                    AudioManager.Play(swingSound, leaf.position);
                }

                wanted = next;
            }
        }

        if (!Mathf.Approximately(angle, wanted))
        {
            angle = Mathf.MoveTowards(angle, wanted, swingSpeed * Time.deltaTime);
            leaf.localRotation = Quaternion.Euler(0f, angle, 0f);
        }
    }

    // Open away from the nearest of the Haven's people within reach; shut if there is none.
    private float Look()
    {
        Vector3 centre = transform.position + Vector3.up;
        int count = Physics.OverlapSphereNonAlloc(centre, openWithin, Near, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        float swing = 0f;
        for (int i = 0; i < count; i++)
        {
            Collider found = Near[i];
            if (found == null || !OfTheHaven(found))
            {
                continue;
            }

            Vector3 at = found.transform.position;
            float distance = (at - centre).sqrMagnitude;
            if (distance < best)
            {
                best = distance;
                // Keep it swinging the way it is while it's open, so it doesn't flap past someone walking through.
                swing = IsOpen ? wanted : SwingFor(transform.InverseTransformPoint(at).z, openAngle);
            }
        }

        return swing;
    }

    private static bool OfTheHaven(Collider found)
    {
        if (found.GetComponentInParent<Player>() != null)
        {
            return true;
        }

        if (found.GetComponentInParent<MonsterAI>() != null)
        {
            return false;
        }

        return found.GetComponentInParent<ResidentNpc>() != null || found.GetComponentInParent<Companion>() != null;
    }
}
