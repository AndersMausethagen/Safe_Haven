using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Makes a tree in the world fellable with an axe. It sits beside the tree's ResourceNode (the "Tree" node: needs an
/// axe, the swing minigame, grows back) and ResourceSource (whose first drop, Wood, is the minigame's bonus log), and
/// grants the felled tree's logs itself: how many and which woods follow TreeChoppingSettings, from the axe's tier and
/// the tree's size (its height, measured once). Painted terrain trees get a stand-in with one near the player
/// (TerrainTreeFelling).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Interactable), typeof(ResourceNode), typeof(ResourceSource))]
public sealed class ChoppableTree : MonoBehaviour
{
    private readonly List<ItemAmount> logs = new List<ItemAmount>();
    private Interactable interactable;
    private ResourceSource source;

    /// <summary>The tree's size for its yield (1 = a tree of the settings' reference height).</summary>
    public float Size { get; private set; } = 1f;

    private void Awake()
    {
        interactable = GetComponent<Interactable>();
        source = GetComponent<ResourceSource>();
        float top = float.MinValue;
        float bottom = float.MaxValue;
        foreach (Renderer part in GetComponentsInChildren<Renderer>(true))
        {
            top = Mathf.Max(top, part.bounds.max.y);
            bottom = Mathf.Min(bottom, part.bounds.min.y);
        }

        Size = TreeChoppingSettings.Current.SizeOf(top > bottom ? top - bottom : 0f);
    }

    /// <summary>Sets the size for a stand-in without a model of its own (a terrain tree's, see TerrainTreeFelling).</summary>
    public void SetSize(float size) => Size = size;

    private void OnEnable() => interactable.Completed.AddListener(OnFelled);

    private void OnDisable() => interactable.Completed.RemoveListener(OnFelled);

    private void OnFelled()
    {
        GameObject user = interactable.Interactor;
        int axe = Mathf.Max(1, ItemInventory.GatheringTier(user, ToolType.Axe));
        Roll(axe, Size, () => Random.value, logs);
        source.GrantItems(user, logs);
    }

    /// <summary>The logs a tree of <paramref name="size"/> gives to an axe of <paramref name="axeTier"/>, merged by wood.</summary>
    public static void Roll(int axeTier, float size, System.Func<float> random, List<ItemAmount> into)
    {
        into.Clear();
        TreeChoppingSettings rules = TreeChoppingSettings.Current;
        var counts = new Dictionary<ItemDefinition, int>();
        int total = rules.Logs(axeTier, size);
        for (int i = 0; i < total; i++)
        {
            ItemDefinition wood = rules.RollLog(axeTier, size, random);
            if (wood != null)
            {
                counts.TryGetValue(wood, out int n);
                counts[wood] = n + 1;
            }
        }

        foreach (var pair in counts)
        {
            into.Add(new ItemAmount(pair.Key, pair.Value));
        }
    }
}
