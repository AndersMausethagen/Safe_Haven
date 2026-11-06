using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A festival of the Haven's year: on its day of the year, between its hours, the residents leave what they are doing
/// and gather round a bonfire with music, saying the festival's lines, and whoever joins them there is given its boon.
/// Played by <see cref="Festivals"/> in the Haven.
/// </summary>
[CreateAssetMenu(fileName = "Festival", menuName = "Safe Haven/World/Festival")]
public sealed class FestivalDefinition : ScriptableObject
{
    [SerializeField] private string displayName = "Harvest Feast";
    [Tooltip("Day of the year it falls on (0 = the first day of the Season Cycle; its seasons' days added up).")]
    [SerializeField, Min(0)] private int dayOfYear = 15;
    [Tooltip("Hours of that day it lasts (from, to; up to 24).")]
    [SerializeField] private Vector2 hours = new Vector2(17f, 23f);
    [Tooltip("Said when it begins.")]
    [SerializeField, TextArea] private string announcement = "Tonight is the Harvest Feast. The Haven gathers at the fire.";
    [Tooltip("What residents say in passing while it lasts.")]
    [SerializeField, TextArea] private string[] lines = { };
    [SerializeField] private SoundDefinition music;
    [Tooltip("The boon for joining in at the fire.")]
    [SerializeField] private string boonName = "Harvest Cheer";
    [SerializeField] private List<StatBonus> boon = new List<StatBonus>();
    [SerializeField, Min(1f)] private float boonSeconds = 1200f;

    public string DisplayName => displayName;
    public int DayOfYear => dayOfYear;
    public Vector2 Hours => hours;
    public string Announcement => announcement;
    public IReadOnlyList<string> Lines => lines;
    public SoundDefinition Music => music;
    public string BoonName => boonName;
    public IReadOnlyList<StatBonus> Boon => boon;
    public float BoonSeconds => boonSeconds;

    /// <summary>Whether it is on at that day of the year (a whole number of days since the year began) and hour.</summary>
    public bool IsOn(int day, float hour) => day == dayOfYear && hour >= hours.x && hour < hours.y;

    public void Configure(string name, int day, Vector2 from, string said, string[] spoken, SoundDefinition tune,
        string boonCalled, List<StatBonus> bonuses, float seconds)
    {
        displayName = name;
        dayOfYear = day;
        hours = from;
        announcement = said;
        lines = spoken;
        music = tune;
        boonName = boonCalled;
        boon = bonuses;
        boonSeconds = seconds;
    }
}
