# Death and what it costs (research, 2026-11-22)

Agent-written, read-only research. Nothing here is built. It answers the user's note of 2026-11-22: "Dying in the Haven costs
nothing. The player should be afraid of dying. Maybe he can be set back some in proficiencies, lose gear where he dies or
something else." And on ward breaches: "There should also be a risk to losing or dying."

**In short.** At home a fall is a free full heal and a free trip to the portal. In a breach it is worse than free: it puts the
player back beside the ward they are defending, at full health, three seconds later. The recommendation (§4.6) gives each kind
of death one clear cost that can mostly be won back: the bag falls where you fell, skills lose some progress (never a level),
a fall in a breach costs the ward, and a lost breach costs some stores that can be taken back in the next raid.

---

## 1. How death works now

### 1.1 At home, on a normal day

| What happens | Where |
|---|---|
| The player lies still for **3 s**, then wakes at the portal's `HomePortal` arrival point with **100% health** | `Settlement/HomeRevival.cs` (`delay: 3`, `healthOnWaking: 1` in `SafeHaven.unity`) |
| Bag, materials, equipped gear, durability, skills, boons (meals, tonics) and hunger are **all untouched** | Nothing else listens for the player's death at home |
| One line of text: "You fall... and the portal's light pulls you back home." No report, unlike raids | `HomeRevival.fallenText` |

Where players can die at home: walking into the dark at night (Night Prowlers, up to 8, boars and draugar after two breaches),
corruption nights (`CorruptionNights`, 3 to 8 creatures from portal Tier II), ward breaches, and wildlife out in the meadows.
The playtest logs show Haven deaths to wolves, draugar, boars, a Bog Spitter and a Black Widow, but most of those runs are
agent probes, so there is no real rate yet.

**What this means in play:** a fall is the fastest way home and a full heal. Nothing pushes the player to retreat, eat or bring
a torch.

### 1.2 Falling during a ward breach

- In a real breach (`WardBreachDefinition.ThreatensHome`) the director ignores the player's death
  (`WardBreachDirector.OnPlayerDied` returns early). `HomeRevival` wakes the player at the portal, and the ward anchor stands at
  the portal. So a fall is a full heal plus a teleport to the objective in 3 s.
- In the first, scripted breach, a fall ends the breach and it can be tried again. Nothing else is lost.
- After `LongestDefense` (300 s) any attackers left slink away and the breach **counts as won**, with its full reward, as long
  as the ward still stands (`WardBreachDirector.Update` calls `Win()`).
- Guards never die: a guard's stand-in `Companion` gets up by itself (`ResidentDefender`).

### 1.3 Losing a breach (the ward falls)

| Cost today | Where |
|---|---|
| The attackers leave. The ward draws in by **4 m** until **2** more successful raids. The same breach waits to be tried again | `SettlementState.LoseBreach`, `WardBreachSettings` (`strainRaids: 2`, `strainRadius: 4`) |
| Damaged buildings are **broken**, never destroyed (`Damageable.HealthFloor`), and out of use until mended for **30%** of the damage's build cost, all at once at the portal | `BuildingObject`, `BuildingRepairs.MendAction`, `repairShare: 0.3` |
| No reward | `OnWardFell` |
| Stores, residents, crops, portal tier, renown: **untouched** | |

So a lost breach costs a repair bill and a smaller ward for two raids. The GDD asks for this on purpose: "bounded, repairable
consequences" and "no downward spiral" (GDD §6.3, §6.9).

### 1.4 In a raid, for comparison (see `RaidLoopResearch_2026-11-22.md` §1.5)

