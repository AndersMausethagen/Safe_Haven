using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Puts the player's swings and skills on their clips' own pace (the user, 2026-11-25: "smooth, responsive and satisfying combat
/// animations are more important than keeping animation speed within a certain threshold to maintain dps ... the animations directly
/// from the pack look and feel much smoother out of the box"). Before, each attack's Hit Delay and Recovery Time were held where damage
/// per second wanted them and the clip was stretched to fit: wind-ups at 0.2-0.8x, strikes at 1.25x, follow-throughs at 0.2-1.8x.
/// Now the timing follows the clip: every phase plays at 1x, and the blow and the chain point stay on the same clip frames they were
/// fitted to (the contact found and checked by eye, CombatSwingTiming), so only when they happen moves:
/// <list type="bullet">
/// <item>an attack: Hit Delay = the clip time its blow landed on, Strike Lead = the clip time its strike covered, Recovery Time = the
/// clip time it recovered at; Animation, Strike and Follow-through Speed = 1;</item>
/// <item>a skill played faster or slower (Skill › Animation Speed): everything timed on its clip (cast time, lunge, the blow, the cry's
/// delay, its effects' cast cues) stretched by that speed, then Animation Speed = 1.</item>
/// </list>
/// Damage per second moves with the cadence; the user retunes damage later. Monsters' attacks keep their slowed wind-ups: those are the
/// player's windows to dodge. Run once from the menu; CombatSwingTimingTests keeps the rule.
/// </summary>
public static class CombatClipPace
{
    private const string PlayerPrefab = "Assets/_Project/Prefabs/Characters/PlayerArmature.prefab";

    /// <summary>True when the attack plays its clip at its own pace in every phase.</summary>
    public static bool AtClipPace(AttackDefinition attack) =>
        Mathf.Abs(attack.AnimationSpeed - 1f) < 0.001f && Mathf.Abs(attack.StrikeSpeed - 1f) < 0.001f && Mathf.Abs(attack.FollowThroughSpeed - 1f) < 0.001f;

    /// <summary>Every attack the player swings, throws or casts: weapons' (all their attacks), runes' arts, the finishing stab, the
    /// shield bash on the player, and skills' blows.</summary>
    public static HashSet<AttackDefinition> PlayerAttacks()
    {
        var attacks = new HashSet<AttackDefinition>();
        void From(Object source)
        {
            if (source == null)
            {
                return;
            }

            SerializedProperty field = new SerializedObject(source).GetIterator();
            while (field.Next(true))
            {
                if (field.propertyType == SerializedPropertyType.ObjectReference && field.objectReferenceValue is AttackDefinition attack)
                {
                    attacks.Add(attack);
                }
            }
        }

        foreach (string guid in AssetDatabase.FindAssets("t:WeaponDefinition"))
        {
            From(AssetDatabase.LoadAssetAtPath<WeaponDefinition>(AssetDatabase.GUIDToAssetPath(guid)));
        }

        foreach (string guid in AssetDatabase.FindAssets("t:ItemDefinition"))
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (item != null && item.RuneArt != null)
            {
                attacks.Add(item.RuneArt);
            }
        }

        From(Resources.Load<CombatTechniqueSettings>("CombatTechniqueSettings"));
        GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
        if (player != null)
        {
            foreach (MonoBehaviour behaviour in player.GetComponentsInChildren<MonoBehaviour>(true))
            {
                From(behaviour);
            }
        }

        foreach (SkillDefinition skill in Skills())
        {
            if (skill.Attack != null)
            {
                attacks.Add(skill.Attack);
            }
        }

        attacks.Remove(null);
        return attacks;
    }

    public static IEnumerable<SkillDefinition> Skills() => AssetDatabase.FindAssets("t:SkillDefinition")
        .Select(guid => AssetDatabase.LoadAssetAtPath<SkillDefinition>(AssetDatabase.GUIDToAssetPath(guid))).Where(skill => skill != null);

    [MenuItem("Safe Haven/Tools/Combat/Play Swings At Their Clips' Pace")]
    private static void FromMenu() => Debug.Log(Apply());

    /// <summary>Re-times every player attack and skill to its clip's pace (see the class). Returns what changed, one line each.</summary>
    public static string Apply()
    {
        var log = new List<string>();
        // Skills first: a skill's own blow is stretched with its clip, before its attack is put on the attack's own pace.
        foreach (SkillDefinition skill in Skills())
        {
            string line = SkillAtPace(skill);
            if (line != null)
            {
                log.Add(line);
            }
        }

        foreach (AttackDefinition attack in PlayerAttacks().OrderBy(a => a.name))
        {
            string line = AttackAtPace(attack);
            if (line != null)
            {
                log.Add(line);
            }
        }

        AssetDatabase.SaveAssets();
        return log.Count + " re-timed\n" + string.Join("\n", log);
    }

    // The same clip frames at 1x: the blow, the end of the wind-up and the chain point stay where they were in the clip.
    private static string AttackAtPace(AttackDefinition attack)
    {
        if (AtClipPace(attack))
        {
            return null;
        }

        float hit = attack.HitDelay;
        float recovery = attack.RecoveryTime;
        string speeds = $"{attack.AnimationSpeed:0.00}/{attack.StrikeSpeed:0.00}/{attack.FollowThroughSpeed:0.00}x";
        float lead = Mathf.Min(attack.StrikeLead, hit);
        float clipAtHit = attack.AnimationSpeed * (hit - lead) + attack.StrikeSpeed * lead;
        float clipLead = attack.StrikeSpeed * lead;
        float clipAtRecovery = clipAtHit + attack.FollowThroughSpeed * Mathf.Max(0f, attack.RecoveryTime - hit);
        var data = new SerializedObject(attack);
        data.FindProperty("hitDelay").floatValue = Round(clipAtHit);
        data.FindProperty("strikeLead").floatValue = Round(clipLead);
        data.FindProperty("recoveryTime").floatValue = Round(Mathf.Max(clipAtRecovery, clipAtHit + 0.05f));
        data.FindProperty("animationSpeed").floatValue = 1f;
        data.FindProperty("strikeSpeed").floatValue = 1f;
        data.FindProperty("followThroughSpeed").floatValue = 1f;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(attack);
        return $"{attack.name}: hit {hit:0.00}->{clipAtHit:0.00}, recovery {recovery:0.00}->{Mathf.Max(clipAtRecovery, clipAtHit + 0.05f):0.00} (was {speeds})";
    }

    private static string SkillAtPace(SkillDefinition skill)
    {
        var data = new SerializedObject(skill);
        SerializedProperty speed = data.FindProperty("animationSpeed");
        float s = speed.floatValue;
        if (Mathf.Abs(s - 1f) < 0.001f)
        {
            return null;
        }

        foreach (string field in new[] { "castTime", "lungeDuration", "foesDelay" })
        {
            SerializedProperty property = data.FindProperty(field);
            property.floatValue = Round(property.floatValue * s);
        }

        // Its effects' cues timed from the cast; with no blow (a cry, a blessing) the release comes with the cast, so those too.
        ScaleCues(data.FindProperty("look.cast"), s);
        if (skill.Attack == null)
        {
            ScaleCues(data.FindProperty("look.release"), s);
        }

        speed.floatValue = 1f;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(skill);

        if (skill.Attack != null)
        {
            var attack = new SerializedObject(skill.Attack);
            foreach (string field in new[] { "hitDelay", "recoveryTime", "strikeLead" })
            {
                SerializedProperty property = attack.FindProperty(field);
                property.floatValue = Round(property.floatValue * s);
            }

            attack.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(skill.Attack);
        }

        return $"{skill.name}: played at {s:0.00}x, now 1x (its times x{s:0.00})";
    }

    private static void ScaleCues(SerializedProperty cues, float s)
    {
        if (cues == null || !cues.isArray)
        {
            return;
        }

        for (int i = 0; i < cues.arraySize; i++)
        {
            SerializedProperty cue = cues.GetArrayElementAtIndex(i);
            foreach (string field in new[] { "delay", "stepDelay" })
            {
                SerializedProperty property = cue.FindPropertyRelative(field);
                property.floatValue = Round(property.floatValue * s);
            }
        }
    }

    private static float Round(float seconds) => Mathf.Round(seconds * 1000f) / 1000f;
}
