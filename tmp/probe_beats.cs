var player = Player.Current;
if (player == null) throw new System.Exception("no player");
string log = "D:/Safe Haven/Safe Haven/tmp/probe_beats.txt";
System.IO.File.WriteAllText(log, "");
var cam = UnityEngine.Camera.main.transform.forward; cam.y = 0f; cam.Normalize();
var side = UnityEngine.Vector3.Cross(UnityEngine.Vector3.up, cam);
UnityEngine.Vector3 Snap(UnityEngine.Vector3 p) { UnityEngine.AI.NavMeshHit h; return UnityEngine.AI.NavMesh.SamplePosition(p, out h, 5f, UnityEngine.AI.NavMesh.AllAreas) ? h.position : p; }
var wolfPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/_Project/Prefabs/Enemies/ForestBittenWolf.prefab");
var draugPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/_Project/Prefabs/Enemies/Draug.prefab");
var slash = UnityEditor.AssetDatabase.LoadAssetAtPath<AttackDefinition>("Assets/_Project/Data/Combat/Attacks/SwordSlash.asset");
void Log(string s) => System.IO.File.AppendAllText(log, s + "\n");
string State() => GameStateManager.Current != null ? GameStateManager.Current.CurrentState.ToString() : "?";
DamageInfo Blow(UnityEngine.GameObject target, float amount)
{
    UnityEngine.Vector3 d = target.transform.position - player.transform.position; d.y = 0f;
    return new DamageInfo(player.Combatant, slash, amount, DamageType.Physical, false, target.transform.position + UnityEngine.Vector3.up, d.normalized, 0f, HitTechnique.Melee);
}
System.Collections.IEnumerator Run()
{
    // 1. Two wolves: the first kill is not the end, the second is.
    var a = UnityEngine.Object.Instantiate(wolfPrefab, Snap(player.transform.position + cam * 4f + side * 1.5f), UnityEngine.Quaternion.identity);
    var b = UnityEngine.Object.Instantiate(wolfPrefab, Snap(player.transform.position + cam * 4f - side * 1.5f), UnityEngine.Quaternion.identity);
    yield return new UnityEngine.WaitForSeconds(0.3f);
    a.GetComponent<MonsterAI>().Engage(player.Health); b.GetComponent<MonsterAI>().Engage(player.Health);
    yield return new UnityEngine.WaitForSeconds(1.2f);
    Log($"engaged: state {State()} a hunting {a.GetComponent<MonsterAI>().IsHuntingPlayerSide} b {b.GetComponent<MonsterAI>().IsHuntingPlayerSide}");
    a.GetComponent<Damageable>().ApplyDamage(Blow(a, 5f));
    yield return new UnityEngine.WaitForSecondsRealtime(0.6f);
    a.GetComponent<Damageable>().ApplyDamage(Blow(a, 9999f));
    float began = UnityEngine.Time.realtimeSinceStartup; float least = 1f; string seen = "";
    while (UnityEngine.Time.realtimeSinceStartup - began < 1f) { least = UnityEngine.Mathf.Min(least, UnityEngine.Time.timeScale); yield return null; }
    Log($"first kill: state {State()} slowest {least:F2}");
    b.GetComponent<Damageable>().ApplyDamage(Blow(b, 9999f));
    began = UnityEngine.Time.realtimeSinceStartup;
    while (UnityEngine.Time.realtimeSinceStartup - began < 1.2f) { seen += $"{UnityEngine.Time.timeScale:F2} "; yield return new UnityEngine.WaitForSecondsRealtime(0.05f); }
    Log($"last kill: state {State()} timeScale over 1.2 s: {seen}");
    // 2. Damage numbers: three blows at once on one body make one number.
    yield return new UnityEngine.WaitForSecondsRealtime(1.5f);
    var d = UnityEngine.Object.Instantiate(draugPrefab, Snap(player.transform.position + cam * 3f), UnityEngine.Quaternion.identity);
    yield return new UnityEngine.WaitForSeconds(0.3f);
    d.GetComponent<MonsterAI>().enabled = false;
    int Numbers() { int n = 0; foreach (var x in UnityEngine.Object.FindObjectsByType<DamageNumber>(UnityEngine.FindObjectsInactive.Exclude)) n++; return n; }
    yield return new UnityEngine.WaitForSecondsRealtime(1.2f);
    int before = Numbers();
    var body = d.GetComponent<Damageable>();
    body.ApplyDamage(Blow(d, 3f)); body.ApplyDamage(Blow(d, 4f)); body.ApplyDamage(Blow(d, 5f));
    yield return null;
    int after = Numbers();
    string text = "";
    foreach (var x in UnityEngine.Object.FindObjectsByType<DamageNumber>(UnityEngine.FindObjectsInactive.Exclude)) text += x.GetComponentInChildren<TMPro.TMP_Text>().text + "/" + x.transform.localScale.x.ToString("F4") + " ";
    Log($"numbers before {before} after three blows {after}: {text}");
    UnityEngine.Object.Destroy(d);
    // 3. A Draug out of frame, behind the camera's side of the player.
    yield return new UnityEngine.WaitForSecondsRealtime(1f);
    var behind = UnityEngine.Object.Instantiate(draugPrefab, Snap(player.transform.position - cam * 9f), UnityEngine.Quaternion.LookRotation(cam));
    yield return new UnityEngine.WaitForSeconds(0.3f);
    var view = UnityEngine.Camera.main;
    Log($"behind viewport {view.WorldToViewportPoint(behind.transform.position + UnityEngine.Vector3.up * 1.2f)}");
    behind.GetComponent<MonsterAI>().Engage(player.Health);
    var warnings = player.GetComponent<OffScreenWarnings>();
    float until = UnityEngine.Time.realtimeSinceStartup + 20f; bool shot = false;
    while (UnityEngine.Time.realtimeSinceStartup < until && !shot)
    {
        var strikes = AttackTelegraph.AtThePlayer;
        if (strikes.Count > 0 && strikes[0] != null && strikes[0].IsWarning)
        {
            var p = view.WorldToViewportPoint(behind.transform.position + UnityEngine.Vector3.up * 1.2f);
            int active = 0; foreach (var t in warnings.GetComponentsInChildren<UnityEngine.RectTransform>(true)) { }
            var canvas = UnityEngine.GameObject.Find("Off Screen Warnings");
            if (canvas != null) foreach (UnityEngine.Transform t in canvas.transform) if (t.gameObject.activeSelf) active++;
            if (strikes[0].Progress > 0.5f)
            {
                Log($"strike {strikes[0].Progress:F2} at viewport {p} offscreen {OffScreenWarnings.IsOffScreen(p)} chevrons shown {active}");
                UnityEngine.ScreenCapture.CaptureScreenshot("D:/Safe Haven/Safe Haven/tmp/probe_offscreen.png");
                shot = true;
            }
        }
        yield return null;
    }
    yield return new UnityEngine.WaitForSecondsRealtime(0.5f);
    UnityEngine.Object.Destroy(behind);
    Log("done");
}
player.StartCoroutine(Run());
return "started";
