using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// One manifest entry compared with what Unity holds right now. Unity is the source of truth: a plan only ever creates new
/// assets or sets the fields the planner owns on existing ones. It never deletes, moves or replaces anything.
/// </summary>
public sealed class PlannerPlanEntry
{
    public enum Kind { New, Changed, Unchanged, Error }

    public PlannerEntity Entity;
    public Object Asset;
    public Kind State;
    public bool Apply = true;
    public readonly List<FieldChange> Changes = new List<FieldChange>();
    public readonly List<string> Errors = new List<string>();

    public struct FieldChange
    {
        public PlannerField Field;
        public string Old;
        public string New;
    }
}

/// <summary>
/// Reads a planner manifest, works out what is new and what changed, and applies it with SerializedObject so GUIDs,
/// references and fields the planner does not know about are left alone. Planning has no side effects; show the plan to the
/// user and call Apply only after they confirm.
/// </summary>
public static class PlannerImporter
{
    private static readonly Dictionary<string, Type> KindTypes = new Dictionary<string, Type>
    {
        { "item", typeof(ItemDefinition) },
        { "resource", typeof(ResourceDefinition) },
        { "building", typeof(BuildingDefinition) },
        { "recipe", typeof(RecipeDefinition) },
        { "node", typeof(GatheringNodeDefinition) },
    };

    public static PlannerManifest Parse(string json)
    {
        var manifest = JsonUtility.FromJson<PlannerManifest>(json);
        if (manifest == null || manifest.version != 1)
        {
            throw new InvalidDataException("Not a planner manifest, or an unsupported version.");
        }

        return manifest;
    }

