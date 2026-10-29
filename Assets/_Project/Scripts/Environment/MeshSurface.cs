using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(MeshRenderer))]
public sealed class MeshSurface : MonoBehaviour
{
    public const string ShaderName = "Safe Haven/Mesh Surface (Tessellated)";
    private readonly List<Material> materials = new List<Material>();
    private MeshRenderer target;
    private Bounds originalBounds;

    private void OnEnable()
    {
        target = GetComponent<MeshRenderer>();
        originalBounds = target.localBounds;
        UpdateBounds();
    }

    private void LateUpdate() => UpdateBounds();

    private void UpdateBounds()
    {
        if (target == null) return;
        target.GetSharedMaterials(materials);
        float distance = 0;
        foreach (Material material in materials)
        {
            if (material == null || material.shader == null || material.shader.name != ShaderName) continue;
            float midpoint = material.GetFloat("_HeightMidpoint");
            distance = Mathf.Max(distance, Mathf.Abs(material.GetFloat("_HeightStrength")) * Mathf.Max(midpoint, 1 - midpoint));
        }
        Matrix4x4 inverse = transform.worldToLocalMatrix;
        var padding = new Vector3(
            new Vector3(inverse.m00, inverse.m01, inverse.m02).magnitude,
            new Vector3(inverse.m10, inverse.m11, inverse.m12).magnitude,
            new Vector3(inverse.m20, inverse.m21, inverse.m22).magnitude) * distance;
        Bounds expanded = originalBounds;
        expanded.Expand(padding * 2);
        target.localBounds = expanded;
    }

    private void OnDisable()
    {
        if (target != null) target.localBounds = originalBounds;
    }
}
