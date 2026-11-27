# Making skills a choice: research (2026-11-27)

The user: "skills are okay now, a good addition to combat. But right now they're mostly just basically a way to do damage. so the meta
is to just put all skills on the bar and spam the one that does the most damage. How can we make skills more of a choice? and how can
we balance them? Can we tie them more to weapon type or something else? research how safe haven handles this now and how other similar
games handle it."

**Phase 1 built 2026-11-27** (the user: "do as you recommend"): 3.1 (kinds and prices; techniques' damage cut but kept at 2.5 light swings or more, the floor from the 11-25 playtest; Trollsbane Wrath 110 + 4.4 x weapon, poise 80; War Cry's fury 12 s; Wolf's Lunge's "Light Step" became "Running Wolf", +50% haste, as a technique has no Focus to refund), 3.2 (the typed bar; empty slots say Art, Tech, Power, Ult) and 3.7 step 2 (the `Loadout` row, `SkillDamage.<id>` rows, the Playtest Runs window's ability table). The bar stays changeable anywhere (question 4's default). Phases 2-3 wait for a playtest.

**New skills built 2026-11-27** (the user: "create new skills for each weapon type and a few magic abilities. make them tie nicely into
each other. some can set up, while others do the damage"): 3.3 and 3.4 in full, ahead of the playtest.

| Skill | Kind | Weapon | Sets up | Pays off (uses up, burst x the blow) |
|---|---|---|---|---|
| Crossing Cut | Technique | sword | Rend | |
| Open the Vein | Technique | dagger | | Rend x1.6 |
| Shield-Splitter | Technique | axe or club | Sundered | |
| Reaping Arc | Technique | greataxe | | Rend x0.9 to all it sweeps |
| Headsman's Drop | Technique | greatsword | | Sundered x1.4 |
| Earthbreaker | Technique | hammer | | Roots x1.5, leaves them Hamstrung |
| Run Through | Technique | spear | | Cowed x1.4, pierces two |
| Rib-Breaker | Technique | fists | Cowed | |
| Kindling Bolt | Technique | staff | Burning | |
| Hunter's Snare | Technique | bow | Roots | |
| Wildfire | Power (1 circle), Ember | any | | Burning x2 to all near (Yrsa's Stillwater Remembered) |
| Hoarfrost | Power (1), Frost | any | Frostbite, then Frozen | (heavy blows shatter) (Liv's Eastwood Remembered) |
| Elf-Shot | Power (1), Wyrd | any | Cowed | (Ingrid's Alder Mere Remembered) |

- **Weapons.** A weapon's own technique is learned the first time one of copper (tier 2) or better is wielded
  (`PlayerSkillSettings.WeaponTechniques`). It is cast only with that weapon in hand and greys on the bar after a swap. Fists teach the
  first time the player fights barehanded. The four older techniques favour weapons (+25%): Whirling Steel sword, greataxe and dagger;
  Wolf's Lunge dagger, spear and sword; Mountain Leap greatsword, hammer and greataxe; Troll's Hurl axe or club, hammer and fists.
- **Older skills joined in.** The Ash Lad's Ember cauterises Rend (x1), and Mountain Leap lands on Sundered (x1, Crushing Landing).
- **Rules.** Payoffs are `SkillDefinition.Combos`, run with the altar's options (`PlayerSkills.AfterOptions`). The burst lands before the
  status is used up, so a Sundered foe takes its +15%. It staggers by the attack's poise times the factor, and counts as the skill's own
  damage in the log. Thrown skills (Kindling Bolt, Hunter's Snare, Elf-Shot) only set up: their blow lands after the cast.
- **Checked in Play** through the game's own payoff code on Draugr: each status was used up. The bursts came out different from the plain
  factor, because a Draug's weaknesses and resistances apply to them: Wildfire 75 from a 20-point blow, Earthbreaker 45.
- **Not yet seen:** the new techniques in a real fight. Their hit times are measured from each clip's contact, not watched.
- **Icons:** eleven are Chroma1-HD. Open the Vein and Wildfire are Qwen-Image, because Chroma left white edges on them.
- **Tests:** `SkillPriceTests`:
  - every kind of weapon teaches exactly one technique;
  - every status that is paid off can be laid in at least two ways;
  - every status a weapon lays is paid off somewhere.

It follows Docs/CombatSkillsResearch_2026-11-24.md (the V Rising-style plan) and
Docs/SkillsImprovementResearch_2026-11-24.md (altar options, built on three skills). Numbers about other games come from wikis and
guides; **[unverified]** marks claims no source confirmed.

## 1. Why spamming the strongest skill wins today

What the data says (every `SkillDefinition` and its attack, read 2026-11-27; damage at a copper weapon of 15, `DamageCalculator`:
base + weapon × ratio, before Player/Art damage scales):

| Skill | School | Focus | Cooldown | Damage per foe | Shape, most foes | Besides damage |
|---|---|---|---|---|---|---|
| Whirling Steel | Steel | 1 circle | 6 s | 39 | ring 2.8 m, 6 | Sundered, Steel Wall 4 s |
| Wolf's Lunge | Beast | 1 | 8 | 48 | 120° 2.4 m, 3 | 5 m lunge, Rend, haste 4 s |
| Ash Lad's Ember | Ember | 1 | 9 | 61 | 90° 4.5 m, 5 | Burning, embers on the ground |
| Frost Rune | Frost | 1 | 10 | 68 | lance 6 m, 4 | Frostbite, Frozen on repeat |
| Heartroot's Grasp | Frost | 1 | 10 | 68 | 60° 6 m, 4 | Roots |
| Mountain Leap | Beast | 1 | 14 | 42 | ring 3.6 m, 6 | 7 m leap, poise 45 |
| Troll's Hurl | Steel | 1 | 14 | 86 | thrown 18 m | poise 60 |
| Winter's Breath | Frost | 1 | 16 | ~156 (8 ticks of 19.5) | 60° 5 m, 5, channel 2.4 s | Frostbite |
| Huldra's Blessing | Wyrd | 1 | 20 | none | self | heal 35, armour and speed 6 s |
| War Cry | Steel | 1 | 30 | none | 7 m round | Cowed, damage up 8 s |
| Trollsbane Wrath | Steel | 1 | 40 | 126 | ring 4 m, **10** | Sundered, lifesteal 8 s, but poise only 10 |
| Weapon's Art | per weapon | 1 | none | per family | per family | the family's movement |

The five causes:

1. **One price for everything.** Every skill, and the weapon's art, costs one Focus circle (since a7fb0fff). The 11-26 run earned 86 Focus
   in a minute of fighting, so a circle takes about a minute, while most cooldowns are 6-16 s. **Cooldowns almost never bind; Focus
   does**, and with one price the only question left is "which skill gives most for a circle?" Winter's Breath (about 156 to each foe in
   a cone) and Trollsbane Wrath (126 to up to ten) win, and nothing else comes close. That is the meta the user saw.
2. **Every skill is mostly damage.** Their other effects are short buffs (4-8 s) or a status, worth little next to a big number. Huldra's
   Blessing and War Cry (no damage at all) pay the same circle as Trollsbane.
3. **No roles on the bar.** Five free slots (1-5) hold any skill, so the bar fills with the top hitters.
4. **Skills don't care about the weapon.** The weapon side has a strong identity: 10 families, each with its own art, rune art and
   signature (Docs/Combat.md). The 11 abilities work the same in any hand; the weapon only scales their numbers.
5. **The foe rarely changes the answer.** Statuses are only consumed by altar options (Shattering Lance, Blood Scent, Cold Comfort), and
   only Frost Rune, Wolf's Lunge and War Cry have those options. Enemies differ (shieldbearers, charging boars, rootling swarms, bosses
   that stagger), but no skill is clearly *the* answer to one of them.

Also: Trollsbane Wrath, the biggest slam in the game, does 10 poise, less than Mountain Leap's 45 or Troll's Hurl's 60 (probably an
oversight).

**Telemetry is thin.** Skill rows (`Skill.Cast`, `Skill.<id>`, `Focus.Earned`) exist since dfa87a66, but only one run has them
(20261126-221809, 15 rows) and it used cheats 35 times, perhaps Endless Focus.

## 2. How similar games make it a choice

- **V Rising** ([gamerguides](https://www.gamerguides.com/v-rising/guide/powers/abilities),
  [spellsandguns wiki](https://vrising.wiki.spellsandguns.com/Weapon_Skills), [gameplay.tips](https://gameplay.tips/guides/v-rising-list-of-all-skill-jewels.html)):
  - **The bar has typed slots.** One travel dash, two free spells, one ultimate; plus two weapon skills that come with the weapon type
    (the second at copper, the third at iron).
  - **Weapon skills are mostly tools, not damage.** They are on short cooldowns (8-10 s) with no resource: Mace Smack knocks back and
    stuns 1.2 s, Spear Harpoon pulls, Axe X Strike stuns 2 s, Slashers dash and slow.
  - **Spells mix damage with a role**: shields (Ice Block grants CC immunity), a counter (Chaos Barrier), a pull with charges (Void,
    2 charges), summons.
  - **Builds pay for combinations, not raw damage.** Each school owns a status, and jewels consume one for a payoff (consume Chill to
    Freeze 2.3-4 s; consume Leech for damage *or* healing, not both). School passives reward staying in one school.
- **Dota 2** ([Liquipedia](https://liquipedia.net/dota2/Strong_Dispel), [dotabuff](https://cs.dotabuff.com/blog/2021-08-17-on-the-importance-of-dispels)):
  - Each hero's kit is a fixed mix of roles (nuke, disable, escape, sustain).
  - One skill point per level makes "which to raise first" a real choice. The ultimate ranks only at 6/12/18.
  - Strong effects exist because answers exist (basic and strong dispels, Black King Bar).
- **Diablo IV** ([Maxroll](https://maxroll.gg/d4/getting-started/skill-trees), [Icy Veins](https://www.icy-veins.com/d4/guides/barbarian-skills)):
  - Six slots, and skills come in categories: Basic (no cost, generates resource), Core (spends it for big damage), class clusters for
    defence and mobility, and one Ultimate on a cooldown of 45 s or more.
  - Each skill offers exclusive pick-one-of-two upgrades.
  - Season 9 gave Basic skills +5 resource on first use, to make them worth a slot.
- **Path of Exile 2** ([Maxroll](https://maxroll.gg/poe2/resources/skills-in-path-of-exile-2), [Game8](https://game8.co/games/Path-of-Exile-2/archives/495713)):
  - Attack skills require a weapon type.
  - Damage comes from chains: Armour Breaker breaks armour, Leap Slam consumes the break to build stun, Boneshatter cashes in the primed
    stun.
  - How much stun primes a foe rises with its rarity: 40% normal, up to 80% unique.
- **Lost Ark** ([Fextralife](https://lostark.wiki.fextralife.com/Tripods)): every skill is rated for Stagger, Weak Point and Counter, so a
  skill earns its slot against bosses, not by damage alone.
- **Elden Ring** ([Fextralife](https://eldenring.wiki.fextralife.com/Ash+of+War:+Lion's+Claw)):
  - Each Ash of War lists the weapon classes it fits. Lion's Claw fits swords, axes and hammers, not small or thrusting weapons.
  - FP cost grows with power, and with no FP left Bloodhound's Step falls back to a plain quickstep.
- **Nioh 2 / Wo Long** ([rpgsite](https://www.rpgsite.net/feature/9545-nioh-2-combat-tips-guide-burst-counter-yokai-shift-and-more-strategies-to-beat-any-boss),
  [Prima](https://primagames.com/gaming/how-does-spirit-work-in-wo-long-fallen-dynasty)):
  - One gauge pays for everything, and costs differ by art and by weapon.
  - Timing well refunds the gauge with a stance-specific bonus.
  - Wo Long's Spirit fills by fighting and empties by martial arts, the nearest match to Focus **[that detail unverified]**.
- **Dragon's Dogma 2** ([Shacknews](https://shacknews.com/article/139426/unlock-equip-skills-dragons-dogma-2)): four skill slots per
  vocation, changed only at a guild or while resting. The loadout is a commitment before setting out.
- **Balance practice:**
  - Power, utility and cost trade against each other ([Game Developer](https://www.gamedeveloper.com/design/the-3-key-factors-of-balance-in-game-design)):
    a skill "too strong not to use, or too weak to ever use" is a false choice.
  - Riot flags a champion as too strong above a 54.5% win rate, and as weak below 49% or under 5% presence
    ([League dev blog](https://www.leagueoflegends.com/en-us/news/dev/dev-champion-balance-framework/)).
  - No studio publishes a points formula for damage against CC against mobility **[none found]**. The "same budget for every kit" idea
    is qualitative.

The common thread: **choice comes from a limited, typed bar, from costs that differ by kind (cooldown against resource), from skills
whose worth depends on the weapon or the foe, and from setting up and paying off rather than one big number.**

## 3. Suggestions

Most worth it first. Each says what it costs us and what in Safe Haven it builds on.

### 3.1 Two prices: tools on cooldown, powers on Focus (the core fix)

Split the skills by what pays for them, as V Rising (weapon skills on cooldown, spells and ultimate) and Diablo IV (free basics,
costly cores, ultimate on a long cooldown) do:

| Kind | Paid by | Cooldown | What they are for | Skills |
|---|---|---|---|---|
| **Techniques** | nothing (0 circles) | 6-14 s | moving, opening, setting up: modest damage | Wolf's Lunge, Mountain Leap, Whirling Steel, Troll's Hurl |
| **Powers** | 1 circle (2 for the strongest) | 10-20 s | a school's damage, control or care | Frost Rune, Ember, Heartroot's Grasp, Huldra's Blessing (1); Winter's Breath (2) |
| **Ultimates** | 3 circles (a full bar) | 40-60 s | the big turn of a fight | Trollsbane Wrath, War Cry |

- **Effect:** techniques are pressed on cooldown all fight, and Focus goes to powers when they matter. Trollsbane Wrath costs a full bar,
  so it is a moment, not the default. With different prices, "most damage per press" stops being the only question.
- **Builds on:** `SkillDefinition.focusCircles` (already 0-3) and `cooldown`. The data change is small. The bar's greying (no circle
  full) must read per skill.
- **Fix with it:** Trollsbane Wrath's poise, 10 to about 80, so the ultimate slam topples as it looks.
- **Weapon arts:** they keep their one circle. With techniques free, the art stays the weapon's own Focus spender.
- **Cost:** low. **Balance:** yes, it moves every number, and needs a playtest.

### 3.2 A typed bar: what goes in which slot

Keys stay 1-5, draggable, as the user asked (2026-11-24). Each slot gets a kind:

`[1 Art] [2 Technique] [3 Technique] [4 Power] [5 Power or Ultimate]`

- **Effect:** "all skills on the bar" stops being possible. With 11 skills and two technique slots, two power slots and one free slot,
  the player chooses within each kind: which two techniques fit this weapon, Frost Rune or Ember, a second power or the ultimate. This is
  V Rising's and Diablo IV's lesson.
- **Builds on:** the skill bar and its drag-and-drop. A slot shows its kind as a frame. A skill can't be dropped on the wrong kind, and
  the bar says why.
- **Cost:** low to medium (UI, and saved loadouts keep working by kind).

### 3.3 Techniques belong to weapons ("tie them to weapon type")

The four physical techniques get **weapon affinities**, as Elden Ring's Ashes of War list the weapon classes they fit and V Rising's
weapon skills come with the weapon:

| Technique | Fits (its own version) | Example of what the fitting weapon adds |
|---|---|---|
| Whirling Steel | sword, greataxe, dagger | greataxe: Momentum stacks from each foe struck; dagger: Rend instead of Sundered |
| Wolf's Lunge | dagger, spear, sword | spear: pierces two and stops a charge (its Anti Charge); dagger: a backstab if it ends behind |
| Mountain Leap | greatsword, hammer, greataxe | hammer: Crush (a reeling foe staggers at once) |
| Troll's Hurl | one-handed axe or club, hammer, fists | axe or club: Guard Breaker (double poise against a shield) |

- **Soft, not hard.** With a weapon that doesn't fit, a technique still works as now. With a fitting one it gets its weapon's version
  (the family's signature joins in) and, say, 25% more. Nothing a player can do today is taken away (the never-remove rule).
- **The second weapon becomes a second kit.** Z swaps weapons (Docs/Combat.md). With affinities, swapping from a spear to a hammer
  changes which techniques are at their best, as V Rising's weapon swap changes skills.
  - Guides say V Rising carries cooldowns across a swap so the two kits can't be chained **[unverified]**. Do the same.
- **The card shows it:** "Fits: dagger, spear, sword" with the family icons, the current weapon's version lit.
- **Cost:** medium: per-family variants as data on the technique, as `WeaponArtSlot` already does for arts.

### 3.4 Set up, then pay off: damage comes from order

Make free techniques the *appliers* and Focus powers the *consumers*, as V Rising's jewels and Path of Exile 2's armour break and stun
priming do. Spamming one skill then never reaches the best damage. Using the statuses that exist:

| Set up by (technique or art) | Status | Paid off by (power or ultimate) |
|---|---|---|
| Whirling Steel, Mountain Leap | Sundered (armour broken) | Troll's Hurl and Trollsbane Wrath deal +40% and topple a Sundered foe |
| Wolf's Lunge | Rend (bleeding) | Ember cauterises it: a burst and a stagger (a cross-school reaction, research 2026-11-24 #9) |
| Frost Rune, Winter's Breath | Frostbite, then Frozen | any heavy or physical technique Shatters (exists: `ConsumedBy`) |
| Heartroot's Grasp | Roots (held fast) | a backstab is sure (the dagger's art blinks behind) |
| War Cry | Cowed | Huldra's Blessing heals more for each Cowed foe near |

- **Builds on:** `StatusEffectDefinition.ConsumedBy`, `SkillOption.consumes`, and the altar options of the three skills that have them.
  The suggestion moves the consumers from rank-2 altar options into the skills themselves, and gives every power one.
- **Cards:** say "sets up: Sundered" and "pays off on: Sundered" with the status icons, so the pairs are learned by looking.
- **Cost:** medium. **Balance:** yes.

### 3.5 The foe decides which skill is right

Give each skill two or three **ratings** shown on its card (Lost Ark's Stagger and Counter ratings, Path of Exile 2's priming), and
make sure each matches a foe the raids already have:

| Rating | Foe it answers | Skills |
|---|---|---|
| **Breaks guard** | Barrow Shieldbearer (its shield takes 75% from the front) | Troll's Hurl, Whirling Steel |
| **Stops a charge** | boars, the Old Moss Troll's rush | Troll's Hurl, Wolf's Lunge with a spear, Heartroot's Grasp |
| **Crowd** | rootlings, wolf packs, draugr groups | Whirling Steel, Ember, Trollsbane Wrath |
| **Topple** (poise) | bosses' stagger windows, elites | Mountain Leap, Troll's Hurl, Trollsbane Wrath |
| **Reach** | shooters, the bog's spitters | Frost Rune's lance, Troll's Hurl, Wolf's Lunge |

- **Effect:** the briefing and the rumours already say what a raid holds (Raid Pacing, rumours). Choosing the bar at home for the foes
  ahead becomes the decision Dragon's Dogma 2 makes of its four slots.
- **Optional, stronger:** the bar is set at home or at a lit raid camp, not mid-fight (Dragon's Dogma 2). That fits an extraction game,
  but it is a restriction: ask first.
- **Cost:** low for the ratings (data plus card icons). Their worth depends on enemies honouring them, which most already do through
  poise, guard and charge rules.

### 3.6 A small reward for staying in a school

If two or more slotted skills share a school, that school's status lasts 30% longer and its first application each fight is stronger,
in the manner of V Rising's school passives.

- **Effect:** mixing "the best of every school" is no longer free. It is small on purpose: a nudge, not a lock.
- **Builds on:** `SkillSchool`. The teachers (Liv for Frost, Gunnar for Beast...) could teach it, as the 11-24 plan suggested.
- **Cost:** low.

### 3.7 How to balance: a budget, a test and the log

1. **A power budget per skill.** No game publishes one, so this is our own, to be tuned by playtests:
   ```
   budget = damage per foe (% of a light swing) × (1 + 0.5 per extra foe it usually reaches, up to 3)
          + 40 per second of hard control (stagger, Frozen, Roots) + 15 per second of a slow
          + 5 per metre moved + damage reduction % × seconds / 10 + heal / 2
   ```
   - **Pricing:** Focus circles ≈ budget / 100 (rounded to 0-3). The cooldown is the second lever: techniques trade budget for no Focus by
     having lower numbers.
   - **A test enforces it:** `SkillBudgetTests` computes every skill's budget from its data, prints the table, and fails a skill more
     than 25% over or under its kind's band. Balancing then becomes a data change the test confirms.
2. **The playtest log answers "is it a choice?"** The rows exist (`Skill.<id>` casts, `Skill.Damage`). Add:
   - a `Loadout` row as each raid starts (the five slots and the weapons);
   - the Run Comparison window showing, per skill, its share of loadouts, casts per fighting minute and share of skill damage.
3. **Thresholds** (after Riot's framework):
   - A skill in over 80% of loadouts, or doing over 40% of skill damage, is a default, not a choice. Cut it, or give its slot rivals.
   - One in under 5% of loadouts needs a reason to exist.
   - Runs with cheats are left out, as the log already marks them.

### 3.8 Not recommended now

- **"Foes learn a repeated skill"**, as they learn backstabs. It would punish repetition directly, but it fights the symptom. With 3.1
  and 3.2 there is little to repeat. Keep it in reserve for after a playtest.
- **Hard weapon locks** (a technique only with its weapons). It is clearer, but it takes away what players can do now. The soft
  affinity in 3.3 gets most of the identity.

## 4. Order to build it

1. **Phase 1, the meta fix:** 3.1 (two prices, the Trollsbane poise), 3.2 (typed bar), 3.7 step 2 (Loadout row and the per-skill view).
   Then a playtest.
2. **Phase 2, weapon identity:** 3.3 (affinities, with variants for the four techniques), and the cards' ratings from 3.5.
3. **Phase 3, depth:** 3.4 (set up and pay off), 3.6 (school reward), and 3.7 step 1 (the budget test) once the numbers have settled.

## 5. Questions for the user

1. **Techniques free of Focus** (on cooldown only), with powers on Focus and the ultimate on a full bar: is that the right split?
2. **The typed bar** `[Art][Technique][Technique][Power][Power or Ultimate]`, still on keys 1-5 and draggable: good?
3. **Weapon tie:** soft affinities (any weapon works, a fitting one is better and different), or a hard lock?
4. **The bar set only at home or at a camp,** or changeable anywhere as now?
5. **Build phase 1 now** and keep phases 2-3 until after a playtest?
