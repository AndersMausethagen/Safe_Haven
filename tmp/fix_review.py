"""The review's eight fixes (and the card over a raid), applied in one go after the test run. Line-ending safe."""
import os

ROOT = r"D:\Safe Haven\Safe Haven\Assets\_Project\Scripts"


def edit(rel, pairs):
    p = os.path.join(ROOT, rel)
    raw = open(p, encoding="utf-8-sig" if open(p, "rb").read(3) == b"\xef\xbb\xbf" else "utf-8", newline="").read()
    bom = open(p, "rb").read(3) == b"\xef\xbb\xbf"
    crlf = "\r\n" in raw
    s = raw.replace("\r\n", "\n")
    for old, new in pairs:
        assert s.count(old) == 1, (rel, old[:80], s.count(old))
        s = s.replace(old, new)
    if crlf:
        s = s.replace("\n", "\r\n")
    open(p, "w", encoding="utf-8-sig" if bom else "utf-8", newline="").write(s)
    print("ok", rel)


# 1. A full bag never swallows a find: refused until it fits; given only when it does.
edit("Level/PlaceFind.cs", [
    ("""        reason = null;
        return find != null && Player.Current != null && user == Player.Current.gameObject;""",
     """        reason = null;
        if (find == null || Player.Current == null || user != Player.Current.gameObject)
        {
            return false;
        }

        // Taken whole or not at all: a full bag lost the stack for good, and the quest still counted it.
        if (Player.Current.Bag.Container.SpaceFor(find.item) < find.amount)
        {
            reason = $"Your bag has no room for {find.amount} {find.item.DisplayName}.";
            return false;
        }

        return true;"""),
    ("""        if (find == null || player == null || !GameSession.Progress.MarkStoneUsed(TakenKey(place.id, find.item.Id)))
        {
            return;
        }""",
     """        if (find == null || player == null || player.Bag.Container.SpaceFor(find.item) < find.amount
            || !GameSession.Progress.MarkStoneUsed(TakenKey(place.id, find.item.Id)))
        {
            return;
        }"""),
])

# 2. A friend who follows runs on the host's clock, not their own Raid Length.
edit("Raids/RaidParameters.cs", [
    ("""        RaidTimeOfDay timeOfDay = null, RaidDifficulty difficulty = null, float extractionCut = 0f)
    {""",
     """        RaidTimeOfDay timeOfDay = null, RaidDifficulty difficulty = null, float extractionCut = 0f, float lengthShare = -1f)
    {
        // Below 0: this game's own Raid Length (Accessibility). A friend following a raid runs on the host's clock instead (RaidLauncher).
        float length = lengthShare < 0f ? GameOptions.RaidLengthShare : lengthShare;"""),
    ("""        if (GameOptions.RaidLengthShare > 1f)
        {
            effects.Add(new RaidStatModifier(RaidStat.Duration, StatModifierOp.Percent, GameOptions.RaidLengthShare - 1f));
        }""",
     """        if (length > 1f)
        {
            effects.Add(new RaidStatModifier(RaidStat.Duration, StatModifierOp.Percent, length - 1f));
        }"""),
])
edit("Raids/RaidLauncher.cs", [
    ("""    public static RaidSession FromInvite(RaidInvite invite, RaidDefinition raid, ContentDatabase content, PlayerSnapshot loadout, string returnScene, float elapsed) =>
        Build(raid, invite.ModifiersFrom(content), loadout, returnScene, invite.seed, invite.DifficultyIn(raid), invite.sparks, invite.large && raid.CanBeLarge,
            invite.extraSeconds, invite.extractionCut, invite.foretoldEvents, null, elapsed, invite.questsUnderWay);""",
     """    public static RaidSession FromInvite(RaidInvite invite, RaidDefinition raid, ContentDatabase content, PlayerSnapshot loadout, string returnScene, float elapsed)
    {
        // The host's clock: this game's own Raid Length left out, then the total set to the host's (which carries theirs and any time
        // events added), so both clocks end together.
        RaidSession session = Build(raid, invite.ModifiersFrom(content), loadout, returnScene, invite.seed, invite.DifficultyIn(raid), invite.sparks,
            invite.large && raid.CanBeLarge, invite.extraSeconds, invite.extractionCut, invite.foretoldEvents, null, 0f, invite.questsUnderWay, lengthShare: 1f);
        if (invite.totalSeconds > 0f)
        {
            session.AddTime(invite.totalSeconds - session.TotalSeconds);
        }

        if (elapsed > 0f)
        {
            session.Tick(elapsed);
        }

        return session;
    }"""),
    ("""        IReadOnlyList<string> questsUnderWay = null)
    {
        RaidParameters parameters = RaidParameters.Calculate(raid, modifiers, extraSeconds, RaidTimeOfDay.For(raid, runSeed), difficulty, extractionCut);""",
     """        IReadOnlyList<string> questsUnderWay = null, float lengthShare = -1f)
    {
        RaidParameters parameters = RaidParameters.Calculate(raid, modifiers, extraSeconds, RaidTimeOfDay.For(raid, runSeed), difficulty, extractionCut, lengthShare);"""),
])

