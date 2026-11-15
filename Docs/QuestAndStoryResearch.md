# Safe Haven quest and story research: the flood after the first raid, the stone-block gap, a line for every person, and the lore

Research date: 2026-11-15. This was read-only: no game code, data, scenes or assets were changed, and neither the Unity Editor nor the
`unity` CLI was used. Paths are relative to `d:\Safe Haven\Safe Haven` unless absolute. `file:line` points at the `test` branch as of
commit c89605f2. Quest assets are in `Assets/_Project/Data/Quests/` (home) and `Assets/_Project/Data/Prologue/Quests/` (prologue)
unless another folder is named.

The user's request: "After the first raid, we get overwhelmed with a lot of quests, and after saving the first NPC, we need to build a
building which requires stone blocks, but the player is not taught how to make them. So check the order in which quests are added and
triggered. Do they make sense? Suggest a better order for the quest lines. Also, add quest lines to all NPCs we rescue, where they teach
us about the mechanic of their profession and how they can help us, in the style that the runemaster NPC now does. Look at other story
progression quests we can add. Check the story so far, what is missing? Create and deepen the lore. Research how other games create
engaging story lines and how quests and story lines from NPCs can make the player have positive feelings for them."

"Add quest lines" is answered here as designs (§3.6). Nothing is built.

---

## 0. Summary

### What the data shows

1. **The flood is real, and it is measured.** In the user's own run on 2026-11-15 (`%USERPROFILE%\AppData\LocalLow\DefaultCompany\Safe Haven\Playtests\20261115-184337.csv`):
   - **9 quests started in the 11.4 minutes after the first extraction** (minute 24.35 to 35.77), three of them in the same second.
   - **6 quests were active at once** at minutes 30.9, 34.8 and 35.8.
   - The causes:
     - Two completions at extraction (Into the Misty Woods and Bring Ingrid Home) each unlock several quests that all start by themselves.
     - There is no limit on active quests. `QuestManager.StartWaitingQuests` (`Scripts/Quests/QuestManager.cs:109-126`) starts every ready quest in the same frame. `StoryPacing.ReadyAt` (`Scripts/Quests/StoryPacing.cs`) only spaces a quest 3 s after the *last completion* (`Resources/StoryPacingSettings.asset:15`), not after other starts.
     - The Haven quests are a bush, not a tree. Tools of the Clearing opens Strength for the Road and A Roof for the Work together. A Roof for the Work opens A Safe Place, Something to Fight With and The Smith's Request together. Tor's lodge later opens three quests at once.
     - Two Main Story quests start before the player can act on them:
       - The Weakening Ward begins on extraction.
       - What Breathes Under the Roots began at minute 35 after one raid, but its rumour only comes up after two extractions.
     - Know Your Enemy starts by itself 90 s after the raid.
   - On top of the quests, the portal speaks one line per open quest, the briefing lists up to 3 Haven asks (`Resources/HavenRequestSettings.asset`, `atOnce: 3`), and the tracker shows 4 quests plus "+N more" (`Prefabs/UI/QuestTracker.prefab:216`).
   - A second flood is built in at the third raid. Tor's three Haven quests, The Cold Stones, What the Land Asks, Eirik's rescue and the Heartroot hunt can all be open together: up to about 8.
2. **The stone-block gap is a chain of four missing teachings, and it blocks the progression spine.**
   - **The ask.** The Alchemist (Haven, starts the instant Ingrid is home) asks for the Purification Altar.
   - **The cost.** The altar costs **16 Stone Block and 6 Life Orb** (`Data/Buildings/PurificationAltar.asset:22-27`).
   - **The recipe.** Stone Block is 4 Grey Stone → 2 at the **Workshop** (`Data/Crafting/Recipes/StoneBlock.asset:21-26`, known from the start): 8 crafts and 32 Grey Stone. Grey Stone needs the stone pickaxe.
   - **Nothing teaches it.** No quest text, journal note, portal line or build-menu hint names Stone Block anywhere in the game data. The only mention is the item itself. The Workshop quest (A Roof for the Work) is optional and opens at the same moment. The build menu shows the missing cost but not where it is made (`Scripts/UI/BuildMenuWindow.cs:462-507`).
   - **The person who should teach it comes tenth.** The job that runs the block bench is Hild's (Mason, `Data/Settlement/Mason.asset`), and she arrives tenth of eleven.
   - **In the user's run** The Alchemist never completed, and the player spoke to Ingrid six times.
   - **What it blocks.** The altar is the only source of Purified Essence. The portal's tier II needs 6 Purified Essence and a Smithy (`Resources/SettlementSettings.asset:197-209`), and tier II opens tier II raids. So the gap sits on the spine of the whole game.
3. **The same gap repeats for planks and charcoal.**
   - Oak Plank and Charcoal are made only at Tor's Woodcutter's Lodge (`Data/Crafting/Recipes/OakPlank.asset:24`, `Charcoal.asset:24`). Tor is the third rescue.
   - Bjarne, the second rescue, asks for a Smithy that needs 8 Oak Plank (`Data/Buildings/Forge.asset:23`).
   - Ingrid's Cauldron needs 8 Oak Plank.
   - Copper ingots need Charcoal, yet The Smith's Request (3 Copper Ingots and a Copper Pickaxe) is offered as soon as the Workshop stands.
   - The only early planks are Supply Crates: a 50% chance of 1-2 (`Prefabs/World/Breakables/Supply Crate.prefab:126-130`).
4. **Rescues: the chain in the data is Ingrid → Bjarne → Tor → Eirik → Yrsa → Gunnar → Liv → Asa → Runa → Hild → Sigrid.**
   - The quest prerequisites (`BringXHome.asset`) and the rumours' After Home (`Data/Raids/Rumours/*.asset`) agree. The recent rechain Gunnar → Liv → Asa → Runa → Hild is in place.
   - `Docs/GameDesignDocument.md:887` (§15.3) still lists the old order, and says the Leader gives 30 s where the data gives 120 s.
   - Every person after Tor except Liv has exactly one job quest: give the job, then build the workplace.
   - Yrsa, Gunnar, Asa and Hild have **3 voiced lines each** in the whole game. The expedition members have 26-83.
   - **8 of 11 have no homecoming memory line** (`welcomeLine` empty), though the approved redesign's rule 6 is "each person brought home remembers one thing".
5. **Liv's enchanting line is the right model, but it is thin as a person.**
   - The model: five Haven quests in a strict chain, after her job quest.
   - Each step teaches one verb with an existing objective type:
     - Talk To: her lesson;
     - Enchant Item;
     - Craft Item, Use Object and Learn Enchantment;
     - Learn Enchantment from an elite;
     - Give Inscription;
     - Enchant Item (great).
   - Each step pays with the tool for the next step (Blank Parchment), with essence, and with her worker rank, which is her mechanical growth: she carves further past the player's Runecraft. The line ends with her own enchantment, Thornguard.
   - Only one of her quests is active at a time.
   - What it lacks: a personal story beat. And her talk topics are hard-coded strings (`Scripts/Essence/EnchanterTalk.cs:16-24`) that are not in the VoiceBank, so her lesson is the one silent conversation in a voiced game.
6. **The story has a strong first act and nothing after it.**
   - Act I ends at the Heartroot and its twist: people broke the pact first.
   - After The Road North **there is no main quest**:
     - Troll Valley opens with no quest in it.
     - The old troll that broke the stones has no boss.
     - The Haven's 12 named regions and 27 named places carry no story.
   - At least seven seeds are planted with no payoff:
     - why the stones woke for the player's hand (asked twice by the portal);
     - what Sigrid knows ("It will not wake for me. It never has"; Eirik: "watch her hands");
     - who has been waking the stones ("Someone opened it on purpose");
     - what "the hunger" that got into the Heartroot is;
     - Eirik's sister;
     - the figure in Granheim mail;
     - who the four strangers are when the portal speaks of "seven".

### Recommendations, ranked by player impact per effort

Effort: S = data or a few lines, under a day; M = 1-3 days; L = a week or more. "Voice" is new spoken lines.

1. **Turn the Haven bush into a tree (data only, S, no voice).**
   - Rechain the existing quests so at most one portal Haven quest is open at a time.
   - Move Know Your Enemy to the notice board.
   - Start What Breathes Under the Roots after Bjarne's rescue, which is when its rumour is up.
   - Start Tools of the Clearing with the fire, as the approved plan said.
   - With these changes the first homecoming starts **3 quests instead of 9**.
2. **Teach Stone Block before asking for it (S, 2 lines).**
   - Ingrid's first step is a new "Squared Stone" quest: talk to her, cut 16 Stone Blocks at the Workshop.
   - The Alchemist waits for it and names the Life Orbs.
   - The build menu says "made at the Workshop" under a missing cost.
3. **Close the plank and charcoal gaps (S, data).**
   - A slow "rough plank" at the Workshop and charcoal at the campfire, so the problem is felt before Tor and Tor still answers it.
   - The Smith's Request waits for the Lodge.
4. **A reachability audit (M).** `QuestAuditTests` should fail any quest that asks for something nothing taught by that point in the chain. It would have caught all of the above.
5. **Make the first rescue robust (S-M).** In 2 of the 3 real runs that reached a raid, Ingrid was not brought home on raid 1. Everything after (Bjarne, the altar, portal tier II) waits on her.
6. **Eight homecoming memories (S, 8 lines).** One line each for the eight who have none. This is the cheapest attachment in this document.
7. **A profession line for every rescued person**, in Liv's pattern plus one personal beat each (§3.6). Ingrid, Bjarne and Tor come first, since every player meets them.
   - Effort: M per three people.
   - Voice: about 10 lines per person.
8. **A homecoming release rule (M).** At most two new self-starting quests per return home; the rest wait for the next return. The tracker keeps one quest per colour in view.
9. **Act II as a main line (L):**
   - six quests up the road north;
   - the old troll as a guardian who breaks doors, fought or paid;
   - Sigrid's rescue as its end;
   - a lore bible in GDD §4 (§3.7, §3.8).
10. **Small systems the lines need (S-M each):**
    - objective types for eating a meal, taking a lesson, raiding with a companion, reaching a bond rank and handing an item to someone;
    - job talk topics as data, in place of one C# class per person;
    - a "!" over residents with a step waiting.

A phased plan is in §3.11 and verification in §3.12. Constraints honoured:
- quest deeds, not one Quest Event per person;
- the Haven is run from H and feeds raids;
- enchantments are never called runes;
- every proposal states its voice cost;
- nothing is removed: changes are rechains, rewrites and additions;
- the systems another agent is building now (NPC window, huts with traits, house tiers, timed crafting, harvest bar) are hooked into, not duplicated.

---

## Part 1: How quests and story work today

### 1.1 The quest machine in brief

- **Kinds** (`Scripts/Quests/QuestDefinition.cs:24-36`):
  - Main Story (gold);
  - Rescue (teal);
  - Haven (green);
  - Tale (silver, `SideQuest`);
  - Errand (bronze, `Optional`).

  62 quests are in play: 50 at home and 12 in the prologue, plus 2 in the Story Demo. By kind:
  - 15 Main Story (7 in the prologue, 8 at home);
  - 11 Rescue;
  - 28 Haven;
  - 7 Errand;
  - 1 Tale (Echoes in the Wilds).
- **Starts** (`QuestDefinition.cs:6-17`):
  - Automatically when the prerequisites are done;
  - On Quest Event;
  - By Quest Giver;
  - When Player Does (any objective as the condition: talk to someone, reach a place).

  Chains are prerequisite lists.
- **Objective types** (`Scripts/Quests/QuestObjective.cs:6-54`), 22 in all:
  - Collect Item, Gather Resource, Kill Monster, Destroy Building, Craft Item, Complete Raids;
  - Story Event (one-off);
  - Build Structure, Catch Fish;
  - Talk To, Reach Place, Use Object, Rescue Person, Assign Job: the deeds, with a Resident or Quest Target as the target;
  - Defend Breach, Reach Haven Rank, Store Item, Repair Item, Upgrade Building;
  - Enchant Item, Learn Enchantment, Give Inscription.

  Every type has a co-op rule (`QuestObjective.cs:281`, `QuestCoopTests`).
- **Pacing** (`Scripts/Quests/StoryPacing.cs`; `Resources/StoryPacingSettings.asset`):
  - a self-starting quest waits 3 s after the last completion, 5 s after a fight and 1.5 s after talk;
  - it waits at most 60 s for a fight or talk to end;
  - its own Start Delay replaces the 3 s.

  **Nothing counts how many quests are active or how many just started.** All waiting quests whose moment has come start in one loop (`QuestManager.cs:109-126`).
- **Tracker:** up to 4 quests, sorted by kind and then by name, then "+N more" (`Scripts/UI/QuestTrackerView.cs:22, 100-153`). **The portal** speaks for every open quest: the story's line first, and Listen cycles through the rest (`Docs/QuestRedesign.md`, as built step 1).
- **Haven asks** are a separate system (`Scripts/Settlement/HavenRequestDefinition.cs`). Up to 3 stand at once, from residents who live at the Haven. They show in the raid briefing and in the asker's asides, and are handed in when the item is home.
- **Teaching on rescue.** `ResidentNpc.TeachWhatTheyKnow` (`Scripts/Settlement/ResidentNpc.cs:302`) teaches the workplaces of every job the person can do. Lessons in the job's skill come once between raids (`ResidentNpc.cs:185-198`, `ExperienceTracker.TakeLesson`), and raise no game event.

### 1.2 The real quest timeline, New Game to the end of the content

"When" is from the user's run of 2026-11-15 (`20261115-184337.csv`, prologue errands skipped, cheats used once in the prologue) where it got
that far. The rest is estimated:
- one rescue per raid, since every rescue rumour is urgent and waits only for the person before plus one extraction;
- raids are 12 minutes (720 s since 2026-11-14);
- about 10 minutes at home between raids.

**Prologue: The Unquiet Forest** (`Data/Prologue/ForestPrologue.asset`, nine chapters for seven Main quests)

