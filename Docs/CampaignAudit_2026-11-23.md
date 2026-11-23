# The Campaign checked against its budget (2026-11-23)

Follows `Docs/CampaignAndSandboxResearch_2026-11-23.md`. The user's answers there:
- check the Campaign now;
- the Haven may continue after the story;
- the Sandbox waits until the Campaign is done and refined;
- in the Sandbox, the eleven can be rare wanderers.

This document checks each system the player meets, against the budget below and the playtest logs. Nothing is built from it yet.

**Verdict:** the Campaign's spine is sound. Rescues bring trades, quests teach buildings, and the big colony systems are gated late:
the Keeper's view after the Heartroot, territory in Act II. Two systems break the budget: **the horse quest** and **forays**. Both
arrive early without the story asking for them, and the horse quest measurably stalled a run. The people layer is fine in kind, but it
shows its whole face from the first rescue. The logs say home time is high (58-72%), and about half of the measured home time is
spent in windows. That is the biggest lever, and it is UI rather than sandbox.

---

## 1. The budget

1. **Every system enters through a story beat or a person.** Nothing appears in the first two hours unless the prologue or the
   early Act I uses it.
2. **Upkeep never blocks the story.** Defaults are generous, and residents fend for themselves unless the player chooses to manage.
3. **The writing wins.** Simulation colours the written people. It never contradicts them.
4. **The raid stays the centre.** Target: the home share of the first two hours at or under about half (GDD §3.3 implies several raids
   per 45-90 minute session).

## 2. What the logs say

The user's three long runs (`%USERPROFILE%/AppData/LocalLow/DefaultCompany/Safe Haven/Playtests`). Minutes are counted between log
rows, with gaps over 5 minutes left out as away time.

| Run | Length | Home | Raids | Prologue | Longest stretch at home | What filled it |
|---|---|---|---|---|---|---|
| 2026-11-19 | 84 min | 49 min (58%) | 23 min | 11 min | min 57→82 | the altar, a fence |
| 2026-11-20 | 82 min | 57 min (70%) | 13 min | 11 min | **min 24→67 (43 min)** | a flower bed, a torch, a chest, fences, a hedge, a gate, the storage shed |
| 2026-11-21/22 | 255 min, 7 sittings | 182 min (72%) | 49 min (19%) | 15 min | **min 80→154 (74 min)** | Safe Keeping and a chest; **A Horse of Your Own (34 min)**; fences; the cauldron; Cut Stone |

The home share per half hour in the long run was 49, 71, 81, 100, 97, 61, 72, 57 and 27%. The two hours from minute 60 had almost
no raids.

**Where home time goes** (the logger's HomeStay rows, written from the long run's last hour: 56 minutes at home):

| In a window | Standing still | Gathering | Building | Walking |
|---|---|---|---|---|
| 48% | 39% | 7% | 5% | 1% |

Caveats:
- **The walk/idle split looks wrong.** 1% walking is not believable for top-down home play. `PlaytestLogger.Doing` probably counts
  walking as idle, so read walking and standing still together.
- **"Window" covers every open window:** the H window, the build menu, stations, chests, the inventory, maybe dialogue. The logger
  doesn't say which.
- **These runs come before the Keeper's view, the 11-22 economy and the people layer.** The newest systems are not in them, and
  the drift predates them.
- **The user enjoys building and decorating**, and much of the 11-20 stretch was chosen decoration. That is fine play, not a chore.
  The budget's target should be measured on the friends' runs. Their 11-22 logs live on their machines; ask for the CSVs.

## 3. Each system: when it comes, what it asks, the verdict

Introduction points come from the quest assets (`Unlocks Buildings`), the settings that name an unlocking quest, and
`Docs/RescueOrder.md`. Times come from the long run.

