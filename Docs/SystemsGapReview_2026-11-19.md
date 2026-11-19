# Systems gap review: what is thin, ranked by fun and retention per effort (2026-11-19)

A read-only lead-design review. The user asked: "research what systems and design may be lacking? improve those to the best of
your abilities." Another agent with Editor access builds from this file, so every item is written to be built with what the
project already has.

**Scope.** The core loop and its pacing, progression and economy, the base sim and residents, raids as a loop, crafting, gear
and loot, onboarding, mid- and long-term goals, failure and catch-up, co-op as a loop, accessibility and options, saves and
quality of life. **Out of scope:** combat feel, and exploring the Haven world. Two other reviewers cover those today. The
prologue changes in `Docs/PrologueAndQuestFlow_2026-11-18.md` are also out, as the user asked.

**Built on.** The GDD status table (`Assets/_Project/Scripts/ARCHITECTURE.md`), `GameDesignDocument.md`,
`SystemReview_2026-11-17.md`, `SystemReview2_2026-11-17.md`, `SystemDesignReview_2026-11-17.md`, `DesignReview_2026-11-13.md`,
`GameDesignResearch_2026-11-17.md`, `LootAndDropsResearch_2026-11-17.md`, `QuestRedesign.md`, `AgentDecisions_2026-11-17.md`
and `AgentDecisions_2026-11-18.md`, and the user's decisions in memory. Every "missing" claim below was checked by searching
the code (`Assets/_Project/Scripts`, `Data`, `Resources`).

**Evidence marks.** **Confirmed** means read in code or data, with the file given. **Measured** means taken from the playtest
CSVs, human runs only. **Genre** means web research, with sources in §6.

---

## 0. Verdict: where the design is thin

The game is wide and well connected:
- People found on raids open buildings.
- Buildings feed raids.
- Bosses are keys.
- Loss is softened: gear is kept, there is a safe pocket, and the Lost carry what you lost.
- Most of the 11-17 reviews' loopholes are closed.

What is thin is at four seams:

1. **The seam between two raids.** Here the player decides "what do I bring home, why do I stay, and what changed while I was
   away".
   - Items say what they are *used for* (`ItemUses`), but not what is *still missing* for the step you are working towards.
   - A homecoming tells you only what workers made.
   - Restocking for the next raid is done by hand.
   - Nothing on the map pulls you to stay past the rumour. Real raids end at 3.4 to 5.6 of 12 minutes.
2. **People are numbers more than people.**
   - 9 of 11 rescuable companions use the same `Fighter` definition, and the other 2 use `Archer`.
   - Worker ranks count crafts.
   - Residents have no tales, no morale and no signature.
   - The user approved fixes for all of this on 11-13 (8.8, 8.9, 8.11, 8.13 and B6), "after the playtest". They are still unbuilt.
3. **The long arc stops.**
   - The main story ends at the Brukam ("Nobody Crosses").
   - There is 1 Tale quest (silver) and 2 Errands (bronze) in all of `Data/Quests`.
   - Tier V gear exists, but no raid asks for it.
   - Moss Marsh tier IV is the last unlock. After about 15 to 20 raids the loop offers no new target.
4. **One rule set for every player.**
   - Valheim and Enshrouded ship world settings: death price, enemy strength, resources and raids.
   - Here there is a damage-taken choice and timing help, nothing for the raid clock, home attacks or hunger, and no assist that
     grows with failure.

**What real players did (Measured).** The human runs are 20261114-122242, 20261114-232848, 20261115-184337, and the prologue-only
runs of 11-16 and 11-17. The 11-15 run has 13 `Cheat` rows, all in the prologue.

| Run | Played | Raids (seconds, result) | What happened after the last raid |
|---|---|---|---|
| 11-14 12:22 | 67 min | 2 raids, the second a death at 252 s | the session ended at home |
| 11-14 23:28 | 63 min | 294 s and 320 s, both extracted | 14 min at home: 9 `Stored` rows, one Small Chest built, then quit |
| 11-15 18:43 | 49 min | 201 s and 337 s, both extracted | crafted a Hearth Ember and quit after 1 minute |
| 11-16, 11-17 | 9-10 min | prologue only | stopped at Hold the Portal |

- No human has played a third raid.
- Every raid was Misty Woods tier I. Each ended before the second danger stage (240 s left, 8 min in).
- The between-raid stretch is about 15 to 25 minutes, so it is where the game is won or lost.

---

## 1. The ranked list

Items are ranked by impact on fun and retention per effort. **QW** marks a quick win (S, low risk, no user decision needed).

| # | Gap | Effort | Kind | Decision? |
|---|---|---|---|---|
| 1 | **Wanted at home:** track what the next step still needs, in the bag, the tooltip, the pickup line and the briefing | S | Code | No (QW) |
| 2 | **A fuller homecoming:** what is ready, grown, made and now within reach, in one card | S | Code | No (QW) |
| 3 | **Pack as last time:** restock the raid kit from the stash in one press, and mend everything in one press | S-M | Code | No (QW) |
| 4 | **A reason to stay:** each danger stage reveals one richer site on the map | S-M | Code + data | No (QW) |
| 5 | **Companions who differ:** one signature each, and a second companion at portal tier III | M-L | Code + data | Approved 11-13 (B6, 8.11), "after the playtest" |
| 6 | **The Hearth:** a light morale view, fed by the comfort that rest can no longer use | S-M | Code + data | Approved 11-13 (8.13), "after the playtest" |
| 7 | **Failure that teaches, and a journey that fits:** Hearth's Grace, a raid-length assist and a death recap now; journey settings next | S, then M | Code | (a) no; (b) yes |
| 8 | **The raid's tale and the Haven Chronicle** | M | Code | No |
| 9 | **The Haven's Works:** bundles at the Notice Board that pay in access, with coin as one input | M-L | Code + data | Approved in principle (the board brings fetch and craft quests) |
| 10 | **People who grow by milestones:** worker ranks by deeds, with one master recipe each | M | Code + data | Approved 11-13 (8.9, B1), "after the playtest" |
| 11 | **Bond Tales:** each bond rank opens the companion's own short story | M (writing) | Data + small code | D4, unanswered |
| 12 | **Reforge at the altar:** empowering carries to the next tier at half price | M | Code | Q5, unanswered (agent leaned yes) |
| 13 | **Co-op as a loop:** show each friend's kit against the tier, and make the beacon and the cart better with two | S + M | Code | D5, unanswered |
| 14 | **Defence with choices:** guard posts and attacker roles ("Shaken", never dead) | M-L | Code + data | Approved 11-13 (8.8, B2, B3), "after the playtest" |
| 15 | **An endgame ladder:** Pact marks on tier IV, with one-time bounties, so tier V gear has a home | L | Code + data | Q7, unanswered |

