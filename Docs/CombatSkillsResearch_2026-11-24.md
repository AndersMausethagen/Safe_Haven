# Combat skills like V Rising: what we have, what it takes, how to build it (2026-11-24)

The user: "i want combat and weapon skills like v rising has. with animations and vfx that makes them stand out. we have a very Basic
version of skills now. but can we make something like v rising in this game? how could we integrate skills in the story and player
progression? can we use New animations and cool effects? or tune existing animations so we can at least start with something?"

**Short answer:** yes. The hard part of V Rising-style combat is already here: readable swings, chains, parries, weapon arts, art runes,
ground telegraphs and a Focus economy. What is thin are the **abilities**:
- They have **no effects of their own** (PlayerSkills spawns nothing).
- They **borrow** a handful of clips.
- They do one thing each: lunge, hit, heal or buff.
- **Six of the eight are known from the start**, so finding them carries no story.

The plan below keeps the weapon play and builds three layers around it: **weapon skills, powers and an ultimate**. Then come a shared
effects kit and a story spine for unlocking them. It starts with existing clips tuned up, and adds new animation where it pays most.

---

## 1. What V Rising does (the parts worth taking)

- **The weapon is your kit.**
  - Each weapon type has a basic attack plus its own skills on fixed keys.
  - Higher weapon tiers unlock more of them: per the wiki, the second skill at copper and the third at iron.
  - Changing weapon changes how you fight, not just numbers.
- **Spells come from schools, and each school owns a status.**
  - The schools are Blood, Chaos, Frost, Illusion, Storm and Unholy, and later more.
  - Each owns a status that the others can feed or consume: Chaos ignites, Frost chills then freezes, Storm builds static then stuns,
    Blood leeches, Illusion weakens, Unholy condemns.
  - A loadout is two basic spells, a travel spell (a school-flavoured dash) and an ultimate. Mixing schools is where builds come from.
- **Unlocks are the story.**
  - Every named boss (a "V Blood carrier") grants abilities or technology when defeated. Since 1.0, each one also gives a spell point of
    its school.
  - You hunt a boss because of what it will teach you.
  - Schools have passives bought later, and the Altar of Recollection resets a school for free.
- **Modifiers.**
  - Spell jewels add random modifiers to one spell. Sources disagree on the tier counts.
  - Jewels are found or crafted and can be fused.
- **Readability.**
  - The camera is tilted top-down.
  - Every skill has a short, telegraphed cast, a distinct shape and a school colour.
  - Ground markers show where an area attack will land. Projectiles are big and bright.
  - Cooldowns are short (about 8-12 s for basic spells), so the skills are the fight, not an occasional extra.

Sources are listed at the end. The details of V Rising's numbers vary between guides and patches; the structure above is consistent.

## 2. What Safe Haven has now

