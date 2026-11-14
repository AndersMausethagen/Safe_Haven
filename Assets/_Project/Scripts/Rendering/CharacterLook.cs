using UnityEngine;

/// <summary>
/// The characters' look at runtime. Their materials wear the character variant of Lit (See-Through) (CharacterShading.hlsl:
/// a rim from the sky and sun so figures read from the camera's height, light through skin, never dissolved by the
/// see-through circle), set on the material assets by AgentScripts/BuildCharacterLook.cs, as a build keeps only the
/// keyword sets its materials use; this sets its strengths, and while it rains gives every animated character (the
/// player, residents, companions, creatures, placed, spawned or made with a raid) a CharacterWetness, looking every five
/// seconds. Made by WorldRendering; numbers in RenderingSettings › Characters and › Wet characters.
/// </summary>
public sealed class CharacterLook : MonoBehaviour
{
    private static readonly int RimId = Shader.PropertyToID("_SafeHavenCharacterRim");
    private static readonly int ScatterId = Shader.PropertyToID("_SafeHavenSkinScatter");
    private static readonly string[] SkinNames = { "_Body_0", "_Head_", "Huldra Body", "Huldra Head", "goblin", "Troll Monster" };

    private RenderingSettings settings;
    private float nextScan;
    // Animators already looked at: each is decided once, not searched up and down its hierarchy every look while it rains.
    private readonly System.Collections.Generic.HashSet<Animator> seen = new System.Collections.Generic.HashSet<Animator>();

    public void Initialise(RenderingSettings rules)
    {
        settings = rules;
        SetStrengths();
    }

    private void SetStrengths()
    {
        Shader.SetGlobalVector(RimId, new Vector4(settings.RimStrength, settings.RimSharpness, settings.RimAtNight, settings.RimSunShare));
        Color scatter = settings.SkinScatter.linear;
        Shader.SetGlobalVector(ScatterId, new Vector4(scatter.r, scatter.g, scatter.b, settings.SkinScatterStrength));
    }

    /// <summary>Whether a character material counts as skin (light passes into it) or cloth, hair and hide (rim only).</summary>
    public static bool IsSkin(string materialName)
    {
        if (materialName.IndexOf("Cloth", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return false;
        }

        foreach (string part in SkinNames)
        {
            if (materialName.IndexOf(part, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private void Update()
    {
        if (Time.time < nextScan)
        {
            return;
        }

        nextScan = Time.time + 5f; // characters seldom come and go: a look every few seconds
        SetStrengths();
        WeatherSystem weather = WeatherSystem.Active;
        if (!settings.WetCharacters || weather == null || weather.Snowing || weather.Current.Rain <= 0.02f)
        {
            return;
        }

        seen.RemoveWhere(gone => gone == null);
        foreach (Animator animator in FindObjectsByType<Animator>(FindObjectsInactive.Exclude))
        {
            if (seen.Contains(animator))
            {
                continue;
            }

            // Not decided while it has no skinned body yet (one built in code a moment later): looked at again next time.
            GameObject character = animator.gameObject;
            if (character.GetComponentInChildren<SkinnedMeshRenderer>() == null)
            {
                continue;
            }

            seen.Add(animator);
            // One wetness per character: never inside or over another's (nested animators), or materials would wet twice.
            if (character.GetComponentInParent<CharacterWetness>() == null
                && character.GetComponentInChildren<CharacterWetness>() == null)
            {
                character.AddComponent<CharacterWetness>().Initialise(settings);
            }
        }
    }
}
