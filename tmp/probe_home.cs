var player = Player.Current;
if (player == null) throw new System.Exception("no player");
string log = "D:/Safe Haven/Safe Haven/tmp/probe_home.txt";
System.IO.File.WriteAllText(log, "");
void Log(string s) => System.IO.File.AppendAllText(log, s + "\n");
var smithy = UnityEditor.AssetDatabase.LoadAssetAtPath<BuildingDefinition>("Assets/_Project/Data/Buildings/Forge.asset");
GameSession.Progress.ToggleTracked(WantedAtHome.BuildingKey(smithy, 2), WantedAtHome.MostTracked);
foreach (var want in WantedAtHome.Now(player)) Log($"want {want.Name} {want.Have}/{want.Need} for {want.For}");
var stone = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Materials/StoneBlock.asset");
GameSession.Home.MadeByWorkers[stone] = 4;
var lines = Homecoming.Compose(player, GameSession.Home);
foreach (var line in lines) Log("card: " + line);
HomecomingCard.Show(lines);
System.Collections.IEnumerator Run()
{
    yield return new UnityEngine.WaitForSecondsRealtime(3.5f);
    UnityEngine.ScreenCapture.CaptureScreenshot("D:/Safe Haven/Safe Haven/tmp/probe_home.png");
    yield return new UnityEngine.WaitForSecondsRealtime(1f);
    Log("done");
}
player.StartCoroutine(Run());
return "started";
