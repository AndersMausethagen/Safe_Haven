using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// The steps every content prefab builder shares. A generated prefab is an empty root holding the gameplay components
/// with the 3D model as a child named "Model", so the model can be swapped without touching the gameplay setup.
/// Prefabs are built in an isolated preview scene, so the open scene is never touched.
/// </summary>
public static class PrefabBuildUtility
{
    public const string ModelChildName = "Model";
    private const string HealthBarPath = "Assets/_Project/Prefabs/UI/HealthBar.prefab";
    private const string DamageNumberPath = "Assets/_Project/Prefabs/UI/DamageNumber.prefab";
    private const float HealthBarClearance = 0.45f;
    private const float DamageNumberClearance = 0.25f;

    /// <summary>A stand-in shape for content that has no model yet.</summary>
    public readonly struct Placeholder
    {
        public readonly PrimitiveType Shape;
        public readonly float Size;

        public Placeholder(PrimitiveType shape, float size)
        {
            Shape = shape;
            Size = size;
        }
    }

    /// <summary>Throws ArgumentException when the model can't be used: a scene object, or one carrying gameplay parts.</summary>
    public static void ValidateModel(GameObject model, params Type[] forbiddenComponents)
    {
        if (model == null)
        {
            return;
        }

        if (!PrefabUtility.IsPartOfPrefabAsset(model))
        {
            throw new ArgumentException("The model must be a prefab or 3D model from the Project window, not an object in the scene.");
        }

        foreach (Type forbidden in forbiddenComponents)
        {
            if (model.GetComponentInChildren(forbidden, true) != null)
            {
                throw new ArgumentException(
                    $"'{model.name}' already has a {ObjectNames.NicifyVariableName(forbidden.Name)} component. Choose a plain 3D model instead.");
            }
        }
    }

    /// <summary>Builds and saves a new prefab at a unique path in the folder and returns the saved asset.</summary>
    public static GameObject CreatePrefab(string folder, string fileName, GameObject model, Placeholder placeholder, Action<GameObject> configure)
    {
        EnsureFolder(folder);
        string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{MakeFileName(fileName)}.prefab");
        Scene previewScene = EditorSceneManager.NewPreviewScene();

        try
        {
            var root = new GameObject(Path.GetFileNameWithoutExtension(path));
            SceneManager.MoveGameObjectToScene(root, previewScene);
            AddModel(root, model, placeholder);
            configure(root);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
            if (!success || saved == null)
            {
                throw new InvalidOperationException($"Unity could not save the prefab at '{path}'.");
            }

            RegisterSaveable(saved);
            return saved;
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(previewScene);
        }
    }

