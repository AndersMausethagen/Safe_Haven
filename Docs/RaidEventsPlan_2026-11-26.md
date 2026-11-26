# Raid events: making them worth the walk (plan, 2026-11-26)

The user: "hvis vi skulle utvidet event-systemet, hvordan ville du gjort det? trenger vi det? hvordan kan vi gjøre de enda mer spennende
og unike?" (if we were to expand the event system, how would you do it? do we need it? how can we make events even more exciting and
unique?) and then: "skriv det som et dokument med konkrete forslag" (write it as a document with concrete proposals).

**Read only.** Nothing in the game was changed for this document. It builds on:
- the event system as built: `RaidEventDefinition`, `RaidEventDirector`, `RaidEventPicker`, `RaidEventOutcomes.cs`;
- the rumours and the Lost;
- the playtest log `20261121-151353.csv` (12 Misty Woods raids, 2026-11-21 to 11-25);
- today's raid places rounds (`Docs/RaidLayoutAnalysis_2026-11-26.md`).

What other games do is described from general knowledge of them, not from new sources.

## Status (2026-11-26, the user: "enig med dine anbefalinger. kjør på")

Steps 1-6 are built. Steps 4-6 were built straight after 1-3 (the user: "fortsett"), before step 1's log could say whether they were needed. The §6 answers are as recommended: consequences may show at home, a carried thing takes the hands, and legends are unique. None of it has been played by hand.

