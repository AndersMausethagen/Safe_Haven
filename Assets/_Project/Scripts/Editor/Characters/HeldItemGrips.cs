using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Held weapons and tools (Safe Haven > Characters > Held Item Grips): fits the player's hand grip, gives weapons and
/// tools without a held model a stand-in, and lists every weapon and tool with its held model and grip offset. In Play
/// mode, picking an item puts it in the player's hand and the offsets update it live.
/// <para>The grip is a fist on the player's own right hand (see <see cref="ComputeGrip"/>), so models made handle-up
/// with the grip at their origin sit right without their own offset; tune the rest per item here.</para>
/// </summary>
public sealed class HeldItemGrips : EditorWindow
{
    public const string PlayerPrefab = "Assets/_Project/Prefabs/Characters/PlayerArmature.prefab";
    private const string Crafting = "Assets/Kevin Iglesias/Human Animations/Unity Demo Scenes/Human Crafting Animations/Prefabs/Tools/";
    private const string Melee = "Assets/Kevin Iglesias/Human Animations/Unity Demo Scenes/Human Melee Animations/Prefabs/Weapons/";

    private List<ItemDefinition> items;
    private ItemDefinition selected;
    private Vector2 scroll;

    [MenuItem("Safe Haven/Characters/Held Item Grips")]
    public static void Open() => GetWindow<HeldItemGrips>("Held Item Grips");

    private void OnEnable() => items = null;

    private void OnProjectChange() => items = null;

    private void OnGUI()
    {
        items ??= AssetDatabase.FindAssets("t:" + nameof(ItemDefinition))
            .Select(g => AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(i => i != null && (i.IsWeapon || i.ToolType != ToolType.None)).OrderBy(i => i.IsWeapon ? 0 : 1).ThenBy(i => i.name).ToList();

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(new GUIContent("Fit Player Grip", "Fit the grip to the player's right hand (handle across the palm, edge forward).")))
            {
                Debug.Log(FitPlayerGrip());
            }

            if (GUILayout.Button(new GUIContent("Assign Stand-in Models", "Weapons and tools without a held model get the closest demo model.")))
            {
                Debug.Log($"Held items: {AssignStandIns()} item(s) got a stand-in model.");
            }
        }

        HeldItemView view = Application.isPlaying ? FindFirstObjectByType<HeldItemView>() : null;
        EditorGUILayout.HelpBox(Application.isPlaying
            ? "Pick an item to hold it; change its grip and see it in the hand. Changes are saved to the item."
            : "Enter Play mode to see an item in the player's hand while you tune it.", MessageType.None);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (ItemDefinition item in items)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                bool isSelected = item == selected;
                string kind = item.IsWeapon ? "weapon" : item.ToolType.ToString().ToLowerInvariant();
                if (GUILayout.Toggle(isSelected, $"{item.DisplayName}  ({kind})", EditorStyles.miniButtonLeft, GUILayout.Width(220)) && !isSelected)
                {
                    selected = item;
                    if (view != null)
                    {
                        view.Preview = item;
                    }
                }

