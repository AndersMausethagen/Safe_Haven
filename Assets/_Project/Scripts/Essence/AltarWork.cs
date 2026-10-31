using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>One thing that can be done to a subject at the altar: a level, an enchantment or a rank.</summary>
public sealed class AltarOffer
{
    public string Title = "";
    /// <summary>One line for its row.</summary>
    public string Detail = "";
    /// <summary>What it does, in rich text, for the panel beside the list.</summary>
    public string Effect = "";
    /// <summary>The word on the button.</summary>
    public string Verb = "";
    public RepairCost Cost = new RepairCost();
    /// <summary>Already done to the subject.</summary>
    public bool Done;
    /// <summary>Why it can't be done now, in words for the player, or null when it can.</summary>
    public string Problem;
    /// <summary>Pays and does it.</summary>
    public Action Apply;
}

/// <summary>Something at the altar to work on: a piece of gear or a skill, with what can be done to it.</summary>
public sealed class AltarSubject
{
    public string Title = "";
    public string Detail = "";
    public Sprite Icon;
    public readonly List<AltarOffer> Offers = new List<AltarOffer>();
}

/// <summary>
/// What can be done at the Purification Altar and doing it (GDD §13.5, §16.4): purifying, empowering gear, enchanting it and
/// deepening skills. Everything is deterministic: an offer says what it costs and what it does, and never fails or wastes
/// essence. Costs are paid from the MaterialPool, so at home storage counts. AltarWindow draws what this returns.
/// </summary>
public static class AltarWork
{
    private static readonly string[] Numerals = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII" };

    private readonly struct Held
    {
        public readonly ItemContainer Container;
        public readonly int Slot;
        public readonly string Place;

        public Held(ItemContainer container, int slot, string place)
        {
            Container = container;
            Slot = slot;
            Place = place;
        }

        public ItemStack Stack => Container.GetSlot(Slot);
    }

    public static string Roman(int rank) => rank >= 1 && rank <= Numerals.Length ? Numerals[rank - 1] : rank.ToString();

    // ----- Purifying -----

    /// <summary>Queues batches at the altar, paying their Cursed Essence now. Returns false when it can't.</summary>
    public static bool Purify(GameObject user, int batches)
    {
        SettlementSettings settings = SettlementSettings.Current;
        int room = settings.MaxQueuedBatches - GameSession.Settlement.PurificationQueued;
        batches = Mathf.Min(batches, room);
        int cost = batches * settings.CursedPerBatch;
        var price = new[] { new ResourceAmount(settings.CursedEssence, cost) };
        if (batches <= 0 || !MaterialPool.For(user).TrySpend(price))
        {
            return false;
        }

        GameSession.Settlement.QueuePurification(batches, settings.MaxQueuedBatches);
        GameMessages.Post($"The altar takes {cost} {settings.CursedEssence.DisplayName}.");
        return true;
    }

    // ----- Empowering -----

    /// <summary>The gear in reach that can be empowered, with a level offered for each step.</summary>
    public static List<AltarSubject> Empowering(GameObject user)
    {
        var subjects = new List<AltarSubject>();
        EssenceSettings settings = EssenceSettings.Current;
        ResourceDefinition purified = SettlementSettings.Current.PurifiedEssence;
        var crafting = user != null ? user.GetComponent<PlayerCrafting>() : null;
        foreach (Held held in Gear(user))
        {
            ItemStack stack = held.Stack;
            if (!GearPower.CanEmpower(stack.Item))
            {
                continue;
            }

            AltarSubject subject = Subject(held);
            RecipeDefinition recipe = crafting != null ? Repairs.RecipeFor(stack.Item, crafting.Recipes) : null;
            for (int i = 1; i <= settings.EmpowerLevels.Count; i++)
            {
                int level = i;
                List<StatBonus> bonuses = GearPower.EmpowerBonuses(stack.Item, level, settings);
                var offer = new AltarOffer
                {
                    Title = $"+{level}",
                    Detail = ItemTooltipView.DescribeBonuses(bonuses),
                    Effect = $"<b>{stack.Item.DisplayName} +{level}</b>{Lines(bonuses)}",
                    Verb = $"Empower to +{level}",
                    Cost = GearPower.EmpowerCost(stack.Item, level, recipe, purified, settings),
                    Done = level <= stack.Empower,
                };
                offer.Problem = offer.Done ? "It already holds this." : level > stack.Empower + 1 ? $"Reach +{stack.Empower + 1} first." : Missing(user, offer.Cost);
                offer.Apply = () => Empower(user, held, level, offer.Cost);
                subject.Offers.Add(offer);
            }

            subjects.Add(subject);
        }

        return subjects;
    }

