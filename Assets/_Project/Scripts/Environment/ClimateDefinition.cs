using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>The weather a scene gets (Safe Haven: mostly fair; a raid region: foggier, stormier): which
/// <see cref="WeatherDefinition"/>s come up, how often (relative chance), the weather at start and how long a change
/// takes to blend in. Read-only at runtime; <see cref="WeatherSystem"/> asks it for the next weather.</summary>
[CreateAssetMenu(menuName = "Safe Haven/Environment/Climate", fileName = "Climate")]
public sealed class ClimateDefinition : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public WeatherDefinition weather;
        [Min(0f)] public float chance;
    }

    [SerializeField] private WeatherDefinition start;
    [SerializeField] private List<Entry> weathers = new List<Entry>();
    [Tooltip("Seconds a new weather takes to blend in.")]
    [SerializeField, Min(0f)] private float transitionSeconds = 45f;

    public WeatherDefinition Start => start;
    public float TransitionSeconds => transitionSeconds;
    public IReadOnlyList<Entry> Weathers => weathers;

    public void Configure(WeatherDefinition startWeather, float transition, params Entry[] entries)
    {
        start = startWeather;
        transitionSeconds = transition;
        weathers = new List<Entry>(entries);
    }

    /// <summary>The weather for a roll in [0, 1): weathers take up a share of the range by their chance.</summary>
    public WeatherDefinition Pick(float roll)
    {
        float total = 0f;
        foreach (Entry entry in weathers)
        {
            total += entry.weather != null ? entry.chance : 0f;
        }

        if (total <= 0f)
        {
            return start;
        }

        float target = Mathf.Clamp01(roll) * total;
        foreach (Entry entry in weathers)
        {
            if (entry.weather == null)
            {
                continue;
            }

            target -= entry.chance;
            if (target < 0f)
            {
                return entry.weather;
            }
        }

        for (int i = weathers.Count - 1; i >= 0; i--)
        {
            if (weathers[i].weather != null && weathers[i].chance > 0f)
            {
                return weathers[i].weather;
            }
        }

        return start;
    }
}
