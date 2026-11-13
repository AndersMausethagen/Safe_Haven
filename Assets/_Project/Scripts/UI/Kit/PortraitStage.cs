using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// A portrait camera's stage (the user, 2026-11-13: a painted background in the character and horse windows instead of
/// the world behind them): while that camera draws, and only then, its subject is moved to a layer of its own, a painting
/// hangs behind it facing the camera (Resources/WindowArt/&lt;art&gt;, unlit, filling the view) and a soft fill light lights it
/// the same at any hour; everything goes back as soon as the camera is done, so the game's own view never sees any of it.
/// The camera draws only that layer. Put on the portrait camera by the window that owns it.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class PortraitStage : MonoBehaviour
{
    /// <summary>A layer nothing else uses; the subject is on it only while the portrait draws.</summary>
    public const int Layer = 31;

    private Camera view;
    private Transform subject;
    private MeshRenderer backdrop;
    private Material material;
    private Light fill;
    private readonly List<Renderer> renderers = new List<Renderer>();
    private readonly List<int> layers = new List<int>();
    private bool staged;

    /// <summary>Gives a portrait camera its stage with the painting <paramref name="art"/>, and points it at <paramref name="who"/>.</summary>
    public static PortraitStage On(Camera camera, string art, Transform who)
    {
        PortraitStage stage = camera.GetComponent<PortraitStage>();
        if (stage == null)
        {
            stage = camera.gameObject.AddComponent<PortraitStage>();
            stage.Build(art);
        }

        stage.subject = who;
        return stage;
    }

    private void Build(string art)
    {
        view = GetComponent<Camera>();
        view.cullingMask = 1 << Layer;
        view.clearFlags = CameraClearFlags.SolidColor;
        view.backgroundColor = UIStyle.Current.Panel;

        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "Portrait Backdrop";
        Destroy(quad.GetComponent<Collider>());
        quad.transform.SetParent(transform, false);
        quad.layer = Layer;
        backdrop = quad.GetComponent<MeshRenderer>();
        backdrop.shadowCastingMode = ShadowCastingMode.Off;
        backdrop.receiveShadows = false;
        // Unlit and fog-free: the painting is shown as painted.
        material = new Material(Shader.Find("UI/Default")) { name = "Portrait Backdrop" };
        Sprite sprite = Resources.Load<Sprite>("WindowArt/" + art);
        material.mainTexture = sprite != null ? sprite.texture : null;
        material.color = sprite != null ? Color.white : UIStyle.Current.Panel;
        // Drawn first, writing no depth: the subject, drawn after it, is always in front.
        material.renderQueue = 1000;
        backdrop.sharedMaterial = material;
        backdrop.enabled = false;

        var light = new GameObject("Portrait Fill");
        light.transform.SetParent(transform, false);
        light.transform.localPosition = new Vector3(-1.2f, 1.4f, 0.3f);
        fill = light.AddComponent<Light>();
        fill.type = LightType.Point;
        fill.range = 14f;
        fill.intensity = 2.2f;
        fill.color = new Color(1f, 0.88f, 0.72f);
        fill.cullingMask = 1 << Layer;
        fill.shadows = LightShadows.None;
        fill.enabled = false;
    }

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += Begin;
        RenderPipelineManager.endCameraRendering += End;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= Begin;
        RenderPipelineManager.endCameraRendering -= End;
        Restore();
    }

    private void OnDestroy()
    {
        if (material != null)
        {
            Destroy(material);
        }
    }

    private void Begin(ScriptableRenderContext context, Camera camera)
    {
        if (camera != view || subject == null)
        {
            return;
        }

        renderers.Clear();
        layers.Clear();
        subject.GetComponentsInChildren(false, renderers);
        foreach (Renderer part in renderers)
        {
            layers.Add(part.gameObject.layer);
            part.gameObject.layer = Layer;
        }

        staged = true;
        PlaceBackdrop();
        backdrop.enabled = true;
        fill.enabled = true;
    }

    private void End(ScriptableRenderContext context, Camera camera)
    {
        if (camera == view)
        {
            Restore();
        }
    }

    private void Restore()
    {
        if (!staged)
        {
            return;
        }

        staged = false;
        for (int i = 0; i < renderers.Count && i < layers.Count; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].gameObject.layer = layers[i];
            }
        }

        if (backdrop != null)
        {
            backdrop.enabled = false;
        }

        if (fill != null)
        {
            fill.enabled = false;
        }
    }

    // Behind the subject, square to the camera and large enough to fill its view, the painting's own shape kept.
    private void PlaceBackdrop()
    {
        float distance = Vector3.Distance(view.transform.position, subject.position) + 3.5f;
        float high = 2f * distance * Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.08f;
        float wide = high * Mathf.Max(0.1f, view.aspect);
        Texture art = material.mainTexture;
        float shape = art != null && art.height > 0 ? art.width / (float)art.height : wide / high;
        if (wide / high > shape)
        {
            high = wide / shape;
        }
        else
        {
            wide = high * shape;
        }

        Transform quad = backdrop.transform;
        quad.position = view.transform.position + view.transform.forward * distance;
        quad.rotation = view.transform.rotation;
        quad.localScale = new Vector3(wide, high, 1f);
    }
}
