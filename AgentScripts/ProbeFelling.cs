using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Fells the nearest terrain tree in a playing raid as the player would (`unity command run_script --file
/// AgentScripts/ProbeFelling.cs --entry ProbeFelling.Fell`): proxies near the player, the tree's scale before and after,
/// and the logs that reach the bag.</summary>
public static class ProbeFelling
{
    /// <summary>Stands the player 2 m from the nearest choppable terrain tree.</summary>
    public static string GoToTree()
    {
        Player player = Object.FindAnyObjectByType<Player>();
        Terrain terrain = Object.FindAnyObjectByType<TerrainTreeFelling>().GetComponent<Terrain>();
        TerrainData data = terrain.terrainData;
        Vector3 best = Vector3.zero;
        float bestDistance = float.MaxValue;
        foreach (TreeInstance tree in data.treeInstances)
        {
            GameObject prefab = data.treePrototypes[tree.prototypeIndex].prefab;
            if (prefab == null || prefab.GetComponent<ChoppableTree>() == null || tree.heightScale <= 0f)
            {
                continue;
            }

            Vector3 at = terrain.GetPosition() + Vector3.Scale(tree.position, data.size);
            float distance = (at - player.transform.position).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = at;
            }
        }

        var controller = player.GetComponent<CharacterController>();
        controller.enabled = false;
        player.transform.position = best + Vector3.right * 2f + Vector3.up * 0.5f;
        controller.enabled = true;
        return $"moved to the tree at {best:F0}";
    }

    public static string Fell()
    {
        Player player = Object.FindAnyObjectByType<Player>();
        var bag = player.GetComponent<ItemInventory>();
        var hatchet = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/StoneHatchet.asset");
        if (ItemInventory.GatheringTier(player.gameObject, ToolType.Axe) < 1)
        {
            bag.Give(hatchet, 1);
        }

        TerrainTreeFelling felling = Object.FindAnyObjectByType<TerrainTreeFelling>();
        if (felling == null)
        {
            return "no TerrainTreeFelling on any terrain";
        }

        ChoppableTree[] proxies = felling.GetComponentsInChildren<ChoppableTree>();
        ChoppableTree nearest = proxies.OrderBy(p => (p.transform.position - player.transform.position).sqrMagnitude).FirstOrDefault();
        if (nearest == null)
        {
            return $"no stand-ins near the player at {player.transform.position:F0}";
        }

        int index = int.Parse(nearest.name.Substring("Tree ".Length));
        TerrainData data = felling.GetComponent<Terrain>().terrainData;
        float before = data.GetTreeInstance(index).heightScale;
        string[] woods = { "OakWood", "Hardwood", "Elderwood" };
        int[] had = woods.Select(w => bag.Container.Count(AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Materials/" + w + ".asset"))).ToArray();
        var interactable = nearest.GetComponent<Interactable>();
        bool began = interactable.TryBegin(player.gameObject, out string why);
        interactable.Completed.Invoke();
        data = felling.GetComponent<Terrain>().terrainData;
        float after = data.GetTreeInstance(index).heightScale;
        int[] now = woods.Select(w => bag.Container.Count(AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Materials/" + w + ".asset"))).ToArray();
        string gained = string.Join(", ", woods.Select((w, i) => $"{w} +{now[i] - had[i]}"));
        return $"stand-ins {proxies.Length}; felled tree {index} (size {nearest.Size:F2}, began={began} {why}): scale {before:F2} -> {after:F2}, " +
               $"data '{data.name}', felled count {felling.FelledCount}; {gained}";
    }
}
