using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The year as data: its seasons in order, how many days each lasts, and how each looks. Autumn turns the broadleaf trees
/// gold and red (Tree Wind materials marked Deciduous) and yellows the grass; leaf fall thins their crowns; frost whitens
/// leaves and grass. Each season blends into the next over its last days, so the year turns rather than switches.
/// Read by Seasons, which follows the days that pass at home.
/// </summary>
[CreateAssetMenu(fileName = "Season Cycle", menuName = "Safe Haven/World/Season Cycle")]
public sealed class SeasonCycle : ScriptableObject
{
    [Serializable]
    public sealed class Season
    {
        public string name = "Summer";
        [Min(1)] public int days = 6;
        [Tooltip("How far the broadleaf leaves have turned gold and red, and the grass yellowed (0-1).")]
        [Range(0f, 1f)] public float autumn;
        [Tooltip("How many of the broadleaf leaves have fallen (0-1).")]
        [Range(0f, 1f)] public float leafFall;
        [Tooltip("Frost on leaves and grass (0-1).")]
        [Range(0f, 1f)] public float frost;
        [Tooltip("Weather this season brings, in place of the scene's own (none: the scene's).")]
        public ClimateDefinition climate;
    }

    [SerializeField] private List<Season> seasons = new List<Season>();
    [Tooltip("Days at the end of each season over which it turns into the next.")]
    [SerializeField, Min(0)] private int turningDays = 2;

    public IReadOnlyList<Season> Seasons => seasons;

    public void Configure(List<Season> year, int turning)
    {
        seasons = year;
        turningDays = turning;
    }

    /// <summary>The season on a day, the one after it, and how far it has turned towards that one (0-1).</summary>
    public void At(float day, out Season season, out Season next, out float turned)
    {
        season = next = null;
        turned = 0f;
        int year = 0;
        foreach (Season one in seasons)
        {
            year += Mathf.Max(1, one.days);
        }

        if (seasons.Count == 0 || year == 0)
        {
            return;
        }

        float into = Mathf.Repeat(day, year);
        for (int i = 0; i < seasons.Count; i++)
        {
            int length = Mathf.Max(1, seasons[i].days);
            if (into < length || i == seasons.Count - 1)
            {
                season = seasons[i];
                next = seasons[(i + 1) % seasons.Count];
                float turning = Mathf.Min(turningDays, length);
                turned = turning > 0f ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(length - turning, length, into)) : 0f;
                return;
            }

            into -= length;
        }
    }

    /// <summary>The look on a day: autumn, leaf fall and frost, blended between seasons.</summary>
    public Vector3 LookAt(float day)
    {
        At(day, out Season season, out Season next, out float turned);
        if (season == null)
        {
            return Vector3.zero;
        }

        return Vector3.Lerp(new Vector3(season.autumn, season.leafFall, season.frost), new Vector3(next.autumn, next.leafFall, next.frost), turned);
    }
}
