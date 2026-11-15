# Enchanting research: from a stat shop at the altar to runes that make a build

Research date: 2026-11-15, after the player stats work (`Docs/PlayerStatsResearch.md`, built the same day). Read-only: no Unity, no
CLI, no project edits apart from this file. Paths are relative to `d:\Safe Haven\Safe Haven`, scripts under `Assets/_Project/Scripts/`,
data under `Assets/_Project/Data/`, and `file:line` is the `test` branch at `39d6e65b`.

**The question (the user):** "take a look at enchanting. This feature is very basic right now, but I want this to be a big driver on
how the player can build his character. I want enchanting recipies are gonna be exciting drops that can mainly drop from raids. Best
from bosses, or elites. I want player proficiency to be a driver in enchanting. Maybe reading magical stones in the world or raids can
increase the players proficiency, allowing him to learn more powerfull enchantments, or maybe we should have one of the unlockable NPC's
be the one that enchants? Giving recipies to her, and doing her quests increases her skills in enchanting. Finding magical stones in
raids, can allow the player to transcribe their runes on a parchment, made by fiber or something familiar. this can be in adition to
drops from enemies. So take a look at the different suggestions. Look for ways this can fit in Safe haven. What do we have today? What do
we need to make this a complete feature? Research methods other games do this, and suggest how we can implement this in safe haven,
making raids and progression feel exiting, allowing the player to fine tune his build or completely changes how certain items or systems
work. Do a deep analysis on how this can work in line with other systems in the game and report"

## 0. Summary

**The three biggest findings:**
1. **Enchanting today is a shop, not a discovery.**
   - All 14 enchantments are offered at the altar the moment it stands, for 8 to 18 essence. Nothing is learned, found, or gated by a
     skill, a person or a tier (`Essence/AltarWork.cs:151-179`, `EssenceSettings.enchantments`).
   - The altar comes with Ingrid, the first rescue (`Docs/RescueOrder.md:31`), so the whole list is open from about raid 1.
   - Every enchantment is a flat number. None changes how anything plays.
   - Altar work trains no skill, although the altar is Runecraft's mentor building (`Docs/Skills.md:257`). Altar upgrades only speed
     up purifying. Nothing in a raid teaches enchanting.
   - **The build-changing part already exists, as relics.** `RelicEffect` gives 8 powers that change play (`Essence/RelicEffect.cs`).
     But relics are only found: a random piece of gear with a random power, which can never be learned, made or moved.
2. **It is the biggest unguarded source of power left.**
   - One enchantment per piece, on 9 fighting pieces, and the same one may go on every piece.
     - 7 Mossgrown give +140 health, nearly the whole tier I kit's 150. That alone is ×1.93 toughness at tier I. On top of the
       stacked kit it is about ×2, against the ×1.5 reach.
     - A shield counts as a weapon, so Bloodletting on sword and shield gives +30% damage.
     - Reaping on four pieces doubles the Cursed Essence a kill gives.
   - `GearBalance.StackedKitOf` counts neither enchantments nor relics (`Editor/Raids/RaidTierEditor.cs:129-227`).
   - Gear score adds 10% per enchanted piece (`GearScoreSettings.asset`, `Items/GearScore.cs:34`). That is about 2 points on a tier I
     kit, where the next tier recommends 30.
   - Relic powers stack per piece too: two Hushed pieces leave a quarter of your noise (`PlayerRelics.cs:93-111`).
3. **Nobody has used it, and nothing would show it.**
   - The 1,225 playtest files show no Purification Altar built in any real run, and no enchanting, empowering or relic.
   - The two real runs of 2026-11-14 freed Ingrid and gathered 66 and 91 Cursed Essence.
   - The log has no row for altar work. So the design is free to change without taking anything from anyone's play, and nothing about
     it can be measured yet.

**What the user's ideas come to:**

| Idea | Verdict | Where |
|---|---|---|
| 1. Recipes as exciting raid drops, best from bosses and elites | **Yes, the core.** A *stave* (the recipe) teaches an enchantment for good. A boss's first kill always gives one. An elite drops the stave of its own affix, so you take home the trick that nearly killed you | #2, #3 |
| 2. Player proficiency drives enchanting | **Yes, through Runecraft,** the skill that already says "Purified and Cursed alike". No new skill and no new bar. Each enchantment needs a Runecraft level, as special recipes already need a skill, and work at the altar trains it | #4 |
| 3. Reading magical stones raises proficiency and unlocks stronger enchantments | **Yes, merged with 5.** Rune stones give Runecraft experience once, and higher-tier stones hold stronger runes | #5 |
| 4. An unlockable NPC enchantress, growing with recipes and quests | **Yes, as Liv the Rune Scholar, but as a second route, not a gate.** Her worker rank, which already exists, lets her carve past your own Runecraft. It grows with altar work, staves handed to her (duplicates) and her Tales | #6 |
| 5. Transcribing stones' runes onto parchment made from fiber | **Yes.** A Rubbing Sheet (Nettle Fiber and Charcoal, both tier I) taken into a raid turns a rune stone into a stave you must carry home | #5 |
| "Fine-tune, or completely change how items or systems work" | **Two layers.** Stat runes on armour fine-tune and count once. **Great runes** on weapons and jewellery carry a verb through the relic hook (parry, Focus, Regain, poise, dodge class, stealth, bow, staff) | #7 |

None of the ideas is dropped. Two limits keep them in line with the rest of the game:
- The enchantress is not the only way in: the altar keeps working from raid 1.
- Stones teach once each and are never a farm.

**Ranked recommendations** (detail in §3.3):

| # | Recommendation | Impact | Effort | Changes balance? | Needs sign-off? |
|---|---|---|---|---|---|
| 1 | **Count and bound.** `StackedKitOf` counts enchantments and relic numbers. Each enchantment counts once however many pieces carry it. Trim the toughness leaders until `RaidTierTests` passes | High: guards the tiers, and everything below needs it | S-M | Yes, down from today's worst case | Yes: the once rule and the trims |
| 2 | **Enchantments are learned, not bought.** Staves teach them for good (per character, read at home). The altar offers what you know and shows what you don't, and where it comes from. Old saves know all 14 | High | M | No (it gates) | Yes: a new game starts with the 7 Purified; the 7 Cursed become staves |
| 3 | **Drops with a source and a theme.** Boss first kill: one great-rune stave, guaranteed. Elites: their affix's stave by tier chance, never one you know while others remain. Caches and rumour events: rare. Named in the raid report and the planner | High: raid excitement | M | Only through #1's reach | Yes: the rates |
| 4 | **Runecraft is the enchanter's skill.** Altar work trains it, and each enchantment has a Required Runecraft, as recipes already have | Med-High | S | No | Light: the levels |
| 5 | **Rune stones and rubbings.** Raid set pieces hold rune stones. Reading one gives Runecraft experience once a game. With a Rubbing Sheet in the bag it gives that stone's stave, which must be extracted | High | M | No | Light: the sheet recipe |
| 6 | **Liv carves.** Her rank lets her carve 5 to 20 Runecraft levels past yours. It grows with altar work, staves handed to her and her Tales, built from deed objectives. Rescue her earlier | Med-High | M | No | Yes: the rescue order |
| 7 | **Great runes that change how things work.** Carved runes with a verb (the relic hook), on weapons, off hand and jewellery only. 10 to 14 to start, one per elite affix and one per boss, plus a few Cursed bargains | High: build identity | L | Yes, counted by #1; verbs, not damage numbers | Yes: each rune |
| 8 | **Readable.** The Enchant page lists known and unknown runes with their source. Tooltips say "counts once" and "great rune". New playtest rows for altar work and staves. Staves and relics named in the raid report | Med: enables tuning | S | No | No |
| 9 | **Co-op gaps.** A friend's runes that put a status on a creature reach the host's creature, through a message like the stagger one. A friend's enchanted weapon glows | Med | S-M | No | No (wire bump) |

