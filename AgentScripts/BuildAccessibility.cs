using UnityEditor;

/// <summary>
/// The captions of the sounds worth knowing about without hearing them (GDD §18.5). Run once.
/// Run: unity command script run -- --file AgentScripts/BuildAccessibility.cs --entry BuildAccessibility.Run
/// </summary>
public static class BuildAccessibility
{
    public static string Run()
    {
        int set = 0;
        foreach ((string sound, string caption) in new[]
        {
            ("Spotted", "Something has seen you"),
            ("Suspicious", "Something is listening"),
            ("TrollStep", "Heavy footsteps"),
            ("War Horn", "A war horn"),
            ("Ward Toll", "The ward tolls"),
            ("Ward Falls", "The ward falls"),
            ("Ward Mended", "The ward is whole again"),
            ("Guard Break", "Your guard breaks"),
            ("Out of Stamina", "Out of breath"),
            ("Counter Ready", "Counter ready"),
            ("Tree Crack", "A tree is coming down"),
            ("Thunder", "Thunder"),
            ("Crow Wings", "Crows take flight"),
        })
        {
            var asset = AssetDatabase.LoadAssetAtPath<SoundDefinition>($"Assets/_Project/Audio/Sounds/Library/{sound}.asset");
            if (asset == null)
            {
                throw new System.Exception("No sound: " + sound);
            }

            var so = new SerializedObject(asset);
            so.FindProperty("caption").stringValue = caption;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            set++;
        }

        AssetDatabase.SaveAssets();
        return set + " captions";
    }
}
