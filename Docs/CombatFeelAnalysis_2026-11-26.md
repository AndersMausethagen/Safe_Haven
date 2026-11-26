# Combat feel analysis (2026-11-26)

The user asked: "do an analysis in combat. what can we do to make it feel better? animations? tweaking of numbers? other things?
research and compare our combat with how other games do it and suggest improvements".

## Status (2026-11-26): the user's answers, and what is built

The user: "i agree with most, but keep skills costing focus, increase heavy swing damage with 40%. let Wolf wait a bit before running off."

| Item | Status |
|---|---|
| 1. Heavy and charged | **Built.** One-handers' heavies +40% (Sword Heavy 1.869, Chop Heavy 2.443, Dagger Heavy 2.558); every charged blow x1.29 (Sword, Chop, Hammer 4.0; Axe 3.871; Greatsword 4.258; Spear 3.742; Dagger Focused Stab 3.355). Two-handers' and the spear's heavies were left alone: they already paid 28-53% more than their chains. |
| 2. The wolf waits | **Built.** Monster Attack › Break Away Delay 0.3 s on the wolf's bite; the opening ring runs on through it. Probed live: four bites, each 0.30 s standing still, then the 3.5 m run. |
| 3. Pressure budget | Not built, as recommended: wait for a playtest of the safety round. |
| 4. Skills cooldown only | **Not built: the user keeps the Focus cost.** |
| 5. Wolf animation | In progress (see the commit after this one). |
| 6. Exaggeration pass | In progress. |
| 7. Reach assist | **Built** (0.6 m, not 0.8: the step gets only the end of the wind-up). Probed live: a target 2.94 m off, 0.8 m step and a miss without it, 1.4 m and a hit with it; 4.36 m off still misses. |
| 8. Log the moveset | **Built:** `Move.*` and `Whiffed.*` rows. |
| 9. Reeling hint | **Built:** "Heavy [LMB]" over the focused creature, three times a save, on a screen canvas (world text sank under the mist). |

This was a read-only analysis: no code, data or scenes were changed by it. It builds on the four earlier combat documents
(`Docs/CombatResearch.md`, `Docs/EnemyCombatResearch.md`, `Docs/CombatFeelReview_2026-11-19.md`,
`Docs/CombatSkillsResearch_2026-11-24.md`) and doesn't repeat them. Almost all of their items are built. Paths are relative to
`Assets/_Project/` unless they start with `Docs/`.

**Evidence:** the user's two build runs of 2026-11-25 (`Playtests/20261125-184424.csv`, and the 11-25 part of `20261121-151353.csv`),
every weapon's attack assets, `Resources/CombatFeel.asset`, `Resources/CombatTechniqueSettings.asset`, the wolf's data and animator.
Both runs come **before** the skill payoff round (dfa87a66) and the safety round (a31037f5), which nobody has played yet. Where those
rounds already answer a finding, it says so.

---

## 0. Verdict

The systems are deep, but the player uses very few of them. In both runs **80% of swings were plain light swings**. Heavy swings were
5-9%, arts 1-3%, and the log can't count charged blows, follow-ups or backsteps at all. Parries and perfect dodges came
8-11 times a run, against about 45 hits taken. The game has a fighting game's moveset and is being played as "click, click, roll".

That isn't the player's fault. The numbers and the enemies don't ask for anything else:

1. **For one-handed weapons, the heavy swing pays no more per second than the light chain**, while it plants your feet, and a full
   charge pays *less*. A light is the safer swing for the same reward.
2. **A third of the axe's swings hit nothing.** Wolves, the enemy fought most, bite and then run 3.5 m away, so the answering swing
   tends to land as they leave.
3. **Deaths come from crowds of 5-7**, not from single strikes the player failed to read.
4. **Skills barely appear:** about 1 art per 4 minutes. That matches the user's complaints of 11-25 ("skills deal almost no damage",
   "way too hard to build focus").
5. **The wolf has the thinnest animation set in the game**, yet the player fights it most. Its own model file has clips nobody uses.

The most feel per effort comes from numbers (items 1-4 below, all small), then the wolf's animation (5), then a gentle reach assist
(6). Section 4 lists what I'd leave alone.

---

## 1. What the 11-25 runs show

