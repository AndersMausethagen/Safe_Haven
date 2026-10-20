using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// A spreadsheet of every entry on a Content Editor page, for balancing: one row per asset and one column per simple
/// setting (numbers, toggles, choices, names, references). Cells edit the assets directly with Undo, columns sort with a
/// click and can be hidden, and the whole table exports to CSV and imports back, so numbers can be tuned in a spreadsheet.
/// Lists and nested data are left to the normal details view.
/// </summary>
public sealed class ContentTableView
{
    private const float NameWidth = 190f;
    private const float RowHeight = 20f;
    // Sort key meaning "by name"; null keeps the page's own order.
    private const string NameColumn = "";
    private static readonly Color HeaderColor = new Color(0f, 0f, 0f, 0.25f);
    private static readonly Color StripeColor = new Color(1f, 1f, 1f, 0.03f);

    private sealed class Column
    {
        public string Name;
        public GUIContent Header;
        public SerializedPropertyType Type;
        public float Width;
        public Type FieldType;
        public float Min = float.NegativeInfinity;
        public float Max = float.PositiveInfinity;
    }

    private readonly Dictionary<Type, HashSet<string>> hiddenColumns = new Dictionary<Type, HashSet<string>>();
    private Vector2 scroll;
    private string sortColumn;
    private bool sortDescending;
    private GUIContent warningIcon;
    // The table area from the last repaint. Layout events get a placeholder rect, so row culling uses this instead,
    // keeping the same controls in every event.
    private Rect lastArea = new Rect(0f, 0f, 800f, 600f);

    /// <param name="open">Called when a row's name is clicked.</param>
    /// <param name="changed">Called after a cell changes an asset.</param>
    public void Draw(ContentSection section, IReadOnlyList<Object> assets, ContentInspector inspector, Func<Object, bool> hasProblems,
        Action<Object> open, Action changed)
    {
        warningIcon ??= EditorGUIUtility.IconContent("console.warnicon.sml");
        if (assets.Count == 0)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.HelpBox("Nothing to show. Create an entry or clear the search.", MessageType.Info);
            return;
        }

        List<Column> all = FindColumns(section, assets[0], inspector);
        if (sortColumn != null && sortColumn != NameColumn && !all.Exists(column => column.Name == sortColumn))
        {
            sortColumn = null;
        }

        HashSet<string> hidden = Hidden(section.AssetType);
        List<Column> columns = all.FindAll(column => !hidden.Contains(column.Name));
        var rows = new List<Object>(assets);
        Sort(rows, inspector);

        DrawToolbar(section, all, hidden, rows, inspector);
        float width = NameWidth;
        foreach (Column column in columns)
        {
            width += column.Width;
        }

        // Laid out without a minimum width, so a wide table scrolls instead of widening the window's column.
        Rect header = GUILayoutUtility.GetRect(10f, 100000f, RowHeight, RowHeight, GUILayout.ExpandWidth(true));
        Rect area = GUILayoutUtility.GetRect(10f, 100000f, 10f, 100000f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        if (Event.current.type == EventType.Repaint)
        {
            lastArea = area;
        }

        // The header stays put vertically and follows the rows sideways.
        GUI.BeginClip(header);
        DrawHeader(new Rect(-scroll.x, 0f, width, RowHeight), columns);
        GUI.EndClip();

        var content = new Rect(0f, 0f, width, rows.Count * RowHeight);
        scroll = GUI.BeginScrollView(area, scroll, content, true, false);
        int first = Mathf.Max(0, Mathf.FloorToInt(scroll.y / RowHeight));
        int last = Mathf.Min(rows.Count - 1, Mathf.CeilToInt((scroll.y + lastArea.height) / RowHeight));
        for (int i = first; i <= last; i++)
        {
            DrawRow(new Rect(0f, i * RowHeight, width, RowHeight), rows[i], i, columns, inspector, hasProblems, open, changed);
        }

        GUI.EndScrollView();
    }

    // ----- Columns -----

