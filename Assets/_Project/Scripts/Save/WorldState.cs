using System.Collections.Generic;
using UnityEngine;

/// <summary>What is remembered about one saveable object in memory: whether it is gone, where it is, and its components' state.</summary>
public sealed class EntityState
{
    public EntityState(string id)
    {
        Id = id;
    }

    public string Id { get; }
    /// <summary>For objects created while playing (a placed building): the prefab to recreate them from.</summary>
    public string PrefabId { get; set; }
    public bool Spawned { get; set; }
    public bool Destroyed { get; set; }
    public bool Active { get; set; } = true;
    public bool HasTransform { get; set; }
    public Vector3 Position { get; set; }
    public Quaternion Rotation { get; set; } = Quaternion.identity;
    /// <summary>Each ISaveable's state, by its SaveKey.</summary>
    public Dictionary<string, string> Components { get; } = new Dictionary<string, string>();
}

/// <summary>The remembered state of one persistent scene: every saveable object in it, by id.</summary>
public sealed class SceneState
{
    public SceneState(string scene, bool writeToDisk)
    {
        Scene = scene;
        WriteToDisk = writeToDisk;
    }

    public string Scene { get; }
    /// <summary>False for scenes that remember changes during a play session but leave them out of save files.</summary>
    public bool WriteToDisk { get; set; }
    public Dictionary<string, EntityState> Entities { get; } = new Dictionary<string, EntityState>();

    public EntityState GetOrAdd(string id)
    {
        if (!Entities.TryGetValue(id, out EntityState state))
        {
            state = new EntityState(id);
            Entities.Add(id, state);
        }

        return state;
    }
}

/// <summary>
/// The in-memory state of the world: one SceneState per persistent scene. It lives in GameSession so it survives scene
/// loads (raids, reloads); SaveMapper copies it into save files and back. Scenes without a PersistentScene component
/// (raids) never appear here, so they start fresh every visit.
/// </summary>
public sealed class WorldState
{
    public Dictionary<string, SceneState> Scenes { get; } = new Dictionary<string, SceneState>();

    public SceneState GetOrAdd(string scene, bool writeToDisk)
    {
        if (!Scenes.TryGetValue(scene, out SceneState state))
        {
            state = new SceneState(scene, writeToDisk);
            Scenes.Add(scene, state);
        }

        state.WriteToDisk = writeToDisk;
        return state;
    }
}