| Step | As built |
|---|---|
| 1 (E1) | `RaidEventDefinition` › Stakes (loot, danger, time, 0-2) on every event, told in the open-site alert as words ("rich · risky · takes time"). Every site with choices has one anyone can take that costs no time (test). Log rows `EventReached` (first within 10 m) and `EventSkipped` (still open as the raid ends: reached or not, when it began, how far) |
| 2 (E2) | `RaidEventChoice` › Requires (`ChoiceRequirement`: Skill At Least, Carries Item, Companion Along, Hour Is, Knows Mark). An unmet choice shows greyed with "Needs Woodcutting 20". New choices on the existing events: Cries from the Ditch (Woodcutting 20), Three Wishes (Runecraft 20), Overturned Wagon (Construction 20), Toll Troll (a moss troll's tusk), Living Path (Runecraft 15), Open Barrow (Runecraft 20), Blighted Tree (Alchemy 20) |
| 3 (E3) | `DefendOutcome`/`DefendTask` (hold the site's lit ring while waves come; the count waits outside it; paid once) and `CarryOutcome`/`CarriedBurden` (in the hands: slows, is heard, set down to fight, picked up again, pays in an open exit's circle). Task lines on the HUD under the companions. The Old Beacon: "Light it and hold it". New: **The Elder's Bell** (Woods: hold the oak, or climb up quietly with Sneaking 20), **The Stone Head** (Valley: carry it out, or break it with a pickaxe; Trollstone only from tier IV), **The Sunken Bell Tower** (Marsh: hold the belfry while the drowned come, or dive the stair after dark). LocalSpawn models: Bell Oak, Stone Head, Sunken Tower; paintings. Pools 12/11/9 |
| 4 (E4) | Choices leave marks in the save (`SetMarkOutcome`, the journal's noticed flags). An event can need a mark and be blocked by another (`needsMark`, `blockedByMark`), and every choice of a follow-up sets its blocker, so it comes once. The needed mark is checked against the save as the raid began, so a follow-up comes in a later raid. The Watch Post's forecast obeys the same marks. Follow-ups: **The Pedlar's Brother** (after the wagon: freed or robbed), **The Pedlar's Thanks**, **The Stranger Collects** (a debt), **The Bog Remembers** (after a kindness at the bog body). `CalmForestOutcome` lowers the forest's anger |
| 5 (E5) | Player state weighs the pick (`moreLikelyWhen`): bag at least 70% full, forest angry or worse, died last raid, night. New: **Followed** (bag full, ×10), **The Forest's Warning** (angry, ×40), **Where You Fell** (died last raid, ×40). Each raid has two kinds of day (`RaidEventSettings.conditions`), one rolled from the run's seed at most, which make a few events likelier. It is told on the preparation briefing as a "Today" card, rolled from the same seed |
| Rest (the user: "gjør det som mangler") | **Track** (`TrackOutcome`/`TrackTask`: signs one by one to a hidden place 60-120 m off; `TrackLeader` walks on ahead) and **Escort** (`EscortOutcome`/`EscortFollower`: follows, can fall, delivered in an open exit's circle). `CrossOutcome` (to the farthest or nearest exit), `ShowSitePartOutcome`, `EaseBlightOutcome`, `Mounted`, choices `hiddenUntilMet`, `moreLikelyWithMark`, `nearWater` sites (`RaidSpots.TryFindShore`). New: **The Woodcutter's Last Tree**, **The Snare Line** (Track), **The Ferryman** (shore, cross; Runa sings), **The Dairymaid of the Seter** (Escort; she replaces the plan's cow: there is no cow model, and the folk tales' trolls take dairymaids), **The Peat Fire**, **The Wisp's Bargain**. Follow-ups: **The Ferryman's Due**, **The Cook's Grudge**, **The Stranger's Last Word** / **The Stranger's Hunt**, **The Clan's Grudge**; the bell's "listen" makes the Lindorm likelier. §4: the fog's Liv choice, the Oskorei's waystone (ride with them mounted, Horsemanship 20), and choices the past opens (wagon, toll trolls, sealed barrow), the blight eased at home. The White Hart is followed for real. At home: the pedlar and the dairymaid come by the gate once (`HomecomingSettings` › Visits). Journal › Raids › Strange Things lists the legends met. A fall last raid is read from the journal, so it survives a reload. Tests: `RaidEventsCompletedTests1126` |
| 6 (E6) | One legend a raid, once in a save (`oncePerSave`, noted once the player reaches its site, so one never found comes again), each with a relic: **The White Hart** (Woods), **The Troll Wedding** (Valley, the striding troll rock), **The Lindorm Sleeps** (Marsh). Pools are now 19/17/16 |

**Checked live:**
- **Troll Valley:** the stone head was lifted (the site's own head hidden), its line read "Carry the stone head out: 75 m to …", it was set down and taken again, and it paid when carried into an open exit.
- **Misty Woods:** the Elder's Bell showed the Sneaking choice greyed with its reason, and choosing it did nothing. The hold counted down from 0:40, Rootlings came in waves, and it paid once at 40 s.

Tests: `RaidEventsPlanTests1126`, `RaidEventsMemoryTests1126`, `PropMaterialTests`.

**Models (the user: "vær litt mer forsiktig med reduseringen av polycount"):**
- The 15 new LocalSpawn props were re-ingested at 5,000-9,000 triangles at LOD0, up from 1,000-2,500. `props_manifest.json` now sets `lod0_floor`, `view_scale` and tighter `shape_deviation`/`shape_silhouette`, which `ingest_generated.py` reads.
- All 15 had drawn with the FBX's plain Lit material and a normal map imported as colour, which left them nearly black in patches (Stone Head, White Hart). Each now has its own Layered Lit `.mat`, and `PropMapImport` imports Art/World/Props maps correctly.
- The Stone Head's albedo had a baked black patch, now lifted to the stone's colour.

**Watch in a playtest:**
- the `EventReached` and `EventSkipped` rows: is the share of begun events acted on now over half?
- the Bell Oak (4.5 m) can hide the player standing beside it.

---

## 0. The short answer

**Yes, but not more of the same kind.** The problem today is not how many events there are: players walk past most of them.

| In 12 raids | Count |
|---|---|
| Events and rumours begun | 57 |
| Acted on (a choice taken, or the site used) | about 14, so **1 in 4** |
| Choices written to the log (`EventChoice`, logged since 11-22) | 3 |

**Which events were used:**
- Most used: **The Lost**, 3 of 4. It has a person, a story and a consequence that lasts.
- Never used: Beacon Light (0 of 4), Night Cairn (0 of 3), Deep Patch (0 of 3), Toll Troll (0 of 2), Thickening Fog, Living Stones and Three Wishes (0 of 1 each). These are menu events that cost clock time and pay something unclear.

**What to build, in order:**
1. **Show the stakes before the walk** and measure why players pass events by. Small.
2. **Choices that need something:** a skill, a tool or a companion. Small to medium.
3. **Play at the site:** defend, carry, track, escort. Medium.
4. **Consequences that last, and familiar faces.** Medium to large.
5. **Events that answer how the player plays,** and a condition for each run. Small to medium.
6. **A few legends:** events that happen once per save. Small.

§3 lists concrete new events for each raid, and §4 lists upgrades to the existing ones.

---

## 1. Why events are passed by (hypotheses)

The log shows what players did and when. It does not show why. Each cause below says how to tell.

| Cause | Why it is likely | How to tell (E1) |
|---|---|---|
| **The walk costs clock time and the reward is unclear** | A warning is a line of text. The site is 35-70 m off, and the careful choices cost a further 20-45 s. Players already leave at about 5.5 minutes | A `EventSkipped` row when an event's site closes unvisited, with how far it was and the clock |
| **A menu is not play** | The events never used are the pure menu events. The Lost, which was used, has someone to walk home | Compare use rates of menu events and events with a task once E3 exists |
| **Nothing comes of it after the raid** | Only the rumours and the Lost reach home | The rate at which follow-up events are taken (E4) |
| **The warning is lost in the feed** | It shares the message feed with pickups and skill lines | E1's icons, then the use rate before and after |

---

## 2. The plan

### E1. Show the stakes, and measure (S)

- **The warning shows what is at stake:**
  - three small marks beside the event in the HUD list: **loot**, **danger** and **time**, each 0-2 (for example, Traders Wagon: loot 2, danger 1, time 1);
  - the distance, as the list already shows.

  The marks are data on `RaidEventDefinition` (`Stakes`: three 0-2 values), set by hand for each event, so nothing has to be computed.
- **A choice that is free on the clock** at every site. Today most careful choices cost 20-45 s, and that is the price players avoid. The quick choice should be a fair one, not a trap.
- **New log rows:**
  - `EventSkipped`: the event, metres from the player when it began, and the clock;
  - `EventReached`: when the player first comes within 10 m.

  With these, the 1-in-4 can be split into "never went" and "went but chose nothing".
- **Code:** `RaidEventDefinition.stakes`, `RaidHudView` (the open-sites list), `RaidEventSite` (reached, and skipped when the site closes).
- **Test:** every event in a pool has stakes; every site has at least one choice with no time cost.

### E2. Choices that need something (S-M)

FTL shows extra choices when you have the right crew or equipment. Darkest Dungeon gives a curio a better outcome when you use the right provision on it. Both make what you carry matter at the event.

- **`RaidEventChoice.requires`:** a list of conditions (`[SerializeReference] List<ChoiceRequirement>`). A choice that isn't met **still shows, greyed out, with its reason** ("Needs Woodcutting 20"), so it becomes something to aim for.
- **Kinds of requirement (new classes, one file):**

| Requirement | Reads | Example |
|---|---|---|
| `SkillAtLeast` | `PlayerSkills` level | Woodcutting 20: lift the pedlar's log in 15 s instead of 45 |
| `CarriesTool` | the bag or what is worn (a tool kind) | A pickaxe: dig the scree in half the time |
| `CompanionAlong` | `RaidCompanions` (a resident, or any with a job) | With a hunter along: follow the tracks to the den |
| `HourIs` | the run's `RaidTimeOfDay` | Only at night: the bog lights are kind |
| `KnowsRecipe` / `HasNoticed` | the journal | You've read the Moonsilver Folio: you know what the mark means |

- **Rewards follow the requirement:** a met requirement unlocks the **best** choice of the event, not merely a different one.
- **Test:** each requirement is true or false for a simple player stub; a choice that isn't met can't be taken (`RaidEventSite`).

### E3. Play at the site (M)

New outcomes that start a small task instead of handing over loot. Each is built on what already exists.

| Outcome | What the player does | Built on |
|---|---|---|
| **`DefendOutcome`** | Hold a circle round the site for N seconds while waves come. Leaving the circle pauses the count; the reward comes when it ends | `ExtractionHold` (the hold, the circle, pausing) and `SpawnCreaturesOutcome` |
| **`CarryOutcome`** | Pick up something heavy (a chest, a stone head, a bell) that slows the player and draws creatures. It pays only at an exit, or doubles at home | the cart transport rules (weight slows); a raid item with weight; `ExtractionPoint` |
| **`TrackOutcome`** | Signs appear in a line (prints, blood, broken branches) toward a hidden place 60-120 m away; the place opens on arrival | `RaidSpots` (a spot out of sight), decals, `RaidEventSite` for the end |
| **`EscortOutcome`** | Someone follows the player to an exit | the rescue system (`RescueResidentOutcome`, the follow and the circle) |

- Each task shows in the HUD list like an open site ("Hold: 0:25", "Carry it out: 84 m"), and its stakes are marked under E1.
- **Code:**
  - four outcome classes, each a small `MonoBehaviour` helper spawned at the site (the defend circle, the track's signs);
  - `RaidEventSite` learns that a task is running, so the site isn't resolved until it ends.
- **Test:**
  - a defend hold counts only while the player stands in the circle and pays once;
  - a carried thing slows the player by its weight and is lost if the raid fails;
  - a track's signs lead within reach of the place they point to.

### E4. Consequences that last, and familiar faces (M-L)

Sunless Sea gates its stories on what the player has done before, and Dredge keeps returning to the same few characters. The lesson from both: a world that remembers you is the strongest pull.

- **Flags in the save:** a choice can set a mark in the journal's noticed set (`event:pedlar:freed`) or raise a Story Event, which already exists as `raisesWhenChosen`.
- **Follow-up events:** `RaidEventDefinition.needsFlag` and `blockedByFlag`, and a weight that grows once the flag is set. Examples:
  - **The pedlar you freed** (Cries from the Ditch) comes to the Haven's gate as a trader for one homecoming. If you took his pack, his brother meets you at the next wagon instead, and he is not friendly.
  - **The trolls whose pot you robbed** (The Troll's Cookpot) wait at the next bridge toll: the toll is higher, or the fight comes at once.
  - **The bog body you laid back** (The Bog Body) answers once, raids later: a voice from the water tells you where the deep peat holds iron.
- **The hooded stranger as a recurring figure:** Three Wishes, then *The Stranger Collects* (§3.4) once a wish is outstanding, then a last meeting. The same figure, remembering what you chose.
- **Code:**
  - `RaidEventPicker` filters by flags and weighs follow-ups up;
  - a `SetFlagOutcome`;
  - the Haven's gate visitor uses the existing homecoming moments, and only for the pedlar.
- **Test:**
  - a follow-up can't begin without its flag;
  - once its flag is set it is picked within a few raids (a seeded picker test);
  - the flags survive a save and load (SaveMapper).

### E5. Events that answer how the player plays (S-M)

- **State-weighted picks:** `RaidEventDefinition.moreLikelyWhen`, a short list of `(state, weight)` pairs:
  - **bag over 70% full:** *Followed* (§3.4), something stalking the loot;
  - **forest annoyance at Angry:** *The Forest's Warning*, a last chance to stop cutting before the wrath;
  - **died last raid:** *Where You Fell*, at the place of the last death, with a share of what was lost (the Lost already take the best item);
  - **night:** the night-only events below.
- **A condition for the run (ARC Raiders has the same idea):** rolled at the briefing with the hour, named there, and changing which events can come and how much they pay. Examples:
  - *The trolls are cooking today:* the Valley's troll events are twice as likely;
  - *Fog night:* lights and drowned things in the Marsh.

  This is data on `RaidDefinition` (`conditions`: name, briefing line, chance, event weights). `RaidForecast` already rolls the hour from the seed.
- **Test:** each state's weight changes the picker's odds as written (seeded); the briefing and the raid agree on the condition (same seed).

### E6. A few legends (S)

- **Once per save:** `RaidEventDefinition.oncePerSave` (a journal mark). Each legend is rare (weight 0.1-0.2), unforgettable, has its own reward (a relic of its own, a named item) and fills a page in the journal's **Strange Things** list.
- **One legend per raid** (§3.5).
- **Test:** a legend never comes twice in one save, and its journal page appears.

---

## 3. Concrete new events (using E2-E6)

Site models come from LocalSpawn like today's (concept, Hunyuan3D, ingest). Each event names the mechanic it uses.

### 3.1 Misty Woods

| Event | What happens | Choices | Uses |
|---|---|---|---|
| **The Woodcutter's Last Tree** | A half-felled giant cedar, the woodcutter's axe still in it. Hit it and it falls, the way you choose | Fell it toward the wolves' den (*Woodcutting 25*): it crushes the den and its loot spills. Fell it across the ravine: a bridge for the rest of the raid. Leave it | E2 |
| **The Hunters' Snare Line** | A hunter's snares along a game trail. One holds something big and angry | Follow the line (track to a cache). Free what's caught (a wolf flees and the forest annoyance drops). Take the snares (*Foraging*: rope and hides) | E3 Track, E2 |
| **The Elder's Bell** | A bronze bell in an old oak; ring it and something answers | Ring and hold the oak for 40 s as the root-things come (Defend), and the Elder gives a blessing. Climb up quietly (*Sneaking 20*) for the bell's offerings | E3 Defend, E2 |

### 3.2 Troll Valley

| Event | What happens | Choices | Uses |
|---|---|---|---|
| **The Stone Head** | A troll's head turned to stone at dawn, the size of a barrel, rolled off a ledge | Carry it to an exit (slow, loud, worth a lot at home as Trollstone). Break it for what's inside (*Mining*). Leave it | E3 Carry |
| **The Ferryman** | A raft and an old man at a deep pool of the river | Pay to cross: a shortcut to the far exit and he tells a rumour. Pay with a song (*with Runa along*). Steal the raft (Followed later) | E2 Companion, E4 |
| **The Seter's Last Cow** | A cow left at an empty seter, bellowing; trolls love cows | Drive her to the exit (Escort: she walks slowly, trolls come); she comes home as milk for the Haven. Leave her as bait: the trolls gather, and their camp stands empty | E3 Escort, E4 |

### 3.3 Moss Marsh

| Event | What happens | Choices | Uses |
|---|---|---|---|
| **The Sunken Bell Tower** | The top of a church tower stands out of the black water. At night its bell rings under the surface | Dive the stair (a timed swim, best at *night*). Ring it from the top (Defend: the drowned climb out). Listen (it tells where the Lindorm sleeps: a flag) | E2 Hour, E3 Defend, E4 |
| **The Peat Fire** | The peat itself is burning underground; smoke seeps out across a wide patch | Cross it fast (burns, but a shortcut). Cut a firebreak (*Construction*, 40 s): the peat cutters remember (E4). Go round | E2, E4 |
| **The Will-o'-Wisp's Bargain** | A light that talks: it shows the way out, at a price | Take its path (an exit closer, at the cost of the stage's loot bonus). Catch it in the old lantern (*if the lantern was lit before*, a flag from Lights on the Water) | E4 chain |

### 3.4 Across the raids (follow-ups and state)

| Event | When | What |
|---|---|---|
| **Followed** | Bag over 70% (E5) | Footsteps that stop when you stop. Turn and fight (a pack of thieves), drop a share of the haul, or run for the exit (Hunters set on you, the haul kept) |
| **The Forest's Warning** | Annoyance at Angry (E5) | A ring of standing roots around a felled tree. Put back what you took (pay wood) and the forest calms. Ignore it and the wrath comes sooner |
| **Where You Fell** | Died last raid (E5) | Your own bones and pack, where you fell. A share of the lost haul, guarded by what killed you |
| **The Stranger Collects** | A wish taken at Three Wishes (E4) | The hooded stranger again: pay the wish back in kind (health, or an item), or refuse and be hunted for the raid |
| **The Pedlar's Brother** | The pedlar's pack taken (E4) | At the next overturned wagon, a man with the same face. He wants the pack back, or blood |

### 3.5 Legends (E6, once per save)

| Raid | Legend | Reward |
|---|---|---|
| Misty Woods | **The White Hart**: a white stag that can't be caught, only followed (Track) to where the Heartroot's first seed fell | A relic: *Hart's Tine* (the forest's annoyance rises slower) |
| Troll Valley | **The Troll Wedding**: two clans meet at the Ting at dusk. Watch unseen (*Sneaking*) or crash it | The *Bride's Crown* of trollstone, or a clan's lasting grudge (E4) |
| Moss Marsh | **The Lindorm Sleeps**: its coils above the water, asleep. Walk its back to the hoard on the far islet | The *Lindorm Scale*, earlier than its tier, and a flag the Lindorm remembers |

---

## 4. The existing events, upgraded

The 12 existing events keep their choices. Each gets **one choice that needs something** (E2), **stakes** (E1) and, where it fits, a
lasting mark (E4):

| Event | New choice (requirement) | Lasting mark |
|---|---|---|
| Cries from the Ditch | Lift the log in 15 s (*Woodcutting 20*) | Pedlar freed / pack taken (§3.4) |
| Three Wishes | Bargain the price down (*Runecraft 20*) | Wish outstanding (§3.4) |
| Overturned Wagon | Mend it in 10 s (*Construction 20*) | The trader remembers |
| The Toll Troll | Pay in his own coin: a troll trophy from the bag | The troll's mood at the next bridge |
| Thickening Fog | Walk the fog by the lights' pattern (*with Liv along*) | — |
| Old Beacon | Light it and **Defend** it for 40 s for the best reveal (E3) | — |
| The Living Path | Read the stones (*knows the runes*) | — |
| The Oskorei | Ride with them (*Horsemanship 20, mounted*) | Rode with the Oskorei once |
| The Open Barrow | Seal it properly (*Runecraft*) | The barrow's dead stay quiet in later raids |
| The Blighted Tree | Draw out the rot with a draught (*Alchemy 20*) | The Heartroot's blight a little weaker |
| Bridge Toll, Jaw-keeper | Already a negotiation | — |

---

## 5. Order, cost and how to know it worked

| Step | What | Effort | Prove it with |
|---|---|---|---|
| 1 | E1 stakes, free-on-the-clock choices, `EventSkipped` and `EventReached` rows | S | Use rate, and reached against chosen |
| 2 | E2 requirements, and §4's new choice on the 12 events | S-M | Share of choices needing something that are taken when met |
| 3 | E3 Defend and Carry; three §3 events that use them (Elder's Bell, Stone Head, Sunken Bell Tower) | M | Use rate of task events against menu events |
| 4 | E5 state picks (Followed, Forest's Warning, Where You Fell) and run conditions | S-M | Clock at the end when Followed came |
| 5 | E4 flags and follow-ups (pedlar, stranger, trolls) | M-L | Follow-ups taken |
| 6 | E6 the three legends | S each | — |

**Target after steps 1-3:** at least half of the begun events acted on (today 1 in 4).

**Not recommended:**
- more pure menu events;
- harm with no choice in it;
- higher time costs;
- a penalty for passing an event by. That breaks the "positive incentives" rule.

---

## 6. Questions for you

1. **Follow-ups at home (E4):** may an event's consequence show up in the Haven (the pedlar as a visiting trader), or should consequences stay in the raids? Recommended: yes, small and rare.
2. **Carried things (E3 Carry):** should a carried object take the hands, so the player fights with it set down (as a cart works), or just slow the player? Recommended: it takes the hands, like a cart. Setting it down to fight is the drama.
3. **Legends (E6):** should a legend's reward be unique in the save (one Hart's Tine ever) or repeatable as a rare drop later? Recommended: unique.
