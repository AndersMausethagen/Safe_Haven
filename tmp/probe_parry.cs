var player = Player.Current;
if (player == null) throw new System.Exception("no player");
string log = "D:/Safe Haven/Safe Haven/tmp/probe_parry.txt";
System.IO.File.WriteAllText(log, "");
var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/_Project/Prefabs/Enemies/Draug.prefab");
var cam = UnityEngine.Camera.main.transform.forward; cam.y = 0f; cam.Normalize();
UnityEngine.Vector3 at = player.transform.position + cam * 4f;
UnityEngine.AI.NavMeshHit nh;
if (UnityEngine.AI.NavMesh.SamplePosition(at, out nh, 4f, UnityEngine.AI.NavMesh.AllAreas)) at = nh.position;
var draug = UnityEngine.Object.Instantiate(prefab, at, UnityEngine.Quaternion.LookRotation(-cam));
draug.name = "Probe Draug";
var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
var T = typeof(AttackTelegraph);
System.Collections.IEnumerator Run()
{
    yield return new UnityEngine.WaitForSeconds(0.5f);
    var ai = draug.GetComponent<MonsterAI>();
    var tg = draug.GetComponent<AttackTelegraph>();
    ai.Engage(player.Health);
    float lastStart = -1f; bool lastCued = false; int attacks = 0; bool shot = false; float until = UnityEngine.Time.realtimeSinceStartup + 25f;
    while (attacks < 4 && UnityEngine.Time.realtimeSinceStartup < until)
    {
        var marker = (UnityEngine.Transform)T.GetField("marker", flags).GetValue(tg);
        if (marker != null && marker.gameObject.activeSelf)
        {
            float start = (float)T.GetField("startTime", flags).GetValue(tg);
            var atk = (AttackDefinition)T.GetField("attack", flags).GetValue(tg);
            var line = (UnityEngine.MeshFilter)T.GetField("lineFilter", flags).GetValue(tg);
            bool cued = (bool)T.GetField("cued", flags).GetValue(tg);
            float elapsed = UnityEngine.Time.time - start;
            if (!UnityEngine.Mathf.Approximately(start, lastStart))
            {
                lastStart = start; attacks++; lastCued = false;
                System.IO.File.AppendAllText(log, $"attack {atk?.name} delay {atk?.HitDelay:F2} cueAt {(float)T.GetField("cueAt", flags).GetValue(tg):F3} atMe {(bool)T.GetField("atMe", flags).GetValue(tg)} line {line.gameObject.activeSelf} scale {line.transform.localScale.x:F3} range {marker.localScale.x:F2} holding {ai.IsHoldingStrike} hold {ai.ActiveAction?.holdChance}\n");
            }
            if (cued && !lastCued) { lastCued = true; System.IO.File.AppendAllText(log, $"  cued at {elapsed:F3} (to contact {atk.HitDelay - elapsed:F3})\n"); }
            if (!shot && line.gameObject.activeSelf && elapsed > atk.HitDelay * 0.55f) { shot = true; UnityEngine.ScreenCapture.CaptureScreenshot("D:/Safe Haven/Safe Haven/tmp/probe_parry_line.png"); System.IO.File.AppendAllText(log, $"  shot at {elapsed:F3}\n"); }
        }
        yield return null;
    }
    // Knockback and the merged stop: a sword slash, then two hits of one swing.
    ai.enabled = false;
    yield return new UnityEngine.WaitForSecondsRealtime(1f);
    var slash = UnityEditor.AssetDatabase.LoadAssetAtPath<AttackDefinition>("Assets/_Project/Data/Combat/Attacks/SwordSlash.asset");
    var body = draug.GetComponent<Damageable>();
    UnityEngine.Vector3 dir = (draug.transform.position - player.transform.position); dir.y = 0f; dir.Normalize();
    UnityEngine.Vector3 before = draug.transform.position;
    var hit = new DamageInfo(player.Combatant, slash, 1f, DamageType.Physical, false, before + UnityEngine.Vector3.up, dir, 0f, HitTechnique.Melee);
    var result = body.ApplyDamage(hit);
    float expected = HitReaction.KnockbackFor(hit, result, body);
    float began = UnityEngine.Time.realtimeSinceStartup; float slowFor = 0f;
    while (UnityEngine.Time.realtimeSinceStartup - began < 1.2f) { if (UnityEngine.Time.timeScale < 0.5f) slowFor += UnityEngine.Time.unscaledDeltaTime; yield return null; }
    UnityEngine.Vector3 moved = draug.transform.position - before; moved.y = 0f;
    System.IO.File.AppendAllText(log, $"slash impact {slash.Impact} expected {expected:F3} x scale -> moved {moved.magnitude:F3}; stop {slowFor:F3}s\n");
    began = UnityEngine.Time.realtimeSinceStartup; slowFor = 0f;
    body.ApplyDamage(hit); body.ApplyDamage(hit); body.ApplyDamage(hit);
    while (UnityEngine.Time.realtimeSinceStartup - began < 1.2f) { if (UnityEngine.Time.timeScale < 0.5f) slowFor += UnityEngine.Time.unscaledDeltaTime; yield return null; }
    System.IO.File.AppendAllText(log, $"three hits at once: stop {slowFor:F3}s (ceiling {CombatFeelSettings.Current.StopCeiling})\n");
    UnityEngine.Object.Destroy(draug);
    System.IO.File.AppendAllText(log, "done\n");
}
player.StartCoroutine(Run());
return "started at " + at;