| Kept | Lost |
|---|---|
| Knowledge, recipes, skills and levels (GDD §8.3 promises this) | Everything in the bag except the safe pocket (1 slot, up to 4 with upgrades) |
| Deployed gear, at **−25% durability** (4 falls to break it; a boss encounter's failure 10%) | The raid's bonus experience (the ×1.5 share, `ExperienceTracker.SettleRaid`) |
| | Unextracted Cursed Essence |
| | A Lost One that killed you takes the most valuable lost stack (up to 4 carried) and ranks up; laying it to rest gives them back |

`PlayerSnapshot.AfterFailedRaid`, `RaidDirector.Finish`, `DurabilitySettings.lossOnFailedRaid`, `LostOneSettings`.
Quitting mid-raid counts as a fall (`RaidDirector.Abandon`); a crash puts the player back at the launch save. In co-op a
player who would die is **downed** for 45 s and can be helped up (`CoopDowned`), but only on raids.

### 1.5 What is at risk where

| | Bag | Gear | Skills | Boons | Time | The Haven |
|---|---|---|---|---|---|---|
| **Raid death** | Lost (but the safe pocket) | −25% wear | Raid bonus lost | – | The raid | – |
| **Home death** | – | – | – | – | 3 s | – |
| **Breach death** | – | – | – | – | 3 s (and a heal) | – |
| **Breach lost** | – | – | – | – | 2 raids of strain | Broken buildings (30% to mend) |

The raid has real stakes. The Haven has none.

---

## 2. What other games do

| Game | What a death costs | How it is won back | Numbers | What players say |
|---|---|---|---|---|
| **Valheim** | Everything, equipped gear too, goes into a tombstone; food buffs end; every skill drains | Walk back to the tombstone. A "Corpse Run" buff helps carry it home | Normal: **5%** of every skill **and** the progress to the next level. **No Skill Drain** for **10 min** after a death. Presets from 1% (Casual) to 7.5% (Hard, loose items destroyed) and Hardcore. Tombstones never expire [1] | Drain "keeps me caring about my life at end game"; others find it too high past skill 50 and ask for items and skills on separate sliders [2] |
| **Enshrouded** | Default: **backpack materials only** go into a tombstone; gear and hotbar stay | Tombstone | Setting: materials only / everything / no tombstone. The Survival preset loses the whole backpack [3] | Default seen as fair; the setting is widely used |
| **Palworld** | Items (and by setting equipment and Pals) dropped in a box | The box | Server setting None / Item / ItemAndEquipment / All [4] | Single-player can only turn it off entirely |
| **RuneScape: Dragonwilds** | The whole inventory into a gravestone | The grave; a co-op friend can fetch it | No timer reported [5] | Complaints about graves behind regrown vines, in vaults, or broken by monsters [5] |
| **V Rising** | Inventory drops in a container where you fell; equipped gear stays | The container [6] | | |
| **Dune: Awakening** | After a downed period: backpack drops (skull marker); gear stays, loses durability | The drop. A sandworm or storm death loses it for good | Durability loss cut from **5% to 2%** after complaints [7] | Sandworm deaths "way, way worse" (PC Gamer) |
| **Soulmask** | Gear and items drop | Tribesmen can revive you before you die, so nothing drops [8] | | |
| **Core Keeper** | Inventory into a tombstone; armour wears | Tombstone. Hardcore characters are deleted [9] | | |
| **Grounded 2** | Backpack drops | The backpack; it does not despawn [10] | | |
| **Sons of the Forest** | First death in single-player: **captured**, wake in a cannibal camp with your gear, and escape | Escape. A second death is game over [11] | | Remembered as a story, not a penalty |
| **Terraria** | Classic: part of your coins; Mediumcore: all items; Hardcore: permadeath | Pick them up where you fell | Classic drops **50% / 75% / 100%** of coins by world mode [12] | Chosen per character |
| **Minecraft** | All items and most XP | Pick them up (items despawn after 5 min) | Drops 7 × level XP, at most 100 points; the rest is gone [13] | `keepInventory` is a famous rule players switch on |
| **Don't Starve Together** | You become a ghost | Revive at a Touch Stone (free), or by Telltale Heart / Florid Postern | Heart revives cost **25% of max health**, cured by a Booster Shot [14] | |
| **7 Days to Die** | XP debt; items by the "Drop On Death" setting into a backpack | Earn the debt back; the backpack lasts 1 hour | Default: **50%** of the current level's XP as debt; "Near Death Trauma" from level 6. Other modes: Injured (food and water 50%), Permanent Death [15] | |
| **Dark Souls / Elden Ring** | All held runes (XP and money) | Touch your bloodstain; **one** chance | Die on the way and they are gone [16] | The defining tension of the genre |
| **Hollow Knight** | All Geo; part of the Soul meter | Kill your Shade where you fell | Die again first and the Geo is gone [17] | |
| **Diablo II** | Gold (up to 20% for good), XP in Nightmare/Hell, the corpse with gear | Corpse run gives back **75%** of the lost XP | **5% / 10%** of the XP to the next level. **Never a level** [18] | |
| **Path of Exile 2** | XP late in the game | None | **10%** of the current level's XP; can't lose a level [19] | Long forum threads calling it the reason they quit [19] |
| **Escape from Tarkov** | Everything carried in, but the secure container | Insurance returns gear nobody looted, a day or two later | Return times and chances by trader [20] | |
| **Arc Raiders** | Everything carried but the safe pocket (1 slot, 2-3 with augments) | Friends can revive you | XP is always kept [21] | |

**Attacks on the base:**

| Game | What a lost defence costs | What players do about it |
|---|---|---|
| **Valheim** raids | Damage to structures, repaired with the hammer; only while a player is near the base [22] | Walls and ditches |
| **7 Days to Die** blood moon | Damaged structures; the horde pauses after a death so the body can be fetched [23] | Build a separate "horde base" to protect the real one |
| **Palworld** raids | Buildings wrecked, Pals knocked out; the Bellanoir raid fails if the Palbox breaks [24] | Build "a separate base for raids, one you wouldn't mind losing" [24] |
| **Conan Exiles** purges | Whole bases and thralls lost in minutes | "Purges should not destroy builds on PvE" [25] |

When a base can be truly destroyed, players build decoy bases. That is the wrong lesson.

---

## 3. Design principles drawn from this

1. **Fear needs a visible stake.** The genre's tension comes from something you can see before the fight: runes in Elden
   Ring, the full pack in Valheim, "At risk" in our raid HUD. A hidden cost teaches nothing.
2. **Recoverable beats permanent.** Corpse runs, bloodstains and shades turn a loss into a short, tense second trip. Permanent
   loss belongs only where the player chose to carry something into danger (our raids).
3. **Never take levels, perks, buildings or people.** Diablo II and Path of Exile never take a level. Our skills give perks at
   10/20/30/40 and techniques at 20/40, and raid tiers expect Melee 10/20/30/40, so losing a level would break both.
4. **Scale the loss with time at risk, not with level.** Valheim's complaints all come from high levels, where 5% is hours.
   A share of *the next level's* experience costs about the same minutes of play at any level.
5. **Stop the spiral.** Valheim's 10-minute No Skill Drain, Diablo's partial XP return, our 2-raid ward strain. A second death
   right after the first should cost less, not more (Bycer: one failure "could start a chain reaction" [26]).
6. **Death must never be useful.** Today a fall at home is a free heal and teleport. Any fix must remove that first.
7. **The base is bounded.** Damage, theft and delay: yes. Destruction: no (GDD §6.3, §6.9; the Conan and Palworld lesson).
8. **In co-op, down before dead, and losses stay personal.** Arc Raiders, Dune and Soulmask let friends save you. A guest
   should never pay for the host's lost breach, and players hate a friend quitting before a revive [27].
9. **Say what was lost and where to get it back.** The raid report does this; a home death shows one line.
10. **Let players choose harshness.** Valheim, Enshrouded, Palworld, Terraria and 7 Days to Die all have death presets.
    Hades shows that "take the pain out of dying" can sit beside stakes when time is never wasted [28].

---

## 4. Options for Safe Haven

### Option A: The cairn ("where you fell")

| | |
|---|---|
| **Lost** | At a death at home (and in a breach), the bag's materials and loose items drop into a **cairn** where the player fell. Equipped gear, tools in hand and the safe pocket stay with the player (as Enshrouded's default, and as raids keep deployed gear) |
| **Won back** | Walk back and take it all in one press. It never expires. A second death makes a second cairn; nothing is destroyed |
| **Numbers** | 100% recoverable. Placed on the nearest walkable ground (never in water, on a cliff or inside a wall). Marked on the minimap and compass |
| **Co-op** | Personal: only its owner can open it, others see it (as `DroppedItem`'s personal loot through `CoopDrops`) |
| **Hooks** | `DroppedItem` already saves a stack in the world at home, through raids and saves, with a sack model: a cairn is a many-slot version. `HomeRevival` (drop before waking), `ItemInventory`, `MaterialPool`, `QuestMarker`/`MinimapHud`, `SaveableEntity` |
| **Effort** | **S-M** |
| **Risks** | Short trips at home make it mild by day. At night the cairn lies in the dark among the prowlers, which is the point. Carts and horses' loads are not dropped |

### Option B: Lessons fade (a proficiency setback)

| | |
|---|---|
| **Lost** | Every skill loses **25% of the experience its next level needs**, but **never drops below the start of its level** (no level, perk or technique is ever lost) |
| **Won back** | **Half** of it comes back when the cairn is emptied (Diablo II gives 75%). The rest is earned again by playing |
| **Numbers** | Level 10: −31 at most. Level 20: −84. Level 30: −226. Level 40: −614. From `Docs/Skills.md`'s pace (about 1,100 experience an hour in a main skill at 20-30, 2,300 at 30-50) that is **about 5 minutes of that skill's practice at levels 20-30, about 6-16 minutes at 30-40**, half of it returned by the cairn. Skills with no progress lose nothing. **Grace:** for **10 minutes** after waking, another fall costs no lessons (Valheim's 600 s; kept through quitting) |
| **Co-op** | Each player's own skills. Watching a friend fall costs nothing |
| **Hooks** | `CharacterProgress` (experience is a total per skill; a new clamped `LoseExperience`), `LevelingSettings` (share, grace, returned share), the death card, a `LessonsLost` playtest row |
| **Effort** | **S-M** |
| **Risks** | Feels grindy if too big: watch the `SkillXp` rows. GDD §8.3 promises levels stay after a raid death; this keeps levels everywhere, but see open question 1 on raids |

### Option C: The portal's price (falls in a breach)

| | |
|---|---|
| **Lost** | The portal "wakes the fallen" (GDD §6.4.1). In a real breach that costs it: each fall takes **10% of the ward's full health**. Waking takes **10 s**, **+5 s** for each further fall in the same breach (at most 25 s) |
| **Won back** | Not directly. The ward heals when the breach ends, as now |
| **Numbers** | A fall can't take the ward below **20%**: only the attackers can bring it down, so dying alone never loses a breach. The 300 s "held" rule stays, but it no longer pays to die and come back healed |
| **Co-op** | Friends present: the downed rule applies at home too (45 s to be helped up; helped up costs nothing). A real fall costs 10% as solo |
| **Hooks** | `WardBreachDirector.OnPlayerDied` (damage the ward anchor), `HomeRevival` (delay from the breach), `CoopDowned` (allow in the Haven) |
| **Effort** | **S** (solo), **M** with co-op downed at home |
| **Risks** | Weaker players lose more breaches. The 20% floor and the existing strain rules keep it bounded |

### Option D: Plunder (losing a breach)

| | |
|---|---|
| **Lost** | When the ward falls, the band carries off **20% of the three most valuable material stacks** in the stash (`GameSession.Stash`), at most **60 units each**. Never gear, tools, unique items or quest items. Guards who fought come home **Bruised** (−30% work until the next dawn, the foray rule). Strain and broken buildings stay as now |
| **Won back** | The band's leader walks **the next raid** carrying the plunder, named in the briefing ("It still carries what it took from your stores"), as a Lost One carries what it took. Laying it to rest returns it all. Until then it keeps it |
| **Numbers** | Recovery card: "They took 40 copper ore, 25 iron and 12 linen." No building destroyed, nobody lost, no tier or renown lost |
| **Co-op** | Only the host's stores. Guests lose nothing for a host's breach |
| **Hooks** | `WardBreachDirector.OnWardFell`, `StashStorage`, `LostOnes`/`RaidLostOnes` (carried stacks, `carriedCap`, briefing line), `EliteCreature`, `ResidentRecord.Bruised`, `QuestDeeds` |
| **Effort** | **M** (reusing the Lost Ones' carrying), **L** with a new carrier creature |
| **Risks** | A big stash makes 20% feel huge; the per-stack cap bounds it. Must never take the materials of an upgrade already under way |

### Option E: Death rules as a choice (presets)

| Preset | Home death | Breach | Lost breach |
|---|---|---|---|
| **Gentle** | Today's rules (wake whole) | Today's rules | Strain and repairs |
| **Normal** (default) | Options A + B, boons end | Option C | Option D |
| **Harsh** | Cairn takes equipped gear too; lessons 50% | C, 15% a fall | D, 35% |

Chosen at a new game, changeable any time in the options. A `DeathRules` playtest row marks runs, as `Cheat` rows do.
**Effort M.** Hooks: a `DeathSettings` asset (all numbers above), the new-game screen, the options window. A permadeath mode
(GDD §8.4 mentions one) is not recommended now.

### Small additions that go with any of these

- **Boons end on a fall:** meals and tonics stop (`PlayerBoons`), as Valheim's food does. The cooked meal was the price.
  Effort S.
- **A home death card:** what the fall cost, the last blows (`RecentBlows`, already used by the raid report) and where the cairn
  lies. Effort S.
- **The prologue and the first breach keep today's rules.** They teach; they should not punish.
- **Raids stay as they are.** They already have the heaviest cost in the game.

### 4.6 Recommended combination

| Situation | Cost | Won back by |
|---|---|---|
| **Death at home** | Bag into a cairn (A), lessons −25% of the next level (B), boons end | The cairn: all items and half the lessons |
| **Death in a breach** | As at home (the 10-minute grace means one lesson loss a breach), plus the ward loses 10% and waking takes 10 s and more (C) | The cairn, after the fight |
| **Losing a breach** | Strain and repairs as now, plus plunder and bruised guards (D) | Laying the band's leader to rest in the next raid |
| **Death in a raid** | Unchanged | Unchanged |

Each death costs one thing the player values and can see coming, and most of it can be won back by going back. A worked
example: a player with Mining 22 and Melee 18 walks to the quarry at dusk with 40 copper ore, and a boar brings them down in
the dark. They wake at the portal, unfed, with Mining 22 down about 100 experience and Melee about 70, and a cairn with 40 ore out in
the dark. Going back with a torch returns the ore and half the lessons; going back carelessly risks a second cairn.

**Build order:** C first (S, and it removes the free heal in breaches), then A with the death card and boons, then B, then D,
then E once the numbers are tested.

**How to tell it works (playtest log):** new rows `HomeDeath` (where, by what), `CairnRecovered` (seconds after waking),
`LessonsLost`, `BreachFall`, `Plundered`. Good signs: home deaths stay rare (under 1 an hour), most cairns are recovered within
5 minutes, breaches are still won at least 3 times in 4, and home play at night does not stop altogether.

---

## 5. Open questions for the user

1. **Should skills also fade on a raid death?**
   *Recommended: no.* A raid death already loses the bag for good and wears the gear, and GDD §8.3 promises skills intact.
   One heavy cost per death: in a raid the bag, at home the lessons. Revisit after a playtest.
2. **How big is the skill setback?**
   *Recommended: 25% of the next level, never a level, half back from the cairn, 10-minute grace.* Valheim's 5% of the whole
   skill is harsher at high levels and is what its players complain about.
3. **Does the cairn take equipped gear?**
   *Recommended: no* (Normal); yes on Harsh. Raids keep deployed gear too, and losing your sword at home would make the trip
   back the most dangerous one.
4. **Do meals and tonics end on a fall?**
   *Recommended: yes.* It gives cooking a stake and costs no new system.
5. **Should each fall in a breach cost the ward 10%, with a floor at 20%?**
   *Recommended: yes.* It ends "die to heal" and still means a fall alone never loses a breach.
6. **Should a lost breach let the attackers carry off stores?**
   *Recommended: yes, 20% of the three most valuable stacks, at most 60 each, won back by laying the band's leader to rest in
   the next raid.* Buildings and people stay safe, as the GDD promises.
7. **Death presets (Gentle / Normal / Harsh)?**
   *Recommended: yes, Normal as the default, changeable any time, logged.*
8. **Should co-op friends be able to help a downed player up at home and in breaches, as on raids?**
   *Recommended: yes.* Helped up costs nothing.
9. **Should the first breach and the prologue stay free?**
   *Recommended: yes.*
10. **A permadeath (Hardcore) mode?**
    *Recommended: not now;* look again once the Normal rules have been played.

---

## Sources

1. Valheim death settings, tombstone, No Skill Drain, Corpse Run: [gamever.io](https://gamever.io/knowledge-base/what-death-actually-costs-in-valheim-all-six-death-penalty-settings)
2. Valheim player views on skill drain: [Steam discussions](https://steamcommunity.com/app/892970/discussions/0/4939856028569083841); [Freak Hosting: skill drain setting](https://help.freakhosting.com/games/valheim/configuring-skill-drain-on-death)
3. Enshrouded: [allthings.how](https://allthings.how/enshrouded-death-explained-dropped-items-respawn-and-loot-loss-settings/); [Host Havoc: tombstone mode](https://hosthavoc.com/wiki/game-servers/enshrouded/tombstone-mode)
4. Palworld: [Destructoid](https://www.destructoid.com/how-death-works-in-palworld-do-you-lose-your-stuff/); [Physgun: death penalty settings](https://physgun.com/help/game-hosting/palworld/how-to-change-death-penalty-settings-on-a-palworld-server/)
5. RuneScape: Dragonwilds: [Deltia's Gaming](https://deltiasgaming.com/?p=205682); [Steam discussion](https://steamcommunity.com/app/1374490/discussions/0/604153745761178112)
6. V Rising: [Slyther Games](https://www.slythergames.com/2022/05/20/v-rising-what-happens-when-you-die/)
7. Dune: Awakening: [Beebom](https://beebom.com/dune-awakening-death-penalty-explained); [Mein-MMO: durability change](https://mein-mmo.de/en/dune-awakening-haltbarkeit-frisst-items,1551530); [PC Gamer: sandworm deaths](https://www.pcgamer.com/games/survival-crafting/dying-by-sandworm-in-dune-awakening-is-way-way-worse-than-any-other-death-you-might-face)
8. Soulmask: [GameSkinny](https://www.gameskinny.com/tips/how-to-revive-yourself-in-soulmask/)
9. Core Keeper: [Core Keeper wiki: Death](https://core-keeper.fandom.com/wiki/Death)
10. Grounded 2: [Game8](https://game8.co/games/Grounded-2/archives/539777)
11. Sons of the Forest: [Pro Game Guides](https://progameguides.com/sons-of-the-forest/what-happens-if-you-die-in-sons-of-the-forest)
12. Terraria: [Difficulty mode](https://terraria.wiki.gg/wiki/Difficulty_mode); [Mediumcore](https://terraria.wiki.gg/wiki/Mediumcore)
13. Minecraft: [Minecraft wiki: Death](https://minecraft.fandom.com/wiki/Death)
14. Don't Starve Together: [Health Meter](https://dontstarve.wiki.gg/wiki/Health_Meter); [Resurrection](https://dontstarve.wiki.gg/wiki/Resurrection)
15. 7 Days to Die: [Game Settings](https://7daystodie.wiki.gg/wiki/Game_Settings); [Death](https://7daystodie.fandom.com/wiki/Death)
16. Elden Ring: [Runes](https://eldenring.fandom.com/wiki/Runes)
17. Hollow Knight: [Shade](https://hollowknight.wiki.fextralife.com/Shade)
18. Diablo II: [Maxroll: death](https://maxroll.gg/d2/resources/death)
19. Path of Exile 2: [Game8](https://game8.co/games/Path-of-Exile-2/archives/494914); [forum: punishing death penalties](https://www.pathofexile.com/forum/view-thread/3774790)
20. Escape from Tarkov: [insurance guide](https://eft-ammo.com/wiki/escape-from-tarkov-insurance-guide)
21. Arc Raiders: [safe pocket](https://allthings.how/arc-raiders-safe-pockets-increase-slots-and-keep-loot-on-death); [revives](https://www.gamesradar.com/games/third-person-shooter/arc-raiders-revive-enemies-dead-teammates)
22. Valheim raids: [Valheim wiki: Events](https://valheim.fandom.com/wiki/Events)
23. 7 Days to Die blood moon: [Blood Moon Horde](https://7daystodie.wiki.gg/wiki/Blood_Moon_Horde)
24. Palworld raids: [Palworld wiki: Raids](https://palworld.wiki.gg/wiki/Raids); [GamesRadar: Bellanoir](https://gamesradar.com/the-first-palworld-raid-has-arrived-and-the-survival-games-community-is-shouting-one-base-saving-tip-from-the-rooftops-bellanoir-will-destroy-everything-you-build)
25. Conan Exiles: [Funcom forum: purges should not destroy builds on PvE](https://forums.funcom.com/t/purges-should-not-destroy-builds-on-pve-and-pve-c/190301)
26. Josh Bycer: [The Hierarchy of Fail States](https://www.gamedeveloper.com/design/the-hierarchy-of-fail-states); [The Varying Degrees of Death in Games](https://gamedeveloper.com/design/the-varying-degrees-of-death-in-games); also [Engadget: MMO death mechanics](https://www.engadget.com/2014-01-15-mmo-mechanics-exploring-death-mechanics.html) (corpse runs as a time cost)
27. Co-op revives: [Kotaku: please stop quitting before I can revive you](https://kotaku.com/please-stop-quitting-before-i-can-revive-you-1841606763)
28. Hades: [Game Developer: Kasavin on death](https://gamedeveloper.com/design/how-supergiant-weaves-narrative-rewards-into-i-hades-i-cycle-of-perpetual-death)

Code read: `Settlement/HomeRevival.cs`, `Settlement/WardBreachDirector.cs`, `Settlement/WardBreachSettings.cs`,
`Settlement/SettlementState.cs`, `Settlement/ResidentDefender.cs`, `Settlement/HomeAssault.cs`, `Settlement/CorruptionNights.cs`,
`Raids/RaidDirector.cs`, `Raids/Lost/LostOneSettings.cs`, `Durability/DurabilitySettings.cs`, `Player/PlayerSnapshot.cs`,
`Progression/ExperienceTracker.cs`, `Progression/LevelingSettings.cs`, `Progression/CharacterProgress.cs`,
`Multiplayer/CoopDowned.cs`, `Multiplayer/HavenVisit.cs`, `Items/DroppedItem.cs`, `Territory/ForaySettings.cs`;
`Docs/GameDesignDocument.md` §6.3, §6.6-6.9, §8.3-8.4; `Docs/Skills.md` (pace).
