var player = Player.Current;
if (player == null) throw new System.Exception("no player");
string log = "D:/Safe Haven/Safe Haven/tmp/probe_alarm.txt";
System.IO.File.WriteAllText(log, "");
void Log(string s) => System.IO.File.AppendAllText(log, s + "\n");
var cam = UnityEngine.Camera.main.transform.forward; cam.y = 0f; cam.Normalize();
var side = UnityEngine.Vector3.Cross(UnityEngine.Vector3.up, cam);
UnityEngine.Vector3 Snap(UnityEngine.Vector3 p) { UnityEngine.AI.NavMeshHit h; return UnityEngine.AI.NavMesh.SamplePosition(p, out h, 5f, UnityEngine.AI.NavMesh.AllAreas) ? h.position : p; }
var draugPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/_Project/Prefabs/Enemies/Draug.prefab");
System.Collections.IEnumerator Run()
{
    // A Draug that spots the player, and two more 12 m behind it that haven't.
    var caller = UnityEngine.Object.Instantiate(draugPrefab, Snap(player.transform.position + cam * 8f), UnityEngine.Quaternion.identity).GetComponent<MonsterAI>();
    var a = UnityEngine.Object.Instantiate(draugPrefab, Snap(player.transform.position + cam * 20f + side * 3f), UnityEngine.Quaternion.identity).GetComponent<MonsterAI>();
    var b = UnityEngine.Object.Instantiate(draugPrefab, Snap(player.transform.position + cam * 20f - side * 3f), UnityEngine.Quaternion.identity).GetComponent<MonsterAI>();
    yield return new UnityEngine.WaitForSeconds(0.5f);
    string first = "";
    caller.AttackStarted += () => { if (first == "") first = caller.CurrentAttack != null ? caller.CurrentAttack.name : "?"; };
    caller.Engage(player.Health);
    yield return new UnityEngine.WaitForSeconds(0.3f);
    Log($"just spotted: caller doing {caller.CurrentState} first strike '{first}', the others: {a.CurrentState}/{b.CurrentState} targets {(a.Target != null)}/{(b.Target != null)}");
    float until = UnityEngine.Time.time + 5f;
    while (UnityEngine.Time.time < until && (a.Target == null || b.Target == null)) yield return null;
    Log($"after the call: first strike was '{first}', the others engaged {(a.Target != null)}/{(b.Target != null)} at {UnityEngine.Time.time:F1}");
    foreach (var m in new[] { caller, a, b }) UnityEngine.Object.Destroy(m.gameObject);
    Log("done");
}
player.StartCoroutine(Run());
return "started";
