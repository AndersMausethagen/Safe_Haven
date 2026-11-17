using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A generated map's landmark that the minimap marks once found, and that may make a sound now and then (Docs/WorldDesignReview_2026-11-17.md
/// G4). The top-down camera shows little of the land, so finding the way is the minimap's job: a landmark seen once stays marked, pinned
/// to the minimap's rim when far, and a sound (falls, a bell, crows) says where it is before it is seen. Added by RaidMapGenerator from the
/// rule's Reveal Radius, Known At Start, Icon and Sound; MinimapHud reads <see cref="Active"/>. Found is each player's own (co-op).
/// </summary>
public sealed class RaidMapLandmark : MonoBehaviour
{
    private static readonly List<RaidMapLandmark> active = new List<RaidMapLandmark>();

    // Entering Play doesn't reload scripts in this project, so statics are cleared when it starts.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => active.Clear();

    [Tooltip("Metres within which the player finds it; 0: never marked (unless known at start).")]
    [SerializeField, Min(0f)] private float revealRadius;
    [Tooltip("Marked from the start.")]
    [SerializeField] private bool knownAtStart;
    [Tooltip("The minimap's mark. Empty: a dot.")]
    [SerializeField] private Sprite icon;
    [SerializeField] private SoundDefinition sound;
    [Tooltip("Seconds between its sounds, least and most.")]
    [SerializeField] private Vector2 soundEvery = new Vector2(8f, 20f);
    [Tooltip("The sound loops instead of playing now and then.")]
    [SerializeField] private bool soundLoops;

    private bool found;
    private float due;
    private AudioSource loopSource;

    /// <summary>The landmarks standing now, for the minimap.</summary>
    public static IReadOnlyList<RaidMapLandmark> Active => active;

    /// <summary>True when it is marked on the minimap at all (a reveal radius or known from the start).</summary>
    public bool CanBeMarked => revealRadius > 0f || knownAtStart;

    /// <summary>The minimap's mark; null for a dot.</summary>
    public Sprite Icon => icon;

    public void Configure(float radius, bool known, Sprite mark, SoundDefinition played, Vector2 every, bool loops)
    {
        revealRadius = Mathf.Max(0f, radius);
        knownAtStart = known;
        icon = mark;
        sound = played;
        soundEvery = every;
        soundLoops = loops;
        if (isActiveAndEnabled)
        {
            StartSound();
        }
    }

    /// <summary>True once the player has come within its reveal radius (or from the start when known): it stays found.</summary>
    public bool IsFoundFrom(Vector3 position)
    {
        if (found || knownAtStart)
        {
            return true;
        }

        if (revealRadius <= 0f)
        {
            return false;
        }

        Vector3 offset = transform.position - position;
        offset.y = 0f;
        found = offset.sqrMagnitude <= revealRadius * revealRadius;
        return found;
    }

    private void OnEnable()
    {
        active.Add(this);
        StartSound();
    }

    private void OnDisable()
    {
        active.Remove(this);
        if (loopSource != null)
        {
            loopSource.Stop();
        }
    }

    private void StartSound()
    {
        due = Time.time + Random.Range(0f, Mathf.Max(0.1f, soundEvery.y));
        if (!soundLoops || sound == null)
        {
            return;
        }

        AudioClip clip = sound.PickClip();
        if (clip == null)
        {
            return;
        }

        if (loopSource == null)
        {
            loopSource = gameObject.AddComponent<AudioSource>();
            loopSource.playOnAwake = false;
        }

        AudioManager.Configure(loopSource, sound);
        loopSource.clip = clip;
        loopSource.loop = true;
        loopSource.Play();
    }

    private void Update()
    {
        if (sound == null)
        {
            return;
        }

        if (soundLoops)
        {
            // Loops follow the volume settings as they change, as SoundEmitter's do.
            if (loopSource != null && loopSource.isPlaying)
            {
                loopSource.volume = AudioManager.SteadyVolume(sound);
            }

            return;
        }

        if (Time.time < due)
        {
            return;
        }

        due = Time.time + Random.Range(Mathf.Max(0.1f, soundEvery.x), Mathf.Max(soundEvery.x, soundEvery.y) + 0.1f);
        // AudioManager takes no voice for what nobody can hear, so a far landmark costs nothing.
        AudioManager.Play(sound, transform.position + Vector3.up * 2f);
    }
}
