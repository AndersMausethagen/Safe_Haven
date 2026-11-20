var verbs = HavenPlaceVerbs.Current;
var gap = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Haven/Structures/Dry-stone Gap/Dry-stone Gap.prefab");
var so = new UnityEditor.SerializedObject(verbs);
var finds = so.FindProperty("finds");
for (int i = 0; i < finds.arraySize; i++)
{
    var f = finds.GetArrayElementAtIndex(i);
    f.FindPropertyRelative("marker").objectReferenceValue = gap;
    f.FindPropertyRelative("markerScale").floatValue = 0.55f;
    f.FindPropertyRelative("markerTurn").floatValue = 25f;
}
so.ApplyModifiedPropertiesWithoutUndo();
UnityEditor.EditorUtility.SetDirty(verbs);
UnityEditor.AssetDatabase.SaveAssets();
var r = gap.GetComponentInChildren<Renderer>();
return "set " + finds.arraySize + " bounds " + (r != null ? r.bounds.size.ToString() : "none");
