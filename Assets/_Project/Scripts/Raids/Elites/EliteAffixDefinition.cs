using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One thing that can make a creature an elite: its name on the label, its colour on the ring under it, what it changes in numbers and
/// what it makes the creature do. A raid lists which it can use and a tier says how many a creature gets. Each should come with an
/// answer the player can learn, as Diablo's do: step out of the circle, get away from it, keep hitting it. Authored data; never
/// changes while playing. To add one with no new behaviour: a new asset, and add it to the raid's Elite Affixes. No code.
/// </summary>
[CreateAssetMenu(fileName = "EliteAffix", menuName = "Safe Haven/Raids/Elite Affix")]
public sealed class EliteAffixDefinition : ScriptableObject
{
    [Tooltip("Put before the creature's name: Frenzied Forest-Bitten Wolf.")]
    [SerializeField] private string displayName;
    [Tooltip("What it does and what to do about it, for the designer.")]
    [SerializeField, TextArea] private string description;
    [Tooltip("The ring under it and its word on the label.")]
    [SerializeField] private Color colour = Color.white;
    [Tooltip("The lowest raid tier it appears at.")]
    [SerializeField, Range(1, Tiers.Max)] private int fromTier = 1;
    [Tooltip("How often it comes up, against the others usable at the tier.")]
    [SerializeField, Min(0f)] private float weight = 1f;

    [Header("Numbers (on top of the raid's own scaling)")]
    [Tooltip("Multiplies its health.")]
    [SerializeField, Min(0.1f)] private float health = 1.4f;
    [Tooltip("Multiplies its damage.")]
    [SerializeField, Min(0f)] private float damage = 1f;
    [Tooltip("Multiplies how fast it moves.")]
    [SerializeField, Min(0.1f)] private float speed = 1f;
    [Tooltip("Multiplies how long it waits between strikes. Wind-ups stay as they are, so its attacks can still be read.")]
    [SerializeField, Min(0.1f)] private float cooldowns = 1f;
    [Tooltip("Added to its armor.")]
    [SerializeField, Min(0f)] private float armor;
    [Tooltip("Multiplies its size.")]
    [SerializeField, Min(0.1f)] private float size = 1f;

    [Header("What it does")]
    [SerializeReference] private List<EliteBehaviour> behaviours = new List<EliteBehaviour>();
    [Tooltip("The enchantment an elite with this mark carries: its inscription, at Essence Settings › Elite Inscription Chance " +
             "(Docs/EnchantingResearch.md #3), so the player takes home the trick that nearly killed them.")]
    [SerializeField] private EnchantmentDefinition inscription;

    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public EnchantmentDefinition Inscription => inscription;
    public string Description => description;
    public Color Colour => colour;
    public int FromTier => fromTier;
    public float Weight => weight;
    public float Health => health;
    public float Damage => damage;
    public float Speed => speed;
    public float Cooldowns => cooldowns;
    public float Armor => armor;
    public float Size => size;
    public IReadOnlyList<EliteBehaviour> Behaviours => behaviours;
}
