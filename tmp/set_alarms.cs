var log = new System.Text.StringBuilder();
foreach (string name in new[] { "ForestBittenWolf", "Draug", "MossShaman" })
{
    var monster = UnityEditor.AssetDatabase.LoadAssetAtPath<MonsterDefinition>("Assets/_Project/Data/Monsters/" + name + ".asset");
    if (monster == null) { log.Append(name + ": missing; "); continue; }
    var so = new UnityEditor.SerializedObject(monster);
    var attacks = so.FindProperty("attacks");
    int set = 0;
    for (int i = 0; i < attacks.arraySize; i++)
    {
        var a = attacks.GetArrayElementAtIndex(i);
        if (a.FindPropertyRelative("kind").intValue != (int)MonsterAttackKind.Howl) continue;
        // A call to its side (not one laid on its foe alone).
        a.FindPropertyRelative("onSpotted").boolValue = true;
        a.FindPropertyRelative("alertsOthers").boolValue = true;
        a.FindPropertyRelative("interruptible").boolValue = true;
        a.FindPropertyRelative("alarmRadius").floatValue = 18f;
        var atk = a.FindPropertyRelative("attack").objectReferenceValue;
        log.Append($"{name}[{i}] {(atk != null ? atk.name : "-")} leaderOnly={a.FindPropertyRelative("packLeaderOnly").boolValue}; ");
        set++;
        break;
    }
    if (set == 0) log.Append(name + ": no howl; ");
    so.ApplyModifiedPropertiesWithoutUndo();
    UnityEditor.EditorUtility.SetDirty(monster);
}
UnityEditor.AssetDatabase.SaveAssets();
return log.ToString();
