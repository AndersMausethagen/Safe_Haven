using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Marks an object whose state is saved: an enemy that stays dead, a building that keeps its damage, a gathering node that
/// stays picked. Add it to an object (or its prefab) in a scene with a PersistentScene, and choose below what is kept.
/// Every ISaveable component on the same object is saved with it. Scene objects get a stable id in the editor; objects
/// created while playing (placed buildings) get one when spawned through <see cref="Spawn"/> and are recreated from their
/// prefab. The state lives in memory in GameSession while playing and only reaches disk when the game is saved.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-80)]
public sealed class SaveableEntity : MonoBehaviour
{
    [Tooltip("Identifies this object in saves. Assigned automatically; don't copy it between objects.")]
    [SerializeField] private string id;
    [Tooltip("Identifies the prefab this object is made from, so spawned copies can be recreated. Set automatically on prefabs.")]
    [SerializeField] private string prefabId;

    [Header("What Is Saved")]
    [Tooltip("Position and facing, for things that move (monsters) or are placed by the player.")]
    [SerializeField] private bool saveTransform = true;
    [Tooltip("Whether the object is switched on, e.g. a pickup that was taken.")]
    [SerializeField] private bool saveActiveState = true;
    [Tooltip("When it dies (its Damageable destroys it), it stays gone. Turn off for things that should come back.")]
    [SerializeField] private bool rememberDestruction = true;
    [Tooltip("The state of every ISaveable component here: health, regrowth timers and the like.")]
    [SerializeField] private bool saveComponents = true;

    private static readonly List<SaveableEntity> live = new List<SaveableEntity>();
    private static string pendingSpawnId;

    private ISaveable[] saveables = Array.Empty<ISaveable>();
    private PersistentScene owner;
    private SceneState state;
    private Damageable damageable;
    private bool spawned;
    private bool destroyed;
    private bool started;

    /// <summary>Every saveable object currently loaded, active or not.</summary>
    public static IReadOnlyList<SaveableEntity> Live => live;

    public string Id => id;
    public string PrefabId => prefabId;
    public bool IsPersistent => state != null;

    /// <summary>Creates a saveable object while playing so it is saved and recreated later, such as a building the player places.</summary>
    public static SaveableEntity Spawn(SaveableEntity prefab, Vector3 position, Quaternion rotation)
    {
        return Spawn(prefab, null, position, rotation, null);
    }

    /// <param name="sceneAnchor">An object in the scene the copy belongs to; the copy starts as its child so it wakes up in that scene.</param>
    internal static SaveableEntity Spawn(SaveableEntity prefab, string existingId, Vector3 position, Quaternion rotation, Transform sceneAnchor)
    {
        if (string.IsNullOrEmpty(prefab.prefabId))
        {
            Debug.LogWarning($"{prefab.name} has no prefab id, so the spawned copy can't be recreated after loading.", prefab);
        }

        // Awake runs inside Instantiate, so the id is handed over through a static that Awake picks up.
        pendingSpawnId = existingId ?? "spawned-" + Guid.NewGuid().ToString("N");
        try
        {
            SaveableEntity copy = sceneAnchor != null ? Instantiate(prefab, position, rotation, sceneAnchor) : Instantiate(prefab, position, rotation);
            if (sceneAnchor != null)
            {
                copy.transform.SetParent(null, true);
            }

            return copy;
        }
        finally
        {
            pendingSpawnId = null;
        }
    }

    /// <summary>Records that the object is gone for good, for removals other than dying (picked up, dismantled).</summary>
    public void MarkDestroyed()
    {
        destroyed = true;
        Capture();
    }

    /// <summary>Writes the current state of every loaded saveable object into memory, before saving.</summary>
    public static void CaptureAll()
    {
        foreach (SaveableEntity entity in live)
        {
            entity.Capture();
        }
    }

    private void Awake()
    {
        if (pendingSpawnId != null)
        {
            id = pendingSpawnId;
            spawned = true;
        }

        live.Add(this);
        saveables = GetComponents<ISaveable>();
        damageable = GetComponent<Damageable>();
        owner = PersistentScene.For(gameObject.scene);
        if (owner == null || owner.State == null || string.IsNullOrEmpty(id))
        {
            if (owner != null && string.IsNullOrEmpty(id))
            {
                Debug.LogWarning($"{name} has no save id, so its state isn't saved. Use Safe Haven > Save > Assign Missing Save Ids.", this);
            }

            return;
        }

        state = owner.State;
        if (!state.Entities.TryGetValue(id, out EntityState saved))
        {
            return;
        }

        if (saved.Destroyed)
        {
            // Switched off first so nothing else on the object wakes up before it goes.
            destroyed = true;
            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }

        if (saveTransform && saved.HasTransform)
        {
            MoveTo(saved.Position, saved.Rotation);
        }

        if (saveActiveState && !saved.Active)
        {
            gameObject.SetActive(false);
        }
    }

