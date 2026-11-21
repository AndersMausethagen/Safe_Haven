using UnityEngine;

/// <summary>
/// A run's weather foretold (Docs/GameDesignResearch_2026-11-17.md W3: "rain already helps sneaking, but the player can't know it or plan
/// for it"): the weather a run opens in is picked from the raid's climate by the run's seed, so the briefing can tell it and the raid
/// (RaidSky) starts in it, on every game on the run (co-op). After that the sky turns by itself as before. The briefing's line says
/// what the weather does, read from the rules it names (Docs/BugPatterns.md guard 4: a shown number reads its rule).
/// </summary>
public static class RaidWeather
{
    // Rain at or above this muffles steps enough to be worth telling; above Drowns the ground gets wet enough to put fire out
    // (GroundHazard.Wet: wetness over 0.5, and wetness rises toward the rain).
    private const float TellsRain = 0.2f;
    private const float Drowns = 0.5f;
    private const float TellsMist = 0.35f;
    private const float TellsFog = 2f;

    /// <summary>The weather <paramref name="raid"/>'s run of <paramref name="seed"/> opens in; null when the raid has no climate of its own.</summary>
    public static WeatherDefinition Opening(RaidDefinition raid, int seed) => raid != null ? Opening(raid.Climate, seed) : null;

    /// <summary>The weather a run of <paramref name="seed"/> opens in under <paramref name="climate"/>, by its chances. Pure, for tests.</summary>
    public static WeatherDefinition Opening(ClimateDefinition climate, int seed)
    {
        if (climate == null)
        {
            return null;
        }

        var roll = new System.Random(RaidLayout.SeedFor(seed, "Opening Weather"));
        return climate.Pick((float)roll.NextDouble());
    }

    /// <summary>The sky's name as the briefing's chip says it: rain falls as snow in a snowing season.</summary>
    public static string NameFor(WeatherDefinition weather, bool snowing) =>
        weather == null ? "" : snowing && weather.State.Rain >= TellsRain ? "Snow" : weather.DisplayName;

    /// <summary>The kit icon for the sky (HearthKit): lightning, then rain, then mist or fog, else clear.</summary>
    public static string IconFor(WeatherDefinition weather)
    {
        if (weather == null)
        {
            return "Sun";
        }

        WeatherState state = weather.State;
        return state.LightningPerMinute > 0f ? "Bolt" : state.Rain >= TellsRain ? "Drop" : state.Mist >= TellsMist || state.Fog >= TellsFog ? "Eye" : "Sun";
    }

    /// <summary>
    /// The briefing's line for a run opening in <paramref name="weather"/>: its name and what it does, as the game's rules have it. Rain
    /// falls as snow in a snowing season (<paramref name="snowing"/>). Null for none.
    /// </summary>
    public static string BriefingLine(WeatherDefinition weather, bool snowing, PerceptionSettings perception)
    {
        if (weather == null)
        {
            return null;
        }

        WeatherState state = weather.State;
        string name = NameFor(weather, snowing);
        var line = new System.Text.StringBuilder($"The sky as you go: {name}.");
        if (state.Rain >= TellsRain && perception != null)
        {
            // Falling snow muffles steps as rain does (PlayerNoise reads the weather's rain either way).
            float carry = PerceptionRules.RainNoiseScale(state.Rain, perception);
            line.Append($" Your steps carry {carry:P0} as far");
            line.Append(state.Rain > Drowns ? ", and fire won't take." : ".");
        }

        if (state.Rain >= TellsRain && snowing)
        {
            line.Append(" The cold bites in the open.");
        }

        if (state.Fog >= TellsFog)
        {
            line.Append(" Creatures see less far in it.");
        }

        if (state.Mist >= TellsMist || state.Fog >= TellsFog)
        {
            line.Append(" Little can be seen far off.");
        }

        if (state.LightningPerMinute > 0f)
        {
            line.Append(" Lightning.");
        }

        line.Append(" It may turn.");
        return line.ToString();
    }
}
