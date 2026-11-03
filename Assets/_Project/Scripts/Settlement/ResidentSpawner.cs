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
    private readonly HashSet<ResidentDefinition> cairns = new HashSet<ResidentDefinition>();
    private const float TrophyRing = 6.5f;
    private readonly HashSet<BossDefinition> trophies = new HashSet<BossDefinition>();
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

    // A cairn near the portal for everyone who didn't come home.
    private void RaiseCairns()
    {
        FallenCairn prefab = SettlementSettings.Current.FallenCairn;
        IReadOnlyList<FallenRecord> fallen = GameSession.Settlement.Fallen;
        Vector3 centre = HomePortal.Current != null ? HomePortal.Current.transform.position : transform.position;
        for (int i = 0; prefab != null && i < fallen.Count; i++)
        {
            if (!cairns.Contains(fallen[i].Resident))
            {
                cairns.Add(fallen[i].Resident);
                Instantiate(prefab, FallenCairn.SpotFor(centre, i), Quaternion.Euler(0f, i * 73f, 0f), transform).Bind(fallen[i]);
            }
        }

        RaiseTrophies(centre);
    }

    // What has been beaten stands near the portal, after the cairns.
    private void RaiseTrophies(Vector3 centre)
    {
        IReadOnlyList<BossDefinition> bosses = SettlementSettings.Current.Bosses;
        for (int i = 0; i < bosses.Count; i++)
        {
            BossDefinition boss = bosses[i];
            if (boss != null && boss.Trophy != null && GameSession.Settlement.IsBossDefeated(boss.Id) && trophies.Add(boss))
            {
                Instantiate(boss.Trophy, FallenCairn.SpotFor(centre, i, TrophyRing, 200f), Quaternion.Euler(0f, i * 53f, 0f), transform).Bind(boss);
            }
        }
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

        RaiseCairns();
    }
}
