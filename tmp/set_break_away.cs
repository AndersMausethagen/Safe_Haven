var log = new System.Text.StringBuilder();
var plan = new System.Collections.Generic.Dictionary<string, (float metres, float least, float most)> {
    {"wolf", (3.5f, 0.8f, 1.4f)}, {"rootling", (2.5f, 0.5f, 0.9f)}, {"runt", (3f, 0.6f, 1.0f)} };
foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:MonsterDefinition", new[] { "Assets/_Project/Data" }))
{
    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
    var monster = UnityEditor.AssetDatabase.LoadAssetAtPath<MonsterDefinition>(path);
    if (monster == null) continue;
    string lower = monster.name.ToLowerInvariant();
    string key = null;
    foreach (var k in plan.Keys) if (lower.Contains(k)) key = k;
    if (key == null) continue;
    var so = new UnityEditor.SerializedObject(monster);
    var attacks = so.FindProperty("attacks");
    if (attacks == null) { log.Append(monster.name + ": no attacks list; "); continue; }
    int set = 0;
    for (int i = 0; i < attacks.arraySize; i++)
    {
        var a = attacks.GetArrayElementAtIndex(i);
        if (a.FindPropertyRelative("kind").intValue != (int)MonsterAttackKind.Melee) continue;
        // The quick strikes only: a heavy blow keeps its weight where it lands.
        if (a.FindPropertyRelative("heavy").boolValue) continue;
        a.FindPropertyRelative("breakAway").floatValue = plan[key].metres;
        a.FindPropertyRelative("breakAwaySeconds").vector2Value = new Vector2(plan[key].least, plan[key].most);
        var atk = a.FindPropertyRelative("attack").objectReferenceValue;
        log.Append($"{monster.name}[{i}] {(atk != null ? atk.name : "-")} -> {plan[key].metres} m; ");
        set++;
    }
    if (key == "wolf")
    {
        var advice = so.FindProperty("journalAdvice");
        if (advice != null && !advice.stringValue.Contains("howls"))
        {
            advice.stringValue = (advice.stringValue.TrimEnd() + " Bring down the one that howls, the biggest of them, and the rest will run.").Trim();
        }
    }
    so.ApplyModifiedPropertiesWithoutUndo();
    UnityEditor.EditorUtility.SetDirty(monster);
    if (set == 0) log.Append(monster.name + ": no quick melee; ");
}
UnityEditor.AssetDatabase.SaveAssets();
return log.ToString();
