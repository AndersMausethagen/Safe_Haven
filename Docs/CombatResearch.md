# Safe Haven combat research: why it feels basic, and what to change

Research date: 2026-11-15. This was read-only: no project files were edited, and no Unity CLI or Play mode was used.
Paths are relative to `d:\Safe Haven\Safe Haven` unless they are absolute. `file:line` points at the current `test` branch.

---

## 0. Summary

### What the code shows

Combat is not missing systems. It already has:

- a light chain with rhythm
- a hold-to-heavy attack with a charge
- run and roll attacks (called "rush")
- a weapon art paid with Focus
- weight-based dodges (quickstep, roll, shoulder barge) and dodging round an enemy to its back
- parry, perfect dodge, backstab, and enemies that learn to answer backstabs
- poise, finishing blows, hit stop, camera shake, rumble, blood and gore

Many of these are the No Rest for the Wicked (NRftW) features the user asked for on 2026-11-02 (`Docs/Combat.md`). The "too basic" feeling comes from five concrete things:

1. **The player is rooted for the whole swing.**
   - `windupMoveSpeed`, `swingMoveSpeed` and `heavyMoveSpeed` are all **0** in `Resources/CombatTechniqueSettings.asset:23-25`.
   - Commit 69504d8c (2026-11-01, "planted feet during swings, no post-swing glide") set them to 0. Before that they were 0.4 and 0.15.
   - So from the press until `RecoveryTime` the player cannot move, and facing is locked after the aim snap and the tracking.
   - The next chain swing only starts at `RecoverAt` (`PlayerCombat.Swings.cs:257-266`).
   - Mashing queues the next swing at any moment (`PlayerCombat.Input.cs:172-189`, an unbounded one-press buffer), so three presses commit the player to 2.3 s of standing still (sword) or 2.4 s for two swings (greatsword).
   - The code to swing on the arms over walking legs (`Attack Arms` layer, `PlayerAnimationController.Combat.cs:124-163`) and to walk out of a follow-through (`WalkOutOfSwing`, `:73-100`) already exists. The data switches it off.
   - The GDD (§10.3) and `Docs/Combat.md` ("you move slowly while swinging") still describe the walk-out.
2. **Parry is far better than anything else, so waiting for the parry is the right play.** A 0.15 s parry, cued by a glint (`AttackTelegraph.cs:135-150`), gives:
   - no damage
   - a full **1.3 s stagger on any monster, bosses included**. There is no per-monster resistance: `PlayerGuard.cs:210-221`, `MonsterAI.Reactions.cs:14-51`.
   - a guaranteed critical counter (×1.3)
   - **35 Focus**, more than one weapon art (25–40)
   - 20 stamina back (`PlayerStaminaSettings.asset:23`)
   - a staggered backstab on top (×1.75, sure crit)

   A whole sword chain earns 24 Focus in 2.3 s if every swing lands.
3. **Attacking into an enemy is punished from both sides.**
   - Only **12 of 84** monster attack entries are `interruptible` (`MonsterAttacks.cs:306-315`).
   - Monster poise is high: a Draug has 140, but a sword chain does 45 poise, so about 7 s of nonstop hitting.
   - Poise comes back after 2 s without a hit (`PoiseMeter.cs`).
   - Any hit worth 5% of the player's health or more cancels the player's wind-up (`PlayerCombat.Reactions.cs:22-64`).
   - So "spam and die" is accurate: monsters trade through the player's swings, and the player's swings don't trade through theirs.
