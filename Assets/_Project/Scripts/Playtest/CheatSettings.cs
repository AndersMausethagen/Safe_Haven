using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The cheat menu's lists, which a build can't search the project for, and whether it can be opened. Filled by
/// AgentScripts/BuildCheatMenu.cs; CheatTests fails when a new creature, raid or kind of weather isn't listed.
/// </summary>
public sealed class CheatSettings : GameSettings<CheatSettings>
{
    [Tooltip("Open the menu in builds that aren't development builds. Playtests need it: turn it off for release. The Editor and development builds can always open it.")]
    [SerializeField] private bool inReleaseBuilds = true;
    [SerializeField] private List<MonsterDefinition> creatures = new List<MonsterDefinition>();
    [SerializeField] private List<RaidDefinition> raids = new List<RaidDefinition>();
    [SerializeField] private List<WeatherDefinition> weathers = new List<WeatherDefinition>();
    [SerializeField] private List<ResourceDefinition> essences = new List<ResourceDefinition>();

    public bool IsAvailable => Application.isEditor || Debug.isDebugBuild || inReleaseBuilds;
    public IReadOnlyList<MonsterDefinition> Creatures => creatures;
    public IReadOnlyList<RaidDefinition> Raids => raids;
    public IReadOnlyList<WeatherDefinition> Weathers => weathers;
    public IReadOnlyList<ResourceDefinition> Essences => essences;
}
