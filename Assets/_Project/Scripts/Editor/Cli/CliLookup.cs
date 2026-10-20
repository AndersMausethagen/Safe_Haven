using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Shared helpers for the Safe Haven CLI commands: finding content by a loose key (id, display name or asset name),
/// resolving content types from short aliases, and printing serialized values compactly. Output is kept terse on
/// purpose; every character is read back by whoever called the command.
/// </summary>
internal static class CliLookup
{
    private const int MaxListedElements = 12;

    /// <summary>Short names for the content types, with the fields a listing shows when none are asked for.</summary>
    private static readonly Dictionary<string, (string type, string fields)> Aliases =
        new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            ["item"] = ("ItemDefinition", "category,tier,weight,maximumStack,value"),
            ["recipe"] = ("RecipeDefinition", "station,craftSeconds,itemCost,resourceCost,output,outputAmount"),
            ["node"] = ("GatheringNodeDefinition", "gatherTool,requiredToolTier,gatherSeconds,yields,respawnSeconds"),
            ["building"] = ("BuildingDefinition", "station,constructionItems,constructionCost,showInBuildMenu,knownFromStart"),
            ["quest"] = ("QuestDefinition", "chain,start,prerequisites,objectives,rewardItems"),
            ["monster"] = ("MonsterDefinition", "combatStats,attackDamage,attackCooldown,moveSpeed,drops"),
            ["raid"] = ("RaidDefinition", "sceneName,tier,locked,durationSeconds,lootMultiplier"),
            ["modifier"] = ("RaidModifierDefinition", "*"),
            ["essence"] = ("ResourceDefinition", "category,tier"),
        };

    public static string AliasList => string.Join(", ", Aliases.Keys);

    public static ContentDatabase Database => Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);

    /// <summary>Resolves "item", "recipe"... or any ScriptableObject class name. Throws with the choices when unknown.</summary>
    public static Type ResolveType(string key, out string defaultFields)
    {
        defaultFields = null;
        string typeName = key;
        if (Aliases.TryGetValue(key ?? "", out (string type, string fields) alias))
        {
            typeName = alias.type;
            defaultFields = alias.fields;
        }

        Type type = TypeCache.GetTypesDerivedFrom<ScriptableObject>()
            .FirstOrDefault(candidate => string.Equals(candidate.Name, typeName, StringComparison.OrdinalIgnoreCase));
        if (type == null)
        {
            throw new ArgumentException($"Unknown type '{key}'. Use one of: {AliasList}, or a ScriptableObject class name.");
        }

        return type;
    }

    public static List<Object> LoadAll(Type type)
    {
        var assets = new List<Object>();
        foreach (string guid in AssetDatabase.FindAssets("t:" + type.Name))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset != null && type.IsInstanceOfType(asset) && !assets.Contains(asset))
                {
                    assets.Add(asset);
                }
            }
        }

        assets.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
        return assets;
    }

    /// <summary>
    /// Finds one asset of T by exact id, display name or asset name (case-insensitive), else by a unique partial match.
    /// Returns null when nothing matches and throws when a partial key is ambiguous.
    /// </summary>
    public static T Find<T>(string key) where T : Object => (T)Find(typeof(T), key);

    public static Object Find(Type type, string key)
    {
        List<Object> exact = FindExact(type, key, out List<Object> partial);
        if (exact.Count > 0)
        {
            return exact[0];
        }

        if (partial.Count > 1)
        {
            throw new ArgumentException($"'{key}' matches several {type.Name}s: {string.Join(", ", partial.Take(10).Select(a => a.name))}");
        }

        return partial.Count == 1 ? partial[0] : null;
    }

    /// <summary>Every asset whose id, display name or asset name equals the key; partial holds the contains-matches.</summary>
    public static List<Object> FindExact(Type type, string key, out List<Object> partial)
    {
        var exact = new List<Object>();
        partial = new List<Object>();
        if (string.IsNullOrWhiteSpace(key))
        {
            return exact;
        }

        key = key.Trim();
        foreach (Object asset in LoadAll(type))
        {
            string id = ReadString(asset, "id");
            string display = ReadString(asset, "displayName");
            if (Same(asset.name, key) || Same(id, key) || Same(display, key))
            {
                exact.Add(asset);
            }
            else if (Contains(asset.name, key) || Contains(id, key) || Contains(display, key))
            {
                partial.Add(asset);
            }
        }

        return exact;
    }

    /// <summary>Like Find, but throws "No X matches 'key'" instead of returning null.</summary>
    public static T Require<T>(string key) where T : Object
    {
        T found = Find<T>(key);
        if (found == null)
        {
            throw new ArgumentException($"No {typeof(T).Name} matches '{key}'.");
        }

        return found;
    }

    public static bool Matches(Object asset, string filter)
    {
        return string.IsNullOrWhiteSpace(filter)
               || Contains(asset.name, filter)
               || Contains(ReadString(asset, "id"), filter)
               || Contains(ReadString(asset, "displayName"), filter);
    }

    /// <summary>"Name" or "Name(id)" when the id differs from the asset name.</summary>
    public static string Label(Object asset)
    {
        string id = ReadString(asset, "id");
        return string.IsNullOrWhiteSpace(id) || Same(id, asset.name) ? asset.name : $"{asset.name}({id})";
    }

    /// <summary>The requested top-level fields as "field=value" pairs; "*" means every visible field.</summary>
    public static string DescribeFields(Object asset, string fields)
    {
        var so = new SerializedObject(asset);
        var parts = new List<string>();
        if (fields == "*")
        {
            SerializedProperty it = so.GetIterator();
            it.NextVisible(true);
            while (it.NextVisible(false))
            {
                if (it.name != "id" && it.name != "displayName")
                {
                    parts.Add($"{it.name}={Format(it, 0)}");
                }
            }
        }
        else
        {
            foreach (string field in fields.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string name = field.Trim();
                SerializedProperty property = so.FindProperty(name);
                parts.Add(property == null ? $"{name}=?" : $"{name}={Format(property, 0)}");
            }
        }

        return string.Join("; ", parts);
    }

    /// <summary>A compact single-line rendering of a serialized value: "OakWood x5" for item/amount pairs, [a, b] for lists.</summary>
    public static string Format(SerializedProperty property, int depth)
    {
        switch (property.propertyType)
        {
            case SerializedPropertyType.ObjectReference:
                return property.objectReferenceValue != null ? property.objectReferenceValue.name : "-";
            case SerializedPropertyType.Integer:
                return property.intValue.ToString(CultureInfo.InvariantCulture);
            case SerializedPropertyType.Boolean:
                return property.boolValue ? "true" : "false";
            case SerializedPropertyType.Float:
                return property.floatValue.ToString("0.###", CultureInfo.InvariantCulture);
            case SerializedPropertyType.String:
                return Shorten(property.stringValue, 60);
            case SerializedPropertyType.Enum:
                int index = property.enumValueIndex;
                return index >= 0 && index < property.enumDisplayNames.Length
                    ? property.enumDisplayNames[index].Replace(" ", "")
                    : property.intValue.ToString(CultureInfo.InvariantCulture);
            case SerializedPropertyType.Color:
                return "#" + ColorUtility.ToHtmlStringRGBA(property.colorValue);
            case SerializedPropertyType.Vector2:
                return property.vector2Value.ToString();
            case SerializedPropertyType.Vector3:
                return property.vector3Value.ToString();
        }

        if (property.isArray)
        {
            if (property.arraySize == 0)
            {
                return "[]";
            }

            if (depth > 2)
            {
                return $"[{property.arraySize}]";
            }

            var elements = new List<string>();
            int shown = Mathf.Min(property.arraySize, MaxListedElements);
            for (int i = 0; i < shown; i++)
            {
                elements.Add(Format(property.GetArrayElementAtIndex(i), depth + 1));
            }

            string more = property.arraySize > shown ? $", +{property.arraySize - shown}" : "";
            return "[" + string.Join(", ", elements) + more + "]";
        }

        if (property.propertyType == SerializedPropertyType.Generic && property.hasVisibleChildren && depth <= 3)
        {
            return FormatStruct(property, depth);
        }

        return property.propertyType.ToString();
    }

    public static string Shorten(string text, int max)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "\"\"";
        }

        text = text.Replace("\n", " ").Replace("\r", "");
        return text.Length <= max ? text : text.Substring(0, max - 1) + "…";
    }

    /// <summary>Joins lines and appends "(+N more)" when the limit cut them short.</summary>
    public static string JoinLimited(IReadOnlyList<string> lines, int limit, string header = null)
    {
        var sb = new StringBuilder();
        if (header != null)
        {
            sb.AppendLine(header);
        }

        int shown = limit > 0 ? Mathf.Min(limit, lines.Count) : lines.Count;
        for (int i = 0; i < shown; i++)
        {
            sb.AppendLine(lines[i]);
        }

        if (shown < lines.Count)
        {
            sb.AppendLine($"(+{lines.Count - shown} more; raise --limit)");
        }

        return sb.ToString().TrimEnd();
    }

    private static string FormatStruct(SerializedProperty property, int depth)
    {
        // Drop tables and costs are usually {reference, amount(s)}: print them as "Name x3" or "Name 2-4".
        string reference = null;
        var numbers = new List<string>();
        var pairs = new List<string>();
        SerializedProperty child = property.Copy();
        SerializedProperty end = property.GetEndProperty();
        bool enter = true;
        while (child.NextVisible(enter) && !SerializedProperty.EqualContents(child, end))
        {
            enter = false;
            if (IsBlank(child))
            {
                continue;
            }

            string value = Format(child, depth + 1);
            if (child.propertyType == SerializedPropertyType.ObjectReference && reference == null)
            {
                reference = value;
                continue;
            }

            if (child.propertyType == SerializedPropertyType.Integer || child.propertyType == SerializedPropertyType.Float)
            {
                numbers.Add(value);
            }

            pairs.Add($"{child.name}={value}");
        }

        if (reference != null && pairs.Count == numbers.Count && numbers.Count > 0 && numbers.Count <= 2)
        {
            return numbers.Count == 1 ? $"{reference} x{numbers[0]}" : $"{reference} {numbers[0]}-{numbers[1]}";
        }

        if (reference != null)
        {
            pairs.Insert(0, reference);
        }

        return "{" + string.Join(" ", pairs) + "}";
    }

    /// <summary>Unset references, empty text and false flags inside a struct are noise in a one-line summary.</summary>
    private static bool IsBlank(SerializedProperty property)
    {
        switch (property.propertyType)
        {
            case SerializedPropertyType.ObjectReference:
                return property.objectReferenceValue == null;
            case SerializedPropertyType.String:
                return string.IsNullOrEmpty(property.stringValue);
            case SerializedPropertyType.Boolean:
                return !property.boolValue;
            default:
                return property.isArray && property.propertyType == SerializedPropertyType.Generic && property.arraySize == 0;
        }
    }

    private static string ReadString(Object asset, string field)
    {
        using var so = new SerializedObject(asset);
        SerializedProperty property = so.FindProperty(field);
        return property != null && property.propertyType == SerializedPropertyType.String ? property.stringValue : null;
    }

    private static bool Same(string a, string b) =>
        !string.IsNullOrEmpty(a) && string.Equals(a.Trim(), b, StringComparison.OrdinalIgnoreCase);

    private static bool Contains(string text, string part) =>
        !string.IsNullOrEmpty(text) && text.IndexOf(part.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
}
