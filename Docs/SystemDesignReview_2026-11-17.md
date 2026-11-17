# System design review: how the systems fit together, 2026-11-17

A read-only design review. The user asked: "look at all the systems we have. Research how other similar games have designed their
systems. How can we improve ours, and have everything complement each other? Do we have any systems that work against each other?"

**Scope.** The whole loop: Haven (residents, jobs, buildings, homes, farming, crafting), raids (portal, clock, loot, extraction, loss,
wear), skills, gear tiers and gear score, essence and enchanting, horses, fishing, quests and story, bosses and co-op. Today's two
bug reviews (`SystemReview_2026-11-17.md`, `SystemReview2_2026-11-17.md`) cover bugs. The 11-13 design review
(`DesignReview_2026-11-13.md`) covers the spec. This review is about **how the systems connect**.

**Evidence.** Each claim is marked as one of:
- **Confirmed:** read in code or data, with the file given.
- **Measured:** from the real playtest CSVs in LocalLow. Only the user's own runs were counted: 5 human raids on 11-14/15, all Misty Woods tier I. Some cheats were used earlier in those sessions.
- **Likely:** strong evidence, not traced to the end.

Nothing here has been played by hand. The user's standing decisions are respected. Where this review would change one, it says so in §6 as a question.

---

## 1. Summary

The game's systems are unusually well connected for its stage:
- People found on raids open buildings.
- Buildings feed raids: time, Well Rested, rumours, pockets, field kits.
- Won breaches teach defences.
- Raid experience is held until extraction.
- Lost companions become rumours.
- Junk breaks down, and duplicate inscriptions feed Liv.

Most of the problems are **loopholes and leaks at the edges**, not broken loops. Two other things weaken the core loop: the **raid clock
hasn't bound in real play yet**, and **essence investment is thrown away at every tier step**.

**Conflicts found** (details in §4):

| # | Conflict | Kind |
|---|---|---|
| K1 | Quitting a raid is a better outcome than dying, including in the "all in" boss fights | Reward undermines risk |
| K2 | Reopening the raid planner rerolls the hour, the rumours and the foretold events for free | Reward undermines risk; Watch Post devalued |
| K3 | The stat-reach test counts 2 meals and 1 tonic, but the game allows 4 and 3 | Bound with a blind spot |
| K4 | Territory tithes deliver raid-only creature parts at home every dawn | Home bypasses a raid-only rule |
| K5 | Gear wears only on a failed raid, so the Field Repair Kit ("out where it counts") mends nothing a raid does | Dead item |
| K6 | Comfort can reach about 40 minutes of Well Rested; the longest raid is 24.5 minutes and real raids last 5 | Home stops feeding raids |
| K7 | Six or more speed bonuses act on station queues that already finish during a raid | Reward on a constraint that doesn't bind |
| K8 | Real raids end at 3.4 to 5.6 minutes of 12, so Reach and stage loot bonuses are rarely reached | Paid-for benefit unused |
| K9 | Empowering is lost at every tier step and on salvage; a +3 kit costs 30 to 55 raids of essence | Sink that pushes players to never invest |
| K10 | A ridden horse makes no noise to monsters | Bypasses stealth, Forest Annoyance and the clock |
| K11 | The boss rematch waits 24 real hours (UTC); everything else runs on in-game turns | Clash with the game's own clock |
| K12 | Three death costs: 25% wear on a raid, 10% on a boss, 0% on a crash or quit | Inconsistent rules |
| K13 | Old Silver Pennies have about 30 sources and one real sink | Slack chain |
| K14 | Seven goal-givers compete for the player's attention | Goal overload |
| K15 | Workers take over refining, which also removes the player's Construction practice | Mild tension |
| K16 | Tier V gear has no tier V raid; there is no endgame loop | Dead end |

**Top 10 recommendations, in priority order** (details in §5):

1. **A quit or crash mid-raid counts as a failed raid**, with a crash grace. Decision needed. Code S.
2. **Pin the briefing to the turn:** the next run's seed is set at homecoming or dawn, not each time the planner opens. Code S.
3. **The reach test counts every meal and tonic slot**, or the extra slots become alternatives rather than sums. Test/data S.
4. **No raid-only items in tithes.** Swap the Wolf Fang and Bog Venom rows and add a guard test. Data S.
5. **Horses make noise** by gait, through `WorldNoise`. Code S.
6. **Empowering carries forward:** the altar reforges it onto the next tier's piece, or salvage refunds half. Code M.
7. **Well Rested stops at raid length**, and surplus comfort becomes rested charges for the next raid. Code S to M.
8. **Measure the clock before tuning it.** If raids keep ending at about 5 minutes, make stages pay in ways players can see and redirect Reach. Data plus playtest.
9. **Give the Field Repair Kit a real job**, or re-word and price it as a home repair. Data S, or a decision for wear in use.
10. **One goal ledger at the Notice Board:** asks, orders and territory tasks in one place, with Stardew-style bundles that pay in world access. M to L.

