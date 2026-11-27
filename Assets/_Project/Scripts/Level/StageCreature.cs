using UnityEngine;

/// <summary>
/// A creature a place's stage stands (HavenPlaceVerbs.Stage › Creature: the Old Grey at the Wolf Den). Goes when the stage goes, but
/// only while it lives: a body killed stays, with its loot, for the player.
/// </summary>
[DisallowMultipleComponent]
public sealed class StageCreature : MonoBehaviour
{
    private GameObject creature;

    /// <summary>The creature stood (tests and probes).</summary>
    public GameObject Creature => creature;

    public void Keep(GameObject made) => creature = made;

    private void OnDestroy()
    {
        Damageable body = creature != null ? creature.GetComponentInChildren<Damageable>() : null;
        if (creature != null && (body == null || body.IsAlive))
        {
            Destroy(creature);
        }
    }
}
