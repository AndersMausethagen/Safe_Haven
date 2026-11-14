using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The Haven's buildings at the size they read best from above (the user, 2026-11-14: "at least halve the size of the
/// buildings: many of them are far too big, and above all hard to read for a top-down game"). Sets each big building's
/// BuildingDefinition.ModelScale and applies every definition's scale to its prefab
/// (`unity command script run --file AgentScripts/BuildBuildingScale.cs --entry BuildBuildingScale.Apply`).
///
/// The model child is scaled, not the root: a building's footprint, yard, snapping, work spot and scaffold are measured
/// in the root's space (BuildPlacement.LocalBounds), so they follow a scaled child by themselves and would ignore a
/// scaled root. What sits in the building's space on the root moves with the model by the same ratio: the chimney's
/// smoke and the window's glow (HearthSmoke), the washing line, a stable's stalls and door. Then the collider,
/// navigation obstacle and health bar are fitted to the model again. The yard (people's ground in front) keeps its size.
/// BuildStructureModels and BuildHorses read ModelScale too, so running them again keeps the size. Re-runnable: a
/// building already at its scale is left alone.
/// </summary>
public static class BuildBuildingScale
{
    // Half size across for the big buildings, and a little taller than half (the user: "a bit low now"); less small where
    // horses or carts have to fit under the roof or in the yard. The stations (campfire, cauldron, furnace, kitchen), small
    // structures and the dock (the boat lies beside it) keep 1.
    private static readonly (string building, float scale, float height)[] Scales =
    {
        ("ConstructionTable", 0.5f, 1.3f), ("Forge", 0.5f, 1.3f), ("Sawbench", 0.5f, 1.3f), ("TanningRack", 0.5f, 1.3f),
        ("Loom", 0.5f, 1.3f), ("TradingPost", 0.5f, 1.3f), ("Cottage", 0.5f, 1.3f), ("Hut", 0.5f, 1.3f), ("Sauna", 0.5f, 1.3f),
        ("Stable", 0.7f, 1.15f), ("WainwrightsYard", 0.7f, 1.15f),
    };

    // Children of a building's root that stand outside its body: kept out of the collider while it is fitted.
    private static readonly string[] OutsideTheBody = { "Lived In" };

    public static string Apply()
    {
        var report = new List<string>();
        foreach ((string building, float scale, float height) in Scales)
        {
            var definition = AssetDatabase.LoadAssetAtPath<BuildingDefinition>($"Assets/_Project/Data/Buildings/{building}.asset");
            if (definition == null)
            {
                report.Add($"{building}: no definition");
                continue;
            }

            var data = new SerializedObject(definition);
            data.FindProperty("modelScale").floatValue = scale;
            data.FindProperty("modelHeight").floatValue = height;
            if (data.ApplyModifiedPropertiesWithoutUndo())
            {
                AssetDatabase.SaveAssetIfDirty(definition);
            }
        }

        foreach (string guid in AssetDatabase.FindAssets("t:BuildingDefinition", new[] { "Assets/_Project/Data/Buildings" }))
        {
            var definition = AssetDatabase.LoadAssetAtPath<BuildingDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (definition != null && definition.Prefab != null)
            {
                string line = ApplyTo(definition);
                if (line != null)
                {
                    report.Add(line);
                }
            }
        }

        AssetDatabase.SaveAssets();
        return report.Count > 0 ? string.Join("\n", report) : "every building already at its scale";
    }

    /// <summary>Scales the prefab's model to the definition's ModelSize; null when it is already there.</summary>
    public static string ApplyTo(BuildingDefinition definition)
    {
        string path = AssetDatabase.GetAssetPath(definition.Prefab);
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            // Only models this step sizes: ours are as wide as they are deep (a stand-in's model child may carry a shape of its
            // own, a wall's long box, which is not a scale to undo).
            Transform model = ModelChild(root.transform);
            Vector3 target = definition.ModelSize;
            bool shown = target != Vector3.one;
            Vector3 now = model != null ? model.localScale : Vector3.one;
            bool ours = Mathf.Approximately(now.x, now.z);
            if (model == null)
            {
                return shown ? $"{definition.name}: no model child to scale" : null;
            }

            if (!ours)
            {
                return shown ? $"{definition.name}: its model child has a shape of its own; left alone" : null;
            }

            var ratio = new Vector3(target.x / Mathf.Max(0.0001f, now.x), target.y / Mathf.Max(0.0001f, now.y), target.z / Mathf.Max(0.0001f, now.z));
            if ((ratio - Vector3.one).sqrMagnitude < 0.000001f)
            {
                return null;
            }

            model.localScale = target;
            foreach (Transform child in root.transform)
            {
                if (child != model && child.name != "HealthBar" && !child.name.StartsWith("Wall Torch")) // torches: BuildWallTorches puts them back
                {
                    child.localPosition = Vector3.Scale(child.localPosition, ratio);
                    child.localScale = Vector3.Scale(child.localScale, ratio);
                }
            }

            if (root.TryGetComponent(out HearthSmoke smoke))
            {
                var data = new SerializedObject(smoke);
                data.FindProperty("chimney").vector3Value = Vector3.Scale(data.FindProperty("chimney").vector3Value, ratio);
                data.FindProperty("lightAt").vector3Value = Vector3.Scale(data.FindProperty("lightAt").vector3Value, ratio);
                data.ApplyModifiedPropertiesWithoutUndo();
            }

            // Fitted to the body alone, as BuildStructureModels does: the washing line isn't part of the house.
            var aside = new List<Transform>();
            foreach (string name in OutsideTheBody)
            {
                Transform part = root.transform.Find(name);
                if (part != null)
                {
                    part.SetParent(null, false);
                    aside.Add(part);
                }
            }

            PrefabBuildUtility.FitToModel(root);
            foreach (Transform part in aside)
            {
                part.SetParent(root.transform, false);
            }

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Bounds body = BuildPlacement.LocalBounds(root.transform);
            return $"{definition.name}: x{target.x:0.##} (height x{target.y:0.##}) -> {body.size.x:0.0} x {body.size.z:0.0} x {body.size.y:0.0} m";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // The child holding the building's model: "Model", or "<Name> Model" (the kit buildings').
    private static Transform ModelChild(Transform root)
    {
        foreach (Transform child in root)
        {
            if (child.name == "Model" || child.name.EndsWith(" Model"))
            {
                return child;
            }
        }

        return null;
    }
}