---

## 2. What comparable games do, and where we stand

Condensed from web research. Sources are in §7. "Ours" is what the code does today.

| Pattern | Who does it, and how | Ours today |
|---|---|---|
| **Bosses hand out keys, not stats** | Valheim: Eikthyr's antler makes the pickaxe for the next ore, and the Elder's Swamp Key opens the crypts [V3]. V Rising: every V Blood boss unlocks recipes, structures or abilities [VR4]. Enshrouded: boss heads pay for Flame Altar levels [E1] | Partly. Ward III asks for one boss beaten (`SettlementSettings.wardLevels[2].bossesBeaten = 1`). The Heartroot opens the Valley. Boss drops are mostly materials |
| **Home quality pays out as a timed buff, longer rather than stronger** | Valheim: rested lasts 7 min + 1 min per comfort point (22 comfort = 29 min), with +100% stamina regeneration and +50% skill XP [V1]. Enshrouded: comfort lengthens rested [E3] | Yes. Well Rested is 60 s per comfort point: +15% stamina, +10% health, +20% XP (`HavenSettings.restedBonuses`). There is no cap and no base time (K6) |
| **People rescued in the world unlock crafting** | Enshrouded's five craftspeople come from vaults [E2]; Hades' contractor [H4] | Yes, and a strength: `RescueOrder.md`, people open their workplaces |
| **Outgrown gear becomes base income** | V Rising: servants hunt for 2 to 23 h, with success set by their gear against the region [VR2, VR3]. Arc Raiders: Scrappy brings materials home even after a death [AR4]. Tarkov: hideout production [T3] | No. Old gear only salvages (50% of its recipe × its durability). Workers refine, but they never go out |
| **A safety net under losable gear** | Arc Raiders: a free random kit, plus a Safe Pocket [AR1, AR2]. Hunt: a free hunter, half XP on death, no permanent loss below rank 11 [HU1, HU2]. Tarkov: insurance and a secure container [T1, T2] | Strong. Worn gear is kept at −25% durability, there are 1 to 3 safe-pocket slots, and Insight still comes on a failed raid (40 against 150) |
| **Raw materials travel the hard way** | Valheim forbids ore through portals [V4] | Partly. The bag holds 150 weight, but carts and horses come through the portal (K10) |
| **Per-item crafting budget** | Last Epoch: Forging Potential [D3]. Diablo IV: Tempering Durability [D1] | Yes. Each enchantment counts once, with a ×1.12/×1.08 budget. But empowering doesn't carry forward (K9) |
| **Short runs feed a permanent city** | Against the Storm: settlements under an hour restore the Smoldering City [C5]. RimWorld: threat scales with wealth, eased after losses [C1] | Yes. Corruption nights follow portal tier and blight (never buildings), and come only after a successful extraction. That is a good anti-spiral |
| **Opt-in difficulty with exclusive pay** | Hades' Pact of Punishment pays one-time bounties [H3]. Diablo IV's Pit [D1] | Seeds only: Blood Moon and Deep Rune pay more loot, but nothing only they give. No endgame ladder (K16) |
| **One visible goal list that needs every system** | Stardew's 30 bundles need farming, fishing, foraging and mining, and each room opens a bridge, minecarts or the greenhouse [S2] | Partly. Haven asks (21, three at once) pay coin and XP, not access (K14) |
| **Death moves the story on** | Hades: "the story would advance when you died" [H1] | Partly. Lost companions return as rumours, and residents react to losses |
| **Sinks a little larger than sources** | Lostgarden's value chains: otherwise "there is no longer a pull on the earlier nodes" [G1]. Value-scaled sinks [G3] | Mixed: essence has no pull in the mid game, pennies are slack (K9, K13) |

---

## 3. Our loops, mapped

### 3.1 The core loop as built

```
 HOME (no clock)                                     RAID (12:00 + ward 1:30/level + Leader 2:00 + Sparks 3:00 × 2 ≤ 24:30)
 craft/refine (workers, queues run during raids)     gather (noise → Forest Annoyance), fight (Melee/Archery/Runecraft XP,
 cook 2 meals + 1 tonic, Well Rested (comfort × 60 s)    raid bonus ×1.5 held), caches/events/rumours (relics, people,
 choose: tier (gear floor + ward), runes, Sparks,        inscriptions, blueprints), stages +25/50/80% loot,
 companion, cart/horse                    ──────►    light + hold an exit 30-35 s (beacon calls creatures)
        ▲                                                         │ extract: haul, essence, bonus XP, people, horse/cart
        │                                                         │ die/time out: lose carried + meals; gear −25%; Insight 40
 purify 2:1 → ward (opens tiers), empower, enchant,               ▼
 skill ranks; Haven asks; Trading Post; renown → ranks → settlers; corruption night may be foretold (25-50%)
```

