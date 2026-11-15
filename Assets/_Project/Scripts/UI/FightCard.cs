using UnityEngine;

/// <summary>
/// The character window's Fight card (Docs/PlayerStatsResearch.md #2), worked out in plain words from the player's stats now: damage as
/// one share of the weapon's (Might, Damage Bonus, the Melee skill and critical hits, FightMath), blows to fell and blows to take against
/// the reference creature at the raid tier ahead (the one the gear is ready for, or the raid the player is in), stamina as dodges or
/// swings, and the dodge the equipment load gives with how far it is from the next class. Nothing new on the HUD.
/// </summary>
public readonly struct FightCard
{
    // The raid tiers' reference creature (RaidTierEditor): 60 health, 5 armour, hits for 15.
    public const float ReferenceHealth = 60f;
    public const float ReferenceArmor = 5f;
    public const float ReferenceDamage = 15f;

    public readonly string Damage;
    public readonly string Blows;
    public readonly string Stamina;
    public readonly string Dodge;

    private FightCard(string damage, string blows, string stamina, string dodge)
    {
        Damage = damage;
        Blows = blows;
        Stamina = stamina;
        Dodge = dodge;
    }

    public static FightCard Read(GameObject player, StatSheet stats, CombatRules rules, float maxHealth)
    {
        string muted = UIStyle.Current.MutedHex;
        FightMath.Totals totals = FightMath.Of(stats, maxHealth);
        float share = totals.DamageShare(rules);
        PlayerEquipment equipment = player.GetComponent<PlayerEquipment>();
        PlayerCombat combat = player.GetComponent<PlayerCombat>();
        WeaponDefinition weapon = combat != null ? combat.Weapon : null;
        float weaponDamage = weapon != null && !weapon.IsBow ? weapon.Damage : 0f;
        string damage = weapon != null && weaponDamage > 0f
            ? $"×{share:0.00} <color={muted}>{WeaponName(equipment, weapon)} {weaponDamage:0} → {weaponDamage * share:0.#} a blow</color>"
            : $"×{share:0.00} <color={muted}>of a weapon's</color>";

        // Against the raid tier ahead: the one being fought in, else the highest the gear is ready for.
        RaidDifficulty tier = TierAhead(player);
        string blows = "-";
        if (tier != null)
        {
            float kill = weaponDamage > 0f ? FightMath.HitsToKill(weaponDamage * share, ReferenceHealth, ReferenceArmor, tier.enemyHealth, rules) : 0f;
            float die = FightMath.HitsToDie(totals.Toughness(rules), ReferenceDamage, tier.enemyPower);
            blows = $"{(kill > 0f ? $"fell in {kill:0.#}" : "unarmed")} · take {die:0.#} <color={muted}>(tier {Tiers.ToRoman(tier.tier)})</color>";
        }

        PlayerStamina stamina = player.GetComponent<PlayerStamina>();
        PlayerMovement movement = player.GetComponent<PlayerMovement>();
        PlayerMovementSettings settings = movement != null ? movement.Settings : null;
        float load = equipment != null ? equipment.EquipLoad : 0f;
        DodgeStyle style = settings != null ? settings.StyleFor(load) : DodgeStyle.Roll;
        string staminaText = "-";
        if (stamina != null && settings != null)
        {
            float dodgeCost = stamina.DodgeCost * settings.ShapeOf(style).stamina * (combat != null ? combat.DodgeCostScale : 1f);
            float swingCost = weapon != null && weapon.Combo.Count > 0 && weapon.Combo[0] != null
                ? weapon.Combo[0].StaminaCost * Mathf.Max(0.1f, 1f + stats.Get(StatType.MeleeStaminaCost)) : 0f;
            int dodges = dodgeCost > 0f ? Mathf.FloorToInt(stamina.Max / dodgeCost) : 0;
            int swings = swingCost > 0f ? Mathf.FloorToInt(stamina.Max / swingCost) : 0;
            staminaText = $"{Mathf.RoundToInt(stamina.Max)} <color={muted}>{dodges} dodges{(swings > 0 ? $" or {swings} swings" : "")}</color>";
        }

        string dodgeText = settings == null ? "-" : style switch
        {
            DodgeStyle.Quickstep => $"Quickstep <color={muted}>load {load:0.#}, a roll past {settings.LightLoad:0.#}</color>",
            DodgeStyle.HeavyRoll => $"Heavy roll <color={muted}>load {load:0.#}, a roll under {settings.HeavyLoad:0.#}</color>",
            _ => $"Roll <color={muted}>load {load:0.#}, quickstep at {settings.LightLoad:0.#}</color>",
        };

        return new FightCard(damage, blows, staminaText, dodgeText);
    }

    private static string WeaponName(PlayerEquipment equipment, WeaponDefinition weapon) =>
        equipment != null && !equipment.MainHand.IsEmpty ? equipment.MainHand.Item.DisplayName : weapon.name;

    private static RaidDifficulty TierAhead(GameObject player)
    {
        if (GameSession.ActiveRaid != null && GameSession.ActiveRaid.Difficulty != null)
        {
            return GameSession.ActiveRaid.Difficulty;
        }

        RaidPortal portal = Object.FindFirstObjectByType<RaidPortal>();
        if (portal == null || portal.Destinations == null || portal.Destinations.Count == 0 || portal.Destinations[0] == null)
        {
            return null;
        }

        return portal.Destinations[0].DifficultyFor(GearScore.Of(player));
    }
}
