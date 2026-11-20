const string targets = "Assets/_Project/Data/Quests/Targets/";
QuestTargetDefinition Target(string file, string id, string shown, string verb)
{
    string path = targets + file + ".asset";
    var t = UnityEditor.AssetDatabase.LoadAssetAtPath<QuestTargetDefinition>(path);
    if (t == null) { t = ScriptableObject.CreateInstance<QuestTargetDefinition>(); UnityEditor.AssetDatabase.CreateAsset(t, path); }
    t.Configure(id, shown, verb);
    UnityEditor.EditorUtility.SetDirty(t);
    return t;
}
var quarry = Target("Old Quarry", "haven.old-quarry", "the Old Quarry", "Use");
var stack = Target("Old Quarry Cut Stone", "haven.old-quarry.cut-stone", "the old masons' cut stone", "Take");

// The place layer: reaching the quarry, taking its stack.
var verbs = HavenPlaceVerbs.Current;
var vso = new UnityEditor.SerializedObject(verbs);
var finds = vso.FindProperty("finds");
for (int i = 0; i < finds.arraySize; i++)
{
    var f = finds.GetArrayElementAtIndex(i);
    if (f.FindPropertyRelative("placeId").stringValue == "haven.old_quarry.0") f.FindPropertyRelative("questTarget").objectReferenceValue = stack;
}
var list = vso.FindProperty("targets");
list.arraySize = 1;
list.GetArrayElementAtIndex(0).FindPropertyRelative("placeId").stringValue = "haven.old_quarry.0";
list.GetArrayElementAtIndex(0).FindPropertyRelative("target").objectReferenceValue = quarry;
vso.ApplyModifiedPropertiesWithoutUndo();
UnityEditor.EditorUtility.SetDirty(verbs);

// The quest, from the green template (A Hand for the Shelf).
const string questPath = "Assets/_Project/Data/Quests/CutStone.asset";
if (UnityEditor.AssetDatabase.LoadAssetAtPath<QuestDefinition>(questPath) == null)
    UnityEditor.AssetDatabase.CopyAsset("Assets/_Project/Data/Quests/AHandForTheShelf.asset", questPath);
var quest = UnityEditor.AssetDatabase.LoadAssetAtPath<QuestDefinition>(questPath);
var alchemist = UnityEditor.AssetDatabase.LoadAssetAtPath<QuestDefinition>("Assets/_Project/Data/Quests/TheAlchemist.asset");
var q = new UnityEditor.SerializedObject(quest);
q.FindProperty("id").stringValue = "haven.cut_stone";
q.FindProperty("displayName").stringValue = "Cut Stone";
q.FindProperty("description").stringValue = "The altar wants squared stone, and squaring it is slow work. The portal remembers masons cutting it in the north of Ravenwood, past the Charcoal Camp, and says some of it may still be stacked where they left it. Walk out to the Old Quarry and see.";
q.FindProperty("chain").stringValue = "";
q.FindProperty("journalNotes").stringValue = "The Old Quarry lies about 290 m north of the portal, past the Charcoal Camp. The masons who worked it left in the middle of a day.";
q.FindProperty("rank").intValue = (int)QuestRank.Haven;
q.FindProperty("prerequisites").arraySize = 0;
var first = q.FindProperty("startedFirst");
first.arraySize = 1;
first.GetArrayElementAtIndex(0).objectReferenceValue = alchemist;
q.FindProperty("start").intValue = (int)QuestStart.Automatically;
q.FindProperty("objectivesInOrder").boolValue = true;
var objectives = q.FindProperty("objectives");
objectives.arraySize = 2;
void Step(int index, ObjectiveType type, string text, QuestTargetDefinition target)
{
    var o = objectives.GetArrayElementAtIndex(index);
    o.FindPropertyRelative("type").intValue = (int)type;
    o.FindPropertyRelative("description").stringValue = text;
    o.FindPropertyRelative("amount").intValue = 1;
    o.FindPropertyRelative("person").objectReferenceValue = null;
    o.FindPropertyRelative("target").objectReferenceValue = target;
    o.FindPropertyRelative("countAlreadyOwned").boolValue = true;
}
Step(0, ObjectiveType.ReachPlace, "Walk out to the Old Quarry", quarry);
Step(1, ObjectiveType.UseObject, "Take the cut stone the masons left", stack);
q.FindProperty("rewardWorkFor").objectReferenceValue = null;
q.FindProperty("rewardWork").intValue = 0;
q.FindProperty("experience").intValue = 0;
q.ApplyModifiedPropertiesWithoutUndo();
UnityEditor.EditorUtility.SetDirty(quest);
UnityEditor.AssetDatabase.SaveAssets();
return quest.DisplayName + " rank " + quest.Rank + " steps " + quest.Objectives.Count + ": " + quest.Objectives[0].Describe() + " / " + quest.Objectives[1].Describe() + "; starts after " + (quest.StartedFirst.Count > 0 ? quest.StartedFirst[0].DisplayName : "-");
