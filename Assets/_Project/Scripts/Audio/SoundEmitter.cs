using UnityEngine;

/// <summary>
/// Plays a sound from this object, for hooking sounds up without code: as a looping sound source (a campfire, a portal
/// hum), when the object appears, or from any event in the Inspector (an Interactable's Completed event, a button's
/// On Click) by choosing SoundEmitter > Play.
/// </summary>
public sealed class SoundEmitter : MonoBehaviour
{
    [SerializeField] private SoundDefinition sound;
    [Tooltip("Plays once when the object appears.")]
    [SerializeField] private bool playOnEnable;
    [Tooltip("Keeps playing for as long as the object is active, from this object's position.")]
    [SerializeField] private bool loop;

    private AudioSource loopSource;

    private void OnEnable()
    {
        if (loop)
        {
            StartLoop();
        }
        else if (playOnEnable)
        {
            Play();
        }
    }

    private void OnDisable()
    {
        if (loopSource != null)
        {
            loopSource.Stop();
        }
    }

    /// <summary>Plays the sound once from this object. Can be chosen from any event in the Inspector.</summary>
    public void Play()
    {
        AudioManager.Play(sound, transform.position);
    }

    private void StartLoop()
    {
        AudioClip clip = sound != null ? sound.PickClip() : null;
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

    // Loops follow the volume settings as they change.
    private void Update()
    {
        if (loopSource != null && loopSource.isPlaying)
        {
            loopSource.volume = AudioManager.SteadyVolume(sound);
        }
    }
}
