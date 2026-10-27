using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds the bow and arrows (`unity command run_script --file AgentScripts/BuildArchery.cs --entry BuildArchery.All`):
/// materials, the bow and arrow models (Kevin Iglesias' Human Archer Animations), the arrow projectile, the Bow Shot
/// attack, the Crude Wooden Bow weapon and item, the Crude Stone Arrow definition and item, their hand recipes in the
/// recipe book, icons, the player's archery components and the animator's Bow layer. Re-running updates in place;
/// numbers on assets that already exist are kept.
/// </summary>
public static class BuildArchery
{
    private const string Pack = "Assets/Kevin Iglesias/Human Animations/Unity Demo Scenes/Human Archer Animations/Models/";
    private const string Palette = "Assets/Kevin Iglesias/Human Animations/Unity Demo Scenes/Human Melee Animations/Textures/HumanMeleeAnimationsTexture.png";
    private const string ArtFolder = "Assets/_Project/Art/Items/Archery";
    private const string ModelFolder = "Assets/_Project/Prefabs/Items/Models";
    private const string ProjectileFolder = "Assets/_Project/Prefabs/Effects/Combat";
    private const string ArrowFolder = "Assets/_Project/Data/Combat/Arrows";
    private const string PlayerPrefab = "Assets/_Project/Prefabs/Characters/PlayerArmature.prefab";
    private const string Sounds = "Assets/_Project/Audio/Sounds/Library/";

    public static string All()
    {
        var log = new List<string>();
        Folder(ArtFolder);
        Folder(ArrowFolder);

        Material wood = LitMaterial(ArtFolder + "/Crude Archery.mat");
        Material bowstring = StringMaterial(ArtFolder + "/Bowstring.mat");
        Material targetOutline = OutlineMaterial("Assets/_Project/Art/UI/Materials/TargetOutline.mat");

        GameObject bowModel = Model(Pack + "HumanArcher_Bow.fbx", ModelFolder + "/CrudeWoodenBowModel.prefab", wood, Vector3.zero, Vector3.one * 0.7f);
        // The pack's arrow runs up +Y from its nock; the game's arrows point +Z.
        GameObject arrowModel = Model(Pack + "HumanArcher_Arrow.fbx", ModelFolder + "/CrudeStoneArrowModel.prefab", wood, new Vector3(90f, 0f, 0f), Vector3.one * 0.8f);
        ArrowProjectile projectile = Projectile(arrowModel, ProjectileFolder + "/CrudeStoneArrowProjectile.prefab");

        AttackDefinition shot = Attack("Assets/_Project/Data/Combat/Attacks/BowShot.asset");
        ArrowDefinition arrow = Arrow(ArrowFolder + "/CrudeStoneArrow.asset", projectile);
        WeaponDefinition bow = Bow(CombatAnimationSetup.WeaponFolder + "/CrudeWoodenBow.asset", shot);

        ItemDefinition bowItem = Item("Assets/_Project/Data/Items/CrudeWoodenBow.asset", "crude_wooden_bow", "Crude Wooden Bow",
            "A bent branch strung with nettle cord. Hold attack to draw, let go to shoot; it uses arrows from your bag. " +
            "A full draw flies faster, hits harder and strays less.", ItemCategory.Equipment, false, 1, 2f, bowModel);
        Set(bowItem, so => so.FindProperty("weapon").objectReferenceValue = bow);
        ItemDefinition arrowItem = Item("Assets/_Project/Data/Items/CrudeStoneArrow.asset", "crude_stone_arrow", "Crude Stone Arrow",
            "A straight stick with a chipped stone head. Shot by a bow from your bag; the arrow decides the damage.",
            ItemCategory.Equipment, true, 50, 0.05f, arrowModel);
        Set(arrowItem, so => so.FindProperty("arrow").objectReferenceValue = arrow);

        Icon(bowItem, bowModel, "CrudeWoodenBow");
        Icon(arrowItem, arrowModel, "CrudeStoneArrow");

        var sticks = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Materials/Sticks.asset");
        var rocks = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Materials/SmallRocks.asset");
        var fiber = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Materials/NettleFiber.asset");
        RecipeDefinition bowRecipe = Recipe("Assets/_Project/Data/Crafting/Recipes/CrudeWoodenBow.asset", "crudewoodenbow", bowItem, 1, -3,
            (sticks, 5), (fiber, 3));
        RecipeDefinition arrowRecipe = Recipe("Assets/_Project/Data/Crafting/Recipes/CrudeStoneArrow.asset", "crudestonearrow", arrowItem, 5, -2,
            (sticks, 2), (rocks, 1));
        AddToBook(bowRecipe, arrowRecipe);

        log.Add(SetUpPlayer(targetOutline, bowstring, arrowModel));
        log.Add(CombatAnimationSetup.BuildBowLayer());
        AssetDatabase.SaveAssets();
        log.Add($"bow={AssetDatabase.GetAssetPath(bowItem)} arrows={AssetDatabase.GetAssetPath(arrowItem)} icons={bowItem.Icon != null}/{arrowItem.Icon != null}");
        return string.Join("\n", log);
    }

