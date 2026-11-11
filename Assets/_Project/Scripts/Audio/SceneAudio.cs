using UnityEngine;

/// <summary>
/// The music of one scene, set right here on the component: what plays while exploring, while fighting and in the last
/// stretch of a raid, plus the ambience underneath. Empty music lists fall back to the game-wide defaults in Audio Settings.
/// Add one to each scene (Safe Haven > Sound Editor lists them all). Object-specific sounds such as a monster's
/// death don't belong here; they live on the monster's data or a DeathSound on its prefab.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Safe Haven/Audio/Scene Music")]
public sealed class SceneAudio : MonoBehaviour
{
    [Tooltip("Plays while the player explores. Empty uses the default exploring music from Audio Settings.")]
    [SerializeField] private MusicPlaylist exploring = new MusicPlaylist();
    [Tooltip("Plays while the player fights. Empty uses the default combat music from Audio Settings.")]
    [SerializeField] private MusicPlaylist combat = new MusicPlaylist();
    [Tooltip("Raids only: replaces all other music when little time is left. Empty keeps the normal music.")]
    [SerializeField] private MusicPlaylist finalStretch = new MusicPlaylist();
    [Tooltip("Background sound under the music (wind, birds, fire). Loops; several tracks take turns.")]
    [SerializeField] private MusicPlaylist ambience = new MusicPlaylist();

    [Header("Rules")]
    [Tooltip("Switch to combat music when the player fights. Turn off for menus and calm scenes.")]
    [SerializeField] private bool combatMusic = true;
    [Tooltip("Silences music until a quest/event starts it; a fight still brings combat music. Ambience still plays.")]
    [SerializeField] private bool startSilent;
    [Tooltip("The final stretch music starts when this many seconds of the raid are left.")]
    [SerializeField, Min(0f)] private float finalStretchSeconds = 60f;

    public MusicPlaylist Exploring => exploring;
    public MusicPlaylist Combat => combat;
    public MusicPlaylist FinalStretch => finalStretch;
    public MusicPlaylist Ambience => ambience;
    public bool CombatMusic => combatMusic;
    public float FinalStretchSeconds => finalStretchSeconds;
    private bool musicOverridden;
    private bool fightsInterruptEventMusic;
    private MusicPlaylist eventMusic;
    public bool MusicOverridden => musicOverridden || startSilent;
    public MusicPlaylist EventMusic => eventMusic;
    /// <summary>The event music steps aside for combat music while the player fights, and comes back after.</summary>
    public bool FightsInterruptEventMusic => fightsInterruptEventMusic;

    /// <param name="fightsInterrupt">Combat music takes over while the player fights (a place's mood music, such as the
    /// prologue's dark forest); false keeps this music through fights (a boss's or a scripted moment's).</param>
    public void PlayEventMusic(MusicPlaylist playlist, bool fightsInterrupt = false)
    {
        if (playlist == null || !playlist.HasTracks)
        {
            Debug.LogError("Event music needs a playlist with at least one audio clip.", this);
            return;
        }
        musicOverridden = true;
        // The same music asked for again (the prologue's forest music after the wolves, then at the tree line) carries on
        // instead of starting over: the music player tells playlists apart, not their clips.
        if (eventMusic == null || !eventMusic.SameMusicAs(playlist))
        {
            eventMusic = playlist;
        }

        fightsInterruptEventMusic = fightsInterrupt;
    }

    public void SilenceMusic()
    {
        musicOverridden = true;
        eventMusic = null;
    }

    public void ResumeMusic()
    {
        startSilent = false;
        musicOverridden = false;
        eventMusic = null;
    }
}
