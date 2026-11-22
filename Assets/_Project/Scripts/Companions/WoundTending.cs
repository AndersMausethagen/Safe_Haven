using UnityEngine;

/// <summary>
/// A follower's wounds (a companion, or someone freed in a raid). They carry a couple of bandages, tear another from cloth
/// now and then, and bandage themselves when badly hurt with no enemy close; a hit stops them, bandage kept. The player can
/// bandage them too: a hurt follower shows a Bandage prompt, which uses the mildest healing item in the player's bag and
/// gives back the share of the follower's health it would give the player (times Tend Strength). Numbers in
/// AllyCareSettings. The owner holds still while <see cref="IsBandaging"/>.
/// </summary>
[DisallowMultipleComponent]
public sealed class WoundTending : MonoBehaviour
{
    private Damageable health;
    private string displayName = "them";
    private TendSpot spot;
    private int bandages;
    private float madeAt;
    private float doneAt = -1f;

    /// <summary>True while they are bandaging themselves: the owner keeps them still.</summary>
    public bool IsBandaging => doneAt >= 0f;

    public int Bandages => bandages;

    /// <summary>Adds the care to a follower (once) and names them for the prompt.</summary>
    public static WoundTending For(GameObject follower, string name)
    {
        WoundTending tending = follower.TryGetComponent(out WoundTending found) ? found : follower.AddComponent<WoundTending>();
        tending.displayName = string.IsNullOrWhiteSpace(name) ? tending.displayName : name;
        if (tending.spot != null)
        {
            tending.spot.name = tending.displayName;
        }

        return tending;
    }

    private void Awake()
    {
        health = GetComponent<Damageable>();
        bandages = AllyCareSettings.Current.BandagesCarried;
        madeAt = Time.time;
    }

    private void Start()
    {
        spot = TendSpot.Create(this, displayName);
    }

    private void OnEnable()
    {
        if (health != null)
        {
            health.Damaged += OnHit;
        }
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.Damaged -= OnHit;
        }

        doneAt = -1f;
    }

    private void OnHit(DamageInfo info, DamageResult result) => doneAt = -1f;

    private void Update()
    {
        if (health == null || !health.IsAlive)
        {
            doneAt = -1f;
            if (spot != null)
            {
                spot.SetAvailable(false);
            }

            return;
        }

        AllyCareSettings care = AllyCareSettings.Current;
        if (bandages < care.MostBandages && Time.time - madeAt >= care.MakeOneEvery)
        {
            bandages++;
            madeAt = Time.time;
        }
        else if (bandages >= care.MostBandages)
        {
            madeAt = Time.time;
        }

        if (IsBandaging)
        {
            if (Time.time >= doneAt)
            {
                doneAt = -1f;
                bandages--;
                health.Heal(health.MaxHealth * care.BandageHeals);
            }
        }
        else if (bandages > 0 && health.HealthFraction < care.BandageBelow && !EnemyWithin(transform.position, care.SafeDistance))
        {
            doneAt = Time.time + care.BandageSeconds;
        }

        if (spot != null)
        {
            spot.SetAvailable(health.HealthFraction < care.TendBelow);
        }
    }

    /// <summary>True when a living creature that would attack the player's side is within <paramref name="metres"/>.</summary>
    public static bool EnemyWithin(Vector3 at, float metres)
    {
        float most = metres * metres;
        foreach (MonsterAI monster in MonsterAI.Active)
        {
            if (monster != null && monster.isActiveAndEnabled && monster.Definition != null
                && monster.Definition.Faction.WillAttack(Faction.Player) && (monster.transform.position - at).sqrMagnitude <= most)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The player bandages them with <paramref name="item"/>: the share of the player's health it heals, on theirs.</summary>
    public float TendedWith(ItemDefinition item, Damageable player)
    {
        if (item == null || health == null || !health.IsAlive)
        {
            return 0f;
        }

        float share = player != null ? item.HealTotal / player.MaxHealth : item.HealTotal / 100f;
        return health.Heal(health.MaxHealth * share * AllyCareSettings.Current.TendStrength);
    }

    /// <summary>The mildest healing item in the bag (a bandage before a draught), or null.</summary>
    public static ItemDefinition MildestHealing(ItemContainer bag)
    {
        ItemDefinition best = null;
        if (bag == null)
        {
            return null;
        }

        for (int i = 0; i < bag.SlotCount; i++)
        {
            ItemStack stack = bag.GetSlot(i);
            if (!stack.IsEmpty && stack.Item.HealTotal > 0f && stack.Item.Category == ItemCategory.Consumable
                && (best == null || stack.Item.HealTotal < best.HealTotal))
            {
                best = stack.Item;
            }
        }

        return best;
    }
}
