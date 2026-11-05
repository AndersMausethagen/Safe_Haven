using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Grows a tessellated mesh's bounds by the most its displacement can push the surface out, so it isn't culled while
/// part of it still shows. The padding is in world metres, so it only changes with the object's scale: thousands of
/// rocks and logs carry this, and none of them has a per-frame callback. One static ticker rechecks a slice of them each
/// frame and pads again only those whose scale changed (a node shrinking as it breaks); in the editor a slice is padded
/// again in full each update, so tuned materials show within moments.
/// </summary>
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(MeshRenderer))]
public sealed class MeshSurface : MonoBehaviour
{
    public const string ShaderName = "Safe Haven/Mesh Surface (Tessellated)";
    private const int CheckedPerFrame = 256;

    private static readonly List<MeshSurface> all = new List<MeshSurface>();
    private static readonly Dictionary<Material, float> reachOf = new Dictionary<Material, float>();
    private static readonly List<Material> materials = new List<Material>();
    private static int nextChecked;
    private static int tickedFrame = -1;

    private MeshRenderer target;
    private Bounds originalBounds;
    private Vector3 paddedFor;
    private int slot = -1;

    private void OnEnable()
    {
        target = GetComponent<MeshRenderer>();
        originalBounds = target.localBounds;
        slot = all.Count;
        all.Add(this);
        Refresh();
    }

    private void OnDisable()
    {
        if (slot >= 0 && slot < all.Count && all[slot] == this)
        {
            int last = all.Count - 1;
            all[slot] = all[last];
            all[slot].slot = slot;
            all.RemoveAt(last);
        }

        slot = -1;
        if (target != null) target.localBounds = originalBounds;
    }

    /// <summary>Pads the bounds again now (after changing the object's materials or scale from code).</summary>
    public void Refresh()
    {
        if (target == null) return;
        target.GetSharedMaterials(materials);
        float distance = 0;
        foreach (Material material in materials)
        {
            distance = Mathf.Max(distance, Reach(material));
        }

        Matrix4x4 inverse = transform.worldToLocalMatrix;
        var padding = new Vector3(
            new Vector3(inverse.m00, inverse.m01, inverse.m02).magnitude,
            new Vector3(inverse.m10, inverse.m11, inverse.m12).magnitude,
            new Vector3(inverse.m20, inverse.m21, inverse.m22).magnitude) * distance;
        Bounds expanded = originalBounds;
        expanded.Expand(padding * 2);
        target.localBounds = expanded;
        paddedFor = transform.lossyScale;
    }

    // How far a material's displacement can push the surface (metres). Read once per material while playing; the
    // editor reads it every time, as it is being tuned.
    private static float Reach(Material material)
    {
        if (material == null) return 0;
        if (Application.isPlaying && reachOf.TryGetValue(material, out float known)) return known;
        float reach = 0;
        if (material.shader != null && material.shader.name == ShaderName)
        {
            float midpoint = material.GetFloat("_HeightMidpoint");
            reach = Mathf.Abs(material.GetFloat("_HeightStrength")) * Mathf.Max(midpoint, 1 - midpoint);
        }

        if (Application.isPlaying) reachOf[material] = reach;
        return reach;
    }

    // While playing: a slice of the surfaces each frame, padded again only if their scale changed.
    private static void Tick()
    {
        if (tickedFrame == Time.frameCount || all.Count == 0) return;
        tickedFrame = Time.frameCount;
        int count = Mathf.Min(CheckedPerFrame, all.Count);
        for (int i = 0; i < count; i++)
        {
            nextChecked = (nextChecked + 1) % all.Count;
            MeshSurface surface = all[nextChecked];
            if (surface != null && surface.transform.lossyScale != surface.paddedFor) surface.Refresh();
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Hook()
    {
        reachOf.Clear();
        nextChecked = 0;
        tickedFrame = -1;
        Application.onBeforeRender -= Tick;
        Application.onBeforeRender += Tick;
    }

#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
    private static void HookEditor()
    {
        UnityEditor.EditorApplication.update -= EditorTick;
        UnityEditor.EditorApplication.update += EditorTick;
    }

    // Out of play mode the materials may be being tuned, so each is padded again in full, a slice per editor update.
    private static void EditorTick()
    {
        if (Application.isPlaying || all.Count == 0) return;
        int count = Mathf.Min(CheckedPerFrame, all.Count);
        for (int i = 0; i < count; i++)
        {
            nextChecked = (nextChecked + 1) % all.Count;
            if (all[nextChecked] != null) all[nextChecked].Refresh();
        }
    }
#endif
}