### 3.2 Economy: sources and sinks

| Resource | Sources | Sinks | Balance |
|---|---|---|---|
| **Cursed Essence** (raid only) | Creatures, about 1.1 per kill (**Measured**: 6 to 22 per raid in raids of 3.4 to 5.6 min, about 3.6 a minute). Night ×2, stages up to +80%, elites 2 to 3, caches 30 to 35% | Purify 2:1 (20 s a batch). Raw enchanting | Healthy source |
| **Purified Essence** | The altar (+25% at Altar II and III) | Ward I to III: 6/12/20 (38 in all). Skill ranks: 10/20/35 per ability. Empower: 6/12/24 + 2 per tier above I, **per piece**. Enchantments: 8 to 22 | Ward is cheap (about 3 raids). Empower is enormous: a +3 tier IV kit is about 600 Purified, roughly 1,200 Cursed or 30 to 55 raids, and **it resets each tier** (K9) |
| **Life Orbs** | Life Orb nodes, caches (25%), breaches, events | Portal repair 3. Portal levels 4/6/8. Ward II 4, ward III 6. **Portal Sparks: 2 each** | A real choice between time now and pockets or speed later. But time isn't binding (K8) |
| **Old Silver Penny** | About 30 creature, cache and event tables. Valuables (6/15/40). Fine and Masterwork gear (0.1 / 0.25 × value). Haven asks (6 to 25) | Trading Post (all stock ≈ 153 coins a restock, restocked after each raid survived). Bridge tolls. 2 pedlar events | Slack once the Trollstone line is bought (K13) |
| **Pact Rune** | Rot's heart (1). Caches (5%). Traveller's Pack (4%) | Territory claims, +25 hold | Fine |
| **Insight** | Quests 50, extraction 150, failure 40, exploring, first crafts | Any skill: full value below level 20, half above | Good catch-up; keeps practice ahead |
| **Renown** | Buildings, residents (8), clearing 40, territory (up to 132), homes (0 to 8), total level (1 per 20) | Ranks at 25/90/180/300: ward radius and settlers | Good. Strength never scales with it (decision 8.6) |
| **Comfort** | 14 building kinds (23 at most), beds, best home (≤4), yards | Well Rested length only | Saturates (K6) |
| **Durability** | Repair at 50% of the recipe for a full mend | Lost only on a failed raid (25%) or a glancing tool blow (2%) | No sink for winning players (K5) |
| **Materials** | Raid nodes (about 10 copper, 6 iron, 8 hardwood, 6 flax, 4 granite). Caches. Home nodes. Workers. **Tithes** | Buildings, gear, upgrades, Haven asks, repairs, salvage back in | Watch tithes (K4) |

### 3.3 Gates

| Gate | Asks | Comment |
|---|---|---|
| Raid tier | Gear-score floor (15/30/45) **and** a ward level. The ward needs Purified Essence, the Smithy tier, Yrsa (tier III) and a boss (tier IV) | Clear and visible. The planner also shows the fighting skill expected (Melee 10/20/30/40) |
| Smithy and other building tiers | Materials, plus other buildings (`BuildingLevel.requires`) | "The whole haven climbs together", which is good |
| People | Each rumour waits for the person before (`RescueOrder.md`). Yrsa is 8th | At least about 12 raids for everyone. This sets the pace of ward II |
| Recipes | Station tier, people, blueprints found on raids | Visible locks: good |
| Enchantments | Inscriptions found, plus a Runecraft level (Liv carves past it) | Good |

### 3.4 Expected pace (from `BaseProgression.md` and `Skills.md`, calculated)

| Goal | Raids |
|---|---|
| Copper pickaxe, Smithy II, Workshop II | 2 |
| Full copper kit | 2 more |
| Every tier III upgrade | 5 to 6 |
| Every tier IV upgrade | 5 to 6 |

That is about 15 raids to finish tier IV crafting, or 8 to 11 hours at 30 to 40 minutes a cycle. Skills reach level 30 in about 9.5 hours of mixed play and level 40 in an estimated 15 to 20 hours. So at tier IV the expected Melee 40 will probably lag by about 5% damage. That is small, and higher tiers pay more XP per action, but it should be watched in the `SkillXp` rows.

---

## 4. Conflicts in detail

### K1. Quitting beats dying (High; Confirmed)

`SaveGameService.SaveBeforeRaid` saves at launch and nothing during the raid. `PauseMenu.cs:476` offers "Quit the game? This raid ends
here, and nothing from it comes home. You will be back at the portal as you set out."

Quitting therefore **keeps**:
- the consumables you carried in;
- the gear without its 25% wear;
- the horse and cart, which are otherwise lost if not in reach;
- a companion who would otherwise be "forgotten".

In a boss encounter (decided as "all in": win or die, 10% wear), quitting gives a **free retry with full flasks and no wear**. That undoes the
user's decision. Death costs more than the button beside it, so Pillar C ("meaningful risks") rests on the player not using the menu.

