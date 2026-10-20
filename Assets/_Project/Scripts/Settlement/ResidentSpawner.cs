using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shows Safe Haven's residents (GDD §15): for everyone in SettlementState it places a ResidentNpc with their model at a
/// home spot, and adds newcomers as they arrive (their arrival line is shown in the raid summary).
/// <para>
/// Where it belongs: one per Safe Haven scene, with a few home spots (empty objects) and the generic resident prefab.
/// </para>
/// </summary>
[DisallowMultipleComponent]
public sealed class ResidentSpawner : MonoBehaviour
{
    [Tooltip("The generic resident: collider, Interactable and ResidentNpc. The person's model is added as a child.")]
    [SerializeField] private ResidentNpc residentPrefab;
    [Tooltip("Where residents live, in order of arrival.")]
    [SerializeField] private Transform[] homeSpots = new Transform[0];

    private readonly Dictionary<ResidentDefinition, ResidentNpc> spawned = new Dictionary<ResidentDefinition, ResidentNpc>();
    private bool started;

    private void OnEnable()
    {
        GameSession.Settlement.Changed += Sync;
        if (started)
        {
            Sync();
        }
    }

    private void OnDisable()
    {
        GameSession.Settlement.Changed -= Sync;
    }

    private void Start()
    {
        started = true;
        Sync();
    }

    private void Sync()
    {
        if (residentPrefab == null)
        {
            return;
        }

        IReadOnlyList<ResidentRecord> residents = GameSession.Settlement.Residents;
        for (int i = 0; i < residents.Count; i++)
        {
            ResidentDefinition resident = residents[i].Resident;
            if (resident == null || spawned.ContainsKey(resident))
            {
                continue;
            }

            Transform spot = homeSpots.Length > 0 ? homeSpots[i % homeSpots.Length] : transform;
            ResidentNpc npc = Instantiate(residentPrefab, spot.position, spot.rotation, transform);
            if (resident.Model != null)
            {
                Instantiate(resident.Model, npc.transform, false);
            }

            npc.Bind(resident);
            spawned[resident] = npc;
        }
    }
}