    private void OnEnable()
    {
        if (damageable != null)
        {
            damageable.Died += OnDied;
        }
    }

    // Component state is applied after every Awake, so it isn't overwritten by components setting their defaults.
    private void Start()
    {
        started = true;
        if (state == null || destroyed || !saveComponents || !state.Entities.TryGetValue(id, out EntityState saved))
        {
            return;
        }

        foreach (ISaveable saveable in saveables)
        {
            if (saved.Components.TryGetValue(saveable.SaveKey, out string component))
            {
                try
                {
                    saveable.RestoreState(component);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }
        }
    }

    // Switching the object off during play records its state. Scene changes record everything up front (SceneLoader),
    // because Unity switches root objects off while unloading, which would read as "inactive".
    private void OnDisable()
    {
        if (damageable != null)
        {
            damageable.Died -= OnDied;
        }

        if (!SceneLoader.IsLeaving && gameObject.scene.isLoaded)
        {
            Capture();
        }
    }

    private void OnDestroy()
    {
        live.Remove(this);
    }

    private void OnDied(Damageable victim, DamageInfo killingBlow)
    {
        if (rememberDestruction && victim.DestroysOnDeath)
        {
            MarkDestroyed();
        }
    }

    private void Capture()
    {
        // A loaded save replaces the world; objects from before it must not write into the new one.
        if (state == null || !GameSession.World.Scenes.TryGetValue(state.Scene, out SceneState current) || current != state)
        {
            return;
        }

        EntityState entity = state.GetOrAdd(id);
        entity.PrefabId = prefabId;
        entity.Spawned = spawned;
        entity.Destroyed = destroyed;
        if (destroyed)
        {
            entity.Components.Clear();
            return;
        }

        if (saveActiveState)
        {
            entity.Active = gameObject.activeSelf;
        }

        if (saveTransform || spawned)
        {
            entity.HasTransform = true;
            entity.Position = transform.position;
            entity.Rotation = transform.rotation;
        }

        // Before Start the components still hold their defaults, not the remembered state, so they are left alone.
        if (saveComponents && started)
        {
            foreach (ISaveable saveable in saveables)
            {
                try
                {
                    entity.Components[saveable.SaveKey] = saveable.CaptureState();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }
        }
    }

    private void MoveTo(Vector3 position, Quaternion rotation)
    {
        if (TryGetComponent(out NavMeshAgent agent) && agent.enabled && agent.isOnNavMesh)
        {
            agent.Warp(position);
        }
        else
        {
            transform.position = position;
        }

        transform.rotation = rotation;
    }

#if UNITY_EDITOR
    /// <summary>Editor only: gives the object a new id. Used when ids are missing or copied from another object.</summary>
    public void AssignNewId()
    {
        id = Guid.NewGuid().ToString("N");
        UnityEditor.EditorUtility.SetDirty(this);
        // On a prefab instance the id must be stored as an override, or saving the scene drops it.
        if (UnityEditor.PrefabUtility.IsPartOfPrefabInstance(this))
        {
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);
        }
    }

    /// <summary>Editor only: on a prefab asset, points prefabId at the prefab and clears the scene id.</summary>
    public bool RefreshPrefabId()
    {
        string path = UnityEditor.AssetDatabase.GetAssetPath(gameObject);
        string guid = string.IsNullOrEmpty(path) ? null : UnityEditor.AssetDatabase.AssetPathToGUID(path);
        if (guid == null || (prefabId == guid && string.IsNullOrEmpty(id)))
        {
            return false;
        }

        prefabId = guid;
        id = "";
        UnityEditor.EditorUtility.SetDirty(this);
        return true;
    }

    // Scene copies get their own id; duplicating an object in the editor copies the id, so duplicates get a fresh one.
    private void OnValidate()
    {
        if (Application.isPlaying || UnityEditor.PrefabUtility.IsPartOfPrefabAsset(this)
            || UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(gameObject) != null || !gameObject.scene.IsValid())
        {
            return;
        }

        if (string.IsNullOrEmpty(id) || IsDuplicate())
        {
            AssignNewId();
        }
    }

    private bool IsDuplicate()
    {
        foreach (SaveableEntity other in FindObjectsByType<SaveableEntity>(FindObjectsInactive.Include))
        {
            if (other != this && other.id == id && other.gameObject.scene == gameObject.scene)
            {
                return true;
            }
        }

        return false;
    }
#endif
}
