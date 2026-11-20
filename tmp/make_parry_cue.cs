const string wav = "Assets/_Project/Audio/SFX/Safe Haven Combat/Parry Cue 1.wav";
const string path = "Assets/_Project/Audio/Sounds/Library/Parry Cue.asset";
UnityEditor.AssetDatabase.ImportAsset(wav, UnityEditor.ImportAssetOptions.ForceSynchronousImport);
var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(wav);
if (clip == null) throw new System.Exception("no clip");
var parry = UnityEditor.AssetDatabase.LoadAssetAtPath<SoundDefinition>("Assets/_Project/Audio/Sounds/Library/Parry.asset");
var cue = UnityEditor.AssetDatabase.LoadAssetAtPath<SoundDefinition>(path);
if (cue == null)
{
    cue = UnityEngine.Object.Instantiate(parry);
    UnityEditor.AssetDatabase.CreateAsset(cue, path);
}
var so = new UnityEditor.SerializedObject(cue);
so.FindProperty("displayName").stringValue = "Parry Cue";
var clips = so.FindProperty("clips");
clips.arraySize = 1;
clips.GetArrayElementAtIndex(0).objectReferenceValue = clip;
so.FindProperty("volume").floatValue = 0.22f;
so.FindProperty("volumeVariation").floatValue = 0f;
so.FindProperty("pitchRange").vector2Value = new Vector2(1f, 1f);
so.FindProperty("audibleDistance").floatValue = 25f;
so.FindProperty("maxAtOnce").intValue = 1;
so.FindProperty("minimumGap").floatValue = 0.1f;
so.ApplyModifiedPropertiesWithoutUndo();
UnityEditor.EditorUtility.SetDirty(cue);
var feel = CombatFeelSettings.Current;
var fso = new UnityEditor.SerializedObject(feel);
fso.FindProperty("parryCueSound").objectReferenceValue = cue;
fso.ApplyModifiedPropertiesWithoutUndo();
UnityEditor.EditorUtility.SetDirty(feel);
UnityEditor.AssetDatabase.SaveAssets();
return "cue " + UnityEditor.AssetDatabase.GetAssetPath(cue) + " clip " + clip.length + "s; feel lead " + feel.ParryCueLead + " sound " + (feel.ParryCueSound != null) + " lineAlpha " + feel.ParryLineAlpha;