# 3. A tracked level is dropped once built, and only the player's own Haven is asked what stands.
edit("Progression/CharacterProgress.cs", [
    ("""    public bool IsTracked(string key) => key != null && tracked.Contains(key);""",
     """    public bool IsTracked(string key) => key != null && tracked.Contains(key);

    /// <summary>Stops tracking a target (built, so done). True when it was tracked.</summary>
    public bool RemoveTracked(string key) => key != null && tracked.Remove(key);"""),
])
edit("Settlement/WantedAtHome.cs", [
    ("""        GameEvents.Raised -= OnRaised;
        GameEvents.Raised += OnRaised;
    }
""",
     """        GameEvents.Raised -= OnRaised;
        GameEvents.Raised += OnRaised;
        Construction.Finished -= OnFinished;
        Construction.Finished += OnFinished;
    }

    // A level finished in the player's own Haven is done with: its key goes, so it neither counts on a raid nor holds a slot.
    private static void OnFinished(Construction construction)
    {
        if (construction == null || HavenVisit.IsVisiting || GameSession.Progress == null
            || construction.Definition == null)
        {
            return;
        }

        for (int tier = 1; tier <= construction.Level; tier++)
        {
            GameSession.Progress.RemoveTracked(BuildingKey(construction.Definition, tier));
        }
    }
"""),
    ("""            BuildingDefinition building = content.FindBuilding(parts[1]);
            BuildingLevel level = building != null ? building.UpgradeTo(tier) : null;
            if (level == null || BuildingUnlocks.HighestTier(building) >= tier)
            {
                continue;
            }""",
     """            BuildingDefinition building = content.FindBuilding(parts[1]);
            BuildingLevel level = building != null ? building.UpgradeTo(tier) : null;
            // What stands is read from the scene, so only at the player's own home; elsewhere a built level was dropped when it finished.
            bool ownHome = Place.AtHome && !HavenVisit.IsVisiting;
            if (level == null || (ownHome && BuildingUnlocks.HighestTier(building) >= tier))
            {
                continue;
            }"""),
])

# 4. A charged blow (its own flag) shoves to the most; only an uncharged heavy keeps the body near.
edit("Combat/HitReaction.cs", [
    ("""        float most = heavy && !art && impact < 2f ? Mathf.Min(1f, feel.KnockbackMost) : feel.KnockbackMost;""",
     """        bool charged = (kind & HitTechnique.Charged) != 0;
        float most = heavy && !art && !charged ? Mathf.Min(1f, feel.KnockbackMost) : feel.KnockbackMost;"""),
])

