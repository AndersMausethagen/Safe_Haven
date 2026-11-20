var glint = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Raids/Caches/Traveller's Pack.prefab");
var hoard = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Raids/Caches/Barrow Chest.prefab");
if (glint == null || hoard == null || !glint.GetComponent<LootCache>() || !hoard.GetComponent<LootCache>()) throw new System.Exception("caches");
var log = new System.Text.StringBuilder();
foreach (string name in new[] { "MistyWoods", "TrollValley", "MossMarsh" })
{
    var raid = UnityEditor.AssetDatabase.LoadAssetAtPath<RaidDefinition>("Assets/_Project/Data/Raids/" + name + ".asset");
    var so = new UnityEditor.SerializedObject(raid);
    var stages = so.FindProperty("stages");
    log.Append(name + " " + stages.arraySize + " stages: ");
    for (int i = 0; i < stages.arraySize && i < 2; i++)
    {
        var s = stages.GetArrayElementAtIndex(i);
        s.FindPropertyRelative("reveal").intValue = i == 0 ? 1 : 2;
        s.FindPropertyRelative("revealLoot").floatValue = i == 0 ? 1.5f : 1.8f;
        s.FindPropertyRelative("revealCache").objectReferenceValue = i == 0 ? glint : hoard;
        log.Append(s.FindPropertyRelative("displayName").stringValue + "=" + (i == 0 ? "Glint" : "Hoard") + " ");
    }
    so.ApplyModifiedPropertiesWithoutUndo();
    UnityEditor.EditorUtility.SetDirty(raid);
    log.Append("; ");
}
UnityEditor.AssetDatabase.SaveAssets();
return log.ToString();
