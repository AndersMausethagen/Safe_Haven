var verbs = HavenPlaceVerbs.Current;
var so = new UnityEditor.SerializedObject(verbs);
var finds = so.FindProperty("finds");
for (int i = 0; i < finds.arraySize; i++) finds.GetArrayElementAtIndex(i).FindPropertyRelative("offset").vector2Value = new Vector2(3f, -7f);
so.ApplyModifiedPropertiesWithoutUndo();
UnityEditor.EditorUtility.SetDirty(verbs);
UnityEditor.AssetDatabase.SaveAssets();
HavenPlaceVerbs.Begin("SafeHaven");
var player = Player.Current;
var find = UnityEngine.GameObject.Find("Haven Place Verbs").GetComponentInChildren<PlaceFind>();
var cc = player.GetComponent<UnityEngine.CharacterController>();
cc.enabled = false; player.transform.position = find.transform.position + new UnityEngine.Vector3(1.5f, 0.3f, -4f); cc.enabled = true;
System.Collections.IEnumerator Run()
{
    yield return new UnityEngine.WaitForSecondsRealtime(3f);
    UnityEngine.ScreenCapture.CaptureScreenshot("D:/Safe Haven/Safe Haven/tmp/look_find3.png");
}
player.StartCoroutine(Run());
return find.transform.position.ToString();