**Not recommended** (§3.2):
- **Dice at the altar:** random rolls, rerolls, failure chances or random affixes. The GDD rule is that "nothing fails, breaks or
  wastes essence". Monster Hunter World's random decorations are the cautionary tale.
- **Sockets on top of one enchantment per piece.**
- **A 14th skill for enchanting:** Runecraft already is it.
- **Single-use recipes:** Diablo IV's stash full of aspects, before Season 4.
- **Conditional "+X% damage to Y":** Diablo IV removed these in Season 4.
- **Learning a relic's power by breaking it:** not now; it breaks the GDD's promise that the altar can't make a relic.

## 1. How it works today

### 1.1 The pipeline, end to end

| Piece | Where | What it does |
|---|---|---|
| `EnchantmentDefinition` | `Essence/EnchantmentDefinition.cs:33-57` | Id (saved), name, description, school, what it fits, cost, stat bonuses, and a `RelicEffect` list |
| Schools | `:6-14` | **Purified** (safe, paid in Purified Essence), **Cursed** (raw Cursed Essence, always with a stated price), **Relic** (found only) |
| What it fits | `:18-25, 62-85` | Weapon, Armor, Jewelry, Tool. **A shield is a weapon** (`IsWeapon => weapon != null`, `Content/ItemDefinition.cs:207`), so it takes weapon enchantments. Backpacks take none |
| The list | `Essence/EssenceSettings.cs:51-52, 89-100` | 14 enchantments in `Resources/EssenceSettings`. Lookup falls back to `RelicSettings` |
| At the altar | `Essence/AltarWork.cs:151-209` | For every piece worn, in the bag or in the pocket: every enchantment that fits. Paid from the `MaterialPool`. A relic piece is skipped (`:157-161`). Replacing costs the new one in full |
| What it gives | `Essence/GearPower.cs:15-32`; `Items/GearSet.cs:207-228` | The stack's bonuses are added to the stat sheet while worn and not broken, slot by slot. Nothing stops the same enchantment counting on every piece |
| Effects | `Essence/PlayerRelics.cs:93-111` | Starts the `RelicEffect`s of **any** enchantment on worn gear, not only relics. Each piece attaches its own copy, so two pieces double it (Twice-Buried excepted: it is spent per power, `RelicEffect.cs:88`) |
| Saved | `Save/SaveMapper.cs:188, 842-844` | By enchantment id on the slot. An unknown id is reported missing and the item loads plain |
| Shared stash (co-op) | `Multiplayer/StashShare.cs:150-152, 232, 243` | The id travels with the item |
| Shown | `UI/ItemTooltipView.cs:740-762`; `Essence/GearGlow.cs` | Name and bonus lines in the tooltip, effects in the accent colour, "A relic. Nobody at home could make this." Slot frame tint and weapon motes by school (`HeldItemView.cs:176-182`) |
| Gear score | `Items/GearScore.cs:23-36`; `GearScoreSettings.asset` › Per Enchantment 0.1 | +10% of the piece's score, whatever the enchantment |
| Balance model | `Editor/Raids/RaidTierEditor.cs:122-227` | `StackedKitOf` adds perks, meals, tonics and Well Rested. **No enchantments, no relics.** Empowering is left out by design (`:126-127`) |
| Tests | `Editor/Tests/EssenceTests.cs` | Work stays with the item; saves keep it; every Cursed enchantment has a stated price and only Cursed ones do (`:214-237`); there is a safe and a risky choice for every kind of gear |

### 1.2 What there is: 14 enchantments and 8 relic powers

| Enchantment | School | Fits | Gives | Price | Cost |
|---|---|---|---|---|---|
| Mossgrown | Purified | Armour, jewellery | +20 health | | 10 PE |
| Hearthbound | Purified | Armour | +8 armour | | 10 PE |
| Springfed | Purified | Armour, jewellery | +20 stamina | | 8 PE |
| Fleetfoot | Purified | Armour | +5% speed | | 12 PE |
| Ashwarded / Frostwarded | Purified | Armour, jewellery | +15% fire / frost resistance | | 8 PE |
| Sure-Handed | Purified | Weapon | +6% crit | | 12 PE |
| Patient | Purified | Tool | +10% yield | (+20% tool care) | 8 PE |
| Bloodletting | Cursed | Weapon | +15% damage | -15 health | 14 CE |
| Gravebound | Cursed | Armour | +16 armour | -6% speed | 14 CE |
| Hollow | Cursed | Weapon | 6% life drain | -6 armour | 18 CE |
| Reaping | Cursed | Weapon, jewellery | +30% Essence Find | -10 health | 16 CE |
| Witchlit | Cursed | Jewellery | +10 spell power | -15% frost resistance | 14 CE |
| Greedy | Cursed | Tool | +20% yield | -30% tool care | 10 CE |

PE is Purified Essence and CE is Cursed Essence (`Data/Essence/Enchantments/*.asset`). The relic powers (`Data/Essence/Relics`, GDD
§13.6) are Wolf-Fed, Hushed, Twice-Buried, Moonbound, Harefoot, Far-Seeing, Trollblooded and Rime-Touched. Each is a `RelicEffect`
class: `FedByKillsEffect`, `HushedEffect`, `DeathlessEffect`, `MoonboundEffect`, `AfterDodgeSpeedEffect`, `FarSeeingEffect`,
`MendingEffect` and `StatusOnHitEffect` (`Essence/RelicEffect.cs:27-282`).

The design rules in GDD §13.5 are good and worth keeping (`Docs/GameDesignDocument.md:772-783`):
- every use is deterministic, and the price of a risk is stated, never rolled;
- Cursed work is stronger and costs you something;
- the work belongs to the item and survives a failed raid.

### 1.3 Where enchantments come from: nowhere

- **No knowledge.** There is no "known enchantments" anywhere: not in `CharacterProgress`, not in `PlayerCrafting`. The altar reads
  the whole list (`AltarWork.cs:164`).
- **No skill.**
  - Runecraft's description is "Old words cut and spoken: Purified and Cursed alike." Its mentor building is the altar
    (`Data/Proficiencies/Runecraft.asset`).
  - It is trained only by spells (`Docs/Skills.md`, "What trains what"), and its level bonuses are Spell Damage and spell stamina.
  - `AltarWork.Finished` posts a message and plays a sound; it grants no experience (`:331-335`).
- **No person.** The altar needs Ingrid only to *purify* (`UI/AltarWindow.cs:333-342`, `SettlementSettings.PurifierJob`).
  Enchanting needs nobody.
- **No tier.** The GDD promises "Tier 3+: ... advanced enchantment" (`GameDesignDocument.md:758`); nothing is gated. The altar's
  upgrades, Carved and Radiant, add 25% purified yield each and nothing else (`Data/Buildings/PurificationAltar.asset`).
- **No raid source.** No item, rumour, boss or event teaches an enchantment. Raids give the essence and the relics.

### 1.4 How far it stacks

