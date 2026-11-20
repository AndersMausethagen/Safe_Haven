const string sfx = "Assets/_Project/Audio/SFX/Safe Haven Combat/";
const string lib = "Assets/_Project/Audio/Sounds/Library/";
var log = new System.Text.StringBuilder();
SoundDefinition Make(string name, string template, int takes, bool positional, float volume, int most, float gap)
{
    var clips = new System.Collections.Generic.List<AudioClip>();
    for (int i = 1; i <= takes; i++)
    {
        string wav = sfx + name + " " + i + ".wav";
        UnityEditor.AssetDatabase.ImportAsset(wav, UnityEditor.ImportAssetOptions.ForceSynchronousImport);
        var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(wav);
        if (clip == null) throw new System.Exception("no clip " + wav);
        clips.Add(clip);
    }
    string path = lib + name + ".asset";
    var sound = UnityEditor.AssetDatabase.LoadAssetAtPath<SoundDefinition>(path);
    if (sound == null)
    {
        sound = UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<SoundDefinition>(lib + template + ".asset"));
        UnityEditor.AssetDatabase.CreateAsset(sound, path);
    }
    var so = new UnityEditor.SerializedObject(sound);
    so.FindProperty("displayName").stringValue = name;
    var list = so.FindProperty("clips");
    list.arraySize = clips.Count;
    for (int i = 0; i < clips.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
    so.FindProperty("positional").boolValue = positional;
    so.FindProperty("volume").floatValue = volume;
    so.FindProperty("maxAtOnce").intValue = most;
    so.FindProperty("minimumGap").floatValue = gap;
    if (takes == 1) { so.FindProperty("volumeVariation").floatValue = 0f; so.FindProperty("pitchRange").vector2Value = Vector2.one; }
    so.ApplyModifiedPropertiesWithoutUndo();
    UnityEditor.EditorUtility.SetDirty(sound);
    log.Append(name + " (" + clips.Count + ", from " + template + ") ");
    return sound;
}
var axeSwing = UnityEditor.AssetDatabase.LoadAssetAtPath<SoundDefinition>(lib + "Axe Swing.asset");
float swingVolume = axeSwing.Volume;
var thrust = Make("Thrust Swing", "Axe Swing", 4, true, swingVolume, 2, 0.05f);
var blunt = Make("Blunt Swing", "Axe Swing", 3, true, swingVolume, 2, 0.05f);
var great = Make("Great Blade Swing", "Axe Swing", 3, true, swingVolume, 2, 0.05f);
var block = UnityEditor.AssetDatabase.LoadAssetAtPath<SoundDefinition>(lib + "Block.asset");
var interrupt = Make("Interrupt Break", "Block", 4, true, block.Volume, 2, 0.05f);
var charge = Make("Charge Rise", "Counter Ready", 1, false, 0.35f, 1, 0.1f);
var won = Make("Fight Won", "Counter Ready", 1, false, 0.55f, 1, 1f);
var weapons = new System.Collections.Generic.Dictionary<string, SoundDefinition> {
  {"FangDagger", thrust}, {"Spear", thrust}, {"CopperSpear", thrust}, {"FireHardenedSpear", thrust}, {"MoonsilverSpear", thrust},
  {"IronWarhammer", blunt}, {"KnottedClub", blunt}, {"TrollstoneMaul", blunt},
  {"IronGreatsword", great}, {"MoonsilverGreatsword", great} };
foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:WeaponDefinition", new[] { "Assets/_Project/Data" }))
{
    var weapon = UnityEditor.AssetDatabase.LoadAssetAtPath<WeaponDefinition>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
    if (weapon == null || !weapons.ContainsKey(weapon.name)) continue;
    var so = new UnityEditor.SerializedObject(weapon);
    var was = so.FindProperty("swingSound").objectReferenceValue;
    so.FindProperty("swingSound").objectReferenceValue = weapons[weapon.name];
    so.ApplyModifiedPropertiesWithoutUndo();
    UnityEditor.EditorUtility.SetDirty(weapon);
    log.Append("| " + weapon.name + ": " + (was != null ? was.name : "none") + " -> " + weapons[weapon.name].name + " ");
    weapons.Remove(weapon.name);
}
if (weapons.Count > 0) log.Append("| NOT FOUND: " + string.Join(",", weapons.Keys));
var feel = CombatFeelSettings.Current;
var fso = new UnityEditor.SerializedObject(feel);
fso.FindProperty("fightWonSound").objectReferenceValue = won;
fso.FindProperty("chargeRiseSound").objectReferenceValue = charge;
fso.ApplyModifiedPropertiesWithoutUndo();
UnityEditor.EditorUtility.SetDirty(feel);
var audio = AudioManager.Sounds != null ? AudioManager.Sounds : null;
string audioPath = null;
foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:AudioSettingsDefinition")) { audioPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guid); log.Append("| audio settings " + audioPath); }
var audioAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioSettingsDefinition>(audioPath);
var aso = new UnityEditor.SerializedObject(audioAsset);
aso.FindProperty("interrupt").objectReferenceValue = interrupt;
aso.ApplyModifiedPropertiesWithoutUndo();
UnityEditor.EditorUtility.SetDirty(audioAsset);
UnityEditor.AssetDatabase.SaveAssets();
return log.ToString();
