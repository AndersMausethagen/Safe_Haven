# People worth caring about (research, 2026-11-23)

The agent wrote this as read-only research. Nothing in it is built yet. It covers item 8 of the deep-dive list ("people worth caring about") and answers the user's note of 2026-11-23: "how we can deepen npc talents, gear etc. look at how Rimworld handles npc with skills, traits, passion. I think we could take a lot of inspiration from that game to make npcs feel unique and make the player care about them".

**In short.** Our residents are written people with good lines, but under the hood every one of them is the same machine: one job, one rank earned by how much they make, one raid bond and one wish. The plan below gives each person:

- **Talents:** the player's own 13 skills, with a passion for some of them.
- **Two or three traits** that change what they do, not just a percentage.
- **Spirits** that pay out in inspired days.
- **Friendships and rivalries** that matter in raids and forays.
- **Their own gear** that keeps a history.
- **Marks** that raids leave on them.

All of it is visible on one card. Six questions for the user are in §6.

---

## 1. What our people have now

| What | Where | What it does |
|---|---|---|
| 11 written people | `ResidentDefinition` (`Data/Settlement`, `Data/Prologue/Residents`) | Ingrid, Bjarne, Tor, Eirik, Yrsa, Gunnar, Liv, Asa, Runa, Hild, Sigrid. Each has a description, portrait, rescue, arrival, welcome, fallen and fight lines, memory lines, a fixed job list and one fixed way of fighting (`CompanionDefinition`). |
| Work rank | `ResidentRecord.WorkDone` → `SettlementSettings.WorkerRanks` | Novice, Apprentice, Journeyman, Expert, Master at 0, 10, 30, 70 and 150 crafts. Speed ×1.0 to ×1.4, extra-piece chance 0 to 12%. The same ladder for everyone, in one track only. |
| Bond with the player | `CompanionBond`, `SettlementSettings.BondRanks` | Trusted (3 raids along: +15% health, +10% damage), Sworn (8 raids: +30% / +20%). Memories of raids are spoken at home. Lost in the forest, they come back as a stranger at bond 0, and their memories return every 2 raids. |
| Strength | `HaulDone`, `WorkerSettings` | Carrying grows with weight hauled and raids gone along on. |
| Gear | `ResidentRecord.WorkGear` (`ItemDefinition` › Worker Gear) | One item for carrying more or working faster. A companion's weapon comes from their `CompanionDefinition`, not from anything the player gives them. |
| Home and wishes | `HomeLifeSettings` › Likings, `HomeWishes` | One liking each ("a lit way home"). Met: thanks, a gift and +5% work. Every effect is positive (the user's rule, 2026-11-15). |
| Meals | `ResidentMeal` | Well fed +10%, hungry −25%. Nobody starves. |
| Small scenes | `SmallScenes`, `SmallSceneSettings` | 9 written pairs laugh, sing or tell stories. Everyone near is cheered for a while. |
| Forays | `Forays.PowerOf` | Power = base + work rank + raids along + gear tier + breakfast. A failed foray leaves them bruised until dawn. |
| Missing | the GDD §15.4 asks for these | Traits, passions, any skill besides the job, relationships between residents, wounds that last, personal combat gear, a mood the player can read. |

The design document (§15.4) already asks for "one or two personality traits, basic needs… well-being can influence work productivity or available side stories, but avoid turning residents into a constant micromanagement chore." The ARCHITECTURE status line still says "No needs or relationships".

**What this means.** The written lines make people distinct when you talk to them. The systems make them identical when you use them. Bjarne and Tor differ only in which building they speed up. Choosing who goes on a raid, who goes on a foray and who stays to work is a choice between numbers that rise the same way for everyone. RimWorld is the opposite: a pawn's talents, flaws and moods decide what they are good for, so every assignment is a choice about a person.

---

## 2. What RimWorld does, and what carries over

The numbers come from the RimWorld wiki (1.5/1.6 unless a DLC is named). Sources are in the agent's report. This section keeps the parts that matter here.

| RimWorld | The numbers | Why it works | For us |
|---|---|---|---|
| **12 skills, 0-20**, named levels ("Weak professional" 7, "Master" 14, "Legendary master" 20) | Work speed and quality scale with level. Crafting skill decides quality, not speed. | One look tells you who is good at what, and the level names do the characterisation for free. | Yes. Use **our** 13 skills, so a resident's sheet reads like the player's. |
| **Passion** none / minor / burning | Learning ×0.35 / ×1.0 / ×1.5. +8 / +14 mood while doing that work. Inspirations weighted ×1 / ×2.5 / ×5. Adults never change their passions. | A passion is three things at once: they learn faster, they are happier there, and their best moments happen there. | Yes, the strongest single idea. Keep the "happier" part positive only (see §3.2). |
| **Daily cap** (×0.2 beyond 4,000 XP/day), **decay** above level 10 | | Stops everyone becoming good at everything. | The cap yes, so a few people can't do everything. Decay no: it is a chore. |
| **"Incapable of"** (pacifist, won't do dumb labour) | Backstories and traits forbid whole kinds of work. | Gaps are as memorable as strengths. | Sparingly, as a trait with an upside ("Gentle: won't fight; heals the whole party"). |
| **1-3 traits** | Some are spectrums (Industrious +35% … Slothful −35%). The memorable ones change behaviour: Pyromaniac, Night Owl, Jealous, Kind, Bloodlust. | Players tell stories about behaviour, not percentages. Tynan Sylvester wanted "simplified characterization that tries to impact gameplay as much as possible". | Yes: 2-3 per person, each with a tell and a number. |
| **Backstories** | Childhood + adulthood. Skill offsets −5 to +9. Some forbid work. | They explain the numbers ("ex-noble, won't haul"). | Our 11 already have backstories in their descriptions and quests. Their skills and traits should follow from them. |
| **Mood** = base + thoughts | Thoughts are timed memories ("ate a fine meal" +5 for 1 day, "my friend died" −10 for 20 days). Expectations rise with wealth. Breaks at 35% / 20% / 5%. | Mood carries all the other systems, and grief lasts. | Lighter: three readable spirits, not a meter of thoughts. No mental breaks (§3.3). |
| **Inspirations** | Need mood ≥ 50%. About every 10 days at full mood. Inspired Creativity: +2 quality levels. Work Frenzy ×1.8 for 8 days. | Good management pays out in a person's own moment of brilliance, shaped by their passion. | Yes, and it fits our "positive only" rule. |
| **Opinion** −100..100 | Chitchat +0.66 (stacks to +10), deep talk +15, insult −15, kind words +15. Social fights end 50/50 in "cathartic" +38 or "angering" −22. Family and lovers. | Relationships make loss and friction personal. | Friendships and rivalries yes, on a small scale. Romance later or never (§6). |
| **Quality tiers** awful → legendary | Protection ×0.6 → ×1.8, melee damage ×0.8 → ×1.65. A legendary item made in an inspiration gets a name. **Tainted** clothes (from the dead): −5 to −14 mood. | Gear has a maker and a story. | Our Fine/Masterwork + `ItemStack.Maker` already sign the player's work. Extend that to residents' work and to companions' own gear. |
| **Scars, lost parts, bionics** | A scar permanently lowers the part's health. Disfigured: −15 opinion. Bionics up to 125-150%. | The body is the record of what happened. | As raid **marks**, healable at a price (Darkest Dungeon's middle ground). |
| **Tales and letters** | Every notification names the person. Art shows the colony's own history. | "Doc is going berserk", never "a colonist". | Yes: named lines everywhere, and a Haven chronicle. |

**Darkest Dungeon** fits our raids best. Heroes come back from missions with quirks earned by what happened: good ones about 50% of the time, bad ones more often under stress, and a quarter of bad ones lock in. Removing one costs 1,500 to 5,000 gold. Spending scarce money on *who* to fix is what makes players care.

**XCOM 2 War of the Chosen** gives the model for pairs: bonds grow from missions together and pay in combat (a free action for the partner). Rescuing a downed squadmate raises compatibility.

**Mount & Blade** gives each companion one written friend and two written dislikes.

**Wildermyth and Fire Emblem** unlock written scenes when systems reach a level. This fits our 11 written people better than RimWorld's random ones.

**Going Medieval's 2026 rework** reached the same conclusion we would. Perks should be "less like silent stat modifiers and more like parts of a settler's personality". Rebellions only when basic needs are ignored. Personality should touch the people nearby.

**The lessons that matter most here** (§9 of the agent's report):

1. **A few loud facts beat many quiet numbers.** Each person needs 2-3 things the player can say from memory.
2. **Every flaw needs an upside or a story.** Otherwise it is a bad roll and players reload.
3. **Nothing hidden.** A rule the player can't see is "lost in the silicon" (Sylvester).
4. **Keep the cast knowable.** Sylvester limits the cast on purpose. We have 11, which is ideal.
5. **Make the cost of fixing a mark real but fair.**

---

## 3. Recommendation

Six parts. Each stands on its own, and they are listed in build order. Everything is designer data (ScriptableObjects and settings), as the rest of the people systems are.

### 3.1 Talents: the player's 13 skills, for residents

- **Each resident gets a level in all 13 skills** (Woodcutting, Mining, Foraging, Fishing, Weaponcrafting, Armorcrafting, Cooking, Alchemy, Construction, Melee, Archery, Runecraft, Sneaking), on the player's scale. Baseline 10 is "ordinary". Bands of levels get names (Novice … Master, below; `LevelingSettings` names only the total level today), so a resident is "Bjarne, Weaponcrafting 31, Expert".
  - The player can then compare themselves with a resident. "Bjarne is still the better smith" is a reason to give him the work, and a lesson from him means something.
- **Starting levels come from who they are.** The job's skill starts at 18-26: they were professionals before the forest. One or two other skills sit at 12-16, from their backstory. The rest start at 3-9.
  - Examples: Gunnar has Archery 22, Sneaking 16 and Cooking 4. Hild has Construction 24, Mining 15 and Melee 12. Yrsa has Cooking 26, Foraging 13 and Melee 3.
  - The 11 are written by hand in each `ResidentDefinition` (a new **Talents** list). Later strangers roll from ranges.
- **They learn by doing**, through the same practice the player's skills use (`ExperienceTracker`'s numbers, a resident copy):
  - Crafts at their station train the station's skill.
  - Hauling trains nothing (Strength stays as it is).
  - Raids along train Melee or Archery by how they fought.
  - Felling rings train Woodcutting, quarrying trains Mining, fields train Foraging.
  - Forays train the skill of what they gathered.
- **A soft daily cap.** Beyond a set amount of practice in one skill in one Haven day, the rest counts at ×0.2. This keeps the cast distinct.
- **What a level does.** It replaces the work rank's flat ladder:
  - Station speed = 1 + 0.015 × (level − 10). Level 10 gives ×1.0, 30 gives ×1.3 and 40 gives ×1.45, close to the old Master ×1.4.
  - Extra-piece chance = 0.4% a level above 10, so 12% at 40.
  - Residents' crafts roll **quality** the way the player's do (Fine/Masterwork by level), signed with their name.
  - Companions in raids:
    - Melee or Archery adds to their weapon's damage (+1.5% a level over 10).
    - Their Dodge Chance rises 0.5% a level, kept below the player's.
    - Sneaking lowers how far enemies notice them.
  - Forays: a party's power reads the destination's skills (Woodcutting for a timber foray, Mining for ore) instead of the work rank.
- **The work rank names stay** (never remove a thing): Novice … Master become the titles of the job skill's bands. Old saves are converted once: `WorkDone` 0, 10, 30, 70 and 150 become the job skill at 10, 15, 20, 28 and 36, or their written start if that is higher.
- **Lessons get better.** `JobDefinition.Teaches` already offers a lesson between raids. Its size now follows the resident's level in that skill, so a Master smith's lesson is worth seeking out.

### 3.2 Passions: where they light up

- Every resident has **passions** in 1-3 skills: **Keen** (one flame) or **Burning** (two flames). The job skill is usually one of them, but not always (§3.2 example below).
- **Learning:** ×0.5 with no passion, ×1.0 Keen, ×1.5 Burning. RimWorld's ×0.35 is harsh for a cast of 11, so it is softened here.
- **Joy at the work:** working a passion skill gives a small, short **cheer** (the cheer `SmallScenes` already gives: a little quicker at work). It is a bonus only, never a penalty for other work. This keeps the user's "every effect positive" rule.
- **Inspirations** (§3.4) are weighted ×1 / ×2.5 / ×5 by passion, as in RimWorld, so a person's best day happens where their heart is.
- **The interesting case is a person whose passion isn't their job.** Tor is a Carpenter with a Burning passion for Fishing. Runa is a Tracker with a Keen passion for Cooking. That opens a choice: keep them where they are good, or move them where they are happy and grow fast.
  - This is the RimWorld choice. It needs a second job to be open to them (`ResidentDefinition.Jobs` already allows several), or a role that uses the skill (a fishing party, a field).
- **Passions can grow.** At bond rank Trusted and again at Sworn, the player chooses one of two offered passions for that person: a new Keen, or Keen to Burning. This is RimWorld's growth moment (children's choices) and Wildermyth's, and it gives the player a hand in who they become.