| System | Met when | Introduced by | Upkeep, and the cost of ignoring it | Verdict |
|---|---|---|---|---|
| Survival: hunger, thirst, warmth, rest | Prologue; Food and Water at ~26 min | Quest | Eat and drink; penalties | **Keep** (GDD §11). Weighed in deep-dive item 6 later |
| Gathering, building, paths, decoration | Opening | Quests (Construction Table) | None | **Keep.** Chosen play |
| Gear and tool wear, repair | From the first tools; Bjarne repairs at half cost | Bjarne (repairs) | Materials for repairs; broken tools | **Keep.** Costs belong to deep-dive item 7 |
| First ward breach | After one extraction, started by the player at the portal, three wolves | Quest (The Weakening Ward) | None | **Keep.** A gentle, chosen lesson, as GDD §5.3 asks |
| **Horses** (A Horse of Your Own) | Starts after A Roof for the Work, the first rescue's quest (~45-90 min) | A place near the wild horse | A Lasso, then **4 Barley**, normally grown on Asa's farm, and **Asa is the 8th person home**; bond wears 2 a day when idle | **Gate later.** In the long run it took 34 minutes and sat in the middle of the 74-minute raidless stretch |
| Torches burning charcoal | Once a torch is built (~30 min in the 11-20 run) | A building | One charcoal a night from the bag and the chests; dark without | **Keep.** Residents' gifts bring charcoal (seen in the log). Check that the first torch never waits on a fuel the player can't make yet |
| Talents, passions, traits, marks, habits, scenes, favourite dish | From the first rescue | Rescues (written people) | None that blocks: Low spirits cost 10% at work | **Keep in kind.** Show it gradually (§4.3) |
| Friends and rivals drifting | From the second rescue | Written friend/friction, then scenes and work | A rival nearby costs a little at work and spirits | **Keep with a band** (§4.4) |
| Gifts, bond with the player | From the first rescue | Talk | None: gifts are a choice | **Keep** |
| Any job for anyone, Who does what | From the first rescue | Trades still come with rescues | None | **Keep.** Show the grid only once it helps (≥ 3 people) |
| Residents' meals | When the Kitchen stands | **Yrsa (5th person home)** | Food in the chests at dawn; hungry is slower, never stopped; favourites now a treat | **Keep.** Well gated |
| Hauling, carry limits, work speed | From the first worker | Automatic | Slower work, no decision needed | **Keep automatic.** Never ask the player to manage it before the Keeper's view |
| Keeper's view: plans, felling rings, quarry, posts | After the Heartroot | Quest (The Road North) | Optional | **Keep.** Well gated |
| Fields and watering | Farm Plot | **Asa (8th person home)** | The Farmer waters; rain | **Keep** |
| **Forays** | **Portal tier I (~20 min)**, the H window's Lands tab, sendable from the first rescue | **Nothing**: no person or quest | A party away, odds, bruises, marks | **Gate**: let a person or a quest bring them |
| Territory, outposts, land parties, the forest's pushes | Act II | Quest (The Cold Stones) | The forest pushes on held regions | **Keep**; weigh it in the defence-loop deep dive |
| Corruption nights, guard posts, the horn, walls | With Eirik and the defences | Eirik (4th person home) | Nights spread blight; repairs | **Keep** (GDD pillar A); weigh it in the defence loop |
| Haven town: homes, yards, comforts, extensions | Huts early; the Cottage with Hild | Buildings, Hild | None | **Keep.** Cosy and optional |
| Enchanting, inscriptions | With Liv | Liv (7th person home) | None | **Keep** |
| The Haven's asks | After the first homecoming | The board | None; asks point to raids | **Keep.** They pull toward raids |
| Death rules (cairn, plunder, Harsh raids) | On raids | Rules | Losses by the stated rules | **Keep** |
| The Stablehand | Asa's trades | Asa | None | **Keep.** It removes horse upkeep for those who want it |

## 4. Changes the budget asks for (proposed, none built)

1. **Start the horse quest when barley is at hand.**
   - Start A Horse of Your Own once Asa is home, or once 4 Barley are in the stash, whichever comes first.
   - Until then the wild horse can still graze near the Haven as a teaser.
   - If the user wants horses early, ask for something the player has by then (berries or wild turnips as the treat) instead of
     barley.
2. **Let someone bring forays.**
   - Hide the Lands tab's foray shelf until a quest opens it. A fitting person: Runa the Tracker ("raids feel random", 9th person home)
     is late; Gunnar the hunter (6th) or Eirik the warden (4th) fit the middle game.
   - The simplest is a short quest from whoever is home third ("Send someone out"), so forays arrive after the player has felt the
     need for more materials.
3. **Show the people layer as it fills.**
   - The resident window's Bonds tab when three people are home or a standing has moved.
   - Kit after their first raid along.
   - Who does what once three trades are known.
   - The Talents tab stays from the start: it is who they are.
   - Hiding empty tabs is a UI change, not a rules change.
4. **A band for the writing.**
   - Written friends never sink below Friendly, and written frictions may warm, since redemption is a story.
   - People with no written tie can become Rivals only through a rivals' scene, never by working near each other alone.
5. **Measure the right things.**
   - Fix the logger's walk/idle split.
   - Write which window was open (H, build, station, chest, dialogue) into HomeStay, so the 48% can be broken down.
   - Then ask the friends for their 11-22 CSVs and measure the first two hours with the newest systems in.
6. **Later, not now: the Haven after the story.** When the Campaign ends, the Haven continues. A director for nights, wanderers
   (the Sandbox's rare-wanderer idea fits here too) and wishes switches on. Built once the story's end exists.

## 5. What stays as it is

Nothing built needs taking out. The spine (rescue order, one building per quest, the Keeper's view after the Heartroot, territory
in Act II, meals with the Kitchen, fields with Asa) already follows the budget. Most of the sandbox depth sits behind people and
quests, as Dragon Quest Builders 2 and Enshrouded do it.
