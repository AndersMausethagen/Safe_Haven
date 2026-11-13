using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A live picture of something in the world, in a gold-framed card (Hearthline 3): a camera of its own draws the subject
/// before a painting (PortraitStage, Resources/WindowArt/&lt;art&gt;) into the card, placed each frame by the window's
/// framing. The camera only runs while the card is shown and has a subject, so a closed window costs nothing.
/// </summary>
[DisallowMultipleComponent]
public sealed class KitPortrait : MonoBehaviour
{
    // The picture is drawn at twice the card's size, for a sharp image on a large screen.
    private const float Resolution = 2f;
    private const float FramedAspect = 4f / 3f;

    private RenderTexture texture;
    private Camera view;
    private string art;
    private Transform subject;
    private Func<Transform, (Vector3 eye, Vector3 look)> framing;

    /// <summary>Builds the card at <paramref name="x"/>, <paramref name="y"/> (from the parent's top left).</summary>
    public static KitPortrait Make(Transform parent, string name, float x, float y, float width, float height, string art)
    {
        RectTransform frame = HearthKit.Card(parent, name, x, y, width, height, 0f, null, 1.5f);
        RectTransform inner = CodeUI.Stretch("Image", frame);
        inner.offsetMin = new Vector2(5f, 5f);
        inner.offsetMax = new Vector2(-5f, -5f);
        inner.gameObject.AddComponent<RectMask2D>();
        var picture = CodeUI.Stretch("Render", inner).gameObject.AddComponent<RawImage>();
        picture.raycastTarget = false;
        HearthKit.Rim(frame, 0.85f, 1.5f).transform.SetAsLastSibling();

        var portrait = frame.gameObject.AddComponent<KitPortrait>();
        portrait.art = art;
        portrait.texture = new RenderTexture(Mathf.RoundToInt(width * Resolution), Mathf.RoundToInt(height * Resolution), 24) { name = name + " Portrait" };
        picture.texture = portrait.texture;
        return portrait;
    }

    /// <summary>The card, to place or hide.</summary>
    public RectTransform Frame => (RectTransform)transform;

    /// <summary>Shows <paramref name="who"/>, the camera placed by <paramref name="shot"/> (where it stands, what it looks at).</summary>
    public void Show(Transform who, Func<Transform, (Vector3 eye, Vector3 look)> shot)
    {
        subject = who;
        framing = shot;
    }

    /// <summary>Stops drawing: the camera rests until a subject is shown again.</summary>
    public void Hide()
    {
        subject = null;
        if (view != null)
        {
            view.enabled = false;
        }
    }

    private void LateUpdate()
    {
        if (subject == null || framing == null)
        {
            if (view != null && view.enabled)
            {
                view.enabled = false;
            }

            return;
        }

        if (view == null)
        {
            var holder = new GameObject(name + " Camera");
            holder.transform.SetParent(transform, false);
            view = holder.AddComponent<Camera>();
            view.targetTexture = texture;
            view.fieldOfView = 30f;
            view.nearClipPlane = 0.2f;
            view.farClipPlane = 150f;
        }

        PortraitStage.On(view, art, subject);
        view.enabled = true;
        (Vector3 eye, Vector3 look) = framing(subject);
        // Shots are framed for a 4:3 card; a narrower one steps back so the same width still fits in it.
        eye = look + (eye - look) * Mathf.Max(1f, FramedAspect / Mathf.Max(0.1f, view.aspect));
        view.transform.position = eye;
        view.transform.LookAt(look);
    }

    // The window closing hides the card: its camera stops with it.
    private void OnDisable()
    {
        if (view != null)
        {
            view.enabled = false;
        }
    }

    private void OnDestroy()
    {
        if (texture != null)
        {
            texture.Release();
            Destroy(texture);
        }
    }
}
