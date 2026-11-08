using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Remembers which painted terrain trees are felled (TerrainTreeFelling) in the game's save, so a cleared stretch of the Haven's woods
/// stays cleared after a raid or a reload. With Keep Felled (the Haven) a tree that comes down never grows back by itself:
/// saplings planted there (PlantedTrees, which keeps its own trees) are how the woods return. On the terrain beside a
/// SaveableEntity; the Haven world generator adds both. Felled trees are kept by their index among the trees the terrain was
/// made with, so regenerating the woods starts them all standing again.
/// </summary>
[RequireComponent(typeof(Terrain))]
public sealed class TerrainTreeMemory : MonoBehaviour, ISaveable, ISharedState
{
    [Serializable]
    private sealed class State
    {
        public int treeCount;
        public List<int> indices = new List<int>();
        public List<float> secondsLeft = new List<float>();
    }

    private const float Forever = -1f;

    [Tooltip("A tree that comes down stays down (the Haven): only planted saplings grow back.")]
    [SerializeField] private bool keepFelled = true;

    public bool KeepsFelled => keepFelled;

    // The trees the terrain was made with; planted ones come after them and are PlantedTrees' to keep.
    private int MadeWith(Terrain terrain) =>
        TryGetComponent(out PlantedTrees planted) ? planted.BaseCount : terrain.terrainData.treeInstanceCount;

    public string SaveKey => "felledTrees";

    bool ISharedState.IsShared => true;

    int ISharedState.SharedMark => TryGetComponent(out TerrainTreeFelling felling) ? felling.FelledMark : 0;

    public string CaptureState()
    {
        Terrain terrain = GetComponent<Terrain>();
        if (!terrain.TryGetComponent(out TerrainTreeFelling felling) || felling.FelledCount == 0)
        {
            return null;
        }

        var state = new State { treeCount = MadeWith(terrain) };
        foreach ((int index, float secondsLeft) in felling.FelledNow())
        {
            if (index >= state.treeCount)
            {
                continue;
            }

            state.indices.Add(index);
            state.secondsLeft.Add(float.IsPositiveInfinity(secondsLeft) ? Forever : secondsLeft);
        }

        return JsonUtility.ToJson(state);
    }

    public void RestoreState(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return;
        }

        State state = JsonUtility.FromJson<State>(json);
        Terrain terrain = GetComponent<Terrain>();
        // Saved against other woods (the world was generated again): nothing of it applies.
        if (state == null || state.treeCount != MadeWith(terrain))
        {
            return;
        }

        TerrainTreeFelling felling = TerrainTreeFelling.On(terrain);
        // Restored again in a friend's Haven (co-op), felled trees add up: each game grows its own back on its timers, so a
        // friend's list without a tree felled here a moment ago never stands that tree back up.
        for (int i = 0; i < state.indices.Count && i < state.secondsLeft.Count; i++)
        {
            felling.RestoreFelled(state.indices[i], state.secondsLeft[i] < 0f ? float.PositiveInfinity : state.secondsLeft[i]);
        }
    }
}
