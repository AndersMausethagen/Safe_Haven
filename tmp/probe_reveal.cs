var player = Player.Current;
var director = RaidDirector.Current;
if (player == null || director == null) throw new System.Exception("no raid");
string log = "D:/Safe Haven/Safe Haven/tmp/probe_reveal.txt";
System.IO.File.WriteAllText(log, "");
void Log(string s) => System.IO.File.AppendAllText(log, s + "\n");
var session = director.Session;
session.AddTime(900f);
float left = session.TimeRemaining;
Log($"left {left:F0}, stage {director.StageIndex}");
System.Collections.IEnumerator Run()
{
    // To 4 s before the first stage (480 s left), by the clock itself.
    while (session.TimeRemaining > 484f)
    {
        session.Tick(session.TimeRemaining - 484f);
        yield return null;
    }

    Log($"now {session.TimeRemaining:F0} left, stage {director.StageIndex}, sites {StageReveal.Sites.Count}");
    float until = UnityEngine.Time.realtimeSinceStartup + 20f;
    while (StageReveal.Sites.Count == 0 && UnityEngine.Time.realtimeSinceStartup < until) yield return null;
    Log($"after: {session.TimeRemaining:F0} left, stage {director.StageIndex} ({director.CurrentStage?.DisplayName}), sites {StageReveal.Sites.Count}");
    foreach (var cache in StageReveal.Sites)
    {
        float d = UnityEngine.Vector3.Distance(cache.transform.position, player.transform.position);
        float exit = float.MaxValue;
        foreach (var e in ExtractionPoint.Active) exit = UnityEngine.Mathf.Min(exit, UnityEngine.Vector3.Distance(e.transform.position, cache.transform.position));
        Log($"  {cache.name} {d:F0} m from the player, {exit:F0} m from the nearest exit, extra loot {cache.ExtraLoot}, locked {cache.IsLocked}");
    }
    yield return new UnityEngine.WaitForSecondsRealtime(1f);
    UnityEngine.ScreenCapture.CaptureScreenshot("D:/Safe Haven/Safe Haven/tmp/probe_reveal.png");
    yield return new UnityEngine.WaitForSecondsRealtime(1f);
    Log("done");
}
player.StartCoroutine(Run());
return "started";