| | Run A (40 min) | Run B (33 min) |
|---|---|---|
| Where | Prologue, Haven, two Misty Woods raids | Misty Woods raid, the Heartroot, another raid |
| Weapon | Hand Axe (Woodsman's Axe) | Copper Sword and shield |
| Swings: light / rush / heavy / art / other | 343 / 33 / 40 / 11 / 1 | 335 / 48 / 20 / 5 / 11 bashes |
| Light share / heavy share | 80% / 9% | 80% / 5% |
| Swings that hit nothing | **150 of 428 (35%)** | 90 of 419 (21%) |
| Parry / perfect dodge / dodge / block | 7 / 1 / 26 / 2 | 5 / 6 / 79 / 35 |
| Times hurt | 45 | 46 |
| Deaths | 1: three wolf bites in ~20 s, **6 creatures** within 8 m | 2: the Heartroot; then wolf pounces and Marsh Light bolts, **5-7 creatures** |

- **Wolves hurt the most.** In run A, 23 of the 26 big blows (5% of health or more) were the wolf's Hamstring Bite.
  Wolves were also 28 of the 45 kills.
- **The Heartroot's opening:** three blows in about 5 s (25, 28, 36) took 89 of 160 health (56%). Its Ground Slam took 54 (34%). The
  player toppled it twice with parries and died with the boss at 49%. Boss work is already waiting on the user's answers (P0-P6,
  `Docs/BossEncounterResearch_2026-11-17.md`, `Docs/HeartrootBossReview_2026-11-17.md`), so this analysis doesn't re-derive it.
- **What the log can't see:** charged blows, heavy follow-ups and backstep attacks are all counted as `Swing.Light`
  (`CombatTally.SwingName` only names heavy, rush, art, bash and guard counter). Whiffs carry no distance to the target. Ability use
  (the 1-5 bar) wasn't logged until dfa87a66.

---

## 2. How other games handle the same problems

| Problem here | How others do it | Where we are |
|---|---|---|
| Heavy swings go unused | **Elden Ring:** heavies are the stance-breaking tool. A charged heavy does about 3x a light's stance damage (wikis: 8 vs 24), and a broken stance opens a critical. Lights keep the bar from recovering. **No Rest for the Wicked:** the charged blow is the big, slow commitment that pays for itself. | Heavies do 3-4x a light's poise, but the poise reward is easy to miss, and for one-handers the damage per second is equal. |
| Swings that miss a moving target | **Batman Arkham:** aim roughly in a direction and Batman leaps to the target, even one off screen. **God of War (2018):** target choice that doesn't rely on lock-on, from the stick plus visual cues. **Hades:** dash-strike closes the gap in one press. | A focus target and an aim turn (300°/s), a 0.8-1.0 m step-in, and a rush attack out of a dodge. No stretch when the target steps away. |
| Packs overwhelm | **Doom (2016):** each kind of attack has a limited number of "tokens"; an enemy must hold one to attack, and the rest keep moving and pressuring. (This comes from a developer blog's account of id's talk, unverified.) | Two melee attackers at once, 0.35 s apart, off-screen ones wait, and one shooter in a crowd (the safety round). There is no limit on total incoming damage, and pounces, ranged attacks and great beasts don't queue. |
| Skills rarely used | **V Rising:** weapon skills and spells run on **cooldowns only** (about 8-12 s), so skills are part of every fight. Its combat comes from Battlerite: aiming and dodging with WASD. | Abilities cost a whole Focus circle (100) on top of a cooldown; a landed light earns about 8. |
| Hit stop on many hits | Guides suggest 2-4 frames for light hits and 5-8 for medium ones, scaling with how much the hit matters; freezing on every hit slows the whole game. | Already capped (0.25 s, 0.3 s on kills) since 11-19. Fine as is. |

---

## 3. Recommendations, ranked by feel gained per effort

Effort: **S** under half a day, **M** one to two days.

### Numbers

#### 1. Give the heavy and the charged blow a reason (S)

Damage per second as weapon-damage ratio ÷ recovery. The light chain uses the 1.15 pace. A grown heavy's clock counts the hold
(`WindupTakenUp`); for a charged blow I add the 0.9 s it waits at full charge.

| Family | Light chain | Heavy | Full charge | Heavy vs chain |
|---|---|---|---|---|
| Sword | 1.62 | 1.80 | ~1.50 | +11% |
| Hand axe, club | 1.69 | 1.72 | ~1.50 | +2% |
| Dagger | 1.70 | 1.71 | ~1.32 | 0% |
| Spear | 1.27 | 1.63 | ~1.37 | +28% |
| Greatsword | 1.10 | 1.68 | ~1.40 | +53% |
| Hammer | 1.08 | 1.45 | ~1.35 | +34% |

For the weapons the user played (axe, sword), a heavy earns the same per second as tapping. It also plants the feet for 0.37-0.48 s,
can't be dodged out of as freely, and gets punished on a whiff from tier II. Holding the full charge pays **less** than the chain for
every family. The two-handers are fine: their heavies earn clearly more.