    private static void Empower(GameObject user, Held held, int level, RepairCost cost)
    {
        ItemStack stack = held.Stack;
        if (stack.IsEmpty || stack.Empower != level - 1 || !MaterialPool.For(user).TrySpend(cost.Resources, cost.Items))
        {
            return;
        }

        held.Container.SetSlot(held.Slot, stack.WithEmpower(level));
        Finished(user, $"{stack.Item.DisplayName} is empowered to +{level}.");
    }

    // ----- Enchanting -----

    /// <summary>The gear in reach that takes an enchantment, with every enchantment that fits it.</summary>
    public static List<AltarSubject> Enchanting(GameObject user)
    {
        var subjects = new List<AltarSubject>();
        foreach (Held held in Gear(user))
        {
            ItemStack stack = held.Stack;
            // A relic's power can't be undone here: losing it to a misclick would be the worst loss in the game.
            if (stack.Enchantment != null && stack.Enchantment.IsRelic)
            {
                continue;
            }

            AltarSubject subject = Subject(held);
            foreach (EnchantmentDefinition enchantment in EssenceSettings.Current.Enchantments)
            {
                if (enchantment != null && enchantment.Fits(stack.Item))
                {
                    subject.Offers.Add(EnchantOffer(user, held, stack, enchantment));
                }
            }

            if (subject.Offers.Count > 0)
            {
                subjects.Add(subject);
            }
        }

        return subjects;
    }

    private static AltarOffer EnchantOffer(GameObject user, Held held, ItemStack stack, EnchantmentDefinition enchantment)
    {
        bool cursed = enchantment.School == EssenceSchool.Cursed;
        var offer = new AltarOffer
        {
            Title = cursed ? $"{enchantment.DisplayName}  <size=75%><color=#{Hex(UIStyle.Current.Danger)}>Cursed</color></size>" : enchantment.DisplayName,
            Detail = ItemTooltipView.DescribeBonuses(enchantment.Bonuses),
            Effect = $"<b>{enchantment.DisplayName}</b>{Lines(enchantment.Bonuses)}\n\n{enchantment.Description}" +
                     (cursed ? "\n\n<alpha=#AA>Raw Cursed Essence: stronger, and it always takes its price." : ""),
            Verb = stack.Enchantment != null ? $"Replace {stack.Enchantment.DisplayName}" : "Enchant",
            Done = stack.Enchantment == enchantment,
        };
        offer.Cost.Resources.AddRange(enchantment.Cost);
        offer.Problem = offer.Done ? "It already carries this." : Missing(user, offer.Cost);
        offer.Apply = () => Enchant(user, held, enchantment, offer.Cost);
        return offer;
    }

    private static void Enchant(GameObject user, Held held, EnchantmentDefinition enchantment, RepairCost cost)
    {
        ItemStack stack = held.Stack;
        if (stack.IsEmpty || !enchantment.Fits(stack.Item) || !MaterialPool.For(user).TrySpend(cost.Resources, cost.Items))
        {
            return;
        }

        held.Container.SetSlot(held.Slot, stack.WithEnchantment(enchantment));
        Finished(user, $"{stack.Item.DisplayName} now carries {enchantment.DisplayName}.");
    }

    // ----- Deepening skills -----

