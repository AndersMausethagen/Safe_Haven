using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// How enchantments are learned (Docs/EnchantingResearch.md #2-#6). An inscription is a parchment carrying one enchantment (the
/// stack's Enchantment) until it is read at home, which teaches it for good. Inscriptions come from raids, each source with its own
/// list: carved stones (a rubbing on a Blank Parchment), elites (their own mark's), great creatures (their own) and caches and strange
/// events. Every source gives one not yet known while it has any (known, or carried home unread), so a drop is never a waste; once a
/// list is done a copy still teaches Runecraft, or goes to Liv. Carving needs Runecraft at the enchantment's level, or Liv at home,
/// whose rank carves past the player's own. Plain rules over the settings and GameSession; the altar, loot and stones call these.
/// </summary>
public static class Inscriptions
{
    /// <summary>Every enchantment of <paramref name="source"/> handed out at raids of <paramref name="tier"/> (its Tier and up).</summary>
    public static List<EnchantmentDefinition> Pool(EnchantmentSource source, int tier)
    {
        var pool = new List<EnchantmentDefinition>();
        foreach (EnchantmentDefinition enchantment in EssenceSettings.Current.Enchantments)
        {
            if (enchantment != null && enchantment.Source == source && enchantment.Tier <= tier)
            {
                pool.Add(enchantment);
            }
        }

        return pool;
    }

    /// <summary>True when the player knows it, or carries its inscription home unread (the bag, the pocket).</summary>
    public static bool KnownOrCarried(EnchantmentDefinition enchantment, GameObject player)
    {
        if (enchantment == null || GameSession.Progress.Knows(enchantment))
        {
            return true;
        }

        // The stash too: an unread inscription put away at home is still one the player has (review 2026-11-16).
        ItemInventory bag = player != null ? player.GetComponentInParent<ItemInventory>() : null;
        return (bag != null && (Carries(bag.Container, enchantment) || Carries(bag.SafePocket, enchantment)))
            || Carries(GameSession.Stash.Items, enchantment);
    }