    // ----- Materials and models -----

    private static Material LitMaterial(string path)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Palette));
            material.SetColor("_BaseColor", new Color(0.86f, 0.8f, 0.72f));
            material.SetFloat("_Smoothness", 0.2f);
            AssetDatabase.CreateAsset(material, path);
        }

        return material;
    }

    private static Material StringMaterial(string path)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetColor("_BaseColor", new Color(0.55f, 0.5f, 0.38f));
            AssetDatabase.CreateAsset(material, path);
        }

        return material;
    }

    private static Material OutlineMaterial(string path)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/UI/Materials/InteractionOutline.mat"));
            material.SetColor("_OutlineColor", new Color(1f, 0.12f, 0.08f, 1f));
            AssetDatabase.CreateAsset(material, path);
        }

        return material;
    }

    private static GameObject Model(string source, string path, Material material, Vector3 turn, Vector3 scale)
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
        {
            return existing;
        }

        var root = new GameObject(Path.GetFileNameWithoutExtension(path));
        var mesh = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(source));
        PrefabUtility.UnpackPrefabInstance(mesh, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        mesh.transform.SetParent(root.transform, false);
        mesh.transform.localRotation = Quaternion.Euler(turn);
        mesh.transform.localScale = scale;
        foreach (Renderer renderer in mesh.GetComponentsInChildren<Renderer>())
        {
            renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
        }

        foreach (Animator animator in mesh.GetComponentsInChildren<Animator>())
        {
            Object.DestroyImmediate(animator);
        }

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static ArrowProjectile Projectile(GameObject model, string path)
    {
        Folder(ProjectileFolder);
        var existing = AssetDatabase.LoadAssetAtPath<ArrowProjectile>(path);
        if (existing != null)
        {
            return existing;
        }

        var root = new GameObject(Path.GetFileNameWithoutExtension(path));
        var copy = (GameObject)PrefabUtility.InstantiatePrefab(model);
        copy.transform.SetParent(root.transform, false);
        foreach (Renderer renderer in copy.GetComponentsInChildren<Renderer>())
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        root.AddComponent<ArrowProjectile>();
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab.GetComponent<ArrowProjectile>();
    }

    // ----- Data -----

    private static AttackDefinition Attack(string path)
    {
        var attack = AssetDatabase.LoadAssetAtPath<AttackDefinition>(path);
        if (attack != null)
        {
            return attack;
        }

        attack = ScriptableObject.CreateInstance<AttackDefinition>();
        AssetDatabase.CreateAsset(attack, path);
        Set(attack, so =>
        {
            so.FindProperty("displayName").stringValue = "Bow Shot";
            so.FindProperty("baseDamage").floatValue = 0f;
            so.FindProperty("weaponDamageRatio").floatValue = 1f;
            so.FindProperty("powerScaling").floatValue = 0.8f;
            so.FindProperty("structureDamageMultiplier").floatValue = 0.2f;
            so.FindProperty("poiseDamage").floatValue = 8f;
            so.FindProperty("staminaCost").floatValue = 4f;
            so.FindProperty("hitSound").objectReferenceValue = Sound("Hit");
        });
        return attack;
    }

    private static ArrowDefinition Arrow(string path, ArrowProjectile projectile)
    {
        var arrow = AssetDatabase.LoadAssetAtPath<ArrowDefinition>(path);
        bool made = arrow == null;
        if (made)
        {
            arrow = ScriptableObject.CreateInstance<ArrowDefinition>();
            AssetDatabase.CreateAsset(arrow, path);
        }

        Set(arrow, so =>
        {
            so.FindProperty("projectile").objectReferenceValue = projectile;
            if (made)
            {
                so.FindProperty("displayName").stringValue = "Crude Stone Arrow";
                so.FindProperty("damage").floatValue = 14f;
                so.FindProperty("speedMultiplier").floatValue = 1f;
                so.FindProperty("gravityScale").floatValue = 0.6f;
                so.FindProperty("hitSound").objectReferenceValue = Sound("Hit");
                so.FindProperty("impactNoise").floatValue = 6f;
            }
        });
        return arrow;
    }

    private static WeaponDefinition Bow(string path, AttackDefinition shot)
    {
        var bow = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
        bool made = bow == null;
        if (made)
        {
            bow = ScriptableObject.CreateInstance<WeaponDefinition>();
            AssetDatabase.CreateAsset(bow, path);
        }

        Set(bow, so =>
        {
            so.FindProperty("stance").intValue = (int)WeaponStance.Bow;
            so.FindProperty("kind").intValue = (int)WeaponKind.Bow;
            so.FindProperty("shotAttack").objectReferenceValue = shot;
            if (made)
            {
                so.FindProperty("displayName").stringValue = "Crude Wooden Bow";
                so.FindProperty("damage").floatValue = 0f;
                so.FindProperty("guardLetThrough").floatValue = 0.6f;
                so.FindProperty("drawSeconds").floatValue = 1.1f;
                so.FindProperty("reloadSeconds").floatValue = 0.7f;
                so.FindProperty("arrowSpeed").floatValue = 30f;
                so.FindProperty("spread").floatValue = 2f;
                so.FindProperty("minimumDraw").floatValue = 0.25f;
                so.FindProperty("drawMoveSpeed").floatValue = 0.55f;
                so.FindProperty("swingSound").objectReferenceValue = Sound("Swing");
            }
        });
        return bow;
    }

    private static ItemDefinition Item(string path, string id, string displayName, string description, ItemCategory category,
        bool stackable, int maximumStack, float weight, GameObject heldModel)
    {
        var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
        bool made = item == null;
        if (made)
        {
            item = ScriptableObject.CreateInstance<ItemDefinition>();
            AssetDatabase.CreateAsset(item, path);
        }

        Set(item, so =>
        {
            so.FindProperty("heldModel").objectReferenceValue = heldModel;
            if (made)
            {
                so.FindProperty("id").stringValue = id;
                so.FindProperty("displayName").stringValue = displayName;
                so.FindProperty("description").stringValue = description;
                so.FindProperty("category").intValue = (int)category;
                so.FindProperty("tier").intValue = 1;
                so.FindProperty("stackable").boolValue = stackable;
                so.FindProperty("maximumStack").intValue = maximumStack;
                so.FindProperty("weight").floatValue = weight;
            }
        });
        return item;
    }

    private static void Icon(ItemDefinition item, GameObject model, string file)
    {
        if (item.Icon != null)
        {
            return;
        }

        Sprite icon = IconRenderer.RenderToSprite(model, "Assets/_Project/UI/ItemIcons/" + file + ".png");
        if (icon != null)
        {
            Set(item, so => so.FindProperty("icon").objectReferenceValue = icon);
        }
    }

    private static RecipeDefinition Recipe(string path, string id, ItemDefinition output, int amount, int sortOrder,
        params (ItemDefinition item, int amount)[] cost)
    {
        var recipe = AssetDatabase.LoadAssetAtPath<RecipeDefinition>(path);
        if (recipe != null)
        {
            return recipe;
        }

        recipe = ScriptableObject.CreateInstance<RecipeDefinition>();
        AssetDatabase.CreateAsset(recipe, path);
        Set(recipe, so =>
        {
            so.FindProperty("id").stringValue = id;
            so.FindProperty("output").objectReferenceValue = output;
            so.FindProperty("outputAmount").intValue = amount;
            so.FindProperty("station").intValue = (int)CraftingStationType.None;
            so.FindProperty("knownFromStart").boolValue = true;
            so.FindProperty("sortOrder").intValue = sortOrder;
            SerializedProperty items = so.FindProperty("itemCost");
            items.ClearArray();
            foreach ((ItemDefinition item, int count) in cost)
            {
                items.InsertArrayElementAtIndex(items.arraySize);
                SerializedProperty entry = items.GetArrayElementAtIndex(items.arraySize - 1);
                entry.FindPropertyRelative("item").objectReferenceValue = item;
                entry.FindPropertyRelative("amount").intValue = count;
            }
        });
        return recipe;
    }

    private static void AddToBook(params RecipeDefinition[] recipes)
    {
        var book = AssetDatabase.LoadAssetAtPath<RecipeBook>("Assets/_Project/Data/Crafting/RecipeBook.asset");
        Set(book, so =>
        {
            SerializedProperty list = so.FindProperty("recipes");
            foreach (RecipeDefinition recipe in recipes)
            {
                bool listed = false;
                for (int i = 0; i < list.arraySize; i++)
                {
                    listed |= list.GetArrayElementAtIndex(i).objectReferenceValue == recipe;
                }

                if (!listed)
                {
                    list.InsertArrayElementAtIndex(list.arraySize);
                    list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = recipe;
                }
            }
        });
    }

    // ----- Player -----

    private static string SetUpPlayer(Material targetOutline, Material bowstring, GameObject arrowModel)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
        try
        {
            var mask = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/UI/Materials/InteractionOutlineMask.mat");
            Component(root, out PlayerArchery _);
            Component(root, out PlayerArcheryAnimation _);
            Component(root, out ArcheryTargetOutline outline);
            Set(outline, so =>
            {
                so.FindProperty("outlineMaterial").objectReferenceValue = targetOutline;
                so.FindProperty("maskMaterial").objectReferenceValue = mask;
            });
            Component(root, out HeldBowView view);
            Set(view, so =>
            {
                so.FindProperty("stringMaterial").objectReferenceValue = bowstring;
                so.FindProperty("nockedArrow").objectReferenceValue = arrowModel;
            });
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
            return "player: archery, its animation, target outline and bow view";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void Component<T>(GameObject root, out T component) where T : Component
    {
        if (!root.TryGetComponent(out component))
        {
            component = root.AddComponent<T>();
        }
    }

    // ----- Helpers -----

    private static SoundDefinition Sound(string name) => AssetDatabase.LoadAssetAtPath<SoundDefinition>(Sounds + name + ".asset");

    private static void Set(Object target, System.Action<SerializedObject> change)
    {
        var so = new SerializedObject(target);
        change(so);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        Folder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
