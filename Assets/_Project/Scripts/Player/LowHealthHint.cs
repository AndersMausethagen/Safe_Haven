using UnityEngine;

/// <summary>
/// Once a scene, when a blow in a fight leaves the player below LowShare of their health, says how to heal: the key of the healing on
/// their bar, or that the healing they carry can go on the bar (the user's playtest, 2026-11-25: two healing draughts drunk in five
/// hours, and every death mid-fight). The strongest healing is named. Nothing when they carry none. Added to the player by Player.Awake
/// (AddTo).
/// </summary>
[DisallowMultipleComponent]
public sealed class LowHealthHint : MonoBehaviour
{
    /// <summary>The share of health below which the hint comes.</summary>
    public const float LowShare = 0.35f;

    private Player player;
    private Damageable health;
    private bool told;

    public static void AddTo(GameObject owner)
    {
        if (owner != null && !owner.TryGetComponent(out LowHealthHint _))
        {
            owner.AddComponent<LowHealthHint>();
        }
    }

    // Its own lookups: it is added in Player.Awake before Player's own references are set.
    private void Awake()
    {
        player = GetComponent<Player>();
        health = GetComponent<Damageable>();
    }

    private void OnEnable()
    {
        if (health != null)
        {
            health.Damaged += OnDamaged;
        }
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.Damaged -= OnDamaged;
        }
    }

    // On the blow itself (Damaged), not on health changing: the first blow of an ambush comes before the game has turned to Combat, and
    // a blow from something is a fight whatever the mode says.
    private void OnDamaged(DamageInfo damage, DamageResult result)
    {
        if (told || !health.IsAlive || health.MaxHealth <= 0f || health.CurrentHealth / health.MaxHealth >= LowShare || Player.Current != player)
        {
            return;
        }

        GameStateManager state = GameStateManager.Current;
        bool fighting = (damage.Source != null && !damage.IsOverTime) || (state != null && state.CurrentState == GameState.Combat);
        if (!fighting)
        {
            return;
        }

        PlayerSkills skills = GetComponent<PlayerSkills>();
        string words = Words(skills != null ? skills.Loadout : null, GetComponent<ItemInventory>(), health.MaxHealth);
        if (words == null)
        {
            return;
        }

        told = true;
        GameMessages.Post(words, MessageKind.Warning);
    }

    /// <summary>What to say: the strongest healing on the bar and its key, else the strongest carried (bag or safe pocket); null when
    /// there is none.</summary>
    public static string Words(SkillLoadout bar, ItemInventory bag, float maxHealth)
    {
        int bestSlot = -1;
        ItemDefinition onBar = null;
        if (bar != null)
        {
            for (int slot = 0; slot < SkillLoadout.SlotCount; slot++)
            {
                ItemDefinition item = bar.GetItem(slot);
                if (Heals(item) && (bag == null || bag.CountCarried(item) > 0) && Better(item, onBar, maxHealth))
                {
                    onBar = item;
                    bestSlot = slot;
                }
            }
        }

        if (onBar != null)
        {
            return $"Health low: press {PlayerInputHandler.SkillKeyLabel(bestSlot)} for your {onBar.DisplayName}.";
        }

        ItemDefinition carried = bag != null ? Best(bag.Container, Best(bag.SafePocket, null, maxHealth), maxHealth) : null;
        return carried != null
            ? $"Health low: your {carried.DisplayName} is in your bag. Put it on the skill bar " +
              $"({PlayerInputHandler.SkillKeyLabel(5)}-{PlayerInputHandler.SkillKeyLabel(SkillLoadout.SlotCount - 1)}) to use it in a fight."
            : null;
    }

    /// <summary>Something that restores health when used.</summary>
    public static bool Heals(ItemDefinition item) => item != null && item.HealTotal > 0f;

    private static bool Better(ItemDefinition item, ItemDefinition than, float maxHealth) =>
        than == null || item.HealTotalFor(maxHealth) > than.HealTotalFor(maxHealth);

    private static ItemDefinition Best(ItemContainer container, ItemDefinition best, float maxHealth)
    {
        if (container == null)
        {
            return best;
        }

        for (int i = 0; i < container.SlotCount; i++)
        {
            ItemStack stack = container.GetSlot(i);
            if (!stack.IsEmpty && Heals(stack.Item) && Better(stack.Item, best, maxHealth))
            {
                best = stack.Item;
            }
        }

        return best;
    }
}
