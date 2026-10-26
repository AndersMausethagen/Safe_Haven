using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

[Category("InteractionReadability")]
public sealed class InteractionOutlineTests
{
    private GameObject user;
    private GameObject target;

    [TearDown]
    public void TearDown()
    {
        if (user != null) Object.DestroyImmediate(user);
        if (target != null)
        {
            Invoke(target.GetComponent<Interactable>(), "OnDisable");
            Object.DestroyImmediate(target);
        }
    }

    [Test]
    public void NearbyOutlinePreservesOriginalMaterialsAndDisappearsOutsideReach()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/UI/Materials/InteractionOutline.mat");
        Assert.IsNotNull(material);
        Assert.IsTrue(material.shader.isSupported);
        Assert.IsFalse(ShaderUtil.ShaderHasError(material.shader));

        user = new GameObject("Outline test player");
        user.AddComponent<PlayerInteractor>();
        var presentation = user.AddComponent<PlayerInteractionOutline>();
        var data = new SerializedObject(presentation);
        data.FindProperty("outlineMaterial").objectReferenceValue = material;
        data.FindProperty("maskMaterial").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/UI/Materials/InteractionOutlineMask.mat");
        data.ApplyModifiedPropertiesWithoutUndo();
        Invoke(presentation, "Awake");

        target = new GameObject("Nearby object");
        var interactable = target.AddComponent<Interactable>();
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.transform.SetParent(target.transform, false);
        Renderer source = visual.GetComponent<Renderer>();
        Material original = source.sharedMaterial;
        Invoke(interactable, "Awake");
        Invoke(interactable, "OnEnable");

        Invoke(presentation, "LateUpdate");
        Renderer outline = visual.transform.Find("Interaction Outline").GetComponent<Renderer>();
        Renderer mask = visual.transform.Find("Interaction Mask").GetComponent<Renderer>();
        Assert.IsTrue(outline.enabled);
        Assert.IsTrue(mask.enabled);
        Assert.AreSame(original, source.sharedMaterial);
        Assert.AreSame(material, outline.sharedMaterial);
        Assert.IsNull(outline.GetComponent<Collider>());

