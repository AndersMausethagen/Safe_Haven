using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Drives base building in Play mode in Safe Haven (start with ProbeArchery.PlayIn): `ProbeBuilding.Place` puts up a
/// Sawbench scaffold in front of the player; `Hammer` with seconds starts the work for that long; `Finish` ends it;
/// `Upgrade` pays and finishes the next level; `Clear` with a count clears that much overgrowth; `State` reports.
/// </summary>
public static class ProbeBuilding
{
    private static Construction Last => Object.FindObjectsByType<Construction>().FirstOrDefault(c => c.name.StartsWith("Sawbench") && c.name != "Ruined Storage Shed");

    public static string Place()
    {
        var building = AssetDatabase.LoadAssetAtPath<BuildingDefinition>("Assets/_Project/Data/Buildings/Sawbench.asset");
        Transform player = Player.Current.transform;
        Vector3 at = player.position + player.forward * 4f;
        GameObject placed = SaveableEntity.Spawn(building.Prefab.GetComponent<SaveableEntity>(), at, Quaternion.LookRotation(-player.forward)).gameObject;
        placed.GetComponent<Construction>().BeginBuilding();
        return State();
    }

    public static string Hammer(float seconds)
    {
        Construction site = Last;
        Interactable work = site.GetComponentsInChildren<Interactable>(true).First(i => i.GetComponent<ConstructionSite>() != null);
        work.TryBegin(Player.Current.gameObject, out string reason);
        return $"hammering: {reason ?? "ok"}; " + State();
    }

    public static string Finish()
    {
        Construction site = Last;
        Interactable work = site.GetComponentsInChildren<Interactable>(true).FirstOrDefault(i => i.GetComponent<ConstructionSite>() != null && i.IsInUse);
        work?.End(InteractionResult.Cancelled);
        site.Finish(Player.Current.gameObject);
        return State();
    }

    public static string Upgrade()
    {
        Construction site = Last;
        BuildingLevel next = site.Definition.UpgradeTo(site.Level + 1);
        Player.Current.Bag.Give(next.cost);
        bool ordered = site.TryOrderUpgrade(Player.Current.gameObject);
        return $"ordered {ordered}; " + State();
    }

    public static string Clear(int count)
    {
        int cleared = 0;
        foreach (Overgrowth piece in Overgrowth.All.ToArray())
        {
            if (cleared >= count) break;
            if (piece.IsCleared) continue;
            piece.GetComponent<Interactable>().Completed.Invoke();
            cleared++;
        }

        return $"cleared {cleared}; " + State();
    }

    public static string Look(float x, float z)
    {
        Transform player = Player.Current.transform;
        var body = player.GetComponent<CharacterController>();
        body.enabled = false;
        var at = new Vector3(x, 0f, z);
        at.y = Terrain.activeTerrain.SampleHeight(at) + Terrain.activeTerrain.transform.position.y + 0.1f;
        player.position = at;
        body.enabled = true;
        return State();
    }

    public static string State()
    {
        Construction site = Last;
        string building = site == null ? "no sawbench" : $"{site.Title} L{site.Level}→{site.Target} work {site.Work:0.0}/{site.WorkNeeded:0} paid {site.Paid}";
        return $"{building}; renown {HavenRenown.Compute()} rank {GameSession.Settlement.HavenRank} ward {HeartTree.Current?.WardRadius:0}; overgrowth {Overgrowth.ClearedShare * 100f:0}% of {Overgrowth.All.Count}; level {GameSession.Progress.Level}";
    }
}
