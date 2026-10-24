using UnityEditor;
using UnityEngine;

/// <summary>
/// Gives the player a human body (Safe Haven › Characters › Use Human Body for Player): Kevin Iglesias' HumanM model,
/// the body the locomotion, work and gathering clips were made for, which also takes the Quaternius combat clips
/// cleanly. Replaces the Starter Assets robot's mesh and skeleton under the player prefab, switches the Animator's avatar,
/// gives it a plain linen material (placeholder, readable on dark ground and grass) and refits the held-item grip to the
/// new hand. Running it again rebuilds the body.
/// </summary>
public static class PlayerBodySetup
{
    public const string ModelPath = "Assets/Kevin Iglesias/Human Animations/Models/HumanM_Model.fbx";
    public const string MaterialPath = "Assets/_Project/Art/Characters/Survivor.mat";
    // Children of the player root that make up its body: the old robot's and this model's.
    private static readonly string[] BodyParts = { "Geometry", "Skeleton", "HumanM_BodyMesh", "Rig" };

    [MenuItem("Safe Haven/Characters/Use Human Body for Player")]
    public static void UseHumanBodyMenu() => Debug.Log(UseHumanBody());

    public static string UseHumanBody()
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        Avatar avatar = model != null ? model.GetComponent<Animator>()?.avatar : null;
        if (avatar == null)
        {
            return $"Player body: no humanoid model at {ModelPath}.";
        }

        Material material = EnsureMaterial();
        GameObject root = PrefabUtility.LoadPrefabContents(CombatAnimationSetup.PlayerPrefabPath);
        try
        {
            foreach (string part in BodyParts)
            {
                Transform old = root.transform.Find(part);
                if (old != null)
                {
                    Object.DestroyImmediate(old.gameObject);
                }
            }

            // The model's children go straight under the player root, so the avatar's bone paths still match.
            GameObject body = Object.Instantiate(model);
            while (body.transform.childCount > 0)
            {
                Transform child = body.transform.GetChild(0);
                child.SetParent(root.transform, false);
                child.SetSiblingIndex(1);
            }

            Object.DestroyImmediate(body);
            var animator = root.GetComponent<Animator>();
            animator.avatar = avatar;
            foreach (SkinnedMeshRenderer mesh in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                mesh.sharedMaterial = material;
                mesh.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                mesh.updateWhenOffscreen = false;
            }

            PrefabUtility.SaveAsPrefabAsset(root, CombatAnimationSetup.PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        string grip = HeldItemGrips.FitPlayerGrip();
        return $"Player body: {model.name} with {material.name}. {grip}";
    }

    private static Material EnsureMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material != null)
        {
            return material;
        }

        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(MaterialPath));
        AssetDatabase.Refresh();
        material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Survivor" };
        // Undyed linen: light enough to read against the dark forest floor, warm enough not to look like a robot.
        material.SetColor("_BaseColor", new Color(0.74f, 0.68f, 0.58f));
        material.SetFloat("_Smoothness", 0.18f);
        AssetDatabase.CreateAsset(material, MaterialPath);
        return material;
    }
}
