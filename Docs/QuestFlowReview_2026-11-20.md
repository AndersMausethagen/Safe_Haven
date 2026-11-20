# Quest flow review: the order, the gaps, and a smoother line (2026-11-20)

A read-only review by a quest and story designer. Nothing in the project was changed except this file, and Unity was not used.

**The user's words:** "Right now I feel like its a bit chaos with the quests, the order we unlock buildings. All NPCs we save should
start a quest chain. Some quests dont quite fit in or are not explained properly. [...] When we save Ingrid, or any other NPC, they
should start a quest immediately where we learn what they can help us with. I just saved the first NPC and she didnt give me a quest
until a while later. You should not be able to build the item that NPC can work at until you've started that person's first quest."

**The new rule (being built in code in parallel):** a rescued person's first quest starts on rescue and is never held for a
homecoming, and the building tied to that person unlocks when their first quest starts. Everything below is built on that rule.

**Read for this:** every `QuestDefinition` in `Data/Quests` and `Data/Prologue/Quests` (parsed, not read from the docs), the 13
`JobDefinition` and 11 `ResidentDefinition` assets, all 63 `BuildingDefinition` assets, the 22 raid rumours, the story graphs in
`SafeHaven.unity` that start or react to quests, the portal's per-quest lines, `QuestManager` (the homecoming hold),
`QuestTrackerView` (one per colour), `QuestObjective.Owned`, `ResidentNpc.TeachWhatTheyKnow`, and the user's own run today
(`%USERPROFILE%\AppData\LocalLow\DefaultCompany\Safe Haven\Playtests\20261120-214517.csv`, 72 minutes, no cheats). Quest assets
are as of commit 6a560e04. None of them had local changes when this was read.

Effort: **S** = data or a few lines of text, under a day. **M** = several assets and new dialogue, or a small code change, 1-3 days.
**L** = new raid content, a set piece or a boss, a week or more. **Data** = asset edits only. **Content** = new dialogue (and later
voice), assets or set pieces. **Code** = a script change.

---

## 0. Summary

### What is wrong, in order of how much the player feels it

1. **Ingrid's first quest is held behind the Workshop.** `TheAlchemist.asset` needs `ARoofForTheWork` as well as her rescue. In
   today's run she came home at 24.1 min. The player spoke to her at 24.4 and 24.9 and got nothing. The Workshop went up at 29.6, and
   her quest was offered at 32.3. That is 8 minutes and three conversations.
2. **Her quest can't count what was already done.** Both objectives of `TheAlchemist.asset` have Count Already Owned off. The
   player gave Ingrid the Alchemist's job at 24.9, before the quest existed. That job is never counted, so the quest can stall
   until the job is taken away and given again. Every other job quest has the flag on. This is a data bug.
3. **The real stall was Life Orbs, not stone.** The player cut 26 Rough Stone Blocks (the altar wants 6) but had only 2 of the 4
   Life Orbs. No quest, marker or waypoint shows where more are. The altar was never built, so the essence loop, the portal's tier
   II and Liv's line never started.
4. **Bjarne's first quest never started at all.** He came home at 67.4 min. The homecoming's two held starts went to Cut Stone and
   Something to Fight With, which had waited since minutes 30-32. A Smith's Hearth (`TheSmith.asset`) was still held when the player
   left on raid 3. Every job quest except Ingrid's starts by itself as a held Haven quest, so this can happen to any of them.
5. **A rescue teaches two or three buildings at once, with no quest.** `RescuableTraveler` and `ResidentNpc.TeachWhatTheyKnow`
   teach every building of every job the person has, the moment they are rescued: the Altar and Cauldron (Ingrid), the Lodge and
   Wainwright's Yard (Tor), the Kitchen and Dock (Yrsa), the Hunter's Lodge and Dock (Gunnar), the Farm Plot and Tailor's Shop (Asa),
   and the Cottage, Stone Wall and Stone Wall Gate (Hild). Only the first of each pair has a quest.