Fighting pieces that take an enchantment:
- main hand and off hand (both "weapon");
- head, chest, hands, waist and feet ("armour");
- neck and one ring ("jewellery", `Items/GearSlot.cs:59-72`).

Arithmetic with `GearBalance`'s own formula, toughness = health ÷ (1 − armour ÷ (armour + 100)), against the plain kits in
`PlayerStatsResearch.md` §1.6 (the choice of stacks is mine):

| Tier | Plain kit toughness | 7 Mossgrown (+140 health) | Best mix (Mossgrown and Gravebound) | One of each (Mossgrown, Hearthbound, Gravebound) | Stacked kit today | Stacked + 7 Mossgrown, at least |
|---|---|---|---|---|---|---|
| I | 198 | ×1.93 | ×2.09 | ×1.34 | ×1.21 | ×2.1 |
| II | 267 | ×1.87 | ×1.94 | ×1.29 | ×1.27 | ×2.1 |
| III | 501 | ×1.61 | ×1.65 | ×1.21 | ×1.28 | ×1.9 |
| IV | 682 | ×1.58 | ×1.58 | ×1.17 | ×1.44 | ×2.0 |

The last column adds the enchantments' toughness to the stacked kit's at the plain kit's armour, which is a lower bound. The reach that
`RaidTierTests` guards is ×1.5 toughness and ×1.35 damage (`Editor/Tests/RaidTierTests.cs:158-172`).

**Damage.**
- Bloodletting on sword and shield is +30% Damage Bonus, which adds to meals' Damage Bonus.
- The stacked kit is already ×1.30 at tier II, so it would reach about ×1.6 to ×1.7. Even one Bloodletting puts tiers II to IV near
  ×1.45.
- A two-hander takes only one.

**Essence.**
- Reaping is +30% Essence Find per piece, and Essence Find adds up (`GearPower.Reaped`, `:125-128`).
- Four pieces (sword, shield, neck, ring) give +120%: 2.2 times the Cursed Essence, for -40 health. More essence buys more
  enchantments.

**Price.**
- The real runs of 2026-11-14 brought home about 45 Cursed Essence a raid at tier I. At 2:1 that is about 22 Purified Essence.
- Seven Mossgrown cost 70 Purified Essence: about three raids of essence, if it all went there (an estimate; the ward, empowering and
  abilities compete for the same essence).

**Gear score doesn't see it.** A fully enchanted tier I kit scores about 21 instead of 19. Tier II recommends 30. So the portal and the
planner read a ×1.9-toughness kit as an ordinary tier I kit.

**Relics stack as well,** because `PlayerRelics.PutOn` attaches each worn piece's effects:
- two Wolf-Fed weapons (sword and shield) give back 12% health per kill;
- two Hushed pieces leave 25% of your noise;
- two Moonbound pieces give +60% damage at night.

Relics are random finds, so this needs luck, but the stacking rule should be the same for both.

### 1.5 What the player sees

- **The altar window** (`UI/AltarWindow.cs:39`) has four pages: Purify, Empower, Enchant and Abilities. The GDD still says
  "Skills", `:783`.
  - Enchant lists every piece in reach with every fitting enchantment, each with its bonus line, the Cursed tag and the price.
  - It is a flat list of 14. Nothing is locked, so nothing is a goal.
- **The tooltip** names the enchantment and its bonuses (`ItemTooltipView.cs:745-762`). It doesn't say that a second copy stacks,
  or what a number means in blows.
- **In the world:** the frame tint and corner text in slots, and motes on the weapon in hand (`Essence/GearGlow.cs`). A co-op
  friend's weapon never glows (§1.9).
- **The raid report** lists relics by their plain item name (`Docs/TODO.md:662`).

### 1.6 Runes and Runecraft today

"Rune" already names six different things. Enchantments are not one of them.

| Thing | Where | What it is |
|---|---|---|
| **Art runes** (8) | `Data/Items/Runeof*.asset`; `Progression/ArtRunes.cs`; recipes `Data/Crafting/Recipes/Runeof*.asset` | Made at the Cauldron from 2 Bone, 1 Nettle Cord and 1 Orb Tincture. Known from the start, but need **Runecraft 5**. Used from the bag, a rune attunes every weapon of its kind to a second art, paid with Focus. One per kind, saved in `CharacterProgress` (`:41-70`) |
| **Raid runes** | Hush, Deep, Blood Moon, Plenty (`ItemDefinition.raidModifier`) | Carved on the portal to change a raid. Hush and Deep are "Liv's" (`Data/Settlement/RuneScholar.asset` teaches 6 recipes) |
| **Wayfinder Rune** | `Data/Items/WayfinderRune.asset` | A vegvísir in stone made at the Workshop: the way home opens faster |
| **Pact Rune** | `Data/Items/PactRune.asset` | Claims a region (Territory) |
| **Runestone Fragment** | `Data/Items/RunestoneFragment.asset` (tier II, rare) | A raid material from supply caches, The Living Path and the Trader's Wagon. Used by the Ward Totem and an order |
| **Rune Book, experience stones** | `Data/Items/RuneBook.asset` (120 Insight); `Progression/ExperienceStone.cs` | Read for **Insight** once. Stones are remembered per scene and place (`:22-31`): 7 in the prologue and 3 fixed in `Raid_MistyWoods` |

**Runecraft** (`Data/Proficiencies/Runecraft.asset`) is a combat skill:
- **Level bonuses:** Spell Damage -12% at level 1 to +20% at 50, and spell stamina +25% to -20%.
- **Perks:** Rune Lore, Warding Signs, Sharp Runes, the Spell Echo technique, Holy Marks, Deep Words, Runes of Ruin, the Soul Siphon
  technique, Runemaster and Cursed Tongue.
- **Mentor building:** the altar.
- **Teacher:** Liv, the Rune Scholar, gives lessons in it between raids (`Docs/Skills.md:166`).

**Places for "magic stones" already exist:**
- **The Standing Stones** set piece, guarded by wolves, plus a tier II creature (`Data/Raids/SetPieces/Standing Stones.asset`).
- **Liv's rescue site,** "The Humming Stones" (`Data/Raids/Rumours/Liv.asset`, `Events/LivsCircle.asset`).
- **The Living Path** event: "Three runestones stand where the path should be" (`Data/Raids/Events/LivingStones.asset`). It was
  logged in 6 runs.

### 1.7 What raids hand over today, and how

- **Relics:**
  - **Great trolls** give one every time: `LootOnDeath.relicChance` is 1 on the Old Moss Troll, Stone Troll and Bergjutul
    (`Items/LootOnDeath.cs:18-19, 110-114`).
  - **Elites** give one at 0/5/10/15% by tier (`RaidDifficulty.eliteRelicChance`, `Raids/Elites/EliteCreature.cs:249-252`).
  - **Supply caches:** 6% × the loot multiplier (`Raids/LootCache.cs:86-91, 152`).
  - **Rumour events:** `GiveRelicOutcome` (`Raids/Events/RaidEventOutcomes.cs:302-315`).
  - A relic is a random fitting item and power up to one tier above the raid (`Essence/RelicSettings.cs:42-68`). It arrives with the
    Legendary pickup sound and "A relic: ...!".
- **Knowledge:**
  - Blueprint items teach recipes or buildings when read (`ItemDefinition` › Teaches Recipes, `Content/ItemDefinition.cs:136-144`).
    **Learned At Home** means it must survive the way back: the Moonsilver Folio, the Bathhouse Plans.
  - A rumour can lead to a teaching item until everything in it is known (`RaidRumourDefinition.teaches`, `:40-41, 126`).
  - A creature can teach recipes when it dies (`LootOnDeath.unlocksRecipes`).
  - A `BossDefinition` gives an ability for good on the first win: the Heartroot gives Heartroot's Grasp (`AI/BossDefinition.cs`).
