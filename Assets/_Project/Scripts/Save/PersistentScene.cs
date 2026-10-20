using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Makes a scene remember what happens in it. With one in the scene, every SaveableEntity keeps its state when the player
/// leaves (for a raid, say) and comes back, and, if writeToSaveFile is on, in save files too. Scenes without one, such as
/// raids, start fresh every time. It also recreates objects that were spawned while playing (placed buildings) from their
/// prefab, before the rest of the scene wakes up.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-85)]
public sealed class PersistentScene : MonoBehaviour
{
    [Tooltip("Off: changes are remembered while the game runs but not written to save files.")]
    [SerializeField] private bool writeToSaveFile = true;
    [Tooltip("Recreates spawned objects from their prefabs. Left empty, spawned objects aren't restored.")]
    [SerializeField] private ContentDatabase content;

    private static readonly List<PersistentScene> active = new List<PersistentScene>();

    /// <summary>The state this scene reads and writes. It belongs to the GameSession's world at the time the scene loaded.</summary>
    public SceneState State { get; private set; }

    /// <summary>The persistent scene a GameObject belongs to, or null when its scene doesn't persist.</summary>
    public static PersistentScene For(Scene scene)
    {
        foreach (PersistentScene candidate in active)
        {
            if (candidate.gameObject.scene == scene)
            {
                return candidate;
            }
        }

        return null;
    }

    private void Awake()
    {
        if (For(gameObject.scene) != null)
        {
            Debug.LogError("Only one PersistentScene per scene.", this);
            enabled = false;
            return;
        }

        active.Add(this);
        State = GameSession.World.GetOrAdd(gameObject.scene.name, writeToSaveFile);
        RecreateSpawned();
    }

    private void OnDestroy()
    {
        active.Remove(this);
    }

    private void RecreateSpawned()
    {
        foreach (EntityState entity in new List<EntityState>(State.Entities.Values))
        {
            if (!entity.Spawned || entity.Destroyed)
            {
                continue;
            }

            SaveableEntity prefab = content != null ? content.FindPrefab(entity.PrefabId) : null;
            if (prefab == null)
            {
                Debug.LogWarning($"Can't recreate saved object '{entity.Id}': no prefab with id '{entity.PrefabId}' in the content database.", this);
                continue;
            }

            SaveableEntity.Spawn(prefab, entity.Id, entity.Position, entity.Rotation, transform);
        }
    }
}
