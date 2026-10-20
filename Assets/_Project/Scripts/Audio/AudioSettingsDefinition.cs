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

    [Header("Default Volumes (players can change these later in settings)")]
    [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.6f;
    [SerializeField, Range(0f, 1f)] private float ambienceVolume = 0.7f;
    [SerializeField, Range(0f, 1f)] private float effectsVolume = 0.9f;

    [Header("What Counts as Combat")]
    [Tooltip("The player has drawn their weapon (Tab).")]
    [SerializeField] private bool weaponDrawn = true;
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

    [Header("Effects")]
    [Tooltip("How many sound effects can play at once. The oldest stops when more are needed.")]
    [SerializeField, Range(4, 64)] private int maxSimultaneousEffects = 24;

    public MusicPlaylist DefaultExploring => defaultExploring;
    public MusicPlaylist DefaultCombat => defaultCombat;
    public float CrossfadeSeconds => crossfadeSeconds;
    public float MasterVolume => masterVolume;
    public bool WeaponDrawnIsCombat => weaponDrawn;
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
