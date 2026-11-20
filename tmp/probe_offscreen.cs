var player = Player.Current;
string log = "D:/Safe Haven/Safe Haven/tmp/probe_offscreen.txt";
System.IO.File.WriteAllText(log, "");
var cam = UnityEngine.Camera.main.transform.forward; cam.y = 0f; cam.Normalize();
UnityEngine.Vector3 Snap(UnityEngine.Vector3 p) { UnityEngine.AI.NavMeshHit h; return UnityEngine.AI.NavMesh.SamplePosition(p, out h, 5f, UnityEngine.AI.NavMesh.AllAreas) ? h.position : p; }
var draugPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/_Project/Prefabs/Enemies/Draug.prefab");
void Log(string s) => System.IO.File.AppendAllText(log, s + "\n");
System.Collections.IEnumerator Run()
{
    var d = UnityEngine.Object.Instantiate(draugPrefab, Snap(player.transform.position + cam * 3f), UnityEngine.Quaternion.LookRotation(-cam));
    yield return new UnityEngine.WaitForSeconds(0.3f);
    d.GetComponent<MonsterAI>().Engage(player.Health);
    var tg = d.GetComponent<AttackTelegraph>();
    var view = UnityEngine.Camera.main;
    float until = UnityEngine.Time.realtimeSinceStartup + 25f; int shots = 0;
    while (UnityEngine.Time.realtimeSinceStartup < until && shots < 2)
    {
        if (tg.IsWarning && tg.Progress < 0.35f)
        {
            var agent = d.GetComponent<UnityEngine.AI.NavMeshAgent>();
            agent.Warp(Snap(player.transform.position - cam * 9f + UnityEngine.Vector3.Cross(UnityEngine.Vector3.up, cam) * (shots == 0 ? 0f : 9f)));
            float wait = UnityEngine.Time.realtimeSinceStartup + 3f;
            while (UnityEngine.Time.realtimeSinceStartup < wait && tg.IsWarning && tg.Progress < 0.7f) yield return null;
            var p = view.WorldToViewportPoint(d.transform.position + UnityEngine.Vector3.up * 1.2f);
            int active = 0; string where = "";
            var canvas = UnityEngine.GameObject.Find("Off Screen Warnings");
            if (canvas != null) foreach (UnityEngine.Transform t in canvas.transform) if (t.gameObject.activeSelf) { active++; var r = (UnityEngine.RectTransform)t; where += r.anchorMin + " scale " + r.localScale.x.ToString("F2") + " "; }
            Log($"warning {tg.IsWarning} progress {tg.Progress:F2} viewport {p} off {OffScreenWarnings.IsOffScreen(p)} heavy {tg.WarnsHeavy} chevrons {active} {where}");
            UnityEngine.ScreenCapture.CaptureScreenshot($"D:/Safe Haven/Safe Haven/tmp/probe_offscreen_{shots}.png");
            shots++;
            yield return new UnityEngine.WaitForSecondsRealtime(2f);
        }
        yield return null;
    }
    UnityEngine.Object.Destroy(d);
    Log("done");
}
player.StartCoroutine(Run());
return "started";