- **Elites:** 10 affixes, each a learnable trick with a stated answer (`Data/Raids/Elites`). Ironhide and Frenzied from tier I;
  Frostbound, Regrowing, Unyielding and Watchful from II; Mistcloaked, Relentless and Vengeful from III; Rallying at IV. Elites per
  run are 1, 1-2, 2-3 and 3-4.
- **Personal loot in co-op:** when a friend helped kill a creature, it dies in that friend's game with them as the killer
  (`Multiplayer/CreaturePuppets.cs:352-356`). Each player rolls their own drops, and raid loot is never shared
  (`Multiplayer/CoopDrops.cs:8-12`).

### 1.8 Residents, ranks and quests: what an enchantress can be built on

- **Worker ranks already exist.**
  - A resident's `WorkDone` grows with every craft at their workplace (`Settlement/SettlementState.cs:45-46`).
  - The ranks are Novice, Apprentice (10), Journeyman (30), Expert (70) and Master (150), each faster and luckier
    (`Settlement/SettlementSettings.cs:69-72`).
  - The rank is saved, and shown on the People page.
- **Lessons:** every resident with a job gives one lesson in their skill between raids. Liv's is Runecraft.
- **Orders:** residents order a Fine or Masterwork piece once the player's skill reaches a level (`HavenRequestDefinition` › Quality,
  From Level). The order pays coin, a full lesson and renown.
- **Liv** (`Data/Settlement/RuneScholar.asset`): "Knows the old marks: she teaches runes nobody else can". She works at Ingrid's
  Cauldron and teaches six raid-rune recipes. She is **12th of 14** in the rescue order, "Mid game" (`Docs/RescueOrder.md:42`).
- **Quests:**
  - Five categories: Main Story, The Lost Expedition, Haven, Tales and Errands (`Docs/QuestRedesign.md:76-87`).
  - Deed objectives (Talk To, Reach Place, Use Object, Collect, Craft...) take an assigned target. The project's rule is never a Quest
    Event per person or place (`Quests/QuestDeeds.cs`).

### 1.9 Save and co-op

- An enchantment is runtime state on the `ItemStack`, saved by id. Raid drops between friends carry the dropped item's whole state
  (`CoopDrops.cs:159-178`), and the shared stash carries the id.
- **A friend's look sends item ids only** (`Multiplayer/PlayerLookState.cs:77-92`), and the remote held model is made from the item
  alone (`RemotePlayerBody.cs:125`). A friend's enchanted weapon shows plain.
- **Status effects from a friend don't reach the host's creature.**
  - Co-op creature messages are spawn, poses, strike, died, hit, gone, wake, call and stagger (`Multiplayer/CoopCreatures.cs:21-29`).
    There is no status message.
  - A friend's Rime-Touched chill lands on their own copy of the creature. The host's creature, which does the fighting, is not
    slowed (read from the message list; §4).
- Relic effects that act on the wearer (health, stamina, noise, speed) need nothing: each game runs its own player.
- Wire format is 24 (`Multiplayer/MultiplayerSettings.cs:85`).

### 1.10 Real play

The playtest CSVs (`%USERPROFILE%\AppData\LocalLow\DefaultCompany\Safe Haven\Playtests`) hold 1,225 files, most of them probes.
- **991 runs start in `SafeHaven`** and 24 start in `Raid_MistyWoods`: probes and tests. Only 16 runs last 20 played minutes or more.
- **No row in any file names an enchantment, empowering, the altar or a relic.** One `ResourceGathered PurifiedEssence` row exists.
  There is no altar row type to log it with.
- **The real runs:**

  | Run | Length | Raids | Cursed Essence | Notes |
  |---|---|---|---|---|
  | 2026-11-14, the first | 67 min | 1 | 66 | One `Cheat` row. Ingrid freed |
  | 2026-11-14, the second | 63 min | 2 | 91 | No cheats. Ingrid freed |
  | 2026-10-17 | 91 min | 9 short raids | 50 | Old build |

  No run built the altar.

So enchanting has never been played, and the ×2 stacks above are arithmetic only. Caveat: some long runs are agent sessions; I told
real runs apart by their start in the main menu and their length.

### 1.11 What is missing or misleading

- No discovery, no raid source, no skill, no person: the four things the user asks for.
- The GDD's "advanced enchantment" at tier 3+ (`GameDesignDocument.md:758`) has no counterpart in data or code.
- Runecraft is mentored by the altar but never trained there.
- The same enchantment on every piece; double weapon enchantments through the shield; Reaping feeding itself.
- Neither the balance model nor gear score sees enchantments. Relics stack per piece.
- Every enchantment is a number. The verbs live only in relics, which can't be chosen.
- "Rune" means six things, and the word for runes in gear isn't used at all.
- In co-op: a friend's status relics don't reach the host's creature, and a friend's enchanted weapon doesn't glow.
- The raid report lists a relic as its plain item.
- No playtest row.

## 2. What other games do (web research)

Facts come from official or press sources unless marked "(community)". The searches ran on 2026-11-15.

