using UnityEngine;

/// <summary>What a loot table is for, so the Content Editor's Loot Tables list can group them.</summary>
public enum LootTableKind
{
    Creature,
    Cache,
    Raid,
    Elite,
    Other,
}

/// <summary>
/// A loot table kept as its own asset (Data/Loot), so what a creature, a kind of cache or a raid gives is in one place to read and
/// tune: the Content Editor's Loot Tables section lists them and shows what each gives at every tier and who uses it. A creature's
/// definition and its prefab's LootOnDeath point at the same table, so the journal can't show something other than what drops.
/// Rows can be limited to raid tiers (From Tier, To Tier) and keep a Fixed Amount where a raid's loot multiplier must not double
/// them (a trophy). Authored data; never changes while playing.
/// </summary>
[CreateAssetMenu(menuName = "Safe Haven/Loot Table", fileName = "New Loot Table")]
public sealed class LootTable : ScriptableObject
{
    [Tooltip("What the table is, as the Content Editor lists it: \"Wolf\", \"Hunter's Cache\", \"Troll Valley finds\".")]
    [SerializeField] private string displayName;
    [Tooltip("What it is for: a creature's body, a kind of cache, what only one raid holds, or what its elites give.")]
    [SerializeField] private LootTableKind kind;
    [Tooltip("Why it holds what it holds, for whoever tunes it next.")]
    [SerializeField, TextArea(2, 6)] private string notes;
    [Tooltip("Each row drops on its own chance. From Tier and To Tier limit a row to raid tiers (0: any, and a row with a From Tier " +
             "never drops outside a raid). Rows scale with the raid's loot multiplier unless they keep a Fixed Amount.")]
    [SerializeField] private ItemDropTable drops = new ItemDropTable();

    public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
    public LootTableKind Kind => kind;
    public string Notes => notes;
    public ItemDropTable Drops => drops;

    private void OnValidate()
    {
        drops.Validate();
    }
}