**Change (data only):**
- One-handers' heavies +20%: Sword Heavy ratio 1.335 → 1.6, Chop Heavy 1.745 → 2.1, Dagger Heavy 1.827 → 2.2.
- Every charged blow 3.1 → 4.0 (dagger 2.6 → 3.4). That brings a full charge to about 1.15x the chain, which pays for standing
  still 0.9 s in front of a pack.
- Re-baseline `GearBalance` and the raid-tier tests afterwards. The user's rule: feel first, numbers after.

**Why it feels better:** the player gets a reason to choose. A light hits now and safely; a heavy is a bet that pays off. Elden Ring
and No Rest for the Wicked both work this way.

#### 2. Let the wolf's opening be one you can take (S)

The Hamstring Bite lands at 0.45 s and the wolf is free 0.45 s later. Then, half the time, it bites again, otherwise it breaks
away 3.5 m at 5.5 m/s (`ForestBittenWolf.asset`, bite `breakAway: 3.5`). The axe's first swing lands 0.25 s after the press. Add a
human's 0.2-0.25 s to react to being bitten, and the answer arrives just as the wolf starts running. That matches the 35% of axe
swings that hit nothing.

**Change:** the bite's break-away waits 0.3 s after its recovery, under the pale green opening ring that already shows a spent
blow. Optionally shorten the break-away to 2.5 m at tier I. The wolf stays a hit-and-run animal, but an answer on reflex lands.
**First:** add the distance to the focus target to `Whiffed` rows (S), so the next playtest can confirm the cause.

#### 3. Limit the total damage coming in, not only the attackers (S-M)

Both deaths were crowds of 5-7 creatures. The melee limit (2 at once) works. What gets through it: pounces (a charge), Marsh Light
bolts (ranged), and a second pack that joins the first. Doom's tokens limit *each kind* of attack; we limit only melee.

**Change:** give a tier a **pressure budget**, the damage that may start against the player per second (say 25% of max health at
tier I, 35% at tier III). A strike that would go over it waits, circling and ready, as one crowded out does now. Nothing is made
weaker; blows are only spaced out. The safety round's grace (0.5 s at half damage after a 15% blow) covers bursts. This covers
pressure that lasts. Watch the 11-25 safety build first. If the next death recap still says "crowd 6", build this.

#### 4. Skills: try cooldown only, with Focus as the bonus (S, data first)

