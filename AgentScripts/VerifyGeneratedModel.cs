using System;
using UnityEditor;
using UnityEngine;

public static class VerifyGeneratedModel
{
    public static object Run()
    {
        const string path = "Assets/_Project/Models/Generated/axe_fix_00001.glb";
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (model == null)
            throw new InvalidOperationException("Generated GLB did not import as a GameObject.");

        var filters = model.GetComponentsInChildren<MeshFilter>(true);
        var renderers = model.GetComponentsInChildren<Renderer>(true);
        if (filters.Length == 0 || renderers.Length == 0)
            throw new InvalidOperationException("Generated GLB has no rendered mesh.");

        var material = renderers[0].sharedMaterial;
        if (material == null || material.mainTexture == null)
            throw new InvalidOperationException("Generated GLB has no textured material.");

        return new
        {
            path,
            meshes = filters.Length,
            triangles = filters[0].sharedMesh.triangles.Length / 3,
            shader = material.shader.name,
            textured = true
        };
    }
}
