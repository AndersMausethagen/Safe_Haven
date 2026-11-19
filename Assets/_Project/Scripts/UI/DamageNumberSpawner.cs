using UnityEngine;

/// <summary>
/// Shows a floating number whenever the Damageable on this object is hit. The numbers come from the scene's
/// PrefabPool rather than living under the target, so they finish their animation even when the hit destroys it.
/// </summary>
[RequireComponent(typeof(Damageable))]
[OnAFriendsCopy(CopyRule.Same, "shows the blows struck here")]
public sealed class DamageNumberSpawner : MonoBehaviour
{
    [SerializeField] private DamageNumber prefab;
    [Tooltip("Height above the target's pivot where numbers appear, in meters.")]
    [SerializeField] private float height = 2.2f;
    [Tooltip("Random sideways offset so quick hits don't stack on top of each other.")]
    [SerializeField, Min(0f)] private float spread = 0.4f;

    private Damageable damageable;
    // The number any spawner floats: for words over things that have none (a node's clean cut).
    private static DamageNumber anyPrefab;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => anyPrefab = null;

    /// <summary>Floats a word at a point in the world, using the floating number of any spawner in the game. False when there is none.</summary>
    public static bool ShowAt(Vector3 position, string text, Color color)
    {
        if (anyPrefab == null)
        {
            foreach (DamageNumberSpawner spawner in FindObjectsByType<DamageNumberSpawner>())
            {
                if (spawner.prefab != null)
                {
                    anyPrefab = spawner.prefab;
                    break;
                }
            }
        }

        if (anyPrefab == null)
        {
            return false;
        }

        PrefabPool.Spawn(anyPrefab, position, Quaternion.identity).ShowText(text, color);
        return true;
    }

    private void Awake()
    {
        damageable = GetComponent<Damageable>();
    }

    private void OnEnable()
    {
        damageable.Damaged += OnDamaged;
    }

    private void OnDisable()
    {
        damageable.Damaged -= OnDamaged;
    }

    private void OnDamaged(DamageInfo info, DamageResult result)
    {
        if (!GameOptions.DamageNumbers.On)
        {
            return;
        }

        if (prefab == null || result.Damage <= 0f)
        {
            return;
        }

        PrefabPool.Spawn(prefab, SpawnPoint(), Quaternion.identity).Show(result);
    }

    /// <summary>Floats a word over the target, such as "Dodged".</summary>
    public void ShowText(string text, Color color)
    {
        if (prefab == null)
        {
            return;
        }

        // The same word again at once (two wolves' blows dodged together): one is read, two drawn over each other are not.
        if (text == lastWord && Time.unscaledTime - lastWordAt < SameWordSeconds)
        {
            return;
        }

        lastWord = text;
        lastWordAt = Time.unscaledTime;
        PrefabPool.Spawn(prefab, SpawnPoint(), Quaternion.identity).ShowText(text, color);
    }

    // What floats up close together steps upwards instead of landing on what is already there ("DDodged": the HUD review of
    // 2026-11-18); the sideways spread alone left them over each other.
    private const float StackSeconds = 0.6f;
    private const float StackStep = 0.35f;
    private const int MostStacked = 3;
    private const float SameWordSeconds = 0.35f;
    private float lastSpawnAt = float.NegativeInfinity;
    private int stacked;
    private string lastWord;
    private float lastWordAt = float.NegativeInfinity;

    private Vector3 SpawnPoint()
    {
        stacked = Time.unscaledTime - lastSpawnAt < StackSeconds ? (stacked + 1) % (MostStacked + 1) : 0;
        lastSpawnAt = Time.unscaledTime;
        Vector2 offset = Random.insideUnitCircle * spread;
        return transform.position + Vector3.up * (height + stacked * StackStep) + new Vector3(offset.x, 0f, offset.y);
    }
}