                EditorGUILayout.LabelField(item.HeldModel != null ? item.HeldModel.name : "no model", EditorStyles.miniLabel);
            }

            if (item == selected)
            {
                DrawItem(item, view);
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private static void DrawItem(ItemDefinition item, HeldItemView view)
    {
        var serialized = new SerializedObject(item);
        EditorGUI.indentLevel++;
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(serialized.FindProperty("heldModel"));
        EditorGUILayout.PropertyField(serialized.FindProperty("heldPosition"));
        EditorGUILayout.PropertyField(serialized.FindProperty("heldRotation"));
        EditorGUILayout.PropertyField(serialized.FindProperty("heldScale"));
        bool changed = EditorGUI.EndChangeCheck();
        serialized.ApplyModifiedProperties();
        EditorGUI.indentLevel--;
        if (changed && view != null)
        {
            view.Refresh(); // the model or its grip changed: make it again
        }
    }

    private void OnDisable()
    {
        // Closing the window hands the hand back to the equipped weapon.
        HeldItemView view = Application.isPlaying ? FindFirstObjectByType<HeldItemView>() : null;
        if (view != null)
        {
            view.Preview = null;
        }
    }

    /// <summary>Fits the PlayerArmature prefab's grip to its own hand (adds the Held Item View if missing). Returns a
    /// summary.</summary>
    public static string FitPlayerGrip(string playerPath = PlayerPrefab)
    {
        var playerAsset = AssetDatabase.LoadAssetAtPath<GameObject>(playerPath);
        if (playerAsset == null)
        {
            return "Held items: the player prefab is missing.";
        }

        Pose grip = ComputeGrip(playerAsset);
        GameObject contents = PrefabUtility.LoadPrefabContents(playerPath);
        try
        {
            HeldItemView view = contents.GetComponent<HeldItemView>();
            if (view == null)
            {
                view = contents.AddComponent<HeldItemView>();
            }

            view.GripPose = grip;
            PrefabUtility.SaveAsPrefabAsset(contents, playerPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }

        return $"Held items: player grip set to {grip.position:F3} / {grip.rotation.eulerAngles:F1} (relative to the right hand).";
    }

    /// <summary>A fist grip on a humanoid's right hand: the held model's up (+Y, the handle towards the head or blade)
    /// runs across the palm from the little finger past the index finger, its +X (the edge, the axe head) faces the way
    /// the knuckles point, and the grip point sits in the palm. Relative to the hand bone, so it holds in every animation.
    /// Models made handle-up with the grip at their origin (the Kevin Iglesias props, most tool kits) sit right.</summary>
    public static Pose ComputeGrip(GameObject character)
    {
        UnityEngine.SceneManagement.Scene scene = EditorSceneManager.NewPreviewScene();
        try
        {
            GameObject instance = Object.Instantiate(character);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(instance, scene);
            Animator animator = instance.GetComponentInChildren<Animator>();
            Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Transform index = animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
            Transform little = animator.GetBoneTransform(HumanBodyBones.RightLittleProximal);
            Transform middle = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
            Transform thumb = animator.GetBoneTransform(HumanBodyBones.RightThumbProximal);
            if (index == null || little == null || middle == null)
            {
                return new Pose(Vector3.zero, Quaternion.identity); // no finger bones: hold at the hand bone
            }

            Vector3 handle = (index.position - little.position).normalized;
            Vector3 knuckles = middle.position - hand.position;
            Vector3 edge = Vector3.ProjectOnPlane(knuckles, handle).normalized;
            Vector3 palm = Vector3.Cross(handle, edge);
            if (thumb != null && Vector3.Dot(thumb.position - hand.position, palm) < 0f)
            {
                palm = -palm; // the palm is on the thumb's side
            }

            Vector3 point = hand.position + knuckles * 0.55f + palm * 0.025f;
            Quaternion rotation = Quaternion.LookRotation(Vector3.Cross(edge, handle), handle);
            return new Pose(hand.InverseTransformPoint(point), Quaternion.Inverse(hand.rotation) * rotation);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    /// <summary>Stand-in held models from the demo packs for weapons and tools that have none (undoable).</summary>
    public static int AssignStandIns()
    {
        int assigned = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(ItemDefinition)))
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (item == null || item.HeldModel != null || (!item.IsWeapon && item.ToolType == ToolType.None))
            {
                continue;
            }

            (string path, float scale) = StandIn(item);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model == null)
            {
                continue;
            }

            var serialized = new SerializedObject(item);
            serialized.FindProperty("heldModel").objectReferenceValue = model;
            serialized.FindProperty("heldScale").floatValue = scale;
            serialized.ApplyModifiedProperties();
            assigned++;
        }

        AssetDatabase.SaveAssets();
        return assigned;
    }

    private static (string Path, float Scale) StandIn(ItemDefinition item)
    {
        switch (item.ToolType)
        {
            case ToolType.Axe: return (Crafting + "Human_Axe.prefab", 0.85f);
            case ToolType.Pickaxe: return (Crafting + "Human_Pickaxe.prefab", 0.85f);
            case ToolType.Sickle: return (Crafting + "Human_Knife.prefab", 1.2f);
        }

        string name = item.name.ToLowerInvariant();
        if (name.Contains("spear") || name.Contains("polearm")) return (Melee + "Human_Polearm.prefab", 1f);
        if (name.Contains("greataxe") || name.Contains("hammer")) return (Melee + "Human_Warhammer.prefab", 1f);
        if (name.Contains("axe")) return (Crafting + "Human_Axe.prefab", 1f);
        if (name.Contains("greatsword")) return (Melee + "Human_Greatsword.prefab", 1f);
        if (name.Contains("dagger") || name.Contains("knife")) return (Melee + "Human_Dagger.prefab", 1f);
        return (Melee + "Human_Sword.prefab", 1f);
    }
}
