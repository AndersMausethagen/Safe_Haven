var impacts = new System.Collections.Generic.Dictionary<string, float> {
  {"WolfHamstringBite",0.7f},{"BoarGore",0.7f},{"BlightGore",0.7f},{"RootGrasp",0.7f},{"TongueLash",0.7f},{"ShamanShove",0.7f},{"TrollShove",0.7f},{"MonsterStrike",0.7f},{"HuldraClaw",0.7f},{"WidowBite",0.7f},
  {"DraugCleave",1.3f},{"ShieldbearerCleave",1.3f},{"KnightFireSlash",1.3f},{"KnightIceSlash",1.3f},{"KnightToxicSlash",1.3f},{"LostFinisher",1.3f},{"TrollRush",1.3f},
  {"GuardianSlam",1.4f},{"TrollSweep",1.4f},{"HeartrootSlam",1.4f},{"HeartrootSweep",1.4f},{"HeartrootChop",1.4f},
  {"DraugLunge",1.6f},{"ShieldRush",1.6f},{"BoarCharge",1.6f},{"LostRush",1.5f},{"LostHeavy",1.6f},
  {"KnightFireCharge",1.7f},{"KnightIceCharge",1.7f},{"KnightToxicCharge",1.7f},
  {"TrollStomp",1.8f},{"FrostStomp",1.8f},{"OldTrollCharge",1.8f},{"GuardianGroundSlam",1.8f},
  {"BergQuake",2.0f},{"HeartrootUproot",2.0f},{"HeartrootHammerfall",2.0f},{"HeartrootGroundSlam",2.0f} };
var pulls = new System.Collections.Generic.Dictionary<string, float> {
  {"TrollStomp",-1.0f},{"TrollSweep",-0.8f},{"GuardianGroundSlam",-1.0f},{"BergQuake",-1.0f},{"FrostStomp",-1.0f},{"BoarCharge",-1.0f},
  {"KnightFireCharge",-1.0f},{"KnightIceCharge",-1.0f},{"KnightToxicCharge",-1.0f},{"OldTrollCharge",-1.0f},{"DraugLunge",-0.6f},
  {"HeartrootGroundSlam",-1.0f},{"HeartrootHammerfall",-1.0f} };
var log = new System.Text.StringBuilder();
int changed = 0;
foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:AttackDefinition", new[] { "Assets/_Project/Data" }))
{
  string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
  var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<AttackDefinition>(path);
  if (asset == null || !impacts.ContainsKey(asset.name)) continue;
  var so = new UnityEditor.SerializedObject(asset);
  var impact = so.FindProperty("impact");
  var pull = so.FindProperty("pull");
  float wasImpact = impact.floatValue, wasPull = pull.floatValue;
  impact.floatValue = impacts[asset.name];
  if (pulls.ContainsKey(asset.name) && Mathf.Approximately(wasPull, 0f)) pull.floatValue = pulls[asset.name];
  if (so.ApplyModifiedPropertiesWithoutUndo()) { changed++; UnityEditor.EditorUtility.SetDirty(asset); }
  log.Append(asset.name + " " + wasImpact + "->" + impact.floatValue + (pulls.ContainsKey(asset.name) ? " pull " + wasPull + "->" + pull.floatValue : "") + "; ");
}
var feelAsset = CombatFeelSettings.Current;
var fso = new UnityEditor.SerializedObject(feelAsset);
var shake = fso.FindProperty("shakeWhenHurt");
log.Append(" | shakeWhenHurt " + shake.floatValue + "->0.4");
shake.floatValue = 0.4f;
fso.ApplyModifiedPropertiesWithoutUndo();
UnityEditor.EditorUtility.SetDirty(feelAsset);
UnityEditor.AssetDatabase.SaveAssets();
return "changed " + changed + ": " + log;