The policy was an agent's choice ("No penalty was added", in the memory note on raid companions), not the user's.

### K2. The briefing rerolls for free (Medium; Confirmed)

`RaidPlan.Begin` → `FreshSeed` runs **every time the planner opens** (`RaidPreparationWindow.cs:229`). The seed decides:
- the hour: night pays +40% cache loot and ×2 essence (§7.3);
- the rumours: relics, people, blueprints;
- the events the Watch Post foretells.

Closing and reopening the planner fishes for a night run or a relic rumour. That undercuts:
- the Watch Post's "+1 rumour" Knowledge, since rerolling is cheaper;
- "night is a real choice";
- relic scarcity (6% from caches, otherwise rumours).

### K3. The reach bound misses food slots (Medium; Confirmed)

`PlayerBoons` adds every source of extra slots:
- meals: `SurvivalSettings.MealSlots` (2), plus Kitchen III, plus the Cooking technique Third Plate, giving **4**;
- tonics: 1, plus the Cauldron's Master's Shelf, plus the Alchemy perk Second Vial, giving **3**.

`RaidTierEditor.StackedKitOf` (lines 240-259) eats only `MealSlots` and `TonicSlots`, so 2 and 1. The ×1.5 toughness / ×1.35 damage
guarantee (`RaidTierTests`, from the user's player-stats decision) therefore doesn't see two meals and two tonics.

GDD §11.4 says the second tonic slot comes from the Shelf "**or**" the perk. The code adds both.

### K4. Tithes hand out raid-only parts at home (Medium; Confirmed)

Some regions' tithes (`Resources/TerritorySettings.asset`) deliver items flagged `ItemDefinition.RaidOnly` every dawn, while a region is Secure:
- the Wolfwood gives **Wolf Fang** ×1;
- the Drowning Fen gives **Bog Venom** ×1.

This conflicts with three things:
- the user's loot answer: creature parts are raid-only;
- GDD §7.7: the Haven "can't make anything new";
- two Haven asks that exist to send the player out: Sigrid's 6 Wolf Fangs and Ingrid's Bog Venom.

The amounts are small, but the rule is broken quietly, and nothing tests for it.

### K5. Gear doesn't wear in use, so the Field Repair Kit is miscast (Low to Medium; Confirmed)

`PlayerSnapshot.cs:512` (a failed raid) is the only place that lowers equipment durability. Tools also lose 2% on a glancing blow
(`PlayerGathering.cs:527`).

The Field Repair Kit (Workshop III) "mends everything you are wearing by a third, **out where it counts**". On a raid nothing has worn, so its
only real use is as a cheaper repair after a death.

There is also a side effect: repair is a material sink only for players who fail.

### K6. Comfort outgrows the raid (Medium; Confirmed numbers)

Comfort sources:
- 14 building kinds, 23 points at most;
- 1 per resident with a bed: about 12;
- the best home: up to 4;
- yards.

That is about 40 comfort, or **40 minutes of Well Rested**.

The longest possible raid is 720 + 270 (ward III) + 120 (Leader) + 360 (two Sparks) = **24.5 minutes**. Measured raids run 3.4 to
5.6 minutes. Past about 13 comfort (real raids) or 25 (the theoretical maximum), new decorations, homes and beds add nothing to raids.
That works against the colony-direction rule: judge base features by what they do for raids.

Valheim avoids this because its trips have no clock and its comfort caps at about 22 [V1].

### K7. Speed bonuses on queues that don't bind (Low; Likely)

`BaseProgression.md` states that queues finish during raids: the longest full Smithy queue is 3.5 minutes. `HavenProgressionTests`
therefore forbids upgrades that are only speed.

But newer systems pay out in speed:
- home level: +0 to 20% work speed;
- a bed: +25%;
- worker ranks: ×1.1 to ×1.4;
- the Kitchen Garden: +20% station speed;
- horse jobs.

Unless worker production targets are sometimes still unmet at homecoming, these bonuses are invisible. The 11-13 review already noted
that "percent bonuses are barely felt". Measure it (`WorkerProduction` targets reached by homecoming) before changing anything.

### K8. The clock hasn't bound yet (Medium; Measured, small sample)

The 5 human raids lasted **201, 252, 294, 320 and 337 s** of 720 (4 extractions, 1 death). The Misty Woods' first stage starts at 8:00
left, which is 4:00 in. Most runs ended near the first stage and before "The Hunt", so these were never reached:
- the stage loot bonuses (+25/50/80%);
- the Reach purchases: ward +90 s per level, Sparks +180 s for 2 Life Orbs, the Leader +120 s.

The first raid was a rescue (rescue, then leave). Early, quest-driven tier I raids may be short by nature. This is a signal to measure
(decision 8.17 already says to measure), not a verdict.

### K9. Empowering is thrown away (Medium; Confirmed)

`GearPower.cs:145`: an empowering step costs `step.purified + (tier−1) × 2`. Salvage lists the empowering and the enchantment as lost
(`Salvage.cs:64-75`), and nothing moves them to a new piece.

A full kit is about 10 pieces at +3 (6+12+24 plus the tier surcharge):
- tier I: about 420 Purified;
- tier IV: about 600 Purified.

At the measured income of roughly 20 to 40 Cursed a raid, that is 30 to 55 raids, longer than a whole tier (5 to 6 raids).

So the rational play is to **empower nothing until the last tier**. Essence then has little pull in the mid game (ward 38 in all, one
skill rank or two), and a cliff at the end. Empowering also raises the gear score toward the next tier, just before the piece is
replaced.

### K10. Horses are silent in raids (Medium; Likely)

`PlayerNoise` makes footsteps only from `PlayerMovement` (`Update`: `movement.IsMoving`). No script in `Horses/` emits `WorldNoise`;
hoofbeats are only audio. A gallop is 10 to 12.5 m/s (`Wild Horse.asset`).

`HorseTravel` brings the ridden horse into raids, so a mounted player probably crosses the map faster and quieter than on foot. That
bypasses:
- monsters' hearing;
- Forest Annoyance;
- part of the clock.

Check this in play with the stealth readout.

### K11. Real-time rematch wait (Low; Confirmed)

`RaidLauncher.CheckRematch(raid, DateTime.UtcNow)` uses 24 real hours. Everything else counts in-game "turns": a dawn or a raid end,
used by territory, the Trading Post's restock and corruption.

A real-time wait in a PC survival game can be beaten by changing the system clock, and it blocks a weekend player for a day. The user
asked for "24 hours after a completed kill"; it may have meant game hours (§6, Q3).

### K12. Three death prices (Low; Confirmed)

- A raid death or time-out: 25% wear (`DurabilitySettings.lossOnFailedRaid`).
- A boss: 10% (the user's call, with "we might lower the global cost" left open).
- A crash or quit: 0%.

The first two are a known open question. The third is K1.

### K13. Pennies pile up (Low; Likely)

Pennies come from about 30 tables, valuables and better-made gear. The Trading Post's whole stock costs about 153 coins and restocks
after each raid survived. Its most expensive line is Trollstone (35), and many lines stop mattering once the player has outgrown the
tier. Mid to late game, coin has no pull (Cook: "no longer a pull on the earlier nodes" [G1]).

### K14. Seven goal-givers (Medium; Confirmed count)

All of these can want the player's next raid:
- quests (gold, silver, bronze);
- rumours (2 to 3);
- Haven asks (3 at once);
- crafting orders;
- territory tasks and pushes, with urgent rumours;
- forgotten companions;
- breach strikes, plus boss find steps.

The homecoming hold (2 per return) eased the 11-15 overload. The 11-13 review's "too many parallel ladders" still stands.

### K15. Delegating refining removes practice (Low; Confirmed)

Workers' orders train nobody, which is the user's rule. Once Tor and Bjarne refine, the player loses Construction's 0.5-per-batch refining
practice. Base growth and skill growth pull in opposite directions here, but mildly. The rule stays; §5 offers a complement.

### K16. No tier V, no endgame (Medium; Confirmed)

Tier V gear (Trollstone) exists, but the highest raid tier is IV, which has no new creature. The status table in `ARCHITECTURE.md` says
"Later loops (seasons, endgame) not built". Boss rematches at higher tiers are the only use for a full tier V kit.

### Doc drift (Low)

- `BaseProgression.md` says Yrsa arrives by rank; `RescueOrder.md` has her as the 8th rescue.
- The GDD says 13 skills; there are 14 assets (Horsemanship).
- GDD §11.4 says the tonic slot comes from one source "or" the other; the code adds both.

---

## 5. Recommendations

### 5.1 Quick data tuning and small fixes

| # | Change | Where | Impact | Effort | Risk | How it complements |
|---|---|---|---|---|---|---|
| R1 | **Quit or crash mid-raid = a failed raid** on the next load. Keep the launch save, add a `raidUnderWay` flag, and on load apply `PlayerSnapshot.AfterFailedRaid`: carried items lost, wear. Crash grace: the first occurrence in a session only warns, or real crashes (no clean `SessionEnd`) apply wear only | `SaveGameService`, `PauseMenu`, load path | Restores Pillar C and boss "all in" | S | Medium: real crashes punish (hence the grace) | Makes the safe pocket and portal levels (Steadied, Moonlit) worth their Life Orbs. **User decision** (Q1) |
| R2 | **Pin the briefing to the turn:** store the next seed per destination in `SettlementState`; renew it at a dawn or a raid's end, not on `RaidPlan.Begin` | `RaidPlan.FreshSeed`, `SettlementState` | Ends hour and rumour fishing | S | Low | Uses territory's turns. The Watch Post's extra rumour and foretold events become real knowledge, and night becomes a choice of *when* to go. Optional coin sink: "pay the pedlar for a different word" (R11) |
| R3 | **The reach test counts every slot source** (Kitchen III, Master's Shelf, Third Plate, Second Vial), or the GDD's "or" is enforced (`Max`, not sum, of building and perk) | `RaidTierEditor.StackedKitOf`, `PlayerBoons` | Keeps the user's ×1.5/×1.35 promise honest | S | Low; may need meal numbers trimmed | Protects the gear-tier model the user set |
| R4 | **No raid-only items in tithes:** the Wolfwood gives Rough Hide or Bone, and the Fen gives Peat or Cloudberries plus Flax. Add a test: every tithe item has `RaidOnly == false` | `TerritorySettings.asset`, a new test in `TerritoryTests` | Closes a §7.7 leak | S | None | Keeps Sigrid's and Ingrid's asks as reasons to raid |
| R5 | **Horses make noise:** `HorseMotion` emits `WorldNoise` per stride by gait (walk ≈ a sneaking step, trot ≈ running, gallop ≈ a cart). A Horsemanship technique can quiet it | `Horses/HorseMotion.cs`, `PerceptionSettings` | Restores stealth and the clock's weight for riders | S | Low | Horsemanship and breeding gain a raid-relevant stat (a "calm" temperament) |
| R6 | **The Field Repair Kit gets a job.** (a) Simplest: re-describe it ("mends what the last raid cost, anywhere") and price it as a cheaper repair. (b) Better, but a decision: light wear in use (a block or a heavy hit taken, e.g. 0.2% each), so winners pay a little upkeep and the kit has a field role | `FieldRepairKit.asset` text, or `Combat` hooks | (a) removes a misleading item; (b) gives a sink for successful play | S / M | (b) touches the "modest repair" rule | (b) feeds the repair sink (Valheim-like). **Q4** |
| R7 | **The boss rematch counts in-game time or turns** (e.g. 24 game hours, or 3 turns), not UTC | `BossEncounterDefinition.RematchWait`, `RaidLauncher.CheckRematch` | No clock exploit, no real-day wall | S | Low | The same clock as territory and restock. **Q3** |
| R8 | **Measure the clock** before touching it. Add a `RaidStage` row (already logged as an event) and "seconds left at extraction" to the report, then read 10 or more human raids per tier. If raids keep ending at about 5 minutes: move Misty Woods' first stage to 9:00 left and make stage loot visible (a gold "+25%" chip on caches) | `PlaytestLogger`, `RaidDefinition` › Escalation | Learns whether Reach and stages matter | S | None | Decision 8.17 ("measure") |
| R9 | **Fix the doc drift** (Yrsa, 14 skills, the tonic "or") | `BaseProgression.md`, GDD §11.4 and §16 | One truth | S | None | |

### 5.2 New system work

| # | Change | Where | Impact | Effort | Risk | How it complements |
|---|---|---|---|---|---|---|
| R10 | **Reforge at the altar:** move a piece's empowering (and optionally its enchantment) to a new piece of the next tier for 50% of the essence it cost. Salvage refunds 25% of the essence put in | `AltarWork`, `GearPower`, `Salvage` | Essence has pull all game. Investing in tier I gear stops being a trap | M | Low: the budget per item is unchanged | Like Last Epoch and Diablo IV, where investment carries meaning [D1, D3]. Tier steps no longer reset the player's choices |
| R11 | **Well Rested stops at raid length; surplus comfort gives rested charges.** Rested lasts min(comfort × 60 s, raid length). Every 8 comfort past that is one charge: the next raid also sets out rested even straight from a raid (or after a failed one) | `HavenReadiness.GiveRest`, `HavenSettings` | Every comfort point keeps a raid meaning, without a stronger buff (the reach bound holds) | S to M | Low | Homes, decorations and beds matter to raiders, which is the colony rule |
| R12 | **Speed becomes yield where the queue never binds.** If R8's numbers show workers finish before homecoming: home quality, beds and worker rank give extra-output chance (worker ranks already give 3 to 12%) or quality, not speed | `HomeLifeSettings`, `SettlementSettings.workerRanks` | Base bonuses become felt | M | Medium: balance | The rule "no upgrade is only speed" spreads to people and homes |
| R13 | **The Haven's Ledger at the Notice Board** (the user already chose: the board opens fetch and crafting quests). It holds the three Haven asks, crafting orders and territory tasks, plus **bundles**: 6 to 8 standing collections that each need several systems (fish + crop + raid part + a crafted piece) and pay out in **access**, not coin. Examples: a new raid shape, a Trading Post line, a portal improvement step, a shortcut path, Paddock grazing | `HavenRequestBoard`, `HavenRequestDefinition` (a "bundle" kind) | One place for "what next"; every system gets a reason; fixes K14 | M to L | Medium: content | Stardew's Community Center [S2]. Makes fishing, farming and horses feed the same goals as raids |
| R14 | **Coin sinks that build the home:** the caravan sells decor and cosmetics, horses (breeds), extra Trading Post stock tiers, "a different word" (R2's reroll, priced), and repairs paid in coin at a premium | `TradeSettings`, `HorseTrader` | Pennies keep a pull | S to M | Low | Value-scaled sinks [G3]. Ties raids to the town's look |
| R15 | **An endgame ladder: Pact marks.** The raid runes (Blood Moon, Deep Rune, plus 3 to 4 new ones) stack as a "Heat" on tier IV. Each mark adds danger and pays a **one-time bounty per raid and level**: an inscription of a great enchantment, a relic choice, Trollstone. Boss rematches use the same marks | `RaidModifier`, `RaidDifficulty`, `RaidReport` | Tier V gear gets a home; replay value | L | Medium: tuning | Hades' Pact [H3], Diablo IV's Pit [D1]. Uses the existing raid tier model (raise multipliers, not nerf players) |
| R16 | **Resident errands with outgrown gear.** During a raid, a resident without a job can be sent out with an old kit. They bring back tier materials scaled by kit against tier, and come back hurt, not dead, if they fail | `SettlementState`, `JobDefinition`, a small `ErrandRules` | Outgrown gear becomes income; residents become more than speed | M | Medium: must not replace raids (cap at one tier below the player) | V Rising's servant hunts [VR2] and Arc's Scrappy [AR4]. Feeds companions' bond. Strictly "at home", so §6.3 holds |
| R17 | **Practice from overseeing (optional):** collecting a worker's batch gives the player a small Insight trickle (not practice), so delegating isn't a skill loss | `WorkerProduction`, `ExperienceTracker` | Softens K15 | S | Low | Keeps the user's "crafting XP only from what the player makes" (Insight isn't practice) |

### 5.3 What to keep exactly as is (strong links)

- The raid bonus (×1.5) is held until extraction. That is risk without stripping earned progress.
- Corruption nights come only after successful extractions, and their strength follows portal tier and blight, never buildings. This is RimWorld's adaptation idea without the punishment for building.
- People open their workplaces, and lost companions come back as strangers through rumours.
- Won breaches teach the next defences. Breaches never pay essence.
- Each enchantment counts once, within a budget, and inscriptions are learned, never bought.
- Junk breaks down, and every drop has a use (`ItemUses` on tooltips).
- Co-op adds +60% creature health per friend, and loot is personal.

---

## 6. Decisions for the user

| # | Question | My recommendation and why |
|---|---|---|
| Q1 | **Should quitting mid-raid count as a failed raid?** Today it resets to the launch save, with no wear and consumables back. That was an agent's choice, not yours | Yes, with a crash grace (wear only, once per session). Otherwise the boss "all in" rule and the 25%/10% wear are optional |
| Q2 | **One death price?** A raid costs 25% wear and a boss 10%. You said "rules should stay consistent… we might lower the global cost" | Make both 15%. Gear is kept anyway, and the haul is the real stake. One number is easier to teach |
| Q3 | **Rematch wait: real hours or game time?** It is 24 real (UTC) hours today | 24 game hours or 3 turns. That matches every other clock and can't be beaten by the system clock |
| Q4 | **Should worn gear lose a little durability in use** (blocks and heavy hits), as in Valheim? | Yes, lightly (≈10% over a full 12-minute raid). The Field Repair Kit then has a role, and successful players have a small upkeep sink. Otherwise keep "only on death" and re-word the kit |
| Q5 | **Should empowering carry forward** (reforge at 50%)? | Yes. Otherwise essence has no pull until the last tier |
| Q6 | **Should surplus comfort become rested charges** rather than longer rest? | Yes. Homes and decorations keep their raid value without a stronger buff |
| Q7 | **Do you want an endgame ladder** (stackable raid marks with one-time bounties), or should tier IV and bosses be the end for now? | Build it after the slice is validated (decision 8.16 froze new scope). Design it now so tier V gear has a target |

---

## 7. Sources

Web research (2026-11-17). Numbers marked "uncertain" in the research are not relied on above.

- **V1** Valheim comfort and rested: https://wiki.indifferentbroccoli.com/en/Valheim/Comfort
- **V2** Valheim skill drain on death: https://help.freakhosting.com/games/valheim/configuring-skill-drain-on-death
- **V3** Valheim boss keys: https://allthings.how/valheim-progression-guide-every-biome-boss-and-key-drop/
- **V4** Valheim portals forbid ore: https://www.gamepressure.com/valheim/portals/zce2d1 , https://www.gamesradar.com/valheim-devs-still-undecided-on-the-option-to-teleport-ore
- **E1, E2** Enshrouded Flame Altar and survivors: https://enshrouded.wiki.gg/wiki/Flame_Altar , https://enshrouded.wiki.gg/wiki/Survivors
- **E3** Enshrouded comfort: https://twinfinite.net/guides/enshrouded-comfort-system-explained-how-it-works-what-it-does/
- **RS1** Dragonwilds skills: https://dragonwilds.runescape.wiki/w/Skills
- **VR1** V Rising Castle Heart: https://www.gamerguides.com/v-rising/database/structures/castle/castle-heart
- **VR2, VR3** V Rising servant hunts: https://gamerguides.com/v-rising/guide/dominance/servants/how-to-send-servants-out-for-hunts , https://segmentnext.com/v-rising-servants
- **VR4** V Rising V Blood unlocks: https://xgamingserver.com/blog/v-rising-v-blood-bosses-guide/
- **T1, T2** Tarkov insurance and secure containers: https://afkgaming.com/esports/guide/everything-about-insurance-in-escape-from-tarkov , https://afkgaming.com/esports/guide/everything-about-secure-containers-in-escape-from-tarkov
- **T3** Tarkov hideout production: https://timesaver.gg/blog/tarkov-bitcoin-farm-guide
- **AR1, AR2** Arc Raiders free loadout and Safe Pocket: https://www.gamesradar.com/games/third-person-shooter/arc-raiders-free-loadout , https://www.pcgamer.com/games/third-person-shooter/arc-raiders-free-loadout
- **AR3** Arc Raiders workshop: https://allthings.how/arc-raiders-how-to-upgrade-workshop-stations-and-what-they-unlock/
- **AR4** Arc Raiders Scrappy: https://primagames.com/tips/all-scrappy-the-rooster-upgrade-items-and-how-to-get-them-in-arc-raiders
- **HU1, HU2** Hunt: Showdown hunters and death: https://huntshowdown.fandom.com/wiki/Hunters , https://mein-mmo.de/en/hunt-showdown-what-happens-when-you-die,850013
- **C1** RimWorld raid points: https://www.rimworldwiki.com/wiki/Raid_point
- **C2** RimWorld mood: https://www.rimworldwiki.com/wiki/Mood
- **C3** Frostpunk laws: https://www.gamepressure.com/frostpunk/the-book-of-laws/zca19a
- **C4** Manor Lords families: https://hoodedhorse.com/wiki/Manor_Lords/Family
- **C5** Against the Storm meta-progression: https://wiki.hoodedhorse.com/Against_the_Storm/Upgrades , https://eremitegames.com/progression-update/
- **D1** Diablo IV Season 4 (tempering, masterworking, the Pit): https://news.blizzard.com/en-us/article/24077223
- **D2** Path of Exile currency design: https://pathofexile.com/forum/view-thread/55102
- **D3** Last Epoch Forging Potential: https://www.icy-veins.com/last-epoch/crafting-guide
- **H1** Hades, story driven by death: https://gamedeveloper.com/design/how-supergiant-weaves-narrative-rewards-into-i-hades-i-cycle-of-perpetual-death
- **H2** Hades Mirror of Night: https://primagames.com/gaming/hades-guide-mirror-of-night-upgrades-fated-list-of-prophecies
- **H3** Hades Pact of Punishment: https://rpgsite.net/feature/10287-hades-pact-of-punishment-heat-modifiers-and-how-to-maximize-your-rewards
- **H4** Hades House Contractor: https://www.trueachievements.com/game/Hades/walkthrough/7
- **S1** Stardew energy: https://stardewvalleywiki.com/Exhaustion
- **S2** Stardew bundles: https://stardewvalleywiki.com/Bundles
- **G1** Daniel Cook, value chains: https://lostgarden.com/2021/12/12/value-chains/
- **G2** Machinations: https://machinations.gitbook.io/docs/getting-started/framework-basics
- **G3** Sinks and faucets: https://medium.com/1kxnetwork/sinks-faucets-lessons-on-designing-effective-virtual-game-economies-c8daf6b88d05
- **G4** Co-op loot: https://www.co-optimus.com/blog/article-poster/3400/loot-distribution-is-the-hardest-problem-in-co-op-design-and-most-games-get-it-wrong.html

Project sources read: GDD §5-§13, `ARCHITECTURE.md` (status table, "What only raids give", "Raid tension", "Loot"), `BaseProgression.md`,
`GearScoreAndRaidTiers.md`, `Skills.md`, `Territory.md`, `RescueOrder.md`, `LootTables.md`, `DesignReview_2026-11-13.md` §4.7 and §6.2,
the memory decisions. Data: `Resources/` (Durability, Essence, Settlement, Haven, Leveling, Trade, Corruption, Territory, Relic), `Data/Buildings`,
`Data/Settlement/Requests`, `Data/Essence/Enchantments`. Code as cited above. Playtest CSVs from 11-14 and 11-15 (5 human raids).
