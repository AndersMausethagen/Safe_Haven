using UnityEditor;

/// <summary>
/// Who can take a finishing blow: the person-shaped creatures, whom the two bodies line up with. Run once.
/// Run: unity command script run -- --file AgentScripts/BuildFinishingBlow.cs --entry BuildFinishingBlow.Run
/// </summary>
public static class BuildFinishingBlow
{
    public static string Run()
    {
        int set = 0;
        foreach (string name in new[] { "Draug", "BarrowShieldbearer" })
        {
            var monster = AssetDatabase.LoadAssetAtPath<MonsterDefinition>($"Assets/_Project/Data/Monsters/{name}.asset");
            var so = new SerializedObject(monster);
            so.FindProperty("canBeFinished").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(monster);
            set++;
        }

        // The stab: this clip's hand reaches furthest at 0.28 s, where every rush lands, so at its own speed it needs no fitting.
        const string stabPath = "Assets/_Project/Data/Combat/Attacks/Finishing Stab.asset";
        var stab = AssetDatabase.LoadAssetAtPath<AttackDefinition>(stabPath);
        if (stab == null)
        {
            stab = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<AttackDefinition>("Assets/_Project/Data/Combat/Attacks/Combos/Sword Rush.asset"));
            AssetDatabase.CreateAsset(stab, stabPath);
        }

        var look = new SerializedObject(stab);
        look.FindProperty("animationState").stringValue = "1H Stab";
        look.FindProperty("animationSpeed").floatValue = 1f;
        look.FindProperty("followThroughSpeed").floatValue = 1f;
        look.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(stab);
        var rules = new SerializedObject(CombatTechniqueSettings.Current);
        rules.FindProperty("finishStab").objectReferenceValue = stab;
        rules.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(CombatTechniqueSettings.Current);

        AssetDatabase.SaveAssets();
        return set + " can take a finishing blow";
    }
}