    private static List<Column> FindColumns(ContentSection section, Object sample, ContentInspector inspector)
    {
        var columns = new List<Column>();
        SerializedProperty property = inspector.Get(sample).GetIterator();
        bool enterChildren = true;
        while (property.NextVisible(enterChildren))
        {
            enterChildren = false;
            FieldInfo field = FindField(sample.GetType(), property.name);
            if (property.name == "m_Script" || property.name == "id" || section.HiddenFields.Contains(property.name)
                || !IsSupported(property) || field == null || IsMultiline(field))
            {
                continue;
            }

            float width = ColumnWidth(property.propertyType);
            string label = ObjectNames.NicifyVariableName(property.name);
            width = Mathf.Max(width, EditorStyles.miniBoldLabel.CalcSize(new GUIContent(label + "  ▲")).x + 8f);
            var column = new Column
            {
                Name = property.name, Type = property.propertyType, Width = width, FieldType = field.FieldType,
                Header = new GUIContent(label, property.tooltip),
            };
            GetLimits(field, out column.Min, out column.Max);
            columns.Add(column);
        }

        return columns;
    }

    private static FieldInfo FindField(Type type, string fieldName)
    {
        for (Type current = type; current != null && current != typeof(ScriptableObject); current = current.BaseType)
        {
            FieldInfo field = current.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (field != null)
            {
                return field;
            }
        }

        return null;
    }

    // [Min] and [Range] are respected in cells and on import, as the normal inspector does.
    private static void GetLimits(FieldInfo field, out float min, out float max)
    {
        min = float.NegativeInfinity;
        max = float.PositiveInfinity;
        if (field == null)
        {
            return;
        }

        if (field.GetCustomAttribute<RangeAttribute>() is RangeAttribute range)
        {
            min = range.min;
            max = range.max;
        }
        else if (field.GetCustomAttribute<MinAttribute>() is MinAttribute minimum)
        {
            min = minimum.min;
        }
    }

    private static void Clamp(SerializedProperty property, float min, float max)
    {
        if (float.IsNegativeInfinity(min) && float.IsPositiveInfinity(max))
        {
            return;
        }

        if (property.propertyType == SerializedPropertyType.Integer)
        {
            long clamped = (long)Math.Max(Math.Min(property.longValue, (double)max), min);
            if (clamped != property.longValue)
            {
                property.longValue = clamped;
            }
        }
        else if (property.propertyType == SerializedPropertyType.Float)
        {
            float clamped = Mathf.Clamp(property.floatValue, min, max);
            if (!Mathf.Approximately(clamped, property.floatValue))
            {
                property.floatValue = clamped;
            }
        }
    }

    private static bool IsSupported(SerializedProperty property)
    {
        switch (property.propertyType)
        {
            case SerializedPropertyType.Integer:
            case SerializedPropertyType.Float:
            case SerializedPropertyType.Boolean:
            case SerializedPropertyType.Enum:
            case SerializedPropertyType.String:
            case SerializedPropertyType.ObjectReference:
            case SerializedPropertyType.Color:
                return !property.isArray || property.propertyType == SerializedPropertyType.String;
            default:
                return false;
        }
    }

    // Long texts such as descriptions don't fit in a cell.
    private static bool IsMultiline(FieldInfo field)
    {
        return field.GetCustomAttribute<TextAreaAttribute>() != null || field.GetCustomAttribute<MultilineAttribute>() != null;
    }

    private static float ColumnWidth(SerializedPropertyType type)
    {
        switch (type)
        {
            case SerializedPropertyType.Boolean:
                return 52f;
            case SerializedPropertyType.Integer:
            case SerializedPropertyType.Float:
            case SerializedPropertyType.Color:
                return 72f;
            case SerializedPropertyType.Enum:
                return 118f;
            default:
                return 160f;
        }
    }

    private HashSet<string> Hidden(Type type)
    {
        if (!hiddenColumns.TryGetValue(type, out HashSet<string> hidden))
        {
            hidden = new HashSet<string>();
            hiddenColumns[type] = hidden;
        }

        return hidden;
    }

    // ----- Drawing -----

