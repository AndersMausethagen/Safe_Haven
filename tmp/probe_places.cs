var player = Player.Current;
if (player == null) throw new System.Exception("no player");
string log = "D:/Safe Haven/Safe Haven/tmp/probe_places.txt";
System.IO.File.WriteAllText(log, "");
void Log(string s) => System.IO.File.AppendAllText(log, s + "\n");
var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
var interactor = player.GetComponent<PlayerInteractor>();
var press = typeof(PlayerInteractor).GetMethod("OnInteractionRequested", flags);
var focus = typeof(PlayerInteractor).GetMethod("FindFocus", flags);
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
    if (root == null) { Log("no root"); Log("done"); yield break; }
    Log($"root children {root.transform.childCount}, lookouts {root.GetComponentsInChildren<HavenLookout>(true).Length}, thoughts {root.GetComponentsInChildren<CuriousPlace>(true).Length}, beacons {root.GetComponentsInChildren<PlaceBeacon>(true).Length}, finds {root.GetComponentsInChildren<PlaceFind>(true).Length}");
    var cairn = root.transform.Find("haven.lookout_cairn.1");
    Go(cairn.position + new UnityEngine.Vector3(1.5f, 0f, 0f));
    yield return new UnityEngine.WaitForSecondsRealtime(3f);
    var target = interactor.FindFocus();
    Log($"at the cairn: ground {cairn.position.y:F1}, focus {(target != null ? target.name : "none")}, explored 100 m off {ExplorationMap.Current.ExploredAt(cairn.position + new UnityEngine.Vector3(100f, 0f, 0f)):F2}");
    UnityEngine.ScreenCapture.CaptureScreenshot("D:/Safe Haven/Safe Haven/tmp/probe_lookout.png");
    yield return new UnityEngine.WaitForSecondsRealtime(0.5f);
    int insight = GameSession.Progress.Insight;
    int seenBefore = 0; foreach (var s in GameSession.Progress.Journal.Seen) seenBefore++;
    press.Invoke(interactor, null);
    yield return new UnityEngine.WaitForSecondsRealtime(1f);
    int seen = 0; string names = ""; foreach (var s in GameSession.Progress.Journal.Seen) { seen++; names += s + " "; }
    Log($"looked: insight {insight} -> {GameSession.Progress.Insight}, seen {seenBefore} -> {seen} ({names}), explored 100 m off {ExplorationMap.Current.ExploredAt(cairn.position + new UnityEngine.Vector3(100f, 0f, 0f)):F2}");
    int again = GameSession.Progress.Insight;
    press.Invoke(interactor, null);
    yield return new UnityEngine.WaitForSecondsRealtime(1f);
    Log($"looked again: insight {again} -> {GameSession.Progress.Insight}");
    // The Old Quarry's cut stone.
    var find = root.GetComponentInChildren<PlaceFind>();
    if (find == null) { Log("no find"); Log("done"); yield break; }
    Go(find.transform.position + new UnityEngine.Vector3(1.2f, 0f, 0f));
    yield return new UnityEngine.WaitForSecondsRealtime(2.5f);
    var stone = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Materials/StoneBlock.asset");
    int before = player.Bag.Container.Count(stone);
    target = interactor.FindFocus();
    Log($"at {find.name}: focus {(target != null ? target.name : "none")}, stone in bag {before}");
    UnityEngine.ScreenCapture.CaptureScreenshot("D:/Safe Haven/Safe Haven/tmp/probe_find.png");
    yield return new UnityEngine.WaitForSecondsRealtime(0.5f);
    press.Invoke(interactor, null);
    yield return new UnityEngine.WaitForSecondsRealtime(3f);
    Log($"took: stone in bag {before} -> {player.Bag.Container.Count(stone)}, find still active {(find != null && find.gameObject.activeSelf)}, used {GameSession.Progress.HasUsedStone(PlaceFind.TakenKey("haven.old_quarry.0", stone.Id))}");
    Log("done");
}
player.StartCoroutine(Run());
return "started";