### 3.3 Traits: two or three loud facts

- **Each person has 2-3 traits.** Each trait is a `ResidentTraitDefinition` asset with:
  - a **tell** the player can see: what the person does or says that shows it, an icon and a bark;
  - a **number**;
  - for a flaw, an **upside**.
- No trait takes control away (no mental breaks: GDD §15.4, and the user's home rules). The most a flaw does is cost a little or change a habit.
- **Kinds of trait,** with examples written for our people. Their descriptions already say most of this.

| Trait | Tell | Number | Fits |
|---|---|---|---|
| **Night Eyes** | Works into the evening and walks home last; sees marks after dark | Works 2 h later; +1 rumour only seen at night; in raids, spots enemies at dusk 30% further | Runa (found by marks seen only after dark) |
| **Proud Cook** | Won't serve a plain meal if a better one can be made; tells you of the jarl | Her dishes are one tier better for Well Fed; −10% speed on tier 1 food | Yrsa |
| **Opinionated** | Comments on what you build ("Your footings…"); has a favourite and a hated building kind | Buildings she finishes get +10% health; a building she dislikes near her home annoys her (no cheer from home) | Hild |
| **Loner** | Eats apart, walks the edge of the Haven | +25% foray power alone or as a pair, −10% in a bigger party; no cheer from scenes, but never bothered by rivals | Gunnar |
| **Secretive** | Some topics only open at a bond rank; avoids the portal | Her quest lines open by bond; her alchemy has +1 quality step at Sworn | Ingrid |
| **Steadfast** | Never retreats unless ordered; stands at the ward | +20% health while defending; ignores the Retreat order once below 30% health, holds instead | Eirik |
| **Born Leader** | Others cheer when she returns; she gives orders in scenes | Everyone in a party or foray she leads +10%; raid time as now | Sigrid |
| **Bookish** | Reads by the stones in free time | Learns Runecraft and Alchemy ×1.5 regardless of passion; −10% carry | Liv |
| **Patient Hands** | Slow, careful work | −10% speed, +1 quality step chance on everything; never fumbles a lesson | Tor |
| **Hammer-Gruff** | Grumbles at praise, warms to gifts | Gifts count double toward his bond; scenes with him start slower | Bjarne |
| **Seed-Keeper** | Saves seeds from every harvest, mourns her village | +1 seed from each harvest; on the anniversary of her village (a set Haven day) she stays home | Asa |

- **General traits for strangers and later people.** These are rolled two at a time, one good and one double-edged:
  - Early Riser / Night Owl
  - Tireless (+1 h work) / Slow Starter
  - Light Sleeper (wakes at the first prowler; good guard)
  - Iron Gut (eats raw food without harm)
  - Glutton (eats double; a feast cheers them twice)
  - Superstitious (won't walk the mist at night; senses Mist-touched sooner)
  - Brave / Wary (charges first / stays back, dodges more)
  - Sharp-Tongued (scenes may sour, but ends rivalries in one big scene)
  - Kind (eases others' bad days)
  - Hoarder (keeps a few odds; finds a gift for the player now and then)
  - Gentle (won't fight; heals a party or ward-side)
- **Exclusions,** as in RimWorld: Kind excludes Sharp-Tongued, and Brave excludes Wary.

### 3.4 Spirits and inspired days

**Spirits are the lightweight mood.** Three readable states (**Low**, **Steady**, **Bright**) on the card and the Keeper's view tag, worked out from what already exists plus a few new inputs. There is no list of −3s to manage. The card shows only the top two reasons as pictures (the user's no-text-walls rule).

**What raises them:**
- a good home and its wish met (exist);
- well fed (exists);
- a small scene (exists);
- working a passion skill (new);
- a friend nearby or come home (new);
- a raid along that went well, or a boss beaten (exist as memories);
- an inspired day just had (new).

**What lowers them:**
- hungry (exists);
- bruised (exists);
- a friend lost to the forest (new: about 5 Haven days, as RimWorld's "my friend died" 20 days);
- a rival living next door (new);
- a mark from a raid not yet healed (new, §3.6).

**What spirits do:**
- **Bright:** a chance each morning of an **inspired day**. RimWorld's inspirations, adjusted for us and chosen by passion:
  - **Inspired craft:** the next piece at their station is one quality step higher and gets a name and line ("made the morning after Tor came home").
  - **Work fever:** ×1.5 speed for the day.
  - **A good find:** a foray or field turns up something rare.
  - **A story told:** a scene that cheers the whole Haven.
  - **Fighting heart:** along on the next raid, +20% damage.
  - **A lesson to give:** a second lesson today.
  - Roughly one inspired day per person every 10-15 Haven days at Bright, none below.
- **Steady:** as now.
- **Low:** −10% work speed and no inspired days. A visible habit: they sit at the fire, and say why when spoken to. They never refuse work, never break things and never leave.
  - A user call (§6 Q3): whether Low may cost more. RimWorld's breaks are its most criticised feature, and Going Medieval cut theirs back in 2026.

### 3.5 Friends and rivals

- **Every pair of residents has a standing** from −2 to +2: Rival, Cool, Neutral, Friendly, Friend. That is RimWorld's opinion with the noise taken out.
  - Each of the 11 starts with **one written friend and one written friction** (Mount & Blade). Examples: Hild and Tor clash over footings, Yrsa and Ingrid share a cauldron badly, Gunnar and Runa are trackers who respect each other, Sigrid and Eirik go back to the expedition.
- **Standings move through what systems already do:**
  - **Small scenes** between Friendly pairs raise them.
  - **Working side by side** and **forays together** raise them slowly (XCOM bonds: missions together).
  - **Helping up** a friend in a breach raises it a lot (XCOM: rescuing a downed squadmate).
  - **A rivalry scene** (new, written per pair): the pair argue at the fire, and like RimWorld's social fight it ends 50/50. Either they laugh it off and the standing rises by 2 (cathartic), or it hardens by 1 (angering). Never blows, never injury.
- **What standings do:**
  - Friends at work within reach: +5% speed each.
  - Friends on a foray: +10% power.
  - A friend lost to the forest: spirits Low for a while, and they ask the player to bring them home. This makes the lost-and-found rule personal.
  - Friends at Friend (+2) unlock a **written scene** (Fire Emblem supports), one per pair, as `SmallScenes` content.
  - Rivals at −2 with homes side by side: no cheer from home. The player can move one.
- **The player's bond stays as it is** (Trusted, Sworn) and gains **written conversations** at each rank, as Ingrid's quest line does. It is also the growth moment for passions (§3.2).

### 3.6 Their own gear, and the marks raids leave

**Companions get real gear.** In conversation, give a resident a **weapon** and a **body piece** from your stash, under the one worker-gear item they have today.

- The weapon replaces the companion definition's Weapon Damage with the item's. Its looks show in their hand (`HeldItemView` already does this).
- The body piece adds health from its armour value.
- The kit wears like the player's and is repaired at the Smithy. This is how the player's crafting feeds their people.
- The companion definition stays: how they fight, which orders they take, the Archer clips.
- A companion can only use gear in skills they have: Melee for blades and maces, Archery for bows. Their level shows how well.
- A **piece carried long becomes theirs.** After 8 raids along, the item gets their name ("Gunnar's Yew Bow") and a line of where it has been. If they are lost to the forest, the item stays in their empty house until they are found. Wearing a lost friend's gear makes their friends sad (RimWorld's tainted clothes, kept gentle).
- **Resident-made gear is signed** ("made by Bjarne, Masterwork"), as the player's already is.

**Marks: what raids leave on a person** (Darkest Dungeon's quirks and RimWorld's scars, with no permanent death). Coming home from a raid, each companion may gain a mark from what happened (`CompanionBond.Remember` already knows the biggest thing):

- **Good marks**, about 40% after a raid that went well:
  - Troll-wary: +15% damage to trolls, after felling 3 along.
  - Mist-walker: Mist-touched lighter on them.
  - Steady Hands: from a raid with no one downed.
- **Hard marks** come from being downed, lost or Mist-touched long:
  - Old Wound: −10% health.
  - Nightmares: spirits one step lower after a breach.
  - Afraid of Widows: −10% damage against spiders. Flips to Spider-bane after 5 widows felled along.
- **Each hard mark has a way back:**
  - Ingrid heals wounds with a tonic.
  - A Friend's scene eases nightmares.
  - A fear can turn into its opposite through deeds.
- At most 3 good marks and 3 hard marks per person. A new one replaces the oldest.
- **Being lost to the forest leaves one hard mark** that can't be healed until all their memories are back. That ties into the existing lost-and-found rule. Their talents and traits stay when they come back: they are still who they were, they just don't know the player.

---

## 4. Showing it (no text walls, small HUD)

- **The person's card** (`ResidentWindow`), Hearthline 3 picture cards in four rows:
  - traits as icons with names;
  - their top 4 skills as level numbers with flame icons, and the other 9 behind a "more" hover;
  - spirits as one icon with its top two reasons as pictures;
  - friends and rivals as portraits with a +/− rim.
- **The Keeper's view tag** gets the spirits icon and a flame when they are at passion work.
- **Barks** come from their traits ("Your footings again…").
- **Named messages everywhere**: "Bjarne had an inspired morning: a Masterwork axe", never "a resident".
- **A short Haven chronicle** on the Journal page: one line a day about the people, read only when wanted.
- **Assigning work shows talent.** The job picker and the foray and raid-companion pickers sort people by the skill that matters and show their flames, like RimWorld's Work tab. This is where talents become choices.

---

## 5. Order, cost and risks

| Step | What | Built on | Size |
|---|---|---|---|
| 1 | Talents + passions (§3.1-3.2), save conversion, job, foray and companion readers | `ExperienceTracker`, `Forays`, `SettlementState.WorkerSpeed`, `CompanionDefinition` | Large. It touches every place that reads the work rank. |
| 2 | Traits (§3.3): asset, 11 written sets, tells and barks, the trait hooks | `HomeLifeSettings`, `ResidentLife`, `Forays`, `Companion` | Medium per trait. Start with the 11 people's own. |
| 3 | Spirits + inspired days (§3.4) | `ResidentMeal`, `HomeWishes`, `SmallScenes` cheer | Medium |
| 4 | Friends and rivals (§3.5), written friendship scenes | `SmallScenes` | Medium, plus writing and voice lines |
| 5 | Companion gear + named pieces, marks (§3.6) | `WorkGear`, `HeldItemView`, `CompanionBond.Remember`, durability | Large |

**Risks.**
- **Micromanagement** (GDD §15.4). Kept down by three spirit states, no breaks, and reasons shown as pictures.
- **Co-op.** The host keeps the residents. Guests read the host's copy, and every new record field goes through `ResidentRecord.Change`, as the bug guards require.
- **Saves.** A new format number and a conversion of `WorkDone` into the job skill.
- **Balance.** Station speed at level 40 must stay near the old Master ×1.4, and companion damage must stay below the player's (the "player stays the better fighter" rule in `CompanionDefinition`).
- **Voice.** New barks and scenes need IndexTTS lines, unheard by the user until a playtest.

**Fits the deep-dive item's other points.**
- *Forays and companions compete for the same residents.* With talents that competition becomes the interesting choice: the best archer goes on the raid, and the best woodcutter on the timber foray.
- *The companion death rule awaiting playtest* stays as it is. Marks (§3.6) give it a cost that is a story, not a reset.

---

## 6. Questions for the user

1. **Talents scale.** Should residents use the player's own 13 skills and level scale (recommended), or a simpler 1-5 stars per job?
2. **Flaws.** Traits with real downsides, each with an upside (recommended)? Or keep "every effect positive" for people as for homes?
3. **Low spirits.** Recommended: −10% work and no inspired days, nothing more. Should Low ever cost more (refusing a raid, leaving for a while)?
4. **Fixed or rolled.** The 11 keep written talents and traits, with player choices at bond ranks (recommended). Should later strangers and wanderers be rolled RimWorld-style, which would bring more recruits than the 11?
5. **Companion gear.** Should you equip companions from your stash, with the items named after long use (recommended)?
6. **Romance.** Friendships and rivalries only (recommended for now), or relationships and families as RimWorld has?