    /// <summary>The skills the player knows, with a rank offered for each step.</summary>
    public static List<AltarSubject> Deepening(GameObject user)
    {
        var subjects = new List<AltarSubject>();
        var skills = user != null ? user.GetComponent<PlayerSkills>() : null;
        if (skills == null)
        {
            return subjects;
        }

        EssenceSettings settings = EssenceSettings.Current;
        ResourceDefinition purified = SettlementSettings.Current.PurifiedEssence;
        foreach (SkillDefinition skill in skills.KnownSkills)
        {
            int rank = GameSession.Progress.RankOf(skill);
            var subject = new AltarSubject { Title = skill.DisplayName, Detail = rank > 0 ? $"Rank {Roman(rank)}" : "Not deepened", Icon = skill.Icon };
            for (int i = 1; i <= settings.SkillRanks.Count; i++)
            {
                int target = i;
                EssenceSettings.SkillRank step = settings.SkillRanks[i - 1];
                string gives = $"+{step.power * 100f:0}% power, {step.cooldownCut * 100f:0}% less cooldown";
                var offer = new AltarOffer
                {
                    Title = $"Rank {Roman(target)}",
                    Detail = gives,
                    Effect = $"<b>{skill.DisplayName}, rank {Roman(target)}</b>\n<color=#{Hex(UIStyle.Current.Good)}>{gives}</color>\n\n" +
                             $"Cooldown {skill.Cooldown * (1f - step.cooldownCut):0.#} s. Power counts for its damage, healing and buffs.",
                    Verb = $"Deepen to rank {Roman(target)}",
                    Done = target <= rank,
                };
                offer.Cost.Resources.Add(new ResourceAmount(purified, step.purified));
                offer.Problem = offer.Done ? "It already holds this." : target > rank + 1 ? $"Reach rank {Roman(rank + 1)} first." : Missing(user, offer.Cost);
                offer.Apply = () => Deepen(user, skill, target, offer.Cost);
                subject.Offers.Add(offer);
            }

            subjects.Add(subject);
        }

        return subjects;
    }

    private static void Deepen(GameObject user, SkillDefinition skill, int rank, RepairCost cost)
    {
        if (GameSession.Progress.RankOf(skill) != rank - 1 || !MaterialPool.For(user).TrySpend(cost.Resources, cost.Items))
        {
            return;
        }

        GameSession.Progress.DeepenSkill(skill);
        Finished(user, $"{skill.DisplayName} deepens to rank {Roman(rank)}.");
    }

    // ----- Shared -----

    // What the player wears first: it is what they will want to work on.
    private static IEnumerable<Held> Gear(GameObject user)
    {
        PlayerEquipment equipment = user != null ? user.GetComponent<PlayerEquipment>() : null;
        ItemInventory inventory = user != null ? user.GetComponent<ItemInventory>() : null;
        var held = new List<Held>();
        if (equipment != null)
        {
            Collect(equipment.Gear.Slots, "Worn", held);
        }

        if (inventory != null)
        {
            Collect(inventory.Container, "In the bag", held);
            Collect(inventory.SafePocket, "In the pocket", held);
        }

        return held;
    }

    private static void Collect(ItemContainer container, string place, List<Held> into)
    {
        if (container == null)
        {
            return;
        }

        for (int i = 0; i < container.SlotCount; i++)
        {
            ItemStack stack = container.GetSlot(i);
            if (!stack.IsEmpty && stack.Item.HasDurability)
            {
                into.Add(new Held(container, i, place));
            }
        }
    }

    private static AltarSubject Subject(Held held)
    {
        ItemStack stack = held.Stack;
        return new AltarSubject { Title = stack.DisplayName, Detail = held.Place, Icon = stack.Item.Icon };
    }

    private static string Missing(GameObject user, RepairCost cost)
    {
        return MaterialPool.For(user).CanAfford(cost.Resources, cost.Items, out string missing) ? null : missing;
    }

    // Each on its line; what is lost in the enchantment's bargain reads as a warning, and says so in the number too.
    private static string Lines(IReadOnlyList<StatBonus> bonuses)
    {
        UIStyle style = UIStyle.Current;
        var lines = new System.Text.StringBuilder();
        foreach (StatBonus bonus in bonuses)
        {
            lines.Append($"\n<color=#{Hex(bonus.value >= 0f ? style.Good : style.Danger)}>{bonus.Describe()}</color>");
        }

        return lines.ToString();
    }

    private static string Hex(Color colour) => ColorUtility.ToHtmlStringRGB(colour);

    private static void Finished(GameObject user, string message)
    {
        GameMessages.Post(message, MessageKind.Gain);
        AudioManager.Play(HavenSettings.Current.RankSound, user != null ? user.transform.position : Vector3.zero);
    }
}