    private static bool Carries(ItemContainer container, EnchantmentDefinition enchantment)
    {
        for (int i = 0; container != null && i < container.SlotCount; i++)
        {
            ItemStack stack = container.GetSlot(i);
            if (!stack.IsEmpty && stack.Item.IsInscription && stack.Enchantment == enchantment)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// One of <paramref name="pool"/>: one the player neither knows nor carries while there is any, else any (a copy) when
    /// <paramref name="copies"/>, else null. Monster Hunter's lesson: never the same find while others are left.
    /// </summary>
    public static EnchantmentDefinition Pick(IReadOnlyList<EnchantmentDefinition> pool, GameObject player, System.Random random, bool copies = true)
    {
        if (pool == null || pool.Count == 0)
        {
            return null;
        }

        var unknown = new List<EnchantmentDefinition>();
        foreach (EnchantmentDefinition enchantment in pool)
        {
            if (enchantment != null && !KnownOrCarried(enchantment, player))
            {
                unknown.Add(enchantment);
            }
        }

        if (unknown.Count > 0)
        {
            return unknown[random.Next(unknown.Count)];
        }

        return copies ? pool[random.Next(pool.Count)] : null;
    }

    /// <summary>A parchment carrying <paramref name="enchantment"/>.</summary>
    public static ItemStack Make(EnchantmentDefinition enchantment)
    {
        ItemDefinition parchment = EssenceSettings.Current.Inscription;
        return parchment == null || enchantment == null ? ItemStack.Empty : new ItemStack(parchment, 1, 1f, 0, enchantment);
    }

    /// <summary>
    /// Puts an inscription in the player's bag (at their feet when it is full), with the find's sound and words, and notes it for the
    /// raid's report and the playtest log. <paramref name="from"/> says where it came from: "a carved stone", "the Frenzied wolf".
    /// </summary>
    public static ItemStack Grant(GameObject player, EnchantmentDefinition enchantment, string from)
    {
        ItemStack found = Make(enchantment);
        ItemInventory bag = player != null ? player.GetComponentInParent<ItemInventory>() : null;
        if (found.IsEmpty || bag == null)
        {
            return ItemStack.Empty;
        }

        if (ItemDelivery.Give(bag, found, newFind: false, toStash: false).Fell)
        {
            GameMessages.Post("Bag full: it falls at your feet.", MessageKind.Warning);
        }

        bool known = GameSession.Progress.Knows(enchantment);
        GameMessages.Post(known ? $"An inscription: {enchantment.DisplayName}, one you know. Liv would want it."
                                : $"An inscription: {enchantment.DisplayName}! Read it at home to learn it.", MessageKind.Gain);
        AudioManager.Play(Rarity.StyleOf(known ? ItemRarity.Rare : ItemRarity.Legendary).PickupSound, bag.transform.position);
        RaidDirector raid = RaidDirector.Current;
        raid?.Session.RecordEvent($"Found the {enchantment.DisplayName} inscription");
        GameEvents.Raise(new GameEvent(GameEventKind.InscriptionFound, enchantment, enchantment.Tier, player));
        return found;
    }

    /// <summary>
    /// Reads the inscription in a slot: at home it teaches its enchantment for good; one already known is studied for Runecraft
    /// instead. Out on a raid it waits: the find has to get home. True when it was used up.
    /// </summary>
    public static bool Read(GameObject reader, ItemContainer from, int slot)
    {
        ItemStack stack = from != null ? from.GetSlot(slot) : ItemStack.Empty;
        EnchantmentDefinition enchantment = stack.Enchantment;
        if (stack.IsEmpty || enchantment == null)
        {
            GameMessages.Post("The parchment is blank. Take a rubbing of a carved stone with it.", MessageKind.Warning);
            return false;
        }

        if (RaidDirector.Current != null)
        {
            GameMessages.Post($"There is no time to work out {enchantment.DisplayName} out here. Get it home.", MessageKind.Warning);
            return false;
        }

        from.RemoveAt(slot, 1);
        if (!Learn(enchantment, reader, "inscription"))
        {
            int learned = Study(enchantment, EssenceSettings.Current.StudyWork);
            GameMessages.Post($"You know {enchantment.DisplayName} already. Working it through again steadies your hand (+{learned} Runecraft).", MessageKind.Gain);
        }

        GameEvents.Raise(new GameEvent(GameEventKind.Consumed, stack.Item, 1, reader));
        return true;
    }

    /// <summary>Learns an enchantment for good, with the words and the event quests count (and the playtest log writes). False when it
    /// was already known. <paramref name="how"/>: "inscription", "given", "quest".</summary>
    public static bool Learn(EnchantmentDefinition enchantment, GameObject by, string how)
    {
        if (!GameSession.Progress.Learn(enchantment))
        {
            return false;
        }

        GameMessages.Post($"You can carve {enchantment.DisplayName} now: {enchantment.DescribeTargets()}, at the altar.", MessageKind.Gain);
        GameEvents.Raise(new GameEvent(GameEventKind.EnchantmentLearned, enchantment, 1, by));
        return true;
    }

    /// <summary>Runecraft practice at the enchantment's tier, times <paramref name="work"/>. Returns the experience it gave.</summary>
    public static int Study(EnchantmentDefinition enchantment, float work) => Practise(enchantment != null ? enchantment.Tier : 1, work);

    /// <summary>Runecraft practice: the tier's experience times <paramref name="work"/>. Returns the experience it gave.</summary>
    public static int Practise(int tier, float work)
    {
        ProficiencyDefinition skill = EssenceSettings.Current.EnchantingSkill;
        if (skill == null || work <= 0f)
        {
            return 0;
        }

        int before = GameSession.Progress.ExperienceOf(skill);
        ExperienceTracker.Practise(skill, Mathf.Max(1, tier), LevelingSettings.Current.TierExperience(Mathf.Max(1, tier)) * work, false);
        return GameSession.Progress.ExperienceOf(skill) - before;
    }

    /// <summary>The player's own Runecraft level.</summary>
    public static int RunecraftLevel()
    {
        ProficiencyDefinition skill = EssenceSettings.Current.EnchantingSkill;
        return skill != null ? GameSession.Progress.LevelOf(skill, LevelingSettings.Current) : 0;
    }

    /// <summary>The resident at home who carves (a job with Carves Past, Liv), or null. Not while away on a raid with the player.</summary>
    public static ResidentRecord Carver()
    {
        // The player's own carver: in a friend's Haven (co-op) theirs carves for them, not the host's (it lent its reach and wrote its
        // work on the copy, undone at the host's next word), as Liv's carving talk is offered only at home.
        if (HavenVisit.InHostsHaven)
        {
            return null;
        }

        foreach (ResidentRecord record in GameSession.Here.Residents)
        {
            if (record != null && record.Job != null && record.Job.Carves)
            {
                return record;
            }
        }

        return null;
    }

    /// <summary>How many Runecraft levels past the player's own the carver at home can carve, by her rank. 0 without one.</summary>
    public static int CarverReach(ResidentRecord carver) =>
        carver == null ? 0 : carver.Job.CarvesPastAt(ResidentTalents.RankIndex(carver));

    /// <summary>
    /// Whether the enchantment can be carved now, and by whom: the player's own hand, or the carver's. <paramref name="problem"/> says
    /// what is missing: "Needs Runecraft 25 (you have 12; Liv reaches 22)".
    /// </summary>
    public static bool CanCarve(EnchantmentDefinition enchantment, out bool byCarver, out string problem)
    {
        byCarver = false;
        problem = null;
        int own = RunecraftLevel();
        if (enchantment == null || enchantment.RequiredRunecraft <= own)
        {
            return true;
        }

        ResidentRecord carver = Carver();
        int reach = CarverReach(carver);
        if (carver != null && enchantment.RequiredRunecraft <= own + reach)
        {
            byCarver = true;
            return true;
        }

        string skill = EssenceSettings.Current.EnchantingSkill != null ? EssenceSettings.Current.EnchantingSkill.DisplayName : "Runecraft";
        problem = carver != null
            ? $"Needs {skill} {enchantment.RequiredRunecraft} (you have {own}; {carver.Resident.DisplayName} reaches {own + reach})."
            : $"Needs {skill} {enchantment.RequiredRunecraft} (you have {own}). Someone who knows the old marks could carve it for you.";
        return false;
    }

    /// <summary>Hands an inscription to the carver at home: her rank grows, and the player learns from watching her read it.</summary>
    public static bool Give(GameObject player, ItemContainer from, int slot, ResidentRecord carver)
    {
        ItemStack stack = from != null ? from.GetSlot(slot) : ItemStack.Empty;
        if (carver == null || stack.IsEmpty || !stack.Item.IsInscription || stack.Enchantment == null)
        {
            return false;
        }

        EssenceSettings settings = EssenceSettings.Current;
        from.RemoveAt(slot, 1);
        carver.WorkDone += settings.WorkPerInscriptionGiven;
        ResidentTalents.PractiseWork(carver, settings.WorkPerInscriptionGiven, stack.Item != null ? stack.Item.Tier : 1);
        Learn(stack.Enchantment, player, "given");
        Study(stack.Enchantment, settings.StudyWork);
        GameEvents.Raise(new GameEvent(GameEventKind.InscriptionGiven, stack.Enchantment, 1, player, detail: carver.Resident));
        return true;
    }
}
