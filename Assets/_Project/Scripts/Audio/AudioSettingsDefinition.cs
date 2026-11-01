using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Game-wide audio tuning: default volumes, how music changes, what counts as being in combat and which sound plays when
/// something dies and nothing more specific is set. Each scene's own music lives on its SceneAudio component; the music
/// here only plays where a scene sets none. Lives in a Resources folder so the AudioManager finds it by itself; edit it
/// from Safe Haven > Audio > Sound Editor.
/// </summary>
[CreateAssetMenu(fileName = "AudioSettings", menuName = "Safe Haven/Audio/Audio Settings")]
public sealed class AudioSettingsDefinition : ScriptableObject
{
    /// <summary>The name the AudioManager loads it by, from a Resources folder.</summary>
    public const string ResourceName = "AudioSettings";

    [Header("Default Music (used when a scene sets none)")]
    [Tooltip("Plays while exploring in scenes whose Scene Music has no exploring tracks.")]
    [SerializeField] private MusicPlaylist defaultExploring = new MusicPlaylist();
    [Tooltip("Plays while fighting in scenes whose Scene Music has no combat tracks.")]
    [SerializeField] private MusicPlaylist defaultCombat = new MusicPlaylist();
    [Tooltip("Seconds one piece of music takes to fade into the next.")]
    [SerializeField, Min(0f)] private float crossfadeSeconds = 2f;
    [Tooltip("Seconds for the music to change when a fight starts: quicker, or the danger is over before it arrives.")]
    [SerializeField, Min(0f)] private float combatFadeSeconds = 0.9f;

