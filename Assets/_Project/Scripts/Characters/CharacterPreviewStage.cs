using UnityEngine;

/// <summary>
/// The character creation screen's little photo studio: the character being made, standing idle far below the world, with a
/// camera and two lights of its own rendering into a texture the screen shows. Made when the screen opens and destroyed
/// with it, so nothing of it lingers in the game.
/// </summary>
public sealed class CharacterPreviewStage : MonoBehaviour
{
    private static readonly Vector3 StagePosition = new Vector3(0f, -1000f, 0f);
    private const float StartYaw = -14f;

    private Camera view;
    private ModularCharacter model;
    private RenderTexture texture;
    private float yaw = StartYaw;

    public RenderTexture Texture => texture;

    public static CharacterPreviewStage Create(int width, int height)
    {
        var root = new GameObject("Character Preview Stage");
        root.transform.position = StagePosition;
        var stage = root.AddComponent<CharacterPreviewStage>();
        stage.Build(width, height);
        return stage;
    }

    private void Build(int width, int height)
    {
        texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { name = "Character Preview", antiAliasing = 4 };

        var cameraObject = new GameObject("Preview Camera");
        cameraObject.transform.SetParent(transform, false);
        cameraObject.transform.localPosition = new Vector3(0f, 1.0f, 4.6f);
        cameraObject.transform.LookAt(transform.position + new Vector3(0f, 0.92f, 0f));
        view = cameraObject.AddComponent<Camera>();
        view.targetTexture = texture;
        view.clearFlags = CameraClearFlags.SolidColor;
        view.backgroundColor = new Color(0.09f, 0.08f, 0.07f, 1f);
        view.fieldOfView = 26f;
        view.nearClipPlane = 0.1f;
        view.farClipPlane = 12f;

        AddLight("Key Light", new Vector3(-1.8f, 2.6f, 3.2f), new Color(1f, 0.93f, 0.82f), 26f);
        AddLight("Fill Light", new Vector3(2.2f, 1.4f, 2.6f), new Color(0.7f, 0.78f, 0.95f), 9f);
        AddLight("Rim Light", new Vector3(0.6f, 2.4f, -2.4f), new Color(1f, 0.85f, 0.65f), 14f);

        var body = new GameObject("Character");
        body.transform.SetParent(transform, false);
        body.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        var animator = body.AddComponent<Animator>();
        animator.runtimeAnimatorController = CharacterCatalog.Current.PreviewAnimations;
        animator.applyRootMotion = false;
        model = body.AddComponent<ModularCharacter>();
    }

    private void AddLight(string lightName, Vector3 localPosition, Color colour, float intensity)
    {
        var holder = new GameObject(lightName);
        holder.transform.SetParent(transform, false);
        holder.transform.localPosition = localPosition;
        holder.transform.LookAt(transform.position + new Vector3(0f, 1f, 0f));
        var light = holder.AddComponent<Light>();
        light.type = LightType.Spot;
        light.spotAngle = 50f;
        light.range = 9f;
        light.color = colour;
        light.intensity = intensity;
        light.shadows = LightShadows.None;
    }

    /// <summary>Dresses the stand-in in this look.</summary>
    public void Show(CharacterAppearance look, params GearLookDefinition[] wearing)
    {
        model.SetOutfit(wearing);
        model.SetAppearance(look);
    }

    /// <summary>Spins the character round (dragging the preview).</summary>
    public void Turn(float degrees)
    {
        yaw += degrees;
        model.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
    }

    private void OnDestroy()
    {
        if (view != null)
        {
            view.targetTexture = null;
        }

        if (texture != null)
        {
            texture.Release();
            Destroy(texture);
        }
    }
}