    /// <summary>Every definition of the planner's kinds in the project, for matching manifest entries.</summary>
    public static List<Object> LoadProjectAssets()
    {
        var assets = new List<Object>();
        foreach (Type type in KindTypes.Values)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:" + type.Name))
            {
                Object asset = AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(guid), type);
                if (asset != null)
                {
                    assets.Add(asset);
                }
            }
        }

        return assets;
    }

    public static string KeyOf(Object asset)
    {
        switch (asset)
        {
            case ItemDefinition item: return "item:" + item.Id;
            case ResourceDefinition resource: return "resource:" + resource.Id;
            case BuildingDefinition building: return "building:" + building.Id;
            case RecipeDefinition recipe: return "recipe:" + recipe.Id;
            case GatheringNodeDefinition node: return "node:" + node.Id;
            default: return null;
        }
    }

    public static List<PlannerPlanEntry> Plan(PlannerManifest manifest, IEnumerable<Object> projectAssets = null)
    {
        var index = new Dictionary<string, Object>();
        foreach (Object asset in projectAssets ?? LoadProjectAssets())
        {
            string key = KeyOf(asset);
            if (key != null && !index.ContainsKey(key))
            {
                index[key] = asset;
            }
        }

        var willExist = new HashSet<string>(index.Keys);
        foreach (PlannerEntity entity in manifest.entities)
        {
            willExist.Add(entity.key);
        }

        var plan = new List<PlannerPlanEntry>();
        foreach (PlannerEntity entity in manifest.entities)
        {
            var entry = new PlannerPlanEntry { Entity = entity, Asset = Find(entity, index) };
            if (!KindTypes.ContainsKey(entity.kind))
            {
                entry.Errors.Add("Unknown kind '" + entity.kind + "'.");
            }
            else if (entry.Asset == null && !IsValidNewPath(entity.path))
            {
                entry.Errors.Add("Invalid asset path '" + entity.path + "'.");
            }

            if (entry.Errors.Count == 0)
            {
                bool creating = entry.Asset == null;
                var so = creating ? null : new SerializedObject(entry.Asset);
                foreach (PlannerField field in entity.fields)
                {
                    if (field.createOnly && !creating)
                    {
                        continue;
                    }

                    CheckReferences(field, willExist, entry.Errors);
                    string wanted = Canonical(field);
                    string current = creating ? string.Empty : CanonicalFromAsset(so, field, index);
                    if (creating || wanted != current)
                    {
                        entry.Changes.Add(new PlannerPlanEntry.FieldChange { Field = field, Old = creating ? null : current, New = wanted });
                    }
                }
            }

            entry.State = entry.Errors.Count > 0 ? PlannerPlanEntry.Kind.Error
                : entry.Asset == null ? PlannerPlanEntry.Kind.New
                : entry.Changes.Count > 0 ? PlannerPlanEntry.Kind.Changed
                : PlannerPlanEntry.Kind.Unchanged;
            entry.Apply = entry.State == PlannerPlanEntry.Kind.New || entry.State == PlannerPlanEntry.Kind.Changed;
            plan.Add(entry);
        }

        return plan;
    }

    /// <summary>
    /// Applies the ticked entries: creates new assets first (so entries can refer to each other), then sets fields. Returns a
    /// summary. <paramref name="create"/> exists so tests can avoid writing files; by default new assets are saved to disk
    /// at a path that is never an existing file.
    /// </summary>
    public static string Apply(IList<PlannerPlanEntry> plan, Func<Type, string, Object> create = null)
    {
        create = create ?? CreateAssetOnDisk;
        var index = new Dictionary<string, Object>();
        foreach (Object asset in LoadProjectAssets())
        {
            string key = KeyOf(asset);
            if (key != null && !index.ContainsKey(key))
            {
                index[key] = asset;
            }
        }

        foreach (PlannerPlanEntry entry in plan)
        {
            if (entry.Apply && entry.State == PlannerPlanEntry.Kind.New)
            {
                entry.Asset = create(KindTypes[entry.Entity.kind], entry.Entity.path);
                var so = new SerializedObject(entry.Asset);
                so.FindProperty("id").stringValue = entry.Entity.id;
                so.ApplyModifiedPropertiesWithoutUndo();
                index[entry.Entity.key] = entry.Asset;
            }
        }

        int created = 0, updated = 0;
        foreach (PlannerPlanEntry entry in plan)
        {
            if (!entry.Apply || entry.Asset == null || (entry.State != PlannerPlanEntry.Kind.New && entry.State != PlannerPlanEntry.Kind.Changed))
            {
                continue;
            }

            var so = new SerializedObject(entry.Asset);
            foreach (PlannerPlanEntry.FieldChange change in entry.Changes)
            {
                Write(so, change.Field, index);
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(entry.Asset);
            if (entry.State == PlannerPlanEntry.Kind.New)
            {
                created++;
            }
            else
            {
                updated++;
            }
        }

        AssetDatabase.SaveAssets();
        return $"Created {created} and updated {updated} assets. Nothing was deleted.";
    }

    private static Object CreateAssetOnDisk(Type type, string path)
    {
        string unique = AssetDatabase.GenerateUniqueAssetPath(path);
        EnsureFolder(Path.GetDirectoryName(unique).Replace('\\', '/'));
        var asset = ScriptableObject.CreateInstance(type);
        AssetDatabase.CreateAsset(asset, unique);
        return asset;
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
        {
            return;
        }

        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }

    private static bool IsValidNewPath(string path)
    {
        return !string.IsNullOrEmpty(path) && path.StartsWith("Assets/", StringComparison.Ordinal)
            && path.EndsWith(".asset", StringComparison.Ordinal) && !path.Contains("..");
    }

    private static Object Find(PlannerEntity entity, Dictionary<string, Object> index)
    {
        if (!string.IsNullOrEmpty(entity.guid) && KindTypes.TryGetValue(entity.kind, out Type type))
        {
            string path = AssetDatabase.GUIDToAssetPath(entity.guid);
            Object byGuid = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath(path, type);
            if (byGuid != null)
            {
                return byGuid;
            }
        }

        return index.TryGetValue(entity.key, out Object byKey) ? byKey : null;
    }

    private static void CheckReferences(PlannerField field, HashSet<string> willExist, List<string> errors)
    {
        if (field.ftype == "ref" && !string.IsNullOrEmpty(field.value) && !willExist.Contains(field.value))
        {
            errors.Add($"{field.name} refers to {field.value}, which does not exist.");
        }

        foreach (PlannerLine line in field.lines)
        {
            if (!willExist.Contains(line.target))
            {
                errors.Add($"{field.name} refers to {line.target}, which does not exist.");
            }
        }
    }

    // ----- canonical text, identical for manifest values and asset values so they can be compared -----

    private static string Float(float value) => Math.Round(value, 4).ToString("0.####", CultureInfo.InvariantCulture);

    private static string Canonical(PlannerField f)
    {
        switch (f.ftype)
        {
            case "bool": return f.value == "true" ? "true" : "false";
            case "int":
            case "enum": return int.Parse(string.IsNullOrEmpty(f.value) ? "0" : f.value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
            case "float": return Float(float.Parse(string.IsNullOrEmpty(f.value) ? "0" : f.value, CultureInfo.InvariantCulture));
            case "amounts": return Join(f.lines, l => $"{l.target}*{l.amount}");
            case "drops": return Join(f.lines, l => $"{l.target}*{l.min}-{l.max}@{Math.Round(l.prob, 3).ToString("0.###", CultureInfo.InvariantCulture)}");
            default: return (f.value ?? string.Empty).Trim();
        }
    }

    private static string Join(List<PlannerLine> lines, Func<PlannerLine, string> format)
    {
        var parts = new List<string>();
        foreach (PlannerLine line in lines)
        {
            parts.Add(format(line));
        }

        return string.Join(";", parts);
    }

    private static string PathOf(PlannerField f) => f.name == "yields" ? "yields.entries" : f.name;

    private static string CanonicalFromAsset(SerializedObject so, PlannerField f, Dictionary<string, Object> index)
    {
        SerializedProperty p = so.FindProperty(PathOf(f));
        if (p == null)
        {
            return "<missing field>";
        }

        switch (f.ftype)
        {
            case "bool": return p.boolValue ? "true" : "false";
            case "int": return p.intValue.ToString(CultureInfo.InvariantCulture);
            case "enum": return p.enumValueFlag.ToString(CultureInfo.InvariantCulture);
            case "float": return Float(p.floatValue);
            case "ref": return KeyOf(p.objectReferenceValue) ?? string.Empty;
            case "lookup": return p.objectReferenceValue != null ? p.objectReferenceValue.name : string.Empty;
            case "amounts":
            {
                var parts = new List<string>();
                for (int i = 0; i < p.arraySize; i++)
                {
                    SerializedProperty e = p.GetArrayElementAtIndex(i);
                    Object target = (e.FindPropertyRelative("item") ?? e.FindPropertyRelative("resource")).objectReferenceValue;
                    parts.Add($"{KeyOf(target)}*{e.FindPropertyRelative("amount").intValue}");
                }

                return string.Join(";", parts);
            }
            case "drops":
            {
                var parts = new List<string>();
                for (int i = 0; i < p.arraySize; i++)
                {
                    SerializedProperty e = p.GetArrayElementAtIndex(i);
                    Object target = e.FindPropertyRelative("item").objectReferenceValue ?? e.FindPropertyRelative("resource").objectReferenceValue;
                    parts.Add($"{KeyOf(target)}*{e.FindPropertyRelative("minQuantity").intValue}-{e.FindPropertyRelative("maxQuantity").intValue}" +
                              $"@{Math.Round(e.FindPropertyRelative("probability").floatValue, 3).ToString("0.###", CultureInfo.InvariantCulture)}");
                }

                return string.Join(";", parts);
            }
            default: return (p.stringValue ?? string.Empty).Trim();
        }
    }

    private static void Write(SerializedObject so, PlannerField f, Dictionary<string, Object> index)
    {
        SerializedProperty p = so.FindProperty(PathOf(f));
        if (p == null)
        {
            Debug.LogWarning($"Planner import: {so.targetObject.name} has no field '{f.name}'.");
            return;
        }

        switch (f.ftype)
        {
            case "bool": p.boolValue = f.value == "true"; break;
            case "int": p.intValue = int.Parse(f.value, CultureInfo.InvariantCulture); break;
            case "enum": p.enumValueFlag = int.Parse(f.value, CultureInfo.InvariantCulture); break;
            case "float": p.floatValue = float.Parse(f.value, CultureInfo.InvariantCulture); break;
            case "ref": p.objectReferenceValue = string.IsNullOrEmpty(f.value) ? null : index[f.value]; break;
            case "lookup": p.objectReferenceValue = FindByName(typeof(ResourceCategory), f.value); break;
            case "amounts":
                p.arraySize = f.lines.Count;
                for (int i = 0; i < f.lines.Count; i++)
                {
                    SerializedProperty e = p.GetArrayElementAtIndex(i);
                    (e.FindPropertyRelative("item") ?? e.FindPropertyRelative("resource")).objectReferenceValue = index[f.lines[i].target];
                    e.FindPropertyRelative("amount").intValue = f.lines[i].amount;
                }

                break;
            case "drops":
                p.arraySize = f.lines.Count;
                for (int i = 0; i < f.lines.Count; i++)
                {
                    SerializedProperty e = p.GetArrayElementAtIndex(i);
                    Object target = index[f.lines[i].target];
                    e.FindPropertyRelative("item").objectReferenceValue = target is ItemDefinition ? target : null;
                    e.FindPropertyRelative("resource").objectReferenceValue = target is ResourceDefinition ? target : null;
                    e.FindPropertyRelative("minQuantity").intValue = f.lines[i].min;
                    e.FindPropertyRelative("maxQuantity").intValue = f.lines[i].max;
                    e.FindPropertyRelative("probability").floatValue = f.lines[i].prob;
                }

                break;
            default: p.stringValue = f.value ?? string.Empty; break;
        }
    }

    private static Object FindByName(Type type, string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        foreach (string guid in AssetDatabase.FindAssets("t:" + type.Name))
        {
            Object asset = AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(guid), type);
            if (asset != null && asset.name == name)
            {
                return asset;
            }
        }

        return null;
    }
}
