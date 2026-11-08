using UnityEngine;

/// <summary>
/// Gives every animated character in the scene a CharacterWetness while it rains: the player, residents, companions and
/// creatures, whether placed, spawned or generated with a raid. Looks every two seconds while it is raining; once on a
/// character the wetness dries by itself. Made by WorldRendering when RenderingSettings › Wet Characters is on.
/// </summary>
public sealed class WetCharacters : MonoBehaviour
{
    private RenderingSettings settings;
    private float nextScan;

    public void Initialise(RenderingSettings rules) => settings = rules;

    private void Update()
    {
        if (Time.time < nextScan)
        {
            return;
        }

        nextScan = Time.time + 2f;
        WeatherSystem weather = WeatherSystem.Active;
        if (weather == null || weather.Snowing || weather.Current.Rain <= 0.02f)
        {
            return;
        }

        foreach (Animator animator in FindObjectsByType<Animator>(FindObjectsInactive.Exclude))
        {
            GameObject character = animator.gameObject;
            // One per character: never inside or over another's (nested animators), or materials would wet twice.
            if (character.GetComponentInChildren<SkinnedMeshRenderer>() == null || character.GetComponentInParent<CharacterWetness>() != null
                || character.GetComponentInChildren<CharacterWetness>() != null)
            {
                continue;
            }

            character.AddComponent<CharacterWetness>().Initialise(settings);
        }
    }
}