# 5. Pack never moves gear or plans, even from the quickbar.
edit("Items/PackForRaid.cs", [
    ("""    public static bool IsKit(ItemStack stack, QuickItems quick) =>
        !stack.IsEmpty && stack.Item != null && (stack.Item.Category == ItemCategory.Consumable || (quick != null && quick.Contains(stack.Item)));""",
     """    public static bool IsKit(ItemStack stack, QuickItems quick) =>
        !stack.IsEmpty && stack.Item != null && !stack.Item.IsEquippable && !stack.Item.IsBlueprint
        && (stack.Item.Category == ItemCategory.Consumable || (quick != null && quick.Contains(stack.Item)));"""),
])

# 6 + 7. A lookout's press is its own (the thought is added first, before its Interactable); a place with more to think has
# "Look closer" of its own.
edit("Level/HavenPlaceVerbs.cs", [
    ("""        if (kind.lookout)
        {
            HavenLookout.AddTo(holder, place, verbs);
        }

        if (kind.thoughts.Length > 0)
        {
            holder.AddComponent<CuriousPlace>().Configure(place.name, kind.thoughts, Mathf.Clamp(place.radius * 0.5f, 7f, 12f), remembered: true);
        }""",
     """        // The thought first: a CuriousPlace answers the Interactable it finds as it wakes, and a lookout's press is the look out, not
        // "Look closer". A place with more to think than its first thought gets "Look closer" of its own.
        if (kind.thoughts.Length > 0)
        {
            if (!kind.lookout && kind.thoughts.Length > 1)
            {
                holder.AddComponent<Interactable>().Configure(InteractionAnimation.GatherLow, 0f, 4f, false, InteractionKind.Use);
            }

            holder.AddComponent<CuriousPlace>().Configure(place.name, kind.thoughts, Mathf.Clamp(place.radius * 0.5f, 7f, 12f), remembered: true);
        }

        if (kind.lookout)
        {
            HavenLookout.AddTo(holder, place, verbs);
        }"""),
])
edit("Level/HavenLookout.cs", [
    # 8. Only places not yet found are newly marked.
    ("""                if (journal.NoteSeen(other.id))
                {
                    newlySeen++;
                }""",
     """                if (!journal.HasDiscovered(other.id) && journal.NoteSeen(other.id))
                {
                    newlySeen++;
                }"""),
    # 6. The first look's line waits for a thought being spoken instead of being lost.
    ("""        if (unfound != null && DialogueBox.Current != null && !DialogueBox.IsSpeaking)
        {
            DialogueBox.Current.Bark(DialogueSpeaker.Player(),
                $"<i>{Sightline(unfound)} To the {HavenPlaceVerbs.Compass(place.at, unfound.at)}. I haven't been there yet.</i>", null);
        }
    }""",
     """        if (unfound != null)
        {
            StartCoroutine(SayWhenFree($"<i>{Sightline(unfound)} To the {HavenPlaceVerbs.Compass(place.at, unfound.at)}. I haven't been there yet.</i>"));
        }
    }

    // Said once the line being spoken (the place's own thought, often) is done: pressing during it lost the first look's hint for good.
    private System.Collections.IEnumerator SayWhenFree(string line)
    {
        float giveUp = Time.unscaledTime + 15f;
        while ((DialogueBox.Current == null || DialogueBox.IsSpeaking) && Time.unscaledTime < giveUp)
        {
            yield return null;
        }

        if (DialogueBox.Current != null && !DialogueBox.IsSpeaking)
        {
            DialogueBox.Current.Bark(DialogueSpeaker.Player(), line, null);
        }
    }"""),
])

# The homecoming card never shows over a raid or the menu.
edit("UI/HomecomingCard.cs", [
    ("""        bool busy = (UIStateManager.Current != null && UIStateManager.Current.IsAnyWindowOpen) || DialogueBox.IsSpeaking;""",
     """        bool busy = (UIStateManager.Current != null && UIStateManager.Current.IsAnyWindowOpen) || DialogueBox.IsSpeaking
            || Player.Current == null || RaidDirector.Current != null;"""),
])
print("all applied")
