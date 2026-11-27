using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The start menu's living picture (the user, 2026-11-27: "in the style of the cinematics, where there is a moving foreground and
/// background, and some vfx to make the image come to life"): the first shot of a <see cref="CinematicDefinition"/>, its painted
/// layers drawn as a cinematic draws them (CinematicScreen.DrawLayer, CinematicCamera.Place), but held for as long as the menu is up.
/// The camera eases from the shot's Camera From to its Camera To and back over Sway Seconds, and leans a little after the pointer, so
/// the near layers slide over the far ones. A layer's Drift sways to and fro instead of sliding away, its Flicker flickers, its specks
/// rise, and its Opacity curve runs once a loop (a blink, a glow coming and going). It loops without a seam (the user, 2026-11-27: "make
/// sure the camera movement and animations are loopable"): the camera, the drifting mist and the opacity curves all repeat exactly every
/// two sways; the flicker, the sparks and the smoke are random and never repeat, so they have no seam to show. Covers this RectTransform
/// as a 16:9 frame (ArtCover). With no backdrop set, the scene's still picture stays as it was. Reads its asset only.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public sealed class MenuBackdrop : MonoBehaviour
{
    private const float FrameAspect = 16f / 9f;
    private const float WidestCrop = 0.2f;

    private sealed class Drawn
    {
        public CinematicLayer Layer;
        public RectTransform Rect;
        public Graphic Graphic;
        public CinematicMotesGraphic Motes;
    }

    [Tooltip("Its first shot is the picture: layers back to front, and the camera's two ends of its sway.")]
    [SerializeField] private CinematicDefinition backdrop;
    [Tooltip("Seconds the camera takes from Camera From to Camera To; it comes back as slowly. Twice this is the loop that the mist's sway " +
             "and the layers' Opacity curves follow too.")]
    [SerializeField, Min(4f)] private float swaySeconds = 40f;
    [Tooltip("How far the camera leans after the pointer at the screen's edge, in frame widths and heights (as a pose's position).")]
    [SerializeField] private Vector2 pointerLean = new Vector2(0.012f, 0.006f);
    [Tooltip("Seconds the lean takes to catch up with the pointer.")]
    [SerializeField, Min(0.05f)] private float leanSeconds = 1.4f;
    [Tooltip("The scene's still picture, hidden while the backdrop draws.")]
    [SerializeField] private Graphic stillPicture;

    private readonly List<Drawn> layers = new List<Drawn>();
    private RectTransform frame;
    private Vector2 lean;
    private float time;

    /// <summary>Seconds after which the camera, the mist and the opacity curves are where they began: there and back.</summary>
    public float LoopSeconds => swaySeconds * 2f;

    /// <summary>The asset it draws (null: none, the still picture shows).</summary>
    public CinematicDefinition Backdrop => backdrop;

    private void Start()
    {
        if (backdrop == null || backdrop.Shots.Count == 0)
        {
            return;
        }

        frame = CodeUI.Box("Living Picture", transform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        frame.SetAsFirstSibling();
        var cover = frame.gameObject.AddComponent<ArtCover>();
        cover.AspectRatio = FrameAspect;
        cover.MaxCrop = WidestCrop;
        frame.gameObject.AddComponent<RectMask2D>();
        foreach (CinematicLayer layer in backdrop.Shots[0].layers)
        {
            RectTransform rect = CodeUI.Box(string.IsNullOrEmpty(layer.name) ? "Layer" : layer.name, frame, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var drawn = new Drawn { Layer = layer, Rect = rect };
            drawn.Graphic = CinematicScreen.DrawLayer(layer, rect, out drawn.Motes);
            layers.Add(drawn);
        }

        if (stillPicture != null)
        {
            stillPicture.enabled = false;
        }

        Show();
    }

    private void Update()
    {
        if (frame == null)
        {
            return;
        }

        // The menu runs unpaused, but a cinematic played over it (New Game) may pause the game: the picture goes on underneath.
        time += Time.unscaledDeltaTime;
        Show();
    }

    private void Show()
    {
        CinematicShot shot = backdrop.Shots[0];
        Vector2 size = frame.rect.size;
        lean = Vector2.Lerp(lean, PointerShare(), 1f - Mathf.Exp(-Time.unscaledDeltaTime / leanSeconds));
        CinematicPose pose = PoseAt(shot.cameraFrom, shot.cameraTo, time, swaySeconds);
        pose.position += Vector2.Scale(lean, pointerLean);
        float loopSeconds = LoopSeconds;
        float loop = Mathf.Repeat(time / loopSeconds, 1f);
        // Drift sways once a loop: at its fastest it moves at the layer's Drift.
        float sway = Mathf.Sin(loop * Mathf.PI * 2f) * loopSeconds / (Mathf.PI * 2f);
        foreach (Drawn drawn in layers)
        {
            CinematicCamera.Place(drawn.Layer, pose, out Vector2 centre, out float scale);
            centre += drawn.Layer.drift * sway;
            drawn.Rect.anchoredPosition = Vector2.Scale(centre, size);
            drawn.Rect.sizeDelta = drawn.Layer.round ? new Vector2(size.y, size.y) * scale : size * scale;
            Color tint = drawn.Layer.tint;
            tint.a *= Mathf.Clamp01(drawn.Layer.opacity.Evaluate(loop)) * CinematicScreen.Flicker(drawn.Layer, time);
            drawn.Graphic.color = tint;
            if (drawn.Motes != null)
            {
                drawn.Motes.SetTime(time);
            }
        }
    }

    /// <summary>
    /// The camera at <paramref name="seconds"/>: from <paramref name="from"/> to <paramref name="to"/> over <paramref name="sway"/>
    /// seconds and back, easing at both ends so it never stops with a jolt. Pure, for tests.
    /// </summary>
    public static CinematicPose PoseAt(CinematicPose from, CinematicPose to, float seconds, float sway)
    {
        float there = Mathf.PingPong(seconds / Mathf.Max(0.01f, sway), 1f);
        return CinematicPose.Lerp(from, to, there * there * (3f - 2f * there));
    }

    // Where the pointer is, -1..1 from the screen's middle; nothing while a pad is used (the cursor stands still there).
    private static Vector2 PointerShare()
    {
        if (GameInput.UsingGamepad || Screen.width <= 0 || Screen.height <= 0)
        {
            return Vector2.zero;
        }

        Vector2 pointer = GameInput.PointerPosition;
        return new Vector2(Mathf.Clamp(pointer.x / Screen.width * 2f - 1f, -1f, 1f), Mathf.Clamp(pointer.y / Screen.height * 2f - 1f, -1f, 1f));
    }
}
