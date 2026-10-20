using System;
using UnityEngine;

/// <summary>
/// A system a single raid opts into, such as roaming spawns: added to the raid's list in the Systems Editor (Raids > Raid
/// Systems) and installed on the RaidDirector when the raid starts. To add a new kind, derive from this with a
/// [Serializable] class holding its settings and override Install; the editor's Add menu finds it by itself.
/// Settings are read only while playing.
/// </summary>
[Serializable]
public abstract class RaidSystem
{
    [Tooltip("Switch the system off for this raid without losing its settings.")]
    [SerializeField] private bool enabled = true;

    public bool Enabled => enabled;

    /// <summary>The name shown in the editor.</summary>
    public abstract string DisplayName { get; }

    /// <summary>One line on what the system does, shown in the editor.</summary>
    public virtual string Description => "";

    /// <summary>Called once on the raid's director when the raid scene starts (only when enabled).</summary>
    public abstract void Install(RaidDirector director);
}

/// <summary>Creatures appear out of sight around the player during the raid and wander towards them (RoamingSpawner).</summary>
[Serializable]
[UnityEngine.Scripting.APIUpdating.MovedFrom(false, sourceAssembly: "Assembly-CSharp")]
public sealed class RoamingSpawnsSystem : RaidSystem
{
    [SerializeField] private RoamingSpawnSettings settings = new RoamingSpawnSettings();

    public RoamingSpawnSettings Settings => settings;
    public override string DisplayName => "Roaming Spawns";
    public override string Description => "Creatures appear out of sight around the player during the raid and wander " +
                                          "towards them, more as the stages and alarms rise. Far, lost roamers are recycled.";

    public RoamingSpawnsSystem()
    {
    }

    public RoamingSpawnsSystem(RoamingSpawnSettings settings)
    {
        this.settings = settings ?? new RoamingSpawnSettings();
    }

    public override void Install(RaidDirector director)
    {
        if (!director.TryGetComponent(out RoamingSpawner spawner))
        {
            spawner = director.gameObject.AddComponent<RoamingSpawner>();
        }

        spawner.Configure(settings);
    }
}
