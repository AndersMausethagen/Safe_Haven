using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Small scenes between friends at home (SmallScenes; Docs/ColonyModeResearch_2026-11-21.md §4.9, Wildermyth's small moments, not
/// meters): who meets whom, what they say, where, and what it does for those near (a little quicker at work for a while). Only good
/// moments: both must be content with their homes. Resources/SmallSceneSettings.
/// </summary>
[CreateAssetMenu(menuName = "Safe Haven/Settings/Small Scene Settings", fileName = "SmallSceneSettings")]
public sealed class SmallSceneSettings : GameSettings<SmallSceneSettings>
{
    /// <summary>Where two friends meet.</summary>
    public enum Spot
    {
        /// <summary>Beside the gathering fire nearest the first one's home.</summary>
        Fire = 0,
        /// <summary>On the shore of the still water nearest the portal.</summary>
        Water = 1,
    }

    /// <summary>What a scene is between the two (Docs/PeopleResearch_2026-11-23.md §3.5). Saved as numbers in the asset: add at the end.</summary>
    public enum Kind
    {
        /// <summary>Two who get on, both content at home: everyone near is cheered, and the two grow closer.</summary>
        Gathering = 0,
        /// <summary>Once, when the two have become Friends (PeopleStandings +2): their own scene, easing a hard mark a friend can ease.</summary>
        Friends = 1,
        /// <summary>Two who rub each other up the wrong way (Cool or worse) have it out at the fire: it clears the air or hardens.</summary>
        Rivals = 2,
    }

    /// <summary>Two friends, what they do (shown over them in the Keeper's view), where, and their exchange, the first speaking first.</summary>
    [Serializable]
    public sealed class Scene
    {
        public ResidentDefinition first;
        public ResidentDefinition second;
        [Tooltip("Gathering (two who get on), Friends (once, when they have become Friends), Rivals (an argument that clears the air or hardens).")]
        public Kind kind = Kind.Gathering;
        [Tooltip("Rivals: the first one's last line when it cleared the air, and when it hardened.")]
        [TextArea(1, 3)] public string cleared;
        [TextArea(1, 3)] public string hardened;
        [Tooltip("What it looks like from above, a word or two: \"Laughing\", \"Singing\", \"Telling a story\".")]
        public string doing = "Talking";
        public Spot at = Spot.Fire;
        [Tooltip("Their lines in turn: the first, the second, the first...")]
        [TextArea(1, 3)] public string[] lines = Array.Empty<string>();
    }

    [SerializeField] private List<Scene> scenes = new List<Scene>();
    [Tooltip("Seconds between one scene's end and the next one's start, at the soonest.")]
    [SerializeField, Min(10f)] private float restSeconds = 300f;
    [Tooltip("Seconds each line is given.")]
    [SerializeField, Min(1f)] private float lineSeconds = 4.5f;
    [Tooltip("Seconds the two stay together after their lines.")]
    [SerializeField, Min(0f)] private float lingerSeconds = 25f;
    [Tooltip("Metres from the player within which a scene is heard by itself.")]
    [SerializeField, Min(2f)] private float hearReach = 16f;
    [Tooltip("Metres round a scene within which people are cheered by it (the two themselves too).")]
    [SerializeField, Min(1f)] private float cheerReach = 14f;
    [Tooltip("Seconds a cheered person stays so.")]
    [SerializeField, Min(10f)] private float cheerSeconds = 900f;
    [Tooltip("Work speed a cheered person adds: a share of a worker's own (0.1: a tenth quicker at their bench and at a scaffold).")]
    [SerializeField, Range(0f, 0.5f)] private float cheerSpeed = 0.1f;

    public IReadOnlyList<Scene> Scenes => scenes;
    public float RestSeconds => restSeconds;
    public float LineSeconds => lineSeconds;
    public float LingerSeconds => lingerSeconds;
    public float HearReach => hearReach;
    public float CheerReach => cheerReach;
    public float CheerSeconds => cheerSeconds;
    public float CheerSpeed => cheerSpeed;

    /// <summary>Sets the scenes (the builder that writes them).</summary>
    public void Configure(List<Scene> written) => scenes = written ?? new List<Scene>();
}
