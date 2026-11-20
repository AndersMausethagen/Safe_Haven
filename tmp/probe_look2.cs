var player = Player.Current;
var root = UnityEngine.GameObject.Find("Haven Place Verbs");
var find = root.GetComponentInChildren<PlaceFind>();
var cc = player.GetComponent<UnityEngine.CharacterController>();
cc.enabled = false; player.transform.position = find.transform.position + new UnityEngine.Vector3(0f, 0.3f, -4.5f); cc.enabled = true;
System.Collections.IEnumerator Run()
{
    yield return new UnityEngine.WaitForSecondsRealtime(3f);
    UnityEngine.ScreenCapture.CaptureScreenshot("D:/Safe Haven/Safe Haven/tmp/look_find2.png");
}
player.StartCoroutine(Run());
return find.transform.position.ToString();
