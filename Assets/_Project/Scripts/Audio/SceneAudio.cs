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
    private MusicPlaylist eventMusic;
    public bool MusicOverridden => musicOverridden || startSilent;
    public MusicPlaylist EventMusic => eventMusic;

    public void PlayEventMusic(MusicPlaylist playlist)
    {
        if (playlist == null || !playlist.HasTracks)
        {
            Debug.LogError("Event music needs a playlist with at least one audio clip.", this);
            return;
        }
        musicOverridden = true;
        eventMusic = playlist;
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