| Game | How it works | What went right or wrong | Takeaway |
|---|---|---|---|
| **Diablo IV** | Each dungeon's first clear unlocks an Aspect in the Codex of Power, account-wide; there are over 100 ([GamesRadar](https://www.gamesradar.com/diablo-4s-codex-of-power-builds-on-one-of-diablo-3s-best-ideas)). Tempering recipes drop widely and are kept for good in the Codex ([PC Gamer](https://www.pcgamer.com/games/action/diablo-4-tempering-manuals-masterworking)) | At launch an extracted aspect was a single-use item, so stashes filled with them ([Dexerto](https://dexerto.com/diablo/diablo-4-season-4-legendary-aspect-change-removes-inventory-hassle-2725485)). Season 4 made the Codex keep "the most powerful version of any Aspect" without inventory space ([Blizzard](https://news.blizzard.com/en-us/article/24077223)) | Learn once, use forever. A place can teach a power |
| **Minecraft** | Up to 15 bookshelves raise the table to level 30 ([Minecraft Wiki](https://minecraft.wiki/w/Enchantment_Table)). Treasure enchantments such as Mending never come from the table, only from loot chests, fishing and villagers (community guides) | The best stay out in the world, so exploring pays | The base comes from home and the best from outside |
| **Skyrim** | You learn an enchantment by disenchanting a found item, which destroys it (magnitude doesn't matter). The Enchanting skill sets the strength; trainers stop at 75 and 90 ([UESP](https://en.uesp.net/wiki/Skyrim:Enchanting)) | The alchemy, enchanting and Fortify Restoration loop made gear without a limit ([UESP talk](https://en.uesp.net/wiki/Skyrim_talk:Fortify_Restoration), community) | Learn from finds; skill gives reach; bound every source |
| **Elden Ring** | Ashes of War (weapon skills) are found and applied at a site of grace. Whetblades found in the world unlock affinities. A duplicate needs a rare Lost Ash of War ([wiki.gg](https://eldenring.wiki.gg/wiki/Ash_of_War), [Fextralife](https://eldenring.wiki.fextralife.com/Lost+Ashes+of+War)) | A found move that changes a weapon is among the most talked-about drops | Build-changers as finds; reapplying is cheap |
| **Path of Exile 2** | Runes, soul cores and talismans go into sockets: one on most pieces, two on body armour and two-handers. Replacing one destroys the old. Soul cores come from the Trials of Chaos ([poe2wiki](https://www.poe2wiki.net/wiki/Rune), [Maxroll](https://maxroll.gg/poe2/resources/runes-and-soul-cores); counts vary between guides) | The socket count is the budget | Bound by slot; the strongest pieces come from one place |
| **Last Epoch** | Affix shards drop from enemies. Each item has Forging Potential, a crafting budget. Legendary Potential plus the Temporal Sanctum, whose key drops from bosses, merges affixes into uniques ([Last Epoch support](https://support.lastepoch.com/hc/en-us/articles/46361924310555-Legendary-Items), [Maxroll](https://maxroll.gg/last-epoch/resources/beginner-crafting-guide)) | Deterministic shard crafting is praised; the budget stops endless stacking | A budget per item; a boss-dropped key gates the best craft |
| **Grim Dawn** | Augments ("powders, meticulously crafted enchantments, tinctures and poisons") come only from faction reputation, one kind each for weapon, armour and accessory ([Grim Dawn](https://www.grimdawn.com/guide/items/factions)). Components come from crafting and dismantling | Players dislike resistance augments sitting behind faction walls ([forum](https://forums.crateentertainment.com/t/s-resistances-augments-should-be-on-a-npc-instead-behind-faction-walls-recommended-for-the-future/91789), community) | Standing with people as the route to better enchantments, but not for basics |
| **No Rest for the Wicked** | Eleanor enchants in Sacrament; her shop reaches level 3 for full service. A gem can't be stacked twice on an item and can't be changed once set ([games.gg](https://games.gg/de/no-rest-for-the-wicked/guides/no-rest-for-the-wicked-crafting-enchanting-guide/), [Fextralife](https://norestforthewicked.wiki.fextralife.com/Upgrades), community) | Players ask for rerolls that don't destroy the item ([forum](https://forum.norestforthewicked.com/t/reroll-enchants-infusions-etc-without-destroying-item/8507)) | An NPC whose workshop grows. The combat rework's model game |
| **Monster Hunter** | World's decorations were random drops. Rise made them craftable at the Smithy from monster parts ([Fextralife](https://monsterhunterrise.wiki.fextralife.com/Decorations)) | Wilds' director: "I ended up finishing the game without having completed my build" ([GamesRadar](https://www.gamesradar.com/games/monster-hunter/monster-hunter-wilds-lead-was-also-crushed-by-monster-hunter-worlds-brutal-decoration-rng-i-ended-up-finishing-the-game-without-having-completed-my-build)) | Never leave the core of a build to dice |
| **Remnant 2** | Mutators (passive weapon effects) drop from bosses and elites and are upgraded with materials. Weapon mods are made from boss materials ([Fextralife](https://remnant2.wiki.fextralife.com/Mutator)) | A boss kill hands over a build piece you can use at once | Bosses and elites give the verbs |
| **V Rising** | Jewels change spells. The jewel and gem tables are unlocked by beating V Blood bosses; jewel rolls are random ([wiki](https://vrising.fandom.com/wiki/Jewelcrafting_Table), community) | Boss-gated stations read well; random jewel rolls are farmed | Bosses open the craft |
| **RuneScape: Dragonwilds** | A Runecrafting skill: essence gathered from the world, a rune altar turns it into runes, and runes pay for spells ([guide](https://allthings.how/runescape-dragonwilds-magic-and-runecrafting-guide/), community) | | The game Safe Haven's skills follow puts magic in a gathered and crafted skill |
| **Valheim / Enshrouded** | Valheim has no enchanting: staves come from the Galdr Table, upgraded by placing a Rune Table beside it ([guide](https://progameguides.com/valheim/how-to-make-and-use-a-galdr-table-in-valheim/)). In Enshrouded, runes from enemies and salvage upgrade found weapons at the Blacksmith ([Twinfinite](https://twinfinite.net/guides/how-to-get-and-use-runes-enshrouded/)) | | Survival games keep magic close to buildings and NPCs |
| **Borderlands 3 / Terraria** | Anointments roll randomly on drops; Gearbox removed 21 from the pool and added a 50-Eridium reroll ([Destructoid](https://www.destructoid.com/the-new-borderlands-3-patch-ushers-in-some-welcome-quality-of-life-changes/)). Terraria's Goblin Tinkerer rerolls random modifiers, priced by his happiness ([wiki.gg](https://terraria.wiki.gg/wiki/Reforging)) | Random layers needed patching and turned into reroll grinds | Avoid dice at the bench |
| **Hades** | The skill system "transformed into the boons that each god gives" ([GameSpot](https://gamespot.com/articles/hades-changes-what-it-means-to-be-a-roguelike/1100-6483420/)). Duo boons need two gods | Players call Hades II's duo requirements too strict ([Steam](https://steamcommunity.com/app/1145350/discussions/2/4353373056338025006), player opinion) | Schools of power that combine; verbs over numbers |

**Takeaways:**
1. **Learn once, carve forever.** This is where Diablo IV Season 4, Monster Hunter Rise and Elden Ring all ended up. A single-use
   recipe becomes inventory clutter.
2. **The base from home, the best from outside, and the place says what it teaches.** See Minecraft's treasure enchantments, Diablo
   IV's dungeon aspects, Remnant's boss mutators and PoE2's soul cores. For Safe Haven: the altar's basics, the raids' great runes.
3. **No dice on the core of a build.** Randomness belongs in what drops, with protection against repeats, not at the bench (Monster
   Hunter World, Borderlands 3, Terraria).
4. **A budget.** PoE2 has sockets, Last Epoch has Forging Potential, and No Rest for the Wicked forbids the same gem twice. Safe Haven
   has one enchantment per piece. It needs "one of each per kit" too.
5. **Skill gives reach; people give a second route.** Skyrim's skill sets strength. Grim Dawn's and No Rest for the Wicked's NPCs
   unlock more as you stand well with them.
6. **The exciting drops are the verbs.** Ashes of War, legendary aspects, mutators and boons change how you play. Numbers fine-tune.

## 3. Recommendations

### 3.1 A big enchanting system: benefits against costs

| Benefit | Its cost | How to get the benefit without the cost |
|---|---|---|
| Builds that differ (the user's goal) | Power creep past the tiers | Count every rune in `StackedKitOf`; each counts once; great runes are verbs, not multipliers |
| Exciting raid drops | Grind and dice (Monster Hunter World) | Boss first kill guaranteed; elites drop their own affix; unknown first; nothing random at the altar |
| Progression that feels like learning | Another bar | Runecraft (exists) and Liv's rank (exists). No new track |
| A colony that matters | NPC gates that block play | Liv extends what the altar can do; she never gates it |
| Changing how items work | Reading a build gets hard | Great runes only on weapons and jewellery; one line each in the tooltip; the character window lists them |
| Co-op build roles | Gaps between friends; sync | Personal loot as today; runes per character; only statuses on creatures need a message |

### 3.2 The options

| Option | Fits Safe Haven? | Verdict |
|---|---|---|
| A. Keep the shop, bound and count it | Needed whatever else is done | **Yes, first** (#1) |
| B. Learned enchantments from raid drops (staves) | GDD §13.5, Blueprints, Diablo IV Codex | **Yes** (#2, #3) |
| C. Only an NPC may enchant | Gates the altar that works from raid 1; Liv comes mid game | No |
| D. The NPC as a second route that grows | Worker ranks already exist (Grim Dawn, No Rest for the Wicked) | **Yes** (#6) |
| E. Random affixes, rerolls, sockets, failure (Diablo IV tempering, PoE, Borderlands 3, Terraria) | Breaks "deterministic, never wastes essence" | **No** |
| F. A new Enchanting skill | Runecraft already is it; the user doesn't want another bar | No |
| G. Break a relic to learn its power (Skyrim disenchant, Diablo IV extraction) | Strong, but the GDD says the altar can neither make nor replace a relic | Later, with sign-off |
| H. Merge art runes into enchanting | Art runes attune a weapon *kind* (`ArtRunes.cs`); enchantments belong to *one piece*. Merged, an art would live on one sword | **No.** Share the skill, the word and, later, the stave as a way to find art runes |

**Words.** Keep "Enchant" and "enchantment" in the UI. Call the recipe item a **stave** (Old Norse *stafr*; the Icelandic *galdrastafir*
are magical staves, and the vegvísir on the Wayfinder Rune is one). Call the strong ones **great runes**. "Rune" then keeps its current
meanings and gains no new one.

### 3.3 Details

| # | Where in the code and data | UI | Co-op |
|---|---|---|---|
| 1 Count and bound | `GearBalance.StackedKitOf` (`Editor/Raids/RaidTierEditor.cs:129-227`); a once rule in `GearPower.BonusesOf` or `GearSet.ApplyStats` (`Items/GearSet.cs:207-228`) and in `PlayerRelics.PutOn` (`:93-111`); trims on `Data/Essence/Enchantments/*.asset`; `RaidTierTests` | "Counts once: already on your Iron Helm" at the altar and in the tooltip | None: each game runs its own player |
| 2 Learned | `EnchantmentDefinition` gains Known From Start, Required Skill and Level (as on `RecipeDefinition.cs:50-59`), and a source hint; `ItemDefinition` gains Teaches Enchantments beside Teaches Recipes (`:136-144`, Learned At Home on); `CharacterProgress` gains a known set beside `stones` (`:18, 386`); `SaveMapper` migration; `AltarWork.Enchanting` (`:151-179`) | Enchant page: known runes as today; unknown ones greyed with "Found: elites of tier II" | Per character, so no sync |
| 3 Drops | `LootOnDeath` (a stave list, first kill guaranteed through `CharacterProgress.TimesSlain`); `EliteAffixDefinition` gains a stave; `RaidDifficulty` gains Elite Stave Chance beside `eliteRelicChance`; a `GiveStaveOutcome` beside `GiveRelicOutcome`; an unknown-first roll | Pickup message and Legendary sound as relics have; raid report; planner line | Personal loot as today (`CreaturePuppets.cs:352-356`) |
| 4 Runecraft | `AltarWork.Finished` practises Runecraft; offers check Required Runecraft and say so in `AltarOffer.Problem`; the Runecraft asset's "trained by" text | "Needs Runecraft 25" on the row | None |
| 5 Stones | A `RuneStone` component beside `ExperienceStone` (same once-per-game id); a Rubbing Sheet item and recipe; placed in the Standing Stones set piece, Liv's Circle and The Living Path | Interact prompt "Read" or "Take a rubbing" | Each player's own stone state, sheet and stave |
| 6 Liv | `Data/Settlement/RuneScholar.asset` (workplace and "carves past your Runecraft by", per rank); `ResidentRecord.WorkDone`; a conversation choice "Give her a stave"; Tales with deed objectives; `Docs/RescueOrder.md` | People page shows her rank; the altar row says "Liv can carve this" | Her rank is settlement state, sent to visitors with the settlement (`HavenShare`) |
| 7 Great runes | New `RelicEffect` classes; `EnchantmentDefinition` gains a Great flag; `EssenceTests` bargain test widened; one asset per rune | One line per verb in the tooltip; a "Runes" line on the character window's Fight card | Effects on the wearer: none. Effects on creatures: #9 |
| 8 Readable | `UI/AltarWindow.cs`, `UI/ItemTooltipView.cs:740-762`, `Playtest/PlaytestLogger.cs`, raid report | No HUD | Each game logs its own player |
| 9 Co-op | `CoopCreatures` gains a status message like `StaggerMessage`; `PlayerLookState` sends the held item's enchantment (school, great rune id) | A friend's weapon glows | **Wire format 24 → 25** |

**#1, count and bound.**
- **`StackedKitOf` adds**, after food:
  - for each kit piece, the best fitting enchantment of the tier by `Worth`, skipping one already used;
  - relic powers' numbers where they are numbers: Moonbound in the dark, the relic's stated price.
- **Sustain effects stay out of the model,** as attack speed does: Wolf-Fed, Mending and Twice-Buried.
- **The once rule.** An enchantment's bonuses and effects count on the first piece that carries it. Later pieces show it greyed, with
  "counts once". The precedents are No Rest for the Wicked's gems and Grim Dawn's augments.
- **What the arithmetic says.**
  - With the once rule, the toughness enchantments add about ×1.34 / 1.29 / 1.21 / 1.17 to the plain kit (§1.4). Added to today's
    stacked kit, that is about ×1.55 at tiers I, II and IV: a little over the reach.
  - One Bloodletting puts damage near ×1.45 at tiers II to IV.
  - **So the test will fail, and that failure is the case for the trims:**
    - Mossgrown becomes a share of health (+8%), as the health meals became in the stats work;
    - Gravebound goes from +16 to +10 armour;
    - Bloodletting goes from +15% to +8%.
  - Read the multipliers off the test after each trim, gear first (the gear-tier rule).
- **The alternative is to widen the reach.** That is the user's call; it is not recommended, because party scaling assumes friends of
  similar power (`CreatureHost.cs:217`).
- **Reaping.** Counted once, it is +30% essence for -10 health, which is fine.

**#2, learned enchantments.**
- **A new game knows the 7 Purified enchantments.** The altar must do something on day one, and Purified is the safe school made at
  home.
- **The 7 Cursed enchantments become staves.** Cursed Essence comes only from raids, so the dangerous school is learned out there.
- **Old saves know all 14** (a rule like `SaveMapper.LegacyExperience`, `:653`), so no player loses an option. This gates what is
  free today, so it needs sign-off.
- **A stave is a Blueprint item with Learned At Home.** It can be lost on a failed raid, and the safe pocket is the choice. Reading
  it at home teaches the rune for good.
- **A stave you already know can be given to Liv (#6) or read for Runecraft experience.** It is never wasted.
- `RaidRumourDefinition.teaches` works for staves as soon as `PlayerCrafting.KnowsAll` knows about enchantments, so rumours can point
  to a stave.

**#3, drops.** Each source has a theme, so a drop says where it came from:

| Source | What it gives | How often (first estimate, tune from rows) |
|---|---|---|
| Great trolls (Old Moss Troll, Stone Troll, Bergjutul) and the Heartroot | A **great rune** from the boss's own list | The first kill at each tier always gives one you don't know. After that, 25% |
| Elites | **The rune of their own affix** (10 affixes, 10 runes) | 10% at tier I, then 20%, 25% and 30%, beside the relic chance. Elites per run are 1 / 1-2 / 2-3 / 3-4, so about 0.1 to 1 a run |
| Rune stones (#5) | Stat runes and Cursed staves of the stone's tier | 1 to 2 stones a run, once each per game |
| Supply caches, rumour events | Any of the tier's list | Rare (3%), like relics |

- **Unknown first:** a source never rolls a stave the player knows while it has unknown ones left. That is Monster Hunter's lesson.
  Duplicates appear only once a source's list is done, and then they go to Liv.
- **Pace aim:** about one new rune per raid through tiers II and III, slowing as lists complete; a tier's lists done in roughly
  8 to 10 raids at that tier. Diablo IV gives one aspect per dungeon's first clear, and Remnant's bosses always drop theirs. These
  numbers are my estimate: the `RuneLearned` rows (#8) should replace them.
- **What the player sees:** a stave pickup uses the relic's Legendary sound and "A stave: Vengeful!". The raid report names it. The
  planner can say "Unknown here: 2 elite runes, the troll's great rune", which is a reason to choose a raid (TODO › Reasons to raid).

**#4, Runecraft.**
- **Altar work trains Runecraft** at the item's tier experience × a work factor: about 3 for enchanting and 2 for empowering, like
  the crafting skills (`ExperienceTracker.PractiseCraft`, `:296-305`).
- **Each rune has a Required Runecraft.** Tier I runes need any level; tier II runes about 15; tier III about 25; great runes about
  30. That follows the expected levels 10/20/30/40 (`LevelingSettings.ExpectedLevel`).
- **Runecraft unlocks *which* runes you can carve, not bigger numbers,** so it adds no hidden power to the tiers. Skyrim's skill sets
  strength; here the numbers stay inside #1's reach.
- **A melee player raises Runecraft** at the altar, from stones (#5), from Liv's lessons, and with Insight (full below 20).
- Runecraft's spell bonuses and perks are untouched.

**#5, rune stones and rubbings** (the user's ideas 3 and 5 as one feature).
- **The stone.** A `RuneStone` sits beside `ExperienceStone` and remembers its use the same way, per scene and place (`:22-31`).
  - It lives in the Standing Stones set piece, Liv's Circle and The Living Path, and a raid may roll one more.
  - Its tier is the raid session's.
- **Reading it** gives Runecraft experience once a game, held until extraction like the raid bonus: "reading stones raises
  proficiency".
- **A rubbing.** With a **Rubbing Sheet** in the bag, the player can also take a rubbing.
  - It takes about 4 seconds, makes noise and can be broken off by a hit.
  - It gives that stone's stave: unknown first, from the tier's stone list.
  - The rubbing is Learned At Home, so it must get out.
- **The sheet:** 2 Nettle Fiber and 1 Charcoal, by hand. Charcoal is tier I, from the Woodcutter's Lodge. Weight 0.1, stack 5.
  - Carrying sheets is a packing choice, like torches.
  - It adds one item, not a currency (the design review warned against more currencies).
  - Linen could make a "fine sheet" for tier III and IV stones later.
- **Existing experience stones and the Rune Book keep giving Insight.** Nothing is removed.

**#6, Liv carves** (idea 4).
- **She carves past your Runecraft.** With Liv home, her rank lets her carve a rune whose Required Runecraft is above yours: 5, 10, 15
  or 20 levels past it, at Apprentice to Master.
  - This is the colony route: a player who never trains Runecraft still reaches great runes through her.
  - A player who does train it carves without waiting for her.
  - Data: the Rune Scholar job gains "carves past your Runecraft by" per rank, and the altar becomes a workplace of her job.
- **Her rank grows from what she already counts** (`ResidentRecord.WorkDone`):
  - every rune carved at the altar while she is home: +1;
  - a stave handed to her: +5. This is the user's "giving recipes to her", and the home for duplicates;
  - each of her Tales: +10 to +20.
- **Her Tales** (silver, Tales category) use deed objectives with assigned targets, never a Quest Event per stone:
  - "Read a rune stone": Use Object, with a "rune stone" Quest Target that every `RuneStone` answers to;
  - "Bring me a rubbing of tier II": Collect;
  - "Carve a Cursed rune": a new **Enchant Item** objective type, which needs its co-op rule (`QuestCoopTests`).
  - The rewards are staves she knows, and Runecraft lessons.
- **Rescue her earlier** (sign-off). She is 12th now. Her problem, "the deep woods need more than steel", is tier II's problem.
  Placing her after Gunnar (9th) brings her home about when elites start dropping staves. Without the move, everything still works:
  she arrives mid game and extends the altar.
- **Ingrid keeps purifying.** Enchanting still needs nobody, as today.

**#7, great runes** (the "completely change how it works" layer).
- **The machinery exists.**
  - `EnchantmentDefinition.effects` is a `SerializeReference` list of `RelicEffect` on every enchantment.
  - `PlayerRelics` already starts the effects of *any* worn enchantment.
  - A great rune is an enchantment with Great on, an effect, and a stated price where it is Cursed.
- **Where they fit:** main hand, off hand, neck and ring only; armour takes stat runes. So a kit wears at most four verbs, each once
  (#1). That is a Hades-sized handful.
- **The rules:**
  - Verbs before numbers.
  - Never attack speed: clips keep their cadence so damage per second holds.
  - Keep the blow order: plain, mid-strike backstab, counter, staggered backstab.
  - Every number is counted by #1.
- **Examples.** Each hooks a verb the game already has. These are first designs, for the user to pick from:

  | Rune | Source | Fits | What it does | Price |
  |---|---|---|---|---|
  | Ironhide | Ironhide elite (I) | Armour (stat) | +10 armour, the load +1 | |
  | Frenzied | Frenzied elite (I) | Weapon | After a perfect dodge, the next swing costs no stamina | |
  | Frostbound | Frostbound elite (II) | Weapon | Counters and backstabs Chill | (needs #9 in co-op) |
  | Regrowing | Regrowing elite (II) | Jewellery | Regain wins back twice as much | Health doesn't come back out of a fight |
  | Unyielding | Unyielding elite (II) | Off hand, jewellery | The first blow of each fight can't stagger you | |
  | Watchful | Watchful elite (II) | Jewellery | A blow from behind shows its ring 0.5 s early | |
  | Mistcloaked | Mistcloaked elite (III) | Jewellery | A perfect dodge leaves mist: creatures lose sight of you for 1.5 s, so an ambush is possible again | |
  | Relentless | Relentless elite (III) | Weapon | Each blow in a string on one creature wears its poise 20% more than the last | |
  | Vengeful | Vengeful elite (III) | Weapon | What you kill bursts after 1 s, 25% of its health, 3.5 m | |
  | Rallying | Rallying elite (IV) | Jewellery | Friends and companions within 9 m hit 10% harder | Nothing for you alone |
  | Rootbinder | Old Moss Troll | Weapon | A staggered creature is rooted for 1.5 s | |
  | Avalanche | Bergjutul | Jewellery | The heavy roll knocks small creatures down | Works only at a heavy load (13.5 and over) |
  | Heartwood | The Heartroot | Weapon | A kill with a Rune Art gives back its Focus | |
  | Blood-Price | Cursed stave (III-IV) | Weapon | Weapon and Rune Arts cost 8% health instead of Focus | Health, not Focus |
  | Split Fletch | Cursed stave (III) | Bow | Every third arrow splits in three at 40% | Draw 15% slower |
  | Stormcall | Stave (III) | Staff | A staff bolt leaps to a second creature at 50% | |

- **Later, for tools and other systems** (phase E):
  - a pickaxe that marks ore within 25 m when it strikes stone;
  - an axe that shakes a sapling loose;
  - a lantern that shows hidden caches.

  These change "how certain systems work" outside combat.
- **Relics stay** as the found, uncarvable powers. Their eight powers are never carvable.

**#8, readable.**
- **The Enchant page:**
  - known runes, as today;
  - fitting unknown runes, greyed, with "Elites of tier II (Unyielding)";
  - a count per source.
- **Tooltip:** "Great rune", "Counts once", and its source.
- **Character window (C):** one line on the Fight card listing the great runes worn.
- **Raid report and planner:** staves and relics by name (`TODO.md:662`).
- **Playtest rows:** `Enchanted` (item, rune, cost), `RuneLearned` (source, tier), `StaveFound` (source), `Rubbing`, `RuneGiven`
  (Liv). The `Kit` row gains the runes worn.

**#9, co-op.**
- **A status message** (creature id, status, from whom), handled by the host as `CreatureStagger` is. It fixes Rime-Touched for
  friends today, and makes Frostbound and Rootbinder work.
- **Look state** carries the held item's school and great rune: the glow for friends, and Rallying for the friend's own game.
- **Both change the wire format:** 24 to 25.

### 3.4 Phased plan

| Phase | Items | Balance | Done when |
|---|---|---|---|
| A. Bound and read | #1, #8, #4's training | Down, from today's worst case | `RaidTierTests` passes with enchantments counted after the trims; altar rows appear in a playtest |
| B. Learned from raids | #2, #3, about 6 new stat runes that use stats nobody gives yet (Blunt, Slash and Stab resistance; Draw Speed; Sneak Noise) | Within the reach | A new game learns a rune from an elite, carves it and sees it counted; an old save knows all 14 |
| C. Stones and Liv | #5, #6, #4's Required Runecraft | None | A rubbing taken in a probe raid and read at home; Liv carves past Runecraft at Journeyman; a Tale passes `QuestAuditTests` |
| D. Great runes | #7 (10 elite and 4 boss runes, 2 to 4 Cursed bargains), #9 | Yes, counted | Each rune has a test; the stacked test passes; a friend's Chill reaches the host's creature |
| E. Options | Tool and lantern runes; breaking a relic to learn it; art runes as staves | | After a played tier II and III run |

**Sign-off before building:**
- the once rule and the trims (#1);
- the 7 Cursed enchantments becoming staves (#2);
- the drop rates (#3);
- Liv's place in the rescue order (#6);
- each great rune (#7).

### 3.5 How to verify

- **Edit Mode tests** through `RunTests.ps1`:
  - `EssenceTests`:
    - a known-rune gate;
    - an old save knows all 14;
    - a stave read at home teaches, and one read in a raid waits;
    - two Mossgrown pieces add +20 once;
    - altar work trains Runecraft;
    - the bargain test widened for great runes (an effect instead of a positive bonus; Cursed still states a price).
  - `RaidTierTests.AFullyStackedKit_StaysWithinTheTiersReach` with enchantments and relic numbers counted.
  - `RaidReasonsTests`: a boss's first kill always gives a stave; unknown first; elite chance by tier.
  - `QuestCoopTests` for the Enchant Item objective; `QuestAuditTests` after Liv's Tales.
- **The Raids › Rules tab:** read the "fully stacked" column after each trim; change gear first.
- **Probes:** a launched raid, invulnerable, with the clock extended. Take a rubbing and kill an elite with a forced stave; time
  things with in-game coroutines (the raid-probe rules). Never `sh_save` in a probe.
- **Playtest:** `RuneLearned` and `StaveFound` rows over a tier I-II run, against the pace aim in #3. `Kit` rows show what was worn.
- **Co-op:** one two-player check that a friend's Frostbound chills the host's creature and that the friend's weapon glows.

### 3.6 Co-op notes

- **Staves are ordinary items.** They drop as personal loot, as raid loot does today, and a friend's drop carries its whole state. No
  new message.
- **Known runes are per character**, so a friend's knowledge never needs syncing.
- **Rune stones are per player:** each game keeps its own read flag, and each player needs their own sheet.
- **Liv's rank is settlement state.** It reaches visitors with the settlement the host already sends (`HavenShare`). A visitor carving
  at a friend's altar pays from their own pool, as today. Whether a visitor may use the host's Liv should go through
  `HavenDecisions`, as purifying does.
- **Party scaling** adds 60% creature health per friend (`MultiplayerSettings` › Creature Health Per Friend). The once rule and
  counting keep friends' power in the band that assumes.
- **Rallying gives co-op a support build:** one friend wears it, the others hit harder.
- **Wire format:** #9 bumps it from 24 to 25. Nothing else does.

### 3.7 Constraints honoured and overlaps

- **Data-driven.**
  - Runes are `EnchantmentDefinition` assets. Staves are `ItemDefinition`s, and the stone lists are data.
  - What the player knows lives in `CharacterProgress`; the enchantment stays runtime state on the `ItemStack`.
  - Definitions are never changed at runtime.
- **Never remove functionality.**
  - The 14 enchantments, the 8 relics, empowering and deepening stay, and their ids are kept.
  - Old saves keep everything, including all 14 known.
  - Experience stones and the Rune Book keep their Insight.
  - The once rule and the trims change numbers, not features, with sign-off.
  - The builders are retired, so assets are edited directly.
- **Deterministic altar** (GDD §13.5): randomness only in what drops, with unknown first.
- **No new bar or skill:** Runecraft and the worker rank already exist.
- **Small HUD:** everything new is in the altar, tooltips and the character window.
- **Gear-tier rule:** tiers are tuned to measured kits, and runes are counted. There is no hidden under-gear penalty.
- **Overlaps:**
  - `PlayerStatsResearch.md` #1, counting every source, which this extends.
  - #6 there, weight as the build stat: Ironhide and Avalanche use the load.
  - `RangedStealthResearch.md`: Split Fletch and Stormcall must respect the bow and staff fixes. Mistcloaked gives stealth the second
    chance that study found missing.
  - `EnemyCombatResearch.md` phase C, enemies learning repeats: the elite runes mirror their affixes.
  - `CombatResearch.md`: art runes stay as they are, and great runes stay off the swing cadence.

## 4. Unsure or unverified

- **All stacking figures are arithmetic** on `GearBalance`'s formula and `PlayerStatsResearch.md` §1.6's plain kits. The "stacked +
  enchantments" figures add toughness at plain-kit armour, a lower bound. The real `StackedKitOf` numbers will differ a little.
- **The essence figure** (about 45 Cursed Essence a raid) comes from two real runs at tier I and is not a measurement.
- **A friend's status on the host's creature:** I read the co-op message list and `RelicEffect`, not every path. A status may reach
  the host some way I didn't find.
- **Elites on a friend's side:** I assumed `EliteCreature` hears a puppet's death as `LootOnDeath` does. I didn't check that the
  puppet carries the elite component.
- **Experience stone counts** are prefab instances in two scenes; generated raids may place more through set pieces.
- **Playtest counts:** some long runs are agent sessions, so "real runs" are a judgement.
- **Drop rates, Runecraft levels, Liv's reach and the great runes' numbers** are first designs to be tuned, not measured.
- **Web sources:**
  - The Minecraft treasure-enchantment, Grim Dawn forum, No Rest for the Wicked, V Rising, Dragonwilds, Skyrim loop and PoE2
    socket-count facts come from community wikis and guides. PoE2 guides disagree on socket counts.
  - The Hades II duo complaint is player opinion.
  - Elden Ring's free re-applying of an Ash is described on the wikis but I didn't check it in a patch note.
