using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Remembers which painted terrain trees are felled (TerrainTreeFelling) in the game's save, so a cleared stretch of the Haven's woods
/// stays cleared after a raid or a reload, until each tree grows back. On the terrain beside a SaveableEntity; the Haven world generator
/// adds both. Felled trees are kept by their index on the terrain, so regenerating the woods starts them all standing again.
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

        var state = new State { treeCount = terrain.terrainData.treeInstanceCount };
        foreach ((int index, float secondsLeft) in felling.FelledNow())
        {
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
        if (state == null || state.treeCount != terrain.terrainData.treeInstanceCount)
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