6. **Twelve buildings and the portal's tiers have no quest that introduces them.** The **Cauldron** makes healing draughts and the
   raid runes Liv's quest sends you to. The **Tailor's Shop** stands in the way of Smithy III. Then the **Dock**, the **Stone Wall**,
   the **Notice Board** (the only way to get errands and the Haven's asks), the **Spike Trap**, the **Stable**, the **Well**, the
   **Trading Post**, the **Larder**, **Smoke Rack** and **Herb Garden**, and **raising the portal**, which opens every raid tier.
7. **Quests ask for what nothing has taught.** The Kitchen and the Workshop at Tier II want copper ingots. Smelting is taught only
   by The Smith's Request, an errand on a Notice Board that no quest mentions. Liv's job quest sends you to the Cauldron, which no
   quest builds. Her first lesson needs the Purification Altar, which comes from an optional quest of Ingrid's.
8. **The tracker hides a person's quest behind the portal's.** One quest per colour is shown, and within a colour the tracker sorts
   **alphabetically** (`QuestTrackerView.ByRankThenName`). "A Roof for the Work", "A Safe Place" and "Strength for the Road" all come
   before "The Alchemist", so Ingrid's quest was the one hidden.
9. **One slow optional quest blocks the queue.** Strength for the Road was open from minute 16 to minute 59.7. A held quest also
   begins when none of its colour is under way, so while it stood, the green quests behind it waited a whole raid.
10. **Misranked, duplicated and stale quests.**
    - Ingrid's three follow-ups are ranked Tale (silver), though Tales are folk tales found in the woods.
    - "Find Ingrid" is in two quests at once.
    - Ingrid (Marks of the Old Ways) and Liv (The Old Marks) both teach carving a mark at the altar.
    - Sigrid's horn plays at the Hamlet rank, about raid 4, though she is found 11th.
    - The Cold Stones' opening line ("three times through me") no longer matches its trigger (The Road North).
    - Act II stops after the Brukam, and the old troll that broke the stones is never tied to him.

### What to do, in short

1. **Every person's first quest has only their rescue as a prerequisite.** A building it needs that may not stand yet becomes an
   earlier step inside it, with Count Already Owned on, so it ticks at once when the building is already up. Data, S.
2. **Every first quest opens on "Talk to <person>"**, which plays their own offer text: who they are and what they do for you. It
   becomes their quest line's first offer (`ResidentDefinition › Quest Line`), as Ingrid's already is. Data and content, M, about 3
   short lines each.
3. **One building per quest, unlocked when that quest starts** (a new `QuestDefinition › Unlocks Buildings`). The second building of
   a two-job person comes with their second or third quest. Code S, data S.
4. **A chain of 2 to 4 quests for every person** (§5.3), most of it from existing assets. The first introduces the person and their
   station. Later ones introduce the buildings and systems no quest covers now: the Cauldron (Ingrid), smelting (Bjarne), Yard (Tor),
   traps (Eirik), Dock (Gunnar), Tailor's Shop (Asa), Stone Wall (Hild).
5. **Two portal quests for the two silent systems:** raising the portal (tier II) and the Notice Board. Content, M.
6. **The tracker shows a person's quest first within its colour, then the newest.** It should never sort alphabetically. Code, S.
7. **Fix the data bugs and the misranks now** (§5.5). This needs no new content.

---

## 1. The order as built

Read from the assets and the code, and timed against today's run where it got that far. "Held" means `QuestDefinition.
HeldForHomecoming`: a Haven, Tale or Errand quest that starts by itself begins only at home. At most 2 begin per return. One more may
begin whenever none of its colour is under way.

### 1.1 Prologue (`Data/Prologue/Quests`, 7 Main and 4 Errands)

| Run min | Quest (asset) | Kind | Starts | Asks | Teaches | Unlocks |
|---|---|---|---|---|---|---|
| 0.8 | A Morning by the Fire (MorningCamp) | Main | Sigrid gives it | Hear the three out | Who they are | The errands; The Road East |
| (1.8-2.7) | Wood for the Road → Anything But Porridge → Meat for the Road (WoodForTheRoad, FishForBreakfast, HuntForThePot) | Errand | Eirik, Eirik, Runa, in a chain; lapse when the road starts | 6 logs, 2 fish, a boar | Chopping, fishing, the bow | 4 Sticks, 2 Stewed Berries, 20 arrows |
| optional | A Lesson from Eirik (EirikLesson) | Errand | Eirik | Dummy, dodge, **parry**, duel | The combat basics | 2 Bandages |
| 2.9 | The Road East (RoadToForest) | Main | Sigrid | Walk with the party; the cart; wolves | The weapon, torches | – |
| 5.7 | Shelter from the Storm (FindShelter) | Main | Auto, +5 s | Shelter, 8 Sticks, 12 Small Rocks, **a Campfire** | Gathering by hand, the build menu | – |
| 6.7 | A Moment of Warmth (CatchBreath) | Main | Auto | Rest and talk, tell Sigrid | Liv's lost name | – |
| 8.1 | Eyes Between the Trees (DefendCamp) | Main | Story (Sigrid's "go on" topic) | Two wolf waves | Fighting in a group | – |
| 9.1 | Something Is Coming (ReachPortal) | Main | Auto, +8 s | The waystones, the centre stone | The stones answer you, not Sigrid | – |
| 9.9 | The Last Connection (HoldPortal) | Main | Auto | Hold 60 s | – | Home; what you wore comes too |

The prologue is in good shape. It runs one quest at a time with no flood, and it took 11 minutes today. Two earlier ideas from
`PrologueAndQuestFlow_2026-11-18.md` are still open (§3): the parry lesson is still optional, and the morning errands are still a
chain.

### 1.2 Home: the hearth and the door

| Run min | Quest | Kind | Starts | Asks | Teaches | Unlocks |
|---|---|---|---|---|---|---|
| 11.2 | The One It Caught (Awakening) | Main | On Home Entered | Talk to the portal | The portal, the forgetting | – |
| 12.5 | A Light in the Wood (FirstCamp) | Main | Auto | **A Campfire** (the second one) | Nothing new | – |
| 13.6 | The Rootbound Stones | Main | Auto | 3 Life Orbs from the three stones | Life Orbs | – |
| 14.7 | The Door Wakes (AwakeningThePortal) | Main | Auto | Feed the orbs to the portal | – | Raids |
| 15.2 | Into the Misty Woods (ThroughThePortal) | Main | Auto | **Find Ingrid**; extract | Raids, worn vs carried | Norn's Hourglass, Portal Spark |
| 15.2 | Bring Ingrid Home (AStrangerInTheMist) | Rescue | Auto | **Find Ingrid**; bring her home | – | Ingrid |
| 15.3 | Tools of the Clearing (PrimitiveTools) | Haven, held | Auto +30 s after the door | Stone Hatchet, Stone Pickaxe | Crafting by hand | – |
| 16.0 | Strength for the Road (FoodAndWater) | Haven, held | After the tools | 5 Berries, a drink, Stewed Berries | Food and water | Waterskin |

### 1.3 Raid 1, and the first homecoming (measured)

| Run min | What happened |
|---|---|
| 18.8 | Raid 1. Echoes in the Wilds (Tale) starts at its place |
| 23.6 | Extracted with Ingrid. Into the Misty Woods and Bring Ingrid Home complete. **The Altar and the Cauldron are now in the build menu** (taught at rescue) |
| 24.2 | **Three starts in one second:** A Roof for the Work (Haven, held slot 1), The Weakening Ward (Main), The Smith in the Woods (Rescue) |
| 24.4, 24.9 | The player talks to Ingrid twice. **No quest** (The Alchemist needs A Roof for the Work). The player gives her the Alchemist's job anyway (24.9) |
| 27.4 | The first breach won |
| 29.6 | Workshop built. A Safe Place starts (held slot 2) |
| 32.1-32.3 | The player talks to Ingrid again: **The Alchemist** is offered and taken. Its "give her work" step can't count the job given at 24.9 |
| 32.3 | **Cut Stone** (Ingrid's helper quest) is ready, but held: both slots are used and green quests are under way |
| 38.8 | Chest built, A Safe Place done. 2 Life Orbs stored. The altar wants 4 |
| 59.7 | Strength for the Road done, 43 minutes after it started |
| 63.4-67.4 | Raid 2, Bjarne extracted. The Breathing Hollow (Main) and Planks, Not Logs (Rescue) start in the raid |
| 67.9 | Home. The two held slots go to **Cut Stone** and **Something to Fight With**, both waiting since minute 29-32 |
| 68.5 | The player gives Bjarne the Smith's job by talking to him. **A Smith's Hearth never starts.** The run goes into raid 3 at about minute 70 |

The Purification Altar was never built. Ingrid's line, the essence loop, the portal's tier II and Liv's later line all stand behind
it.

### 1.4 The main story after raid 1

| Order | Quest | Starts | Asks | Unlocks |
|---|---|---|---|---|
| 1 | The Weakening Ward | Into the Misty Woods done | Defend the first breach | – |
| 2 | The Breathing Hollow | The Weakening Ward **and** Bjarne home | Find the hollow in the Misty Woods; get home | (closed by the next if done early) |
| 3 | What Breathes Under the Roots | The Breathing Hollow | Hearth Ember at the campfire; kill the Heartroot | Troll Valley and Brukam's Bridge (on the kill) |
| 4 | The Road North | Heartroot down, +4 s | Hear the portal: the twist | Starts The Cold Stones (story in `SafeHaven.unity`) |
| 5 | The Toll | The Road North | Find the valley's head in Troll Valley; get home | – |
| 6 | Nobody Crosses | The Toll | Kill the Brukam | Moss Marsh (on the kill), Mountain Leap |
| – | *nothing* | | | Moss Marsh has no quest. Act II has no end |

### 1.5 The rescues and each person's first quest

The rescue quests (`BringXHome.asset`) chain each person to the one before. Each rumour (`Data/Raids/Rumours`) waits for the person
before and one extraction (Sigrid three), and all of them are urgent and in the Misty Woods. The order is Ingrid, Bjarne, Tor,
Eirik, Yrsa, Gunnar, Liv, Asa, Runa, Hild, Sigrid.

| # | Person | Rescue opens these buildings at once | First quest (asset) | How it starts | Prerequisites besides the rescue | Given by the person? | Follow-ups |
|---|---|---|---|---|---|---|---|
| 1 | Ingrid | Purification Altar, Cauldron | The Alchemist (TheAlchemist) | By giver (Ingrid's quest line) | **A Roof for the Work** | Yes | Cleansing the Curse, What the Steel Remembers, Marks of the Old Ways (all **Tale**). Cut Stone (portal, held) |
| 2 | Bjarne | Smithy | A Smith's Hearth (TheSmith) | Auto, **held** | **A Roof for the Work** | No | The Smith's Request (errand on the board) |
| 3 | Tor | Woodcutter's Lodge, **Wainwright's Yard** | The Woodcutter's Lodge | Auto, held | **A Roof for the Work** | No | Raise the Walls, A Growing Home and Heavy Loads, **all three at once** |
| 4 | Eirik | Watch Post | The Watch (TheWarden) | Auto, held | **The Woodcutter's Lodge** | No | – |
| 5 | Yrsa | Kitchen, **Dock** | Show Me Your Kitchen (TheCook) | Auto, held | **A Smith's Hearth** | No | – |
| 6 | Gunnar | Hunter's Lodge, **Dock** | Hides and Hooks (TheHunter) | Auto, held | **A Roof for the Work** | No | – |
| 7 | Liv | (raid runes: 6 recipes) | Runes for the Road (TheRuneScholar) | Auto, held | – | No | The Old Marks → Rubbings → Marks of the Marked → A Hand for the Shelf → The Great Work |
| 8 | Asa | Farm Plot, **Tailor's Shop** | Good Dirt (TheFarmer) | Auto, held | **A Roof for the Work** | No | – |
| 9 | Runa | – (+1 rumour) | Eyes in the Woods (TheTracker) | Auto, held | – | No | – |
| 10 | Hild | Cottage, **Stone Wall, Stone Wall Gate** | Stone on Stone (TheMason) | Auto, held | **A Smith's Hearth** | No | – |
| 11 | Sigrid | Haven Banner | Under One Banner (TheLeader) | Auto, held | **A Roof for the Work** | No | – |

Only Ingrid gives her quest in person. For the other ten, the quest appears by itself whenever the hold lets it. The person never
says what they do, and no "!" shows over them. Only Ingrid, Bjarne and Tor have a homecoming memory (`welcomeLine`).

### 1.6 The portal's Haven quests ("The First Days") and the rest

| Quest | Starts | Asks | Then |
|---|---|---|---|
| Tools of the Clearing | The Door Wakes, +30 s, held | Hatchet, pick | Strength for the Road |
| Strength for the Road | Tools, held | Berries, a drink, a stew | – |
| A Roof for the Work | Into the Misty Woods **and** Tools, held | Workshop | A Safe Place, Something to Fight With (+60 s), and eight job quests |
| A Safe Place | Roof, held | Small Chest; store something | – |
| Something to Fight With | Roof, +60 s, held | Knotted Club | – |
| Cut Stone | The Alchemist **started**, held | Walk to the Old Quarry; take the cut stone | – (no chain name, no reward: the find is the reward) |
| A Growing Home | The Woodcutter's Lodge, held | Storage Shed, Hut | A Light They Can See |
| Raise the Walls | The Woodcutter's Lodge, held | 4 Palisades | – |
| Heavy Loads | The Woodcutter's Lodge, held | Lodge II, Wainwright's Yard, Wheelbarrow | – |
| A Light They Can See | The Weakening Ward **and** A Growing Home, held | Haven rank Hamlet | 2 Life Orbs; Sigrid's horn story |
| The Cold Stones | Story after The Road North | Hear the portal; light a boundary stone | What the Land Asks; The Forest Answers (on its event) |
| Know Your Enemy (Errand) | Notice Board, after Into the Misty Woods | Kill 2 Forest-Bitten Wolves | – |
| The Smith's Request (Errand) | Notice Board, after A Smith's Hearth **and** the Lodge | 3 Copper Ingots, Smithy II, Copper Pickaxe | 2 Iron Ingots |
| Echoes in the Wilds (Tale) | Reach the echo in a raid | Kill 3, reach the western beacon | Greater Healing Draught |

### 1.7 Every building, and what makes it buildable

"Known" means known from the start. "Taught" means it is learned from a person or an item. "Needs" is `BuildingDefinition.Requires`
or a Haven rank. Decorations and fences that need nothing are left out.

| Building | How it becomes buildable | Quest that introduces it | Problem |
|---|---|---|---|
| Campfire | Known | Shelter from the Storm (prologue), A Light in the Wood | Taught twice |
| Workshop | Known | A Roof for the Work (after raid 1) | Every station except the Altar needs it, yet it waits for raid 1 |
| Small Chest | Known; needs Workshop | A Safe Place | – |
| Hut | Known; needs Workshop | A Growing Home (after Tor) | – |
| Spike Trap | Known; needs Workshop | **none** | Breach defence never shown |
| Well | Known; needs Workshop | **none** | – |
| Notice Board | Known; needs Workshop and **Homestead** rank | **none** | It carries all errands and all 21 Haven asks |
| Stable | Known; needs nothing | **none** | Horses have no quest |
| Palisade, Storage Shed | Known; need the Lodge | Raise the Walls, A Growing Home | Greyed in the menu from minute 1 with "needs Lodge" |
| Purification Altar | Taught: Ingrid's rescue | The Alchemist | Taught before the quest; Life Orbs unexplained |
| Cauldron | Taught: Ingrid's rescue; needs Workshop | **none** | Healing draughts and every raid rune are made here |
| Smithy | Taught: Bjarne's rescue; needs Workshop | A Smith's Hearth | Its quest can be held a whole raid |
| Woodcutter's Lodge | Taught: Tor's rescue; needs Workshop | The Woodcutter's Lodge | – |
| Wainwright's Yard | Taught: Tor's rescue, or the Wainwright's Ledger; needs Lodge II | Heavy Loads | Taught a long time before it can be built |
| Watch Post | Taught: Eirik's rescue; needs Lodge | The Watch | – |
| Kitchen | Taught: Yrsa's rescue; needs Smithy | Show Me Your Kitchen | Wants copper ingots: smelting untaught |
| Dock | Taught: Yrsa's **or** Gunnar's rescue (Fisher) | **none** | – |
| Hunter's Lodge | Taught: Gunnar's rescue; needs Workshop | Hides and Hooks | – |
| Farm Plot | Taught: Asa's rescue; needs Workshop | Good Dirt | – |
| Tailor's Shop | Taught: Asa's rescue (Weaver); needs Hunter's Lodge | **none** | **Smithy III needs it** |
| Cottage | Taught: Hild's rescue; needs Workshop II | Stone on Stone | – |
| Stone Wall, Stone Wall Gate | Taught: Hild's rescue; needs Workshop III | **none** | – |
| Haven Banner | Taught: Sigrid's rescue; needs Workshop | Under One Banner | – |
| Larder, Smoke Rack, Herb Garden | Known; need the Kitchen | none | Small; can stay without a quest |
| Trading Post | Known; needs Workshop II and **Village** rank | **none** | – |
| Iron Trap, Runic Ward Totem | Known; need Smithy III / Watch Post III and the Altar | none | Late; can stay |
| Sauna, Beehive, Elder Tree | Taught by a raid find (Bathhouse Plans, Swarm in a Skep, Elder Sapling) | The find's rumour | Fine: the rumour is the introduction |
| **Portal tiers II-IV** | Ward levels: Purified Essence, Life Orbs, Smithy tier, Yrsa, a boss (`SettlementSettings.wardLevels`) | **none** (only the "Wanted at home" target) | It opens every raid tier |

---

## 2. Problems, with the assets

### 2.1 The first quest of a rescued person (the user's complaint)

| # | Problem | Where | Severity |
|---|---|---|---|
| P1 | Ingrid's quest needs the Workshop before it can even be offered. Measured: 8.2 min and 3 talks. | `TheAlchemist.asset` › prerequisites: `AStrangerInTheMist`, `ARoofForTheWork` | High |
| P2 | Its two objectives (Assign Job, Build Structure) have Count Already Owned off. A job given before the quest (done in the run at 24.9) or an altar built before it (it is in the menu from the rescue) never counts. Every other job quest has it on. | `TheAlchemist.asset` › objectives › `countAlreadyOwned: 0` | **High (bug)** |
| P3 | Ten of eleven first quests start by themselves as held Haven quests. They wait for the share of 2 per return, and the portal's quests use it up. Measured: A Smith's Hearth never started after Bjarne came home. | `TheSmith`, `WoodcuttersLodge`, `TheWarden`, `TheCook`, `TheHunter`, `TheRuneScholar`, `TheFarmer`, `TheTracker`, `TheMason`, `TheLeader` (rank Haven, start Auto) | High |
| P4 | Eight of those also wait for a building quest: the Workshop (6), the Lodge (Eirik), the Smithy (Yrsa, Hild). | Same assets › prerequisites | High |
| P5 | None of the ten is given by the person, so no conversation says what they do. Their texts are second-hand ("Gunnar cures hides into leather"). | `ResidentDefinition › Quest Line` is filled for Ingrid only | Medium |
| P6 | A rescue teaches every building of every job at once, before any quest. Six people open two or three buildings each. The build menu fills with things nobody explained. | `RescuableTraveler.cs:384,399` → `ResidentNpc.TeachWhatTheyKnow`; `JobDefinition › Teaches Buildings` | High |
| P7 | The tracker sorts within a colour alphabetically, so a person's green quest named "The …" loses to the portal's "A …" quests. | `QuestTrackerView.OnePerKind` + `ByRankThenName` | Medium |
| P8 | A held quest also begins when none of its colour is under way. One quest left open for 43 minutes (Strength for the Road) therefore held every green quest behind it for a whole raid. | `QuestManager.MayStartHeld` | Medium |

### 2.2 Asked before taught, and what the player can't find

| # | Problem | Where |
|---|---|---|
| P9 | **Life Orbs for the altar.** The altar costs 4 Life Orbs. The door used the only three the quests point to. The text says "the rootbound stones in the meadows still hold orbs", with no marker or quest. The next orbs from a quest are A Light They Can See's 2, at the Hamlet rank, several raids later. Measured: 2 of 4 after 40 minutes, altar never built. | `PurificationAltar.asset` cost; `TheAlchemist.asset` description |
| P10 | **Copper ingots** are asked by Show Me Your Kitchen (Kitchen: 4) and Stone on Stone (Workshop II: 4). Smelting is taught only by The Smith's Request, an errand on the Notice Board, which no quest asks for. | `Kitchen.asset`, `ConstructionTable.asset` T2; `SmithsRequest.asset`; `NoticeBoard.prefab` |
| P11 | **The Cauldron.** Liv's job quest says she "teaches you to cut [runes] at the Cauldron", and every raid rune and healing draught is made there (station 4). No quest asks for a Cauldron (8 Life Mote, 8 Rough Plank). | `TheRuneScholar.asset`; `Cauldron.asset`; recipes `RuneOfPlenty`, `HealingDraught` |
| P12 | **Liv's first lesson needs the altar.** The Old Marks asks to carve at the Purification Altar. The altar comes from an optional Haven quest of Ingrid's that stalls on Life Orbs (P9). | `TheOldMarks.asset` |
| P13 | **The Notice Board is never introduced.** It needs the Homestead rank, and no quest asks for that rank either. Without it there are no errands and none of the 21 Haven asks (`HavenRequestSettings.postedAt`). | `NoticeBoard.asset`; Data/Settlement/Requests |
| P14 | **The portal's tiers are never introduced.** Tier II needs 6 Purified Essence and a Smithy, tier III 12 essence, 4 Life Orbs, Smithy II and Yrsa, tier IV a boss. They open the raid tiers. Cleansing the Curse pays 4 Purified Essence "for the next" without saying what it is for. | `SettlementSettings.wardLevels`; `CleansingTheCurse.asset` |
| P15 | **The Tailor's Shop is on the spine with no quest.** Smithy III needs a Tailor's Shop (iron gear wants linen). Asa's only quest asks for the Farm Plot. | `Forge.asset` T3 requires; `TheFarmer.asset` |

### 2.3 Pacing: bushes and floods that remain

| # | Problem | Where |
|---|---|---|
| P16 | **Tor's bush.** The Woodcutter's Lodge opens four quests at once: Raise the Walls, A Growing Home, Heavy Loads and The Watch (once Eirik is home). The 11-15 fix that chains them (§3.4.1 of `QuestAndStoryResearch.md`) was not built. | `RaiseTheWalls`, `AGrowingHome`, `HeavyLoads` › prerequisites: `WoodcuttersLodge` |
| P17 | **The first homecoming has three starts in the same second**: a Main, a Rescue and a Haven quest. Then 2-3 more follow. That is fine in count, but nothing is a person's. | Run 24.23 |
| P18 | **The first raid's two quests have the same objective.** "Find Ingrid" is in both Into the Misty Woods and Bring Ingrid Home, so there are two tracker lines for one act. | `ThroughThePortal.asset`, `AStrangerInTheMist.asset` |
| P19 | **About 40 minutes at home between raids 1 and 2** in the run, much of it on optional chores (Strength for the Road, fences). The prologue and the door move fast. The first home stretch then sags, because nothing personal pulls the player along. | Run 24.1 → 63.4 |

### 2.4 Quests that don't fit, or aren't explained

| # | Problem | Where | Fix kind |
|---|---|---|---|
| P20 | Ingrid's follow-ups are ranked **Tale** (silver). Tales are "folk tales found out in the woods" (`QuestRank`). They take the silver slot from Echoes in the Wilds and read as side stories. | `CleansingTheCurse`, `WhatTheSteelRemembers`, `MarksOfTheOldWays` › `rank: 1` | Data |
| P21 | **Two teachers for one verb.** Ingrid's Marks of the Old Ways ("carve a mark into a piece of gear at the altar") and Liv's The Old Marks ("carve an enchantment into your gear at the Purification Altar") teach the same thing, raids apart. | `MarksOfTheOldWays.asset`, `TheOldMarks.asset` | Text |
| P22 | **A Light in the Wood** asks for the campfire the prologue taught 6 minutes earlier. The 11-18 proposal (the shelter's ember lights the Haven's hearth) is unbuilt. | `FirstCamp.asset` | Content |
| P23 | **Sigrid's horn** ("That's Sigrid.") plays when A Light They Can See completes, about raid 4. She is rescued 11th, at about raid 11. The approved plan puts the horn at The Road North. | `SafeHaven.unity`, story on A Light They Can See completed | Scene data |
| P24 | **The Cold Stones'** opening line says "Three times through me, and three times home". Its trigger was moved to The Road North (11-15). | `SafeHaven.unity`, story on The Road North completed | Text and voice |
| P25 | **The old troll** that "broke my stones" and "walks that road" (The Road North) is never resolved. The Act II boss is the Brukam, "old when my stones were raised", but nothing says they are one creature. The 11-15 decision "fought or paid" is built as a kill only. | `TheRoadNorth` story; `NobodyCrosses.asset`; `Boss Brukam.asset` | Text, or content |
| P26 | **Act II stops after the Brukam.** Moss Marsh opens with no quest, and every rescue is still in the Misty Woods. The decision of 11-15 was that Act II ends with Sigrid's rescue in Troll Valley. | `MossMarsh.asset`; `Data/Raids/Rumours/Sigrid.asset` | Content (L) |
| P27 | Old contradictions, still there: Ingrid "can tend your wounds" at the prologue camp, where she isn't; "a heart of black wood, **Runa says**" before Runa is home; Sigrid speaks of "the children", and there are none. | `DefendCamp.asset:17`; `Rumours/RotsHeart.asset`; `Requests/wolf-fangs.asset`, `order-masterwork-mail.asset` | Text |
| P28 | **Something to Fight With** gives a club to a player who already has the weapon they chose in the prologue. Its reason ("won't last for ever") is thin. | `FirstWeapon.asset` | Text |
| P29 | **Know Your Enemy** ("kill 2 wolves") is the board's only errand early. It teaches nothing the first raid didn't. | `KnowYourEnemy.asset` | Keep as filler |
| P30 | **Docs out of date.** GDD §15.3 gives the old rescue order and "30 more seconds" for the Leader. `RescueOrder.md` row 7 says Eirik comes "before the first breach" (the breach is after raid 1, Eirik is fourth). | `GameDesignDocument.md:887`; `RescueOrder.md` | Text |

---

## 3. What the earlier documents asked for and is still unbuilt

| Source | Item | State |
|---|---|---|
| QuestRedesign (11-10) | The figure in Granheim mail in raid 1; the troll's footfalls | Unbuilt (in Ingrid's memory only) |
| QuestRedesign | Sigrid's horn at The Road North | **Unbuilt** (P23) |
| QuestRedesign rule 6 | "Each person brought home remembers one thing" | 3 of 11 (Ingrid, Bjarne, Tor) |
| QuestAndStoryResearch (11-15) | Fixes 1-4: the homecoming hold, the tracker, cheaper altar, rough plank, campfire charcoal, the reachability audit, the Notice Board building | Built |
| " §3.4.1 | Raise the Walls → A Growing Home → Heavy Loads as a chain | **Unbuilt** (P16) |
| " §3.4.1 | Know Your Enemy on the board; The Smith's Request after the Lodge; the Heartroot hunt after Bjarne | Built (the hunt through The Breathing Hollow) |
| " §3.4.4 | A robust first rescue | Partly built: rescue waypoints and the stranded kept alive (11-19) |
| " §3.6.0 | Eight homecoming memories (330 words) | **Unbuilt** |
| " §3.6 | A profession line for every person | Ingrid's four at-home steps (11-19, unvoiced) and Liv's five exist. **The other nine have one quest each** |
| " §3.7.1 | Act I patches: the figure, the vardøger, the horn | Unbuilt |
| " §3.7.2 | Act II "The Doors": the man in grey, a door broken, the old troll fought or paid, Sigrid in Troll Valley, What Sigrid Knows | Two quests built as The Toll and Nobody Crosses (the Brukam). **The rest is unbuilt** |
| " §3.7.4 | Tales from the raid events | Unbuilt (1 Tale in the game) |
| " §3.8.8 | Contradictions | **Unbuilt** (P27, P30) |
| " §3.9 | Objective types: eat a meal, take a lesson, bond rank, give an item | Unbuilt (Empower Item was added) |
| PrologueAndQuestFlow (11-18) | Cold open; The Fire at the Stones told at home | Unbuilt (the cinematic was tightened on 11-20, not moved) |
| " | Eirik's spar mandatory; the morning errands as one choice | Unbuilt (still optional; still a chain) |
| " | The shelter's ember lights the Haven's hearth | **Unbuilt** (P22) |
| " | Each rescue its own situation | Partly: each rumour has its own site and set piece |
| SystemsGapReview (11-19) §10-11 | Worker ranks by milestones with a Deed quest; Bond Tales | Unbuilt (they fit as step 3-4 of the chains in §5.3) |

---

## 4. How comparable games do it

### 4.1 What they do

| Game | How a person or a station is introduced | What we take |
|---|---|---|
| **Enshrouded** | The first survivor quest starts as soon as the player builds a base. Once the Blacksmith is summoned, two quests open at once: **"Forge a Weapon"** (do the job once) and **"The Blacksmith Needs A Shop"** (give him a place). Later steps send the player to fetch a real object from a real place, and the object is the upgrade. "Crucible Needed For A Smelter" unlocks the Smelter, and "The Blacksmith's Request" gives Smithing Tools. [gamerguides][e1], [wiki][e2], [allthings.how][e3] | Rescue, then at once: use the trade once, then build the place. Station upgrades are finds, not lists |
| **Stardew Valley** | **"Forging Ahead"**: Clint comes by the morning after the player first holds copper ore and gives the Furnace blueprint. **"Smelting"** follows: smelt one bar. Willy's rod arrives by letter on day 2. Most story quests come by mail, and a few start with the game to teach it. [Tom's Hardware][s1], [Tom's Hardware][s2], [search summary of the wiki][s3] | The problem first (holding the ore), then the person, then **use it once**. One introduction per day |
| **Animal Crossing: New Horizons** | Give Tom Nook 5 fish or bugs, and Blathers' tent goes up the next day. 15 donations later the museum opens, also the next day. [GameSpot][a1], [Nintendo Life][a2] | The person asks a small thing in their own trade. The building follows, one at a time, with a night between |
| **V Rising** | A station comes from the boss whose camp is its trade: Keely, from the trappers' camp, gives the Tannery; Rufus the Foreman, from the logging camp, the Woodworking Bench. [TechRaptor][v1], [GamesRadar][v2] | The unlock is visibly tied to a person and a place, never to a list |
| **Valheim** | Picking up a material shows the recipes it is used in, and the first hammer opens the workbench. Hugin the raven appears on new content, says one thing and leaves. [allthings.how][h1], [PC Gamer][h2] | Show a building when its material or person first arrives. One contextual line, once |
| **Palworld** | A tech tree: points from levels, fast-travel points and books, and rarer Ancient Technology points from bosses. [wiki][p1], [wiki][p2] | The opposite model: choice without a story, and many unlocks per level. Safe Haven's people-gating is closer to Enshrouded, and is the stronger fit for the theme |
| **Medieval Dynasty** | Technology points are earned by doing the activity (building, survival, farming), and buildings unlock when a category fills. Chapter quests work as the tutorial and pay the reputation that gates recruiting. [JustPlayBetter, low reliability][m1], [Tesseract][m2], [TechRaptor][m3] | An unlock follows practice. The main chapters teach |
| **Dragon Quest Builders 2** | Furrowfield: Rosie's first ask is a scarecrow. Once a field is set up, the villagers farm it, and more join as the harvest proves itself. [Siliconera][d1], [dragon-quest.org][d2] | The player does the job by hand once, then the person takes it over |
| **Ni no Kuni II** | Citizens are recruited through side quests ("a major chunk of the available sidequests") and staff facilities, which opens research. [Newsweek][n1], [RPG Site][n2] | Every recruit is a quest, and every quest staffs a building |
| **RuneScape: Dragonwilds** | 9 primary and 29 secondary quests (as of 10 Nov 2026). The secondary ones "teach mechanics or unlock recipes", and some wait for their region's primary quest. Four short tutorial quests come first. [Dragonwilds wiki][r1] | Few main quests. The mechanics live in the side lines, gated so they don't flood |
| **Sons of the Forest** | Kelvin is found early and taken orders through a notepad, taught when you meet him. [Pindrop][k1] | A companion comes with the way to use them, explained at the meeting |
| **Super Mario 3D World** (kishōtenketsu) | Each mechanic is introduced safely, developed, twisted, then concluded. [Engadget][m4], [Kotaku][m5] | A person's chain: introduce (lesson and station), develop (use it on a raid), twist (a personal beat), conclude (a capstone) |
| Quest floods | A "quest bush" makes each quest less valuable. Fallout 4's endless settlement asks became a meme. (From `QuestAndStoryResearch.md` §2.5: [howtomakeanrpg][f1], [Kotaku][f2]) | A tree, not a bush. The person's own steps are opt-in |

Going Medieval, Myth of Empires and Nightingale were left out: nothing found showed them tying stations to people or quests.

### 4.2 The lessons for Safe Haven

1. **A rescue is a promise kept the same evening.** In Enshrouded, Stardew and Animal Crossing, the person's first ask comes on
   arrival and is about their own trade. Today Safe Haven delays it for buildings and for the hold.
2. **The first ask is "watch me, then give me a place".** Do the verb once, then build the station: Forge a Weapon, then The
   Blacksmith Needs A Shop; Forging Ahead, then Smelting. Ingrid's first quest should be her lesson, then the altar. The Workshop
   comes in only because the altar needs squared stone.
3. **One new building per beat.** No comparable game hands over two or three stations with one recruit. The second station of a
   two-trade person (Dock, Tailor's Shop, Yard, Cauldron, Stone Wall) arrives with a later step of theirs.
4. **Upgrades are things fetched from places tied to the person.** Enshrouded's Crucible and V Rising's camps show this. Safe Haven
   already has raid set pieces, Quest Targets and Haven place verbs for it: the Old Quarry, carved stones, offering stones.
5. **Gates are visible and personal.** "Needs Bjarne home" reads better than a greyed button that says "needs Woodcutter's Lodge"
   from minute one.
6. **The main chapters teach the base sparingly, and the people teach the rest.** Dragonwilds' split fits the user's rule that main
   quests are events.

---

## 5. Recommendations

### 5.1 Rules for the line

These keep the standing rules: main quests are events; Haven quests are held, at most 2 new per return; one quest per colour on the
tracker; never ask before teaching; the Notice Board brings fetch and craft quests later; a dark tone; names left unnamed.

1. **A person's first quest starts on rescue, never held, and its only prerequisite is the rescue quest** (the new rule). A building
   it needs that may not stand yet is an earlier step in it, with Count Already Owned on, so it ticks the moment the quest starts if
   already built.
2. **Its first objective is "Talk to <person>"**, and the person's quest-line offer text is the lesson: who they are, what they do
   for you, and what to build. It is said in their own voice, never second-hand.
3. **One building per quest, learned when that quest starts.** The rescue itself teaches nothing. Giving the job still teaches it,
   for a co-op friend who wasn't there.
4. **Later steps are given by the person** (quest line, "!"), one at a time, and opt-in. They never flood the tracker, because an
   offer is not an active quest.
5. **The portal teaches the commons** (tools, Workshop, chest, the portal's tiers, the board). **The people teach their trades.** The
   portal's quests stay held and never compete with a person's first quest for the green slot.
6. **Every chain follows kishōtenketsu.** Introduce: the lesson and the station. Develop: use it, or a raid step. Twist: a personal
   beat, after the player likes them. Conclude: something of their own.

### 5.2 The proposed order

Times are estimates from today's run. Rescues are one per raid.

| Step | When | Quest (kind) | NPC / giver | What it teaches | What it unlocks | Change |
|---|---|---|---|---|---|---|
| 1-7 | 0-11 min | Prologue: 7 Main, 4 Errands | Sigrid, Eirik, Runa | Moving, fighting, gathering, the fire, defending, the stones | The weapon kept | Keep. Later: the parry lesson mandatory (11-18) |
| 8 | Home, 11 | The One It Caught (Main) | Portal | The portal, the forgetting | – | Keep |
| 9 | 12 | A Light in the Wood (Main) | Portal | The build menu at home | – | Later: the shelter's ember (P22) |
| 10 | 13 | The Rootbound Stones (Main) | Portal | Life Orbs, **and that more stones in the meadows hold them** | – | Add one portal line naming the meadow stones (S) |
| 11 | 14 | The Door Wakes (Main) | Portal | – | Raids | Keep |
| 12 | 15 | Tools of the Clearing (Haven) | Portal | Crafting by hand | – | Keep |
| 13 | 15 | Into the Misty Woods (Main: extract) + Bring Ingrid Home (Rescue: find, bring) | Portal | Raids; worn vs carried | – | Remove the duplicate "Find Ingrid" from the Main (P18) |
| 14 | Raid 1 | Echoes in the Wilds (Tale) | – | Events | – | Keep |
| 15 | Home 1 | **Ingrid 1: The Alchemist** (starts on rescue) | **Ingrid** | Her trade; the Workshop squares stone; Life Orbs in the meadows | **Purification Altar** (Workshop already known) | Restructured (§5.3) |
| 16 | Home 1 | The Weakening Ward (Main) | Portal | Breaches | – | Keep |
| 16 | Home 1 | The Smith in the Woods (Rescue) | – | The next rescue | – | Keep |
| 17 | Home 1 | A Roof for the Work (Haven, held) | Portal | The Workshop | – | Keep. It ticks when Ingrid's quest builds it |
| 18 | Home 1-2 | Cut Stone (Haven, **not held**) | Portal | A walk out; pre-cut blocks | – | Start without waiting; chain "Ingrid" |
| 19 | after the Workshop | A Safe Place → Something to Fight With (Haven, a chain) | Portal | The chest; a second weapon | – | Chain them (one portal quest at a time) |
| 20 | after the altar | **Ingrid 2: Cleansing the Curse** | Ingrid | Purifying | – | Re-rank to Haven |
| 21 | after Ingrid 2 and the Smithy | **Steady the Door** (Haven, new) | Portal | **Raising the portal**: tier II opens tier II raids | Tier II | New (§5.4) |
| 22 | Home 2 | **Bjarne 1: A Smith's Hearth** (on rescue) | **Bjarne** | His trade; repairs | **Smithy** | Restructured |
| 23 | Home 2 | The Breathing Hollow (Main) | Portal | – | – | Keep |
| 24 | after the Smithy | **Bjarne 2: First Copper** (moved from the board) | Bjarne | Smelting; charcoal by the fire | – | The Smith's Request, split |
| 25 | Home 3 | **Tor 1: The Woodcutter's Lodge** (on rescue) | **Tor** | Planks, charcoal | **Woodcutter's Lodge** | Restructured |
| 26 | Home 3 | What Breathes Under the Roots (Main) | Portal | The ember; the boss | – | Keep |
| 27 | after the Lodge | **Tor 2: Raise the Walls → Tor 3: A Growing Home → Tor 4: Heavy Loads** | Tor | Walls, storage, beds, carts | **Wainwright's Yard** at Tor 4 | Chain them; give them through Tor |
| 28 | Raid 3-4 | The Heartroot; **The Road North** (Main) | Portal | The twist | Troll Valley | Keep; the horn moves here (P23) |
| 29 | after The Road North | The Cold Stones (Haven's Reach) | Portal | Territory | – | Fix its opening line (P24) |
| 30 | Home 4 | **Eirik 1: The Watch** (on rescue) | **Eirik** | Wardens, breaches | **Watch Post** | Restructured |
| 31 | Homestead rank | **Word at the Fire** (Haven, new) | Portal | The Notice Board: errands and asks | Notice Board (its rank already gates it) | New (§5.4) |
| 32 | after Eirik 1 | **Eirik 2: Hold the Line** | Eirik | Spike traps | – | New |
| 33 | Home 5 | **Yrsa 1: Show Me Your Kitchen** (on rescue) | **Yrsa** | Meals that last | **Kitchen** | Restructured |
| 34 | Raid 5+ | The Toll → Nobody Crosses (Main) | Portal | Troll Valley; the Brukam | Moss Marsh | Keep; tie the old troll in (P25) |
| 35 | Home 6 | **Gunnar 1: Hides and Hooks** (on rescue) | **Gunnar** | Hunting at home, curing | **Hunter's Lodge** | Restructured |
| 36 | after Gunnar 1 | **Gunnar 2: Never Say Its Name** | Gunnar | The Dock, still water | **Dock** | New |
| 37 | Home 7 | **Liv 1: Runes for the Road** (on rescue) | **Liv** | Raid runes at the Cauldron | (runes) | Restructured |
| 38 | after Liv 1 | Liv's five (The Old Marks → The Great Work) | Liv | Enchanting | – | Keep; re-explain The Old Marks (P21) |
| 39 | Home 8 | **Asa 1: Good Dirt** (on rescue) | **Asa** | Crops that grow while you raid | **Farm Plot** | Restructured |
| 40 | after Asa 1 | **Asa 2: Flax and Linen** | Asa | Linen; why iron needs it | **Tailor's Shop** | New |
| 41 | Home 9 | **Runa 1: Eyes in the Woods** (on rescue) | **Runa** | The extra rumour | – | Restructured |
| 42 | Home 10 | **Hild 1: Stone on Stone** (on rescue) | **Hild** | Building in stone | **Cottage** | Restructured |
| 43 | after Hild 1 | **Hild 2: Granite** | Hild | Walls that don't burn | **Stone Wall, Stone Wall Gate** | New |
| 44 | Home 11 | **Sigrid 1: Under One Banner** (on rescue) | **Sigrid** | Leading: more time per raid | **Haven Banner** | Restructured |

**Starts per homecoming with this order:**
- the person's first quest (1, opt-out of the hold);
- at most 1 Main and 1 Rescue;
- at most 2 held portal quests.

After raid 1 the tracker shows gold (The Weakening Ward), teal (Bjarne) and green (**Ingrid**).

### 5.3 A chain for every rescued person

Each first quest starts on rescue and opens on Talk To. Each building is learned when its quest starts (§5.4, item C2). "Existing"
means the asset is there; "new" means a new `QuestDefinition`. Lines are short (15-30 words) and unvoiced at first, then go through
Safe Haven › Voice Lines.

**Ingrid, Alchemist: the altar first, the Cauldron later**

| # | Quest | Existing/new | Objectives (in order) | Learns at start | Effort |
|---|---|---|---|---|---|
| 1 | The Alchemist | Existing, restructured | Talk To Ingrid ("what I can do for you"); Assign Job Alchemist [owned]; Build Workshop [owned]; Gather Life Orb ×4 [owned] with a marker on the nearest meadow stone; Build Purification Altar [owned] | Purification Altar | S data + M for the orb marker (a Quest Target at a meadow stone, Haven place verbs) |
| 2 | Cleansing the Curse | Existing; rank Tale → **Haven** | As now; its end line names the portal: "feed the clean essence to the door" | – | S |
| 3 | A Draught for the Road | **New** | Build Cauldron; Craft Healing Draught ×3 | **Cauldron** | M (asset, 3 lines). Check a Life Mote source is reachable by then |
| 4 | What the Steel Remembers | Existing; rank → Haven | Empower | – | S |
| (5) | Marks of the Old Ways | Existing; rank → Haven | Keep as the first taste of safe marks. Liv's The Old Marks is re-explained as "where new marks come from" (P21) | – | S text |

**Bjarne, Smith: the hearth, then copper**

| # | Quest | Existing/new | Objectives | Learns at start | Effort |
|---|---|---|---|---|---|
| 1 | A Smith's Hearth | Existing, restructured | Talk To Bjarne; Assign Job Smith [owned]; Build Workshop [owned]; Build Smithy [owned]; Repair Item ×1 | Smithy | S data, 3 lines |
| 2 | First Copper | The Smith's Request, split and moved to Bjarne's quest line | Craft Copper Ingot ×3 (its text: charcoal smoulders out of oak at the campfire) | – | S data; edit `NoticeBoard.prefab`'s giver; 3 lines |
| 3 | Copper Fire | The Smith's Request's other half | Upgrade Smithy to tier II; Craft Copper Pickaxe | – | S. It also sets up the portal's tier III |
| 4 | The Iron on the Road | New (11-15 §3.6.2) | Collect Grave Iron ×3; Use Object at an Iron Marker in a raid set piece; Talk To Bjarne | – | L (set piece) |

The Notice Board keeps Know Your Enemy and the Haven's asks. The Smith's Request's note "in the last smith's hand" becomes Bjarne's
line: "I found this in the old hearth stones."

**Tor, Carpenter: the lodge, then walls, roofs and carts**

| # | Quest | Existing/new | Objectives | Learns at start | Effort |
|---|---|---|---|---|---|
| 1 | The Woodcutter's Lodge | Existing, restructured | Talk To Tor; Assign Job [owned]; Build Workshop [owned]; Build Lodge [owned]; Craft Fine Plank ×8 at the Lodge (Lodge II and the Yard want them) | Woodcutter's Lodge | S, 3 lines |
| 2 | Raise the Walls | Existing; prerequisite the Lodge quest; given by Tor | 4 Palisades | – | S |
| 3 | A Growing Home | Existing; prerequisite Raise the Walls; given by Tor | Storage Shed, Hut | – | S |
| 4 | Heavy Loads | Existing; prerequisite A Growing Home; given by Tor | Lodge II, Yard, Wheelbarrow | **Wainwright's Yard** | S |

**Eirik, Warden: the watch, then a plan for the breach**

| # | Quest | Existing/new | Objectives | Learns at start | Effort |
|---|---|---|---|---|---|
| 1 | The Watch | Existing, restructured | Talk To Eirik; Assign Job [owned]; Build Lodge [owned]; Build Watch Post [owned] | Watch Post | S, 3 lines |
| 2 | Hold the Line | New | Build Spike Trap ×3; Defend Breach (any) | – (Spike Trap is known; this is its first mention) | M, 2 lines |
| 3 | Her Name in Stone | New (11-15 §3.6.4, personal) | Talk To Eirik; Build Rune Stone (needs Workshop II) | – | M, 3 lines |

**Yrsa, Cook: the Kitchen, then food as armour**

| # | Quest | Existing/new | Objectives | Learns at start | Effort |
|---|---|---|---|---|---|
| 1 | Show Me Your Kitchen | Existing, restructured | Talk To Yrsa; Assign Job [owned]; Build Smithy [owned] (its text: "Bjarne's fire, for the pots"); Build Kitchen [owned]. The text names 12 Rough Stone Blocks and 4 Copper Ingots | Kitchen | S, 3 lines |
| 2 | What You Eat | New | Craft a Kitchen meal; eat it before a raid (a new Consume objective, code S; or Craft only for now) | – | M |
| 3 | The Jarl's Table | New (11-15 §3.6.5), after Asa is home | Forage cloudberries and chanterelles on a raid; plant them | – | M |

The Dock moves to Gunnar's line, though Yrsa can still be a Fisher once it stands.

**Gunnar, Hunter and Fisher: the lodge, then the water**

| # | Quest | Existing/new | Objectives | Learns at start | Effort |
|---|---|---|---|---|---|
| 1 | Hides and Hooks | Existing, restructured | Talk To Gunnar; Assign Job [owned]; Gather Rough Hide ×4 (the boars outside the light); Build Hunter's Lodge [owned] | Hunter's Lodge | S, 3 lines |
| 2 | Cured | New | Cure leather ×4 at the Hunter's Lodge | – | S-M |
| 3 | Never Say Its Name | New (11-15 §3.6.6) | Build Dock; Catch Fish ×1 in still water | **Dock** | M |

**Liv, Rune Scholar: runes, then marks**

| # | Quest | Existing/new | Objectives | Learns at start | Effort |
|---|---|---|---|---|---|
| 1 | Runes for the Road | Existing, restructured | Talk To Liv; Assign Job [owned]; Build Cauldron [owned] (Ingrid's); Craft one raid rune | (rune recipes, as now) | S, 3 lines; fixes P11 |
| 2-6 | The Old Marks → Rubbings → Marks of the Marked → A Hand for the Shelf → The Great Work | Existing | The Old Marks adds "Build Purification Altar [owned]" first (P12) and is re-worded (P21) | – | S |

**Asa, Farmer and Weaver: the field, then linen**

| # | Quest | Existing/new | Objectives | Learns at start | Effort |
|---|---|---|---|---|---|
| 1 | Good Dirt | Existing, restructured | Talk To Asa; Assign Job Farmer [owned]; Build Workshop [owned]; Build Farm Plot [owned] | Farm Plot | S, 3 lines |
| 2 | Flax and Linen | New | Build Hunter's Lodge [owned]; Build Tailor's Shop; Craft Linen ×4; Assign Job Weaver | **Tailor's Shop** | M; fixes P15 |
| 3 | Who's Been Using It? | New (11-15 §3.6.8), the Haven world | Reach the Burnt Farmstead (a place); Use Object there | – | M |

**Runa, Tracker: the extra rumour, then what she was paid for**

| # | Quest | Existing/new | Objectives | Learns at start | Effort |
|---|---|---|---|---|---|
| 1 | Eyes in the Woods | Existing, restructured | Talk To Runa; Assign Job [owned]; Complete Raids ×1 [extract] (she names the extra rumour in the briefing) | – | S, 3 lines |
| 2 | Read the Ground | New | Use Object at the Night Cairn (existing rumour site); extract | – | M |
| 3 | Someone Is Paying | New (the man in grey) | Talk To Runa; Reach the Cold Camp in a raid | – | M-L |

**Hild, Mason: the cottage, then stone walls**

| # | Quest | Existing/new | Objectives | Learns at start | Effort |
|---|---|---|---|---|---|
| 1 | Stone on Stone | Existing, restructured | Talk To Hild; Assign Job [owned]; Build Smithy [owned]; Upgrade Workshop to tier II [owned]; Build Cottage [owned] | Cottage | S, 3 lines |
| 2 | Granite | New | Craft Granite Block ×4; Upgrade Workshop to tier III; Build Stone Wall ×4 | **Stone Wall, Stone Wall Gate** | M |
| 3 | The Sleeping Quarry | New (11-15 §3.6.10) | Reach the Old Quarry (the existing Quest Target Cut Stone uses); Use Object at the sun-caught troll | – | M |

**Sigrid, Leader: the banner, then everyone's place**

| # | Quest | Existing/new | Objectives | Learns at start | Effort |
|---|---|---|---|---|---|
| 1 | Under One Banner | Existing, restructured | Talk To Sigrid; Assign Job [owned]; Build Haven Banner [owned] | Haven Banner | S, 3 lines |
| 2 | Seven Went In | New | Talk To Ingrid, Bjarne, Tor, Liv, Eirik, Runa | – | S-M |
| 3 | What Sigrid Knows | New Main quest (the end of Act II, 11-15 §3.7.2) | Talk To Sigrid; the portal's scene | – | L |

### 5.4 Merge, move, re-explain, add or cut

| # | What | Kind | Effort | Data or content |
|---|---|---|---|---|
| A1 | `TheAlchemist`: Count Already Owned on both objectives (P2) | Fix | S | Data |
| A2 | Every first job quest: prerequisites = its rescue quest only. Building prerequisites become ordered [owned] steps (§5.3). Objectives in order on | Re-order | S | Data |
| A3 | Every first job quest: a "Talk To <person>" first step | Add | S | Data |
| A4 | Ten first quests into each person's `ResidentDefinition › Quest Line`, with offer, accept and waiting text | Move | M | Data + content (about 30 short lines) |
| A5 | Cleansing the Curse, What the Steel Remembers, Marks of the Old Ways: rank Tale → Haven; chain "Ingrid" | Re-rank | S | Data |
| A6 | Cut Stone: Starts Without Waiting; chain "Ingrid" | Re-time | S | Data |
| A7 | Raise the Walls → A Growing Home → Heavy Loads as a chain, given by Tor (P16) | Re-chain | S | Data + 3 lines |
| A8 | A Safe Place → Something to Fight With as a chain | Re-chain | S | Data |
| A9 | The Smith's Request: split into First Copper and Copper Fire, moved to Bjarne's quest line, Lodge prerequisite dropped (campfire charcoal exists) | Move | S-M | Data + `NoticeBoard.prefab` + 3 lines |
| A10 | Into the Misty Woods: drop the duplicate "Find Ingrid" objective (keep the extraction) (P18). **User's call.** | Merge | S | Data |
| A11 | The Rootbound Stones' end, or the door: one portal line that more meadow stones hold orbs | Re-explain | S | Content (1 line) |
| A12 | Fix the texts: Runes for the Road (the Cauldron is Ingrid's), DefendCamp (no Ingrid), RotsHeart ("Runa says"), the "children" in two asks, The Old Marks ("Ingrid showed you the safe marks; I'll show you where new ones come from"), Something to Fight With (the club as a stun tool, not a spare) | Re-explain | S | Text |
| A13 | Sigrid's horn: from A Light They Can See to the end of The Road North. A Light They Can See keeps a new portal line (P23) | Move | S | Scene data + 1 line |
| A14 | The Cold Stones: rewrite its opening line for The Road North (P24) | Re-explain | S | Text + voice |
| A15 | **Steady the Door**: a Haven quest from the portal, after Cleansing the Curse and the Smithy, to raise the portal to tier II. Needs a portal-tier objective (a new type, or a Story Event `PortalTiers` raises) | Add | M | Code S + data + 2 lines |
| A16 | **Word at the Fire**: a Haven quest from the portal when the Homestead rank is reached: build the Notice Board. Know Your Enemy and the asks follow | Add | S-M | Data + 2 lines |
| A17 | **A Draught for the Road** (Ingrid 3), **Hold the Line** (Eirik 2), **Never Say Its Name** (Gunnar 3), **Flax and Linen** (Asa 2), **Granite** (Hild 2): the buildings no quest introduces | Add | M each | Data + content |
| A18 | Tie the Brukam to the old troll, or say he isn't (one line in Nobody Crosses' portal whisper or its defeat line) (P25) | Re-explain | S | Text |
| A19 | Eight homecoming memories (11-15 §3.6.0, already written) | Add | S | Data + 8 lines |
| A20 | A Light in the Wood → the shelter's ember (11-18 §4.2) | Re-explain | M | Content |
| A21 | Act II after the Brukam: a Moss Marsh quest, the man in grey, Sigrid in Troll Valley (11-15 §3.7.2) | Add | L | Content |
| A22 | The Stable: a short Tale when the first wild horse is met (When Player Does). Optional | Add | S-M | Data + 2 lines |
| A23 | Update GDD §15.3 and `RescueOrder.md` row 7 (P30) | Docs | S | Text |
| – | **Cut nothing.** Know Your Enemy stays as the board's first errand, Echoes in the Wilds stays, Cut Stone stays as the shortcut to blocks | – | – | – |

**Code that the line needs (all small):**

| # | Change | Why |
|---|---|---|
| C1 | The new rule: a resident's first quest starts on rescue and is never held (being built) | P3 |
| C2 | `QuestDefinition › Unlocks Buildings`, learned when the quest starts. `RescuableTraveler` stops teaching job buildings. `GiveJob` still teaches them (co-op). `QuestReachability.AskedBeforeTaught` counts a quest's own start as teaching | P6, rule 3 |
| C3 | Tracker: within a colour, a quest given by a person first, then the newest. Never alphabetical | P7 |
| C4 | The hold: "one more when none of its kind is under way" should count only quests started this homecoming, so one stale quest can't block the queue for a raid | P8 |
| C5 | A portal-tier objective (or a Story Event raised by `PortalTiers` on a raise) | A15 |
| C6 | Later: a Consume Item objective (Yrsa 2), as 11-15 §3.9 proposed | §5.3 |

### 5.5 Pure data changes that can be done right away

These need no new dialogue, assets or code. Each was checked against the current asset values.

1. **`TheAlchemist.asset`**: `countAlreadyOwned: 1` on both objectives (A1). This is the bug that can strand Ingrid's quest today.
2. **Prerequisites cut to the rescue quest only**, with the missing building as an ordered [owned] step (A2). Objectives In Order is
   on in each:

   | Quest | Prerequisite dropped | Step added |
   |---|---|---|
   | `TheAlchemist` | `ARoofForTheWork` | Build Workshop |
   | `TheSmith`, `WoodcuttersLodge`, `TheHunter`, `TheFarmer`, `TheLeader` | `ARoofForTheWork` | Build Workshop |
   | `TheWarden` | `WoodcuttersLodge` | Build Woodcutter's Lodge |
   | `TheCook`, `TheMason` | `TheSmith` | Build Smithy |

3. **A Talk To <person> first step** on those quests and on `TheRuneScholar` and `TheTracker` (A3). It uses the existing Talk To deed,
   so there is no new event.
4. **Ingrid's follow-ups re-ranked** from Tale to Haven, with chain "Ingrid" (A5).
5. **`CutStone.asset`**: `startsWithoutWaiting: 1` and a chain name (A6).
6. **Tor's three as a chain** (A7, prerequisites only): Raise the Walls ← The Woodcutter's Lodge; A Growing Home ← Raise the Walls;
   Heavy Loads ← A Growing Home.
7. **A Safe Place → Something to Fight With** (A8): Something to Fight With's prerequisite becomes `SafeKeeping`.
8. **The Smith's Request**: drop the `WoodcuttersLodge` prerequisite. Nothing in it needs the Lodge any more: a Copper Ingot is 3
   ore and 2 Charcoal (charcoal at the campfire), and the Copper Pickaxe is 3 ingots, 2 Rough Plank (Workshop) and 1 Nettle Cord. The
   move to Bjarne is A9 and needs his lines.
9. **`TheOldMarks.asset`**: a Build Purification Altar [owned] step first, so Liv's first lesson says what it needs (P12).
10. **Text-only fixes** in A12 (`DefendCamp`, `RotsHeart`, `TheRuneScholar`, the two asks with "children").
11. **Sigrid's horn** moved to The Road North (A13). It is a scene story trigger change in `SafeHaven.unity`. The horn line exists.

Expect these tests to change with them: `IngridQuestLineTests`, `RescueOrderTests` (job quests' prerequisites),
`QuestCategoryTests`, `QuestAuditTests` (AskedBeforeTaught), `QuestPacingTests` and `HavenProgressionTests`. Run them as
targeted classes.

### 5.6 How to know it worked

- **In the playtest log:** each `PersonRescued` is followed by that person's first `QuestStarted` within the homecoming's first 10
  seconds. No `JobAssigned` comes before its quest. No held quest waits more than one homecoming.
- **A flood test on the real assets** (`QuestFloodTests`, proposed 11-15): New Game to raid 4. It asserts at most 1 person's quest,
  1 Main, 1 Rescue and 2 portal quests per homecoming, and that every building a quest asks for was learned at or before that quest's
  start.
- **A new audit**: every building that isn't a decoration and isn't known from the start is learned at the start of exactly one
  quest, or from a raid find. Today all 15 that people teach fail it, because a rescue teaches them before any quest. A second
  check: every building that isn't a decoration is named by at least one quest. Today the 12 in §0 point 6 fail it.
- **Play it from New Game to Bjarne's homecoming** once by hand. Watch three things: the "!" over Ingrid on arrival, the green slot
  showing her quest, and the altar standing before raid 2.

---

[e1]: https://www.gamerguides.com/enshrouded/guide/quests/blacksmith/all-blacksmith-quests-in-enshrouded
[e2]: https://enshrouded.wiki.gg/wiki/Survivors
[e3]: https://allthings.how/enshrouded-how-to-awaken-the-survivor-and-summon-oswald-anders/
[s1]: https://forums.tomshardware.com/faq/stardew-valley-%E2%80%9Cforging-ahead%E2%80%9D-story-quest-guide.3339371
[s2]: https://forums.tomshardware.com/faq/stardew-valley-%E2%80%9Csmelting%E2%80%9D-story-quest-guide.3336572
[s3]: https://stardewvalleywiki.com/Quests
[a1]: https://www.gamespot.com/articles/animal-crossing-new-horizons-museum-unlock-guide/1100-6474994/
[a2]: https://www.nintendolife.com/guides/animal-crossing-new-horizons-blathers-how-to-unlock-the-museum
[v1]: https://techraptor.net/gaming/guides/v-rising-crafting-and-unlocks-guide
[v2]: https://www.gamesradar.com/v-rising-boss-locations-map-rewards-blood-carriers
[h1]: https://allthings.how/valheim-how-to-survive-your-first-day-in-the-meadows/
[h2]: https://www.pcgamer.com/au/valheim-guide-tips
[p1]: https://palworld.wiki.gg/wiki/Technology_Points
[p2]: https://palworld.wiki.gg/wiki/Ancient_Technology_Points
[m1]: https://justplaybetter-web.pages.dev/medieval-dynasty/progression/skills-and-technology
[m2]: https://tesseract.academy/how-to-get-production-technology-points-fast-in-medieval-dynasty-beginners-guide/
[m3]: https://techraptor.net/gaming/guides/medieval-dynasty-starter-guide
[d1]: https://www.siliconera.com/dragon-quest-builders-2-farming-has-a-communal-story-of-seasons-vibe/
[d2]: https://dragon-quest.org/wiki/Furrowfield
[n1]: https://www.newsweek.com/entertainment/video-games/ni-no-kuni-ii-2-kingdom-building-guide-research-upgrades-revenant-king-evan-857979
[n2]: https://www.rpgsite.net/feature/6960-ni-no-kuni-ii-kingdom-building-management-guide-how-to-create-the-best-kingdom
[r1]: https://dragonwilds.runescape.wiki/w/Quests
[k1]: https://pindrop.gg/sons-of-the-forest/guides/how-to-command-kelvin
[m4]: https://www.engadget.com/2015/03/17/super-mario-3d-world-design/
[m5]: https://kotaku.com/what-made-super-mario-3d-world-so-great-1691916400
[f1]: https://howtomakeanrpg.com/r/a/avoid-quest-bushes.html
[f2]: https://kotaku.com/the-internet-loves-to-make-fun-of-fallout-4s-preston-ga-1751573683
