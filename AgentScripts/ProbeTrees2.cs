using System.Diagnostics;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Choppable trees in a playing scene: how many, whether the player may chop the nearest one, what felling it
/// gives, and what the many interactables cost the outline's per-frame distance pass
/// (`unity command run_script --file AgentScripts/ProbeTrees2.cs --entry ProbeTrees2.Report`).</summary>
public static class ProbeTrees2
{
    public static string Report()
    {
        Player player = Object.FindAnyObjectByType<Player>();
        ChoppableTree[] trees = Object.FindObjectsByType<ChoppableTree>();
        int active = Interactable.Active.Count;
        var watch = Stopwatch.StartNew();
        float sum = 0f;
        foreach (Interactable target in Interactable.Active)
        {
            sum += target.DistanceFrom(player.transform.position);
        }

        watch.Stop();
        ChoppableTree nearest = trees.OrderBy(t => (t.transform.position - player.transform.position).sqrMagnitude).FirstOrDefault();
        string can = "-";
        string felled = "-";
        if (nearest != null)
        {
            bool allowed = nearest.GetComponent<ResourceNode>().CanInteract(player.gameObject, out string reason);
            can = allowed ? "yes" : reason;
            var bag = player.GetComponent<ItemInventory>();
            var hatchet = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/StoneHatchet.asset");
            if (!allowed)
            {
                bag.Give(hatchet, 1);
                allowed = nearest.GetComponent<ResourceNode>().CanInteract(player.gameObject, out reason);
                can += $" -> with a Stone Hatchet: {(allowed ? "yes" : reason)}";
            }

            var logs = new System.Collections.Generic.List<ItemAmount>();
            ChoppableTree.Roll(ItemInventory.GatheringTier(player.gameObject, ToolType.Axe), nearest.Size, () => Random.value, logs);
            felled = string.Join(", ", logs.Select(l => $"{l.Amount} {l.Item.DisplayName}"));
        }

        return $"choppable trees {trees.Length}, interactables {active}, outline distance pass {watch.Elapsed.TotalMilliseconds:F2} ms; " +
               $"nearest {nearest?.name} size {nearest?.Size:F2}: can chop {can}; felling gives {felled}";
    }
}
