using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The Haven's lands as the story has them (Docs/HavenWorldStory_2026-11-26.md): the mist over the outer Haven is the forest's
/// forgetting, and each region is a forgotten part of the hearth-keepers' village. Here: each region's own look (a colour filter
/// over its ground and its mist's colour) and the ring it belongs to, which opens with the story; how grey forgotten land is; how
/// thick the mist lies over forgotten land and how thin over remembered. A region is remembered when its boundary stone is lit
/// (Territory). Read by HavenLand. Lives at Resources/HavenRegionSettings.
/// </summary>
[CreateAssetMenu(menuName = "Safe Haven/Settings/Haven Region Settings", fileName = "HavenRegionSettings")]
public sealed class HavenRegionSettings : GameSettings<HavenRegionSettings>
{
    [Serializable]
    public sealed class RegionLook
    {
        [Tooltip("The region's name in the Haven's atlas.")]
        public string region;
        [Tooltip("When it can be remembered: 0 home (always), 1 the near ring (with the Cold Stones), 2 the outer ring (after the " +
                 "Heartroot), 3 the last (Act II's end).")]
        [Range(0, 3)] public int ring = 1;
        [Tooltip("A colour filter over its ground once remembered (1 is as painted): restriction gives each region its own feel.")]
        public Color filter = Color.white;
        [Tooltip("Its mist's colour (a multiplier over the mist's own).")]
        public Color mistTint = Color.white;
        [Tooltip("How thick its mist lies while forgotten, times the authored map's (1: as painted; above: thicker).")]
        [Range(0f, 2f)] public float forgottenMist = 1.2f;
        [Tooltip("At least this much mist while forgotten, wherever the authored map has none: forgotten land is never clear.")]
        [Range(0f, 1f)] public float forgottenFloor = 0.35f;
        [Tooltip("Shown when it is remembered, under its name.")]
        [TextArea(1, 3)] public string rememberedLine = "";
        [Tooltip("Lighting its boundary stone counts as using this, for quests (Use Object): a region's arc ends with remembering it.")]
        public QuestTargetDefinition stoneTarget;
    }

    [Serializable]
    public sealed class RingOpening
    {
        [Tooltip("The ring this opens (2 or 3; ring 1 opens with the territory itself, ring 0 is always open).")]
        [Range(2, 3)] public int ring = 2;
        [Tooltip("Open once this quest has started (or finished). Empty: not by a quest.")]
        public QuestDefinition questStarted;
        [Tooltip("Open once this person has come home. Empty: not by a person.")]
        public ResidentDefinition residentHome;
        [Tooltip("What a stone in a ring not yet open says.")]
        [TextArea(1, 3)] public string notYet = "The mist here is too thick to remember through. Not yet.";
    }

    [SerializeField] private List<RegionLook> regions = new List<RegionLook>();
    [SerializeField] private List<RingOpening> openings = new List<RingOpening>();

    [Header("Forgotten land")]
    [Tooltip("How grey forgotten land is (0 as painted, 1 grey).")]
    [SerializeField, Range(0f, 1f)] private float forgottenDesaturation = 0.7f;
    [Tooltip("How much darker forgotten land is.")]
    [SerializeField, Range(0f, 1f)] private float forgottenDarkening = 0.12f;
    [Tooltip("What forgotten land is washed toward.")]
    [SerializeField] private Color forgottenColor = new Color(0.78f, 0.82f, 0.88f);

    [Header("Remembered land")]
    [Tooltip("How much of the authored mist still lies over remembered land: thin wisps, not the forgetting.")]
    [SerializeField, Range(0f, 1f)] private float rememberedMist = 0.2f;
    [Tooltip("Seconds the mist takes to draw back from a region as it is remembered.")]
    [SerializeField, Min(0.5f)] private float drawBackSeconds = 10f;
    [Header("Worked places")]
    [Tooltip("A worked place in forgotten land (a relit clamp, smoking) keeps the land round it in mind: this much remembered at its " +
             "heart (1 as if its region were), out to Kept Radius metres, so its smoke and its work show through the forgetting.")]
    [SerializeField, Range(0f, 1f)] private float keptRemembered = 0.7f;
    [SerializeField, Min(5f)] private float keptRadius = 32f;
    [Tooltip("Metres over which one region's look and mist fade into the next's.")]
    [SerializeField, Min(0f)] private float borderSoftness = 18f;

    public IReadOnlyList<RegionLook> Regions => regions;

    /// <summary>The region whose boundary stone this is (its Stone Target), or null.</summary>
    public string RegionOfStone(QuestTargetDefinition stone)
    {
        foreach (RegionLook look in regions)
        {
            if (look != null && stone != null && look.stoneTarget == stone)
            {
                return look.region;
            }
        }

        return null;
    }
    public IReadOnlyList<RingOpening> Openings => openings;
    public float ForgottenDesaturation => forgottenDesaturation;
    public float ForgottenDarkening => forgottenDarkening;
    public Color ForgottenColor => forgottenColor;
    public float RememberedMist => rememberedMist;
    public float DrawBackSeconds => drawBackSeconds;
    public float KeptRemembered => keptRemembered;
    public float KeptRadius => keptRadius;
    public float BorderSoftness => borderSoftness;

    /// <summary>A region's look, or null when it has none (it is then left as painted and never grey).</summary>
    public RegionLook LookOf(string region)
    {
        if (string.IsNullOrEmpty(region))
        {
            return null;
        }

        foreach (RegionLook look in regions)
        {
            if (look != null && look.region == region)
            {
                return look;
            }
        }

        return null;
    }

    /// <summary>The ring a region belongs to: 0 for home or a region with no look.</summary>
    public int RingOf(string region) => LookOf(region)?.ring ?? 0;

    /// <summary>What a stone in a ring not yet open says, or a plain line.</summary>
    public string NotYet(int ring)
    {
        foreach (RingOpening opening in openings)
        {
            if (opening != null && opening.ring == ring && !string.IsNullOrWhiteSpace(opening.notYet))
            {
                return opening.notYet;
            }
        }

        return "The mist here is too thick to remember through. Not yet.";
    }
}
