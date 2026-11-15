using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// What a monster leaves its killer: items rolled from its loot table, given straight to the killer's bag, resources
/// (hides, Cursed Essence) given to their resource bars, and recipes it teaches (a boss unlocking iron smithing). Only a
/// killer with an inventory (the player) collects anything; what falls to someone on the player's side who carries no bag
/// (a companion, a spike trap, a watch post's archer) is the player's haul (<see cref="TakerOf"/>). Whatever doesn't fit is
/// reported rather than silently lost.
/// </summary>
[RequireComponent(typeof(Damageable))]
public sealed class LootOnDeath : MonoBehaviour
{
    [SerializeField] private ItemDropTable drops = new ItemDropTable();
    [Tooltip("Recipes the killer learns, for bosses that unlock new crafting.")]
    [SerializeField] private List<RecipeDefinition> unlocksRecipes = new List<RecipeDefinition>();
    [Tooltip("Chance it leaves a relic (RelicSettings), in a raid only: for what stands at the end of the map.")]
    [SerializeField, Range(0f, 1f)] private float relicChance;
    [Tooltip("A great creature's own enchantments (Source Boss), in a raid only: while the killer doesn't know one, it always leaves " +
             "an inscription of one; after that, at Essence Settings › Boss Repeat Chance (Docs/EnchantingResearch.md #3).")]
    [SerializeField] private List<EnchantmentDefinition> inscriptions = new List<EnchantmentDefinition>();

    private Damageable damageable;

    public ItemDropTable Drops => drops;

    /// <summary>
    /// This creature leaves no Cursed Essence: what is killed outside a raid (home's night prowlers) doesn't pay in what only
    /// raids should give (GDD §13.5).
    /// </summary>
    public bool WithholdsEssence { get; set; }
    public IReadOnlyList<RecipeDefinition> UnlocksRecipes => unlocksRecipes;
    public IReadOnlyList<EnchantmentDefinition> Inscriptions => inscriptions;

    private void Awake()
    {
        damageable = GetComponent<Damageable>();
    }

    private void OnEnable()
    {
        damageable.Died += OnDied;
    }

    private void OnDisable()
    {
        damageable.Died -= OnDied;
    }

    private void OnValidate()
    {
        drops.Validate();
    }

    /// <summary>
    /// Who collects what <paramref name="killer"/> brought down: the killer, or the player when the killer fights on their
    /// side and has no bag of its own. Before this a companion's finishing blow lost the player the whole drop.
    /// </summary>
    public static Combatant TakerOf(Combatant killer, Combatant player)
    {
        if (killer == null || player == null || !killer.Faction.IsPlayerSide() || killer.GetComponentInParent<ItemInventory>() != null)
        {
            return killer;
        }

        return player;
    }

    /// <summary><see cref="TakerOf(Combatant, Combatant)"/> for the player in this scene.</summary>
    public static Combatant TakerOf(Combatant killer) => TakerOf(killer, Player.Current != null ? Player.Current.Combatant : null);

    private void OnDied(Damageable victim, DamageInfo killingBlow)
    {
        Combatant taker = TakerOf(killingBlow.Source);
        if (taker == null)
        {
            return;
        }

        var inventory = taker.GetComponentInParent<ItemInventory>();
        if (inventory != null)
        {
            List<ItemAmount> items = drops.Roll();
            if (GameSession.ActiveRaid != null)
            {
                items.RemoveAll(drop => !GameSession.ActiveRaid.Allows(drop.Item));
            }

            ItemGrantResult result = inventory.Give(items);
            if (result.HasOverflow)
            {
                GameMessages.Post("Bag full, left behind: " + AmountText.Describe(result.Overflow), MessageKind.Warning);
                LootGrant.LeftBehind(inventory.gameObject, result.Overflow);
            }
        }

        if (taker.GetComponentInParent<ResourceInventory>() != null)
        {
            List<ResourceAmount> rolled = drops.RollResources();
            if (WithholdsEssence)
            {
                rolled.RemoveAll(entry => entry.Resource == SettlementSettings.Current.CursedEssence);
            }

            List<ResourceAmount> overflow = LootGrant.GiveResources(taker.gameObject, Reaped(rolled, taker));
            if (overflow.Count > 0)
            {
                GameMessages.Post("Can't carry more: " + AmountText.Describe(overflow) + " left behind.", MessageKind.Warning);
                LootGrant.LeftBehind(taker.gameObject, overflow);
            }
        }

        RaidDirector raid = RaidDirector.Current;
        if (raid != null && relicChance > 0f && inventory != null && Random.value < relicChance)
        {
            RelicSettings.Grant(inventory.gameObject, new System.Random(Random.Range(0, int.MaxValue)), raid.Session.Tier);
        }

        if (raid != null && inscriptions.Count > 0 && inventory != null)
        {
            var random = new System.Random(Random.Range(0, int.MaxValue));
            EnchantmentDefinition own = global::Inscriptions.Pick(inscriptions, inventory.gameObject, random, copies: false);
            if (own == null && Random.value < EssenceSettings.Current.BossRepeatChance)
            {
                own = inscriptions[random.Next(inscriptions.Count)];
            }

            if (own != null)
            {
                global::Inscriptions.Grant(inventory.gameObject, own, name);
            }
        }

        var crafting = taker.GetComponentInParent<PlayerCrafting>();
        if (crafting != null && unlocksRecipes.Count > 0)
        {
            crafting.Unlock(unlocksRecipes);
        }
    }

    private static IReadOnlyList<ResourceAmount> Reaped(IReadOnlyList<ResourceAmount> rolled, Combatant killer)
    {
        // The night's share and Essence Find on gear multiply: both are "more of what fell".
        float find = (1f + killer.Stats.Get(StatType.EssenceFind)) * NightSettings.Current.EssenceAt(NightSettings.Darkness) * RaidDirector.StageLootNow - 1f;
        ResourceDefinition cursed = SettlementSettings.Current.CursedEssence;
        if (find <= 0f || cursed == null)
        {
            return rolled;
        }

        var reaped = new List<ResourceAmount>(rolled.Count);
        foreach (ResourceAmount entry in rolled)
        {
            reaped.Add(entry.Resource == cursed ? new ResourceAmount(cursed, GearPower.Reaped(entry.Amount, find, Random.value)) : entry);
        }

        return reaped;
    }
}