    private void DrawToolbar(ContentSection section, List<Column> all, HashSet<string> hidden, List<Object> rows, ContentInspector inspector)
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField($"{section.Title} — Table", ContentUtility.HeaderLabel);
        EditorGUILayout.LabelField(
            "Edit numbers across every entry at once. Click a column to sort, a name to open the entry. Export to CSV to balance in a spreadsheet and import it back.",
            ContentUtility.WrappedMiniLabel);

        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button($"Columns ({all.Count - hidden.Count}/{all.Count})", EditorStyles.toolbarDropDown, GUILayout.Width(120f)))
            {
                var menu = new GenericMenu();
                foreach (Column column in all)
                {
                    string name = column.Name;
                    menu.AddItem(new GUIContent(column.Header.text), !hidden.Contains(name), () =>
                    {
                        if (!hidden.Remove(name))
                        {
                            hidden.Add(name);
                        }
                    });
                }

                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Show All"), false, hidden.Clear);
                menu.ShowAsContext();
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label($"{rows.Count} entries", EditorStyles.miniLabel);
            if (GUILayout.Button(new GUIContent("Export CSV", "Saves the shown entries and every column to a CSV file."), EditorStyles.toolbarButton))
            {
                ExportCsv(section, rows, all, inspector);
                GUIUtility.ExitGUI();
            }

            if (GUILayout.Button(new GUIContent("Import CSV", "Reads a CSV made by Export CSV and applies the changed values, after asking."), EditorStyles.toolbarButton))
            {
                ImportCsv(section, rows, inspector);
                GUIUtility.ExitGUI();
            }
        }
    }

    private void DrawHeader(Rect rect, List<Column> columns)
    {
        EditorGUI.DrawRect(rect, HeaderColor);
        var cell = new Rect(rect.x, rect.y, NameWidth, rect.height);
        HeaderButton(cell, new GUIContent("Name", "Click to sort by name; click again to reverse."), NameColumn);
        foreach (Column column in columns)
        {
            cell.x += cell.width;
            cell.width = column.Width;
            HeaderButton(cell, column.Header, column.Name);
        }
    }

    private void HeaderButton(Rect rect, GUIContent content, string column)
    {
        string arrow = sortColumn == column ? (sortDescending ? "  ▼" : "  ▲") : "";
        if (GUI.Button(rect, new GUIContent(content.text + arrow, content.tooltip), EditorStyles.miniBoldLabel))
        {
            // Ascending, then descending, then back to the page's own order.
            if (sortColumn != column)
            {
                sortColumn = column;
                sortDescending = false;
            }
            else if (!sortDescending)
            {
                sortDescending = true;
            }
            else
            {
                sortColumn = null;
                sortDescending = false;
            }
        }
    }

    private void DrawRow(Rect rect, Object asset, int index, List<Column> columns, ContentInspector inspector, Func<Object, bool> hasProblems,
        Action<Object> open, Action changed)
    {
        if (index % 2 == 1)
        {
            EditorGUI.DrawRect(rect, StripeColor);
        }

        var nameRect = new Rect(rect.x + 2f, rect.y + 1f, NameWidth - 4f, rect.height - 2f);
        bool warn = hasProblems != null && hasProblems(asset);
        var label = new GUIContent(ContentUtility.DisplayName(asset), warn ? warningIcon.image : null, warn ? "Needs attention: open it to see why." : "Open this entry.");
        if (GUI.Button(nameRect, label, EditorStyles.label))
        {
            open(asset);
            GUIUtility.ExitGUI();
        }

        SerializedObject data = inspector.Get(asset);
        data.Update();
        EditorGUI.BeginChangeCheck();
        float x = rect.x + NameWidth;
        foreach (Column column in columns)
        {
            SerializedProperty property = data.FindProperty(column.Name);
            if (property != null)
            {
                DrawCell(new Rect(x + 2f, rect.y + 1f, column.Width - 4f, rect.height - 2f), property, column);
            }

            x += column.Width;
        }

        if (EditorGUI.EndChangeCheck())
        {
            data.ApplyModifiedProperties();
            changed?.Invoke();
        }
    }

    // Drawn by type rather than with PropertyField, which would also draw [Header] decorations inside the cell.
    private static void DrawCell(Rect rect, SerializedProperty property, Column column)
    {
        switch (property.propertyType)
        {
            case SerializedPropertyType.Integer:
                property.longValue = EditorGUI.LongField(rect, property.longValue);
                break;
            case SerializedPropertyType.Float:
                property.floatValue = EditorGUI.FloatField(rect, property.floatValue);
                break;
            case SerializedPropertyType.Boolean:
                property.boolValue = EditorGUI.Toggle(rect, property.boolValue);
                break;
            case SerializedPropertyType.Enum:
                property.enumValueIndex = EditorGUI.Popup(rect, property.enumValueIndex, property.enumDisplayNames);
                break;
            case SerializedPropertyType.String:
                property.stringValue = EditorGUI.DelayedTextField(rect, property.stringValue);
                break;
            case SerializedPropertyType.ObjectReference:
                property.objectReferenceValue = EditorGUI.ObjectField(rect, property.objectReferenceValue, column.FieldType, false);
                break;
            case SerializedPropertyType.Color:
                property.colorValue = EditorGUI.ColorField(rect, property.colorValue);
                break;
        }

        Clamp(property, column.Min, column.Max);
    }

    // ----- Sorting -----

    private void Sort(List<Object> rows, ContentInspector inspector)
    {
        if (sortColumn == null)
        {
            return;
        }

        rows.Sort((a, b) =>
        {
            int result = sortColumn == NameColumn
                ? string.Compare(ContentUtility.DisplayName(a), ContentUtility.DisplayName(b), StringComparison.OrdinalIgnoreCase)
                : CompareValues(SortKey(inspector, a), SortKey(inspector, b));
            return sortDescending ? -result : result;
        });
    }

    private object SortKey(ContentInspector inspector, Object asset)
    {
        SerializedProperty property = inspector.Get(asset).FindProperty(sortColumn);
        if (property == null)
        {
            return null;
        }

        switch (property.propertyType)
        {
            case SerializedPropertyType.Integer:
                return (double)property.longValue;
            case SerializedPropertyType.Float:
                return (double)property.floatValue;
            case SerializedPropertyType.Boolean:
                return property.boolValue ? 1d : 0d;
            case SerializedPropertyType.Enum:
                return (double)property.intValue;
            default:
                return ToCell(property);
        }
    }

    private static int CompareValues(object a, object b)
    {
        if (a is double x && b is double y)
        {
            return x.CompareTo(y);
        }

        return string.Compare(a?.ToString() ?? "", b?.ToString() ?? "", StringComparison.OrdinalIgnoreCase);
    }

    // ----- CSV -----

    private static void ExportCsv(ContentSection section, List<Object> rows, List<Column> columns, ContentInspector inspector)
    {
        string path = EditorUtility.SaveFilePanel($"Export {section.Title}", "", PrefabBuildUtility.MakeFileName(section.Title) + ".csv", "csv");
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        File.WriteAllText(path, BuildCsv(rows, columns.ConvertAll(column => column.Name)), new UTF8Encoding(true));
        EditorUtility.RevealInFinder(path);
    }

    private static void ImportCsv(ContentSection section, List<Object> rows, ContentInspector inspector)
    {
        string path = EditorUtility.OpenFilePanel($"Import {section.Title}", "", "csv");
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        CsvImportPlan plan = PlanImport(File.ReadAllText(path), rows);
        string report = plan.Errors.Count == 0 ? ""
            : $"\n\n{plan.Errors.Count} problem(s) will be skipped:\n• " + string.Join("\n• ", plan.Errors.GetRange(0, Math.Min(8, plan.Errors.Count)));
        if (plan.ChangeCount == 0)
        {
            EditorUtility.DisplayDialog("Import CSV", (plan.Errors.Count > 0 ? "Nothing was imported." : "Nothing to change: every value already matches.") + report, "OK");
            return;
        }

        if (EditorUtility.DisplayDialog("Import CSV", $"Change {plan.ChangeCount} value(s) on {plan.AssetCount} {section.Title.ToLowerInvariant()}?{report}", "Apply", "Cancel"))
        {
            plan.Apply();
            inspector.ClearCache();
            AssetDatabase.SaveAssets();
        }
    }

    /// <summary>
    /// The rows as CSV: an "asset" column (its path, which identifies it on import), a "name" column for reading, then one
    /// column per setting.
    /// </summary>
    public static string BuildCsv(IReadOnlyList<Object> rows, IReadOnlyList<string> columns)
    {
        var csv = new StringBuilder();
        var header = new List<string> { "asset", "name" };
        header.AddRange(columns);
        csv.AppendLine(CsvLine(header));
        foreach (Object asset in rows)
        {
            var data = new SerializedObject(asset);
            var cells = new List<string> { KeyOf(asset), ContentUtility.DisplayName(asset) };
            foreach (string column in columns)
            {
                SerializedProperty property = data.FindProperty(column);
                cells.Add(property != null ? ToCell(property) : "");
            }

            csv.AppendLine(CsvLine(cells));
        }

        return csv.ToString();
    }

    /// <summary>What applying a CSV would change. Nothing is touched until Apply is called.</summary>
    public sealed class CsvImportPlan
    {
        internal readonly List<(SerializedObject data, string column, string value)> Changes = new List<(SerializedObject, string, string)>();

        public List<string> Errors { get; } = new List<string>();
        public int ChangeCount => Changes.Count;
        public int AssetCount { get; internal set; }

        /// <summary>Writes every planned change, with Undo, clamped to the fields' [Min] and [Range].</summary>
        public void Apply()
        {
            var touched = new HashSet<SerializedObject>();
            foreach ((SerializedObject data, string column, string value) in Changes)
            {
                SerializedProperty property = data.FindProperty(column);
                if (TryParse(property, value, out Action apply))
                {
                    apply();
                    GetLimits(FindField(data.targetObject.GetType(), column), out float min, out float max);
                    Clamp(property, min, max);
                    touched.Add(data);
                }
            }

            foreach (SerializedObject data in touched)
            {
                data.ApplyModifiedProperties();
            }
        }
    }

    /// <summary>Matches CSV rows to the given assets by their "asset" column and lists the values that differ.</summary>
    public static CsvImportPlan PlanImport(string csvText, IReadOnlyList<Object> rows)
    {
        var plan = new CsvImportPlan();
        List<List<string>> table = ParseCsv(csvText);
        if (table.Count == 0 || table[0].Count == 0 || table[0][0] != "asset")
        {
            plan.Errors.Add("This file wasn't made by Export CSV: its first column must be 'asset'.");
            return plan;
        }

        var byKey = new Dictionary<string, Object>();
        foreach (Object asset in rows)
        {
            byKey[KeyOf(asset)] = asset;
        }

        List<string> header = table[0];
        var touched = new HashSet<Object>();
        for (int r = 1; r < table.Count; r++)
        {
            List<string> row = table[r];
            if (row.Count == 0 || string.IsNullOrWhiteSpace(row[0]))
            {
                continue;
            }

            if (!byKey.TryGetValue(row[0], out Object asset))
            {
                plan.Errors.Add($"Row {r + 1}: '{row[0]}' isn't on this page (or is hidden by the search).");
                continue;
            }

            var data = new SerializedObject(asset);
            for (int c = 2; c < header.Count && c < row.Count; c++)
            {
                SerializedProperty property = data.FindProperty(header[c]);
                if (property == null || !IsSupported(property) || ToCell(property) == row[c])
                {
                    continue;
                }

                if (!TryParse(property, row[c], out _))
                {
                    plan.Errors.Add($"Row {r + 1}, {header[c]}: '{row[c]}' isn't a valid {property.propertyType}.");
                    continue;
                }

                plan.Changes.Add((data, header[c], row[c]));
                touched.Add(asset);
            }
        }

        plan.AssetCount = touched.Count;
        return plan;
    }

    // Saved assets are identified by path; unsaved ones (in tests) by name.
    private static string KeyOf(Object asset)
    {
        string path = AssetDatabase.GetAssetPath(asset);
        return string.IsNullOrEmpty(path) ? asset.name : path;
    }
    private static string ToCell(SerializedProperty property)
    {
        switch (property.propertyType)
        {
            case SerializedPropertyType.Integer:
                return property.longValue.ToString(CultureInfo.InvariantCulture);
            case SerializedPropertyType.Float:
                return property.floatValue.ToString("R", CultureInfo.InvariantCulture);
            case SerializedPropertyType.Boolean:
                return property.boolValue ? "true" : "false";
            case SerializedPropertyType.Enum:
                return property.enumValueIndex >= 0 && property.enumValueIndex < property.enumNames.Length
                    ? property.enumNames[property.enumValueIndex]
                    : property.intValue.ToString(CultureInfo.InvariantCulture);
            case SerializedPropertyType.String:
                return property.stringValue;
            case SerializedPropertyType.ObjectReference:
                return property.objectReferenceValue != null ? AssetDatabase.GetAssetPath(property.objectReferenceValue) : "";
            case SerializedPropertyType.Color:
                return "#" + ColorUtility.ToHtmlStringRGBA(property.colorValue);
            default:
                return "";
        }
    }

    // Validates the text for the property and returns how to apply it, so nothing changes until every row is checked.
    private static bool TryParse(SerializedProperty property, string text, out Action apply)
    {
        apply = null;
        text = text?.Trim() ?? "";
        switch (property.propertyType)
        {
            case SerializedPropertyType.Integer when long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long whole):
                apply = () => property.longValue = whole;
                return true;
            case SerializedPropertyType.Float when float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float number):
                apply = () => property.floatValue = number;
                return true;
            case SerializedPropertyType.Boolean when bool.TryParse(text, out bool flag):
                apply = () => property.boolValue = flag;
                return true;
            case SerializedPropertyType.Enum:
            {
                int index = Array.FindIndex(property.enumNames, name => string.Equals(name, text, StringComparison.OrdinalIgnoreCase));
                if (index < 0)
                {
                    return false;
                }

                apply = () => property.enumValueIndex = index;
                return true;
            }
            case SerializedPropertyType.String:
            {
                string value = text;
                apply = () => property.stringValue = value;
                return true;
            }
            case SerializedPropertyType.ObjectReference:
            {
                Object target = text.Length == 0 ? null : AssetDatabase.LoadMainAssetAtPath(text);
                if (text.Length > 0 && target == null)
                {
                    return false;
                }

                apply = () => property.objectReferenceValue = target;
                return true;
            }
            case SerializedPropertyType.Color when ColorUtility.TryParseHtmlString(text, out Color color):
                apply = () => property.colorValue = color;
                return true;
            default:
                return false;
        }
    }

    private static string CsvLine(List<string> cells)
    {
        var line = new StringBuilder();
        for (int i = 0; i < cells.Count; i++)
        {
            string cell = cells[i] ?? "";
            if (i > 0)
            {
                line.Append(',');
            }

            bool quote = cell.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0;
            line.Append(quote ? "\"" + cell.Replace("\"", "\"\"") + "\"" : cell);
        }

        return line.ToString();
    }

    /// <summary>Reads CSV with quoted cells, doubled quotes and line breaks inside quotes.</summary>
    public static List<List<string>> ParseCsv(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        bool quoted = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"')
                {
                    cell.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    quoted = false;
                }
                else
                {
                    cell.Append(c);
                }
            }
            else if (c == '"')
            {
                quoted = true;
            }
            else if (c == ',')
            {
                row.Add(cell.ToString());
                cell.Clear();
            }
            else if (c == '\n' || c == '\r')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                row.Add(cell.ToString());
                cell.Clear();
                rows.Add(row);
                row = new List<string>();
            }
            else if (c != '\uFEFF')
            {
                cell.Append(c);
            }
        }

        if (cell.Length > 0 || row.Count > 0)
        {
            row.Add(cell.ToString());
            rows.Add(row);
        }

        return rows;
    }
}
