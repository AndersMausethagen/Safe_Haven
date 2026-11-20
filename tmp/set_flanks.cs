var log = new System.Text.StringBuilder();
foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:MonsterDefinition", new[] { "Assets/_Project/Data/Monsters" }))
{
    var monster = UnityEditor.AssetDatabase.LoadAssetAtPath<MonsterDefinition>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
    if (monster == null) continue;
    int tier = monster.name == "ForestBittenWolf" ? 1 : monster.name == "Draug" || monster.name.StartsWith("Lost") ? 2 : 0;
    if (tier == 0) continue;
    var so = new UnityEditor.SerializedObject(monster);
    so.FindProperty("flanksFromTier").intValue = tier;
    so.ApplyModifiedPropertiesWithoutUndo();
    UnityEditor.EditorUtility.SetDirty(monster);
    log.Append(monster.name + " from tier " + tier + "; ");
}
var wolf = UnityEditor.AssetDatabase.LoadAssetAtPath<MonsterDefinition>("Assets/_Project/Data/Monsters/ForestBittenWolf.asset");
UnityEditor.AssetDatabase.SaveAssets();
return log + " | wolf advice: " + wolf.JournalAdvice;
