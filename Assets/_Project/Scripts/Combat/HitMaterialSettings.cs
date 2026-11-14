using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What each material throws off and sounds like when struck (HitMaterials): a stone troll's grit and sparks, a knight's
/// clang, a rootling's chips. Flesh needs no entry (its blood is CombatFeedback's). Lives at Resources/HitMaterialSettings.
/// </summary>
[CreateAssetMenu(fileName = "HitMaterialSettings", menuName = "Safe Haven/Settings/Hit Materials")]
public sealed class HitMaterialSettings : GameSettings<HitMaterialSettings>
{
    [System.Serializable]
    public sealed class Entry
    {
        public HitMaterial material;
        [Tooltip("Spawned where a blow lands on it (pooled effect prefabs).")]
        public List<GameObject> effects = new List<GameObject>();
        public SoundDefinition sound;
        [Tooltip("Spawned where a missed swing glances off it (a boulder, a tree).")]
        public List<GameObject> glanceEffects = new List<GameObject>();
        public SoundDefinition glanceSound;

        public IReadOnlyList<GameObject> Effects => effects;
        public SoundDefinition Sound => sound;
        public IReadOnlyList<GameObject> GlanceEffects => glanceEffects;
        public SoundDefinition GlanceSound => glanceSound;
    }

    [SerializeField] private List<Entry> materials = new List<Entry>();

    /// <summary>The entry for <paramref name="material"/>, or null.</summary>
    public Entry For(HitMaterial material)
    {
        foreach (Entry entry in materials)
        {
            if (entry != null && entry.material == material)
            {
                return entry;
            }
        }

        return null;
    }
}
