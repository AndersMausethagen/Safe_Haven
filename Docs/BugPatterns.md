# Bug patterns and the guards against them

Written 2026-11-16, after a day of systematic code reviews (commits 7122942c to cdbcf614: about 170 fixes across every
system). The user asked: is there a pattern, can systems be changed so it stops happening when content is added or
changed, and can co-op stay in sync without a check in everything that happens. This is the answer, and the plan built
from it. Status per guard is kept at the bottom.

## What the bugs had in common

Most were not one-off mistakes. A rule lived at each call site instead of in one place everything goes through, so every
new feature had to remember it, and the bugs are where one didn't.

| Pattern | Share of the day's fixes | Examples | Root cause |
|---|---|---|---|
| Not cleaned up when interrupted | ~38 | a creature staggered mid-hop stayed invulnerable; hand crafts and fishing not ending on death; "any resume turned every key back on"; skills cast while floored | each activity undoes its own flags on its happy path, and knows only the interruptions someone thought of |
| Saving gaps | ~35 | a horse's health share, the stable's clock, the held-quest share, quest rewards waiting for home, when the Haven's asks were posted | the save mappers are written by hand; nothing checks a new field is saved and read back |
| Co-op: whose world, who decides | ~28+ | a guest writing to their copy of the host's settlement; quests counting the host's Haven; a friend's poison ticking twice; combo blows replayed as the opener; the strike kind lost on the way to the host | a guest's game holds two Havens under one name (`GameSession.Settlement`), and a puppet creature runs the same simulation as the real one unless each system checks |
| One rule worked out in several places | ~22 | Stamina Cost -1 ("the default") read as the cost; arrows and blades disagreeing on neutral targets; staff and thrown weapons missing what a blade's blow adds | each path rebuilds the rule from raw data |
| Events counting the wrong thing | ~20 | a refund counted as a find (collect quests farmed); a refused job unlocking recipes; kill credit lost | events say what happened mechanically, and each listener guesses what it meant |

Counted from the bullet lines of the day's fix commits by keyword, with overlap: a rough shape, not exact numbers.

The fixes that have held are the ones turned into a test: `ConventionTests` (statics reset each Play session),
`ContentAudit`, `CoopWireTests`, and `QuestObjective.TypesWithACoopRule`, which fails until a new objective type says
whether it counts in a friend's Haven. That last one is the model for most of the guards below: **a choke point every
case goes through, and a test that fails until a new case declares itself.**

Measured in the code the same day: `GameSession.Settlement` used 243 times in 80 files, `HavenVisit.IsVisiting` checked
80 times in 43 files, `IsRemote` 40 times in 14 files, and 176 message fields written by hand in 12 files where
`CoopWireTests` can't see them. `SettlementState.Borrowed()` (31 setters) catches a write to the host's copy only at
runtime, only when co-op is played, and never a read (the quest bug of the day was a read).

## The guards

### 1. Saving: every field saved, or marked as not

