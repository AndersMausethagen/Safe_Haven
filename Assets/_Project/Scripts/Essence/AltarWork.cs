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

    /// <summary>
    /// How many batches can be queued at the altar now: as many as there is room for and Cursed Essence to pay for, and none while no
    /// Alchemist is at work (<paramref name="why"/> says so). The window's buttons and Purify both ask this: the window kept its own sum,
    /// without the guard against a batch of nothing (review 2026-11-27).
    /// </summary>
    public static int Queueable(GameObject user, out string why)
    {
        SettlementSettings settings = SettlementSettings.Current;
        if (!GameSession.Here.HasWorker(settings.PurifierJob))
        {
            why = $"The altar is cold. It needs a resident working as the {(settings.PurifierJob != null ? settings.PurifierJob.DisplayName : "someone")}.";
            return 0;
        }

        why = null;
        int room = settings.MaxQueuedBatches - GameSession.Here.PurificationQueued;
        // As many as there is room for AND essence to pay for: "Purify all" asked for the whole queue's room and, with less Cursed
        // than that, paid for nothing and did nothing (the user's playtest, 2026-11-21).
        int affordable = settings.CursedPerBatch > 0 ? MaterialPool.For(user).Have(settings.CursedEssence) / settings.CursedPerBatch : 0;
        return Mathf.Max(0, Mathf.Min(room, affordable));
    }

    /// <summary>Queues batches at the altar, paying their Cursed Essence now. Returns false when it can't.</summary>
    public static bool Purify(GameObject user, int batches)
    {
        SettlementSettings settings = SettlementSettings.Current;
        batches = Mathf.Min(batches, Queueable(user, out _));
        int cost = batches * settings.CursedPerBatch;
        var price = new[] { new ResourceAmount(settings.CursedEssence, cost) };
        if (batches <= 0 || !MaterialPool.For(user).TrySpend(price))
        {
            return false;
        }

        // A visitor's essence goes on their friend's altar (HavenDecisions).
        return HavenDecisions.Do("altar.purify", batches.ToString(System.Globalization.CultureInfo.InvariantCulture), price);
    }

    [HavenDecision("altar.purify")]
    private static HavenDecisions.Outcome QueueAtAltar(string count)
    {
        SettlementSettings settings = SettlementSettings.Current;
        int room = settings.MaxQueuedBatches - GameSession.Here.PurificationQueued;
        if (!int.TryParse(count, out int batches) || batches <= 0 || batches > room)
        {
            return HavenDecisions.Outcome.Refused("The altar has no room for that much now: your essence is back in your bag.");
        }

        // Made only in the game that keeps the Haven (HavenDecisions.Do), where one's own is the one here.
        GameSession.Home.QueuePurification(batches, settings.MaxQueuedBatches);
        return HavenDecisions.Outcome.Made($"The altar takes {batches * settings.CursedPerBatch} {settings.CursedEssence.DisplayName}.");
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
                ItemDefinition priced = stack.Item;
                offer.Apply = () => Empower(user, held, priced, level, offer.Cost);
                subject.Offers.Add(offer);
            }

            subjects.Add(subject);
        }

        return subjects;
    }

    // The piece priced is the one worked: the window reads the slot again only twice a second, and a piece swapped into it meanwhile
    // was empowered at the old piece's price, or had its carving replaced unseen.
    private static void Empower(GameObject user, Held held, ItemDefinition priced, int level, RepairCost cost)
    {
        ItemStack stack = held.Stack;
        if (stack.IsEmpty || stack.Item != priced || stack.Empower != level - 1 || !MaterialPool.For(user).TrySpend(cost.Resources, cost.Items))
        {
            return;
        }

        held.Container.SetSlot(held.Slot, stack.WithEmpower(level));
        // Work at the altar is Runecraft's practice (Docs/EnchantingResearch.md #4).
        Inscriptions.Practise(stack.Item.Tier, EssenceSettings.Current.EmpowerWork);
        GameEvents.Raise(new GameEvent(GameEventKind.Empowered, stack.Item, level, user));
        Finished(user, $"{stack.Item.DisplayName} is empowered to +{level}.");
    }

    // ----- Enchanting -----

    /// <summary>
    /// The gear in reach that takes an enchantment, with every enchantment that fits it: the known ones to carve, then the unknown
    /// ones greyed with where their inscriptions are found, so each is a goal (Docs/EnchantingResearch.md #8).
    /// </summary>
    public static List<AltarSubject> Enchanting(GameObject user)
    {
        var subjects = new List<AltarSubject>();
        var fitting = new List<EnchantmentDefinition>();
        foreach (Held held in Gear(user))
        {
            ItemStack stack = held.Stack;
            // A relic's power can't be undone here: losing it to a misclick would be the worst loss in the game.
            if (stack.Enchantment != null && stack.Enchantment.IsRelic)
            {
                continue;
            }

            AltarSubject subject = Subject(held);
            fitting.Clear();
            foreach (EnchantmentDefinition enchantment in EssenceSettings.Current.Enchantments)
            {
                if (enchantment != null && enchantment.Fits(stack.Item))
                {
                    fitting.Add(enchantment);
                }
            }

            // Known first, by tier; then the unknown, by tier, so the next goals sit at the top of what is left.
            fitting.Sort((a, b) =>
            {
                int known = GameSession.Progress.Knows(b).CompareTo(GameSession.Progress.Knows(a));
                return known != 0 ? known : a.Tier != b.Tier ? a.Tier.CompareTo(b.Tier) : string.CompareOrdinal(a.DisplayName, b.DisplayName);
            });
            foreach (EnchantmentDefinition enchantment in fitting)
            {
                subject.Offers.Add(EnchantOffer(user, held, stack, enchantment));
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
        UIStyle style = UIStyle.Current;
        bool cursed = enchantment.School == EssenceSchool.Cursed;
        bool known = GameSession.Progress.Knows(enchantment);
        string tags = (enchantment.IsGreat ? $"  <size=75%><color=#{Hex(style.Accent)}>Great</color></size>" : "")
                      + (cursed ? $"  <size=75%><color=#{Hex(style.Danger)}>Cursed</color></size>" : "");
        string effects = Effects(enchantment);
        string what = enchantment.Bonuses.Count > 0 ? ItemTooltipView.DescribeBonuses(enchantment.Bonuses) : "";
        string once = OnceNote(user, held, enchantment);
        bool canCarve = Inscriptions.CanCarve(enchantment, out bool byCarver, out string skillProblem);
        ResidentRecord carver = byCarver ? Inscriptions.Carver() : null;
        var offer = new AltarOffer
        {
            Title = known ? enchantment.DisplayName + tags : $"<alpha=#88>{enchantment.DisplayName}{tags}",
            Detail = known ? (what.Length > 0 ? what : StripTags(effects)) : $"Not known: {enchantment.SourceHint}",
            Effect = $"<b>{enchantment.DisplayName}</b>{Lines(enchantment.Bonuses)}{(effects.Length > 0 ? "\n" + effects : "")}\n\n{enchantment.Description}" +
                     $"\n\n<alpha=#AA>Fits {enchantment.DescribeTargets()}." +
                     (enchantment.RequiredRunecraft > 0 ? $" Needs Runecraft {enchantment.RequiredRunecraft}." : "") +
                     (known ? "" : $"\nNot known yet. {enchantment.SourceHint}: its inscription teaches it, read at home.") +
                     (carver != null ? $"\n{carver.Resident.DisplayName} carves this one for you." : "") +
                     (once.Length > 0 ? "\n" + once : "") +
                     (cursed ? "\nRaw Cursed Essence: stronger, and it always takes its price." : ""),
            Verb = stack.Enchantment != null ? $"Replace {stack.Enchantment.DisplayName}" : "Enchant",
            Done = stack.Enchantment == enchantment,
        };
        offer.Cost.Resources.AddRange(enchantment.Cost);
        offer.Problem = offer.Done ? "It already carries this."
            : !known ? $"Not known yet. {enchantment.SourceHint}."
            : !canCarve ? skillProblem
            : Missing(user, offer.Cost);
        ItemDefinition pricedPiece = stack.Item;
        EnchantmentDefinition carries = stack.Enchantment;
        offer.Apply = () => Enchant(user, held, pricedPiece, carries, enchantment, offer.Cost);
        return offer;
    }

    // A great enchantment's effects, one line each.
    private static string Effects(EnchantmentDefinition enchantment)
    {
        var lines = new System.Text.StringBuilder();
        foreach (RelicEffect effect in enchantment.Effects)
        {
            if (effect != null)
            {
                lines.Append(lines.Length > 0 ? "\n" : "").Append($"<color=#{Hex(UIStyle.Current.Accent)}>{effect.Describe()}</color>");
            }
        }

        return lines.ToString();
    }

    private static string StripTags(string text) => System.Text.RegularExpressions.Regex.Replace(text, "<.*?>", "");

    // "Counts once": another worn piece already gives it, so this would add nothing while both are worn.
    private static string OnceNote(GameObject user, Held held, EnchantmentDefinition enchantment)
    {
        PlayerEquipment equipment = user != null ? user.GetComponent<PlayerEquipment>() : null;
        if (equipment == null)
        {
            return "";
        }

        int except = held.Container == equipment.Gear.Slots ? held.Slot : -1;
        ItemStack carrier = GearPower.CarrierOf(equipment.Gear.Slots, enchantment, except);
        return carrier.IsEmpty ? "" : $"Counts once: your {carrier.DisplayName} already gives it.";
    }

    private static void Enchant(GameObject user, Held held, ItemDefinition priced, EnchantmentDefinition carried, EnchantmentDefinition enchantment, RepairCost cost)
    {
        ItemStack stack = held.Stack;
        if (stack.IsEmpty || stack.Item != priced || stack.Enchantment != carried || !enchantment.Fits(stack.Item) || !GameSession.Progress.Knows(enchantment)
            || !Inscriptions.CanCarve(enchantment, out bool byCarver, out _) || !MaterialPool.For(user).TrySpend(cost.Resources, cost.Items))
        {
            return;
        }

        held.Container.SetSlot(held.Slot, stack.WithEnchantment(enchantment));
        // Carving is Runecraft's practice, and the carver at home learns from every piece worked while she is there. Only a
        // piece carved for the first time teaches: carving one cheap piece over and over with two enchantments in turn was a
        // way to farm both (review 2026-11-16).
        ResidentRecord carver = Inscriptions.Carver();
        if (stack.Enchantment == null)
        {
            Inscriptions.Practise(stack.Item.Tier, EssenceSettings.Current.EnchantWork);
            if (carver != null)
            {
                carver.WorkDone += EssenceSettings.Current.WorkPerEnchanting;
                ResidentTalents.PractiseWork(carver, EssenceSettings.Current.WorkPerEnchanting, stack.Item.Tier);
            }
        }

        GameEvents.Raise(new GameEvent(GameEventKind.Enchanted, enchantment, 1, user, detail: stack.Item));
        Finished(user, byCarver && carver != null
            ? $"{carver.Resident.DisplayName} carves {enchantment.DisplayName} into your {stack.Item.DisplayName}."
            : $"{stack.Item.DisplayName} now carries {enchantment.DisplayName}.");
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
            // The weapon-art entries are the weapon's arts: nothing of theirs deepens (their cost and cooldown are the art's).
            if (skill.WeaponArt != WeaponArtSlot.None)
            {
                continue;
            }

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
                // Mastery: an ability cast often enough earns the rank without essence.
                int uses = GameSession.Progress.UsesOf(skill);
                bool practised = step.practisedUses > 0 && uses >= step.practisedUses;
                if (practised)
                {
                    // Short, as the row is one line: what it gives is on the right when it is picked.
                    offer.Detail = "Practised: free";
                    offer.Effect += $"\n<color=#{Hex(UIStyle.Current.Good)}>Practised: cast {uses} times, so no essence is needed.</color>";
                }
                else
                {
                    offer.Cost.Resources.Add(new ResourceAmount(purified, step.purified));
                    if (step.practisedUses > 0)
                    {
                        offer.Effect += $"\n<color=#{Hex(UIStyle.Current.Muted)}>Or free once cast {step.practisedUses} times ({uses} so far).</color>";
                    }
                }

                offer.Problem = offer.Done ? "It already holds this." : target > rank + 1 ? $"Reach rank {Roman(rank + 1)} first." : Missing(user, offer.Cost);
                offer.Apply = () => Deepen(user, skill, target, offer.Cost);
                subject.Offers.Add(offer);
            }

            AddChoices(user, skill, rank, subject);
            subjects.Add(subject);
        }

        return subjects;
    }

    // The ability's choices, one per rank (Docs/SkillsImprovementResearch_2026-11-24.md 3): free to take or change once it holds that rank.
    private static void AddChoices(GameObject user, SkillDefinition skill, int rank, AltarSubject subject)
    {
        foreach (SkillOption option in skill.Options)
        {
            if (option == null || string.IsNullOrEmpty(option.id))
            {
                continue;
            }

            SkillOption chosen = option;
            string current = GameSession.Progress.ChoiceOf(skill, option.rank);
            bool taken = current == option.id;
            SkillOption replaced = null;
            foreach (SkillOption other in skill.Options)
            {
                if (!taken && other != null && other.rank == option.rank && other.id == current)
                {
                    replaced = other;
                }
            }

            // The row is one line (its words were cut mid-word): the choice's name and where it stands; its words are on the right
            // when it is picked.
            var offer = new AltarOffer
            {
                Title = $"Choice {Roman(option.rank)}: {option.displayName}",
                Detail = taken ? "Your choice" : replaced != null ? $"Instead of {replaced.displayName}" : $"Opens at rank {Roman(option.rank)}",
                Effect = $"<b>{skill.DisplayName}: {option.displayName}</b>\n{option.description}\n\n<color=#{Hex(UIStyle.Current.Muted)}>One choice at rank " +
                         $"{Roman(option.rank)}; free to change here.</color>",
                Verb = taken ? "Chosen" : replaced != null ? "Change to this" : "Choose",
                Done = taken,
            };
            if (replaced != null)
            {
                offer.Effect += $"\n<color=#{Hex(UIStyle.Current.Muted)}>It replaces {replaced.displayName}.</color>";
            }
            offer.Problem = taken ? "Already chosen." : rank < option.rank ? $"Deepen to rank {Roman(option.rank)} first." : null;
            offer.Apply = () =>
            {
                GameSession.Progress.Choose(skill, chosen.rank, chosen.id);
                Finished(user, $"{skill.DisplayName}: {chosen.displayName}.");
            };
            subject.Offers.Add(offer);
        }
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
            lines.Append($"\n<color=#{Hex(StatNames.IsPenalty(bonus.stat, bonus.value) ? style.Danger : style.Good)}>{bonus.Describe()}</color>");
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
