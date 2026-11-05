using UnityEngine;

/// <summary>
/// A sound now and then somewhere near the player: a fly buzzing past, a branch creaking. Every <see cref="every"/> seconds
/// (a random span) it plays its SoundDefinition at a random spot around the listener, at about ear height, through
/// AudioManager like any other sound. Quieter worlds by night can turn it down with <see cref="byNight"/>.
/// <para>Where it belongs: on an object in the scene, one per kind of sound (set up for the Misty Woods by BuildMistyWoodsForest).</para>
/// </summary>
public sealed class AmbientOneShots : MonoBehaviour
{
    [SerializeField] private SoundDefinition sound;
    [Tooltip("Seconds between sounds, shortest and longest.")]
    [SerializeField] private Vector2 every = new Vector2(12f, 35f);
    [Tooltip("Metres from the listener, nearest and farthest.")]
    [SerializeField] private Vector2 distance = new Vector2(2f, 9f);
    [Tooltip("Chance of playing when due at night (0 never, 1 as by day).")]
    [SerializeField, Range(0f, 1f)] private float byNight = 0.2f;

    private float due;

    public SoundDefinition Sound => sound;

    public void Configure(SoundDefinition played, Vector2 seconds, Vector2 metres, float night)
    {
        sound = played;
        every = seconds;
        distance = metres;
        byNight = night;
    }

    private void OnEnable() => due = Time.time + Random.Range(every.x, every.y);

    private void Update()
    {
        if (sound == null || Time.time < due)
        {
            return;
        }

        due = Time.time + Random.Range(every.x, every.y);
        Transform listener = Player.Current != null ? Player.Current.transform : Camera.main != null ? Camera.main.transform : null;
        if (listener == null || (AtmosphereConditions.Night > 0.5f && Random.value > byNight))
        {
            return;
        }

        Vector2 around = Random.insideUnitCircle.normalized * Random.Range(distance.x, distance.y);
        AudioManager.Play(sound, listener.position + new Vector3(around.x, Random.Range(0.6f, 2f), around.y));
    }
}
