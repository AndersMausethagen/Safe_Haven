using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Makes the people of the story modular humans (Safe Haven › Setup › Characters › Build People): a model prefab per person in
/// Prefabs/Characters/People (their look, clothes, the Villager controller and emotes), set as the person's
/// ResidentDefinition model, and put in place of the old stand-in model on the stranded traveler and the prologue's
/// expedition. The looks below are the authored ones; edit them on the prefabs afterwards if you like, but running this
/// again rebuilds them from here.
/// </summary>
public static class PeopleSetup
{
    public const string PeopleFolder = "Assets/_Project/Prefabs/Characters/People";
    public const string PrologueScenePath = "Assets/_Project/Scenes/Prologue_MistyWoods.unity";
    public const string TravelerPrefabPath = "Assets/_Project/Prefabs/Characters/StrandedTraveler.prefab";
    private const string OldModelName = "IngridModel";

    private sealed class Person
    {
        public string Name;
        public string Resident;
        public CharacterAppearance Look;
        public string[] Outfit;
    }

    private static CharacterAppearance Woman(int skin, int hair, int colour, int eyes, int underclothes) => new CharacterAppearance
    {
        gender = CharacterGender.Female, skin = skin, hair = hair, hairColour = colour, eyes = eyes, underclothes = underclothes,
    };

    private static CharacterAppearance Man(int skin, int hair, int colour, int brows, int beard, int eyes, int underclothes) => new CharacterAppearance
    {
        gender = CharacterGender.Male, skin = skin, hair = hair, hairColour = colour, brows = brows, beard = beard, eyes = eyes,
        underclothes = underclothes,
    };

    // Hair colours: 0 blond, 1 black, 2 brown, 3 grey. Eyes: 0 blue, 1 brown, 2 green, 3 pale.
    private static readonly Person[] People =
    {
        new Person { Name = "Sigrid", Resident = "Prologue/Residents/Sigrid", Look = Woman(1, 1, 3, 0, 2),
            Outfit = new[] { "CuredLeather_Body", "CuredLeather_Waist", "CuredLeather_Feet", "CuredLeather_Hands" } },
        new Person { Name = "Ingrid", Resident = "Settlement/Ingrid", Look = Woman(0, 0, 0, 0, 0),
            Outfit = new[] { "WovenHide_Body", "WovenHide_Feet" } },
        new Person { Name = "Runa", Resident = "Prologue/Residents/Runa", Look = Woman(2, 4, 2, 2, 3),
            Outfit = new[] { "WovenHide_Body", "WovenHide_Waist", "WovenHide_Feet" } },
        new Person { Name = "Liv", Resident = "Prologue/Residents/Liv", Look = Woman(1, 5, 1, 1, 4),
            Outfit = new[] { "CuredLeather_Body", "WovenHide_Feet", "WovenHide_Hands" } },
        new Person { Name = "Bjarne", Resident = "Prologue/Residents/Bjarne", Look = Man(3, 5, 2, 1, 1, 1, 1),
            Outfit = new[] { "CopperScale_Body", "CuredLeather_Waist", "CuredLeather_Feet" } },
        new Person { Name = "Tor", Resident = "Prologue/Residents/Tor", Look = Man(1, 1, 0, 0, 3, 0, 0),
            Outfit = new[] { "WovenHide_Body", "WovenHide_Feet", "WovenHide_Hands" } },
        new Person { Name = "Eirik", Resident = "Prologue/Residents/Eirik", Look = Man(4, 2, 1, 2, 0, 2, 2),
            Outfit = new[] { "CuredLeather_Body", "CuredLeather_Feet", "WovenHide_Waist" } },
    };

    [MenuItem("Safe Haven/Setup/Characters/Build People", false, 200)]
    public static void BuildFromMenu() => Debug.Log(BuildAll());

    /// <summary>Builds the prefabs, sets the residents' models and replaces the traveler's model. The prologue scene is
    /// done separately (<see cref="ReplaceInPrologue"/>), as it has to be open.</summary>
    public static string BuildAll()
    {
        CharacterLibraryBuilder.EnsureFolder(PeopleFolder);
        var report = new StringBuilder();
        foreach (Person person in People)
        {
            GameObject prefab = BuildPrefab(person);
            var resident = AssetDatabase.LoadAssetAtPath<ResidentDefinition>($"Assets/_Project/Data/{person.Resident}.asset");
            if (resident != null)
            {
                var serialized = new SerializedObject(resident);
                serialized.FindProperty("model").objectReferenceValue = prefab;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            report.Append(person.Name).Append(resident != null ? " (resident)" : " (no resident)").Append(", ");
        }

        report.Append(ReplaceInTraveler());
        AssetDatabase.SaveAssets();
        return "People: " + report;
    }

    public static GameObject PersonPrefab(string name) => AssetDatabase.LoadAssetAtPath<GameObject>($"{PeopleFolder}/{name}.prefab");

    private static GameObject BuildPrefab(Person person)
    {
        string path = $"{PeopleFolder}/{person.Name}.prefab";
        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CharacterAnimatorBuilder.VillagerControllerPath);
        List<GearLookDefinition> outfit = person.Outfit
            .Select(look => AssetDatabase.LoadAssetAtPath<GearLookDefinition>($"{CharacterLibraryBuilder.GearRoot}/{look}.asset"))
            .Where(look => look != null)
            .ToList();
        CharacterAppearance look = person.Look.Clone();
        look.name = person.Name;

        bool loaded = PersonPrefab(person.Name) != null;
        GameObject root = loaded ? PrefabUtility.LoadPrefabContents(path) : new GameObject(person.Name);
        try
        {
            ModularBodySetup.Apply(root, look, outfit, new Transform[0], controller);
            root.GetComponent<Animator>().cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            return PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            if (loaded)
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            else
            {
                Object.DestroyImmediate(root);
            }
        }
    }

