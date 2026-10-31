using UnityEngine;

/// <summary>
/// What a transport sounds like, all of it read from the physics: wheels or water under it as fast as it really moves, lower
/// and slower under a load, a creak every few metres and more often when heavy, an oar each stroke, and a thud at anything
/// it really hits. The sounds are in TransportSettings. Only runs while it isn't resting.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Transport))]
public sealed class TransportAudio : MonoBehaviour
{
    private const float FullSpeed = 5f;
    private const float FullBoatSpeed = 3f;
    private const float QuietestLoop = 0.02f;
    private const float EaseSeconds = 0.2f;
    private const float EmptyCreakMetres = 7f;
    private const float FullCreakMetres = 2.5f;
    private const float BumpSpeed = 2.2f;
    private const float BumpGap = 0.3f;

    private Transport transport;
    private Rowboat boat;
    private AudioSource loop;
    private SoundDefinition loopSound;
    private float level;
    private float rolled;
    private float lastStroke;
    private float nextBump;

    /// <summary>How loud the loop is now, 0..1 of its sound's volume.</summary>
    public float Level => level;
    public bool IsLooping => loop != null && loop.isPlaying;
    public float Pitch => loop != null ? loop.pitch : 0f;

    private void Awake()
    {
        transport = GetComponent<Transport>();
        boat = GetComponent<Rowboat>();
        var go = new GameObject("Rolling");
        go.transform.SetParent(transform, false);
        loop = go.AddComponent<AudioSource>();
        loop.loop = true;
        loop.playOnAwake = false;
    }

    private void Update()
    {
        Rigidbody body = transport.Body;
        float speed = body.isKinematic ? 0f : body.linearVelocity.magnitude;
        float load = Mathf.Clamp01(transport.Weight / transport.Definition.Capacity);
        bool rollsNow = boat != null ? boat.IsAfloat : true;
        float share = Mathf.Clamp01(speed / (boat != null ? FullBoatSpeed : FullSpeed));
        float wanted = rollsNow ? share * Mathf.Lerp(0.7f, 1f, load) : 0f;
        level = Mathf.MoveTowards(level, wanted, Time.deltaTime / EaseSeconds);
        DrawLoop(share, load);
        if (boat != null)
        {
            Row();
        }
        else
        {
            Creak(speed, load);
        }
    }

    private void DrawLoop(float share, float load)
    {
        TransportSettings settings = TransportSettings.Current;
        SoundDefinition sound = boat != null ? settings.HullWater : settings.Rolling;
        if (sound == null || !sound.HasClips)
        {
            return;
        }

        if (loopSound != sound)
        {
            loopSound = sound;
            AudioManager.Configure(loop, sound);
            loop.clip = sound.Clips[0];
        }

        if (level <= QuietestLoop)
        {
            if (loop.isPlaying)
            {
                loop.Stop();
            }

            return;
        }

        loop.volume = AudioManager.SteadyVolume(sound) * level;
        loop.pitch = Mathf.Lerp(0.7f, 1.15f, share) * Mathf.Lerp(1.05f, 0.82f, load);
        if (!loop.isPlaying)
        {
            loop.Play();
        }
    }

    private void Creak(float speed, float load)
    {
        rolled += speed * Time.deltaTime;
        if (rolled >= Mathf.Lerp(EmptyCreakMetres, FullCreakMetres, load))
        {
            rolled = 0f;
            AudioManager.Play(TransportSettings.Current.Creak, transform.position);
        }
    }

    // Each stroke sounds as the blade goes in: where the stroke passes zero on its way up.
    private void Row()
    {
        float stroke = boat.Stroke;
        if (lastStroke < 0f && stroke >= 0f && boat.InUse)
        {
            AudioManager.Play(TransportSettings.Current.OarStroke, transform.position);
        }

        lastStroke = stroke;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (Time.time < nextBump || collision.contactCount == 0)
        {
            return;
        }

        float into = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, collision.GetContact(0).normal));
        if (into >= BumpSpeed)
        {
            nextBump = Time.time + BumpGap;
            AudioManager.Play(TransportSettings.Current.Bump, collision.GetContact(0).point);
        }
    }
}