**Build order.**
1. Batch 1: items 1-4, 7a and 13a. These are quick wins and touch no decision.
2. Batch 2: items 6, 8, 5 and 9. These are what the user approved, or need no decision.
3. Batch 3: items 10-15. These need the user's yes, or are large.

Write each choice into an `AgentDecisions_2026-11-19.md` as the 11-17 and 11-18 logs do. Items 5, 6, 10 and 14 were approved
"after the playtest". The user's message today ("improve those to the best of your abilities") reads as a go-ahead, but say
so in the log.

---

## 2. The items in detail

### 1. Wanted at home: track what the next step still needs (QW · S)

**What is lacking (Confirmed, Measured).**
- `ItemUses` (`Items/ItemUses.cs`) and `ItemTooltipView.AppendUses` (`UI/ItemTooltipView.cs:709`) show "Used for: A, B, C".
  They never show what is still missing for a goal.
- The second half of the loot research's suggestion 4 ("wanted items marked: asks and a pinned upgrade",
  `LootAndDropsResearch_2026-11-17.md` §10) was never built. No track or wishlist list exists in `SettlementState` or
  `CharacterProgress`.
- Seven goal-givers each keep their own list of needs (`SystemDesignReview` K14).
- In the 11-14 23:28 run the player spent 14 minutes after raid 2 storing and sorting, then quit, with no upgrade or craft
  started.

**The change.**
- **Track up to 3 targets.** A target is one of:
  - a building level ("Track" beside "Upgrade" in the Build menu and in H › Buildings);
  - a recipe (the recipe browser);
  - the portal's next tier (H › Overview and the portal window);
  - a Works bundle (item 9).
- **Standing asks count as tracked** while they stand (`HavenRequestBoard.Standing()`).
- **Default target.** When nothing is tracked, the portal's next tier is tracked, so a new player always has a goal on the spine.
- **"Wanted".** For each item, the sum over tracked targets of cost minus what is at home (bag + stash + a cart at home).
  Recompute when the bag or the stash changes. There are at most 3 targets plus about 3 asks, so this is cheap.
- **Where it shows:**
  - **Tooltip:** an accent line, "Wanted at home: 4 more for Smithy II (6/10)".
  - **Slots:** a small eye mark on wanted items (`ItemSlotView`). Quick Stack (V) keeps its rule: wanted items go to the chests
    like anything else, since the stash counts towards the target.
  - **On a raid:** on picking up a wanted item, "Copper Ore 7/10 for Smithy II" (`MessageKind.Gain`). One line per item per
    2 s, and none once the target is filled.
  - **Briefing:** "Wanted at home: Copper Ore ×4, Wolf Fang ×2", each marked "likely here" when it is in the raid's likely items.
  - **When a target is filled:** "Smithy II: everything is home. Build it (G)." This feeds item 2.

**Why (Genre).**
- ARC Raiders: you track a workshop level's materials, and an eye icon marks them in the inventory and in the field.
- Tarkov marks the items a hideout upgrade needs with the hideout icon.
- Monster Hunter's wishlist does the same.
- It turns "a raid" into "a raid for four copper", the cheapest "slow but not grindy" lever there is. It reads the goal-givers
  that exist and adds none.

**How to build.**
- `Progression/CharacterProgress.cs`: `List<TrackedTarget>`, holding kind (BuildingLevel, Recipe, PortalTier, Works), id and
  level. It is personal, so it follows the player into a friend's raid. Save it through `Save/SaveData.cs` and `SaveMapper.cs`.
- New `Settlement/WantedAtHome.cs`, plain C#: `Compute(targets, IHavenView, bag, stash)` →
  `Dictionary<ItemDefinition, (have, need, target)>`.
- Costs come from `BuildingLevel` cost, `RecipeDefinition`, `PortalTiers.Next(...).cost` and `HavenRequestDefinition`.
- UI hooks:
  - `ItemTooltipView.AppendUses`;
  - `ItemSlotView`;
  - `BuildMenuWindow` and `HavenBuildings` (a `MenuAction` "Track");
  - `RecipeBrowser`;
  - `HavenOverview` and `PortalWindow`;
  - `RaidPreparationWindow.Briefing.cs`.
- Pickup line: a listener on `GameEventKind.ItemObtained` with `ByPlayer` set.

**Risk.** Low. It respects "no hidden" and "only raids give". It adds no goal-giver.

**How to verify.**
- An edit-mode test through the real path: a `SettlementState` with a stash, Smithy II tracked, items added through
  `ItemDelivery.Give`, then `Compute` asserted.
- A guard test: every `BuildingDefinition` level and the portal tiers resolve as targets, and the test fails if a level's cost
  can't be read.
- In Play: track Smithy II, launch Misty Woods tier I, pick up copper, and see the line.

### 2. A fuller homecoming (QW · S)

**What is lacking (Confirmed).**
- `TODO.md:687`, "A fuller homecoming", is still unchecked.
- `HavenProgress.TellWhatWorkersMade` (`Settlement/HavenProgress.cs:166-183`) posts only "While you were gone your people
  made …".
- None of these is told at the moment of return:
  - finished orders waiting in an output slot (since 11-18, gear ordered at a staffed station waits there);
  - grown crops (crops are Farm Plot recipes);
  - asks that can now be answered;
  - building levels or the portal tier the stash now covers.
- The 11-13 review's validation question 1 ("does the player want to go home?") pointed at exactly this.

**The change.**
- **One "While you were away" card, about 2 s after arriving home from a raid.** It gives way to quest starts (Story Pacing), and
  E or Esc closes it. It never shows in a friend's Haven.
- **Sections, in order, at most 8 lines:**
  1. **Ready to collect:** station, item and quality.
  2. **Grown:** farm plots and the beehive.
  3. **Made by your people:** today's message, moved in.
  4. **Now within reach:** at most 3 building levels, portal tiers or tracked targets that the bag + stash now cover, each with
     its key ("Build menu (G)").
  5. **Asks you can answer.**
- **Each line has "Go there"** (`HavenTravel`).
- The same lines go into the Chronicle (item 8) once that exists.

**Why.**
- The raid pays off the moment you are home. RimWorld's letters and Tarkov's "production ready" do this job.
- The user's colony direction is "judge base features by what they do for raids". This shows it.

**How to build.**
- New `Settlement/Homecoming.cs`, plain: `Compose(IHavenView, stash, bag, stations)` → lines with targets. It reads:
  - `StationQueue` and `WorkshopState` output slots;
  - `BuildingUnlocks` with the level costs;
  - `PortalTiers.Missing`;
  - `HavenRequestBoard`.