    private static string ReplaceInTraveler()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(TravelerPrefabPath);
        try
        {
            int replaced = ReplaceModels(root.transform, "Ingrid");
            PrefabUtility.SaveAsPrefabAsset(root, TravelerPrefabPath);
            return $"traveler: {replaced} model replaced.";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>Replaces the expedition's stand-in models in the open prologue scene. Doesn't save the scene.</summary>
    public static string ReplaceInPrologue()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != PrologueScenePath)
        {
            return $"Open {PrologueScenePath} first.";
        }

        var report = new StringBuilder();
        foreach (GameObject top in scene.GetRootGameObjects())
        {
            foreach (Transform t in top.GetComponentsInChildren<Transform>(true).ToList())
            {
                if (t == null || t.Find(OldModelName) == null && !HasPersonModel(t))
                {
                    continue;
                }

                Person person = People.FirstOrDefault(p => t.name == p.Name || t.name.StartsWith(p.Name + " "));
                if (person == null)
                {
                    report.Append("(no look for ").Append(t.name).Append(") ");
                    continue;
                }

                int replaced = ReplaceModels(t, person.Name);
                report.Append(t.name).Append(':').Append(replaced).Append(' ');
            }
        }

        report.Append("torches added: ").Append(EnsureExpeditionTorches(scene));
        EditorSceneManager.MarkSceneDirty(scene);
        return report.ToString();
    }

    // The expedition walks the night woods with a torch in each left hand (kept upright by ExpeditionTorch). Runa's is
    // out: she leads with her bow.
    private const string TorchName = "Expedition Torch";
    private const string TorchWoodGuid = "3848ab56c05532f48ac94a9eddaec3c2";
    private const string TorchFlameGuid = "c147eab80a0a45944bcb821b27bdb434";

    /// <summary>Gives every prologue companion without one an expedition torch in the left hand. Returns how many were added.</summary>
    public static int EnsureExpeditionTorches(Scene scene)
    {
        int added = 0;
        foreach (PrologueCompanion companion in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PrologueCompanion>(true)))
        {
            if (companion.GetComponentsInChildren<Transform>(true).Any(t => t.name == TorchName))
            {
                continue;
            }

            Animator animator = companion.GetComponentsInChildren<Animator>(true).FirstOrDefault(a => a.isHuman);
            Transform hand = animator != null ? animator.GetBoneTransform(HumanBodyBones.LeftHand) : null;
            if (hand == null)
            {
                continue;
            }

            GameObject torch = BuildTorch(hand);
            torch.SetActive(!companion.name.StartsWith("Runa"));
            added++;
        }

        return added;
    }

    private static GameObject BuildTorch(Transform hand)
    {
        var torch = new GameObject(TorchName);
        Undo.RegisterCreatedObjectUndo(torch, "Expedition torch");
        torch.transform.SetParent(hand, false);
        torch.transform.localScale = Vector3.one * 0.4f;
        torch.AddComponent<ExpeditionTorch>();

        Material wood = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(TorchWoodGuid));
        Material flame = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(TorchFlameGuid));
        TorchPart(torch.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.4f, 0f), new Vector3(0.14f, 0.85f, 0.14f), wood);
        TorchPart(torch.transform, PrimitiveType.Cylinder, new Vector3(0f, 1.33f, 0f), new Vector3(0.22f, 0.1f, 0.22f), wood);
        TorchPart(torch.transform, PrimitiveType.Sphere, new Vector3(0f, 1.47f, 0f), new Vector3(0.26f, 0.42f, 0.26f), flame);

        var glow = new GameObject("Torchlight");
        glow.transform.SetParent(torch.transform, false);
        glow.transform.localPosition = new Vector3(0f, 1.5f, 0f);
        var light = glow.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.49f, 0.15f);
        light.intensity = 3f;
        light.range = 8f;
        light.shadows = LightShadows.None;
        glow.AddComponent<LightFlicker>();
        return torch;
    }

    private static void TorchPart(Transform torch, PrimitiveType shape, Vector3 position, Vector3 scale, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(shape);
        Object.DestroyImmediate(part.GetComponent<Collider>());
        part.transform.SetParent(torch, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        if (material != null)
        {
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
    }

    private static bool HasPersonModel(Transform t) =>
        People.Any(p => t.Find(p.Name + " Model") != null);

    // The old model child (or an earlier person model) becomes an instance of the person's prefab, in the same place.
    private static int ReplaceModels(Transform owner, string person)
    {
        Transform old = owner.Find(OldModelName) ?? owner.Find(person + " Model");
        GameObject prefab = PersonPrefab(person);
        if (old == null || prefab == null)
        {
            return 0;
        }

        Vector3 position = old.localPosition;
        Quaternion rotation = old.localRotation;
        Vector3 scale = old.localScale;
        int index = old.GetSiblingIndex();
        Object.DestroyImmediate(old.gameObject);

        var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, owner);
        model.name = person + " Model";
        model.transform.SetSiblingIndex(index);
        model.transform.localPosition = position;
        model.transform.localRotation = rotation;
        model.transform.localScale = scale;
        return 1;
    }
}