A round-trip test over the saved runtime state (SettlementState, QuestLog, and each saved component's state): every
field gets a value that isn't its default, is saved and loaded, and must come back equal. A field that isn't saved on
purpose carries `[NotSaved("why")]`. A new field can no longer be forgotten in the mapper.

### 2. Co-op messages as types

Each co-op message becomes a struct (`INetworkSerializable`), which `CoopWireTests` fingerprints by its fields, so a
field added or dropped fails the test until the wire format is raised. A round-trip test writes and reads every message
struct and compares every field, so a field the serializer forgets fails at once (the strike kind and the guard outcome
were lost exactly that way). A convention test keeps hand-written `WriteValueSafe` to the serializers.

### 3. A declared co-op rule for each kind of thing

As `QuestObjective.TypesWithACoopRule`: each kind of interaction (what a visitor may use in a friend's Haven: anyone,
ask the host, the host only), each kind of game event (whom it counts for), and each elite trick, relic effect and
status (simulated by the host, or by this game) declares its rule, and a test fails until a new kind does. The check is
made once at the choke point (`Interactable`, the quest log, the creature's host) instead of in each feature.

### 4. No placeholder values in public data; shown numbers checked against the rule

A field where -1 means "use the default" is read only through the rule that resolves it (Attack Definition › Stamina
Cost through `PlayerCombat.StaminaCostOf`). A convention test bans the raw reads outside their owner. Numbers the player
is shown (the Fight card, a telegraph's colour, the journal's reasons) get tests against the rule they describe.

### 5. The host's creatures: one way in, and puppets that only show

Everything gameplay does to a creature (damage, stagger, a status, a pull, finishing it) reaches the host through the
same few events (`Damageable.AnyDamaged`, `StatusEffects.Applied`, the stagger notice), carrying the whole blow
(`DamageInfo` through one serializer). On a friend's copy the creature's own simulation is off by construction
(statuses don't tick, poise isn't worn, attacks aren't chosen): a component that simulates says so once, and the puppet
switches it off when it is made, instead of each system checking `IsRemote`. Parity tests apply the same blow, status
and stagger directly and through the co-op path, and compare.

### 6. One interrupt for the player, enter and exit for monster states

Death, a stagger or knockdown, a scene change, pause, a cinematic, a window opening, mounting and a weapon swap raise one
player interrupt. Every activity (a swing, a finishing blow, an interaction, fishing, taming, crafting by hand,
building) registers and ends on it unless it says otherwise. A new activity is cleaned up by default. A monster's states
have enter and exit steps, so what a state gives (a hop's invulnerability) is taken back however the state ends.

### 7. One pipeline for the player's blows

Melee, arrows, staff casts, thrown weapons and Silent Takedown's estimate build their hits through one place: the
swing's techniques, a sure critical (a counter, a sneak attack), Ambush Damage, the mounted bonus. A path can't forget
one.

### 8. Events say why

A game event carries why it happened (found, crafted, rewarded, refunded, moved, loaded) and, in co-op, whose it is.
Quests and unlocks count by a declared rule instead of each listener guessing; a refund can't be counted as a find.

### 9. Two Havens, two names

`GameSession.Settlement` is split into `Here` (the Haven the player stands in; in a friend's, the host's copy, read
only) and `Home` (the player's own, always theirs to change). The old name is marked obsolete so the compiler lists
every use to move; a convention test bans changing `Here`. Writing to the host's world, or reading it for one's own
quests, stops being possible by accident.

## Status

| Guard | Status |
|---|---|
| 1. Save round-trip | built: `SaveCoverageTests` (every field of SettlementState with its residents and fallen, QuestLog, CharacterProgress with its journal; every ISaveable on the prefabs, its JSON probed and read back), `[NotSaved("why")]`. Found on building it: a purification batch's remainder was never saved |
| 2. Messages as types | built (format 28): every co-op message a struct written whole (CreatureSpawn, Poses, Strike, Hit, Stagger, Status, horses, drops, lands, lessons, pings, raid news, visitor rights, the Haven share...); `CoopWireTests` sees their fields; `CoopMessageTests` round-trips all 43 structs and forbids field-by-field writes. The bytes on the wire are as before |
| 3. Declared co-op rules | built: every component answering an Interactable says what a visitor may do in a friend's Haven (`[InAFriendsHaven(HavenUse.Anyone/AsksTheHost/ItsChoicesDecide/HostOnly/NotInAHaven, "how")]`, 40 of them), HostOnly refused once in `Interactable.CanInteract` (none is yet: each host's choice still refuses itself); every elite trick says what it does on a friend's copy; quest objectives (`TypesWithACoopRule`) and arrival causes already declare theirs; statuses are guard 5's. `InteractionRuleTests`. Relic effects reach creatures through guard 5's ways in, so they need no rule of their own |
| 4. Placeholder bans, shown numbers | built: Attack Definition's six "below 0: the default" values are nullable (`OwnStaminaCost`, `OwnWindupMove`...), the sky-occlusion renderer too; `ConventionTests.APlaceholderValue_IsNeverHandedOutAsANumber`; the telegraph's colour reads the AI's own rule (`MonsterAI.AnyBlowBreaksStrike`), the Fight card and Frenzied the swing's real cost (`PlayerCombat.StaminaCostOf`, `LastSwingStaminaPaid`) |
| 5. Creatures: one way in, puppets that only show | built: every component a creature carries says what it does on a friend's copy (`[OnAFriendsCopy(CopyRule.Same/Forwarded/HostOnly/FollowsTheHost, "how")]`, 25 of them); a HostOnly part is an `ICopyAware`, told once by MonsterAI.MakeRemote/MakeLocal (StatusEffects, RisesAgain); a friend's hit goes whole through `CreatureHitMessage.Of` and `Blow`; `CopyRuleTests` (declared, told, the whole blow, the same damage here and through a friend, no ticks on a copy). Found on building it: a Draug's copy held its own floor, so a friend's fire could finish the copy while the host's creature lived |
| 6. Player interrupt, monster state exits | built: `PlayerInterrupts` (death, a flinch, a pause, a window, a weapon swap) and `IPlayerActivity` (EndsOn, Interrupt): building, fishing, riding, interacting, being aboard, crafting by hand and a skill's blow on its way are told there, death ends every one; `PlayerInterruptsTests` (the rule, and no activity listening itself). A monster's states give back what they gave in `MonsterAI.SetState` (a hop's invulnerability, a stagger's damage taken, a read parry, circling) |
| 7. One blow pipeline | built: `PlayerBlows` (FromTheShadows: sure critical and Ambush Damage; CatchesUnaware; UnawareBlowWorth) used by blades, arrows, casts, thrown weapons, Silent Takedown and the focus ring's estimate (which disagreed before); `ConventionTests.BlowsFromTheShadows_AreWorkedOutInPlayerBlows`. High ground stays a projectile's own |
| 8. Events say why | built: `ArrivalCause` (found, crafted, caught, rewarded, bought, filled, refunded, moved) on `GameEvent.Cause`, set by every give that says why (crafting, fishing, water, quest and ask rewards); `ArrivalCauses.IsNewlyHad` is the declared rule collect and gather quests count by; `QuestCoopTests.EveryArrivalCause_SaysWhetherItIsNewlyHad_AndARefundNeverCounts`. Whose it is stays the event's Actor |
| 9. Here and Home | built: `GameSession.Here` (the Haven one stands in, to read) and `GameSession.Home` (one's own, to change; in a friend's Haven the guest's own, alive for the visit, `HavenVisit.OwnHaven`, saved with them); `GameSession.Settlement` obsolete and gone from the code (233 reads to Here, every change to Home, after an agent classified each change: none reachable in a guest's game changes the host's world); the guest's special cases (an extraction, a rescue, a boss, a spirit's gift, Liv's work, the Huldra) are one `Home` call each; `ConventionTests.TheHavenHere_IsRead_AndOnesOwn_IsChanged`. Found on building it: a guest talking to the host's residents used up their raid memories on the copy, and the host's carver lent a guest her reach at the host's altar (both silent: resident records cry out at nothing); and the Haven rank and job quest steps, the Huldra's welcome and "already rescued" read the host's Haven on a friend's raid |

## Second round (the user: "add all recommended fixes")

What the nine guards still left open, found while building them.

| Fix | Status |
|---|---|
| 1. Resident records cry out on a borrowed copy | built: `ResidentRecord`'s setters go through `SettlementState.RecordChanging`, which says so as `Borrowed()` does (the two co-op bugs of guard 9 were silent writes to records); `CoopHomeTests.ABorrowedResident_SaysSoWhenChanged` |
| 2. `Here` read-only by type | built: `GameSession.Here` is an `IHavenView` (what can be read of a Haven), so a change through it doesn't compile; one's own is changed through `Home`. The settlement object itself (`GameSession.HereItself`) is held only by the co-op mirror, saving, what this game sees standing (buildings, scaffolds, a station's queue, footpaths, the territory's state) and a test, and `ConventionTests.TheHavenHereItself_IsHeldOnlyWhereItMustBe` names each with its reason. Still open by type: `Territory`, `Workshops` and resident records handed out through the view can be changed; `RecordChanging` and `TerritoryState.Borrowed` say so at run time |
| 3. Visitor rules per menu choice | built: `MenuAction.HostOnly`, declared where a choice is made; every window greys it for a visitor and says whose it is through `Enabled` and `Detail` (a station's craft, collect and cancel, the trading post's trades, the horse trader's, waking the portal); the choice's own check stays behind it. `InteractionRuleTests` |
| 4. Explicit ids for content | built: `ContentAudit.ImplicitIds` (in the build's audits) and `ContentAuditTests.EveryDefinition_HasAnIdOfItsOwn`; the 19 that went by their name (18 gathering nodes and the StoneSilenced event) were given the id they already used |
| 5. The quest arrow's one-per-kind, Blood-Price's cost | built: the waypoint arrow leads to the quests the tracker shows first (`QuestTrackerView.Shown`); Blood-Price takes its price exactly (`Damageable.PayExactly`: armour, the Damage Taken assist and a dodge made it less or nothing, the Focus given back all the same); `DamageRulesTests` |
| 6. Old saves as test fixtures | built: three real saves from 11-12, 11-14 and 11-16 (no personal data in them) in `Editor/Tests/SaveFixtures`; `SaveFixtureTests`: each loads with all its content found, keeps its residents, quests, raids and bag through a save made from it, and saving again changes nothing |
| 7. A scripted run through the game's loop (Play mode) | built: `GameLoopLiveTests`: a new game in the Haven, into the Misty Woods, creatures killed, extracted, the results screen's Continue pressed, home; the raid counts at home and in the save made there, nothing threw, and the player's own saves are untouched (a folder of the test's own). About 2.5 minutes |
| 8. A two-player test with two built copies | planned |
| 9. One command for the whole suite | built: `RunTests.ps1 -All` (one run, totals and failures); `AgentScripts/BuildWindows.ps1` runs it and builds only when everything passes |
| 10. A leak detector in every build | built: `LeaseWatch` (in `PlaytestLogger`): invulnerability, keys held off, speed changed or a health floor held far too long or by something gone is a warning and a "Stuck" row in the playtest log; `LeaseWatchTests` |