        target.transform.position = Vector3.right * 1000f;
        Physics.SyncTransforms();
        Invoke(presentation, "LateUpdate");
        Assert.IsFalse(outline.enabled);
        Assert.IsFalse(mask.enabled);
        target.transform.position = Vector3.zero;
        Physics.SyncTransforms();
        Invoke(presentation, "LateUpdate");
        Assert.IsTrue(outline.enabled);
        source.enabled = false;
        Invoke(presentation, "LateUpdate");
        Assert.IsFalse(outline.enabled);
        source.enabled = true;
        Invoke(presentation, "OnDisable");
        Assert.IsFalse(outline.enabled);
    }

    [Test]
    public void HoverFindsChildColliderBeyondInteractionReachButNotThroughAnOccluder()
    {
        user = new GameObject("Outline test player");
        var presentation = user.AddComponent<PlayerInteractionOutline>();
        target = new GameObject("Hovered object");
        var interactable = target.AddComponent<Interactable>();
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.transform.SetParent(target.transform, false);
        target.transform.position = new Vector3(500f, 0f, 20f);
        Physics.SyncTransforms();
        var pick = typeof(PlayerInteractionOutline).GetMethod("PickHovered", BindingFlags.Instance | BindingFlags.NonPublic);
        var ray = new Ray(new Vector3(500f, 0f, 0f), Vector3.forward);
        Assert.AreSame(interactable, pick.Invoke(presentation, new object[] { ray }));

        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            wall.transform.position = new Vector3(500f, 0f, 10f);
            Physics.SyncTransforms();
            Assert.IsNull(pick.Invoke(presentation, new object[] { ray }));
        }
        finally { Object.DestroyImmediate(wall); }
    }

    [TestCase(true)]
    [TestCase(false)]
    public void ThinGroundLevelStickHasVisibleContourWithoutPaintingItsInterior(bool orthographic)
    {
        target = new GameObject("Thin stick");
        var interactable = target.AddComponent<Interactable>();
        GameObject stick = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stick.transform.SetParent(target.transform, false);
        stick.transform.localPosition = Vector3.up * 0.06f;
        stick.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        stick.transform.localScale = new Vector3(0.06f, 0.39f, 0.06f);
        user = new GameObject("Outline test player");
        var presentation = user.AddComponent<PlayerInteractionOutline>();
        var data = new SerializedObject(presentation);
        data.FindProperty("outlineMaterial").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/UI/Materials/InteractionOutline.mat");
        data.FindProperty("maskMaterial").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/UI/Materials/InteractionOutlineMask.mat");
        data.ApplyModifiedPropertiesWithoutUndo();
        Invoke(presentation, "Awake");
        Invoke(interactable, "Awake");
        Invoke(interactable, "OnEnable");
        Invoke(presentation, "LateUpdate");
        Renderer outline = stick.transform.Find("Interaction Outline").GetComponent<Renderer>();
        foreach (Transform child in target.GetComponentsInChildren<Transform>()) child.gameObject.layer = 31;

        var cameraObject = new GameObject("Outline test camera");
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var texture = new Texture2D(256, 256, TextureFormat.RGB24, false);
        var renderTarget = new RenderTexture(256, 256, 24);
        RenderTexture previous = RenderTexture.active;
        try
        {
            ground.layer = 31;
            ground.transform.position = Vector3.down * 0.02f;
            ground.transform.localScale = new Vector3(4f, 0.02f, 4f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 3f, -3f);
            camera.transform.LookAt(Vector3.zero);
            camera.cullingMask = 1 << 31;
            camera.orthographic = orthographic;
            camera.orthographicSize = 0.8f;
            camera.fieldOfView = 25f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.targetTexture = renderTarget;
            camera.Render();
            RenderTexture.active = renderTarget;
            texture.ReadPixels(new Rect(0f, 0f, 256f, 256f), 0, 0);
            texture.Apply();
            Color32[] withOutline = texture.GetPixels32();
            outline.enabled = false;
            camera.Render();
            RenderTexture.active = renderTarget;
            texture.ReadPixels(new Rect(0f, 0f, 256f, 256f), 0, 0);
            texture.Apply();
            Color32[] withoutOutline = texture.GetPixels32();
            int changed = 0;
            for (int i = 0; i < withOutline.Length; i++)
            {
                if (Mathf.Abs(withOutline[i].r - withoutOutline[i].r) +
                    Mathf.Abs(withOutline[i].g - withoutOutline[i].g) +
                    Mathf.Abs(withOutline[i].b - withoutOutline[i].b) > 20) changed++;
            }
            Assert.Greater(changed, 100, "Thin rods need an actual visible contour, not just enabled renderers.");
            Vector3 centre = camera.WorldToScreenPoint(stick.transform.position);
            int index = Mathf.RoundToInt(centre.y) * 256 + Mathf.RoundToInt(centre.x);
            Assert.AreEqual(withoutOutline[index], withOutline[index], "The mask must keep the original interior material visible.");
        }
        finally
        {
            RenderTexture.active = previous;
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(ground);
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(renderTarget);
        }
    }

    [Test]
    public void PlayerPrefabHasConfiguredOutlineAndQuickstepUsesAnActionNotOnlyALanding()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Characters/PlayerArmature.prefab");
        var presentation = prefab.GetComponent<PlayerInteractionOutline>();
        Assert.IsNotNull(presentation);
        Assert.IsNotNull(new SerializedObject(presentation).FindProperty("outlineMaterial").objectReferenceValue);
        Assert.IsNotNull(new SerializedObject(presentation).FindProperty("maskMaterial").objectReferenceValue);
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CombatAnimationSetup.PlayerControllerPath);
        AnimatorState quickstep = null;
        foreach (AnimatorControllerLayer layer in controller.layers)
        {
            if (layer.name != "Combat Full Body") continue;
            foreach (ChildAnimatorState state in layer.stateMachine.states)
            {
                if (state.state.name == "Quickstep") quickstep = state.state;
            }
        }
        Assert.IsNotNull(quickstep);
        Assert.AreEqual("Slide_Start", quickstep.motion.name);
        var settings = AssetDatabase.LoadAssetAtPath<PlayerMovementSettings>("Assets/_Project/Data/Player/PlayerMovementSettings.asset");
        Assert.AreEqual(DodgeStyle.Quickstep, settings.StyleFor(0f));
        Assert.Greater(settings.ShapeOf(DodgeStyle.Quickstep).distance, 0f);
    }

    private static void Invoke(object target, string method)
    {
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    }
}
