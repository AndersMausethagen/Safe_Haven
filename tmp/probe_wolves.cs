var player = Player.Current;
if (player == null) throw new System.Exception("no player");
string log = "D:/Safe Haven/Safe Haven/tmp/probe_wolves.txt";
System.IO.File.WriteAllText(log, "");
void Log(string s) => System.IO.File.AppendAllText(log, s + "\n");
var cam = UnityEngine.Camera.main.transform.forward; cam.y = 0f; cam.Normalize();
UnityEngine.Vector3 Snap(UnityEngine.Vector3 p) { UnityEngine.AI.NavMeshHit h; return UnityEngine.AI.NavMesh.SamplePosition(p, out h, 5f, UnityEngine.AI.NavMesh.AllAreas) ? h.position : p; }
var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/_Project/Prefabs/Enemies/WolfPack.prefab");
var pack = UnityEngine.Object.Instantiate(prefab, Snap(player.transform.position + cam * 7f), UnityEngine.Quaternion.identity);
var wolves = pack.GetComponentsInChildren<MonsterAI>();
var bites = new System.Collections.Generic.Dictionary<string, int>();
float lastBite = -1f; var gaps = new System.Collections.Generic.List<float>();
void OnHurt(DamageInfo info, DamageResult result)
{
    string who = info.Source != null ? info.Source.gameObject.name + ":" + System.Array.IndexOf(wolves, info.Source.GetComponent<MonsterAI>()) : "?";
    bites[who] = bites.TryGetValue(who, out int n) ? n + 1 : 1;
    if (lastBite > 0f) gaps.Add(UnityEngine.Time.time - lastBite);
    lastBite = UnityEngine.Time.time;
}
var counts = new int[wolves.Length]; var times = new System.Collections.Generic.List<float>();
for (int k = 0; k < wolves.Length; k++) { int idx = k; wolves[k].AttackStarted += () => { counts[idx]++; times.Add(UnityEngine.Time.time); }; }
System.Collections.IEnumerator Run()
{
    yield return new UnityEngine.WaitForSeconds(0.5f);
    foreach (var w in wolves) w.Engage(player.Health);
    float until = UnityEngine.Time.time + 40f; int close = 0, samples = 0;
    while (UnityEngine.Time.time < until)
    {
        foreach (var w in wolves)
        {
            if (w == null || w.CurrentState == MonsterAI.State.Dead) continue;
            samples++;
            if (UnityEngine.Vector3.Distance(w.transform.position, player.transform.position) < 1.6f) close++;
        }
        yield return new UnityEngine.WaitForSeconds(0.25f);
    }
    gaps.Sort();
    string by = ""; for (int k = 0; k < wolves.Length; k++) by += wolves[k].name + "=" + counts[k] + " ";
    for (int k = 1; k < times.Count; k++) gaps.Add(times[k] - times[k - 1]);
    gaps.Sort();
    Log($"40 s, {wolves.Length} wolves: strikes started {times.Count} by {by}");
    Log($"shortest gaps {(gaps.Count > 0 ? gaps[0].ToString("F2") : "-")} median {(gaps.Count > 0 ? gaps[gaps.Count / 2].ToString("F2") : "-")}; share of samples within 1.6 m {(samples > 0 ? (float)close / samples : 0f):P0}");
    // Kill the leader: the rest should rout.
    var packComp = pack.GetComponent<MonsterPack>();
    var leaderField = typeof(MonsterPack).GetField("leader", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    var leader = leaderField.GetValue(packComp) as MonsterAI;
    Log($"leader {(leader != null ? leader.name + " scale " + leader.transform.localScale.x.ToString("F2") : "none")}");
    if (leader != null)
    {
        var body = leader.GetComponent<Damageable>();
        var slash = UnityEditor.AssetDatabase.LoadAssetAtPath<AttackDefinition>("Assets/_Project/Data/Combat/Attacks/SwordSlash.asset");
        body.ApplyDamage(new DamageInfo(player.Combatant, slash, 99999f, DamageType.Physical, false, leader.transform.position + UnityEngine.Vector3.up, UnityEngine.Vector3.forward, 0f, HitTechnique.Melee));
        yield return new UnityEngine.WaitForSeconds(0.5f);
        string routed = ""; foreach (var w in wolves) if (w != null && w != leader) routed += w.name + (w.IsRoutedNow ? " routed" : " NOT routed") + "; ";
        Log("after the leader fell: " + routed);
    }
    yield return new UnityEngine.WaitForSecondsRealtime(1f);
    
    yield return new UnityEngine.WaitForSecondsRealtime(1f);
    Log("done");
}
player.StartCoroutine(Run());
return "started " + wolves.Length;
