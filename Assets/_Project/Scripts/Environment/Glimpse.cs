using UnityEngine;

/// <summary>
/// Something seen for a moment and then not there: a hollowed deer standing grey in the deep woods, staring; the Huldra
/// between the trees at dusk; a small spirit peering from a stump. It watches the player, and when they come near or it
/// has been seen long enough it sinks into the ground and the mist and is gone. It does nothing and can't be touched:
/// the forest that eats memories, shown rather than told. Made by GlimpseWatcher, one at a time.
/// </summary>
public sealed class Glimpse : MonoBehaviour
{
    [Tooltip("Seconds it stays while the player keeps away, least and most.")]
    [SerializeField] private Vector2 stays = new Vector2(5f, 9f);
    [Tooltip("It goes the moment the player comes this near.")]
    [SerializeField, Min(1f)] private float shyWithin = 11f;
    [Tooltip("Turns to watch the player (a spirit may only peer).")]
    [SerializeField] private bool watches = true;
    [Tooltip("How it goes: metres it sinks, over how many seconds.")]
    [SerializeField, Min(0f)] private float sinks = 2.2f;
    [SerializeField, Min(0.1f)] private float goneSeconds = 1.6f;
    [SerializeField] private SoundDefinition goneSound;
    [Tooltip("Which spirit it is, if one that remembers how it was treated (SpiritBonds: huldra). Empty: none.")]
    [SerializeField] private string spirit;

    /// <summary>Raised as a glimpse begins to go: whether the player came too close (chased it) or it chose to.</summary>
    public static event System.Action<Glimpse, bool> Left;

    public string Spirit => spirit;

    public void SetSpirit(string id) => spirit = id;

    private float leaveAt;
    private float leaving = -1f;
    private Vector3 standing;
    private Light[] lights;
    private float[] lightIntensity;

    public bool IsGone { get; private set; }

    private void Start()
    {
        leaveAt = Time.time + Random.Range(stays.x, stays.y);
        standing = transform.position;
        lights = GetComponentsInChildren<Light>();
        lightIntensity = new float[lights.Length];
        for (int i = 0; i < lights.Length; i++)
        {
            lightIntensity[i] = lights[i].intensity;
        }
    }

    private void Update()
    {
        Player player = Player.Current;
        if (leaving < 0f)
        {
            if (player != null && watches)
            {
                Vector3 to = player.transform.position - transform.position;
                to.y = 0f;
                if (to.sqrMagnitude > 0.01f)
                {
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), 60f * Time.deltaTime);
                }
            }

            bool near = player != null && (player.transform.position - transform.position).sqrMagnitude < shyWithin * shyWithin;
            if (near || Time.time >= leaveAt)
            {
                leaving = 0f;
                Left?.Invoke(this, near);
                if (goneSound != null)
                {
                    AudioManager.Play(goneSound, transform.position + Vector3.up);
                }
            }

            return;
        }

        leaving += Time.deltaTime / goneSeconds;
        float t = Mathf.SmoothStep(0f, 1f, leaving);
        transform.position = standing - Vector3.up * sinks * t;
        for (int i = 0; i < lights.Length; i++)
        {
            lights[i].intensity = lightIntensity[i] * (1f - t);
        }

        if (leaving >= 1f)
        {
            IsGone = true;
            Destroy(gameObject);
        }
    }
}
