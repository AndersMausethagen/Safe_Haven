var player = Player.Current;
if (player == null) throw new System.Exception("no player");
string log = "D:/Safe Haven/Safe Haven/tmp/probe_ai2.txt";
System.IO.File.WriteAllText(log, "");
void Log(string s) => System.IO.File.AppendAllText(log, s + "\n");
var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static;
var cam = UnityEngine.Camera.main.transform.forward; cam.y = 0f; cam.Normalize();
var side = UnityEngine.Vector3.Cross(UnityEngine.Vector3.up, cam);
UnityEngine.Vector3 Snap(UnityEngine.Vector3 p) { UnityEngine.AI.NavMeshHit h; return UnityEngine.AI.NavMesh.SamplePosition(p, out h, 5f, UnityEngine.AI.NavMesh.AllAreas) ? h.position : p; }
var wolfPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/_Project/Prefabs/Enemies/ForestBittenWolf.prefab");
var spitPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/_Project/Prefabs/Enemies/BogSpitter.prefab");
var flankersField = typeof(MonsterAI).GetField("flankers", flags);
var pointsField = typeof(MonsterAI).GetField("searchPoints", flags);
void Go(UnityEngine.Vector3 p) { var cc = player.GetComponent<UnityEngine.CharacterController>(); if (cc) cc.enabled = false; player.transform.position = p + UnityEngine.Vector3.up * 0.3f; if (cc) cc.enabled = true; }
System.Collections.IEnumerator Run()
{
    // 1. The flanker: three wolves at a player standing still, facing the camera's way.
    var wolves = new MonsterAI[3];
    for (int k = 0; k < 3; k++) wolves[k] = UnityEngine.Object.Instantiate(wolfPrefab, Snap(player.transform.position + cam * 6f + side * (k - 1) * 2.5f), UnityEngine.Quaternion.identity).GetComponent<MonsterAI>();
    yield return new UnityEngine.WaitForSeconds(0.3f);
    foreach (var w in wolves) w.Engage(player.Health);
    player.transform.rotation = UnityEngine.Quaternion.LookRotation(cam);
    float until = UnityEngine.Time.time + 15f; bool seen = false; float behindBest = 0f;
    while (UnityEngine.Time.time < until)
    {
        var dict = (System.Collections.IDictionary)flankersField.GetValue(null);
        if (dict.Count > 0 && !seen) { seen = true; Log($"flanker taken at {UnityEngine.Time.time:F1}"); }
        foreach (var w in wolves)
        {
            var to = w.transform.position - player.transform.position; to.y = 0f;
            float angle = UnityEngine.Vector3.Angle(player.transform.forward, to);
            if (angle > 120f && to.magnitude < 5f) behindBest = UnityEngine.Mathf.Max(behindBest, angle);
        }
        yield return new UnityEngine.WaitForSeconds(0.2f);
    }
    Log($"flank: role seen {seen}, a wolf stood behind (angle {behindBest:F0}°)");
    foreach (var w in wolves) UnityEngine.Object.Destroy(w.gameObject);
    yield return new UnityEngine.WaitForSeconds(0.5f);

    // 2. The search: a wolf chases, the player runs on east out of sight; where does it look?
    var hunter = UnityEngine.Object.Instantiate(wolfPrefab, Snap(player.transform.position + cam * 8f), UnityEngine.Quaternion.identity).GetComponent<MonsterAI>();
    yield return new UnityEngine.WaitForSeconds(0.3f);
    hunter.Engage(player.Health);
    var start = player.transform.position;
    // Walk the player east at 4 m/s for 2 s so the wolf sees the way, then jump out of sight further on.
    for (int i = 0; i < 20; i++) { Go(Snap(start + side * (0.4f * i))); yield return new UnityEngine.WaitForSeconds(0.1f); }
    Go(Snap(start + side * 60f));
    float wait = UnityEngine.Time.time + 8f;
    while (UnityEngine.Time.time < wait && hunter.CurrentState != MonsterAI.State.Investigating) yield return null;
    var pts = (System.Collections.IList)pointsField.GetValue(hunter);
    string where = ""; foreach (var p in pts) { var v = (UnityEngine.Vector3)p; where += $"({(v - start).x:F0},{(v - start).z:F0}) "; }
    Log($"search: state {hunter.CurrentState}, points from the start {where} (east is {side})");
    UnityEngine.Object.Destroy(hunter.gameObject);
    Go(start);
    yield return new UnityEngine.WaitForSeconds(1f);

    // 3. A shooter: where it stands after each spit.
    var spitter = UnityEngine.Object.Instantiate(spitPrefab, Snap(player.transform.position + cam * 9f), UnityEngine.Quaternion.identity).GetComponent<MonsterAI>();
    yield return new UnityEngine.WaitForSeconds(0.3f);
    spitter.Engage(player.Health);
    int shots = 0; var spots = "";
    spitter.AttackStarted += () => { shots++; spots += $"{spitter.transform.position.x:F1},{spitter.transform.position.z:F1} "; };
    yield return new UnityEngine.WaitForSeconds(20f);
    Log($"spitter: {shots} shots, from {spots}");
    UnityEngine.Object.Destroy(spitter.gameObject);
    Log("done");
}
player.StartCoroutine(Run());
return "started";
