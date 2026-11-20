var player = Player.Current;
if (player == null) throw new System.Exception("no player");
string log = "D:/Safe Haven/Safe Haven/tmp/probe_look.txt";
System.IO.File.WriteAllText(log, "");
void Log(string s) => System.IO.File.AppendAllText(log, s + "\n");
void Go(UnityEngine.Vector3 p)
{
    var cc = player.GetComponent<UnityEngine.CharacterController>();
    if (cc != null) cc.enabled = false;
    player.transform.position = p + UnityEngine.Vector3.up * 0.3f;
    if (cc != null) cc.enabled = true;
}
System.Collections.IEnumerator Run()
{
    var root = UnityEngine.GameObject.Find("Haven Place Verbs");
    var find = root.GetComponentInChildren<PlaceFind>();
    Go(find.transform.position + new UnityEngine.Vector3(-2.5f, 0f, -3f));
    yield return new UnityEngine.WaitForSecondsRealtime(4f);
    UnityEngine.ScreenCapture.CaptureScreenshot("D:/Safe Haven/Safe Haven/tmp/look_find.png");
    yield return new UnityEngine.WaitForSecondsRealtime(1f);
    // 50 m short of the Burnt Farmstead, toward the portal: its "?" on the minimap's rim.
    UnityEngine.Vector2 farm = new UnityEngine.Vector2(-3f, -267f);
    UnityEngine.Vector2 at = farm + (UnityEngine.Vector2.zero - farm).normalized * 50f;
    Go(HavenPlaceVerbs.Ground(at));
    yield return new UnityEngine.WaitForSecondsRealtime(4f);
    UnityEngine.ScreenCapture.CaptureScreenshot("D:/Safe Haven/Safe Haven/tmp/look_hint.png");
    Log($"at {player.transform.position}");
    Log("done");
}
player.StartCoroutine(Run());
return "started";