V Rising, the model for the skill bar, gates weapon skills and spells by cooldown alone. Here each ability costs one 100-point
circle, and a landed light earns about 8, so a skill costs a dozen landed hits (fewer with the payoff round's crowd and hurt gains).
`SkillDefinition.focusCircles` already accepts 0, so this can be tried in data.

**Change to try:** abilities cost 0 circles and keep their cooldowns. A full circle **empowers** the next ability (for example x1.5
and its status lasts longer) instead of unlocking it. Only the ultimate (Trollsbane Wrath) keeps a real cost of 2 circles. "Fight
well to cast" turns into "fight well to cast big", and the 1-5 bar is in every fight.
**This is the user's call (section 5):** it reverses my 11-24 recommendation, "cooldown plus Focus".

### Animation

#### 5. The wolf: its own unused clips first (S-M)

The wolf is the most-fought enemy (36 of 86 kills, most of the big blows taken), but `Animation/Controllers/Beasts/Wolf.controller`
has only four states: Rest, Locomotion, one **Attack** clip for both the bite and the pounce, and **Stagger**. It has no flinch: a
hit shows only the procedural recoil (`MonsterPose`). Its gallop glides (feet about 1 m/s against a 5.5 m/s chase; animation audit
11-20). Its own model file (`Art/Enemies/Downloaded/Wolf/Wolf.fbx`) has clips nobody plays:

| Unused clip | Use |
|---|---|
| `Gallop_Jump` | The pounce: a leap that reads as different from the bite, so the player knows which is coming |
| `Jump_ToIdle` | The pounce's landing: a visible recovery to punish |
| `Idle_HitReact_Right` | With `Idle_HitReact_Left` (now the stagger): a flinch toward the side the blow came from, on an additive or Arms-style layer |
| `Death` | Not needed: the body falls as a ragdoll |

Then fit the gallop's playback rate to the chase speed, or slow the chase so the feet match. The beast strides tool
(`Fit Beast Strides`) measures this. After that comes a yelp on interrupt. The interrupt sound plays already; a wolf-specific one
would say "you broke its bite". Boars and Rootlings glide the same way (known, open).

**Why it matters:** players learn a fight by what the enemy's body tells them. Right now, the enemy fought most tells them the least.

#### 6. Heavier heavies: an exaggeration pass on the three blows that matter (M)

The user allowed retiming and exaggeration for feel (2026-11-24: "the most important thing is to have satisfying and smooth combat").
Since 11-25 every swing plays its pack clip at 1x, which keeps it smooth. But the pack's heavies and charged blows are built for
realistic swordplay, and from a high camera they read only a little bigger than a light. Use Blender passes on **heavy, charged and
finisher** per family:
- A deeper wind-up, with the body coiling further back. That is the anticipation principle, and it is what reads from above.
- A held frame at contact, 2-3 frames, on top of the hit stop.
- A slower settle after.

Start with the sword and the axe, the ones played. Check them as `safe-haven-combat-animation` says: the blade at contact, in Play, from the side.

### Other

#### 7. A gentle reach assist for light swings (S-M)

When the focus target is just out of reach (2.2-3.7 m for a sword), stretch the light swing's step-in from 0.8-1.0 m to as much as
1.8 m along its last third. It goes toward the target only, never through it, and not out of a backstep. It's a small version of
Arkham's leap to target and Hades' dash-strike, and it removes the "I swung at it and it was just gone" whiff. It also helps against
circling wolves without changing them. Turn it off with Focus on Enemy, as the turn assist does.

#### 8. Let the log see the moveset (S)

Name charged blows, heavy follow-ups, backstep attacks, finishers and abilities in `CombatTally`, and add the distance to the target
on whiffs. Without this, the next playtest can't show whether items 1, 2 and 7 worked.

#### 9. One prompt for the reeling moment (S)

A creature already shows that it is reeling (it sways, embers glow, its poise bar pulses), and a heavy on it floors it for 1.8 s.
That is the best payoff in melee, and the logs suggest it is rarely found. The first few times in a save that a focused creature
reels, show the heavy's button glyph for a moment beside it. That is a picture, not text (the no-text-walls rule), and the journal's
Poise page opens with it.

---

## 4. What I'd leave alone

- **Hit stop and camera shake:** capped since 11-19 and in line with fighting-game practice.
- **Telegraphs, the opening ring, interrupts:** they work. The interrupt is the most-used technique, as the wolves' blue warning asks.
- **The parry window (0.2 s since the safety round):** parrying is meant to be one way in, not the whole fight. A low parry count is
  fine as long as the heavy and the reeling payoff (items 1 and 9) give the timing player something else to do.
- **Swing pace at 1x:** the smoothness is right. Change the numbers (item 1), not the pace.

---

## 5. Decisions for the user

1. **Skills:** cooldown only, with a full Focus circle empowering the next skill (recommended, item 4)? Or keep the Focus cost and see
   whether the payoff round fixes it first?
2. **Heavy and charged buff (item 1):** go ahead? It moves gear scores up for one-handers, and the tier tests will need new baselines.
3. **Wolves (item 2):** keep the 3.5 m break-away and only delay it (recommended), or also shorten it at tier I?

Items 5-9 need no decision, only a yes to build them.

---

## Sources

- [Attack token system: group combat that feels fair (StraySpark blog, on id's Doom AI)](https://www.strayspark.studio/blog/attack-token-system-ue5-group-combat-that-feels-fair)
- [GDC: the combat mechanics of Doom (80.lv)](https://80.lv/articles/gdc-the-combat-mechanics-of-doom)
- [Elden Ring: Stance (wiki.gg)](https://eldenring.wiki.gg/wiki/Stance)
- [Elden Ring: Stance (Fextralife)](https://eldenring.wiki.fextralife.com/Stance)
- [Elden Ring melee combat guide (Windows Central)](https://www.windowscentral.com/elden-ring-melee-combat-guide)
- [Batman: Arkham Asylum combat (StrategyWiki)](https://strategywiki.org/wiki/Batman:_Arkham_Asylum/Combat)
- [Batman Arkham combat design (Game Design Skills)](https://gamedesignskills.com/game-design/batman-arkham-combat/)
- [Evolving Combat in God of War for a New Perspective (GDC Vault)](https://gdcvault.com/play/1026423/Evolving-Combat-in-God-of)
- [V Rising dev update 29: Weapons of the Night (Stunlock)](https://blog.stunlock.com/dev-update-29-weapons-of-the-night/)
- [V Rising gameplay trailer shows off combat (Game Developer)](https://gamedeveloper.com/press-release/new-v-rising-gameplay-trailer-shows-off-combat)
- [Hades review (PC Gamer)](https://pcgamer.com/hades-review)
- [Hit stop frame trick (Game Juice)](https://gamejuice.pages.dev/articles/hit-stop-frame-trick)
- [Hitstop, hitfreeze, hitlag (Critical Points)](https://critpoints.net/2017/05/17/hitstophitfreezehitlaghitpausehitshit/)
- [No Rest for the Wicked: Early Access Patch 1 notes](https://forum.norestforthewicked.com/t/patch-notes-early-access-patch-1/9963)