| Quest (asset) | Kind | Starts | Asks | Rewards | Opens | When |
|---|---|---|---|---|---|---|
| A Morning by the Fire (MorningCamp) | Main | Given by Sigrid | Hear the others by the fire | – | The Road East, the errands | 0.4-2.6 min |
| Wood for the Road | Errand | Given by Eirik; lapses when the road starts | 6 Oak Wood | 4 Sticks, the hatchet kept | Anything But Porridge | optional |
| Anything But Porridge (FishForBreakfast) | Errand | Given by Eirik after the wood | Catch 2 fish | 2 Stewed Berries, the rod | Meat for the Road | optional |
| Meat for the Road (HuntForThePot) | Errand | Given by Runa after the fish | Kill the boar | 20 Crude Stone Arrows, the bow | – | optional |
| A Lesson from Eirik | Errand | Given by Eirik | Dummy 3 hits, dodge, parry, win the duel | 2 Bandages | – | optional |
| The Road East (RoadToForest) | Main | Given by Sigrid | Walk to the forest (the cart, the wolves) | – | Shelter from the Storm | 5.1-10.3 |
| Shelter from the Storm (FindShelter) | Main | Auto, +5 s | Rock shelter, 8 Sticks, 12 Small Rocks, a campfire | – | A Moment of Warmth, Berries | 10.9-12.3 |
| Berries for the Pot | Errand | Given by Eirik; lapses with the wolves | 5 Berries, stew them | 2 Bandages | – | optional |
| A Moment of Warmth (CatchBreath) | Main | Auto, +3 s | Rest and talk (Liv's name lost) | – | Eyes Between the Trees | 12.3-14.1 |
| Eyes Between the Trees (DefendCamp) | Main | Auto | Two wolf waves | – | Something Is Coming | 14.1-14.6 |
| Something Is Coming (ReachPortal) | Main | Auto, +8 s | Reach the waystones, touch the centre stone | – | The Last Connection | 14.7-15.2 |
| The Last Connection (HoldPortal) | Main | Auto | Hold 60 s | – | home, The Catch cinematic | 15.3-16.4 |

**At home: the hearth and the door**

| Quest (asset) | Kind | Starts | Asks | Rewards | Opens | When |
|---|---|---|---|---|---|---|
| The One It Caught (Awakening) | Main | On Home Entered | Talk to the portal | – | A Light in the Wood | 16.7 |
| A Light in the Wood (FirstCamp) | Main | Auto | A campfire | – | The Rootbound Stones | 17.2 |
| The Rootbound Stones | Main | Auto | 3 Life Orbs from the stones | – | The Door Wakes | 17.4 |
| The Door Wakes (AwakeningThePortal) | Main | Auto | Story Event Portal Repaired | – | Into the Misty Woods, Bring Ingrid Home, Tools of the Clearing (+30 s) | 18.5 |
| Tools of the Clearing (PrimitiveTools) | Haven | Auto, +30 s | Stone Hatchet, Stone Pickaxe (by hand) | – | Strength for the Road; A Roof for the Work (with the raid) | 20.1, done 30.8 |
| Into the Misty Woods (ThroughThePortal) | Main | Auto | Find Ingrid; extract | Norns Hourglass, Portal Spark | The Weakening Ward; Know Your Enemy (+90 s); A Roof for the Work (with the tools) | 19.7, done 24.3 |
| Bring Ingrid Home (AStrangerInTheMist) | Rescue | Auto | Find Ingrid; bring her home | – | Bring Bjarne Home, The Alchemist | 19.7, done 24.3 |
| Echoes in the Wilds | Tale | When the player reaches the Wilds Echo in a raid | Kill 3, reach the Western Beacon | Greater Healing Draught | – | raid 1 (20.8-23.4) |

**After the first raid** (all in the 11.4 minutes after extraction)

| Quest (asset) | Kind | Starts | Asks | Rewards | Opens | When |
|---|---|---|---|---|---|---|
| The Alchemist | Haven | Auto, Ingrid home | Give Ingrid the Alchemist's work; build the Purification Altar | 4 Cursed Essence | – | 24.35, **never done** |
| The Smith in the Woods (BringBjarneHome) | Rescue | Auto | Find Bjarne; bring him home | – | A Smith's Hearth, Planks Not Logs | 24.35 |
| The Weakening Ward | Main | Auto | Defend the first breach (started at the portal when ready) | – | What Breathes Under the Roots | 24.35, done 35.3 |
| Know Your Enemy | Errand | Auto, +90 s | Kill 2 Forest-Bitten Wolves | Healing Draught, 5 Hardwood | – | 26.1 |
| Strength for the Road (FoodAndWater) | Haven | Auto after the tools | 5 Berries, drink at water, Stewed Berries | Waterskin | – | 30.9 |
| A Roof for the Work | Haven | Auto after the raid and the tools | A Workshop | – | A Safe Place, Something to Fight With (+60 s), The Smith's Request (board) | 30.9, done 34.7 |
| A Safe Place (SafeKeeping) | Haven | Auto after the Workshop | Small Chest; store something | – | – | 34.8 |
| What Breathes Under the Roots | Main | Auto after the breach | Hearth Ember; kill the Heartroot | – | The Road North | **35.4, after one raid** |
| Something to Fight With (FirstWeapon) | Haven | Auto, +60 s after the Workshop | Knotted Club | – | – | 35.8 |
| The Smith's Request | Errand | Given by the notice board after the Workshop | 3 Copper Ingots, Copper Pickaxe | 2 Iron Ingots | – | offered, **not doable before Tor** |

**The rescues and their work** (one rescue per raid from raid 2; estimated)

| Rescue quest | Raid (est.) | Then (Haven, "Hands at Home") | Asks | Then |
|---|---|---|---|---|
| The Smith in the Woods (Bjarne) | 2 | A Smith's Hearth (TheSmith) | Smith job; Smithy; one repair | – |
| Planks, Not Logs (Tor) | 3 | The Woodcutter's Lodge | Carpenter job; Lodge | **Raise the Walls, A Growing Home and Heavy Loads, all at once** |
| – | – | Raise the Walls / A Growing Home / Heavy Loads | 4 Palisades / Storage Shed and Hut / Lodge II, Yard, Wheelbarrow | A Light They Can See (Hamlet rank; with the breach) → 2 Life Orbs |
| Someone Holding the Line (Eirik) | 4 | The Watch | Warden job; Watch Post | – |
| A Fire in the Woods (Yrsa) | 5 | Show Me Your Kitchen | Cook job; Kitchen (needs the Smithy) | – |
| The Hunter's Pit (Gunnar) | 6 | Hides and Hooks | Hunter job; Hunter's Lodge | – |
| The Humming Stones (Liv) | 7 | Runes for the Road (TheRuneScholar) | Rune Scholar job | Liv's five quests (§1.6) |
| Seed from the Ashes (Asa) | 8 | Good Dirt | Farmer job; Farm Plot | – |
| Marks in the Dark (Runa) | 9 | Eyes in the Woods | Tracker job | – |
| A Wall Round Nothing (Hild) | 10 | Stone on Stone | Mason job; Cottage (Workshop II) | – |
| The Horn at Dusk (Sigrid) | 11+ (rumour needs 3 extractions) | Under One Banner | Leader job; Haven Banner | – (the end of the content) |

**The first act's end and the Haven's Reach**

| Quest | Kind | Starts | Asks | Rewards | When (est.) |
|---|---|---|---|---|---|
| The Road North | Main | Auto, +4 s after the Heartroot falls | Hear the portal (the twist, a choice, the reason line) | – (Troll Valley opens on the kill) | raid 3-4 |
| The Cold Stones | Haven | The portal's story after 3 raids | Hear the portal; light a boundary stone | Pact Rune | after raid 3 |
| What the Land Asks | Haven | Auto after The Cold Stones | A held land's task; offer a Pact Rune; make a land Secure | Pact Rune | after raid 3 |
| The Forest Answers | Haven | On Land Forest Gathers | Answer the push | Pact Rune | later |

After The Road North, Sigrid's rescue and Under One Banner, **no quest follows.**

### 1.3 How many quests are active, and why it overwhelms

Measured, run of 2026-11-15 (QuestStarted and QuestCompleted rows, Haven and raid scenes only):

| Minute | Event | Active |
|---|---|---|
| 19.7 | The door wakes: Into the Misty Woods + Bring Ingrid Home | 2 |
| 20.1 | Tools of the Clearing | 3 |
| 20.8 | Echoes in the Wilds (raid 1) | 4 |
| 24.30 | Extracted with Ingrid: two completions | 1 |
| **24.35** | **The Alchemist + The Smith in the Woods + The Weakening Ward, in the same second** | 4 |
| 26.1 | Know Your Enemy | 5 |
| 30.9 | Tools done → Strength for the Road + A Roof for the Work | **6** |
| 34.8 | Workshop built → A Safe Place | **6** |
| 35.4 | Breach won → What Breathes Under the Roots | 5 |
| 35.8 | Something to Fight With | **6** |
| 48.6 | after raid 2 (no Bjarne): The Alchemist, Bring Bjarne Home, the Heartroot hunt and the club still open | 4 |

The build runs of 2026-11-14 (`20261114-232848.csv`, `20261114-122242.csv`) show the same shape:
- In the first, after raid 1: 6 starts in 15 minutes and a peak of 5 active, without Ingrid home.
- In the second, the player died on raid 1.

Why it feels like a flood, from the data:

1. **Two completions unlock seven quests.**
   - Into the Misty Woods unlocks The Weakening Ward, Know Your Enemy and A Roof for the Work.
   - Bring Ingrid Home unlocks The Alchemist and Bring Bjarne Home.
   - Tools of the Clearing, still open from before the raid, then unlocks two more.
2. **The Haven quests fan out.** Each one finished opens two or three, so finishing work makes the list longer.
3. **Two Main quests start before they can be done.**
   - The Weakening Ward waits for the player to start it at the portal.
   - What Breathes Under the Roots has no rumour to follow for another raid (`Data/Raids/Rumours/TheHeartroot.asset`, After Extractions 2).
4. **Asks that can't be met yet stay on the list.**
   - The Alchemist (stone blocks, §1.4).
   - The Smith's Request (copper needs charcoal, which needs Tor).
5. **Everything shouts.**
   - Each start plays a sound and a banner (already made quieter once, `Docs/TODO.md:429-431`).
   - The portal has a line for each open quest.
   - The briefing adds up to 3 asks.
   - The raid summary adds lessons and "what held you back".

   `Docs/TODO.md:348-349` already noted "four quests start one after another" at the door, and that Know Your Enemy should be given by someone or dropped.
6. **A second flood is built in at Tor (raid 3).**
   - The Woodcutter's Lodge opens Raise the Walls, A Growing Home and Heavy Loads together.
   - The Cold Stones and What the Land Asks arrive after the third raid.
   - Eirik's rescue, the Heartroot hunt, and A Smith's Hearth (newly possible with planks) are open too.
   - That is up to 8 active quests.

What does not cause it: the prologue, which runs one main quest at a time, and Liv's line, which runs one step at a time.

### 1.4 The stone-block gap, and every other ask that comes before its teaching

**What the altar needs, step by step:**

| Step | Data | Taught or reachable? |
|---|---|---|
| The ask | The Alchemist: Build Structure Purification Altar (`TheAlchemist.asset:55`). Text: "build the Purification Altar she showed you". | The building is taught by Ingrid's rescue (`Data/Settlement/Alchemist.asset` › Teaches Buildings: altar and Cauldron) |
| Cost | 6 Life Orb and 16 Stone Block (`PurificationAltar.asset:22-27`) | Life Orbs: three were used on the door. There are 7 more Life Orb sources in the Haven world (memory note on the quest redesign) and 2 from A Light They Can See (a Hamlet-rank reward, much later). **No text says where.** |
| Stone Block | 4 Grey Stone → 2, at the Workshop, 5 s, known from start (`StoneBlock.asset:21-26`) | **Nothing names it.** A search of every asset, script and scene for "Stone Block" finds only the item and one UI test (`Scripts/Editor/Tests/CraftingUITests.cs:120`) |
| The Workshop | A Roof for the Work (optional Haven) | Opens after raid 1 *and* the tools: the same moment as the ask, in a different quest. The quest text names repairs, the chest, the club and bandages, not stone |
| Grey Stone | Nodes that need a pickaxe | Tools of the Clearing makes the pick, but it starts only when the door wakes, and the player may go to the raid without it |
| Who makes blocks for you | The Mason (Hild): "keeps grey stone and granite turning into blocks" (`Data/Settlement/Mason.asset`) | Hild is the 10th rescue |
| What it blocks | Purified Essence comes only from the altar (`SettlementSettings.purifierJob`). Portal tier II needs 6 Purified Essence and a Smithy at tier I, and opens tier II raids (`SettlementSettings.asset:197-209`) | The progression spine waits on an untaught recipe |

`RescueOrderTests.TheSmithy_AndTheCauldron_NeedOnlyTheWorkshop_AndTheYardNeedsLodgeII` (`Scripts/Editor/Tests/RescueOrderTests.cs:76`) checks
building prerequisites, not materials. `ContentAudit` (`Scripts/Editor/Content/ContentAudit.cs`) checks that every item *can* come, not
*when*. So no test sees an ask that comes before its teaching.

**The same check for every early ask:**

| Quest (opens at) | Needs | Made at / comes from | Reachable when asked? |
|---|---|---|---|
| A Light in the Wood (home) | Campfire: 8 Sticks, 12 Small Rocks | Picked up | Yes: taught in the prologue, and the portal says so |
| Tools of the Clearing (door) | Hatchet, pick | By hand | Yes |
| Strength for the Road | 10 Water for the stew | A waterskin | Yes in play (done in both runs) |
| A Roof for the Work (raid 1) | 24 Oak Wood, 12 Grey Stone, 12 Sticks | Gathered; Grey Stone needs the pick | Yes, with the pick |
| Something to Fight With | Knotted Club | Workshop | Yes |
| **The Alchemist (raid 1)** | 16 Stone Block, 6 Life Orb | Workshop; world stones | **Reachable, never taught** |
| **Ingrid's Cauldron** (her job description and `Docs/RescueOrder.md` row 1 promise it) | 8 Life Mote, 8 Oak Plank (`Cauldron.asset:22-27`) | Oak Plank: Lodge only | **Not before Tor (rescue 3)** |
| **A Smith's Hearth (raid 2)** | Smithy: 24 Grey Stone, 8 Stone Block, 8 Oak Plank (`Forge.asset:23-29`) | Planks: Lodge only; Supply Crates 50% × 1-2 | **Not before Tor**, except from about 10 crates |
| "Bjarne smelts the copper you bring him" (A Smith's Hearth text) | Copper Ingot: 3 Copper Ore, 2 Charcoal (`CopperIngot.asset`) | Charcoal: Lodge only (`Charcoal.asset:24`, `CharcoalFromSticks`) | **Not before Tor** |
| **The Smith's Request (Workshop)** | 3 Copper Ingots, Copper Pickaxe (Oak Plank, Nettle Cord) | Smithy, Lodge | **Not before Bjarne and Tor**, yet offered after the Workshop |
| The Weakening Ward | A breach | – | Yes |
| What Breathes Under the Roots | Hearth Ember (3 Sticks, Campfire); the Heartroot | Its rumour after 2 extractions | **Starts a raid before its target exists** |
| Show Me Your Kitchen (Yrsa, 5) | 20 Oak Plank, 12 Stone Block, 4 Copper Ingot; Smithy standing | Lodge, Workshop, Smithy | Yes by then. The text names the Smithy but not the blocks |
| Stone on Stone (Hild, 10) | Cottage: 24 Oak Plank, 12 Stone Block, 8 Nettle Cord; Workshop II | – | Yes |
| The Old Marks (Liv, 7) | Enchant at the Purification Altar | The altar | Only if the altar was ever built |

Two smaller things:
- Ingrid's job teaches no skill (`Alchemist.asset` › Teaches is empty) while every other job does. Alchemy has no mentor.
- The Leader teaches nothing either. Mining and Horsemanship have no teacher.

### 1.5 The rescues: who, when, what they open, what they say

| # | Person | Expedition or stranger | Job (skill taught) | Opens | Rescue site (rumour) | Homecoming memory | Voiced lines | Quests now |
|---|---|---|---|---|---|---|---|---|
| 1 | Ingrid | Expedition (alchemist) | Alchemist (none) | Purification Altar, Cauldron | Hiding near the portal | Yes: the troll came for the door, the figure in Granheim mail, Bjarne's hammer | 27 (most are prologue-era barks) | Bring Ingrid Home; The Alchemist |
| 2 | Bjarne | Expedition (smith) | Smith (Weaponcrafting) | Smithy | A Cold Forge: "won't leave the hammer behind" | Yes: his blazes on the wrong side, the roots moved the road, Tor's leg | 27 | The Smith in the Woods; A Smith's Hearth |
| 3 | Tor | Expedition (carpenter) | Carpenter (Woodcutting) | Lodge, Wainwright's Yard | A Shelter, Built Too Well | Yes: the hollow where the ground breathes | 27 | Planks, Not Logs; The Woodcutter's Lodge; Raise the Walls; A Growing Home; Heavy Loads |
| 4 | Eirik | Expedition (soldier, Granheim) | Warden (Melee) | Watch Post | The Last Stand | **No** | 78 (prologue) | Someone Holding the Line; The Watch |
| 5 | Yrsa | Stranger ("cooked for a jarl") | Cook (Cooking), Fisher | Kitchen | A Fire in the Woods | **No** | **3** | A Fire in the Woods; Show Me Your Kitchen |
| 6 | Gunnar | Stranger ("alone by choice") | Hunter (Archery), Fisher (Fishing) | Hunter's Lodge, Dock | The Hunter's Pit | **No** | **3** | The Hunter's Pit; Hides and Hooks |
| 7 | Liv | Expedition (rune scholar) | Rune Scholar (Runecraft) | Raid runes; carves past your Runecraft | The Humming Stones (loud or slow) | **No** | 26, none from her talk topics | The Humming Stones; Runes for the Road; her five (§1.6) |
| 8 | Asa | Stranger ("a village that isn't there any more") | Farmer (Foraging), Weaver (Armorcrafting) | Farm Plot, Tailor's Shop | The Burned Farmstead | **No** | **3** | Seed from the Ashes; Good Dirt |
| 9 | Runa | Expedition (tracker, poacher) | Tracker (Sneaking) | +1 rumour | Marks on the Trees (night) | **No** | 71 (prologue) | Marks in the Dark; Eyes in the Woods |
| 10 | Hild | Stranger ("followed the smoke") | Mason (Construction) | Cottage, Stone Wall; the block bench | A Wall Round Nothing | **No** | **3** | A Wall Round Nothing; Stone on Stone |
| 11 | Sigrid | Expedition (leader) | Leader (none) | Haven Banner, +120 s per raid | The Horn at Dusk (a wound) | **No** | 83 (prologue) | The Horn at Dusk; Under One Banner |

Order verified in two places:
- **Quest prerequisites:** Asa ← Liv ← Gunnar ← Yrsa ← Eirik ← Tor ← Bjarne ← Ingrid; Runa ← Asa; Hild ← Runa; Sigrid ← Hild.
- **Rumours' After Home:** the same chain (`Data/Raids/Rumours/{Bjarne,Tor,Eirik,Yrsa,Gunnar,Liv,Asa,Runa,Hild,Sigrid}.asset`, each Urgent, After Extractions 1, Sigrid 3).

`Docs/RescueOrder.md` matches. `Docs/GameDesignDocument.md:887` does not.

What exists for relationships already, and is under-used:
- **Companion bonds:** Trusted at 3 raids, Sworn at 8 (`Resources/SettlementSettings.asset`, `bondRanks`).
- **Memory lines per raid:** only Eirik, Runa and Sigrid have their own, 4 each.
- **Grief lines** and **boss talk.**
- **Worker ranks** (Novice to Master) that Liv's quests already raise through `rewardWorkFor`.
- **Crafting orders** with a personal ask:
  - Bjarne: "By your hand, not mine: I want to see what you've learned";
  - Gunnar: "Mine pulls left";
  - Hild: "The quarry eats picks".

### 1.6 The model: Liv's enchanting line

| Step | Quest (asset, id) | Starts | Teaching beat | Objectives (type) | Reward |
|---|---|---|---|---|---|
| 0 | Runes for the Road (TheRuneScholar, work.runescholar) | Auto after her rescue | Her job | Assign Job Rune Scholar | – (the job is the pay) |
| 1 | The Old Marks (liv.oldmarks) | Auto after step 0 | What an enchantment is: one per piece, cut at the altar with essence; Purified safe, Cursed has a price; found, never taught from nothing; counts once | Talk To Liv ("Ask Liv about enchanting": her `EnchanterTalk.Lesson` topic); Enchant Item ×1 | 3 Blank Parchment; Liv's work +10 |
| 2 | Rubbings (liv.rubbings) | Auto | Where they come from (1): carved stones in raids; bring it home to read | Craft Item Blank Parchment; Use Object Carved Stone (a Quest Target); Learn Enchantment ×1 | 2 Blank Parchment; Liv +15 |
| 3 | Marks of the Marked (liv.marked) | Auto | Where they come from (2): elites carry their mark | Learn Enchantment ×1 (from an elite) | 15 Purified Essence; Liv +15 |
| 4 | A Hand for the Shelf (liv.shelf) | Auto | Her growth: every inscription teaches her, even a known one, so she carves further past you | Give Inscription ×2 to Liv | Liv +20 |
| 5 | The Great Work (liv.greatwork) | Auto | Great marks change how a fight goes | Enchant Item (great) | Teaches Thornguard (her own); Liv +30 |

How it is wired:
- **Data.** Prerequisites chain the steps (Automatically, one at a time), and `objectivesInOrder` makes each quest a sequence.
- **Rewards.**
  - Rewards are the next step's tool (parchments) or currency (essence).
  - `rewardWorkFor` and `rewardWork` raise her worker rank (`QuestDefinition.cs:106-108`). Her rank is what her reach grows on: Novice to Master carve 3 to 20 levels past yours.
  - `rewardEnchantments` teaches one for good.
- **Events.** `Enchanted`, `EnchantmentLearned`, `InscriptionGiven` and `InscriptionFound` were added to `GameEventKind` (`Scripts/Events/GameEvents.cs`) with three objective types.
- **Talk.** `EnchanterTalk.AddTopics` (`EnchanterTalk.cs:25`) is called from `ResidentNpc.AddTopics` (`ResidentNpc.cs:200-204`) for a job that Carves. It adds three topics:
  - "Ask about enchanting" (the lesson, which is also the first quest's journal note);
  - "Ask what she can carve" (her rank and reach);
  - one "Give her the … inscription" per carried inscription, at most 4.

The pattern to copy:
1. A conversation that is the lesson, ticked by Talk To.
2. Do the verb once at home with what you already have.
3. Make a small tool and use it on a raid site, then bring the find home. This teaches the raid loop and the rule that what you carry can be lost.
4. A harder raid target (elites).
5. Give the teacher something, so that they grow.
6. A capstone where the teacher gives something of their own.

Three things it doesn't have, and the new lines should:
- **No personal beat.** We learn nothing about Liv: not her teacher (her old companion line: "My teacher said the waystones connected places of shelter"), not "the stones broke along a line", not why she says "Write it down".
- **Not voiced.** The lesson (about 140 words) and the two replies are C# strings. The replies interpolate the enchantment's name, so they can't be spoken as written.
- **One C# class for one person.** Eleven people would mean eleven classes. Topics should be data on `JobDefinition` (§3.9).

### 1.7 The story and lore so far

**The frame (the opening cinematic, "The Old Story", `Docs/BackstoryCinematic.md`).**
- The portal, never named, tells "little one" the old story:
  - the pact: people leave the forest room to live, and its guardians keep "older, hungrier things" at bay;
  - a hearth beside the stones and the night kept from their door;
  - the fire going out;
  - the forgetting: "First the way home. Then their names. Then there is no one left to find";
  - a hundred winters alone.
- Then "four of your people walked in under the trees… So four more came looking. One of them was you."
- It deliberately leaves out who let the fire go out, what the guardians became, the troll, the names, and why the stones wake for the player.

**The prologue.**
- Sigrid leads Eirik, Runa and the player east, after Ingrid, Bjarne, Tor and Liv, who went ahead two days earlier.
- The player says why they came: coin, truth or kin.
- The day goes out at noon. Wolves hunt in daylight. Roots set ambushes. Boar run west.
- Sigrid loses Liv's name for a moment ("Eleven years I have known her"). Eirik had to think for his sister's.
- At the waystones the centre stone ignores Sigrid's hand ("It will not wake for me. It never has") and wakes for the player's.
- The old troll comes. The connection holds a minute and breaks.
- "The Catch" (`Docs/PrologueEndCinematic.md`): seven lights scatter. The portal reaches and catches one, the player: "I could only catch one."

**The portal at home** (86 voiced lines, the voice of the clearing):
- "I am a door, little one."
- The old pact; a hundred winters.
- "The forest does not kill the lost. It forgets them."
- Seven sparks alive in the woods.
- "The forest cannot forget a hearth that someone tends."
- The roots move towards it. The three stones each say a piece of the pact.
- "One day you will tell me why these old stones woke for your hand."

**The first act** (`Docs/QuestRedesign.md`, as built step 3):
- The roots that closed the road, choked the stones and grew from the breach's dead are one thing.
- Tor's breathing hollow. The Heartroot wakes only for a Hearth Ember: "the one thing the forest can't forget".
- Its last word is "Hearth-keeper".
- The twist: it was the forest's guardian. "Your kind let the hearth beside me go cold a hundred winters ago. It kept its half for ninety winters after that, alone, until the hunger got into its roots."
- People broke the pact first.
- The road north opens: "The old one that broke my stones the night you fell walks that road. It came for the door then."

**The first raid** ("The Misty Woods", `Docs/MistyWoodsCinematic.md`):
- "The longer you remain, the deeper its corruption grows."
- "You are here for what the forest holds. Its resources, or its prisoners."
- "Take what you need, save who you can."

**Raids, bosses and folk creatures.**
- Only two raids exist: Misty Woods and Troll Valley.
- One boss (`Data/Monsters/Boss The Heartroot.asset`): three phases, the skill Heartroot's Grasp, four home lines.
- Great creatures: Old Moss Troll, Stone Troll, Berg-jutul, Bog Troll.
- Folk creatures (memory: folk creatures; monster roles):
  - Fossegrim, Trollkjerring, Berg-jutul, the Tusse thief, Huldra;
  - Draug (rises again), Marsh Light (a decoy light), the black widow, the undead knights.
- Raid events with folk-tale bones (`Data/Raids/Events/`): Toll Troll, Three Wishes, Oskorei (the wild hunt), Old Barrow, Trapped Pedlar, Cursed Tree, Living Stones, Thickening Fog, Beacon Light, Trader's Wagon.
- The Nøkken can rise when fishing still water (`NokkenEncounter`).
- The Lost: a companion "taken by the forest" comes back as a stranger who doesn't answer to their name (`Data/Raids/Rumours/TheLost.asset`).

**What each person knows** (from `Tools/Audio/Voices/dialogue_lines.json`, 441 lines):
- **Sigrid:**
  - "I have seen stones like these wake once. It took everyone who stood too near, and it put them somewhere else. Ingrid knew that when she came."
  - "Someone opened it on purpose. Whether they meant to let something in or out… that is what we came to find."
  - "The stones out here are older than any village, and someone has been waking them."
  - She reads the old marks: "Most of them mean wait. One of them means open."
- **Eirik:**
  - Twenty winters in the shield wall at Granheim.
  - "My sister's farm is two valleys from here. Her dogs ran into these woods last month and never came back."
  - "Watch her hands when she talks about the stones."
  - "Bjarne owes me money."
- **Runa:** a poacher on the jarl's land. "Someone is paying." "Something is pushing everything out of the high woods, and it is big."
- **Ingrid:**
  - kept saying "Fire" so the forest couldn't take the word;
  - her grandmother left bread on the old stones;
  - "knows more about the curse than she lets on".
- **Bjarne:** came "to examine the old iron markers. Whatever protects the road has cracked."
- **Tor:** "Give me sound timber and a little daylight, and I could make somewhere worth staying."
- **Liv:** "My teacher said the waystones connected places of shelter." "The stones broke along a line!" "Your ward is a promise too."
- **The strangers:**
  - Yrsa cooked for a jarl;
  - Gunnar walked these woods alone for years;
  - Asa's village "isn't there any more";
  - Hild built "a wall round nothing".

  None of them knows anything about the story.

**Places with names and no story.**
- **Haven map regions** (`Resources/Atlases/SafeHaven.asset`): The Wolfwood, The Hollow Pines, Westwood, The Drowning Fen, Alder Mere, Haven Meadows, Stillwater, Eastwood, The Grey Pines, Greyfell, Ravenwood, Hightarn.
- **Haven map places:**
  - stones: Wayfarer's, Pilgrim's, Wanderer's, Farewell, Island, Drowned and Standing Stones;
  - cairns: Lookout, Raven and Windy Cairn;
  - old sites: Old Barrow, Burnt Farmstead, Old and Grey Quarry, Ruined Watchtower, Broken Tower;
  - trees: Mother Maple, Elder and Grandmother Cedar;
  - camps and rests: Woodland, Trapper's and Charcoal Camp; Fisher's and Netmender's Rest; Shepherd's and Ewe Fold; Wolf Den.
- **Elsewhere:** Granheim and "the villages" are named only in passing. Frost Peaks and Moss Marsh are GDD regions with nothing built.

### 1.8 What is missing

**Mysteries planted without a payoff:**

| Seed | Where | Payoff now |
|---|---|---|
| Why the stones woke for the player's hand | Portal, twice; the Heartroot's "Hearth-keeper" | None |
| What Sigrid knows ("It never has", "seen stones like these wake once") | Prologue; Eirik's warning | None. She comes home last and has 4 generic resident lines |
| Who has been waking the stones / opened the door on purpose | Sigrid's camp line and answer | None |
| "The hunger" that got into the Heartroot; the "older, hungrier things" | Twist; opening | None. The Road North closes the act and opens a road with no quest on it |
| Why the old troll came for the door | Ingrid, the portal | It "walks that road"; no boss, no quest |
| The figure in Granheim mail that forgot its name | Ingrid's memory | The Lost and the Draug exist as systems, not as an answer |
| Eirik's sister, two valleys over | Prologue | None |
| The four strangers when everyone says "seven" | Rescue order | Never explained |
| The last smith's note on the notice board ("this hearth wants copper") | The Smith's Request | Who kept a smithy here? None |
| Liv's "line" | Rescue line | None |
| Sigrid's horn | The portal at Hamlet rank, about raid 4-5 | Paid off only at rescue 11 |

**What is missing as structure:**
- **A force with a face.** "The hunger", "the rot" and "corruption" are names, not motives. The GDD's tone rule asks that "creatures should have motivations beyond hostile monster" (GDD §4.2).
- **A second act.** GDD §4.4 says "Act II – Gather a community: recruit survivors, investigate the lost villages, and learn what fractured the pact". Its gathering exists (the rescues). Its investigation and its why don't.
- **Personal stakes for the people.** No one after Tor wants anything except their job and building.
- **Tales.** One exists. The approved plan wanted Tales as folk tales with "a bargain, a riddle or a twist".

**Contradictions found** (fix lists are in §3.8):
1. **Ingrid in the prologue.** `Data/Prologue/Quests/DefendCamp.asset:17`: "Eirik will help and **Ingrid can tend your wounds**". Ingrid went ahead two days earlier and is not at the camp.
2. **Tree imagery.** `Data/Prologue/ForestPrologue.asset:185`: "You wake beneath **a living canopy**". `GameDesignDocument.md:155`: "wakes beside **its roots**". Both are left over from the Heart Tree; the portal is the centre now (memory: portal voice).
3. **Runa named too early.** `Data/Raids/Rumours/RotsHeart.asset:16`: "a heart of black wood, **Runa says**". The rot's heart comes with the Haven's Reach after raid 3, while Runa comes home ninth.
4. **Children.** `Data/Settlement/Requests/wolf-fangs.asset` and `order-masterwork-mail.asset:17`: Sigrid speaks of "the children". There are none.
5. **The GDD's rescue section is stale.** GDD §15.3 gives the old rescue order, and "30 more seconds" for the Leader where the data gives 120.
6. **The GDD status table is stale.** `ARCHITECTURE.md:74` (§15) says "One recruit (Ingrid) and one job (Alchemist)… No needs or relationships".
7. **Timescale.** The Heartroot fell to the hunger about 10 winters ago (90 of 100), but the prologue describes a change of the last month or so: dogs gone "last month", trolls only now off their crags. This can be reconciled (§3.8) but needs a cause for the recent speed-up.
8. **"Seven" versus eleven.** The portal and Ingrid's description count seven lost, and eleven come home.

### 1.9 Where the built game drifted from the approved plan (`Docs/QuestRedesign.md`)

| Plan | Built |
|---|---|
| Tools of the Clearing and Strength for the Road open when the fire burns | Tools opens when the door wakes (+30 s). Strength for the Road opens after Tools. In the user's run both ran into the first homecoming |
| A Roof for the Work opens when the door wakes | Opens after raid 1 *and* the tools, so the Workshop is out of reach at the moment Ingrid asks for stone |
| The Alchemist: "Her job and the altar → healing draughts" | The altar's 16 Stone Block and 6 Life Orb were not planned for. The draughts need the Cauldron, which needs Tor's planks |
| What Breathes Under the Roots "starts when the Heartroot rumour first comes up, after two extractions" | Starts when the first breach is won, which can be after one raid |
| Know Your Enemy as an Errand | Is one, but starts by itself |
| Haven quests: "Two to four open at once" | Measured: up to 4 Haven quests and 6 of all kinds |
| "Coming home is a scene. Each person brought home remembers one thing" | Built for Ingrid, Bjarne and Tor only |
| The figure in mail and the troll's footfalls in raid 1 | Left out (in Ingrid's memory only; noted in the plan) |
| Sigrid's horn at The Road North | The portal's horn line plays at the Hamlet rank, long before her rescue |

### 1.10 Constraints the proposals keep

- **Quest deeds.** Talk To, Reach Place, Use Object and Rescue Person take a target asset. No Quest Event made for one person or place. Story Events only for genuine one-off moments (memory: quest deeds).
- **The approved quest kinds and rules:**
  - Main quests are events, never "gather N" or "build X".
  - Nothing optional stands before a Main or Rescue quest (`QuestCategoryTests.NothingOptional_StandsBeforeTheStoryOrARescue`).
  - The rescue order is approved.
- **The colony.** The Haven is run from H. Its buildings feed raids. Progression is slow but not grindy. Beds never gate.
- **Naming.** Enchantments are Inscriptions and enchantments (Liv says "marks"), never runes.
- **Voice.** Every line is spoken:
  - the portal by Qwen, the others by IndexTTS;
  - a line with a variable can't be pre-spoken;
  - new lines go through Safe Haven > Voice Lines.
- **No removals.**
- **Builders are retired.** Changes are targeted edits to the assets and scenes.
- **Work in progress elsewhere** (another agent, now):
  - an NPC window like the horse window;
  - assigning NPCs to huts with stat or trait effects;
  - house tiers and comfort (bed, kitchen);
  - timed inventory crafting;
  - a harvest progress bar.

---

## Part 2: How other games do it (web research)

A research subagent ran the searches; claims carry its URLs. **(unverified)** marks a claim found in a single community or secondary
source. Blocked pages:
- the Stardew Valley wiki (403);
- the Valheim and Frostpunk fandom wikis (401/402);
- several PC Gamer pages (truncated);
- the Riksantikvaren bell booklet (unreadable PDF).

### 2.1 Key takeaways for Safe Haven

1. **One person, one station, one chain, and the backstory and the mechanic are the same steps.**
   - Spiritfarer's Gwen: build a loom, learn it, fetch its upgrade and her music box from her old villa, upgrade, learn weaving.
   - Enshrouded's craftspeople: "go get a real tool from a real place and set it next to me".
2. **Every bond has the same three parts:**
   - a personal problem;
   - a mechanical reward at each step:
     - Persona 5: confidant ranks give abilities and shop perks;
     - Mass Effect 2: loyalty gives a power and survival odds;
     - Hades: the first Nectar gives a keepsake;
     - Fire Emblem: support ranks give stat bonuses;
   - small talk that changes as the bond grows.
3. **About three things to chase at once after a milestone.**
   - Working memory holds about 4 chunks (Cowan 2001).
   - One RPG design guide puts manageable quests at "about three" **(unverified)**.
   - The fix it names: turn the "quest bush" into a "quest tree".
4. **Never ask for what you haven't shown.** Kingdom Come teaches alchemy at a real bench, from the recipe, step by step. The material's lesson comes as the first step of the quest that needs it.
5. **Contextual first-time lines, not a tutorial list.**
   - Valheim's raven Hugin appears at new milestones, then stops.
   - A CHI 2012 study of 45,000+ players found tutorials paid off only in the most complex game, and context-sensitive help helped.
   - The portal can be Hugin.
6. **Pulses with rest.**
   - RimWorld's Cassandra: about 4.6 days of threat, then 6 quiet.
   - Animal Crossing: one new building "the next day".
   - After a big milestone, release one new system per return home.
7. **Spend the voice budget on reactive lines.**
   - Hades has 21,020 voice lines, but Charon has 120 and Tisiphone 190.
   - Most lines play once, gated by state.
   - Aim for one new line per person per return from a raid.
8. **A flaw revealed after the player already likes them.** The Bloody Baron's abuse is revealed only after he has helped find Ciri, and players were left conflicted.
9. **Lore through pieces the player assembles.**
   - Dredge's writer: "I always wanted people to have to put in a bit of work to piece things together."
   - Harvey Smith: "It has to be possible to miss some things to make finding them meaningful."
10. **The mist is bergtatt.**
    - In Norwegian folklore the hidden folk take people into the hill.
    - The taken "must not taste food" there, can be rung out with church bells, and come back changed or ill.
    - Naming frees: say the nøkk's name and its victim is free; name the utburd and it rests.
11. **Cross-person threads are cheap.**
    - In Enshrouded the carpenter "borrowed" the blacksmith's chest.
    - The blacksmith's quest is finding his daughter.

### 2.2 Relationships that make players care

**Stardew Valley.**
- **Friendship points.** A heart is 250 points. Talking gives +20 a day. A loved gift +80, a liked one +45.
- **Gift limits.** 2 gifts a week. Birthday gifts count ×8. Decay is −2 a day without talk.
- **Recipes by mail** at 3 and 7 hearts: Lewis's Spaghetti at 3, Eggplant Parmesan at 7.
- **Heart events** at about 2/4/6/8/10 hearts, triggered by place and time **(unverified)**.
- Sources: https://gamerant.com/stardew-valley-friendship-point-system-guide/, https://destructoid.com/all-cooking-recipes-in-stardew-valley-and-how-to-unlock-them-including-full-queen-of-sauce-schedule
- *For Safe Haven:* a few thresholds per person, each paying something usable (a recipe, an upgrade), and a weekly cap so it can't be ground.

**Hades and Hades II.**
- **Volume.** Hades has 21,020 voice lines and 305,433 words. Zagreus has about 8,500, Charon 120. Hades II has 30,000+ lines.
- **Gifts.** The first Nectar returns a keepsake. After 5-6, a favour quest opens the locked hearts.
- **Repetition.** Kasavin wanted players not to run into repeats quickly.
- **Mid-run events** check the player's state.
- Sources: https://www.nintendolife.com/news/2020/12/supergiant_shares_hades_voiceover_stats_charon_hardly_gets_a_word_in, https://www.gamesradar.com/games/hades/hades-2-has-an-epic-script-of-over-400-000-words-and-30-000-voice-lines-around-50-percent-more-than-the-original-roguelike, https://hades.wiki.fextralife.com/Nectar, https://gamedeveloper.com/design/how-supergiant-weaves-narrative-rewards-into-i-hades-i-cycle-of-perpetual-death
- *For Safe Haven:*
  - Treat every return as a return to the House: one priority line per person that reacts to the raid, then filler.
  - The personal step opens at a bond threshold. The existing Trusted rank (3 raids) is the natural gate.

**Spiritfarer.**
- **Gwen's chain** has 14 steps:
  - the tutorial buildings;
  - her own lodge;
  - the loom and its minigame;
  - the key to her old villa, which holds the loom upgrade and her music box;
  - weaving;
  - decorating her lodge;
  - the farewell at the Everdoor.
- **Research.** The writers spent a month in end-of-life care facilities. The spirits' stories run to about 90,000 words.
- Sources: https://guidestrats.com/spiritfarer-gwen-guide/, https://en.wikipedia.org/wiki/Spiritfarer, https://gamedeveloper.com/design/inside-the-thoughtful-design-of-thunder-lotus-i-spiritfarer-i-
- *For Safe Haven:*
  - Each profession's upgrade is a real tool recovered from a place in the person's past. Bjarne's iron markers and Hild's quarry are examples (§3.6).
  - Their house grows with the bond. This hooks into the house tiers now being built.

**Persona 5 confidants.**
- **Party confidants** unlock combat abilities (Baton Pass).
- **The others** unlock shop stock, discounts and experience. Takemi's clinic is one.
- **Time** is limited, so confidants compete.
- Sources: https://en.wikipedia.org/wiki/Persona_5, https://rpgsite.net/feature/5479-persona-5-confidant-guide-conversation-choices-answers-romance-options-gifts-skill-unlocks
- *For Safe Haven:* each person's steps unlock something they do for you on raids (Ingrid heals more, Eirik holds the line) or at home (better meals, cheaper repairs).

**Mass Effect 2 loyalty missions.**
- **The mechanic.** A personal problem becomes a mission. Loyalty gives a unique power and decides who survives the end.
- **Why.** Casey Hudson wanted the optional stories to be as intense as the main plot: "But that is the story."
- Source: https://en.wikipedia.org/wiki/Mass_Effect_2
- *For Safe Haven:* a person who has finished their line is "Sworn" and better in a breach or on a raid.

**Fire Emblem supports.**
- **Ranks.** Units that fight side by side gain C, B and A support ranks, with a conversation at each.
- **Bonuses.** GBA Eliwood and Hector at A get +1 attack, +3 defence, +15 avoid.
- Source: https://fireemblemwiki.org/wiki/Support
- *For Safe Haven:* the existing companion bond is a support system without the conversations. Add one line at Trusted and one at Sworn per person.

**The shared shape.**
- A personal problem in 3-6 steps, gated by time or progress.
- A mechanical reward at each step.
- Ambient lines that change.
- A lasting marker at the end: a keepsake, a flower, an ending slide.

### 2.3 Builders and colonies that teach through residents

**Enshrouded (the closest match).**
- **The five craftspeople** each sleep in a vault, are woken and summoned to a Flame Altar, and own one station:
  - Oswald the blacksmith;
  - Balthazar the alchemist;
  - Cade the carpenter;
  - Athalan the hunter;
  - Emily the farmer.
- **Station quests.** There are 19, each handing out one prop that unlocks recipes. Masonry Tools unlock 173 recipes, the Table Saw 148, the Blacksmith's first request 41.
- **Oswald's list:**
  - forge a first weapon;
  - glider, grappling hook;
  - salvage;
  - find his stolen chest;
  - recover his Crucible from the scavengers' mine (this unlocks the Smelter);
  - find his estranged daughter to be his assistant.
- **Personalities.** Oswald is gruff ("Took ya long enough to find me!"). Cade is punny ("Measure twice, cut once!"), and secretly borrowed Oswald's chest.
- **Survivors gate the Flame.** One source: 12 survivors for Flame level 8 **(unverified)**.
- Sources: https://enshrouded.wiki.gg/wiki/Survivors, https://enshrouded.wiki.gg/wiki/Blacksmith, https://enshrouded.wiki.gg/wiki/Carpenter, https://pindrop.gg/enshrouded/guides/quest-order, https://allthings.how/enshrouded-how-to-awaken-the-survivor-and-summon-oswald-anders/, https://dotesports.com/?p=1121309, https://www.sportskeeda.com/mmo/enshrouded-flame-altar-upgrade-levels
- *For Safe Haven:*
  - Each step is "fetch a real tool from a real place and set it next to me", and the payoff is immediate.
  - The person who uses a material teaches it first.
  - A cross-person thread like the borrowed chest costs two lines.

**Dragon Quest Builders 2.**
- **Gratitude.** Residents release gratitude hearts when builds please them. The town bell levels the base and unlocks recipes.
- **Hand-over.** In Furrowfield the player marks a field and leaves seeds, and the villagers farm it from then on.
- A critic found gratitude "more of a transaction".
- Sources: https://kotaku.com/helping-people-in-dragon-quest-builders-2-gives-me-so-m-1836663948, https://www.siliconera.com/dragon-quest-builders-2-farming-has-a-communal-story-of-seasons-vibe/
- *For Safe Haven:*
  - "Teach once, then they take over." The player does the job by hand in the quest, then the person runs it as a worker.
  - Stone blocks are the example: the player cuts them for ten raids, then Hild runs the bench.

**Medieval Dynasty.**
- **Reputation** from quests gates recruiting. The cost per recruit rises with population **(unverified)**.
- Sources: https://www.gamepressure.com/medieval-dynasty/reputation-and-approval/z2dfa7, https://steamcommunity.com/app/1129580/discussions/0/4038104984901790770

**Gord.**
- **Villagers** have health, sanity and faith.
- **Quests** go outside the walls for a calamity or for a villager's personal matter.
- **Reviews** were mixed: "monotonous micromanagement" (IGN).
- Sources: https://en.wikipedia.org/wiki/Gord_(video_game), https://www.gamesasylum.com/2023/08/17/gord-review/
- *For Safe Haven:* personal quests that start from a resident's state are a good hook. Someone "still half in the mist" fits the trait system now being built. Avoid meters to babysit.

**Frostpunk.**
- **Requests** were reworked into laws, and are few and weighty.
- **The campaign** ends at a telegraphed climax, the Great Storm.
- Source: https://en.wikipedia.org/wiki/Frostpunk
- *For Safe Haven:* a known, announced event as each act's boundary.

**RimWorld storytellers.**
- **Cassandra Classic** starts major threats on day 11, then runs 4.6 days on and 6 off.
- **New colonists** come less often as the colony nears about 20 people.
- Sources: https://rimworldwiki.com/wiki/Cassandra_Classic, https://rimworldwiki.com/wiki/AI_Storytellers
- *For Safe Haven:* after a big return, a quiet window. Each rescue matters more as the Haven fills.

**Valheim.**
- **No quest log.** Hugin, the raven, gives sparse contextual hints at milestones (from guides).
- **Bosses** drop the key to the next material: the antler pickaxe, the swamp key, the wishbone.
- Sources: https://www.shacknews.com/article/150866/valheim-10-review-worth-the-wait, https://dathost.com/tools/valheim-progression-guide, https://www.gamepressure.com/newsroom/valheim-draws-from-skyrim-and-zelda-devs-explain-the-phenomenon/z02da9
- *For Safe Haven:* the portal speaks once, the first time a cost has an unmade part: "Stone answers to the chisel, not the axe."

**RuneScape: Dragonwilds.**
- **The tutorial** is a relay of teachers: the Wise Old Man, Doric (stations and building), Vannaka (combat, cooking), Zanik (magic).
- **One review:** "loves telling you what to do, but forgets to tell you how to do it" **(single review)**.
- Sources: https://dragonwilds.runescape.wiki/w/First_Steps, https://www.gfinityesports.com/article/runescape-dragonwilds-v10-review-a-magical-world-punished-by-brutal-solo-survival
- *For Safe Haven:* the relay is how rescues already work. The stone-block gap is exactly the "what without how" this reviewer names.

### 2.4 Quest writing

**The Witcher 3: the Bloody Baron.**
- **Origin.** It began as one sentence: a Baron wants a monster killed in exchange for news of Ciri.
- **The reveal.** His abuse is revealed after he has helped find Ciri.
- **The botchling.** It can be killed, or named and laid to rest.
- **Endings** range from his death to a cure. "Not every story he is involved in has to have a happy ending" (Stachyra).
- Sources: https://en.wikipedia.org/wiki/The_Bloody_Baron, https://kotaku.com/the-story-behind-the-witcher-3s-bloody-baron-quest-1736090893
- *For Safe Haven:*
  - One or two people with a guilty secret about how they were taken, revealed after their first useful step.
  - Naming a crying spirit is a ready Norwegian story (the utburd, §2.7).

**Kingdom Come: Deliverance.**
- **Reading** is a quest; it unlocks books and alchemy recipes.
- **Alchemy** is done by hand at a bench, following the recipe.
- Sources: https://kingdomcomedeliverance.wiki.fextralife.com/Mightier+than+the+Sword, https://primagames.com/tips/kingdom-come-deliverance-how-learn-alchemy-and-brew-potions
- *For Safe Haven:* every line's first step is doing the craft at the station with the teacher watching. Liv's line already does this.

**Dredge.**
- **Tension.** Night is the horror beat.
- **Lore.** Writer Joel Mason: "I always wanted people to have to put in a bit of work to piece things together." Fan breakdowns are "about 90% correct".
- Sources: https://www.gamedeveloper.com/design/trawling-in-the-deep-how-black-salt-games-made-spooky-fishing-rpg-i-dredge-i-, https://blacksaltgames.com/dredge-launch-interview-with-development-team

**Outer Wilds.**
- **The ship log** records discoveries as a web of connections. Only knowledge carries over.
- **Hints without spoilers** ("South Pole" on a sign).
- Sources: https://en.wikipedia.org/wiki/Outer_Wilds, https://www.mobiusdigitalgames.com/blog/the-intentionality-of-wandering
- *For Safe Haven:* a journal page of what has been heard (a "names and rumours" page), separate from the quest list. Lore leads live there and are not tasks.

### 2.5 Pacing the first hours

- **Memory.** Short-term memory holds about 4 chunks (Cowan 2001). https://memory.psych.missouri.edu/assets/doc/articles/2001/cowan-bbs-2001.pdf
- **Quest bushes and trees.** A "quest bush" makes each quest "individually less valuable". Gate by zone or time, chain, or cap the log. Players manage "about three" **(single source)**. https://howtomakeanrpg.com/r/a/avoid-quest-bushes.html
- **Skyrim.** Journals fill with overheard quests that never leave (forum, **unverified**). https://forums.nexusmods.com/topic/695236-overwelmed-with-quests-how-do-you-refrain
- **Fallout 4.** Preston Garvey's endless settlement asks became a meme. They "aren't very interesting" and can't be declined. This is the warning for a colony game. https://kotaku.com/the-internet-loves-to-make-fun-of-fallout-4s-preston-ga-1751573683
- **Checklists.** Nate Purkeypile: open worlds feel like "that checklist… you don't really feel like you're exploring". https://www.denofgeek.com/games/skyrim-designer-reveals-the-biggest-problems-with-modern-open-world-games/
- **Tutorials.** CHI 2012, 45,000+ players: tutorials justified only in the most complex game; context-sensitive help can help. https://grail.cs.washington.edu/projects/game-abtesting/chi2012/
- **Breath of the Wild.** The Great Plateau teaches cooking, temperature, stamina and physics on the way to shrines, and opens the world after about 90 minutes **(analysis source)**. https://en.wikipedia.org/wiki/Great_Plateau
- **Animal Crossing.** One building moves in "the next day". Critics call it intentional gating. https://shacknews.com/article/117138/how-to-get-the-museum-animal-crossing-new-horizons

### 2.6 Lore delivery

- **Environmental storytelling.** Smith and Worch, "What Happened Here?" (GDC 2010): "staging player-space with environmental properties that can be interpreted as a meaningful whole"; "Every anonymous environmental storytelling moment wastes the opportunity to say something". https://www.gdcvault.com/play/1012647, https://niemanstoryboard.org/2011/01/14/harvey-smith-on-environmental-storytelling-and-embedding-narrative/
- **Hollow Knight.** The Hunter's Journal turns killing into lore collecting. https://www.hollowknight.wiki/w/Hunter%27s_Journal_(Hollow_Knight)
- **Dark Souls.** Lore lives in item descriptions **(secondary)**. https://www.pcgamer.com/the-art-of-flavour-text/
- *For Safe Haven:* the creature journal already exists. Add a folk saying per creature, unlocked by meeting it. Objects the taken carried (a ring, a half loaf) hint at a rescue before it comes.

### 2.7 Nordic folklore for the taken, the mist and the speaking clearing

SNL is *Store norske leksikon*.

| Being or custom | The folklore | Source | For Safe Haven |
|---|---|---|---|
| **Bergtatt** (taken into the mountain) | The hidden folk take people; the taken "must not taste food" inside, can be "rung out with church bells", and come back foolish, ill or changed. Brides and new mothers were typical victims | https://snl.no/bergtaking, https://sv.wikipedia.org/wiki/Bergtagen | **The mist is this.** The taken are alive and forgotten. Those who ate in the mist come home with a trait their line eases |
| **Church bells** | Rung to make the mountain let go; trolls left valleys for the din of bells (Riksantikvaren booklet seen only as an excerpt, **unverified**) | https://en.wikipedia.org/wiki/Troll | A bell for the Haven: a building or upgrade whose ring helps a rescue, extends a raid, or keeps trolls off |
| **Naming** | Saying the nøkk's name frees its victim; naming an utburd lays it to rest. No source found for calling a taken person's own name | https://snl.no/nøkken, https://snl.no/utburd | **The house rule: names have power.** The forest takes names; saying one brings someone back |
| **Huldra** | Beautiful, with a hollow back and a cow's tail; lures and keeps men; dangerous at life's turns; steel binds her | https://snl.no/hulder | Huldra enemies posing as the taken; steel wards from the smith |
| **The hidden folk** (underjordiske, vetter) | Live like people; help the respectful, harm the rude; ask before you build | https://snl.no/underjordiske | Hild asks the land's leave before a big build |
| **Draug** | A dead man who returns; at sea, the drowned with a seaweed head; seeing one is a death omen | https://snl.no/draug | The figure in Granheim mail: what the forgotten become |
| **Nøkken** | A water spirit, often a horse; one life a year; a warning wail; its name frees its victim | https://snl.no/nøkken | Gunnar's Fisher step; the bog horse |
| **Bäckahäst / nykur** | A white horse in fog; you can't get off; a nykur tamed with a carved cross hauled stones | https://en.wikipedia.org/wiki/B%C3%A4ckah%C3%A4st | A late taming of the bog horse as a stone hauler |
| **Trolls** | Mountains and mounds; turn to stone in sunlight (landmark boulders are petrified trolls); take people; hate bells | https://en.wikipedia.org/wiki/Troll | Hild's quarry is a troll the sun caught; the old troll as a guardian |
| **Vardøger** | A spirit double heard arriving before its owner: steps, a door, a cough | https://snl.no/vard%C3%B8ger | The night before a rescue comes home, the Haven hears their steps |
| **Fylgje** | A follower spirit; family guardians speak in dreams | https://snl.no/fylgje | The portal as the clearing's guardian |
| **Utburd** | A secretly abandoned child's ghost crying in the wild; laid to rest by naming: "Eg døyper deg på ei von" ("I baptise you on a hope") | https://snl.no/utburd | A Tale: find the crying spirit and name it |
| **Skogsrå** | A forest woman; leads travellers astray; rewards faithful hunters and charcoal burners, punishes the faithless | https://en.wikipedia.org/wiki/Skogsr%C3%A5 | Gunnar's broken bargain |
| **Oskoreia** | The wild hunt at Christmas; sweeps people away and leaves them far from home; steel over doors protects | https://snl.no/oskoreia | Explains some of the taken; the existing Oskorei raid event; a winter night |
| **Lyktemenn** | Small flames over bogs leading people astray | https://snl.no/lyktemenn | The Marsh Light already does this |
| **Nisse / gardvord / haugbonde** | The farm's first settler in his mound, given porridge with butter at Yule; turns spiteful if neglected | https://snl.no/nisse | The portal as the clearing's first keeper (an Act III reveal, §3.8) |
| **Steel and iron** | Bind the hidden folk: scissors in cradles, steel in water, steel over doors | https://uhi.ac.uk/en/research-enterprise/cultural/institute-for-northern-studies/blogs--exhibitions/mimirs-well-articles/dont-mention-the-tail-a-look-into-the-hidden-world-of-the-huldrefolk.html | Bjarne's iron markers on the old road |

---

## Part 3: Recommendations

### 3.1 Diagnosis in numbers

| Problem | Number | Cause in the data |
|---|---|---|
| The flood after raid 1 | 9 starts in 11.4 min, 3 in one second, a peak of 6 active (user's run) | 7 quests hang on 2 completions, Haven quests fan out, no cap, 3 s spacing from the last completion only |
| A second flood at raid 3 | Up to about 8 active (modelled) | Lodge → 3 Haven at once; Haven's Reach (2); rescue; Heartroot; the Smithy unblocked |
| The stone-block gap | 0 mentions of Stone Block in any text; 32 Grey Stone and 8 crafts needed; The Alchemist never done in the user's run, Ingrid spoken to 6 times | Ask before teach; the Workshop optional and late; the Mason tenth |
| Plank and charcoal gaps | 3 asks before Tor (Smithy, Cauldron, copper) | Planks and charcoal only at the Lodge |
| A stall at the first rescue | Ingrid not home after raid 1 in 2 of 3 real runs | Everything after hangs on her |
| Thin people | 8 of 11 without a homecoming memory; 4 people with 3 lines each | Only Liv got a line |
| No second act | 0 main quests after The Road North | Act II unwritten |

### 3.2 Ranked recommendations

| # | Recommendation | Impact | Effort | Voice (lines) |
|---|---|---|---|---|
| 1 | **Turn the bush into a tree** (data, §3.4) | Very high | S | 0 |
| 2 | **Squared Stone** before The Alchemist, texts that name the materials, and a "made at" hint in the build menu (§3.5) | Very high | S (+S code) | 2-3 |
| 3 | **Planks and charcoal before Tor** (rough plank at the Workshop, charcoal at the campfire); The Smith's Request after the Lodge (§3.5) | High | S | 0 |
| 4 | **Reachability audit** in `QuestAuditTests` (§3.9) | High (stops repeats) | M | 0 |
| 5 | **A robust first rescue** (§3.4.4) | High | S-M | 0-2 |
| 6 | **Eight homecoming memories** (§3.6.0) | High | S | 8 |
| 7 | **Lines for Ingrid, Bjarne and Tor** (§3.6) | High | M | about 30 |
| 8 | **Homecoming release rule** and tracker focus (§3.4.3) | High | M | 0 |
| 9 | **Lines for the other eight**, plus Liv's personal beats and her voiced lesson (§3.6) | High | L (content) | about 80 |
| 10 | **Act II main line** with the old troll (§3.7) | Very high (long term) | L | about 40 |
| 11 | **New objective types:** eat a meal, take a lesson, raid with someone, bond rank, hand over an item (§3.9) | Medium (enables 7, 9, 10) | S each | 0 |
| 12 | **Job talk topics as data**, generalising `EnchanterTalk` (§3.9) | Medium | M | – |
| 13 | **"!" over residents** with a step waiting; line progress in the NPC window being built (§3.9) | Medium | S-M | 0 |
| 14 | **Contradiction fixes** (§3.8.8) | Medium | S | 2-3 |
| 15 | **Tales from the existing raid events** (§3.7.4) | Medium | M | about 20 |
| 16 | **Lines between residents** (Bjarne's debt to Eirik, etc.) | Low-Medium | S-M | about 10 |

### 3.3 The proposed quest order

Kinds:
- M = Main;
- R = Rescue;
- P = the portal's Haven chain ("The First Days");
- N = a person's line (Haven, given by them);
- E = Errand;
- T = Tale.

"Active" is the count on the tracker. Errands and Tales count only when the player picks them up.

| When (est.) | Quest | Kind | What it teaches | Active |
|---|---|---|---|---|
| 0-14 min | Prologue: 7 main quests, errands from givers (unchanged) | M, E | Moving, fighting, gathering, the fire, defending, the stones | 1-2 |
| 14 | The One It Caught | M | The portal's voice | 1 |
| 15 | A Light in the Wood + **Tools of the Clearing (moved: opens with the fire, as planned)** | M, P | The build menu; crafting by hand; **the pick, which Grey Stone needs** | 2 |
| 16 | The Rootbound Stones | M | Life Orbs; a fight at home | 2 |
| 18 | The Door Wakes | M | The portal | 2 |
| 19-20 | Into the Misty Woods + Bring Ingrid Home (its duplicate "Find Ingrid" line hidden) | M, R | Raids, extraction, worn vs carried | 2-3 |
| Raid 1 | (Echoes in the Wilds if found) | T | Rumours and events | 3 |
| **Home 1** | **The Weakening Ward** (warned; the player starts it) | M | Breaches | 1 |
| | **The Smith in the Woods** | R | The next rescue | 2 |
| | **A Roof for the Work** (opens on extraction even without Ingrid) | P | The Workshop | 3 |
| | Strength for the Road, if the tools are done (else it waits) | P | Food, water | (3) |
| | Know Your Enemy: **moved to the notice board** | E | – | – |
| Home 1, after the Workshop | **Squared Stone** (new; Ingrid's step 1) | N | **The stone bench: 4 Grey Stone → 2 blocks** | 3 |
| | A Safe Place (Something to Fight With waits for it) | P | The chest, what is lost on a fall | 3-4 |
| After the blocks | **The Alchemist** (rewritten text: 16 blocks, 6 Life Orbs, where the orbs are) | N | The altar | 3-4 |
| Breach won | (nothing new: the Heartroot waits for Bjarne) | – | – | 2-3 |
| Raid 2 | Bjarne home | | | |
| **Home 2** | **What Breathes Under the Roots** (prereq moved: after the breach *and* Bjarne; its rumour is up now) | M | The ember, the boss | 1 |
| | **Planks, Not Logs** (Tor) | R | Next rescue | 2 |
| | **A Smith's Hearth** (Bjarne 1; the text names rough planks or crates until Tor) | N | The Smithy, repairs | 3 |
| | Something to Fight With (after A Safe Place) | P | A second weapon | 4 |
| | Ingrid 3 waits for Ingrid 2 | | | |
| Raid 3 | Tor home (or the Heartroot) | | | |
| **Home 3** | **The Woodcutter's Lodge** (Tor 1) | N | Planks, charcoal | |
| | Then **Raise the Walls → A Growing Home → Heavy Loads in a chain** (Tor 2-4) | N | Walls, storage, carts | |
| | Someone Holding the Line (Eirik) | R | | |
| | The Smith's Request (board, now after the Lodge) | E | Copper | |
| | The Cold Stones (**moved to after The Road North**) | – | – | 3-4 |
| Raid 3-4 | The Heartroot | M | Act I boss | |
| Home 4 | **The Road North** (the twist) → **The Cold Stones** (the portal's next call, now at a quiet moment) | M, P | Territory | 3 |
| Raids 5-11 | One rescue per raid. Each homecoming: that person's welcome memory and **step 1 of their line**; the previous person's next step when finished | R, N | One profession each | 3-4 |
| From raid 5 | **Act II, "The Doors"** (§3.7): six Main quests along the road north, ending at Sigrid's rescue and "What Sigrid Knows" | M | Troll Valley, the doors, the old troll | 3-4 |

Starts per homecoming:
- **Home 1: 3** (with the Workshop, 5 across the visit), not 9.
- **Home 2: 3-4.**
- **Home 3: 4-5**, chained, not 8 at once.

### 3.4 The flood fix in detail

#### 3.4.1 Data only: rechain what exists (Phase 0)

| Asset | Today | Change | Why |
|---|---|---|---|
| `PrimitiveTools.asset` | Prereq The Door Wakes, +30 s | Prereq **A Light in the Wood** | The approved plan. The pick (Grey Stone) is in hand before raid 1 |
| `ARoofForTheWork.asset` | Prereqs Into the Misty Woods, Tools | Keep. It already opens on extraction, with or without Ingrid | The Workshop must not wait on a rescue |
| `FoodAndWater.asset` | After Tools | Keep | |
| `FirstWeapon.asset` | After A Roof for the Work, +60 s | After **A Safe Place** | One portal quest at a time |
| `KnowYourEnemy.asset` | Auto after the raid, +90 s | **By Quest Giver at the notice board** (the giver of The Smith's Request) | `Docs/TODO.md:348` asked for it. Not removed |
| `SmithsRequest.asset` | After A Roof for the Work | After **A Smith's Hearth and The Woodcutter's Lodge** | Copper needs the Smithy and charcoal |
| `TheAlchemist.asset` | After Bring Ingrid Home | After **Squared Stone** (new, which follows Bring Ingrid Home and the Workshop) | §3.5 |
| `WhatBreathesUnderTheRoots.asset` | After The Weakening Ward | After The Weakening Ward **and** The Smith in the Woods | Its rumour needs 2 extractions. A Rescue may stand before a Main |
| `RaiseTheWalls`, `AGrowingHome`, `HeavyLoads` | All after The Woodcutter's Lodge | Lodge → Raise the Walls → A Growing Home → Heavy Loads | A tree, voiced by Tor |
| `ALightTheyCanSee.asset` | After The Weakening Ward and A Growing Home | Keep | |
| Story - The Cold Stones (`SafeHaven.unity`, Home Moments) | Its trigger is 3 raids done | Trigger on **The Road North completed** (scene story edit) | Spaces home 3 and opens Act II with a system |

Tests that change with this:
- `QuestCategoryTests.NothingOptional_StandsBeforeTheStoryOrARescue` still passes: Bjarne's rescue before the Heartroot is allowed.
- `RescueOrderTests.EachRescueQuest_FollowsTheLast_AndTheWorkFollowsTheRescue` may need to accept an extra prerequisite on The Alchemist.
- `QuestAuditTests` must still find every quest startable (the board must give Know Your Enemy).

#### 3.4.2 What the player sees after the first raid

The tracker shows at most one quest per colour. After raid 1 that is:
- gold: The Weakening Ward, "Speak to the portal when you are ready";
- teal: The Smith in the Woods;
- green: A Roof for the Work, and then Squared Stone.

The portal's Listen offers the gold one first (built), then the green one. Ingrid's welcome memory plays when she is first spoken to (built).

#### 3.4.3 A homecoming release rule (code, M; Phase 1)

Rechaining fixes today's content. A rule keeps future content from flooding again:
- **`QuestDefinition` › Waits For Homecoming (bool)**, on by default for the Haven and Errand kinds when they start Automatically.
  - Such a quest, once ready, starts only at a homecoming (the player arrives home from a raid) or when the player is at home with no quest of its kind active.
  - At most `StoryPacingSettings` › **New Per Homecoming** (2) start per return. The rest wait in order: kind first, then the order they became ready.
  - Main and Rescue quests are never held.
- **`QuestTrackerView`:** show one quest per kind first (gold, teal, then the newest green from a person, then a portal green), then "+N more". `maxQuests` stays 4.
- **The journal** gets a "Waiting" list of what is ready but held, so nothing is hidden.
- **Tests:**
  - `QuestPacingTests`: two ready Haven quests at a homecoming start; the third waits for the next.
  - A new `QuestFloodTests` simulates completions from New Game to raid 4 on the real assets and asserts at most 3 self-started quests per homecoming.
- **Co-op:** pacing is the host's (the `QuestManager` of the player whose save it is). No objective type changes, so no new co-op rule is needed.

#### 3.4.4 A robust first rescue (S-M)

In the 2026-11-14 build run Ingrid was freed at minute 17.5 but did not come home, and raid 2 did not bring her. The other run died on raid 1.
Everything after waits on her. Options:
- She stays within a few metres of the player. If she falls she is carried, as Sigrid's rescue already does: the player takes a wound to
  carry her.
- Into the Misty Woods keeps its "extract" rule, and a portal line at home says plainly "She is still out there, where you found her. She
  will wait."
- Check the Ingrid rumour comes up again after a failed bring-home. Verify in Play: free her, extract without her, launch raid 2.

### 3.5 The stone-block fix, and the other teach-before-ask gaps

**Squared Stone** (new Haven quest, chain "Ingrid", id `ingrid.stone`)

| Field | Value |
|---|---|
| Starts | Automatically after Bring Ingrid Home **and** A Roof for the Work |
| Objectives (in order) | Talk To Ingrid ("Ask Ingrid what the altar needs"); Craft Item Stone Block ×16 ("Cut 16 Stone Blocks at the Workshop: 4 Grey Stone make 2. A pick finds the grey stone.") |
| Reward | 2 Life Orb (a third of the altar's six); Ingrid's work +10 |
| Lines (Ingrid, 2) | Offer: "An altar wants squared stone. The Workshop squares it: four grey stones make two blocks, and your pick finds the grey." Done: "Sixteen. My grandmother would have cut fifteen and said a prayer over the last." |

Then:
- **The Alchemist's** description is rewritten (no new voice): "Give Ingrid the Alchemist's work and build the Purification Altar: 16 Stone Blocks and 6 Life Orbs. The rootbound stones out in the meadows still hold orbs."
- **A Roof for the Work's** description gains: "…and it squares grey stone into blocks."
- **The build menu** (`BuildMenuWindow.ShowCosts`, `Scripts/UI/BuildMenuWindow.cs:462`): under a cost row the player is short of, if a known recipe makes it, write "made at the Workshop: 4 Grey Stone → 2". The same for Oak Plank, Nettle Cord and Charcoal. S, one lookup per row.
- **The portal, once** (S-M, optional): the first time the build menu shows a cost whose recipe the player has never used, one whisper: "Stone answers to the chisel, not the axe, little one. Your Workshop squares it." Valheim's Hugin, once only. Voice: 1 line, or 4 if one per material.
- **The payoff, later.** Hild's welcome (§3.6.0) says she will cut blocks from now on, and her job already runs the block bench.

**Planks and charcoal before Tor** (data, S):
- **A rough plank at the Workshop:** 6 Oak Wood → 1 Oak Plank, known from the start, slow (8 s). The Lodge's 4 → 2 stays three times better, so Tor still answers a problem the player has felt. This is the approved rescue rule: "the problem comes first".
- **Charcoal at the campfire:** 4 Oak Wood → 1 Charcoal, slow. The Lodge's 3 → 2 stays better. A hearth making charcoal also fits the theme.
- **A Smith's Hearth** text: "The Smithy wants planks: rough ones from the Workshop will do until Tor is home."
- **The Smith's Request** after the Lodge (§3.4.1).
- *Alternative, for the user:* swap Bjarne and Tor in the rescue order (Tor second). The design review's spec wanted the carpenter first. It changes an approved order, so it is offered, not recommended.

### 3.6 A profession line for every rescued person

**The pattern** (Liv's, plus the parts it lacks):
1. **Lesson.** Talk To the person: their topic is the lesson.
2. **Do it once at home** with what you have, at their station.
3. **A raid step:** a tool or a find from a place tied to their past.
4. **Growth:** something the player gives or builds that raises their rank or station.
5. **Capstone:** they give something of their own.

**One personal beat per line**, after the first useful step:
- a memory;
- a flaw or a secret (Bloody Baron rule: after the player likes them);
- or a wish.

**Rules for the lines:**
- Steps run one at a time.
- Step 1 starts by itself at the homecoming. Later steps start when the player talks to them (When Player Does: Talk To the person), shown by a "!" (§3.9).
- Rewards:
  - the tool or currency for the next step;
  - their worker rank (`rewardWorkFor`);
  - a lesson;
  - one lasting thing at the end.
- Lines are short. About 10 per person, 15-30 words each.

**Hooks into the systems being built now:**
- **The NPC window** shows each line's step ("Ingrid · 2 of 5").
- **Huts and traits:** a personal beat can ease a trait: Bjarne's slow arm, Tor's leg, Asa's lost time, Gunnar's broken luck.
- **House tiers:** Tor's and Yrsa's capstones are house upgrades.
- **Timed crafting:** Squared Stone and Cured are good first timed crafts.
- **The harvest bar:** Asa's planting step.

Objective types marked **(new)** are in §3.9.

#### 3.6.0 Homecoming memories for the eight without one (8 lines, about 330 words)

Each answers "what do you remember of the night the door broke" (rule 6 of the approved plan) and plants one thread.

| Person | `welcomeLine` (proposed) | Plants |
|---|---|---|
| Eirik | "Three days I held that clearing. At night something in Granheim mail walked the edge and never came in. I knew the walk. I couldn't give it a name, and I know every man I stood beside." | The draug; his forgetting |
| Yrsa | "Your fire's too small to cook on and too big to hide. Good. A fire is how lost people find a table." | Food as a beacon (her line) |
| Gunnar | "Twenty years I never lost a path in these woods. This year the paths started moving. Then the deer stopped running from me and started running from something else." | The push from the high woods |
| Liv | "The stones broke along a line, straight as a chalk mark. Stones don't break straight. Somebody drew that line, and I want to know who." | Act II: the doors |
| Asa | "I'm from Holt, past the ridge. I walked home last spring and the road went somewhere else, and Holt wasn't at the end of it." | The time slip; a forgotten village |
| Runa | "I found my own marks on the wrong trees. Then I followed your light. A light doesn't move. That's the only reason I'm here." | Her trade; the hearth as a fixed point |
| Hild | "Your hearth isn't level, your footings are soft and your walls are wood. Who cut these blocks? Not bad. I'll cut them from now on." | **The stone-block payoff** |
| Sigrid | "I heard your fire before I saw it. Every night a little louder. I knew it was one of mine. Nobody else in a hundred years would be fool enough to light one." | Her pride in the player |

#### 3.6.1 Ingrid, the Alchemist: "Bread on the Stones"

| # | Quest | Teaching | Objectives | Reward | Personal beat |
|---|---|---|---|---|---|
| 1 | **Squared Stone** (new, §3.5) | The Workshop's stone bench; the pick | Talk To Ingrid; Craft Item Stone Block ×16 | 2 Life Orb; Ingrid +10 | – |
| 2 | **The Alchemist** (existing, text rewritten) | The altar; Life Orbs at home | Assign Job Alchemist; Build Structure Purification Altar | 4 Cursed Essence (existing) | – |
| 3 | **What the Curse Leaves** (new) | Cursed Essence is the raid's; the altar turns 2 into 1 Purified while she tends it; Purified pays the portal's tier II | Gather Resource Cursed Essence ×8; Talk To Ingrid | 3 Purified Essence; her first Alchemy lesson (set Alchemist › Teaches = Alchemy: data) | Her grandmother was taken into the hill as a girl, came back a year later without eating a crumb of theirs, and left bread on the stones every winter after |
| 4 | **Bread on the Stones** (new; raid) | Offering stones in raids; some things are answered with a gift, not a blade | Craft Item Stewed Berries; Use Object *Offering Stone* (a Quest Target on the existing Offering Stone prop, placed in a raid set piece) | A trinket of hers (new item, S data and an icon), or Ingrid's rank | She admits she knew what stones like these do (Sigrid: "Ingrid knew that when she came") and came anyway, to see where her grandmother went |
| 5 | **A Draught for the Road** (new; after Tor) | The Cauldron; Orb Tincture; healing draughts | Build Structure Cauldron; Craft Item Healing Draught ×3 | She heals more on raids: her companion's heal share, if it is a field, else S code; Ingrid +30 | She asks the player to come back with the draughts unopened |

Lines (Ingrid, 9; the portal 0):
- **Step 3:**
  - Offer: "Bring me what the woods leave in you. The black essence. I'll show you what it is when it's clean."
  - Personal: "My grandmother was taken into the hill as a girl. A year, she said, and she never ate a crumb of theirs."
  - Personal: "She left bread on the old stones every winter after. So they'd remember she'd paid."
  - Done: "Clean. Feed that to the portal, and it will hold for you."
- **Step 4:**
  - Offer: "There's an offering stone out there. Leave something warm on it. Not for them. For her."
  - Done: "Did anything take it? ...Good. Then they still know her name."
  - Admission: "Sigrid told us what stones like that do. I came anyway. I wanted to see where she went."
- **Step 5:**
  - Offer: "A cauldron, now we have planks. Three draughts, and I'll stop worrying every time you go."
  - Done: "Take them. And bring them back unopened, if you can."

Hooks:
- **Raids:** offering stones; Cursed Essence.
- **Colony:** the portal's tier II through Purified Essence; the Cauldron.

#### 3.6.2 Bjarne, the Smith: "Kept It Lit"

| # | Quest | Teaching | Objectives | Reward | Personal beat |
|---|---|---|---|---|---|
| 1 | **A Smith's Hearth** (existing; text names rough planks) | The Smithy, repairs at half | Assign Job Smith; Build Structure Smithy; Repair Item | – | – |
| 2 | **One-Handed** (new; after the Lodge) | Smelting copper (ore, charcoal); forging a weapon; quality | Craft Item Copper Ingot ×3; Craft Item Copper Sword | Weaponcrafting lesson; Bjarne +15 | His arm: something in the woods held it while he slept, and it hasn't quite let go. A trait the line eases |
| 3 | **The Iron on the Road** (new; raid) | Grave iron from barrows; iron binds the hidden folk; why the road was safe once | Collect Item Grave Iron ×3; Use Object *Iron Marker* (a Quest Target on a waymarker prop in a raid set piece); Talk To Bjarne | 2 Iron Ingot; Bjarne +15 | He recognises the smith's mark on the marker: the same hand as the "last smith" note on the board. Whoever kept this hearth set the iron |
| 4 | **Kept It Lit** (new) | Smithy II: copper tools, and the portal's tier III | Upgrade Building Smithy tier 2 | His mark: his rank +30 (faster ingots); a line repaying Eirik | "Tell Sigrid I kept it lit" (his fallen line) becomes a promise kept |

Lines (Bjarne, 9):
- **Step 2:**
  - Offer: "I can hold the tongs. I can't swing. You swing, I'll tell you when."
  - Mid: "Harder. It's copper, it won't bruise."
  - Done: "That's a blade. Ugly. Yours."
  - Personal: "The arm's not broken. Something held it while I slept out there, and it's never quite let go."
- **Step 3:**
  - Offer: "The old road had iron in it, every mile. Someone pulled it. Find me a marker that's still standing."
  - Done: "Same mark as the note on your board. Whoever kept this hearth set that iron. A hundred winters, and it still bites."
- **Step 4:**
  - Done: "When she's home, tell Sigrid I kept it lit. Don't tell her it went out twice."
  - Aside with Eirik: "Here. What I owe you. Don't count it."
  - Aside with Eirik, Eirik's reply: "I'll count it."

Hooks:
- **Raids:** barrows, Draugr, grave iron.
- **Colony:** portal tier III needs Smithy II; repairs.
- **Lore:** the hearth-keepers set iron on the road (§3.8).

#### 3.6.3 Tor, the Carpenter: "A Week, I Said"

| # | Quest | Teaching | Objectives | Reward | Personal beat |
|---|---|---|---|---|---|
| 1 | **The Woodcutter's Lodge** (existing) + a new 3rd objective | Planks and charcoal at the Lodge | Assign Job Carpenter; Build Structure Lodge; Craft Item Oak Plank ×8 | Woodcutting lesson | – |
| 2 | **Raise the Walls** (existing; now Tor's) | Palisades | Build Structure Palisade ×4 | – | – |
| 3 | **A Growing Home** (existing; now Tor's) | Storage, a hut | Storage Shed; Hut | 2 Healing Draught (existing) | His leg: he lay three nights listening to the hollow breathe. He builds loud so he doesn't hear the quiet |
| 4 | **Heavy Loads** (existing; now Tor's) | Lodge II, the Yard, carts | Upgrade Lodge to 2; Yard; Wheelbarrow | – | – |
| 5 | **It's Built Crooked** (new; with the house tiers being built) | House tiers and comfort | Upgrade Building Hut to tier 2 (or the house system's first tier) | Building repairs at a bigger discount (Carpenter data); Tor +30 | His arrival line ("It's built crooked. Give me a week") comes true |

Lines (Tor, 7):
- **Step 1:** Offer: "Planks first, then everything else. And char for Bjarne's fire, or he'll sulk."
- **Step 2:** Offer: "Four lengths between the fire and the trees. I'll mark where."
- **Step 3:**
  - Personal: "I build loud. Saws, mallets. Out there it was so quiet I could hear the ground breathe."
  - Done: "A roof. Somebody can sleep under it now."
- **Step 5:**
  - Offer: "I said it was built crooked. I said give me a week. It's been a week."
  - Done: "Square and true. Now it'll outlast both of us."
- **Pair aside:** "Bjarne, your chest. I borrowed it. It's in the shed."

Hooks:
- **Colony:** the existing three Haven quests become one voiced chain. This is the cheapest line, since most of it already exists.
- **Raids:** carts carry more home.

#### 3.6.4 Eirik, the Warden: "Twenty Winters"

| # | Quest | Teaching | Objectives | Reward | Personal beat |
|---|---|---|---|---|---|
| 1 | **The Watch** (existing) | Watch Post; the ward holds 1.5× | Assign Job Warden; Watch Post | – | – |
| 2 | **Hold the Line** (new) | Preparing a breach: traps where the path narrows, guards | Build Structure Spike Trap ×3; Defend Breach (any) | Melee lesson; Eirik +15 | – |
| 3 | **Her Name in Stone** (new; no raid) | Names: the forest can't forget what is tended at the hearth | Talk To Eirik; Build Structure Rune Stone (existing decor) beside the fire | Eirik +20; his bond rank +1 if a bond objective exists | At the tree line he had to think for his sister's name, Sigrun. He has the player cut it in stone by the fire |
| 4 | **Sworn** (new) | Companions and bonds (Trusted at 3, Sworn at 8) | **(new)** Reach Bond Sworn with Eirik, or Complete Raids ×5 with Eirik along | His guard holds longer in a breach (Warden data), or a Granheim shield (new item) | He says the shield-kin words of Granheim |

Lines (Eirik, 8):
- **Step 2:**
  - Offer: "Next time they test the ward, they meet a plan. Three spike traps where the path narrows."
  - Done: "That's how a line holds. Not by being strong. By being where they have to come."
- **Step 3:**
  - Offer: "At the tree line I had to think for my sister's name. Sigrun. I'm saying it now so I keep it."
  - Ask: "Cut it in stone for me. Here, by the fire."
  - Done: "Sigrun. There. Let the forest try."
- **Step 4:**
  - Done: "Eight times out and back. In Granheim that makes you shield-kin."
  - Done, continued: "Here it makes you a fool who keeps bringing me."

Hooks:
- **Colony:** breaches, traps, the guard.
- **Story:** Act II can bring Sigrun back as one of the Lost (§3.7.3, optional).

#### 3.6.5 Yrsa, the Cook: "The Jarl's Table"

| # | Quest | Teaching | Objectives | Reward | Personal beat |
|---|---|---|---|---|---|
| 1 | **Show Me Your Kitchen** (existing; text names 12 Stone Blocks and 4 Copper Ingots) | The Kitchen | Assign Job Cook; Kitchen | – | – |
| 2 | **What You Eat** (new) | Meals with lasting bonuses; eat before a raid; two meal slots | Craft Item Hunter's Stew; **(new)** Consume Item Hunter's Stew | Cooking lesson; Yrsa +15 | – |
| 3 | **The Jarl's Table** (new; raid) | Foraging the deep woods; the Seed rumour; farming what you forage | Gather Resource Cloudberries ×3; Gather Resource Chanterelle ×6; Craft Item Trout in Cream | Recipe Farm Cloudberries (known 0 today); Yrsa +20 | She cooked for Jarl Ottar of Granheim. The night his household walked into the trees she had made his favourite, and she has kept cooking it since, so they can smell their way home |
| 4 | **A Feast for the Haven** (new) | The Haven Feast; Kitchen II | Craft Item Haven Feast; Upgrade Building Kitchen tier 2 | The second meal slot comes from Kitchen II (existing); Yrsa +30 | Everyone at one table |

Lines (Yrsa, 8):
- **Step 2:**
  - Offer: "Show me what you eat. ...Berries. In a skin. Sit down and watch."
  - Done: "Now you'll walk further and hit harder. Food is the first armour."
- **Step 3:**
  - Offer: "Cloudberries and chanterelles, from the deep woods. I need them for a dish I owe someone."
  - Personal: "Jarl Ottar's favourite. I made it the night his hall walked into the trees. I've been making it since."
  - Personal, continued: "In case one of them smells it."
  - Done: "Plant the berries. Then we never have to go that far for them again."
- **Step 4:**
  - Offer: "Everyone at one table. That's how you tell a hall from a camp."
  - Done: "There. Now it's a hall."

Hooks:
- **Raids:** foraging, the Seed rumour.
- **Colony:** Asa's farm (cross-person), house kitchens (the comfort system being built).
- **Lore:** Granheim's jarl (§3.8).

#### 3.6.6 Gunnar, the Hunter and Fisher: "The Forest Woman's Due"

| # | Quest | Teaching | Objectives | Reward | Personal beat |
|---|---|---|---|---|---|
| 1 | **Hides and Hooks** (existing) | The Hunter's Lodge | Assign Job Hunter; Hunter's Lodge | – | – |
| 2 | **Cured** (new) | Leather; why copper armour needs hide; bows | Craft Item Cured Leather ×4; Craft Item Hunter's Bow | Archery lesson; 20 Crude Stone Arrows | – |
| 3 | **The Forest Woman's Due** (new; raid; skogsrå) | Bargains with the woods; offering stones; restraint | Kill Monster Deer ×1; Use Object *Offering Stone* | Gunnar +20; a relic or a better hide drop for a while (a raid outcome exists: `GiveRelicOutcome`) | Twenty years she gave him game for the best of every third kill. Last winter he kept it. Then the pit |
| 4 | **Never Say Its Name** (new; Fisher) | The Dock, still water, the Nøkken | Build Structure Dock; Catch Fish (Pike) ×1; **(new or a Story Event)** meet the Nøkken (`NokkenEncounter`) | Fishing lesson; Dock II plan or Gunnar +30 | On still water you don't say its name, unless it has someone |

Lines (Gunnar, 8):
- **Step 2:**
  - Offer: "Hides rot in a stash. Cure them. Then we'll see if you can shoot."
  - Done: "It pulls straight. Better than mine."
- **Step 3:**
  - Offer: "Twenty years she gave me game, and I gave her the best of every third. Last winter I kept it."
  - Offer, continued: "Then the pit. Take her what I owe."
  - Done: "Did she show herself? ...No. She never does. But the deer will come back."
- **Step 4:**
  - Offer: "On still water you don't say its name. Unless it has someone. Then you say it loud."
  - Done: "It looked at you. And it let you go. That's a good lake."
- **Pair aside** with Runa: "You poached the jarl's deer." Runa's reply: "You poached mine."

Hooks:
- **Raids:** hides, deer, offering stones.
- **Colony:** leather for copper armour; the Dock.
- **Lore:** the skogsrå and the nøkk (§2.7).

#### 3.6.7 Liv, the Rune Scholar: the existing line, finished

The five existing quests stay as they are. Add:
- **Voice her lesson.** Split `EnchanterTalk.Lesson` into four lines of about 30 words, and give the two replies forms without a name: "I've seen this hand before, but not this way of closing the loop. Thank you." and "Here, follow the line with me. There. You can carve it now." About 6 lines.
- **Two personal beats in existing quests** (dialogue only, said when they complete):
  - Rubbings: "My teacher took rubbings of every stone between here and the sea. She said they made a map. I laughed at her."
  - The Great Work, when she teaches Thornguard: "This one's mine. I cut it into my wrist the night the stones broke, so I wouldn't lose my own name."
- **A sixth quest, The Line** (new; the Act II hook). Teaches the carved stones of Troll Valley and the map of the doors.
  - Objectives: Use Object *Carved Stone* in Troll Valley (the existing Quest Target, placed in a Troll Valley set piece); Learn Enchantment ×1; Talk To Liv.
  - Reward: recipe Wayfinder Rune (a raid rune, known 0 today); Liv +30.
  - Lines: "The line runs north. Every door on it was woken, and every one was broken after. Somebody's opening them, and something's closing them."

Voice: about 10 lines. Hook: Act II begins in her hands as well as the portal's.

#### 3.6.8 Asa, the Farmer and Weaver: "Who's Been Using It?"

| # | Quest | Teaching | Objectives | Reward | Personal beat |
|---|---|---|---|---|---|
| 1 | **Good Dirt** (existing) | The Farm Plot | Assign Job Farmer; Farm Plot | – | – |
| 2 | **The Last of My Village** (new) | Planting from the stash's seed; crops grow during raids; harvest (the harvest bar being built) | Craft Item Barley ×8 (a crop is a recipe at the Farm Plot: check that a harvest raises Crafted) | Foraging lesson; Asa +15 | The sack is the last of her village |
| 3 | **Flax and Linen** (new) | The Tailor's Shop; linen gates iron gear (Iron Sword, Iron Pickaxe) | Craft Item Flax ×3; Build Structure Tailor's Shop; Craft Item Linen ×4 | Armorcrafting lesson; Asa +15 | – |
| 4 | **Who's Been Using It?** (new; Haven world) | The Haven map's places; the clearing was farmed long ago | Reach Place *Burnt Farmstead* (a Quest Target at the Haven atlas place); Use Object *Old Furrow* (a marker there) | Recipe Farm Chanterelles (known 0 today); Asa +30 | **The time slip:** she says her village burned "last spring", and stops |

Lines (Asa, 8):
- **Step 2:**
  - Offer: "This is the last of my village. In a sack. Let's not eat it all at once."
  - Done: "Four from one. That's how a village comes back."
- **Step 3:**
  - Offer: "Iron's cold against the skin. Linen under it, or you'll blister by the second hill."
  - Done: "Tight. Every seam. I'd wear it."
- **Step 4:**
  - Offer: "There's furrows under your moss. Old ones. Somebody farmed this clearing before the trees came back."
  - Personal: "Holt burned the spring I was married. Last spring. ...It was last spring. Wasn't it?"
  - Done: "Whoever they were, they grew the same barley as us. So we'll grow it again."

Hooks:
- **Colony:** food, linen for tier III gear.
- **Lore:** the strangers were taken in earlier wakings, and the mist slides time (§3.8).

#### 3.6.9 Runa, the Tracker: "Someone Is Paying"

| # | Quest | Teaching | Objectives | Reward | Personal beat |
|---|---|---|---|---|---|
| 1 | **Eyes in the Woods** (existing) | The extra rumour | Assign Job Tracker | – | – |
| 2 | **Read the Ground** (new; night raid) | Rumours have hours; night raids; Sneaking | Use Object *Night Cairn* (a Quest Target at the existing Night Cairn rumour site, which stands only after dark); Complete Raids ×1 [extract] | Sneaking lesson; Runa +15 | – |
| 3 | **Someone Is Paying** (new; personal flaw) | – (story) | Talk To Runa; Reach Place *Cold Camp* (a Quest Target in a raid set piece: a cold camp, a pot, grey ash) | Runa +20 | A man in grey paid her in silver to walk with Sigrid and tell him which stones she woke. She never told anyone |
| 4 | **Big Tracks** (new; Troll Valley) | Troll Valley; reading the old troll's road | Reach Place *Troll's Road* in Troll Valley; Complete Raids Troll Valley ×1 [extract] | Runa +30; a foretold event in briefings (Watch Post-style, if Tracker data allows) | – |

Lines (Runa, 8):
- **Step 2:**
  - Offer: "You walk like a cart. Tonight you walk like me. There's a cairn that's only there after dark."
  - Done: "You touched it and came back. That's tracking. The rest is waiting."
- **Step 3:**
  - Offer: "I told you someone was paying. It was me they paid."
  - Offer: "A man in grey. All silver and no face I can keep. He wanted to know which stones Sigrid woke."
  - Done: "Ash and a cold pot. He's been here. He's always been here first."
- **Step 4:**
  - Offer: "I've tracked things my whole life. Never one that big."
  - Done: "And never one that walked so carefully round a door."

Hooks:
- **Raids:** night, sneaking, Troll Valley.
- **Story:** the Waker's first face (§3.8).
- Her flaw comes after two steps, so the player already likes her.

#### 3.6.10 Hild, the Mason: "A Wall Round Nothing"

| # | Quest | Teaching | Objectives | Reward | Personal beat |
|---|---|---|---|---|---|
| 1 | **Stone on Stone** (existing) | The Mason; the Cottage | Assign Job Mason; Cottage | – | – |
| 2 | **Granite** (new) | Granite Block (1 Grey Stone + 4 Granite → 2); Workshop III; Stone Walls that don't burn | Craft Item Granite Block ×4; Upgrade Building Workshop tier 3; Build Structure Stone Wall ×4 | Construction lesson; Hild +15 | – |
| 3 | **The Sleeping Quarry** (new; Haven world) | Where stone is at home; sun-caught trolls | Reach Place *Old Quarry* (a Haven atlas place, Quest Target); Use Object *Sleeping Troll Rock* (the existing prop, placed there) | Granite nodes opened at the quarry (overgrowth cleared); Hild +15 | – |
| 4 | **A Wall Round Nothing** (new) | Lit boundary stones keep land remembered (the Haven's Reach) | Story Event Land Stone Lit [owned] (existing); Build Structure Rune Stone | Stone buildings resist breaches better (data on Stone Wall or the Mason); Hild +30 | Her house: the forest forgot it while she stood in it, so she walled where it was, so somebody would remember |

Lines (Hild, 8):
- **Step 2:**
  - Offer: "Palisades burn. Stone sulks, but it stands. Granite, and I'll show you a wall."
  - Done: "Now that's a wall. Lean on it. Go on."
- **Step 3:**
  - Offer: "That's no rock. Look at the brow. The sun caught him, long ago."
  - Done: "Best stone in the valley, and he won't mind. Much."
- **Step 4:**
  - Personal: "It was my house. The forest forgot it while I was standing in it."
  - Personal, continued: "So I walled where it had been. So somebody would remember."
  - Done: "Your stones out there do the same. Keep them lit."

Hooks:
- **Colony:** walls, territory (Haven's Reach), the block bench.
- **Lore:** trolls turn to stone in sunlight (§2.7).

#### 3.6.11 Sigrid, the Leader: "Seven Went In"

| # | Quest | Teaching | Objectives | Reward | Personal beat |
|---|---|---|---|---|---|
| 1 | **Under One Banner** (existing) | +2 min a raid | Assign Job Leader; Haven Banner | – | – |
| 2 | **Seven Went In** (new) | Everyone's place at the Haven (the NPC window) | Talk To Ingrid, Bjarne, Tor, Liv, Eirik, Runa (6 Talk To, any order) | Sigrid +20 | Guilt: she sent them |
| 3 | **What Sigrid Knows** (Main, the end of Act II, §3.7) | – | Talk To Sigrid; Talk To the Portal (a Story Event at the conversation's end, per the memory's trap) | Act III opens | §3.8 |
| 4 | **The Horn** (new, optional) | A raid horn: call your companion back to you, or one more minute once a raid (new item) | Craft Item *Expedition Horn* at the Workshop | The horn | The three long notes are the expedition's signal |

Lines (Sigrid, 6, plus Act II):
- **Step 2:**
  - Offer: "Seven went in. I want to see every one of them with my own eyes."
  - Done: "All home. I sent them in. I won't forget that you went and got them."
- **Step 4:**
  - Offer: "Three long notes and a rest. Learn it. If you ever hear it, come."
  - Done: "Now they'll hear you, too."

#### 3.6.12 Voice cost of the lines

| Part | New lines | About words |
|---|---|---|
| Homecoming memories | 8 | 330 |
| Ingrid, Bjarne, Tor | 27 | 600 |
| Eirik, Yrsa, Gunnar, Liv, Asa, Runa, Hild, Sigrid | 64 | 1,350 |
| Pair asides | 6-10 | 100 |
| Squared Stone and the portal's once-lines | 3-6 | 80 |
| **Total for §3.6** | **about 110** | **about 2,500** |

That is about a quarter of today's 437 VoiceBank lines. Every line goes through Safe Haven > Voice Lines (the voice-lines memory note), and the
listening check is the user's.

### 3.7 Story progression quests to add

#### 3.7.1 Act I patches (S-M)

- **The figure in mail in raid 1** (planned, left out): a Draug in Granheim mail at the edge of Ingrid's site that walks away and does not attack. Story graph in the raid, or a set-piece guard with a flee behaviour. No voice.
- **The vardøger the night before a rescue** (S): a Haven story at dusk after a rescue rumour first comes up. Footsteps and a door creak by the fire, no one there. No voice. Teaches nothing; it makes the next rescue feel expected.
- **The Road North keeps Sigrid's horn** (data): move the portal's horn line from the Hamlet moment to the end of The Road North, as planned. The Hamlet keeps a new line (1 voice line, the portal).

#### 3.7.2 Act II: "The Doors" (L; six Main quests and a boss)

Act II starts after The Road North and runs alongside rescues 4-11. Each quest is an event (the approved rule).

| # | Quest | Starts | Objectives (existing types) | What happens | Teaches |
|---|---|---|---|---|---|
| 16 | **Where the Old One Walks** | After The Road North | Reach Place *Troll's Road* (Troll Valley set piece); Complete Raids Troll Valley [extract] | The troll's tracks go round the doors, never through | Troll Valley, tier II raids |
| 17 | **A Door Broken** | After 16 | Use Object *Broken Waystone* (a broken Standing Stones set piece in Troll Valley); Craft Item Blank Parchment; Use Object Carved Stone | A ring woken from outside and smashed the same night. If Liv is home, she reads it. Else the portal says what it can | The doors exist; someone wakes them |
| 18 | **The Man in Grey** | After 17 | Reach Place *Cold Camp* (raid); Talk To the Portal (Story Event at its end) | His camp; the line of doors points north. Runa's step 3 is the same place (one target, two quests) | The antagonist has a face |
| 19 | **The Toll** (boss) | After 18 and Hild home (so it falls near rescue 10) | Kill Monster *The Old One* [owned], **or** Story Event *Toll Paid* (a choice at the Toll Troll event, made the boss's site) | The old troll at the next door. Fight, or pay: a Hearth Ember and Life Orbs, the ordinary thing that matters (Askeladden) | Act II boss; "defeat or negotiate" (GDD §4.4) |
| 20 | **The Horn at Dusk** | Existing rescue, its site moved to Troll Valley past the troll | Rescue Person Sigrid [home] | Sigrid is past the troll country, as the approved plan said | – |
| 21 | **What Sigrid Knows** | After 20 | Talk To Sigrid; Story Event at the portal | Her truth, and the answer to "why me" (§3.8). Opens Act III | – |

Effort and voice:
- **The boss:** a `BossDefinition` and phases for the old troll on the Berg-jutul or Stone Troll body, an arena and a wake. L.
- **The rest:** Quest Targets, two set pieces and story graphs. M each.
- **Voice:** about 40 lines, mostly the portal's (Qwen) and Sigrid's.

#### 3.7.3 Act III outline (to agree before writing)

- **Where:** north to the Frost Peaks and the first hearth, where the pact was made.
- **Who:** the Waker's purpose (below), the hunger itself, a marsh guardian in the Drowning Fen (the GDD's boss ladder: forest guardian, troll chieftain, marsh spirit, mountain ruler).
- **The end:** the player, as Hearth-keeper, sets the terms of a new pact (GDD §4.1: "may influence the terms of a new one").
- **Optional:** Eirik's sister Sigrun comes back as one of the Lost (the existing Seeks Lost rumour flow: a resident, a hook to put her in `LostToFind`, and a site choice only Eirik can make: M).

#### 3.7.4 Tales from what already exists (M; about 20 lines)

Each starts When Player Does: Reach Place or Use Object at an existing raid event's site, as Echoes in the Wilds does. Each has a bargain,
a riddle or a twist (the approved definition of a Tale).

| Tale | Built on | The turn |
|---|---|---|
| **The Crying in the Fen** | A new small site, or Living Stones | An utburd: name it ("I name you on a hope") and it rests, giving a blessing for the raid |
| **The Toll** | Toll Troll event | Pay, trick or fight; the trick is the Askeladden way |
| **Three Wishes** | Three Wishes event | A wish answered too literally |
| **The Wild Hunt** | Oskorei event, in winter | Steel over the Haven's doors that night, or the horses are found spent at dawn |
| **The Pedlar's Pack** | Trapped Pedlar event | What he carries belongs to someone taken |
| **The Barrow Lord** | Old Barrow event | A draug who remembers his name only if you say it |

### 3.8 The lore: a bible that keeps faith with what exists

#### 3.8.1 The clearing and the portal

- **What it is.** The portal is a door, the last kept door of the old pact. It never names itself.
- **What it was.** The clearing was a farmstead and a hearth: Asa's old furrows, the "last smith's" note, Bjarne's iron on the road.
- **The proposed Act III reveal.** The portal is what the first hearth-keeper became when the fire went out: a *gardvord*, the farm's first settler who guards it from his mound (§2.7). That is why it says "a hundred winters I kept this clearing alone" and why it can't remember why the stones woke for the player. It can't see itself.
- **Consistency.** Keep "I am a door". Never say "tree".

#### 3.8.2 The mist

- **What it is.** The mist is the forgetting made visible. Inside it the forest has let go of roads, names and time. It is the bergtatt country: the "land that forgot us" (the portal).
- **What it does.** The longer you stay, the more it takes (the Misty Woods cinematic: "the deeper its corruption grows").
- **Rules to keep:**
  - The forest does not kill the lost; it forgets them.
  - A tended hearth is what it can't forget.
  - Names have power.

#### 3.8.3 Why people are taken

- **The doors.** The waystones were doors between places of shelter (Liv's teacher). With a hearth beside them they carried people home. Woken cold, without a hearth, they scatter whoever stands near into the mist (Sigrid: "It took everyone who stood too near, and it put them somewhere else").
- **The strangers.** Yrsa, Gunnar, Asa and Hild were taken by earlier wakings, some years ago. Time slides in the mist, so Asa's "last spring" was years back. This explains why eleven come home when the portal counts seven sparks. The portal counted the seven it heard calling, and the others had stopped calling.
- **Those who ate in the mist** came back with something the forest kept: Bjarne's slow arm, Tor's leg. These become traits that the lines ease.

#### 3.8.4 The Heartroot

- **What it was.** The forest's guardian, its half of the pact (built).
- **What it did.** It held the hunger off for ninety winters after the hearth went cold.
- **Proposed addition.** The pact had three keepers:
  - people, with the hearth;
  - the forest, with the Heartroot;
  - the mountain, with the old troll.
- **What the player has.** Its heartroot by the fire is a debt acknowledged. Heartroot's Grasp is what it knew of the forest's hold.

#### 3.8.5 The antagonist force

Three options. The first is recommended.

| Option | What it is | For | Against |
|---|---|---|---|
| **A. The Hunger and the Waker (recommended)** | The hunger is the "older, hungrier things" under the forest (the opening's words), which ate into the Heartroot's roots. The Waker, "the man in grey", is a person who has been waking doors along a line for years, to bring back his own taken family. Every cold door he opens lets the hunger further in | A face, a sympathetic motive (Bloody Baron); explains the recent speed-up, "someone opened it on purpose", "someone is paying", the strangers and Liv's line; uses Runa's flaw | Needs an Act III confrontation and a moral choice |
| B. The Hunger alone | An impersonal force; lore in pieces (Outer Wilds, Dredge) | Cheap; mysterious | No face; the tone rule wants motives |
| C. A draug-jarl of Granheim | Jarl Ottar's household, forgotten, leading the forgotten | Ties Eirik, Yrsa and Runa | Makes the dead the enemy; Askeladden tone prefers bargains to armies |

**The old troll is not the enemy.** It is the mountain's guardian, and it smashes the doors the Waker opens before the hunger comes through. "It didn't come for us. It came for the door" (Ingrid, built) then pays off. The player's portal is a door, so the troll is a real threat to the Haven too. That is why Act II can end in a fight *or* a toll.

#### 3.8.6 Why the stones woke for the player

"You kept their fire."
- **The prologue:** the player built the campfire at the rock shelter, the party's hearth, for the others. Sigrid leads, and others cook.
- **The rule:** the stones wake for a hearth-keeper's hand, as the Heartroot's last word says.
- **Why this answer:**
  - no bloodline, which suits Askeladden;
  - it is consistent with "I did not teach it to you";
  - it can be said in one line at the end of Act II, by Sigrid or the portal.
- **Proposed line:** "You lit their fire and kept it. The stones know that hand. They always did."

#### 3.8.7 The second act, names and places

**Granheim.** Eirik's town, with a shield wall. **Jarl Ottar's** hall stands there (a proposed name). His household walked into the trees one
winter after a feast. The figure in Granheim mail is one of his housecarls.
- Runa poached his deer.
- Yrsa cooked for him.
- Eirik stood in his wall.

**Holt** (proposed): Asa's village past the ridge, which the forest forgot.

**Act II places:**
- **The Troll's Road:** Troll Valley.
- **The Broken Doors:** waystone rings smashed by the old troll.
- **The Cold Camp:** the Waker's camp.

**Act III places:**
- **The First Hearth:** in the Frost Peaks.
- **The Drowning Fen:** the marsh guardian.

**The Haven's own places:**
- the Old Quarry (Hild);
- the Burnt Farmstead (Asa);
- the Farewell Stones and Drowned Stones (boundary stones of the Haven's Reach);
- the Grandmother Cedar (the old offering tree, a Tale).

#### 3.8.8 Contradictions to fix (S; text only unless noted)

| Where | Now | Fix |
|---|---|---|
| `Data/Prologue/Quests/DefendCamp.asset:17` | "Ingrid can tend your wounds" | "Eirik will help, and Sigrid can tend your wounds" (she heals at half health, per `Docs/TODO.md:385`) |
| `Data/Prologue/ForestPrologue.asset:185` | "You wake beneath a living canopy" | "You wake in the grass at the foot of old stones" |
| `GameDesignDocument.md:155` | "wakes beside its roots" | "wakes at the foot of its stones" |
| `Data/Raids/Rumours/RotsHeart.asset:16` | "Runa says" | "the portal says", or Runa's words only when she is home (a variant) |
| `Data/Settlement/Requests/wolf-fangs.asset`, `order-masterwork-mail.asset` | "the children" | "the young ones" (Eirik's word), or keep for a later family system |
| `GameDesignDocument.md:887` (§15.3) | Old rescue order; Leader 30 s | The data's order; 120 s |
| `ARCHITECTURE.md:74` (GDD status table, §15) | "One recruit (Ingrid)…" | Eleven residents, bonds, memories, asks, lessons, worker ranks |
| The portal's "seven" | Seven sparks; eleven come home | Keep "seven" (the expedition). Add one portal line at the first stranger's rescue: "Not one of mine. Taken long before. I had stopped hearing her." (1 voice line) |
| Timescale | The Heartroot fell about 10 winters ago; the change felt "last month" | The Waker's wakings sped it up this last year (§3.8.5). Say it in Act II |

### 3.9 New systems the lines need, and their effort

| System | What | Effort | Notes |
|---|---|---|---|
| Reachability audit | `QuestAudit.AskedBeforeReachable()`: walk the quest graph in rescue order, accumulating buildings and recipes known (from the start, by residents' jobs, by quest rewards). Flag each Build, Craft or Upgrade objective whose cost has no recipe at a station the player can have by then, and no raid source. A `QuestChecklist` row with a fix: "Add a Craft step before it" | M | Would have flagged The Alchemist (no Workshop prerequisite), A Smith's Hearth (planks), The Smith's Request (charcoal), the Cauldron |
| Homecoming release | §3.4.3 | M | `QuestPacingTests`, a new `QuestFloodTests` |
| Build menu "made at" | §3.5 | S | `BuildMenuWindow.ShowCosts` |
| **Objective: Consume Item** | Counts `GameEventKind.Consumed` (already raised, `ItemInventory.cs:382, 416, 497`) | S | Co-op rule: counts for the eater |
| **Objective: Take Lesson** | Raise a new `GameEventKind.LessonTaken` in `ExperienceTracker.TakeLesson` (`Progression/ExperienceTracker.cs:219`) | S | Teaching steps: "take a lesson from Bjarne" |
| **Complete Raids "with someone"** | A Resident field on the objective, met when that person went along (`RaidSession.Companion`) | S | Eirik's Sworn |
| **Objective: Reach Bond** | Bond rank with a person (`CompanionBond`) | S | Counts a rank already reached |
| **Objective: Give Item** | Hand an item to a person in conversation; generalises Give Inscription | M | A talk topic from data |
| Job talk topics as data | `JobDefinition` › Talk Topics: a question, an answer (voiced), shown while a quest is in a state. `ResidentNpc.AddTopics` reads them. `EnchanterTalk` keeps its live parts (reach, inscriptions) | M | One system, not eleven classes. Lines are voiced by speaker and text (`ARCHITECTURE.md` › Spoken dialogue) |
| "!" over a resident | When a quest starts When Player Does: Talk To that resident, or a step waits on them | S-M | Also shown in the NPC window being built |
| Pair asides | `SettlementSettings` › Pair Lines: two residents and a line each, said once when both are home | S-M | Bjarne and Eirik's debt; Tor's borrowed chest |
| Quest Targets in scenes | Offering Stone, Iron Marker, Old Quarry, Burnt Farmstead, Old Furrow, Night Cairn, Cold Camp, Troll's Road, Broken Waystone | S each (scene work) | Editor work for whoever holds the Editor |
| A raid choice only a companion can make | A Raid Event choice condition: companion is X | S-M | Eirik naming his sister (optional) |
| The old troll boss | `BossDefinition`, phases, an arena set piece, a wake | L | Act II |

Every new objective type needs a rule in `QuestObjective.InAFriendsHaven` and a case in `QuestCoopTests` (memory: quest deeds).

### 3.10 What it costs in voice, in total

| Phase | Lines | Who |
|---|---|---|
| 0 | 3-4 | Ingrid, the portal |
| 1 | about 45 | 8 memories, Ingrid, Bjarne, Tor, Liv's lesson |
| 2 | about 75 | Eirik, Yrsa, Gunnar, Asa, Runa, Hild, Sigrid, Liv, pairs, Tales |
| 3 | about 40 | Act II (mostly the portal and Sigrid) |
| **All** | **about 165** | |

### 3.11 Phased plan

**Phase 0: "Untangle" (1-2 days, data and two small code changes)**
1. Rechain the quests (§3.4.1). Know Your Enemy goes to the board.
2. Add Squared Stone. Rewrite The Alchemist, A Roof for the Work, A Smith's Hearth and Show Me Your Kitchen to name their materials.
3. Add the rough plank and campfire charcoal recipes. The Smith's Request waits for the Lodge.
4. Build menu "made at".
5. Fix the contradictions in §3.8.8, except the GDD status table, which follows the work.
6. Check the first rescue in Play (§3.4.4).

**Phase 1: "Teach through people" (about a week)**
1. Reachability audit and its test.
2. The homecoming release rule and the tracker focus.
3. The eight homecoming memories.
4. Lines for Ingrid, Bjarne and Tor. Liv's voiced lesson.
5. New objective types: Consume Item, Take Lesson.
6. The "!" marker. Line progress in the NPC window, coordinated with the agent building it.

**Phase 2: "Everyone has a story" (2-3 weeks)**
1. Lines for Eirik, Yrsa, Gunnar, Asa, Runa, Hild and Sigrid. Liv's personal beats and The Line.
2. Job talk topics as data. Pair asides.
3. Objective types: Complete Raids with someone, Reach Bond, Give Item.
4. Tales from existing events.
5. Traits eased by personal beats, with the hut and trait system.

**Phase 3: "The Doors" (3-4 weeks)**
1. Act II quests 16-21, the old troll boss, Sigrid's site in Troll Valley.
2. The lore bible into GDD §4. The GDD status table.
3. Act III outline agreed with the user.

### 3.12 How to verify each step

**Tests** (always through `RunTests.ps1`, per the memory note):

| Test | What it holds | Changes needed |
|---|---|---|
| `QuestTests`, `QuestDeedTests`, `DynamicQuestTests` | Quest and deed mechanics | New objective types |
| `QuestCategoryTests` | Kinds; nothing optional before Main or Rescue; only the story gold at home | Check after the rechain |
| `RescueOrderTests` | The chain; rumours; workplaces; work follows the rescue | The Alchemist's extra prerequisite |
| `QuestAuditTests` | Every quest can start; nothing waits for nothing; portal moments; rescues placed | + `AskedBeforeReachable` (new) |
| `QuestCoopTests` | A co-op rule per objective type | Each new type |
| `QuestPacingTests` | Pacing rules | + the homecoming rule |
| `FirstActTests`, `HomeStoryTests`, `StoryTests` | Act I, home stories, links | The Cold Stones' new trigger; Act II stories |
| `JournalTests.EveryQuest_HasNotes` | Notes on every quest (failing on 22 today, `Docs/TODO.md:358`) | Write notes for the new quests |
| `EnchantingTests` | Liv's line | The Line |
| `ContentAuditTests` | Every item can come; nothing broken | New recipes, Quest Targets |
| **New `QuestFloodTests`** | Simulate New Game to raid 4 on the real assets: no more than 3 self-started quests per homecoming | – |
| **New `QuestReachabilityTests`** | No objective asks for what can't be had by then | – |

**Playtest log check** (no new code): count QuestStarted rows per return home. The script used for §1.3 reads `played_min`, `scene`,
`event` and `subject` from the run's CSV, skips the prologue, and tracks starts and completions. The target is no more than 3 starts in
the first 10 minutes after each extraction, and no more than 4 active quests.

**Hand-playtest checklist** (New Game, errands skipped, no cheats):
1. Tools of the Clearing appears with the first fire. The pick is in hand before the door wakes.
2. The first raid begins by about minute 20.
3. After raid 1, with Ingrid home, no more than 3 quest banners before you leave the portal's area: The Weakening Ward, The Smith in the Woods, A Roof for the Work. Know Your Enemy is on the board, not the tracker.
4. With the Workshop built, Ingrid asks for stone. Her quest names the Workshop, Grey Stone and 4 → 2. The build menu says "made at the Workshop" under Stone Block.
5. The altar stands before raid 3, without cheats. Note the time.
6. A Smith's Hearth can be finished before Tor, with rough planks, or its text says why not.
7. The Smith's Request appears only once the Lodge stands, and copper ingots can then be made.
8. The Heartroot hunt starts after Bjarne's rescue, and its rumour is in the next briefing.
9. Tor's three Haven quests come one after another, in his voice.
10. Every person after Tor says a homecoming memory on first talk. Their first line step appears with a "!".
11. Failure path: free Ingrid, extract without her, launch raid 2. She is there and the portal said so.
12. Listen to every new line once. That is the user's check; the agent can only transcribe it back.

### 3.13 Open questions for the user

1. **The cap.** One quest per colour on the tracker and at most 2 new self-starting quests per homecoming. Is that the right feel, or would you rather pin quests yourself?
2. **Haven quests:** chained one after another (data now), or held for homecomings (the rule in §3.4.3), or both?
3. **Stone blocks:** keep 16 on the altar and teach them through Ingrid (recommended), or lower the altar's tier I cost to Grey Stone and keep blocks for its upgrade?
4. **Planks before Tor:** a slow rough plank at the Workshop and charcoal at the campfire (recommended), or swap Bjarne and Tor in the approved rescue order?
5. **Know Your Enemy:** to the notice board (recommended), or given later by Eirik?
6. **The antagonist:** the Hunger with the Waker as its human face (recommended), the Hunger alone, or a draug-jarl of Granheim?
7. **"Why me":** "you kept their fire", with no bloodline (recommended). Approve?
8. **The strangers:** taken in earlier wakings, with time sliding in the mist. Approve, including Asa's "last spring"?
9. **Runa's flaw** (paid by the man in grey to report which stones Sigrid woke): too dark for her, or right?
10. **Sigrid:** Act II ends with her rescue in Troll Valley, and "What Sigrid Knows" is told then. Her past is proposed as taken as a child when stones woke in her valley; they never woke for her after. Approve the direction?
11. **The old troll:** a guardian who smashes doors, fought or paid. Approve?
12. **Eirik's sister Sigrun** as a twelfth person, found later through the Lost system: yes or no?
13. **Voice:** about 165 new lines across the phases. Which people first? Ingrid, Bjarne and Tor are recommended.
14. **Trust:** quests only, or also a light per-person trust from gifts (Stardew or Hades style) with a weekly cap?
15. **Names:** Jarl Ottar of Granheim, Holt, the man in grey ("the Waker"). Keep, rename, or leave unnamed?

---

## Appendix: key file references

| Topic | File:line |
|---|---|
| Quest kinds, starts, fields | `Assets/_Project/Scripts/Quests/QuestDefinition.cs:6-36, 66-114` |
| Objective types | `Assets/_Project/Scripts/Quests/QuestObjective.cs:6-54`; co-op rules `:281-313`; Craft Item counting `:343-344, 413-414` |
| All ready quests start in one loop | `Assets/_Project/Scripts/Quests/QuestManager.cs:109-149` |
| Pacing (3 s after the last completion) | `Assets/_Project/Scripts/Quests/StoryPacing.cs` (ReadyAt); `Assets/_Project/Resources/StoryPacingSettings.asset:15-18` |
| Tracker cap | `Assets/_Project/Scripts/UI/QuestTrackerView.cs:22, 100-153`; `Assets/_Project/Prefabs/UI/QuestTracker.prefab:216` |
| Haven asks | `Assets/_Project/Scripts/Settlement/HavenRequestDefinition.cs`; `Assets/_Project/Resources/HavenRequestSettings.asset` (atOnce 3); `Assets/_Project/Data/Settlement/Requests/` |
| The flood's quests | `Data/Quests/ThroughThePortal.asset`, `AStrangerInTheMist.asset`, `TheAlchemist.asset:25,55`, `BringBjarneHome.asset`, `TheWeakeningWard.asset:25`, `KnowYourEnemy.asset:25`, `PrimitiveTools.asset`, `FoodAndWater.asset`, `ARoofForTheWork.asset:25`, `SafeKeeping.asset`, `FirstWeapon.asset`, `WhatBreathesUnderTheRoots.asset:28`, `SmithsRequest.asset` |
| Measured runs | `%USERPROFILE%\AppData\LocalLow\DefaultCompany\Safe Haven\Playtests\20261115-184337.csv`, `20261114-232848.csv`, `20261114-122242.csv` |
| Altar cost | `Assets/_Project/Data/Buildings/PurificationAltar.asset:22-27` |
| Stone Block recipe | `Assets/_Project/Data/Crafting/Recipes/StoneBlock.asset:21-26` |
| Smithy, Cauldron, Workshop costs | `Data/Buildings/Forge.asset:23`, `Cauldron.asset:22-27`, `ConstructionTable.asset:23` |
| Plank, charcoal, copper recipes | `Data/Crafting/Recipes/OakPlank.asset:24`, `Charcoal.asset:24`, `CopperIngot.asset:26` |
| Supply Crate planks | `Assets/_Project/Prefabs/World/Breakables/Supply Crate.prefab:124-130` |
| Portal tiers and their asks | `Assets/_Project/Scripts/Settlement/PortalTiers.cs`; `Assets/_Project/Resources/SettlementSettings.asset:197-235` |
| Jobs (workplace, teaches, skill) | `Assets/_Project/Data/Settlement/{Alchemist,Smith,Carpenter,Warden,Cook,Hunter,Fisher,RuneScholar,Farmer,Weaver,Tracker,Mason,Leader}.asset` |
| Residents (lines, jobs) | `Assets/_Project/Data/Settlement/{Ingrid,Yrsa,Gunnar,Asa,Hild}.asset`; `Assets/_Project/Data/Prologue/Residents/{Bjarne,Tor,Eirik,Liv,Runa,Sigrid}.asset`; class `Assets/_Project/Scripts/Settlement/ResidentDefinition.cs` |
| Resident talk, lessons, teaching | `Assets/_Project/Scripts/Settlement/ResidentNpc.cs:159-220, 302-338` |
| Liv's talk | `Assets/_Project/Scripts/Essence/EnchanterTalk.cs:16-24, 25-80` |
| Liv's line | `Data/Quests/TheRuneScholar.asset`, `TheOldMarks.asset`, `Rubbings.asset`, `MarksOfTheMarked.asset`, `AHandForTheShelf.asset`, `TheGreatWork.asset` |
| Rescue rumours and sites | `Assets/_Project/Data/Raids/Rumours/*.asset`, `Assets/_Project/Data/Raids/Rumours/Events/*.asset` |
| Game events | `Assets/_Project/Scripts/Events/GameEvents.cs` (GameEventKind) |
| Lessons | `Assets/_Project/Scripts/Progression/ExperienceTracker.cs:219` |
| Build menu costs | `Assets/_Project/Scripts/UI/BuildMenuWindow.cs:462-507` |
| Audits | `Assets/_Project/Scripts/Editor/Quests/QuestAudit.cs`, `QuestChecklist.cs`; `Assets/_Project/Scripts/Editor/Content/ContentAudit.cs` |
| Rescue tests | `Assets/_Project/Scripts/Editor/Tests/RescueOrderTests.cs:26-128` |
| Boss | `Assets/_Project/Data/Monsters/Boss The Heartroot.asset` |
| Prologue | `Assets/_Project/Data/Prologue/ForestPrologue.asset`; `Assets/_Project/Data/Prologue/Quests/*.asset` |
| Spoken lines | `Tools/Audio/Voices/dialogue_lines.json` (441), `Assets/_Project/Resources/VoiceBank.asset` (437) |
| Map regions and places | `Assets/_Project/Resources/Atlases/SafeHaven.asset` |
| Design docs | `Docs/QuestRedesign.md`, `Docs/RescueOrder.md`, `Docs/EnchantingResearch.md` (Status), `Docs/BackstoryCinematic.md`, `Docs/PrologueEndCinematic.md`, `Docs/MistyWoodsCinematic.md`, `Docs/StoryGraphs.md`, `Docs/GameDesignDocument.md:134-178, 862-966`, `Docs/TODO.md:329-360` |