| Piece | State |
|---|---|
| Weapon play | Strong. Chains per family (10 families), heavies, charged attacks, guard counters, parries, perfect dodges, backstabs, finishers, poise, ground telegraphs, blade ribbons, hit sparks, hit-stop, sounds per weapon (Docs/Combat.md) |
| Weapon arts | Every family has one, paid in Focus (sword dash, dagger blink, thrown axe/spear, leap slams, whirlwind). **This is V Rising's weapon skill already, on one button** |
| Art runes | Eight runes give a family a second art (spin, low cut, vault, flying knee...). **The second weapon skill, already** |
| Abilities (skill bar 1-5) | 8 `SkillDefinition`s in 5 schools (Steel, Beast, Frost, Ember, Wyrd). A cast does lunge → hit → heal → buffs, with an animator state by name. **No effects, no projectiles, no zones, no statuses, no cast phases** |
| Unlocks | 6 known from the start (PlayerSkillSettings). Heartroot's Grasp from the Heartroot boss, Mountain Leap from Brukam. Rune books teach. The altar deepens ranks with Purified Essence. Runecraft (a proficiency) scales spell power |
| Statuses | Chill, Poison, Roots, Webbed, Hamstrung, Heartrot, Grit, Enthralled, Eruption Roots; burning from fire and coatings. A status framework exists |
| Animations | All on one body (the player is Kevin Iglesias' human). About 25 usable cast and special clips are unused or barely used. Stylized: Spell Staff/Unarmed Up/Forward Channel and Impact, Cast SpellA Start/Loop, Cast Attack 01/02, Defend Spell, Emote Roar/Shout, Combat Whirlwind, 1H/2H/2HL/Bow Attack Special. Quaternius: Spell_Simple Enter/Shoot/Exit, Sword_Dash, Shield_Dash, OverhandThrow, NinjaJump. Kevin Iglesias: kicks, jumps, ground hammering. AttackFlow/blend tooling and upper-body layers already exist |
| Effects | Shuriken particle prefabs (29: sparks, bursts, flames, dust), Effect Particle shader (additive/alpha, soft), URP decals and a decal pool, ground telegraph material, ground impacts (craters, dust, stumble), ribbons, camera shake. **No VFX Graph** (not needed) |

## 3. The design: three layers around the weapon

### 3.1 Weapon skills (Q and E): what you hold decides them
- **Q is the family's art, E is its second art.** Both already exist: today's art and its art rune. Making them two fixed keys is mostly
  input and UI work.
- **E unlocks with the weapon, not a rune.** A copper-tier weapon of the family, or the family's proficiency at level 10, as V Rising
  does by tier. The art runes then become E variants: a rune swaps which E you have.
- **Each gets a cooldown and keeps a Focus cost,** so the Focus economy (earned by fighting well) still matters. Q costs less and E is
  stronger.
  - *This is a balance change and needs playtests.* The first pass copies today's art costs.

### 3.2 Powers (R and F): two from any school
- **Each school owns one status.** Mixing schools is where builds come from, as in V Rising. Built on the existing status framework:

  | School | Status it owns | Feeds/consumes |
  |---|---|---|
  | Frost (Isa runes) | Chill → Frozen at 3 stacks (exists: Chill) | Shatter: a heavy on a frozen foe bursts it |
  | Ember (the Ash Lad's fire) | Burning (exists via fire/coatings) | Burning foes take more from Steel |
  | Beast (wolf, bear, raven) | Rend (bleed) or Marked (hunted) | Pack: companions focus the marked |
  | Wyrd (the hidden folk) | Hexed (weaken: deals less, takes more) | Luck: hexed foes drop more |
  | Steel (the hird's craft) | Sundered (armour broken) | Executions on sundered foes |
  | *New: Thunder (Thor's)* | Charged → stun at a threshold | Arcs between charged foes |
  | *New: Grave (the draugr)* | Grave-chill (no healing) | Raise a fallen foe briefly |

- **Two power slots (R, F):** short cooldowns (8-14 s) and moderate Focus.
- **A "travel power" (optional, later):** school-flavoured dodges that leave an effect behind: a Frost roll leaves an ice patch, Ember a
  trail of embers, Beast a pounce. It works on the existing dodge (Space), so no new key.

### 3.3 The ultimate (T)
- One per loadout, on a long cooldown (60-90 s) or a full Focus bar, with a short cinematic beat: camera push, hit-stop, a big ground
  mark.
- Trollsbane Wrath is the model. Each boss's signature move becomes the ultimate it teaches.

### 3.4 Modifiers: deterministic, as the altar already is
- V Rising's jewels are random. This project's rule is "every offer says what it does, never fails or wastes" (Docs/EnchantingResearch.md).
- **Deepening ranks become choices:** at rank II and III the altar offers two variants of the power and you pick one.
  - Frost Rune: a wider lance, or a lance that freezes at once.
  - Ember: a longer cone, or burning that spreads.
- **Inscriptions extend to powers** (the "once rule" and budget kept): carved onto a power, not onto gear.

### 3.5 Keys
- **Q/E** weapon skills, **R/F** powers, **T** ultimate, all on the skill bar's left. Number keys stay for consumables.
- **Controller:** shoulder plus face buttons. This must be checked against the existing pad layout (Docs/ControllerSupport,
  `InputActionsTests`).
- Rebinding and `{key:}` tokens keep working.

## 4. Story and progression: every power has a teacher

The Campaign budget's first rule is "every system enters through a story beat or a person" (Docs/CampaignAudit_2026-11-23.md). Powers
fit it naturally:

- **"What the foe knew"** (V Blood, in this game's voice). Each boss and named foe teaches its power when laid low. Two already do:
  - Brukam teaches Mountain Leap.
  - The Heartroot teaches Heartroot's Grasp.

  Every boss gets one: Act II's Jaw-keeper, the arena bosses. So does each **Lost One** when laid to rest: the nemesis carries a power
  it took from you, or one of its own, a reason to hunt it.
- **A teacher for each school at the Haven.** A rescued resident is a school's mentor:
  - Liv, already the rune-carver, for Frost.
  - The smith for Steel.
  - The hunter Gunnar for Beast.

  Their workplace building gains a "Lore" upgrade that teaches the school's passive (V Rising's school passives), so building the Haven
  grows the kit. It is gated through `BuildingGate`, like every building.
- **The first power arrives through a quest,** not from the start.
  - Start with the weapon's Q only.
  - The Heartroot (end of Act I's first arc) teaches the first power.
  - The second slot opens with the first mentor.
  - The ultimate comes with the second boss.

  Today's six starting abilities move into this order. **Nothing is removed:** each gets a teacher.
- **Proficiencies stay what they are.**
  - Runecraft scales powers.
  - Melee and the weapon families scale weapon skills.
  - Milestone perks can add skill variants: "Swords 20: your E leaves a bleeding cut".
- **Gear score and raid tiers** count a power's rank like a gear piece, so the tier model (Docs/GearScoreAndRaidTiers.md) sees it.

## 5. Animation: start with what we have, then add

1. **Now: tune the clips we own (days).**
   - Give each skill a timeline: wind-up → release → recovery.
   - Play each phase at its own speed, as swings already do (a readable build-up, a fast strike).
   - Casts go on the upper body over walking legs where the skill allows moving.
   - Exaggerate by time: a longer anticipation, hit-stop at release, a camera punch.
   - About 25 unused clips cover:
     - casts forward and up (lance, cone, blessing);
     - a channel loop (a beam or storm);
     - roar and shout (war cries, buffs);
     - whirlwind;
     - the four Attack Specials (leap slams);
     - Sword_Dash and Shield_Dash (charges);
     - OverhandThrow (thrown spears, axes, boulders);
     - NinjaJump (leaps);
     - kicks.

   Mirroring doubles some. AttackFlow already measures blends.
2. **Soon: exaggerate in Blender (Blender MCP).** Additive keys on top of those clips: bigger arcs, held poses, hand shapes for runes.
   Each comes back as an override clip. This is cheap and keeps the body's style.
3. **New clips where they pay most.** These are the ultimates and each school's signature casts.
   - **Buy** a casting pack made for the same Kevin Iglesias body: the user already owns his Human Melee Animations, so his other packs
     fit with no retargeting.
   - **Generate** with LocalSpawn's motion-from-video route: picture → Wan clip → GEM-X → Blender retarget. It's slower and uneven, so
     use it for one or two signature moves.
   - Mixamo needs a browser login the agent doesn't have; the user could download a list.

## 6. Effects: one kit, a language per school

- **A shared skill-effects kit**, data-driven on `SkillDefinition`. Each phase has optional parts:
  - **cast:** a school-coloured rune circle decal under the feet, a hand or weapon glow;
  - **release:** a flash, a light pulse (one pooled light), a shake preset;
  - **projectile or zone:** a mesh, a trail, a decal;
  - **impact:** a burst, ground impact, decal;
  - **lingering:** an ice patch, burning ground, a hex mark.
- **One look per school,** so a power is known by its shape before its colour:

  | School | Colour | Shape |
  |---|---|---|
  | Frost | cyan/white | shards, mist, crystals |
  | Ember | orange | sparks, heat, ash |
  | Beast | grey/blood red | claw slashes, dust, a spectral wolf or bear |
  | Wyrd | green-gold | motes, leaves, runes |
  | Steel | the gold ribbons | already in use |
  | Thunder | violet-white | forks |
  | Grave | green-black | smoke, bone |

- **Built from what we have:**
  - particle systems and the Effect Particle shader;
  - decals (the projected telegraph's material, recoloured);
  - ground impacts (craters, dust);
  - ribbons;
  - two new shaders: an erosion/dissolve for magic shapes (ice lances, root walls, spectral beasts) and an emissive rune-circle decal;
  - meshes from the Blender MCP (spikes, roots, runes);
  - sprites and noise from Python or LocalSpawn's image models.
- **Readability rules for the top-down camera:**
  - The ground shape tells the area.
  - The player's colours are gold and the school's; enemies' warnings stay red and pale, so the two never mix.
  - Big bright cores, short life.
  - An area power shows its full shape on the ground before it lands, so it reads in a crowd. This follows the projected-warning
    rule the game already uses for enemies.
- **Sound:** a cast cue and an impact per school, made the existing way (Python synthesis first, MOSS/Stable Audio by fit).
- **Cost:** effects are pooled (the decal pool pattern), with a particle budget per power. FrameTally's Perf rows now show what they
  cost on friends' machines.

## 7. Architecture

- **`SkillDefinition` grows from four parts to a timeline of phases** (wind-up, release, recovery), each with an animation state, speed,
  movement (lunge, leap, walk-while-casting) and effects.
- **New deliveries:**
  - melee arc (today's AttackDefinition);
  - projectile;
  - ground zone at the cursor (with telegraph);
  - dash path;
  - self aura;
  - channel;
  - summon (a spectral beast).
- **Statuses:**
  - each school's status as a `StatusEffect` asset;
  - stacking rules and thresholds (Chill × 3 → Frozen);
  - consumes ("a heavy on Frozen shatters").
- **Slots:** `SkillLoadout` gains typed slots (two weapon, two power, ultimate). Weapon skills come from the weapon in hand, through
  `WeaponDefinition` (art plus second art).
- **Unlocks:**
  - "taught by" on each power (boss, Lost One, mentor building, quest);
  - `QuestReachability`/`ContentAudit` check that every power can be learned.
- **Saves:**
  - known powers, chosen variants and the loadout through SaveMapper;
  - older saves keep every ability they know.

## 8. A first slice to judge it by (recommended order)

1. **Effects kit plus three powers remade with it:**
   - Frost Rune: lance, Chill.
   - Ash Lad's Ember: cone, Burning.
   - Trollsbane Wrath: the ultimate.

   Each gets phases, tuned clips, a cast circle, an impact and a lingering zone. *Judge it in a raid fight from the game camera.*
2. **Weapon skills on Q/E for the sword and greataxe,** from their arts and runes, with cooldowns.
3. **School statuses and one consume** (Shatter).
4. **The story spine:**
   - The Heartroot teaches the first power.
   - Liv's lore teaches Frost's passive.
   - The starting set moves to teachers.
5. **Then the rest:**
   - all families' E;
   - the remaining schools;
   - Thunder and Grave;
   - variants at the altar;
   - travel powers;
   - the Lost Ones' powers;
   - new clips for ultimates.

## 9. Questions for the user

1. **Keys:**
   - Q/E weapon, R/F powers, T ultimate (recommended).
   - Or keep the 1-5 bar for everything.
2. **Schools:**
   - Keep the five and add Thunder and Grave (recommended: seven, as V Rising's breadth).
   - Or stay at five.
3. **Economy:**
   - Cooldown plus Focus (recommended: keeps "fight well to cast").
   - Or cooldown only, as V Rising.
4. **Starting kit:**
   - The weapon's Q only, with powers taught through the story (recommended).
   - Or keep today's six at the start.
5. **Buying assets:**
   - May I list Kevin Iglesias casting packs and URP effect packs (paid) to choose from?
   - Or keep it homemade (Blender, LocalSpawn, Python) for now?
6. **First slice:** the three powers above, or others you'd rather see first?

## Sources

- [V Rising Weapon Skills (fandom wiki)](https://vrising.fandom.com/wiki/Weapon_Skills): secondary skill at copper, tertiary at iron
- [V Rising Jewels (fandom wiki)](https://vrising.fandom.com/wiki/Jewels), [GINX: Spell Jewel guide](https://www.ginx.tv/en/spell-jewels)
- [AlcastHQ: V Rising 1.0 update](https://alcasthq.com/v-rising-1-0-update): spellbook rework, a spell point per V Blood
- [GameLeap: ten massive changes in 1.0](https://www.gameleap.com/articles/v-rising-1-0-ten-massive-changes-you-should-know-about): Altar of Recollection, boss points
- [Digital Trends: best abilities](https://www.digitaltrends.com/gaming/v-rising-best-abilities): V Blood carriers grant abilities
- [Gamer Choice: best passives](https://gamer-choice.com/v-rising-best-passives-top-skills-to-unlock-for-maximum-power/), [PlayerAuctions: all passives](https://blog.playerauctions.com/others/all-v-rising-passive-and-which-are-the-best-ones/): school passives (ignite, leech, condemn, weaken, static)
- [Stunlock: Interview with the Vampire](https://blog.stunlock.com/interview-with-the-vampire/), [Wccftech 1.0 Q&A](https://wccftech.com/v-rising-1-0-qa-stunlock-on-the-future-of-the-hit-vampire-survival-game/): combat lineage (Battlerite), tilted top-down camera
- [Stunlock dev update 29: Weapons of the Night](https://blog.stunlock.com/dev-update-29-weapons-of-the-night/)
