using UnityEngine;

/// <summary>On a set-piece standing in a raid: who it is, and bringing out what guards it when the raid begins.</summary>
[DisallowMultipleComponent]
public sealed class RaidSetPieceSite : MonoBehaviour
{
    private const float GuardRing = 0.55f;
    private const float FindGround = 6f;

    public RaidSetPieceDefinition Definition { get; private set; }

    public void Bind(RaidSetPieceDefinition definition) => Definition = definition;

    /// <summary>Stands its creatures round it. The raid's strength applies to them as to any.</summary>
    public void BringGuards(RaidDirector director, System.Random random)
    {
        int index = 0;
        foreach (RaidSetPieceGuard guard in Definition.Guards)
        {
            for (int i = 0; guard.prefab != null && guard.ComesAt(director.Session.Tier) && i < guard.count; i++, index++)
            {
                float angle = (index * 137.5f + (float)random.NextDouble() * 40f) * Mathf.Deg2Rad;
                Vector3 spot = transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (Definition.Radius * GuardRing);
                if (UnityEngine.AI.NavMesh.SamplePosition(spot, out UnityEngine.AI.NavMeshHit hit, FindGround, UnityEngine.AI.NavMesh.AllAreas))
                {
                    director.Creatures.Prepare(Instantiate(guard.prefab, hit.position, Quaternion.Euler(0f, angle * Mathf.Rad2Deg, 0f)), false);
                }
            }
        }
    }
}
