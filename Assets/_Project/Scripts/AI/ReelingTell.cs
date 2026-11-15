using UnityEngine;

/// <summary>
/// Shows a creature whose poise is worn low (reeling: under Combat Technique Settings › Reeling Share), so the player sees a stagger
/// is near and presses (Lies of P's white border, Path of Exile 2's Primed, Doom's glow): the body sways (MonsterPose) and embers glow
/// at its chest (Combat Feel › Reeling Effect) until it steadies or staggers. Only a look: the creature fights on as before.
/// Plain C#, owned by a MonsterAI; a friend's copy in co-op shows the host's creature's poise.
/// </summary>
public sealed class ReelingTell
{
    // How fast the sway eases in and out, per second.
    private const float EaseRate = 3f;

    private readonly Transform body;
    private readonly float chestHeight;
    private GameObject embers;

    public ReelingTell(Transform body)
    {
        this.body = body;
        chestHeight = body.TryGetComponent(out CapsuleCollider capsule)
            ? Mathf.Max(0.4f, (capsule.center.y + capsule.height * 0.2f) * body.lossyScale.y)
            : 1.1f;
    }

    /// <summary>Height of its chest above its feet, in metres.</summary>
    public float ChestHeight => chestHeight;

    /// <summary>How much it reels now, 0-1, eased: the sway's strength.</summary>
    public float Amount { get; private set; }

    public void Update(bool reeling, float deltaTime)
    {
        Amount = Mathf.MoveTowards(Amount, reeling ? 1f : 0f, deltaTime * EaseRate);
        GameObject prefab = CombatFeelSettings.Current.ReelingEffect;
        if (reeling && embers == null && prefab != null)
        {
            embers = PrefabPool.Spawn(prefab, Chest, Quaternion.identity);
        }
        else if (!reeling && embers != null)
        {
            Clear();
        }

        if (embers != null)
        {
            embers.transform.position = Chest;
        }
    }

    /// <summary>Puts the embers back (it died, or was put away).</summary>
    public void Clear()
    {
        if (embers != null)
        {
            PrefabPool.Release(embers);
            embers = null;
        }
    }

    private Vector3 Chest => body.position + Vector3.up * chestHeight;

    /// <summary>The sway's lean in degrees at <paramref name="time"/>, side to side at Reeling Sway Rate.</summary>
    public static float Sway(float amount, float time, float degrees, float rate) => amount * degrees * Mathf.Sin(time * rate * Mathf.PI * 2f);
}
