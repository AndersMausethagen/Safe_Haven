using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Keeps what a rebuild didn't change. The World Builder makes a feature's objects afresh beside the old ones, then each
/// fresh object that came out the same as an old one is dropped for the old one: a prefab instance with the same prefab,
/// name and overrides (its save id aside), or a plain container (a GameObject with nothing but its Transform) whose
/// contents are kept the same way. So a rebuild changes only what changed: unchanged objects keep their place in the
/// scene file (the same file ids, so a diff shows the real change instead of every object renumbered) and their save
/// ids (a save's felled trees and mined rocks still find them). Anything else (procedural meshes, water strips) is
/// replaced as before. All steps join the build's undo group.
/// </summary>
public static class GeneratedObjects
{
    private const string SaveIdPath = "id";

    /// <summary>What a reconcile did.</summary>
    public struct Result
    {
        public int Kept;
        public int Added;
        public int Removed;

        public override string ToString() => $"{Kept} kept, {Added} new, {Removed} gone";
    }

    /// <summary>
    /// Brings <paramref name="old"/> in line with <paramref name="fresh"/>: what is new or changed moves from fresh into
    /// old, what fresh no longer has leaves old, the rest of old stays as it is; fresh is destroyed. The two roots must
    /// sit at the same local position, rotation and scale (<see cref="CanReconcile"/>).
    /// </summary>
    public static Result Reconcile(Transform old, Transform fresh)
    {
        var result = new Result();
        Merge(old, fresh, ref result);
        Undo.DestroyObjectImmediate(fresh.gameObject);
        return result;
    }

    /// <summary>True when fresh children can be moved under <paramref name="old"/> without moving in the world.</summary>
    public static bool CanReconcile(Transform old, Transform fresh) =>
        old.localPosition == fresh.localPosition && old.localRotation == fresh.localRotation && old.localScale == fresh.localScale;

    private static void Merge(Transform old, Transform fresh, ref Result result)
    {
        // The old children by what they are, in order, each to be kept at most once.
        var waiting = new Dictionary<string, Queue<Transform>>();
        List<Transform> oldChildren = Children(old);
        foreach (Transform child in oldChildren)
        {
            string key = Signature(child);
            if (key == null)
            {
                continue;
            }

            if (!waiting.TryGetValue(key, out Queue<Transform> same))
            {
                waiting[key] = same = new Queue<Transform>();
            }

            same.Enqueue(child);
        }

        var kept = new HashSet<Transform>();
        var order = new List<Transform>();
        foreach (Transform child in Children(fresh))
        {
            string key = Signature(child);
            if (key != null && waiting.TryGetValue(key, out Queue<Transform> same) && same.Count > 0)
            {
                Transform match = same.Dequeue();
                kept.Add(match);
                order.Add(match);
                if (IsContainer(child))
                {
                    Merge(match, child, ref result);
                }
                else
                {
                    result.Kept++;
                }

                Undo.DestroyObjectImmediate(child.gameObject);
            }
            else
            {
                Undo.SetTransformParent(child, old, false, "Build World");
                order.Add(child);
                result.Added++;
            }
        }

        foreach (Transform child in oldChildren)
        {
            if (!kept.Contains(child))
            {
                result.Removed++;
                Undo.DestroyObjectImmediate(child.gameObject);
            }
        }

        for (int i = 0; i < order.Count; i++)
        {
            if (order[i].GetSiblingIndex() != i)
            {
                Undo.SetSiblingIndex(order[i], i, "Build World");
            }
        }
    }

    private static List<Transform> Children(Transform parent)
    {
        var children = new List<Transform>(parent.childCount);
        foreach (Transform child in parent)
        {
            children.Add(child);
        }

        return children;
    }

    private static bool IsContainer(Transform transform) =>
        !PrefabUtility.IsPartOfPrefabInstance(transform.gameObject) && transform.GetComponents<Component>().Length == 1;

    /// <summary>What an object is, for telling a fresh one from an old one; null when it can't be told and is always replaced.</summary>
    public static string Signature(Transform transform)
    {
        GameObject go = transform.gameObject;
        var key = new StringBuilder();
        key.Append(go.name).Append('|').Append(go.activeSelf).Append('|').Append(go.tag).Append('|').Append(go.layer).Append('|');
        if (IsContainer(transform))
        {
            key.Append("container|");
            AppendVector(key, transform.localPosition);
            AppendVector(key, transform.localEulerAngles);
            AppendVector(key, transform.localScale);
            return key.ToString();
        }

        if (!PrefabUtility.IsOutermostPrefabInstanceRoot(go) || PrefabUtility.GetAddedGameObjects(go).Count > 0
            || PrefabUtility.GetAddedComponents(go).Count > 0 || PrefabUtility.GetRemovedComponents(go).Count > 0)
        {
            return null;
        }

        string prefab = AssetDatabase.AssetPathToGUID(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go));
        if (string.IsNullOrEmpty(prefab))
        {
            return null;
        }

        key.Append("prefab|").Append(prefab).Append('|');
        // Builders set positions and sizes from code, which an instance only lists as overrides once they are recorded.
        foreach (Component component in go.GetComponentsInChildren<Component>(true))
        {
            if (component != null)
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }
        }

        PropertyModification[] modifications = PrefabUtility.GetPropertyModifications(go);
        if (modifications == null)
        {
            return key.ToString();
        }

        var parts = new List<string>(modifications.Length);
        foreach (PropertyModification modification in modifications)
        {
            if (modification.target is SaveableEntity && modification.propertyPath == SaveIdPath)
            {
                continue; // made new for each fresh copy; the old one's is the one to keep
            }

            if (modification.target == null || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(modification.target, out string targetGuid, out long targetId))
            {
                return null;
            }

            string reference = "";
            if (modification.objectReference != null)
            {
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(modification.objectReference, out string guid, out long id))
                {
                    return null; // a reference into the scene: can't be compared, so it is made again
                }

                reference = guid + ":" + id;
            }

            parts.Add($"{targetGuid}:{targetId}/{modification.propertyPath}={modification.value}>{reference}");
        }

        parts.Sort(System.StringComparer.Ordinal);
        key.Append(string.Join(";", parts));
        return key.ToString();
    }

    private static void AppendVector(StringBuilder key, Vector3 value) =>
        key.Append(value.x.ToString("R")).Append(',').Append(value.y.ToString("R")).Append(',').Append(value.z.ToString("R")).Append('|');
}