    [Header("Default Volumes (players can change these later in settings)")]
    [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.6f;
    [SerializeField, Range(0f, 1f)] private float ambienceVolume = 0.7f;
    [SerializeField, Range(0f, 1f)] private float effectsVolume = 0.9f;

    [Header("What Counts as Combat")]
    [HideInInspector, SerializeField] private bool weaponDrawn = true;
    [Tooltip("An enemy is chasing or attacking the player.")]
    [SerializeField] private bool enemyChasingPlayer = true;
    [Tooltip("The player hit something or was hit within this many seconds. 0 turns this off.")]
    [SerializeField, Min(0f)] private float recentDamageSeconds = 6f;
    [Tooltip("Combat music keeps playing this many seconds after the fighting stops, so it doesn't flicker on and off.")]
    [SerializeField, Min(0f)] private float combatLingerSeconds = 5f;

    [Header("Death Sounds (used when the monster, building or object sets none)")]
    [Tooltip("When a creature is killed.")]
    [SerializeField] private SoundDefinition creatureKilled;
    [Tooltip("When a building or other structure is destroyed.")]
    [SerializeField] private SoundDefinition structureDestroyed;

    [Header("Combat Sounds (weapons, attacks and monsters can set their own)")]
    [SerializeField] private SoundDefinition swing;
    [SerializeField] private SoundDefinition heavySwing;
    [Tooltip("A hit on a creature.")]
    [SerializeField] private SoundDefinition hit;
    [Tooltip("Played on top of the hit for critical hits, finishers and counters.")]
    [SerializeField] private SoundDefinition criticalHit;
    [Tooltip("A hit on a building or other structure.")]
    [SerializeField] private SoundDefinition structureHit;
    [SerializeField] private SoundDefinition block;
    [SerializeField] private SoundDefinition parry;
    [SerializeField] private SoundDefinition guardBreak;
    [SerializeField] private SoundDefinition stagger;
    [SerializeField] private SoundDefinition perfectDodge;
    [SerializeField] private SoundDefinition counterReady;
    [SerializeField] private SoundDefinition dodge;
    [SerializeField] private SoundDefinition playerHurt;
    [Tooltip("When an action is refused for lack of stamina.")]
    [SerializeField] private SoundDefinition outOfStamina;

    [Header("Footsteps")]
    [Tooltip("Steps on terrain whose ground layer name contains one of the words (comma separated). First match wins.")]
    [SerializeField] private List<SurfaceSound> footstepSurfaces = new List<SurfaceSound>();
    [Tooltip("Steps on anything else: terrain no word matches, rocks, floors.")]
    [SerializeField] private SoundDefinition footstep;
    [Tooltip("Steps where grass and plants grow on the terrain (detail cover), whatever the ground layer is.")]
    [SerializeField] private SoundDefinition footstepGrass;
    [Tooltip("Grass and plant instances in a terrain detail cell needed for grass steps.")]
    [SerializeField, Min(1)] private int grassForGrassSteps = 2;
    [Tooltip("Steps on buildings and other non-terrain ground with a wooden look.")]
    [SerializeField] private SoundDefinition footstepWood;
    [Tooltip("Steps in water.")]
    [SerializeField] private SoundDefinition footstepWater;
    [SerializeField] private SoundDefinition land;
    [Tooltip("Metres walked per step when the animation has no footstep events.")]
    [SerializeField, Min(0.2f)] private float strideLength = 1.4f;

    [Header("World Sounds")]
    [SerializeField] private SoundDefinition questStarted;
    [SerializeField] private SoundDefinition questCompleted;
    [SerializeField] private SoundDefinition crafted;
    [SerializeField] private SoundDefinition built;
    [Tooltip("Eating or drinking.")]
    [SerializeField] private SoundDefinition consumed;
    [Tooltip("A stack dropped from the bag landing on the ground.")]
    [SerializeField] private SoundDefinition itemDropped;
    [Tooltip("The clearing mended at the Heart Tree.")]
    [SerializeField] private SoundDefinition repaired;

    [Header("Ward Breaches (heard everywhere: they are warnings)")]
    [Tooltip("A breach is coming, or the ward is about to give way.")]
    [SerializeField] private SoundDefinition breachWarning;
    [Tooltip("The attackers are coming, and every wave after the first.")]
    [SerializeField] private SoundDefinition breachBegins;
    [SerializeField] private SoundDefinition breachWon;
    [Tooltip("The ward has fallen.")]
    [SerializeField] private SoundDefinition breachLost;

    [Header("Interface Sounds")]
    [Tooltip("Pressing a button.")]
    [SerializeField] private SoundDefinition click;
    [SerializeField] private SoundDefinition windowOpen;
    [SerializeField] private SoundDefinition windowClose;

    [Header("Weather and Water (loops on the Ambience volume)")]
    [Tooltip("Loops while it rains, as loud as the rain is heavy.")]
    [SerializeField] private SoundDefinition rainLoop;
    [Tooltip("Loops with the wind, louder in strong wind and gusts.")]
    [SerializeField] private SoundDefinition windLoop;
    [Tooltip("Rolls a moment after each lightning flash.")]
    [SerializeField] private SoundDefinition thunder;
    [Tooltip("Loops at every water surface (streams, waterfalls), from the nearest point of it.")]
    [SerializeField] private SoundDefinition waterLoop;

    [Header("Effects")]
    [Tooltip("How many sound effects can play at once. When more are needed the least important gives way, the oldest first.")]
    [SerializeField, Range(4, 64)] private int maxSimultaneousEffects = 24;
    [Tooltip("How loud sounds in the world are, against the music and the interface. Sits under everything that has a place, so close sounds aren't deafening.")]
    [SerializeField, Range(0f, 1f)] private float worldSoundsLevel = 0.75f;

    /// <summary>A footstep sound for ground layers whose name contains one of the words.</summary>
    [System.Serializable]
    public sealed class SurfaceSound
    {
        [Tooltip("Words to look for in the ground layer's name, comma separated (grass, moss).")]
        public string layerWords;
        public SoundDefinition sound;

        public bool Matches(string layerName)
        {
            if (string.IsNullOrEmpty(layerWords) || string.IsNullOrEmpty(layerName))
            {
                return false;
            }

            foreach (string word in layerWords.Split(','))
            {
                string trimmed = word.Trim();
                if (trimmed.Length > 0 && layerName.IndexOf(trimmed, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public SoundDefinition Swing => swing;
    public SoundDefinition HeavySwing => heavySwing;
    public SoundDefinition Hit => hit;
    public SoundDefinition CriticalHit => criticalHit;
    public SoundDefinition StructureHit => structureHit;
    public SoundDefinition Block => block;
    public SoundDefinition Parry => parry;
    public SoundDefinition GuardBreak => guardBreak;
    public SoundDefinition Stagger => stagger;
    public SoundDefinition PerfectDodge => perfectDodge;
    public SoundDefinition CounterReady => counterReady;
    public SoundDefinition Dodge => dodge;
    public SoundDefinition PlayerHurt => playerHurt;
    public SoundDefinition OutOfStamina => outOfStamina;
    public SoundDefinition Footstep => footstep;
    public SoundDefinition FootstepGrass => footstepGrass;
    public int GrassForGrassSteps => grassForGrassSteps;
    public SoundDefinition FootstepWood => footstepWood;
    public SoundDefinition FootstepWater => footstepWater;
    public SoundDefinition Land => land;
    public float StrideLength => strideLength;
    public SoundDefinition QuestStarted => questStarted;
    public SoundDefinition QuestCompleted => questCompleted;
    public SoundDefinition Crafted => crafted;
    public SoundDefinition Built => built;
    public SoundDefinition Consumed => consumed;
    public SoundDefinition ItemDropped => itemDropped;
    public SoundDefinition Repaired => repaired;
    public SoundDefinition BreachWarning => breachWarning;
    public SoundDefinition BreachBegins => breachBegins;
    public SoundDefinition BreachWon => breachWon;
    public SoundDefinition BreachLost => breachLost;
    public float CombatFadeSeconds => combatFadeSeconds;
    public float WorldSoundsLevel => worldSoundsLevel;
    public SoundDefinition Click => click;
    public SoundDefinition WindowOpen => windowOpen;
    public SoundDefinition WindowClose => windowClose;
    public SoundDefinition RainLoop => rainLoop;
    public SoundDefinition WindLoop => windLoop;
    public SoundDefinition Thunder => thunder;
    public SoundDefinition WaterLoop => waterLoop;

    /// <summary>The footstep for a terrain ground layer by name (the first matching surface), else the default.</summary>
    public SoundDefinition FootstepOn(string layerName)
    {
        foreach (SurfaceSound surface in footstepSurfaces)
        {
            if (surface != null && surface.sound != null && surface.Matches(layerName))
            {
                return surface.sound;
            }
        }

        return footstep;
    }

    public MusicPlaylist DefaultExploring => defaultExploring;
    public MusicPlaylist DefaultCombat => defaultCombat;
    public float CrossfadeSeconds => crossfadeSeconds;
    public float MasterVolume => masterVolume;
    public bool EnemyChasingIsCombat => enemyChasingPlayer;
    public float RecentDamageSeconds => recentDamageSeconds;
    public float CombatLingerSeconds => combatLingerSeconds;
    public int MaxSimultaneousEffects => maxSimultaneousEffects;

    public float DefaultVolume(AudioChannel channel)
    {
        switch (channel)
        {
            case AudioChannel.Music:
                return musicVolume;
            case AudioChannel.Ambience:
                return ambienceVolume;
            default:
                return effectsVolume;
        }
    }

    public SoundDefinition DefaultDeathSound(TargetCategory category)
    {
        return category == TargetCategory.Structure ? structureDestroyed : creatureKilled;
    }
}