    /// <summary>
    /// Gives a new prefab with a SaveableEntity its prefab id and lists it in the ContentDatabase right away, so copies
    /// placed while playing (a building, a node) are recreated after loading without waiting for the import hook.
    /// </summary>
    public static void RegisterSaveable(GameObject prefab)
    {
        if (prefab == null || !prefab.TryGetComponent(out SaveableEntity entity) || !entity.RefreshPrefabId())
        {
            return;
        }

        AssetDatabase.SaveAssetIfDirty(prefab);
        foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(ContentDatabase)))
        {
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabase>(AssetDatabase.GUIDToAssetPath(guid));
            if (database != null)
            {
                database.CollectAll();
                AssetDatabase.SaveAssetIfDirty(database);
            }
        }
    }

    /// <summary>Opens a prefab asset in isolation, lets edit change it and saves it.</summary>
    public static void EditPrefab(GameObject prefab, Action<GameObject> edit)
    {
        string path = AssetDatabase.GetAssetPath(prefab);
        if (prefab == null || string.IsNullOrEmpty(path) || !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Choose a prefab asset.");
        }

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            edit(root);
            PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
            if (!success)
            {
                throw new InvalidOperationException($"Unity could not save the prefab at '{path}'.");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>
    /// Swaps the prefab's "Model" child for another model and refits its collider, health bar and navigation size.
    /// </summary>
    public static void ReplaceModel(GameObject prefab, GameObject model, Placeholder placeholder)
    {
        EditPrefab(prefab, root =>
        {
            Transform current = root.transform.Find(ModelChildName);
            if (current == null)
            {
                throw new InvalidOperationException(
                    $"'{prefab.name}' has no child called \"{ModelChildName}\" (it was made by an older tool), so its model can't be swapped. " +
                    "Delete the prefab and create a new one instead.");
            }

            Object.DestroyImmediate(current.gameObject);
            AddModel(root, model, placeholder);
            FitToModel(root);
        });
    }

    /// <summary>True when the prefab keeps its model in a "Model" child that can be swapped.</summary>
    public static bool HasModelChild(GameObject prefab)
    {
        return prefab != null && prefab.transform.Find(ModelChildName) != null;
    }

    private static void AddModel(GameObject root, GameObject model, Placeholder placeholder)
    {
        GameObject visual;
        if (model != null)
        {
            visual = PrefabUtility.InstantiatePrefab(model, root.scene) as GameObject;
            if (visual == null)
            {
                throw new InvalidOperationException($"Unity could not place the model '{model.name}'.");
            }

            visual.transform.SetParent(root.transform, false);
        }
        else
        {
            visual = GameObject.CreatePrimitive(placeholder.Shape);
            SceneManager.MoveGameObjectToScene(visual, root.scene);
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = Vector3.one * placeholder.Size;
            bool tall = placeholder.Shape == PrimitiveType.Capsule || placeholder.Shape == PrimitiveType.Cylinder;
            visual.transform.localPosition = Vector3.up * (tall ? placeholder.Size : placeholder.Size * 0.5f);
        }

        visual.name = ModelChildName;
        visual.transform.SetAsFirstSibling();
    }

    /// <summary>Resizes the root's collider, navigation shapes, health bar and damage numbers to the current model.</summary>
    public static void FitToModel(GameObject root)
    {
        Bounds bounds = CalculateLocalBounds(root);

        if (root.TryGetComponent(out BoxCollider box))
        {
            FitBox(box, bounds);
        }

        bool hasCapsule = root.TryGetComponent(out CapsuleCollider capsule);
        if (hasCapsule)
        {
            FitCapsule(capsule, bounds);
        }

        if (root.TryGetComponent(out NavMeshAgent agent))
        {
            agent.radius = hasCapsule ? capsule.radius : Mathf.Clamp(Mathf.Min(bounds.size.x, bounds.size.z) * 0.5f, 0.2f, 1.5f);
            agent.height = hasCapsule ? capsule.height : Mathf.Max(0.5f, bounds.size.y);
        }

        if (root.TryGetComponent(out NavMeshObstacle obstacle))
        {
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = bounds.center;
            obstacle.size = bounds.size;
        }

        var healthBar = root.GetComponentInChildren<WorldHealthBar>(true);
        if (healthBar != null)
        {
            SetFloat(healthBar, "height", bounds.max.y + HealthBarClearance);
        }

        if (root.TryGetComponent(out DamageNumberSpawner spawner))
        {
            SetFloat(spawner, "height", bounds.max.y + DamageNumberClearance);
        }
    }

    /// <summary>Bounds of everything visible, relative to the root's pivot, ignoring UI. Falls back to a person-sized box.</summary>
    public static Bounds CalculateLocalBounds(GameObject root)
    {
        bool found = false;
        var bounds = new Bounds();
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.GetComponentInParent<Canvas>(true) != null)
            {
                continue;
            }

            Bounds world = renderer.bounds;
            Vector3 size = root.transform.InverseTransformVector(world.size);
            var local = new Bounds(root.transform.InverseTransformPoint(world.center),
                new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z)));
            if (found)
            {
                bounds.Encapsulate(local);
            }
            else
            {
                bounds = local;
                found = true;
            }
        }

        return found ? bounds : new Bounds(Vector3.up, new Vector3(1f, 2f, 1f));
    }

    public static bool HasSolidCollider(GameObject root)
    {
        foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
        {
            if (collider.enabled && !collider.isTrigger)
            {
                return true;
            }
        }

        return false;
    }

    public static void EnsureBoxCollider(GameObject root, Bounds bounds)
    {
        if (!HasSolidCollider(root))
        {
            FitBox(root.AddComponent<BoxCollider>(), bounds);
        }
    }

    public static void EnsureCapsuleCollider(GameObject root, Bounds bounds)
    {
        if (!HasSolidCollider(root))
        {
            FitCapsule(root.AddComponent<CapsuleCollider>(), bounds);
        }
    }

    /// <summary>Adds the floating health bar if the prefab has none.</summary>
    public static void EnsureHealthBar(GameObject root, Bounds bounds)
    {
        if (root.GetComponentInChildren<WorldHealthBar>(true) != null)
        {
            return;
        }

        var healthBarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HealthBarPath);
        if (healthBarPrefab == null)
        {
            Debug.LogWarning($"No health bar prefab at {HealthBarPath}; {root.name} was made without one.");
            return;
        }

        var healthBar = (GameObject)PrefabUtility.InstantiatePrefab(healthBarPrefab, root.scene);
        healthBar.transform.SetParent(root.transform, false);
        SetFloat(healthBar.GetComponent<WorldHealthBar>(), "height", bounds.max.y + HealthBarClearance);
    }

    /// <summary>Adds floating damage numbers if missing, and fills in the number prefab if it is empty.</summary>
    public static void EnsureDamageNumbers(GameObject root, Bounds bounds)
    {
        DamageNumberSpawner spawner = GetOrAdd<DamageNumberSpawner>(root, out bool added);
        var data = new SerializedObject(spawner);
        SerializedProperty prefab = data.FindProperty("prefab");
        if (prefab.objectReferenceValue == null)
        {
            var damageNumber = AssetDatabase.LoadAssetAtPath<GameObject>(DamageNumberPath);
            prefab.objectReferenceValue = damageNumber != null ? damageNumber.GetComponent<DamageNumber>() : null;
        }

        if (added)
        {
            data.FindProperty("height").floatValue = bounds.max.y + DamageNumberClearance;
        }

        data.ApplyModifiedPropertiesWithoutUndo();
    }

    public static T GetOrAdd<T>(GameObject root, out bool added) where T : Component
    {
        T component = root.GetComponent<T>();
        added = component == null;
        return added ? root.AddComponent<T>() : component;
    }

    public static T GetOrAdd<T>(GameObject root) where T : Component
    {
        return GetOrAdd<T>(root, out _);
    }

    /// <summary>Sets serialized fields on a component without going through its (often read-only) C# properties.</summary>
    public static void Configure(Object target, Action<SerializedObject> apply)
    {
        var data = new SerializedObject(target);
        apply(data);
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void AddIfMissing<T>(GameObject prefab, List<string> problems) where T : Component
    {
        if (prefab.GetComponent<T>() == null)
        {
            problems.Add("Missing " + ObjectNames.NicifyVariableName(typeof(T).Name));
        }
    }

    /// <summary>A file-system-safe name with the spaces removed: "Grey Stone" becomes "GreyStone".</summary>
    public static string MakeFileName(string value)
    {
        var builder = new StringBuilder();
        foreach (char character in value ?? "")
        {
            if (!char.IsWhiteSpace(character) && Array.IndexOf(Path.GetInvalidFileNameChars(), character) < 0)
            {
                builder.Append(character);
            }
        }

        return builder.Length > 0 ? builder.ToString() : "New";
    }

    /// <summary>Lowercase letters and digits with underscores between words: "Grey Stone" becomes "grey_stone".</summary>
    public static string MakeId(string value)
    {
        var builder = new StringBuilder();
        foreach (char character in (value ?? "").Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
            }
            else if (builder.Length > 0 && builder[builder.Length - 1] != '_')
            {
                builder.Append('_');
            }
        }

        return builder.ToString().Trim('_');
    }

    public static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }

    private static void FitBox(BoxCollider box, Bounds bounds)
    {
        box.center = bounds.center;
        box.size = Vector3.Max(bounds.size, Vector3.one * 0.2f);
    }

    private static void FitCapsule(CapsuleCollider capsule, Bounds bounds)
    {
        capsule.direction = 1;
        capsule.height = Mathf.Max(0.5f, bounds.size.y);
        capsule.radius = Mathf.Clamp(Mathf.Min(bounds.size.x, bounds.size.z) * 0.5f, 0.2f, capsule.height * 0.5f);
        capsule.center = new Vector3(0f, bounds.center.y, 0f);
    }

    private static void SetFloat(Object target, string property, float value)
    {
        Configure(target, data => data.FindProperty(property).floatValue = value);
    }
}