4. **Few distinct animations, shared across families.**
   - About 28 attack states serve 9 weapon families (`CombatMovesets.cs:21-39`).
   - The greatsword, greataxe and hammer each have a **two-swing** chain built from the same four `2H …` clips in different orders (`CombatMovesets.cs:91-101`).
   - The 1H axe and the dagger share `1H Slash` and `1H Stab`.
   - Spear swings 1 and 2 are the **same** Kevin Iglesias clip (`SpearThrust` and `Spear Thrust B` both play `Attack Polearm`).
   - Every weapon in a family has an identical moveset. The seven abilities (Wolf's Lunge and the others) replay the old stance attack (`PlayerAnimationController.Combat.cs:216-223`).
5. **Movement inside attacks is small and late.**
   - The step into the blow runs only in the last 0.12 s, a fixed constant (`PlayerCombat.cs:78`, `Swings.cs:309-317`).
   - It covers 0.45–0.6 m for chain swings.
   - Clips are imported in place, and the player applies no root motion (`CombatAnimationSetup.cs:113-123`).
   - The player reads as a turret that leans, not a fighter who steps through a combo.

### Recommendations, ranked by fun per effort

Each item says whether it changes DPS.

1. **Unroot the player.** Data now, then a per-attack movement profile. DPS unchanged.
2. **Bound the input buffer and separate the chain point, the move point and the cancel point** in `AttackDefinition`, so mashing doesn't lock in a full chain and the player regains control before the next swing is allowed. DPS unchanged.
3. **Rebalance the parry/aggression economy.** Needs the user's approval: it is balance.
   - Parry gives less Focus and no full stagger on great beasts, elites and bosses (poise damage instead).
   - A whiffed parry has a cost.
   - Hits give more Focus.
   - More light monster attacks can be interrupted, and heavy or charged hits interrupt wind-ups.
4. **Context attacks from clips the project already owns:**
   - backstep attack
   - guard counter (attack right after a block)
   - partial vs full charged release
   - a shield moveset (`Combat_Defend_*`)
   - a heavy follow-up (the unused part of `Sword_Heavy_Combo`)
   - a throw art (`OverhandThrow`)

   Each is one clip plus one `AttackDefinition`, with numbers taken from an existing attack, so DPS is held.
5. **Per-weapon identity:** one signature mechanic and one moving art per family, plus 3–4 distinct chain swings per family. New clips: Kevin Iglesias Human Melee ($23, same character rig as the player body), Mixamo (free, Humanoid) and existing unused clips.
6. **Feel polish:**
   - hit stop per animator, freezing only the attacker and victim, with a victim shake (works in co-op, where global hit stop is off today: `CombatFeedback.cs:95`)
   - a spark on interrupt
   - hit reactions per stance
   - a stamina regen delay split between attacks and dodges

A phased plan is in §3.6. Constraints honoured: no DPS or cadence change unless the user asks, data-driven assets, keyboard layout unchanged, no removal of features, targeted edits only (the bulk builders are retired).

---

## Part 1: How combat works today

### 1.1 The attack pipeline, from press to damage

| Stage | Owner | Where | Notes |
|---|---|---|---|
| Input | `InputSystem_Actions.inputactions` → `PlayerStateMachine.AttackRequested/Released`, `ArrowCycleRequested` (art) | `Assets/_Project/Input/InputSystem_Actions.inputactions`; subscribed in `PlayerCombat.cs:313-333` | Keyboard and mouse: LMB attack, RMB guard, MMB art, Space dodge, Z swap. Pad: RT attack, LT guard, South dodge, R3 art, North = Skill1, d-pad = Skills 2–5. There is no on-foot sprint key (Left Shift is `HorseGallop` only). |
| Press to swing kind | `PlayerCombat.Input` | `PlayerCombat.Input.cs:10-57` | Guard held + attack = shield bash, or with no shield, the weapon art. A rush is chosen when `WantsRush` (`:71-84`): during a dodge or ≤0.3 s after one, or when running at ≥70% speed for ≥0.2 s within 70° of the aim. Otherwise a light swing starts on the press. |
| Hold to heavy | `UpdateHeldPress` | `PlayerCombat.Input.cs:115-151` | Held 0.22 s while the light swing still winds up: it becomes the heavy swing and pays the stamina difference. The heavy waits at the top of its wind-up and charges up to 0.9 s (+40% damage, +50% poise) (`Swings.cs:271-292`). |
| Buffer | `Request` | `PlayerCombat.Input.cs:172-189` | **Any** press while busy (swing, dodge, flinch, guard stun) is queued. One slot, newest wins, **no time window**. With `earlyPressFumbles` 0, an early press keeps the chain going. |
| Chain judge | `ComboRules` (plain, tested) | `ComboRules.cs:27-61` | Early (before the hit) = buffered, no rhythm. Rhythm = −0.16 s / +0.1 s around recovery (+5% per stack, at most 2). In time = up to 0.45 s after recovery. Late = restart. `rhythmSpeedPerStack` is 0, so swings never get faster. |
| Start a swing | `PlayerCombat.Begin` | `PlayerCombat.Swings.cs:10-166` | Picks the attack (chain step, heavy, art, rush, counter, finishing stab) and pays stamina. It **snaps facing to the aim assist** (25° cone, reach +0.6 m), then sets `FacingLocked = true` and `SetSpeedScale(WindupMoveSpeed)` (`:140-142`). A rush starts its lunge at once (`:147-151`). |
| Wind-up | `UpdateSwing` | `Swings.cs:219-267` | `TrackFocus` turns at 240°/s toward the focused enemy until the lunge (`:295-305`). The lunge (`StepIntoBlow`) runs in the last **0.12 s** (const `LungeSeconds`, `PlayerCombat.cs:78`), up to `attack.Lunge` metres, stopping 0.8 m short of the target (`Swings.cs:309-317`). |
| Animation | `PlayerAnimationController` (partial `.Combat`) | `PlayerAnimationController.Combat.cs:124-163` | Crossfades `AttackDefinition.AnimationState` on **Combat Full Body**. It uses **Attack Arms** only if `WindupMoveSpeed > 0` and the player is moving (`:128`), which is never the case today. Heavy swings play on Upper Body and switch to full body at the strike (`:35-49`). Speeds: build-up `AnimationSpeed`, then `StrikeSpeed` for the last `StrikeLead`, then `FollowThroughSpeed` (`:11-31`, `:166-172`). Clips are in place and there is no root motion (`CombatAnimationSetup.cs:113-123`). |
| Hit detection | `HitDetector.FindTargets` | `HitDetector.cs:32-78` | One overlap sphere at the hit moment, filtered by a cone (`Range`, `Arc`, `MaxTargets`). It is not driven by the animation, which makes it dependable and cheap. A whiff can glance off rock or wood (`HitMaterials.Glance`, `Swings.cs:332-335`). |
| Sweep order | `LandSwing` / `PendingHit` | `Swings.cs:319-385` | Targets are struck in the order the blade reaches them (`sweepSeconds` 0.09 s per half circle). Heavy and art swings also hit the ground (`GroundImpacts.PlayerSwing`). |
| Damage | `Strike` → `AttackResolver.Apply` | `Swings.cs:387-429` | Multipliers in order: backstab (×1.75 on a staggered enemy with a sure crit, ×1.35 mid-strike) → Melee skill → ambush → mounted → Silent Takedown. Then the resolver applies armour, resistances and blunt/slash/stab. |
| After the hit | `ReleaseFeet` | `Swings.cs:440-445` | Speed becomes `SwingMoveSpeed` or `HeavyMoveSpeed`, **both 0**, so `FacingLocked` stays true. Movement and facing come back only at `EndSwing` (`:447-465`), at `RecoveryTime`. |
| Next swing | `UpdateSwing` | `Swings.cs:257-266` | The queued swing begins exactly at `RecoverAt`. There is no separate earlier chain point or move point. |
| Cancels | `CanDodge`, `CanGuard`, `CancelRecovery` | `PlayerCombat.cs:248-251`, `Swings.cs:472-479`, `PlayerGuard.cs:100-107` | A dodge cancels anywhere: during the wind-up the swing is lost (`dodgeCancelsWindup` 1), and after the hit it cuts the follow-through. Raising the guard cancels the follow-through. A rush out of a dodge waits for the end of the i-frames, then cuts the dodge short. |

### 1.2 Why the player is "locked in place" (all causes)

1. `windupMoveSpeed: 0`, `swingMoveSpeed: 0`, `heavyMoveSpeed: 0` in `Resources/CombatTechniqueSettings.asset:23-25`, read at `Swings.cs:142` and `:440-445`.
   - Git: 77b07591 (2026-10-24) had `swingMoveSpeed 0.4`, `heavyMoveSpeed 0.15`.
   - 69504d8c (2026-11-01) set all three to 0 to stop foot glide.
   - The GDD §10.3 (`Docs/GameDesignDocument.md:480`) still says "once it has landed the player can walk out of the follow-through".
2. Facing is locked from the start of the swing to its end (`Swings.cs:141`, `:443`). Only the focus tracking turns the player, and only toward the focused enemy.
3. The chain's next swing waits for the full `RecoveryTime`. One field serves as the chain point, the move point and the end of the swing.
4. The buffer has no window, so mashing chains all three swings back to back with no control in between:
   - Sword: A lands at 0.28 s and ends at 0.46 s. B lands at 0.76 s and ends at 1.02 s. The finisher lands at 1.81 s and ends at **2.31 s**.
   - Greatsword, two swings: ends at 2.43 s.
   - The only way out is a dodge, which throws away a swing still in its wind-up.
5. The lunge is short and late: 0.6 m in the last 0.12 s for chain swings (`Lunge` field: Sword A 0.6 m, Finisher 0.6 m, Heavy 0.45 m), against 1.8–2.4 m for rushes. With in-place clips and no root motion, the body barely travels during a chain.
6. The player flinches (0.45 s at 25% speed) from any hit worth 5% of max health or more during the wind-up, and loses the swing. Only heavy swings of heavy weapons push through (`HeavySwingArmor` 0.3–0.4).

### 1.3 Movesets per weapon family (what the player actually sees)

Read from the `WeaponDefinition` assets in `Data/Combat/Weapons` and the `AttackDefinition` assets in `Data/Combat/Attacks/Combos`.

| Family (weapons) | Light chain (animator state, clip source) | Heavy | Rush (run or roll) | Counter | Art (Focus) |
|---|---|---|---|---|---|
| Sword (Practice, Worn, Copper, Iron, Grave, Moonsilver, Trollstone; also every shield and the torch as fallback) | Sword A / Sword B / Sword Finisher (Quaternius UAL2 `Sword_Regular_A/B/C`) | Sword Heavy (UAL2 `Sword_Heavy`, first 1.05 s of `Sword_Heavy_Combo`) | Sword Counter (UAL2 `Sword_Dash`) | Sword Counter | 1H Spin (Stylized `Combat_1H_AttackSpecial`), 30 |
| Dagger (Fang Dagger) | 1H Thrust / **1H Slash** / **1H Stab** | **1H Low Cut** | 1H Thrust | Sword Counter | 1H Spin, 25 |
| 1H axe and club (Copper Hand Axe, Woodsman's Axe, Knotted Club) | **1H Slash / 1H Stab** / 1H Chop | **1H Low Cut** | 1H Stab | Sword Counter | **1H Low Cut**, 30 |
| Greataxe (Stone Axe, Greataxe) | 2H Wide / 2H Cleave (**2 swings**) | 2H Overhead | 2H Sweep | none (next chain swing) | Whirlwind, 40 |
| Greatsword (Iron, Moonsilver) | 2H Sweep / 2H Wide (**2 swings**) | 2H Drive | 2H Drive | none | 2H Slam (leap), 35 |
| Hammer (Iron Warhammer, Trollstone Maul) | 2H Cleave / 2H Sweep (**2 swings**) | 2H Overhead | 2H Cleave | none | 2H Slam (leap), 40 |
| Spear (Spear, Copper, Fire-Hardened, Moonsilver) | **Attack Polearm / Attack Polearm** (same Kevin Iglesias clip twice) / Pole Lunge | Pole Rise | Pole Lunge | none | Pole Slam, 30 |
| Fists | Punch Jab / Punch Cross / Punch Hook | none | Punch Cross | none | none |
| Shield bash (attack while guarding) | Shield Bash (Kevin Iglesias `AttackShield01`) | n/a | n/a | n/a | n/a |

- Bold marks a clip reused by another family or another slot.
- Clip sharing is defined in `Scripts/Editor/Characters/CombatMovesets.cs:21-39` (state → clip) and `:76-123` (swings).
- Family assignment is at `:144-172`, and the base sword, fist, axe and spear specs at `CombatAnimationBuild.cs:442-464`.
- All weapons of a family are identical in moves. They differ only in damage, `HeavySwingArmor` and `StrikeOverride` (the club strikes blunt with axe moves).
- The finishing blow (`Finishing Stab`, state `1H Stab`) uses the rush's numbers.
- Skills (Wolf's Lunge, Whirling Steel, Trollsbane Wrath, Ashlad's Ember, Frost Rune, Heartroot's Grasp, Huldra's Blessing) call the stance's old `PlayAttack` trigger (`PlayerAnimationController.Combat.cs:216-223`).

**Timing and DPS invariant.** Recovery is when the next swing can start. The ratio sum is in units of weapon damage.

| Family | Swings: hit / recovery (s) | Chain time | Σ ratio | **Ratio per second** (keep this) | Example DPS factor |
|---|---|---|---|---|---|
| Sword | 0.28/0.46, 0.30/0.56, 0.79/1.29 | 2.32 | 3.7 | **1.60** | Iron Sword 20 → 31.9 |
| Dagger | 0.21/0.35, 0.22/0.42, 0.59/0.97 | 1.74 | 3.05 | **1.76** | Fang 11 → 19.3 |
| 1H axe/club | 0.28/0.46, 0.30/0.56, 0.58/1.29 | 2.32 | 3.7 | **1.60** | Copper Hand Axe 16 → 25.5 |
| Greatsword | 0.61/1.01, 0.64/1.42 | 2.43 | 2.6 | **1.07** | Iron Greatsword 30 → 32.1 |
| Greataxe | same as greatsword | 2.43 | 2.6 | **1.07** | Greataxe 34 → 36.4 (the TODO notes it beats the Iron Greatsword) |
| Hammer | same as greatsword | 2.43 | 2.6 | **1.07** | Iron Warhammer 26 → 27.8 |
| Spear | 0.44/0.79, 0.44/0.79, 0.41/0.86 | 2.45 | 3.5 | **1.43** | Spear 18 → 25.8 |

Raid tiers are tuned to these numbers (memory: *gear score and raid tiers*; *combat animation*: "New clips never change DPS", `AttackSpec.From`). Any new chain must keep its family's ratio per second, as Σ ratio over Σ recovery.

### 1.4 Defence, mobility and resources

| Mechanic | Numbers (asset) | Code |
|---|---|---|
| **Dodge** by equip load (light ≤6, heavy ≥13) | Base: 4 m, 0.5 s, 0.3 s i-frames, 0.9 s cooldown (`PlayerMovementSettings.asset:20-23`). Quickstep ×0.6 distance, 15 stamina. Roll 25 stamina. Heavy roll ×0.85 distance and 35 stamina, a barge that breaks poise. Clips: UAL2 `Slide_Start`, UAL1 `Roll`, UAL2 `Shield_Dash` (`CombatAnimationBuild.cs:398-402`). | `PlayerDodge.cs`. Dodging round a focused enemy curves to its back (`DodgeRules`, `dodgeAroundSpeed` 1.3, `dodgeAroundSeconds` 0.62). Dodges pass through bodies. |
| **Guard** | Covers a 140° arc. 35–40% gets through with a weapon, 8–22% with a shield, 70% unarmed. Costs 0.8 stamina per point of mitigated damage. A broken guard stuns for 1.1 s. 50% move speed while guarding. | `PlayerGuard.cs:94-117`, `:169-208`; `GuardRules` |
| **Parry** | A hit within 0.15 s of raising the guard (× the Timing assist): no damage, the attacker staggers 1.3 s, counter ready for 1 s (crit, ×1.3, poise ×2), +35 Focus, +20 stamina. The guard can't go up again for 0.3 s after lowering it. | `PlayerGuard.cs:210-232`; asset lines 76-89 |
| **Perfect dodge** | A hit during the i-frames readies a counter and gives +25 Focus. | `PlayerCombat.Reactions.cs:102-114` |
| **Backstab** | A 130° wedge behind the enemy. ×1.75 with a sure crit on a staggered enemy, ×1.35 mid-strike (only for `OpenWhileStriking`, never elites). Refunds half the dodge's stamina within 1.5 s. Enemies answer (strike or hop) and learn: +35% read chance per backstab, at most 90%, forgetting one every 20 s. | `Swings.cs:400-407`, `:431-436`; `MonsterAI.Backstabs.cs` |
| **Finishing blow** | Behind a staggered person-shaped creature (`canBeFinished`: Draug, Shieldbearer). Locks the two together and uses the rush's numbers. | `PlayerCombat.Finishers.cs` |
| **Stamina** | 100 max. Regen 26/s, starting 0.9 s after **any** spend. Light swing 12, heavy ×2 unless set per attack, dodge 25. | `PlayerStaminaSettings.asset:15-23` |
| **Focus** | 6 per target hit (2 count at most). Finisher ×2, heavy ×1.5, counter ×1.5. Parry 35, perfect dodge 25, block 2. Holds 10 s, then drains 4/s. Arts cost 25–40 Focus, or 30 stamina when short. | `CombatTechniqueSettings.asset:85-92`; `PlayerFocus.cs` |
| **Player flinch** | A hit ≥5% of max health: 0.45 s reel at 25% speed, the wind-up is lost. 1 s grace before another flinch. A hit ≥20% gives 0.3 s of invulnerability. These last two are the anti-overwhelm rules of 2026-11-14. | `PlayerCombat.Reactions.cs:22-64` |
| **Monster poise** | Per definition: Rootling 10, default 30, Draug 140, Shieldbearer 110, Undead Knights 220, Stone Troll 220, Heartroot 320, Old Moss Troll and Berg-jutul 450. Recovers 15/s after 2 s without a hit. At 0: 1.3 s stagger, +20% damage taken. | `PoiseMeter.cs`; `MonsterAI.Reactions.cs:110-114` |
| **Monster interrupts** | Only attacks with `interruptible` ticked are cancelled by **any** damaging hit during their wind-up: 12 of 84 entries in `Data`. | `MonsterAttacks.cs:306-315`; `MonsterAI.Reactions.cs:97-101` |
| **Hit stop** | `Time.timeScale` drops to 0.05 for 0.07 s (hit), 0.10 (crit) or 0.13 (kill), × weight (backstab/art ×1.75, heavy/counter ×1.5, finisher ×1.25) × `Impact`. **Global**: everything freezes, and it is **off when hosting co-op** (`GamePause.TimeIsShared`). | `CombatFeedback.cs:88-105`, `:239-248`; `CombatFeel.asset:15-27` |
| **Shake, rumble, flash** | Shake 0.15 / 0.3 / 0.45 (hurt 0.55), Perlin-style with roll. The controller rumbles from the shake (`ControllerRumble`). Backstab flash and slow time. Parry hitch: time scale 0.4 for 0.09 s. | `CombatFeedback.cs`; memory *controller support* |
| **Enemy telegraph** | The ground fill lasts exactly the attack's `HitDelay`. It turns the parry colour and the attacker **glints for the parry window**. Unblockable attacks show in their own colour. | `AttackTelegraph.cs:135-170` |
| **Anti-overwhelm** | At most 2 attackers closing in, 0.35 s apart; the rest circle at 3.5 m for 0.8–1.6 s. At most 2 ranged attackers. Off-screen melee waits 0.6 s. Flinch grace as above. | `CombatTechniqueSettings.cs:127-146`; `AttackTurns` |

### 1.5 Enemy side, as it shapes player combat

- **Readable but uninterruptible.**
  - Monster strikes have long telegraphs: Draug cleave 0.75 s, lunge 1.0 s; troll sweep 1.0 s; stomp 1.3 s.
  - They have follow-ups and held wind-ups (`followUps`, `holdChance`) that defeat reflex rolls.
  - Most can't be interrupted by damage, so the player's answer is defensive by construction.
- **Monster hit reactions are mostly procedural:** `MonsterPose.Hit` tip and squash, and `HitReaction`. Humanoid monsters also play one `Hit` state (Stylized `Combat_1H_Hit` at ×1.7) on blows ≥4% of their health (`MonsterAnimationDriver.cs:23`, `:173`). There is no directional hit reaction, knockback clip or knockdown.
- **Anti-backstab learning and answers** (2026-11-02) mean circling for backstabs stops paying on repeat. Again the parry, which always staggers, is the sure route.
- **Monster roles** (2026-11-14) add lobs, hazards, unblockable pulls, calls and decoys. These are good for variety, and they push the player toward reading, not toward attacking.

### 1.6 Skills and proficiencies that touch combat

- **Melee skill** (`Docs/Skills.md`, memory *skills*). Level 10 is the old tuning; levels below are worse. It scales `MeleeDamage`, `MeleeStaminaCost` and `BlockStaminaCost`. Raid tiers expect Melee 10/20/30/40.
- **Rule from the user:** never nerf movement, dodge or parry windows, or attack cadence.
- **Perks:**
  - Riposte (parry readies a counter)
  - Second Wind (stamina on a melee kill)
  - Silent Takedown (Sneaking)
  - flourish at Melee 30: 1H arts ×damage, 2H arts ×poise (`Swings.cs:86-98`)
- **Finding: Riposte is a dead perk.** `PlayerGuard.cs:223-226` already grants a counter on every parry, so `PlayerTechniques.cs:53-60` only refreshes it and shows a banner.
- **Abilities** (`SkillDefinition`, keys 1–9 / pad North and d-pad) cost Focus on top of their cooldown (Wolf's Lunge 25, Whirling Steel 40, Trollsbane Wrath 60). They have no animations of their own.

### 1.7 Animation sources the project already owns

**Player body.** Kevin Iglesias' human (`Docs/Combat.md` › Readable animation). Every library below is Humanoid and retargets onto it. Quaternius libraries need their import fix: Bake Axis Conversion, root node, avatar after (`CombatAnimationSetup`).

**Unused attack-like or combat clips, by pack:**

| Pack (path) | Unused clips worth pressing into service | Possible use |
|---|---|---|
| Stylized Character base pack (`Assets/StylizedCharacter/Animations/Character/Human/Male/A_Hu_M_BasePack.fbx`, female twin `…/Female/A_Hu_F_BasePack.fbx`) | `Combat_Defend_Attack`, `Combat_Defend_Attack01`, `Combat_Defend_AttackSpecial`, `Combat_Defend_Ready`, `Combat_Defend_Dodge`, `Combat_Defend_Hit`, `Combat_Defend_Spell` | **A sword-and-board moveset:** chain, shield art and stance dodge when a shield is in the off hand. |
| same | `Combat_1H_Attack_Off`, `Combat_1H_Attack_Off02` | Off-hand flourishes: a 4th dagger or 1H step, a dual-wield later. |
| same | `Combat_2HL_Attack_Base`, `Combat_2HL_Attack_Base01` (rejected because they lead with the butt end) | A **spear butt-strike**: backstep attack, guard counter, or a shove that makes space. Or a quarterstaff family. |
| same | `Combat_Unarmed_Attack01` (monsters use `…_Attack` as Strike Claw) | Fists variety. |
| same | `Combat_1H_Dodge`, `Combat_2H_Dodge`, `Combat_2HL_Dodge`, `Combat_Unarmed_Dodge`, `Combat_Bow_Dodge`, `Combat_Cast_Dodge` | Stance-specific **backstep** (start of the backstep attack) and a dodge per weapon weight. |
| same | `Combat_1H_Hit`, `Combat_2H_Hit`, `Combat_2HL_Hit`, `Combat_Unarmed_Hit`, `Combat_Knockdown`, `Combat_Stun` | Player hit reactions per stance. A knockdown for huge blows. A stun for a guard break. |
| same | `Combat_1H_Ready`, `Combat_2H_Ready`, `Combat_2HL_Ready` | Ready stances in combat. |
| same | `Sheath_Back`, `Sheath_Hip` | Animated weapon swap (Z). |
| same | `Combat_Cast_Attack`, `Combat_Cast_Attack01`, `Combat_Cast_Attack02`, `Spell_Staff_*`, `Spell_Unarmed_*` | Ability animations (today they reuse swings). The GDD §10.4 runic staff family. |
| same | `Combat_Bow_Attack`, `Combat_Bow_Attack01`, `Combat_Bow_AttackSpecial` | Bow arts and a melee bow bash. |
| Quaternius UAL2 (`Assets/Quaternius/Universal Animation Library 2/UAL2_Standard.fbx`) | Rest of `Sword_Heavy_Combo` (only 0–1.05 s is cut as `Sword_Heavy`) | A **second and third heavy**: a heavy chain, or a charged release variant. |
| same | `OverhandThrow` | **Throw art** (spear, hand axe), throwing weapon. |
| same | `NinjaJump_Start`, `NinjaJump_Land` | Leap or plunge art, a jump-attack start. |
| same | `Shield_OneShot`, `Sword_Block`, `Idle_Shield_Break`, `Hit_Knockback` (only Knockback is used) | Shield bash variant, guard-counter wind-up, guard-break reaction. |
| same | `Zombie_Scratch`, `Melee_Hook_Rec` | Unarmed and monster extras. |
| Quaternius UAL1 (`…/Universal Animation Library/UAL1_Standard.fbx`) | `Sword_Attack`, `Hit_Chest`, `Hit_Head`, `Spell_Simple_Shoot`, `Push_Loop` | Extra 1H swing, directional hit reactions, cast, shove. |
| Kevin Iglesias free packs (`Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat`) | `Attack1H01_L/_R`, `Attack2H01`, `AttackPolearm01`, `AttackShield01` (used by the legacy states), `CombatDamage01` | Same rig as the body, so they are the cleanest retarget. Also the `Sprint01_*` movement clips (no on-foot sprint exists today). |
| Malbers Horse AnimSet Pro (`Assets/Malbers Animations`) | `Rider_*` (mounted) | Only mounted combat. Not relevant on foot. |

**Free trick: animator Mirror.** A Humanoid state's *Mirror* flag gives a mirrored version of any clip at no cost.
- For **two-handed** weapons, a mirrored forehand sweep becomes a backhand. `TwoHandGrip` re-solves the left hand onto the handle.
- For one-handed weapons it moves the swing to the wrong hand, so not usable there.
- Check the blade at contact, per the memory rule.

### 1.8 Constraints from the project and the memory notes (respect these)

- **DPS and cadence are fixed** unless the user asks. New clips take numbers from existing attacks (`AttackSpec.From`); keep "ratio per second" (§1.3). Never nerf movement, dodge or parry windows, or attack cadence (*skills*).
- **The builders are retired** (`Scripts/Editor/Core/RetiredBuilders.cs:12-26`; "Set Up Combat Animations" refuses at `CombatAnimationBuild.cs:37`). New states and attacks are targeted Editor edits with a before/after diff. Blend trees need a diff inside them.
- **Judge a clip at contact, from the side, in Play mode.** Stylized overheads reach peak hand speed on the upswing (`DrivenContact`, `BladeLag` 0.09 s). Leaping clips are for arts only (`CombatMovesetTests` refuses a heavy that leaps).
- **Keyboard and mouse bindings must stay identical** (*controller support*). New verbs should come from context (hold, movement state, timing) or from pad buttons that are free. The pad has no free face button in Player (West = Interact, North = Skill1). Prefer context.
- **Never remove functionality** (*never remove functionality*). Parry, backstab learning and the like are tuned, not removed.
- **Feel numbers are the user's call.** Nothing was played by hand after the 2026-11-02 rework.
- Telemetry (`PlaytestLogger`) records no combat-technique rows (parries, dodges, hits, flinches). Of 1,200 logs, the combat-related "Custom" rows are tutorial lessons only.

---

## Part 2: How other games make combat exciting (web research)

A research subagent ran the searches, and the claims below carry its URLs. **(unverified)** marks claims from a single secondary or community source. Several primary pages (IGN, Edge, GamesRadar and PC Gamer interviews, poe2wiki, Hades fandom) were blocked, so some quotes come through secondary write-ups.

### 2.1 Key takeaways
- NRftW is deliberate because attacks commit and cost stamina. It avoids feeling "stuck" because **the state you attack from changes the attack**:
  - run attack, roll attack, charged attack, plunge attack, backstab
  - rune moves (many are movement) that cost **Focus, not stamina**
  - weight-based dodge types
  - patch after patch of cancel and queue fixes
- **Its patches move steadily toward responsiveness:**
  - "change targets during attack windups" was added
  - the quickstep queue window was shrunk
  - stamina pauses briefly after a dodge
  - "cancel the recovery animation upon a successful parry" was added
  - attack-queue timing was changed
  - hit reactions now scale with poise damage
- **Every game studied breaks the parry-wait loop the same way:** offence feeds a resource or meter (Focus, Magick, Regain, Guard Regain, posture, stance, Combo, gun ammo), and defence is *one* way to fill it, not the only payoff.

### 2.2 No Rest for the Wicked (Moon Studios)

**Status.** Early Access since April 2024. Updates:
- The Crucible (July 2024)
- The Breach (2025)
- Breach Refined Patch 1 (June 2025)
- Together (January 2026)
- Together Patch 1 (March 2026)

Version 1.0 is planned for March 2027, with an open beta in December 2026. Sources:
- https://forum.norestforthewicked.com/t/patch-notes-the-crucible/13051
- https://forum.norestforthewicked.com/t/patch-notes-the-breach-refined-patch-1/19212
- https://www.gematsu.com/2025/12/no-rest-for-the-wicked-early-access-together-update-launches-january-22-2026-open-beta-test-now-available
- https://forum.norestforthewicked.com/t/patch-notes-together-patch-1/25662
- https://www.pushsquare.com/news/2026/10/1-0-roughly-doubles-the-size-of-the-game-no-rest-for-the-wicked-confirms-march-2027-release-on-ps5

**Intent.**
- "We start the player out with pretty simple movesets", which grow so you "combine various combos, special attacks, and spells", and "your stamina meter makes sure your every move counts": https://blog.playstation.com/2024/03/01/no-rest-for-the-wicked-revealing-new-details-on-combat-crafting-town-building/
- Mahler wanted it "super visceral", where "animations really matter". Runes give the player the boss-style moves that Souls games keep for bosses, the top-down camera keeps big animations readable, and fighting games were an influence: https://www.gamesradar.com/no-rest-for-the-wicked-dev-explains-that-its-arpg-is-and-isnt-like-dark-souls-by-giving-players-the-crazy-moves-usually-reserved-for-soulslike-bosses **(quotes via secondary summaries)**

**Weapons and movesets.**
- One-handed classes: axes, bows, clubs, curved swords, daggers, hammers, katanas, maces, rapiers, spears, straight swords, wands.
- Two-handed classes: curved greatswords, double daggers, greataxes, great clubs, great hammers, greatswords, halberds, scythes, staves.
- Source: https://norestforthewicked.wiki.fextralife.com/Weapons
- Combat is "animation-driven", with movesets per weapon: https://norestforthewicked.wiki.fextralife.com/Combat
- Patch notes confirm per-class movesets:
  - "Filleter now attacks more like a normal sword, rather than a rapier": https://forum.norestforthewicked.com/t/patch-notes-the-breach-refined-patch-1/19212
  - "Updated hit reactions for all humanoids based on weapon class/archetype": https://forum.norestforthewicked.com/t/patch-notes-the-breach/15046
  - "Some bows will have different run attacks & roll attacks": https://forum.norestforthewicked.com/t/no-rest-for-the-wicked-together-update-patch-notes-sneak-peak/21726
- Version 1.0 makes **weapons the classes**: 24 classes, where a spear makes you a Dragoon and a dagger an Assassin. Fighting earns Insight for traits and runes: https://wccftech.com/no-rest-for-the-wicked-1-0-scraps-attributes-24-classes-stealth-swappable-serum-cores/
- **(unverified)** Combo strings per weapon (hit counts, timings) are not published.

**Attack types.**
- Attack, or hold for a "Powerful Charge": https://www.gamepressure.com/no-rest-for-the-wicked/controls/z7113aa
- Run attacks. Together let them come out from standing ("hold A while not moving"). Some run attacks have i-frames, and their stamina cost was raised for it:
  - https://forum.norestforthewicked.com/t/no-rest-for-the-wicked-together-update-patch-notes-sneak-peak/21726
  - https://forum.norestforthewicked.com/t/patch-notes-together-patch-1/25662
- Roll attacks per weapon, and plunge attacks that "now deal Knockdown damage": https://forum.norestforthewicked.com/t/patch-notes-the-breach-refined-patch-1/19212
- **Backstabs.** Elites and Bruisers can be backstabbed only when poise-broken. Every humanoid archetype gained anti-backstab behaviour, and backstab position "demands greater precision":
  - https://forum.norestforthewicked.com/t/no-rest-for-the-wicked-together-update-patch-notes-sneak-peak/21726
  - https://forum.norestforthewicked.com/t/patch-notes-the-breach/15046

**Runes and Focus.** Runes are weapon skills slotted per weapon type; all weapons have 4:
- https://norestforthewicked.wiki.fextralife.com/Runes
- https://forum.norestforthewicked.com/t/patch-notes-the-breach/15046

Many runes are **movement**:
- Piercing Dash (100 Focus)
- Blink (25, 4 m through enemies)
- Leap into a block-breaking slam (100)
- Spin (100)
- Air Dodge with a counter stab (100)
- Dropkick (30 stamina)
- Fire Nova ("charge doubles power")

Source: https://norestforthewicked.wiki.fextralife.com/Runes

Focus comes from attacking, parrying and consumables, and runes "do not consume stamina": https://norestforthewicked.wiki.fextralife.com/Combat, https://norestforthewicked.wiki.fextralife.com/Focus

Together's Focus tuning:
- **Parry gives 50 Focus, down from 100.**
- A small-shield block gives 25, up from 10.
- Armour gives a Focus-gain bonus at the cost of stamina regen.
- Source: https://forum.norestforthewicked.com/t/no-rest-for-the-wicked-together-update-patch-notes-sneak-peak/21726

Breach rune changes: runes add **poise defence while used** (light super armour), and throws cost 50 Focus: https://forum.norestforthewicked.com/t/patch-notes-the-breach/15046

**Stamina.**
- Attacks, parries, blocks and dodges cost stamina. The advice is one or two combos, then keep the rest for dodges: https://norestforthewicked.wiki.fextralife.com/Combat
- Base regen went from 30 to 35: https://forum.norestforthewicked.com/t/no-rest-for-the-wicked-together-update-patch-notes-sneak-peak/21726
- The Breach: "Blocking no longer costs stamina", and shields deal poise damage on block. Light shields allow running, medium shields do not, great shields prevent moving: https://forum.norestforthewicked.com/t/patch-notes-the-breach/15046
- Breach Refined: "Stamina is frozen for a short window upon quick stepping/rolling": https://forum.norestforthewicked.com/t/patch-notes-the-breach-refined-patch-1/19212
- Weapon parries cost stamina (Hotfix 3): https://forum.norestforthewicked.com/t/patch-notes-early-access-hotfix-3/5257

**Dodge and weight.**
- Fast quickstep, normal roll, and a heavy slow roll plus a **Shoulder Barge**: https://www.gamepressure.com/no-rest-for-the-wicked/how-does-weight-class-work/zd113b6, https://norestforthewicked.wiki.fextralife.com/Weight+Class
- The Breach gave the quickstep more i-frames than the medium roll, took away the medium roll's full-animation i-frames, and removed the slowdown after chained quicksteps: https://forum.norestforthewicked.com/t/patch-notes-the-breach/15046
- In 1.0 the quickstep becomes a trait: https://wccftech.com/no-rest-for-the-wicked-1-0-scraps-attributes-24-classes-stealth-swappable-serum-cores/

**Parry.**
- "Parry punishment for missing a parry and subsequently receiving a hit", and "**Bosses are no longer fully staggered from parry**": https://forum.norestforthewicked.com/t/no-rest-for-the-wicked-together-update-patch-notes-sneak-peak/21726
- Together Patch 1: "You can now cancel the recovery animation upon a successful parry": https://forum.norestforthewicked.com/t/patch-notes-together-patch-1/25662
- A parry impact sound was added: https://forum.norestforthewicked.com/t/patch-notes-the-breach-refined-patch-1/19212

**Poise and interrupts.**
- The Breach: hitting an enemy "mid-attack will be more effective at interrupting them", staggered enemies take no poise damage, and the player gets a visible stagger bar: https://forum.norestforthewicked.com/t/patch-notes-the-breach/15046
- The Crucible: a "Spark VFX whenever enemy gets interrupted": https://forum.norestforthewicked.com/t/patch-notes-the-crucible/13051
- 1.0: "hit reactions now depend on how much poise damage an attack deals": https://wccftech.com/no-rest-for-the-wicked-1-0-scraps-attributes-24-classes-stealth-swappable-serum-cores/

**Input and targeting patch trail:**

| Date | Change | Source |
|---|---|---|
| Jul 2024 | "Improved input responsiveness"; input priority for runes | https://forum.norestforthewicked.com/t/patch-notes-the-crucible/13051 |
| 2025 | Lock-on more forgiving; fixed attack spam keeping damage the same | https://forum.norestforthewicked.com/t/patch-notes-the-breach/15046 |
| Jun 2025 | "**Added ability to change targets during attack windups**"; "Fixed the Quickstep queue window being too large"; ledge protection | https://forum.norestforthewicked.com/t/patch-notes-the-breach-refined-patch-1/19212 |
| Jan 2026 | "The timing of when attacks are queued up has been slightly changed" | https://forum.norestforthewicked.com/t/no-rest-for-the-wicked-together-update-patch-notes-sneak-peak/21726 |
| Mar 2026 | Parry recovery can be cancelled; rune input no longer overlaps run | https://forum.norestforthewicked.com/t/patch-notes-together-patch-1/25662 |

Players asked for "an input buffer of about 150-250ms" after inputs dropped following rolls: https://forum.norestforthewicked.com/t/patched-inputs-being-deleted-and-not-queued-sometimes/11683

**Community criticism** (this matches the user's complaint):
- "Once you commit to an attack, you're locked in". Players asked for a "small cancellable window mid-animation".
- Heavy weapons' recovery "makes it very risky to land two hits in a row".
- Enemies "track the player with robotic precision".
- Source: https://forum.norestforthewicked.com/t/high-priority-issues-that-hurt-the-game-right-now/16491
- Parry is "way too random" from an isometric camera. Players proposed three tiers: block, a forgiving parry, a tight riposte: https://forum.norestforthewicked.com/t/no-rest-for-the-wicked-feedback-and-analysis/12375

**Synthesis.** NRftW is deliberate but not stuck in place because:
1. Attacks come from movement states.
2. Its strongest moves use a second resource earned by offence, and many of them move the player.
3. Commitment is *managed* rather than removed: retargeting during the windup, a tuned queue, a stamina freeze after a dodge, a recovery cancel after a successful parry, poise defence while a rune runs.
4. Parrying is a choice, not the whole loop: it gives some Focus, a miss is punished, and bosses aren't fully staggered.

### 2.3 Hades and Hades II
- Each weapon has four verbs: Attack, Special, Cast, Dash. A Dash-Strike comes out of the dash. Stygian Blade: chain 20/25/30, dash-strike 30, Nova Smash 50 AoE: https://hades.fandom.com/wiki/Stygian_Blade
- Hades II: holding Attack, Special or Cast charges an **Omega** version that spends Magick; each weapon has its own: https://hades2.wiki.fextralife.com/Combat, https://gameleap.com/articles/hades-2-what-are-omega-moves
- Aspects change behaviour: https://maxroll.gg/hades-2/guides/hades-2-weapon-and-aspect-overview
- The dash can't be chained into an immediate second dash: https://aftermath.site/supergiant-hades-2-dash-chronos-difficulty
- Elites have an armour bar and can't be stunned or knocked back until it breaks: https://primagames.com/gaming/how-to-break-enemy-armor-in-hades-2
- **Takeaway:** movement is part of the moveset, the dash is defence and an attack starter, and every button has a charged version.

### 2.4 Diablo IV
- Evade has a cooldown of about 5 s per charge, up to 3 charges: https://primagames.com/tips/how-to-dodge-and-evade-in-diablo-4. The Season 8 cooldown changes: https://www.icy-veins.com/d4/news/diablo-4-season-8-official-preview-belial-returns-boss-powers-endgame-boss-rework-and-much-more/
- Basic skills generate resource and Core skills spend it: https://www.ggrecon.com/guides/diablo-4-classes-full-breakdown/
- Bosses can't be crowd-controlled directly. CC fills a **stagger meter**, and when it is full all CC lands at once. Monster CC on players was cut to an average of 1.5 s (2 s at most): https://www.purediablo.com/diablo4/Crowd_control
- **Takeaway:** crowd-control damage turns into a guaranteed punish window on enemies that can't be stunned.

### 2.5 Path of Exile 2
- The dodge roll has **no cooldown**. It cancels almost any animation, and a cancelled skill costs nothing. Its early frames avoid strikes:
  - https://poe2wiki.net/wiki/Dodge_roll (via search summary)
  - https://www.sportskeeda.com/mmo/path-exile-2-poe2-dodge-roll-system-iframes
- The roll lets skills have long animations while the player keeps an escape:
  - https://www.pcgamer.com/path-of-exile-2-is-building-on-a-decade-of-action-rpg-depth-but-its-the-no-cooldown-dodge-roll-im-really-psyched-about (via summary)
- There is an intended lockout after cancelling a skill, which closed animation-cancel exploits:
  - https://maxroll.gg/poe2/news/patch-0-2-0h-removes-animation-cancels-for-rake-and-whirling-slash
  - https://www.pathofexile.com/forum/view-thread/3783274/filter-account-type/staff
- You can move while using skills, at a movement-speed penalty:
  - https://mobalytics.gg/poe-2/guides/mechanics
  - https://poe2wiki.net/wiki/Running_Assault
  - https://game8.co/games/Path-of-Exile-2/archives/490777 (base penalty **unverified**)
- **Heavy stun** builds up. Above a threshold the enemy is "Primed", and a Crushing Blow stuns it instantly:
  - https://poe2db.tw/us/Heavy_Stun
  - https://www.poe2wiki.net/wiki/Primed
- Mace skills only finish primed targets (Boneshatter): https://maxroll.gg/poe2/build-guides/boneshatter-titan-leveling-guide
- A **Combo** counter is spent by quarterstaff skills:
  - https://www.poe2wiki.net/wiki/Combo
  - https://www.poe2wiki.net/wiki/Tempest_Bell
- A buckler parry applies "Parried" (more damage taken), which a spear Disengage then consumes:
  - https://poe2db.tw/us/Parried
  - https://mobalytics.gg/poe-2/guides/parry-release-review
- **Takeaway:** build then spend (prime then crush, combo then spender, parry then consume) turns defence into setup for offence.

### 2.6 Death's Door and Tunic
- **Death's Door:**
  - Aims for "the fast arcadey flow of A Link to the Past", not stamina management: https://www.nme.com/features/acid-nerve-developers-david-fenn-and-mark-foster-on-making-deaths-door-3203321
  - Melee hits recharge magic, so ranged play can't be passive **(review-level)**: https://www.nme.com/reviews/deaths-door-review-2998793
  - A charged heavy does 3× damage over a wide area **(unverified)**: https://speedrun.com/deaths_door/guides/xscbx
  - Weapons trade speed for damage **(guide-level)**: https://www.gfinityesports.com/deaths-door/deaths-door-all-weapon-locations/
- **Tunic:**
  - Lock-on in the style of Ocarina of Time: https://wireframe.raspberrypi.com/articles/wireframe-cover-star-tunic-and-the-art-of-keeping-a-secret
  - Blocking uses stamina: https://www.ludo.guide/guide/tunic/getting-started/game-mechanics-overview/stamina-management

### 2.7 Elden Ring and Dark Souls (weapon identity)
- **Moveset grammar:**
  - R1 chain of 3–5 hits by weapon type
  - R2 of 1–2 hits, charged for more damage and stance damage
  - running, jumping and crouching attacks
  - a backstep that "changes your next attack"
  - **guard counter** (R2 right after blocking, high stance damage)
  - stance break leading to a critical hit
  - power stance when dual-wielding the same weapon type
  - Ashes of War
  - Source: https://www.bandainamcoent.com/news/elden-ring-introduction-part-4-combat-guide
- **Feint:** a charged R2 cancelled into a backstep: https://eldenring.wiki.fextralife.com/Combat
- **Load classes:** light below 30%, medium below 70%, heavy below 100%. i-frames 13/13/12; heavy has twice the recovery and slower stamina: https://eldenring.wiki.fextralife.com/Equip+Load
- **Stance:** guard counters do about a charged heavy's worth of stance damage: https://eldenring.wiki.fextralife.com/Stance
- **Takeaway:** a small animation set multiplied by the state before the attack gives every weapon about 10 distinct attacks.

### 2.8 Others
- **Sekiro:** blocking drains *your* posture and deflecting damages *theirs*. Enemy posture regenerates if left alone: https://news.xbox.com/en-us/2019/03/19/sekiro-shadows-die-twice-interview/. The deflect window is deliberately tight: https://gamingbolt.com/?p=382583
- **Bloodborne Regain:** strike back soon after taking damage to regain health:
  - https://blog.eu.playstation.com/2014/08/12/bloodborne-gameplay-trailer-unveiled-gamescom-2014/
  - https://www.gamespot.com/articles/ps4-exclusive-bloodborne-features-health-regenerat/1100-6421712/
- **Lies of P:**
  - Guard Regain is won back by attacking.
  - Perfect Guard builds Groggy, and a charged attack on a Groggy enemy opens a Fatal Attack.
  - Fable Arts refill by attacking.
  - Weapons are a blade plus a handle.
  - Sources: https://liesofp.wiki.fextralife.com/Combat, https://pcgamer.com/lies-of-p-weapon-locations-crafting
- **Thymesia:** two health bars (HP and Wounds), so the player must alternate weapons: https://cogconnected.com/feature/thymesia-discussing-the-art-of-combat/
- **Hyper Light Drifter:** gun ammo refills only from sword hits: https://hyperlightdrifter.fandom.com/wiki/Abilities_and_Upgrades
- **Monster Hunter Wilds:**
  - Hitstop was lowered in the beta, then restored after veterans objected: https://www.pcgamer.com/games/rpg/monster-hunter-wilds-game-director-says-the-betas-missing-hitstop-has-already-been-tweaked-thanks-to-very-positive-feedback-from-hitstop-hungry-veteran-hunters
  - The Long Sword's Spirit Gauge is spent on a counter: https://www.icy-veins.com/monster-hunter-wilds/long-sword-guide
- **Last Epoch Rive:** the third strike is the heavy one **(forum-level)**: https://forum.lastepoch.com/t/rive-3rd-strike-or-no/77257

### 2.9 Game-feel techniques with numbers

| Technique | Values and notes | Source |
|---|---|---|
| Hitstop, SFV | 8 / 12 / 15 frames for light / medium / heavy (about 133 / 200 / 250 ms) | https://threadreaderapp.com/scrolly/1385104062157254660 |
| Hitstop, Smash Ultimate | Scales with damage, at most 30 frames | https://ssbwiki.com/Hitlag |
| Hitstop, God of War (2018) | "Pops the target to the hit pose and holds both Kratos and the target in that first frame". Camera shake reduced to keep targets readable. Low impact sound ending in a high slash. | https://blog.playstation.com/?p=370399 |
| Sakurai on hitstop | Attacker *and* victim freeze. The victim shakes side to side on the ground and up and down in the air, the shake shrinks, and the victim blends into the hurt pose over 4 frames. | https://sourcegaming.info/2015/11/11/thoughts-on-hitstop-sakurais-famitsu-column-vol-490-1/ |
| Buffer | Smash Ultimate: 9 frames (about 150 ms), with priority ordering | https://www.ssbwiki.com/Buffer |
| Cancel design | Pre-hit vs post-hit cancels. God of War lets basics cancel pre-hit into roll or guard; specials are post-hit only. | https://www.gamedeveloper.com/pc/combat-canceled-i-god-of-war-i-action-game-design |
| Responsiveness | Tag interrupt windows; newest buffered input wins; accept slightly late inputs; root motion only as limits | https://sites.google.com/view/realtime-animation-tips/tutorials/animation-tactile-response |
| Soft-lock and lunge | Turn toward the nearest target in the stick direction (Arkham freeflow). God of War's "suck to target". | https://critpoints.net/2015/05/24/what-is-your-ideal-form-of-lock-on-in-action-games/, https://www.siliconera.com/the-improvements-to-batman-arkham-citys-combat-system/, https://gdcvault.com/play/1026423/Evolving-Combat-in-God-of |
| Unity `Animator.MatchTarget` | Base layer only, one match at a time, needs root motion | https://docs.unity.com/en-us/engine/current/script-reference/unityengine/animator/matchtarget |
| Screenshake | Vlambeer's about 30 juice tweaks. Eiserloh's trauma model: trauma 0–1, shake = trauma² with Perlin noise, includes rotation. | https://gamedeveloper.com/design/vlambeer-co-founder-shares-advice-on-building-better-action-games, https://gdcvault.com/play/1023146/contactUs |
| Juice | "Juice It or Lose It" (GDC Europe 2012) | https://gdcvault.com/play/1016487/Juice-It-or-Lose |

### 2.10 Animation sources (checked)
- **Kevin Iglesias Human Melee Animations.** Paid, $23 on the Unity Asset Store; tags: sword, mace, polearm, warhammer. Per-clip contents are not listed: check the store video. There is a free version. It is the **same character family as Safe Haven's player body**.
  - https://assetstore.unity.com/packages/3d/animations/human-melee-animations-151650
  - https://kevdev.itch.io/human-melee-animations
- **Mixamo.** Free with an Adobe ID. Characters and animations may be used "royalty free for … commercial … projects including creating video games". The raw files can't be redistributed as assets.
  - https://helpx.adobe.com/be_en/creative-cloud/faq/mixamo-faq.html
  - https://community.adobe.com/t5/mixamo-discussions/mixamo-faq-licensing-royalties-ownership-eula-and-tos/td-p/13234775
- **Kubold Longsword Animset Pro** ($50, 205 mocap clips, Humanoid) and **Spear and Halberd Animset** ($49.99, 176 clips). They are realistic mocap and may clash with the stylized look.
  - https://marketplace.unity.com/packages/3d/animations/longsword-animset-pro-92239
  - https://assetstore.unity.com/packages/3d/animations/spear-and-halberd-animset-168429
  - https://www.kubold.com/blog/2020/5/27/spear-and-halberd-animset-is-live-on-unity-asset-store
- **Others seen, publishers not confirmed:** "Sword & Shield AnimSet" ($24.99), "Massive GreatSword AnimSet" ($69.99).
  - https://assetstore.unity.com/packages/3d/animations/animset-sword-shield-207043
  - https://assetstore.unity.com/packages/3d/animations/massive-greatsword-animset-251438
- **HY-Motion 1.0 (Tencent).** Open-weight text-to-motion, released 2025-12-30. Output is SMPL; community tools export FBX. Its licence is a Tencent community licence (a 100M-MAU carve-out is reported, and one mirror says MIT).
  - **Check the territory clause.** Other Tencent Hunyuan licences exclude the EU, UK and South Korea. This is the same kind of concern as the MiniMax H3 memory note. **(unverified)**
  - https://arxiv.org/abs/2512.23464v1
  - https://cgworld.jp/flashnews/01-202601-HY-Motion.html
  - https://github.com/luffy-yu/HMC

---

## Part 3: Recommendations

### 3.1 Diagnosis in numbers: why the two loops happen

**"Spam and die."**
- Three presses commit the player to 2.3 s rooted (sword) or 2.4 s (two-handers).
- A Draug cleave telegraph is 0.75 s, and the attack can't be interrupted by damage.
- The player's wind-up is lost to any hit worth ≥5% of max health.
- The only exit, a dodge, wastes the swing in its wind-up.
- So mashing is legal but always loses.

**"Attack, wait, parry, repeat."** Compare what each choice is worth against a Draug (poise 140, iron sword):

| Choice | Time | Damage taken risk | Focus | Opening |
|---|---|---|---|---|
| Full light chain, all landing | 2.3 s rooted | High: you can't interrupt it, it can interrupt you | 24 | 45 poise; poise refills after 2 s without a hit, so no stagger |
| Charged heavy | about 1.5 s | Medium (light weapons don't push through) | 9 | 60 poise, no stagger |
| **Parry** | 0.15 s window, glint-cued | None | **35** (+20 stamina) | **Instant 1.3 s stagger**, crit counter, staggered backstab (×1.75, the best blow in the game) |

Measured damage order from memory (Draug, iron sword): plain 19, mid-strike backstab 26, perfect-dodge counter 44, staggered backstab 50. The parry route is 2.6× a plain hit, risk-free, and also refills Focus.

### 3.2 Comparison: what Safe Haven already has vs NRftW

| NRftW feature | Safe Haven today | Gap |
|---|---|---|
| Light / hold-charge heavy | Yes (hold 0.22 s, charge 0.9 s) | Charge release has only one outcome. No partial-charge variant and no feint. |
| Run attack / roll attack | Yes, as the single "rush" per family | Same clip as a chain swing in most families. No *roll*-specific attack distinct from the run attack. |
| Runes (4 slots, movement moves, Focus) | One art per **family**, fixed. Two leaps; the rest are spins and cuts. | No choice, little movement, no slotting. |
| Focus from offence, parry 50 | Focus from offence, but **parry 35 ≈ 6 hits** | Parry is still the best Focus source per second. |
| Weight-based dodge with barge | Yes (quickstep, roll, heavy barge) | Good. |
| Parry with miss penalty; bosses not fully staggered | No miss penalty beyond the 0.3 s relock; **everything** fully staggered | Main loop driver. |
| Backstab only when poise-broken on elites; enemies react | Yes, plus learning | Good, maybe too strong against the dodge-behind route (by design). |
| Hit mid-attack interrupts better | Only 12/84 attacks are interruptible | Trading is a losing game everywhere. |
| Hit reactions scale with poise damage | One hit state above 4% of health, plus procedural recoil | No light/heavy/knockdown tiers. |
| Change targets during windup | Focus tracking at 240°/s | Good. |
| Recovery cancellable on a successful parry | Guard cancels the follow-through; the parry itself has no recovery | OK. |
| Moving between swings | **No: 0 move speed until recovery** | Main "locked in" driver. |

### 3.3 Ranked recommendations (impact on fun vs effort)

Effort: S = data only or a few lines; M = a small feature, 1–3 days; L = a week or more, or new animation work. "DPS" says whether damage per second changes.

| # | Recommendation | Impact | Effort | DPS |
|---|---|---|---|---|
| 1 | **Unroot the player.** Now: `swingMoveSpeed` 0.5, `heavyMoveSpeed` 0.2, `windupMoveSpeed` 0.25. Then a per-attack profile (see 3.4.1). | Very high | S, then M | Unchanged |
| 2 | **Bounded buffer and split windows.** Buffer only presses in the last about 0.25 s before the chain point (newest wins); earlier presses are dropped, or held as "hold to continue". Add `AttackDefinition` fields: `ChainAt` (= `RecoveryTime`), `MoveAt` (contact + about 0.1 s), `TurnAt`. | Very high | M | Unchanged (`ChainAt` = `RecoveryTime`) |
| 3 | **Parry economy** (user approval): Focus on parry 35 → 15–20; Focus per hit 6 → 8; per-monster parry response (a full stagger for ordinary monsters, poise damage only for great beasts, elites and bosses); a whiffed-parry penalty (relock 0.3 → 0.6 s when no hit came, or a stamina cost). | Very high | S–M | Unchanged for hits; parry payoff smaller on bosses |
| 4 | **Let aggression interrupt.** Tick `interruptible` on light monster strikes (bites, claws, shoves). Add a rule: a hit during a monster's wind-up whose poise damage ≥ X% of its max interrupts even non-interruptible attacks, so heavies, charged swings, counters and finishers do it. Show NRftW's "spark on interrupt". | High | S–M | Unchanged |
| 5 | **Context attacks from owned clips:** backstep attack, guard counter, partial vs full charge release, heavy follow-up, shield moveset, throw art. | High | M each | Held (numbers via `From`) |
| 6 | **Distinct chains per family** (3–4 swings for one-handers, 3 for two-handers) from Kevin Iglesias Human Melee, Mixamo and unused clips. Fix spear swings 1 and 2 being the same clip. | High | L | Held (keep ratio per second) |
| 7 | **One signature mechanic and one moving art per family** (see 3.5). | High | M–L | Arts are Focus-gated |
| 8 | **Animator hit stop with victim shake** (`Animator.speed` 0 on attacker and victim), scaled by weight, working in co-op; hit reactions per stance (light flinch / heavy stagger / knockdown). | Medium–High | M | Unchanged |
| 9 | **Stamina regen delay split:** 0.5 s after attacks, 0.9 s after dodges and blocks (NRftW freezes stamina after dodges). | Medium | S | Sustain up slightly |
| 10 | **Combat telemetry rows** (parry, perfect dodge, hits landed, flinches, guard breaks, arts, rush and heavy use per raid) to measure the loop before and after. | Medium (decisions) | S | n/a |
| 11 | **Fix the Riposte perk** (it duplicates base behaviour). Ideas: the counter after a parry becomes the weapon's rush-lunge riposte, the parry recovery is cancellable into any attack, or the counter window is longer. | Low–Medium | S | Small |
| 12 | **Ability animations** (Cast/Spell clips, and arts for Whirling Steel and the others) instead of `PlayAttack`. | Medium | M | Unchanged |
| 13 | **Teach the verbs.** The rush, heavy and art are invisible to many players. Spread the combat lessons (TODO line 134), and show the art's Focus ready state on the weapon. | Medium | S–M | n/a |

### 3.4 Details for the top items

#### 3.4.1 Unrooting without foot glide
- **Why it was zeroed.** Foot glide: legs ran under a whole-body swing (commit 69504d8c). The fix is already half built:
  - With `windupMoveSpeed > 0` and moving faster than 0.5 m/s, `OnSwingStarted` plays the swing on the **Attack Arms** (upper-body) layer over the locomotion legs (`PlayerAnimationController.Combat.cs:124-163`).
  - Standing still keeps the full-body swing.
  - After contact, `WalkOutOfSwing` moves a full-body swing onto the arms layer when the player walks off (`:73-100`).
- **Step 1, data.** Restore `swingMoveSpeed` to about 0.5 and `heavyMoveSpeed` to about 0.2, and try `windupMoveSpeed` at 0.25. Judge the look from the top-down camera and in over-the-shoulder.
- **Step 2, code (M).** Move these into `AttackDefinition` as an optional "Movement" header so they live with each attack:
  - `windupMove`
  - `afterContactMove`
  - `turnRate` while winding up (free turning toward the stick or mouse, not only toward the focused enemy)
  - `canStrafe`

  Then:
  - Daggers and fists keep about 60% speed while swinging.
  - Swords 30%.
  - Spears 30%, and a thrust while backpedalling keeps distance.
  - Two-handers 0–10% in the wind-up, 25% after contact.
  - Heavies stay planted (the planted heavy layer logic stays).

  Fallback: the settings asset.
- **Step 3, longer chain lunges.** The lunge window (`LungeSeconds` 0.12) and distance should become per attack: `LungeStart`, `LungeDistance`, and a curve. Chain swings should step about 0.8–1.2 m over the last third of the wind-up, and finishers 1.5 m. Keep `LungeStandOff` so the player doesn't clip into targets. This is the "suck to target" of God of War, Arkham and NRftW.
- **Facing.** Allow a turn of about 90–180°/s toward the *input* direction in the first part of the wind-up (NRftW: "change targets during attack windups"), not only toward the focused enemy.

#### 3.4.2 Buffer and windows
- **Today.** Any press during a swing queues the next one (`PlayerCombat.Input.cs:179-186`), and it fires at `RecoverAt`.
- **Proposal:**
  - `BufferWindow` (about 0.25 s, settings): accept a press only within that window before the swing's chain point, or during a dodge's last 0.25 s. Earlier presses are ignored. Optionally, holding attack through the chain point continues the chain ("hold to keep swinging"), like NRftW's queue and Hades.
  - `ChainAt` = `RecoveryTime` keeps cadence and DPS.
  - `MoveAt`: a few frames after contact the player regains walking and turning. The swing's animation finishes on the arms. Walking does not bring the next swing sooner, so DPS is held.
  - Dodge and guard cancels stay as they are. Add a short **re-attack lockout** after a dodge-cancelled wind-up (PoE2's lockout) so dodge-cancel spam can't fish for i-frames.
- **Tests.** `ComboRulesTests` already exist; add the buffer window there. The judge stays plain C#.

#### 3.4.3 Parry and aggression economy (needs the user's sign-off: balance)
- **Data:**
  - `focusOnParry` 35 → about 18
  - `focusPerHit` 6 → about 8
  - perfect dodge 25 → about 18

  Arts then cost about 4 landed hits, or one parry plus two hits. Aggression and parry combine instead of competing.
- **New `MonsterDefinition` field, "Parry Response":**
  - **Stagger** (today's behaviour, for ordinary creatures)
  - **Poise Damage N** (great beasts, elites, bosses: e.g. 35–50% of max poise, so two parries or a parry plus aggression break them)
  - **Recoil** (a short 0.4 s flinch)

  This mirrors NRftW's "Bosses are no longer fully staggered from parry".
- **Whiffed parry.** When the guard is raised and no hit arrives within the parry window, relock for 0.6 s instead of 0.3, or take a few stamina. This kills "guard-tap fishing" without narrowing the window (the user's rule: never nerf the parry window).
- **The counter stays.** Optionally let a successful parry cancel straight into the counter on the press (NRftW Together Patch 1). It does already, via guard-then-attack, which becomes the art. Note: attacking while guarding without a shield becomes the **art** (`PlayerCombat.Input.cs:25-32`). After a parry the player may want the plain counter instead; consider "attack within 0.4 s of a parry = counter, not art".

#### 3.4.4 Interrupts and enemy reactions
- **Data pass.** Tick `interruptible` on the light melee strikes:
  - wolf bites and hamstring
  - boar gore
  - claws
  - shoves
  - widow bite
  - Rootling grasp

  Not on trolls, knights or boss moves.
- **Rule (M), "Break through".** Inside a monster's wind-up, a hit with technique Heavy, Counter, Finisher or WeaponArt, *or* poise damage ≥ 25% of the monster's max poise, interrupts it even if not interruptible, unless the definition is a great beast or boss.
  - Spark and "clang" feedback (NRftW Crucible).
  - It gives heavy weapons their identity: "hit it as it winds up".
- **Hit reaction tiers** (NRftW 1.0, "reactions depend on poise damage"). Humanoids (Draug, Shieldbearer, Huldra, Root Guardian, trolls):
  - light flinch: the existing `Hit` state
  - heavy stagger: `Combat_2H_Hit` or UAL2 `Hit_Knockback`
  - knockdown: `Combat_Knockdown` on charged heavies or art kills

  Procedural creatures scale `MonsterPose.Hit` amplitude by poise damage.

### 3.5 Per-weapon identity: moveset, animations, timing, feel

All damage and cadence follow the family's "ratio per second" from §1.3. New steps take numbers through `From`. Arts are Focus-gated, so they may be strong.

| Family | Light chain (target) | Heavy / charge | Context attacks | Signature mechanic | Art (moving) | Feel: reach, stagger, sound/VFX, hit stop |
|---|---|---|---|---|---|---|
| **Sword** (flexible, the counter weapon) | 4 steps: A, B, a thrust, then the finisher. Mirror is not usable (1H). New: Kevin Iglesias melee or Mixamo "sword slash" clips, UAL1 `Sword_Attack`. | Heavy → heavy follow-up (`Sword_Heavy_Combo` part 2); partial release = quick heavy | Backstep thrust (`Combat_1H_Dodge` + `1H Thrust`); guard counter; run attack = Sword Dash | **Riposte flow:** after a parry or perfect dodge the next two swings are counters, not one, and the counter window is 1.5 s. Gives "parry into aggression" instead of "parry, wait". | **Dash strike** through the enemy (`Sword_Dash`, 3 m, ends behind for a backstab): the NRftW Piercing Dash | 2.2–2.5 m; light hit stop about 60 ms; bright blade-ring hit; gold ribbon on counters |
| **Dagger** (fast, positional) | 4 steps, very quick: Thrust / Off-hand slash (`Combat_1H_Attack_Off`) / Slash / Stab | Charge = "focused stab" with high crit | Attack while moving at about 70% speed; roll attack | **Bleed or poison stacks** (Fang already poisons); backstab ×2.0 instead of 1.75 (dagger only); a quickstep from a dagger stance costs less | **Shadow step:** blink behind the focused enemy (`DodgeRules.SpotBehind` exists) and stab. NRftW Blink. | 1.9 m; tiny hit stop about 35 ms; dry stab sounds; red mist on crits |
| **1H axe / club** (aggressive, breaks guards) | 3 steps (fix the shared clips): Slash / Chop / overhead chop (`1H Chop`), with the club blunt | Low cut (current) → charged overhead | Shield-breaker hit, a **throw** (`OverhandThrow`) | **Guard-breaker:** hits on a guarding or shielded target do ×2 poise (Barrow Shieldbearer counter-play) | **Hurl:** throw the axe or club, pull back on press (`OverhandThrow`) | 2.2 m; mid hit stop; wood or bone crunch; splinters on shields |
| **Greataxe** (momentum, crowds) | 3 steps: Wide / Cleave / **mirrored Wide** (backhand) | Overhead → charged spin | Run attack = Sweep | **Momentum:** each consecutive landed swing +1 (max 3) widens the arc (+20°) and adds poise; resets on whiff or pause | **Whirlwind** while moving (allow 40% movement during the existing Whirlwind) | 2.7 m; heavy hit stop about 110 ms; deep thud; dust per hit |
| **Greatsword** (stance and hyper-armour) | 3 steps: Sweep / Wide / **Drive** (thrust finisher) | Charged Drive with super armour (already `HeavySwingArmor` 0.3) | Feint: release attack + dodge during a charge = cancel into a backstep (Elden Ring feint) | **Stance:** holding guard and attack enters a low stance; the next swing is a charged "unsheathe" cut (NRftW and Elden Ring) | **Leap slam** (2H Slam, current) | 2.6–2.9 m; heavy hit stop; ringing steel; long ember ribbon |
| **Hammer** (poise crusher) | 3 steps: Cleave / Sweep / **Overhead** (moved from heavy) | Charged ground slam: crater (GroundImpacts exists) | Plunge-like slam after a dodge | **Crush:** primed targets (poise below 30%) take a "Crushing Blow" on a heavy that staggers instantly (PoE2 Primed and Crushing Blow) | **Quake leap** (current), knocking down small creatures | 2.6 m; biggest hit stop about 130–150 ms; low boom; camera shake; ground marks |
| **Spear** (reach, spacing) | 3 steps, all different: Thrust (KI) / **Pole Rise** low sweep / Lunge | Charged triple thrust | **Backpedal thrust** (attack while moving away); **butt strike** (`Combat_2HL_Attack_Base`) as the backstep attack to make space; run attack = Lunge | **Brace:** hold guard and attack against a charging monster (boar, troll rush) for a counter-impale with double poise | **Javelin throw** (`OverhandThrow`), or a **vault** over an enemy | 3.2–3.6 m; mid hit stop; wet pierce; the thrust pierces 2–3 in a line |
| **Fists** (brawler) | 4 steps: Jab / Cross / Hook / **Unarmed Attack01** | Charged haymaker | Shove (`Push_Loop` cut) | **Flurry:** rhythm stacks build faster (max 3), and each stack adds poise | Grab and throw a small creature (later) | 1.6 m; short punchy stop; thud |
| **Sword and shield** (when a shield is held) | Defend chain: `Combat_Defend_Attack` / `…Attack01` | Shield bash charge | **Guard counter** after any block (shield-and-board identity) | **Shield parry pays more poise:** a shield block deals poise to the attacker (NRftW Breach) | `Combat_Defend_AttackSpecial` | Clang on block; sparks |

**Where the animations come from, in the cheapest realistic order:**
1. **Owned clips, today:**
   - `Combat_Defend_*`
   - `Combat_1H_Attack_Off/Off02`
   - `Combat_2HL_Attack_Base*` (butt strikes)
   - `Combat_*_Dodge` (backsteps)
   - `Combat_*_Hit`, `Combat_Knockdown`, `Combat_Stun`
   - UAL2 `Sword_Heavy_Combo` (cut parts 2 and 3), `OverhandThrow`, `NinjaJump_*`, `Shield_OneShot`
   - UAL1 `Sword_Attack`, `Hit_Chest`, `Hit_Head`
   - Mirror on two-handed sweeps

   Each needs an animator state on Combat Full Body and Attack Arms (and Upper Body for heavies), an `AttackDefinition` copied from its `From` attack, a contact check at the frame of contact, and a `CombatMovesetTests` entry.
2. **Kevin Iglesias Human Melee Animations ($23).** Same character family as the player body, so retargeting is the cleanest. Check the clip list first (the store video).
3. **Mixamo (free, royalty-free in games).** There are many great sword, sword-and-shield and spear sets. Import as Humanoid and fix in-place and root. The style is mocap, which suits Safe Haven better than very stylized clips. Check the blade at contact.
4. **Kubold Longsword ($50) or Spear and Halberd ($49.99).** Big realistic mocap sets, if Mixamo's quality is not enough.
5. **Blender (MCP pipeline).** For *edits*, not new swings from scratch: cut, retime, add anticipation or overshoot, fix a foot, make a stance-specific version. The Unity-baked preview path (`ClipPreviewExport`, memory *clip preview*) helps here.
6. **Text-to-motion (HY-Motion 1.0).** Only after checking the licence's territory clause. Good for odd moves (a vault, a javelin throw), not for weapon contact precision.

### 3.6 Phased plan

**Phase 0: "Unlock" (2–3 days, no new animation, DPS unchanged)**
1. Telemetry rows for combat technique use (baseline numbers from the user's next playtest).
2. Restore the walk-out and arms-over-legs movement (data), then add the per-attack movement profile fields (code with data defaults). Check foot glide at both camera styles.
3. Bounded buffer, `MoveAt` and `TurnAt` windows, and the post-dodge re-attack lockout. Add the tests.
4. Per-attack lunge start, distance and curve. Bump chain lunges to about 0.8–1.2 m.
5. Stamina regen delay split by action.
6. **Ask the user:** the parry economy numbers, Parry Response per monster class, the whiff relock, interruptible ticks, the "break through" rule.

**Phase 1: "Context multiplies moves" (1–2 weeks, owned clips)**
1. Backstep attack, guard counter, partial vs full charged release.
2. Shield moveset (`Combat_Defend_*`).
3. Heavy follow-up from `Sword_Heavy_Combo`.
4. Throw art (`OverhandThrow`) for spears and hand axes.
5. Spear: different swings 1 and 2 (`Pole Rise` low sweep, butt strike).
6. Animator hit stop with victim shake. Hit reaction tiers (`Combat_*_Hit`, knockdown). Spark on interrupt.
7. Fix the Riposte perk. Ability animations from `Combat_Cast_*` and `Spell_*`.

**Phase 2: "Weapon identity" (2–4 weeks, new clips)**
1. Buy or download a clip set (Kevin Iglesias Human Melee first, then Mixamo).
2. Give every family a distinct 3–4 swing chain, keeping its ratio per second.
3. One signature mechanic per family (§3.5) as data on `WeaponDefinition`, e.g. `Signature` enum plus numbers.
4. Moving arts: dash strike, shadow step, hurl, leap slam, javelin. Optionally weapon-specific arts per weapon (not per family), so tier-up weapons feel new.

**Phase 3: "Depth" (later)**
1. Art slots: a rune system tied to Runecraft (NRftW's 4 runes), letting the player pick 1–2 arts per weapon.
2. Build and spend: a combo meter or priming (PoE2), Regain (Bloodborne, Lies of P), an optional two-hand or power-stance toggle.
3. A staff family (GDD §10.4 runic staff) from `Spell_Staff_*` and `Combat_Cast_*`.

### 3.7 How to verify each step
- **Every timing change:**
  - `ComboRulesTests` and the new buffer tests in EditMode via `RunTests.ps1` (memory: always use it).
  - `CombatMovesetTests` for states and shape.
  - A DPS check script that sums ratio over recovery per weapon, compared against the table in §1.3.
- **Every new clip:**
  - Capture at contact from the side in Play.
  - Check foot contact while moving (Attack Arms layer).
  - Check the two-hand grip on two-handed weapons.
- **Feel:**
  - The user plays a short scripted gauntlet (wolves, Draug, Shieldbearer, a troll) before and after each phase.
  - Telemetry compares parry share, hits per fight, deaths and time-to-kill.
  - Feel numbers stay the user's call.

---

## Appendix: key file references

| Topic | File:line |
|---|---|
| Move speeds = 0 | `Assets/_Project/Resources/CombatTechniqueSettings.asset:23-25` |
| Parry window, stagger, Focus | `CombatTechniqueSettings.asset:76, 82, 88-89, 100` |
| Lock on swing start | `Assets/_Project/Scripts/Player/PlayerCombat.Swings.cs:140-151` |
| Chain only at recovery | `PlayerCombat.Swings.cs:257-266` |
| Release feet after contact | `PlayerCombat.Swings.cs:440-445` |
| Lunge constant | `Assets/_Project/Scripts/Player/PlayerCombat.cs:78-80`; `Swings.cs:309-317` |
| Unbounded buffer | `Assets/_Project/Scripts/Player/PlayerCombat.Input.cs:172-189` |
| Rush choice | `PlayerCombat.Input.cs:71-84` |
| Hold to heavy | `PlayerCombat.Input.cs:115-151` |
| Chain judging | `Assets/_Project/Scripts/Combat/ComboRules.cs:27-61` |
| Player flinch | `Assets/_Project/Scripts/Player/PlayerCombat.Reactions.cs:22-64` |
| Parry effects | `Assets/_Project/Scripts/Player/PlayerGuard.cs:210-232` |
| Riposte duplicate | `Assets/_Project/Scripts/Progression/PlayerTechniques.cs:53-60` |
| Monster stagger, interrupts | `Assets/_Project/Scripts/AI/MonsterAI.Reactions.cs:14-51, 85-114`; `MonsterAttacks.cs:306-315` |
| Parry cue glint | `Assets/_Project/Scripts/AI/AttackTelegraph.cs:135-150` |
| Global hit stop, off in co-op | `Assets/_Project/Scripts/Combat/CombatFeedback.cs:88-105, 239-248` |
| Swing layer choice (Attack Arms gate) | `Assets/_Project/Scripts/Player/PlayerAnimationController.Combat.cs:124-163` |
| Walk out of swing (dormant) | `PlayerAnimationController.Combat.cs:73-100` |
| Skills reuse PlayAttack | `PlayerAnimationController.Combat.cs:216-223` |
| Clip → state map, movesets, families | `Assets/_Project/Scripts/Editor/Characters/CombatMovesets.cs:21-39, 76-123, 144-172` |
| Base sword, fist, axe, spear specs; dodge clips | `Assets/_Project/Scripts/Editor/Characters/CombatAnimationBuild.cs:398-402, 442-464` |
| Builder retired | `CombatAnimationBuild.cs:34-38`; `Assets/_Project/Scripts/Editor/Core/RetiredBuilders.cs:12-26` |
| In-place clips (no root motion) | `Assets/_Project/Scripts/Editor/Characters/CombatAnimationSetup.cs:113-123` |
| Weapon and attack data | `Assets/_Project/Scripts/Combat/WeaponDefinition.cs:36-56`; `AttackDefinition.cs:41-56, 76-89` |
| Hit detection | `Assets/_Project/Scripts/Combat/HitDetector.cs:32-78` |
| Dodge and stamina numbers | `Assets/_Project/Data/Player/PlayerMovementSettings.asset:15-61`; `PlayerStaminaSettings.asset:15-23` |
| Design docs | `Docs/Combat.md`; `Docs/GameDesignDocument.md:459-500` (§10.1–10.4) |