- Call it where `HavenProgress` now calls `TellWhatWorkersMade`.
- Keep the old message as the fallback when the card can't show (never remove it). This follows the memory rule: never remove
  functionality.
- Draw the card from the Hearthline kit (`UI/Kit`), not as a HUD line. The user has asked for a less cluttered main HUD
  (commit 8e9e55ae).

**Risk.** Low.

**How to verify.**
- A test: `Compose` with a station holding output and a stash that covers Smithy II lists both.
- In Play: queue at the Smithy, raid, come home.

### 3. Pack as last time, and mend all (QW · S-M)

**What is lacking (Confirmed).**
- The planner (`UI/RaidPreparationWindow.Preparation.cs`) offers tier, Sparks, size, companion and runes. It has nothing for the
  bag.
- `StashStorage` (`Items/StashStorage.cs`) can store and quick-stack (`StashQuickStack`, V). Nothing takes out for a raid.
- `RepairBench` lists damaged pieces one by one. No "mend all" exists (no `RepairAll` or `MendAll` in the code).
- The boss planner itself says "Come ready: eat, bring tonics and mend your gear". All of that is done by hand.

**The change.**
- **At launch, record the set-out kit** (`RaidLauncher`, next to `SaveGameService.SaveBeforeRaid`, `RaidLauncher.cs:334`):
  - which items: Consumables, arrows, lantern refills (Firefly), Bandages and field kits;
  - per item: id, quality, count, and its quickbar or skill-bar slot.
- **"Pack as last time" at home.** It is in the chest menu, a planner footer button and the controller's action wheel.
  - It moves from the stash into the bag up to those counts, onto the same quickbar slots.
  - It never moves gear: gear is worn and kept.
  - It stops at the heavy-bag threshold (Player Movement Settings › Carrying › Heavy From Share, 80%).
- **It tells you the shortfall:** "Short: Healing Draught ×2 (Cauldron), Bodkin Arrow ×10 (Workshop)". Each line has a "Track"
  button (item 1), or "Queue" when the recipe is known and the station stands.
- **RepairBench gets one more row: "Mend everything worn (cost)."** It mends in the order worn, as far as the materials go.

**Why (Genre).** ARC Raiders' lead admits that putting a loadout together "is a pain when you want to just get in there". He
sometimes runs a free loadout rather than build one, and he says he is aware of the demand for custom loadouts "that save us
100 clicks". The measured home stretch is 15 to 25 minutes around raids of 3 to 6. Cut its chores, keep its choices.

**How to build.**
- `CharacterProgress.SetOutKit`, saved.
- New `Items/PackForRaid.cs`, plain C#: `Plan(kit, stash, bag, weightLimit)` → moves + shortfall. `Apply` goes through
  `ItemContainer.TransferTo`, so no `ItemObtained` is raised (quests must not count it).
- Its rules: `StashQuickStack.CanStack` (inside one's own ward, `Place.StashUsable`).
- Menu rows in the stash chest's `MenuAction`s, `RaidPreparationWindow`, `CommandWheel` and `RepairBench`.

**Risk.** Low.

**How to verify.**
- Tests for `Plan`: partial stock, the weight cap, quality kept, quickbar slot restored, and no gear moved.
- In Play: launch with 3 draughts, come home, and press it.

### 4. A reason to stay: each danger stage reveals a richer site (QW · S-M)

**What is lacking (Confirmed, Measured).**
- Misty Woods' stages start at 480 / 240 / 60 s left. They add +10/20/35% enemy health and power and +25/50/80% loot
  (`Data/Raids/MistyWoods.asset:689-720`).
- Human raids ended at 201-337 s in. Three of five passed the first stage (240 s in), and none reached the second (480 s in).
- The bonus is only a message ("loot +25%") and `RaidDirector.StageLoot`. Nothing on the map shows what staying would earn.
- The GDD's emotional journey wants "one last temptation" near the deadline. `SystemDesignReview` K8 and `GameDesignResearch`
  T2 both name the gap. T2 is not built: no `StageReveal` exists.

**The change.**
- **New fields on `RaidStage`:** `reveal` (None / Glint / Hoard) and `revealLoot` (a multiplier).
- **At a stage with a reveal:**
  - **Pick the site:** an unfound site 70-140 m from the player and at least 50 m from any lit or open exit. It can be a
    `LootCache` that is unopened, or an unvisited event site. If there is none, place a "Glint" cache through
    `RaidSpots.TryFindSpot`, which already includes `IsDryEnough`.
  - **Mark it:** gold on the minimap rim and the world map, in the rumour style with its own label, "A glint".
  - **Say it:** "Something glints beyond the <landmark or bearing>. The woods give more to those who stay."
  - **Its loot:** the cache rolls with the *next* stage's bonus. The Mist Stirs' Glint pays +50%. The Hunt's Hoard pays +80% and
    carries one elite from the raid's Hunters list as its guard.
  - **It ends at Collapse:** the mark greys out and the cache stays.
- **Data:**
  - Misty Woods, Troll Valley and Moss Marsh: stage 1 Glint (`revealLoot` 1.5), stage 2 Hoard (1.8).
  - Encounters have no stages and so no reveal.
- **Log** a `RaidEvent` "StageReveal". The `Position` rows (built 11-17) then show whether players go.

**Why (Genre, principle).**
- ARC Raiders marks high-loot areas on its maps, and they draw traffic. Embark tightened the Raider Hatch when 40% of playtest
  extractions used it, against "the heart-pounding tension of a desperate sprint" (sources in `GameDesignResearch`).
- A visible, timed offer away from the way home is the cheapest way to make the clock bind (K8). Today Reach, Sparks and the
  stage loot are paid-for benefits nobody reaches.

**How to build.**
- `Raids/RaidStage.cs` gets the fields.
- New `Raids/StageReveal.cs` is started from `RaidDirector`'s `StageChanged`. It picks a site with `RaidSpots`, uses
  `LootCache`'s multiplier, and reads `RaidEventSite` and `IsFoundFrom`.
- `MinimapHud` and `WorldMapHud` get a mark kind.
- Co-op: each game reveals its own, as rumours are placed in every game. A Hoard's guard spawns through
  `CreatureSource.Remote` on a friend's game (the S1 lesson in `SystemReview2`).

**Risk.** Low to medium: raids get longer. That is what the clock is for.

**How to verify.**
- A test: the picking rule on a generated layout (a golden seed) respects the band and the exits, and never places a site in water.
- A Play probe: force the clock to 481 s, see the mark, open the cache, and check that the multiplier is in the roll.

### 5. Companions who differ: one signature each, and a second companion at portal tier III (M-L)

**What is lacking (Confirmed).**
- Every rescuable companion's `raidCompanion` points at one of two shared definitions (`Data/Companions/Fighter.asset`,
  `Archer.asset`). Asa and Runa are Archers. Gunnar, Hild, Ingrid, Yrsa, Bjarne, Eirik, Liv, Sigrid and Tor are Fighters.
