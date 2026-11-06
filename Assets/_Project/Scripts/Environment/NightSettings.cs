using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What night changes, anywhere the sky is dark (GDD §6.3, §7): what walks the dark is stronger, and Cursed Essence comes
/// out of it twice as fast, so going out at night is a real choice. It all follows how dark it is (AtmosphereConditions.Night),
/// so dusk counts for a little. How many more come is the hour's own in a raid (RaidTimeOfDay) and set here for home.
/// </summary>
[CreateAssetMenu(fileName = "NightSettings", menuName = "Safe Haven/Settings/Night")]
public sealed class NightSettings : GameSettings<NightSettings>
{
    [Header("Danger")]
    [Tooltip("Creatures' health in the dark of night, as a multiple of their own.")]
    [SerializeField, Min(1f)] private float creatureHealth = 1.1f;
    [Tooltip("Creatures' damage in the dark of night, as a multiple of their own.")]
    [SerializeField, Min(1f)] private float creaturePower = 1f;

    [Header("Reward")]
    [Tooltip("Cursed Essence from a kill in the dark of night, as a multiple. Essence Find on gear adds to it.")]
    [SerializeField, Min(1f)] private float cursedEssence = 2f;

    [Header("Prowlers at home (they still never step into light)")]
    [Tooltip("More may prowl at once for each ward breach beaten back: the forest has noticed the home.")]
    [SerializeField, Min(0)] private int prowlersPerBreach = 1;
    [SerializeField, Min(1)] private int mostProwlers = 8;
    [Tooltip("Multiplies the seconds between prowlers appearing.")]
    [SerializeField, Range(0.1f, 1f)] private float prowlerSpawnScale = 0.7f;
    [Tooltip("Worse things that join the prowlers later.")]
    [SerializeField] private List<GameObject> deepProwlers = new List<GameObject>();
    [Tooltip("Ward breaches beaten back before they come.")]
    [SerializeField, Min(0)] private int deepProwlersAfter = 2;
    [Tooltip("The share of prowlers they make up once they come.")]
    [SerializeField, Range(0f, 1f)] private float deepProwlerShare = 0.3f;

    [Header("Text")]
    [SerializeField, TextArea] private string nightfallText = "Night falls. What walks the dark beyond the ward is stronger now.";
    [SerializeField, TextArea] private string nightRaidText = "It is night here. What hunts you is stronger, and rich with Cursed Essence.";

    /// <summary>How dark it is now, 0..1.</summary>
    public static float Darkness => Mathf.Clamp01(AtmosphereConditions.Night);

    /// <summary>At or past this it counts as night in words: dusk still gets a little of the numbers, but no warning.</summary>
    public static bool IsDark => Darkness >= 0.5f;

    public float ProwlerSpawnScale => prowlerSpawnScale;
    public string NightfallText => nightfallText;
    public string NightRaidText => nightRaidText;

    public float HealthAt(float darkness) => Mathf.Lerp(1f, creatureHealth, darkness);
    public float PowerAt(float darkness) => Mathf.Lerp(1f, creaturePower, darkness);
    public float EssenceAt(float darkness) => Mathf.Lerp(1f, cursedEssence, darkness);

    /// <summary>How many may prowl at home once this many breaches have been beaten back.</summary>
    public int ProwlersAlive(int baseCount, int breachesWon) => Mathf.Min(Mathf.Max(baseCount, mostProwlers), baseCount + breachesWon * prowlersPerBreach);

    /// <summary>The worse kind for a roll of 0..1, or null for the ordinary prowlers.</summary>
    public GameObject DeepProwler(int breachesWon, float roll)
    {
        bool comes = deepProwlers.Count > 0 && breachesWon >= deepProwlersAfter && roll < deepProwlerShare;
        return comes ? deepProwlers[Mathf.Clamp(Mathf.FloorToInt(roll / deepProwlerShare * deepProwlers.Count), 0, deepProwlers.Count - 1)] : null;
    }

    /// <summary>Makes a creature that has just appeared as strong as the hour makes it, at full health.</summary>
    public void Strengthen(GameObject creature, object source)
    {
        if (!creature.TryGetComponent(out Combatant combatant))
        {
            return;
        }

        StatSheet stats = combatant.Stats;
        stats.AddModifier(new StatModifier(StatType.MaxHealth, StatModifierOp.Percent, HealthAt(Darkness) - 1f, source));
        stats.AddModifier(new StatModifier(StatType.DamageBonus, StatModifierOp.Flat, PowerAt(Darkness) - 1f, source));
        if (creature.TryGetComponent(out Damageable health))
        {
            health.Heal(health.MaxHealth);
        }
    }
}