- `ResidentDefinition` has lines, jobs and a model, but no ability (`Settlement/ResidentDefinition.cs`).
- The choice in the planner is archer or fighter, plus bond: Trusted at 3 raids (+15% health, +10% damage) and Sworn at 8
  (+30% / +20%) (`Resources/SettlementSettings.asset:68-76`).
- `RaidSession.Companion` is singular.
- The user approved both B6 ("each one ability from code that exists") and 8.11 ("one companion, two from portal tier 3") on
  11-13, to be built after the playtest.

**The change.**
- **A `CompanionSignature` per resident.** These are first numbers, to be tuned:

| Resident (job) | Signature | Rule |
|---|---|---|
| Ingrid (Alchemist) | Mend | At ≤50% health within 8 m: 25% of max health over 4 s; 75 s cooldown (`PrologueCompanion`'s support heal already does this: lowHealth 0.5, healShare 0.35) |
| Bjarne (Smith) | Strong Back | +20 carry weight while he is alive and within 20 m |
| Tor (Carpenter) | Shore Up | The beacon fills 20% faster while he stands in its circle |
| Eirik (Warden) | Hold the Line | When 2 or more hunt the player, he draws the nearest one for 6 s; 40 s cooldown |
| Gunnar (Hunter) | Spoor | Every 90 s, marks the nearest unfound cache or game within 45 m on the minimap for 30 s |
| Runa (Tracker) | Night Eyes | Rumour beacons show from 1.5 times as far |
| Asa (Farmer) | Herb-Wise | Plant nodes she passes within 6 m give one more, at most 10 a raid |
| Yrsa (Cook) | Shared Pot | Meal boons last 25% longer on the raid (duration only, not strength, so the reach bound holds) |
| Liv (Rune Scholar) | Stone-Reader | +20% Insight from the raid |
| Hild (Mason) | Breaker | Breakables, doors and compound walls break twice as fast; stone nodes give one more |
| Sigrid (Leader) | Rally | Once a raid, when the player drops to ≤20% health, the party takes 15% less damage for 8 s |

- **Shown** in the planner's companion row and on the person's page.
- **A second companion from portal tier Restored** (ward II, decision 8.11), solo only. A party of friends already has its
  people, and creatures gain +60% health per friend.
- **Bond, bleed-out and forgetting** stay as they are, per companion.

**Why.**
- GDD §15.5: "a companion's utility must justify the genuine risk of losing them".
- Deep Rock Galactic's classes "don't function as well alone as together" (`GameDesignResearch` §6).
- Every signature reuses an existing hook:
  - support heal (`PrologueCompanion`);
  - carry (`BackpackCapacity`);
  - hold rate (`ExtractionHold`);
  - targeting (`MonsterTargeting`);
  - minimap marks;
  - beacon reach (`RaidEventSite`);
  - node yield;
  - boon durations (`PlayerBoons`);
  - Insight (`ExperienceTracker`);
  - breakables.

**How to build.**
- New `Companions/CompanionSignature.cs` holds the data (kind and numbers) and is referenced from `ResidentDefinition`.
- A `CompanionSignatureRunner` on the `Companion` runs it.
- `RaidPlan`, `RaidSession` and `RaidCompanions` move to a list. Keep `Companion` as "the first", so callers stay valid.
  `RaidReport.Companion` follows the same pattern.
- The planner gets a second row when `PortalTiers` ≥ Restored and the player is solo.

**Risk.** Medium. It is approved but was scheduled after the playtest. Eirik's draw touches enemy targeting: keep it to one foe and
a cooldown, and let the combat reviewer see it.

**How to verify.**
- Plain-C# tests per signature trigger (for example, Mend fires at 0.5 and not at 0.6, then waits out its cooldown).
- `RaidCompanions.Settle` with two companions: one lost, one home.
- A Play probe per signature.

### 6. The Hearth: a light morale view that surplus comfort feeds (S-M)

**What is lacking (Confirmed).**
- Rest is comfort × 60 s (`HavenReadiness.RestedSeconds`, `Resources/HavenSettings.asset:39`). It is given in full at every
  launch (`GiveRest`).
- Comfort reaches about 40, so 40 minutes. The longest raid is 24.5 minutes and real ones last about 5. Past about 13 to 25
  comfort, homes, beds and decorations add nothing (K6).
- The 11-17 agent didn't build "rested charges" (Q6) because rest is already given at every launch.
- No mood or well-being exists anywhere in `Settlement/`.
- The user approved 8.13 on 11-13: "light morale view, no decay".

**The change.**
- **Hearth, 0 to 100, in H › Overview, with its parts listed the way renown is.**
- **Points:**
  - 2 × the comfort beyond what the longest offered raid can use: max(0, comfort − ⌈longest raid s / 60⌉);
  - 2 per resident housed in a home;
  - 4 per festival joined this year (at most 16);
  - 3 per breach won (at most 15);
  - minus 30 × blight (0-1).
- **Hearth % = points ÷ 60**, capped at 100. Being away never lowers it. Blight, which already exists, is the only minus.
- **Bands.** These are Manor Lords' brackets with the negative band left out:

| Hearth | Name | Effect (never a raid combat stat, so the reach bound holds) |
|---|---|---|
| 25% | Kindled | Warmer asides (`ResidentAsides`); repairs cost 10% less |
| 50% | Warm | Workers' extra-output chance +3%, on top of the rank's 3-12%: yield, not speed (K7) |
| 75% | Glowing | A homecoming gift: one meal of the Kitchen's tier in the stash; one more settler at each Haven rank |

**Why (Genre).**
- Valheim's comfort, and Enshrouded's.
- Manor Lords' approval bands: 75-100 high growth, 50-74 low, 25-49 none, under 25 negative.
- The user wants no decay, so only the upper bands are kept.
- It gives every comfort point a home again, without a stronger raid buff.

**How to build.**
- New `Settlement/Hearth.cs`, plain rules: `Points(IHavenView, comfort, longestRaidSeconds)` → value + parts. Weights and bands
  go in `HavenSettings`.
- Readers:
  - `HavenOverview` and `HavenWindow`;
  - `WorkerProduction` (extra chance);
  - `Repairs` cost;
  - `HavenProgress` (the gift);
  - `ResidentAsides` (a "hearth" line set);
  - settlers by rank (`HavenSettings` ranks).
- Co-op: the host's Hearth in the host's Haven, as renown is.

**Risk.** Low to medium. It is approved "after the playtest", and its numbers are first guesses.

**How to verify.**
- Rules tests on `Points` and the bands.
- `RaidTierTests` stay green: no kit change.
- In Play, H shows the value and its parts.

### 7. Failure that teaches, and a journey that fits (a: QW · S; b: M, decision)

**What is lacking (Confirmed).**
- `GameOptions.DamageTaken` offers Normal, 75% and 50% (`Core/Options/GameOptions.cs:168`). There is no choice that grows with
  falls: `GameDesignResearch` A1 is unbuilt.
- There is no raid-length assist (A4 unbuilt).
- The death screen gives the killer and its advice (`RaidReport.KillerAdvice`). The last blows are not kept (G3's other half; no
  `RecentBlows` class).
- There are no world or journey settings. Death price, the raid clock, home attacks and hunger are one fixed rule set.

**The change, (a), quick win.**
- **Damage Taken › "Grows with falls".** It starts at 90%. Each fall takes 2% off (a raid death, a boss death or a prologue
  fall), down to 50%. Falls are counted in `CharacterProgress`. It is logged as an `Assist` like the others
  (`PlaytestLogger.cs:155`). This is Hades' God Mode (20% less damage, +2% per death, up to 80%) at a gentler scale.
- **Accessibility › Raid Length:** Off / +25% / +50% on the raid clock (`RaidParameters.Calculate`).
  - The stages count time *left*, so they keep their meaning.
  - It is logged as an `Assist`.
  - In co-op the host's setting rules the shared clock.
- **Death recap.** Keep a ring of the last 3 blows of 5% health or more. Record attacker, attack name, damage, and whether the
  blow was heavy, unblockable, from behind or off-screen (the viewport test built 11-17). Show them under "Brought down by …".

**The change, (b), decision.**
- **A Journey step on the character creation screen**, also in Options › Journey. Changes are logged.
- **Presets:** Gentle, As Made (the default: the user's numbers), Harsh, plus Custom.

| Setting | Gentle | As Made | Harsh |
|---|---|---|---|
| Gear wear on a fall | 10% | 25% (bosses 10%) | 40% |
| What you carried | half kept | lost | lost |
| Home attacks (corruption nights) | fewer (chance ×0.5) | as made | as made |
| Hunger and thirst | ×0.5 drain | as made | ×1.25 |
| Raid clock | +25% | as made | as made |

- Breaches that are part of the story always come.
- A boss's wear scales with the chosen wear (10% × chosen/25).
- Quitting still counts as a fall (K1, as built 11-17). The boss "all in" rule is unchanged.

**Why (Genre).**
- Valheim's world modifiers cover combat (very easy to very hard), death penalty (casual to hardcore), resources, raids (none to
  much more) and portals, with presets from Casual to Hardcore and Immersive.
- Enshrouded's custom settings (30+, from its "Back to the Shroud" update) cover enemy damage and health, what drops on death and
  production speed.
- The Game Accessibility Guidelines: a choice of game speed "very effectively" eases timing problems.

**How to build.**
- (a):
  - `GameOptions` choices;
  - `CharacterProgress.Falls`;
  - where `DamageTaken` is applied in the damage path;
  - `RaidParameters.Calculate`;
  - new `Combat/RecentBlows.cs`, fed by `Damageable.AnyDamaged`, the same feed as the `Hurt` rows;
  - `RaidReport` and `UI/RaidReportText.cs`.
- (b):
  - new `Core/JourneySettings.cs`, saved per save slot in `SaveData`;
  - read by `PlayerSnapshot.AfterFailedRaid` (its `durabilityLoss` and a "keep half" pass), `CorruptionNights` (chance),
    `PlayerNeeds` (drain) and `RaidParameters`;
  - `CharacterCreationScreen` gets a Journey page.

**Risk.**
- (a): none to the rules, because it is opt-in and logged.
- (b): the user said "rules should stay consistent". The defaults stay as they are and every change is opt-in and logged, so
  playtest data stay readable. Ask before building (b).

**How to verify.**
- A falls test: after 2 falls the factor is 0.86.
- A `RaidParameters` test with the assist on.
- `AfterFailedRaid` with "half kept" keeps ⌊n/2⌋ of each stack.
- An `Assist` row is written.
- In Play, die twice and read the recap.

### 8. The raid's tale and the Haven Chronicle (M)

**What is lacking (Confirmed).**
- `RaidReport` holds the facts: `Events`, `Outcome`, `TimeSpent`, `BroughtDownBy`, `Rescued`, `CompanionLost`, `FoundAgain`,
  `LeftBehind` and `Modifiers` (`Raids/RaidReport.cs:95-128`). They are gone once the summary is closed.
- No Chronicle or `RaidTale` exists (searched).
- `GameDesignResearch` W2 is unbuilt.
- GDD Pillar B: "a story the player can tell".

**The change.**
- **`RaidTale.Compose(RaidReport)`** writes one sentence from the biggest facts. Ranked:
  1. a boss felled or fled;
  2. a Lost One met, wounded or laid to rest;
  3. a rescue;
  4. a companion lost or found;
  5. the beacon held while contested;
  6. a close call (under 60 s left);
  7. the killer;
  8. the hour and weather.
- Example: *"At dusk in the rain you brought Bjarne out of the Misty Woods, and held the west beacon with a troll inside it;
  forty breaths were left."*
- **Shown** at the top of the raid summary.
- **Kept** in a new Journal tab, **Chronicle**, newest first, 300 entries. At its head sit **Deeds**: lifetime counts of raids,
  extractions, people brought home, bosses, the Lost laid to rest and breaches won.
- **Retold:** the best tale of the last raid becomes one resident aside at homecoming ("They say you held the west beacon with
  the troll inside it").
- **Co-op:** each game writes its own tale, naming friends from `RaidNews`.

**Why (Genre).**
- RimWorld's tales and Dwarf Fortress's legends, at the scale of one sentence.
- Knowledge and story are the retention this game wants ("slow but not grindy").
- The Deeds can map to Steam achievements later. Steam is already integrated for co-op.

**How to build.**
- New `Raids/RaidTale.cs`, pure.
- Extra facts on `RaidReport`:
  - whether the hold was contested (from `ExtractionHold`);
  - seconds left at extraction (also wanted by `SystemDesignReview` R8);
  - the Lost One met (`Raids/Lost/RaidLostOnes.cs`).
- A saved list of `{day, raid, text}` in `SaveData`.
- `Journal/JournalContent.cs` gets the tab. `Settlement/ResidentAsides.cs` gets the news.

**Risk.** Low.

**How to verify.**
- `Compose` tests on reports made by `RaidReport.Create`, the real path: the order of facts, and an empty raid giving a plain
  line.
- In Play: extract, then reload, and the tab keeps the tale.

### 9. The Haven's Works: bundles at the Notice Board that pay in access (M-L)

**What is lacking (Confirmed).**
- Asks (21 in `Data/Settlement/Requests`) each want one item × count and pay coin, XP, blight eased or renown
  (`Settlement/HavenRequestDefinition.cs:22-43`).
- The Notice Board is a building (`Data/Buildings/NoticeBoard.asset`, still a primitive stand-in). The user said it "unlocks
  fetch or crafting quests".
- Fishing, farming and horses feed few shared goals.
- Pennies have about 30 sources and one real sink (K13). The Trading Post stock is unchanged (`Resources/TradeSettings.asset`).
- `SystemDesignReview` R13 and R14 are unbuilt.

**The change.**
- **Six Works on the board once it stands.** Each has 3-4 slots that span systems. Each pays access, never a stat. Slots can be
  filled bit by bit and are tracked (item 1).
- The slots below are real items. They are first picks for the user's eye:

| Work | Slots | Opens |
|---|---|---|
| The Smoke-House | 4 Smoked Char (fishing + smoke rack), 6 Barley (farm), 4 Wolf Fang (raid), 30 coin | The caravan's second stock line in `TradeSettings` (a coin sink) |
| The Horse Fair | 10 Barley, 2 Leather Saddle, 1 Painted Barding, 40 coin | `HorseTrader` sells a second breed |
| The Barrow Peace | 3 Barrow Goods, 2 Grave Iron (raid junk), 2 Honey Mead, 1 Runestone Fragment | A barrow relic rumour in Misty Woods tier II+ (`RaidRumourDefinition.needsBuilding` = a Barrow Stone decoration the Work builds) |
| The Weavers' Loft | 8 Linen, 4 Huldra Silk (raid), 6 Cured Leather, 2 Widow Silk (raid) | A new Tailor's Shop line past tier II (the loot research found the Tailor thin) |
| The Long Table | 2 Haven Feast, 3 Fish Soup, 4 Honey Cake, 1 Boar Pie | A yearly feast festival (`Festivals`), and +1 Hearth band step for that season (item 6) |
| The Old Road | 20 Granite Block, 8 Hardwood Plank, 2 Stone Troll Knuckle (trophy), 25 coin | A Wainwright's Yard line (the research found the Yard thin), and a carved waystone decoration |

- **No more than 2 Works stand at once.** A new one goes up at a homecoming. This keeps K14 in check.
- **The Works use the board's ask slot** rather than adding a seventh goal-giver.

**Why (Genre).**
- Stardew Valley's Community Center: 30 bundles across 6 rooms, each room opening access (the minecarts, the bridge, the
  greenhouse). One visible list that needs every system.
- Value-scaled sinks keep coin pulling (Lostgarden's value chains, `SystemDesignReview` §7).

**How to build.**
- `HavenRequestDefinition` gets a Works kind:
  - `List<ItemAmount> wanted` plus coins wanted;
  - partial hand-in saved per slot in `SettlementState`;
  - `opens`: a `GameEventDefinition`, `BuildingDefinition` or `TradeSettings` tier.
- `HavenRequestBoard` gives the Works slots their place.
- `HavenRequestSettings` sets their order. They go in a new `Data/Settlement/Works` folder.
- Rewards go through `BuildingUnlocks`, `TradeSettings` tiers and the rumour `needsBuilding` gate. These are all existing gates.
- `QuestAuditTests`-style reachability: every slot has a source before the Work goes up.

**Risk.** Medium, mostly content load.
- The user said the Trading Post "stays narrow". Works add stock *lines* behind effort; they don't make it buy more.
- Horses and fishing are at home, so check "what only raids give". Every raid-only slot is still fetched on a raid.

**How to verify.**
- Tests: partial hand-in survives a save; an opened line appears in `TradingPost`'s menu.
- `ContentAudit`: every Works item has a source.
- In Play: build the board and fill the Smoke-House.

### 10. People who grow by milestones (M)

**What is lacking (Confirmed).**
- Worker ranks go by crafts made: 0/10/30/70/150, at ×1.0-1.4 speed and 0-12% extra output
  (`Settlement/SettlementSettings.cs:69-72`).
- The 11-13 review: "NPC-ene blir bedre passivt" ("the NPCs get better passively"). The user approved 8.9 ("NPC rank by
  milestones, crafting speed within a rank"), after the playtest.
- Speed bonuses barely bind (K7), so ranks are felt little today.

**The change.**
- **Ranks become milestones:**
  - Novice;
  - Apprentice: their building at tier II;
  - Craftsman: their personal Deed, a short Haven quest made of deed objectives. Example: Bjarne, "a Fine copper blade made by
    you, and 3 Grave Iron";
  - Master: one named raid find, the job's `masterFind`. Examples: Bjarne an Offered Blade, Ingrid a Wisp Ember, Tor Elderwood,
    Yrsa Nykk's Mane, Gunnar Antler, Hild a Jutul Heartstone.
- **Master opens one unique recipe per person** (`JobDefinition.masterRecipe`), never a stat.
- **Within a rank**, crafts still raise speed from the rank's floor to its ceiling over 20 crafts (8.9's "speed within a rank").
- **Save migration:** a resident already at a rank by count keeps it. Nobody goes down.
- **H › People shows the next milestone** ("Next: Smithy II").

**Why.**
- The spec's §7.3 and the user's decision.
- It also fixes the felt-bonus problem, since a recipe is felt where speed isn't.

**How to build.**
- `SettlementSettings.WorkerRank` gets requirement fields.
- `JobDefinition` gets `deedQuest`, `masterFind` and `masterRecipe`.
- `ResidentRecord` keeps the rank earned.
- `WorkerProduction` and `HavenPeople` read it.
- The Deed quests are deed objectives, following the memory rule: never a Quest Event per person.

**Risk.** Medium. It is approved after the playtest, and it is content for 11 people. Start with Ingrid, Bjarne and Tor.

**How to verify.**
- Rules tests.
- A save-migration test.
- `QuestAuditTests` reachability for each Deed.

### 11. Bond Tales: each bond rank opens the companion's own story (M, writing · D4)

**What is lacking (Confirmed).**
- Bond ranks give stats and a memory line. `CompanionMemory.RoseInRank` exists (`Settlement/CompanionBond.cs:25,87`), but no
  quest follows it.
- There is one Tale quest (rank SideQuest: `EchoesInTheWilds`) and two Errands in `Data/Quests`.
- Pillar D promises "small personal stories … and wishes".
- `GameDesignResearch` W1 is unbuilt. D4 is unanswered.

**The change.**
- **`ResidentDefinition.bondTales`:** a tale part at Trusted (3 raids together) and another at Sworn (8).
- **Each part is 2-3 steps:** a scene at home, and one ask on a raid, a deed objective whose target is placed through a rumour or
  set piece.
- **The pay is a recipe, a decoration or a spoken line**, never a stat.
- **Held for a homecoming** by Story Pacing, so they don't flood the tracker.
- **Start with Ingrid, Bjarne and Eirik**, the likeliest companions.
- **Written in the user's decided tone:** dark; the jarl of Granheim and the man in grey left unnamed; Runa paid by the man in
  grey.
- **Drafts go to the user first.**

**Why (Genre).** Stardew's heart events, and the user's rule that trust comes through quests and the bond.

**How to build.**
- The field on `ResidentDefinition`.
- On `RoseInRank`, `QuestManager` starts the tale.
- Quests go in `Data/Quests` (rank SideQuest), covered by `QuestAuditTests`. Spoken lines come later through `VoiceBank`.

**Risk.** The story is the user's. Write, don't ship, until they say yes.

**How to verify.**
- A test: every resident with tales starts part 1 when the rank is reached.
- Reachability.
- In Play: raise `RaidsAlong` with the cheat menu and see the tale start.

### 12. Reforge at the altar (M · Q5)

**What is lacking (Confirmed).**
- An empowering step costs `purified + (tier − 1) × 2` (`Essence/GearPower.cs:145`).
- A +3 kit costs about 420-600 Purified, and it resets at every tier step (K9).
- 11-17 added a 25% refund on salvage. Moving the empowering to a new piece is not built (Q5).

**The change.**
- **Altar › Reforge** moves a piece's empowering levels to a piece of the same slot kind and a higher tier. It costs 50% of what
  those levels cost at the new tier, rounded up.
- The old piece stays, unempowered.
- An enchantment moves with it only if the new piece has room in its budget (the once rule), at 50% of its essence cost.
  Otherwise it stays.
- The salvage refund stays 25%.

**Why (Genre).** Last Epoch's Forging Potential and Diablo IV's tempering keep investment meaningful. Lostgarden: sinks need a
pull on the earlier nodes, otherwise players never invest.

**How to build.** A new work kind in `Essence/AltarWork.cs`, the cost in `GearPower`, a page in `UI/AltarWindow.cs`. The
`ItemStack` empowering and enchantment fields exist (`ItemStack.SameKind` already compares them).

**Risk.** It needs the user's yes on Q5. The per-item budget is unchanged.

**How to verify.**
- Tests through `AltarWork` with real stacks: cost, the levels moved, and the budget respected.
- `RaidTierTests` stay green.

### 13. Co-op as a loop: show the gap, reward the pair (a: QW · S; b: M · D5)

**What is lacking (Confirmed).**
- `RaidInvite` (`Multiplayer/RaidInvite.cs`) carries no guest kit. A tier I guest can follow into tier III unwarned
  (`GameDesignResearch` C1).
- The beacon fills only for this game's player (`Raids/ExtractionPoint.cs:211`: `inside` is the local player).
- Beyond revive, only "getting out beside a friend pays experience" (`Multiplayer/RaidsTogether.cs`) rewards playing together.

**The change.**
- **(a)** The invite and the planner list each player's gear score (`GearScore.Of(player)`) against the tier's floor (15/30/45):
  "Runa's kit: tier I · this raid: tier III · hard". Nothing is scaled. This is the user's "no hidden under-gear penalty":
  show the gap instead.
- **(b)** Each extra player standing in the circle adds +35% to the beacon's fill (remote `NetworkPlayerAvatar`s counted by
  `Inside`). Contested still pauses it.
- **(b)** A second player at a cart's handle halves its speed penalty.
- **(b)** The Chronicle (item 8) names the party.
- Solo numbers are unchanged.

**Why (Genre).**
- Deep Rock Galactic is "co-op first", with one drop pod everyone runs to.
- Valheim does no level scaling: friends bring their gear.

**How to build.**
- `RaidInvite` gets a new field and a wire-format bump, per the `CoopMessageTests` rule (messages are structs since format 28).
- `RaidPreparationWindow` and `PartyHud` show it.
- `ExtractionHold.Tick` takes a rate. `Transport/Haulable.cs` gets the second handle.

**Risk.**
- (a): none.
- (b): D5. A two-machine test is still owed.

**How to verify.**
- A `CoopMessageTests` round trip.
- An `ExtractionHold` rate test.
- `CoopTwoCopies.ps1`.

### 14. Defence with choices: guard posts and attacker roles (M-L · approved after the playtest)

**What is lacking (Confirmed).**
- `BreachGroup` (`Settlement/WardBreachDefinition.cs`) has no role.
- Guards (`ResidentDefender`) fight where they stand.
- Since 11-13 the Haven has gained the Ward Totem, the Watch Post archer, and breaches that teach defences (8.7).
- Still missing:
  - the user's 8.8 ("Seekers make residents Shaken, no death");
  - B3's guard posts.
- The 11-13 review rated defence the least validated and least rewarded link.

**The change.**
- **Guard posts.** From a breach's warning until it starts, H › People offers "Post at …" for each guard: the Watch Post, a
  gate, the portal, or a building. They hold there (`CompanionOrder` Stay at that point) and fall back at 30% health (Retreat).
- **Attacker roles** (`BreachRole`):

| Role | Does |
|---|---|
| Runner | Goes for the ward anchor |
| Breaker | Goes for walls and buildings first, at ×1.5 structure damage |
| Seeker | Goes for the nearest resident. Reaching one makes them **Shaken** for the night: they work at 50% and say a line. Never dead |
| Hexer | Stays 15 m out and weakens the ward and totems |

- **The portal window says who comes and from where:** "Rootlings and bog spitters from the north and east"
  (`BreachLanes.Describe`).

**Why.** The spec's §8.5-8.6, the user's decision, and "nothing the player built is ever lost". This puts people at stake
without death.

**How to build.**
- `BreachGroup.role`.
- `HomeAssault` target priority by role, through `AI/MonsterTargeting.cs`.
- A residents' status "Shaken" (`StatusEffectDefinition`), read by `ResidentLife` and `WorkerProduction`.
- `ResidentDefender` gets a post, and the H window shows it.
- Co-op: the host decides (`HavenDecisions`).

**Risk.** Medium: targeting code, and the combat reviewer may want a look. It was scheduled after the playtest.

**How to verify.**
- Rules tests on role targeting.
- A breach probe in Play with each role.

### 15. An endgame ladder: Pact marks on tier IV (L · Q7)

**What is lacking (Confirmed).**
- Tier V gear (Trollstone) exists, but the highest raid tier is IV.
- The Moss Marsh (tiers III-IV) is the last thing opened (`Data/Raids/MossMarsh.asset`, `unlockedBy` the Brukam).
- The main story ends at "Nobody Crosses".
- The Frost Peaks (tiers IV-V) are decided but not built.
- K16: "no endgame loop".

**The change.**
- **At tier IV the planner offers Marks 1-5.** Each Mark adds one forced drawback, drawn from the raid runes (Blood Moon, Deep
  Woods, Hush) and three new ones:
  - **The Hungering:** a Lost One walks this raid, one rank up.
  - **Brittle Light:** the beacon's hold takes 50% longer.
  - **Unquiet Dead:** twice the elites.
- **Each Mark adds** +12% enemy health and power and +15% loot (`RaidDifficulty`).
- **Each Mark pays a one-time bounty per raid and level:**
  - Mark 1: an inscription of a great enchantment;
  - Mark 2: Trollstone ×3;
  - Mark 3: a relic, chosen from 3;
  - Mark 4: a banner or barding;
  - Mark 5: a trophy and a Chronicle Deed.
- **Boss rematches take Marks too.**
- **Tier V kits are tuned against Marks 3-5** by raising multipliers, never by weakening the player (the gear-tier rule).

**Why (Genre).** Hades' Pact of Punishment pays one-time bounties per Heat. Diablo IV's Pit is an opt-in difficulty ladder. Both
are in `SystemDesignReview` §7.

**How to build.**
- `Raids/RaidModifierDefinition.cs` gets a "forced" flag.
- `RaidPlan` and `RaidParameters` carry the Mark, and `Raids/RaidDifficulty.cs` applies it.
- `RaidReport` records it, and `SettlementState` keeps the bounties claimed.
- `RaidPreparationWindow` offers it.
- A `RaidTierTests`-style band test for the tier V kit against Marks.

**Risk.** Q7 (the agent leaned "after the slice playtest"). It is the long-term answer until the Peaks exist, so design it now.

**How to verify.**
- The band test.
- A bounty pays once.
- The planner shows the Marks only at tier IV.

---

## 3. Next after these (short)

- **The Old Ways, taught through Tales** (`GameDesignResearch` F1, D3).
  - Three silver Tales, each ending in a law learned once and true everywhere:
    - "Steel in the Water": the Nøkk and a Steel Needle in fishing;
    - "Fire Ends the Dead": the Draug's `RisesAgain` (already built);
    - a Tale for "Steel Binds the Hidden Folk": a Tusse can't steal from a bag that carries iron.
  - Add `FolkKind` on `MonsterDefinition` and an audit test.
  - Mostly data. It gives identity and mastery without grind.
- **One-line hints in the portal's voice for the first three raids** (O2): at about 8 raid moments, the first time each happens.
  Lines through `VoiceBank`.
- **Speed into yield** (R12) once the logs show workers finish before homecoming. Item 6 already starts this.
- **Story past the Brukam.** Act II ending with Sigrid's rescue in Troll Valley, and Sigrun in the Peaks. Both are the user's
  decided arcs. This is writing for the user, not systems.
- **Meal and tonic slots against the reach bound** (K3): waiting for the user's a, b or c. The agent's lean is b.

## 4. Already done, so don't suggest it again

Checked in code on 11-19:
- **Raid flow:**
  - quitting mid-raid counts as a fall;
  - the briefing is pinned to the turn;
  - no raid-only items in tithes;
  - horses make noise;
  - the Field Repair Kit wording;
  - a 25% essence refund on salvage;
  - the weather foretold in the briefing;
  - stage stingers, the hunt's hush and the beacon's heartbeat;
  - colour-free telegraph rims and High-Contrast Warnings;
  - the killer's advice on the death screen;
  - the on-screen strike grace by viewport;
  - Position heatmaps.
- **Home:**
  - the Ward Totem and Watch Post archer;
  - breaches teach defences;
  - the portal-tier spine;
  - corruption by tier and blight only;
  - Quick Stack (V) and the bag's Sort;
  - crafting pays from the stash and carts;
  - the Haven window's next steps;
  - festivals;
  - seasons and winter.
- **Loot and progression:**
  - Run Comparison milestones (`Editor/Playtest/PlaytestRunsWindow.cs`), so `GameDesignResearch` O3 is mostly there;
  - the Lost carry and return what you lost;
  - bosses grant abilities (`BossDefinition.rewardSkill`);
  - `ItemUses` on tooltips;
  - loot tables, salvage, themed caches and rarity.

## 5. Considered, and not recommended

- **Daily or weekly tasks and login rewards:** FOMO, against "slow but not grindy".
- **Random affixes on drops:** they fight the tiers tuned to measured kits, and the enchanting budget.
- **Hidden co-op or under-gear scaling:** the user's rule. Item 13 shows the gap instead.
- **Permadeath by default:** the user chose the return of the lost.
- **Resident errands that bring materials home** (R16): they would let the Haven make what only raids give (GDD §7.7). If ever
  built, cap them to home staples.

## 6. Sources

New today:
- Valheim world modifiers (combat, death penalty, resources, raids, portals; presets): https://xgamingserver.com/docs/valheim/world-modifiers ,
  https://mobalytics.gg/gamebase/guides/valheim-world-settings-guide
- Enshrouded custom settings (30+ since "Back to the Shroud"; enemies, death drops, production): https://wikily.gg/enshrouded/world-settings ,
  https://www.gamehostbros.com/guides/games/enshrouded/custom-difficulty-settings
- ARC Raiders Track (an eye icon on tracked workshop materials in raids): https://allthings.how/arc-raiders-workshop-upgrades-steps-tracking-and-materials/ ,
  https://primagames.com/tips/the-best-resources-to-always-track-in-arc-raiders
- ARC Raiders' lead on loadouts ("save us 100 clicks"; building a loadout "is a pain"): https://www.gamesradar.com/games/third-person-shooter/arc-raiders-lead-is-super-aware-of-demand-for-custom-loadouts-that-save-us-100-clicks-admits-he-sometimes-runs-a-free-loadout-because-he-cant-be-bothered-making-one ,
  https://www.pcgamer.com/games/action/arc-raiders-lead-admits-putting-together-new-loadouts-is-a-pain-when-you-want-to-just-get-in-there-and-do-stuff-says-the-game-got-a-lot-right-but-looking-through-a-bunch-of-menus-isnt-necessarily-it
- Tarkov's hideout icon on needed items: https://shacknews.com/article/141320/tarkov-hideout
- Manor Lords approval bands: https://pcgamer.com/games/city-builder/manor-lords-families-population

From the 11-17 reviews (their source lists): Valheim comfort and rested, Stardew bundles, Hades God Mode and the Pact, Deep Rock
Galactic, Last Epoch Forging Potential, Diablo IV, RimWorld's storyteller and tales, ARC Raiders' Raider Hatch and marked
high-loot areas, the Game Accessibility Guidelines, and Lostgarden's value chains.
