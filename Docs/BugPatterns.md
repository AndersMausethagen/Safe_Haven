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
| 8. A two-player test with two built copies | built: `AgentScripts/CoopTwoCopies.ps1` starts the build twice on this machine (`CoopSmokeTest`, "-coopSmoke host|guest"; nothing happens without the switch). The host opens a new Haven with a lit stone on the direct port; the guest joins on 127.0.0.1 and checks what one Editor can't, as two games there share every static: the host's settlement came as a copy beside its own, its Pact Rune reaches the host, the save made while visiting is its own Haven, leaving takes it home. Saves in a temp folder, Steam and the playtest log off. Passed in 40 s (2026-11-16); close the Editor first: the two copies take about 8 GB |
| 9. One command for the whole suite | built: `RunTests.ps1 -All` (one run, totals and failures); `AgentScripts/BuildWindows.ps1` runs it and builds only when everything passes. Its first run found a test broken since a morning change that targeted runs had missed (RaidReasonsTests); 2038 tests in about 11 minutes |
| 10. A leak detector in every build | built: `LeaseWatch` (in `PlaytestLogger`): invulnerability, keys held off, speed changed or a health floor held far too long or by something gone is a warning and a "Stuck" row in the playtest log; `LeaseWatchTests` |


## Third round: the engine's own traps, by kind (2026-11-18)

The user asked for a systematic check for bugs across all code, looking for patterns, and for a better system that catches
and removes several bugs in one pass. The first two rounds were about this game's rules (who decides in co-op, what is
saved). This round is about what Unity and C# let through: mistakes a reader misses and the compiler accepts, which come
back in every system. Each kind is now one test that reads all 1051 runtime scripts at once (`EngineHazardTests`), with what
it allows on purpose listed beside the rule and why, and a Play-mode pass through every scene (`SceneSmokeLiveTests`).

| Kind | The rule | Found and fixed |
|---|---|---|
| State left in statics | Entering Play doesn't reload scripts here, so a static written at run time is reset in a SubsystemRegistration hook. Found by reflection (every mutable static of the game's types) and the source (written anywhere, reset in the hook). A static holding an asset made once (a material, a mesh, a settings asset) is allowed; counters, sort scratch and a few kept on purpose are listed with reasons. An [ExecuteAlways] registry is reset like any other: entering Play calls OnEnable again on the open scene (measured) | 85 statics in 45 files. Real ones: stopping Play mid-raid left `SaveGameService.raidUnderWay`, so the next session's saves said a raid was under way; `BuildPlacer.IsBusy` stuck from a session stopped while building blocked building; `GraphicsOptions.nearShadows` stale skipped the cascade splits on the next session's pipeline copy; dialogue, options, saves and creation screens kept destroyed windows and old frame stamps; seasons, festivals, horse travel, co-op spectating and the cheat window's choices carried over |
| Unity objects checked with `?.` `??` or `is null` | A destroyed object is not C#'s null, and in the Editor an empty Inspector slot or a missing `GetComponent` is a stand-in object: `?.` calls into it and throws. Use `== null` or `TryGetComponent`. Reflection names every field and property of a Unity type; the type's own body is read, and a type's static ones wherever they are reached (`Birds.Current?.`) | 41 statements in 21 files rewritten (fishing poses, the world's renderers, warmth's chill boon, eating's slow-down, wound tending, a pack's fallen member, tooltips) and 6 expressions (`GetComponent<Image>() ?? AddComponent` in the window kit and recipe rows never added the component in the Editor; the command wheel's torch; a resident's work gesture; the territory's region; a landmark's sound), and 7 reached from other classes (the fishing camera close-ups, the fish drawn to the bait, the crows landing, the command wheel's windows). `AudioManager.Sounds?.` is allowed: the settings asset, C#'s own null without a manager |
| Subscriptions to longer-lived systems | Whatever subscribes to a system reached through Current, Active or GameSession unsubscribes in the same file | none found (the guard keeps it so) |
| What is made in code | A mesh, texture, material or render target made in code is destroyed by its owner, returned to a caller, kept once in a static, or listed as handed to an owner | none found. `CharacterWetness` looked like one (wet copies of a creature's materials never destroyed) and is on purpose: a death ragdoll's severed and smashed pieces take them. Destroying them with the creature was tried and undone after `SceneSmokeLiveTests` caught pieces drawn with destroyed materials in a rainy Moss Marsh (the GPU Resident Drawer's "invalid Material ID"); the reason is now beside the rule |
| Renderer features | `Create` runs on every change to the renderer: the material is made only when there is none | the atmosphere, tonemap and UI blur features made a new material each time; the UI blur's old pass and the atmosphere's history targets were left behind too. The rule asks that the material's own field is checked first |
| Names in strings | Every `Resources.Load` path, `Shader.Find` name, animator state or parameter, tag (an unknown tag makes `CompareTag` throw), layer, input action, scene and string-named method names something that exists; a `Shader.Find` shader is one a build has (always included, in Resources, or named by a material or a shipped settings asset: in the Editor `Shader.Find` finds every shader) | none found (759 animator names, 331 resources, every tag, layer, action and scene) |
| References in assets | Every GUID in the scenes, prefabs, materials, controllers and data points at an asset that exists (a missing script, mesh or definition) | none found (35,205 known GUIDs) |
| Systems meeting each other | `SceneSmokeLiveTests`: the prologue as New Game starts it, the Haven, Troll Valley, the Moss Marsh and both arenas from the Haven, a fight in each, home through the results screen; any error or exception fails it (`GameLoopLiveTests` covers the Misty Woods) | its first run: the raid's navmesh, built as the map loads from the physics colliders, read 55 models' collider meshes that weren't readable, which Unity says works in the Editor and not in a build. In a build the creatures' ground lost every such rock, stump and ore node, and the Troll Bridge, bridge spans and boardwalks they walk on. Read/Write on the models would have kept a second copy of every render mesh (up to 55 MB; `ModelBudgetTests` forbids it), so the colliders alone are copied: `NavMeshColliderCopies` (a Resources asset, 55 meshes, made by Safe Haven › Navigation › Gather Collider Copies) and the raid's rebuild collecting its sources as NavMeshSurface does, each unreadable collider mesh swapped for its copy. Its second run: no errors in any scene |
| A cache that outlives what it caches | The whole suite in one run, in its own order: `RunTests.ps1 -All` | `GameInput` cached its actions per input asset, and the Input System gives the same asset new maps around Play and its test fixture: the cached actions belonged to maps no longer in it, never resolved again, and every key label threw (12 tutorial tests, only after the input fixture's tests and a Play session in one run, which the new smoke test made happen). The cache now notices the maps changing |
| Meshes the navmesh reads | Every unreadable mesh collider mesh in a prefab has an up-to-date copy (`EveryMeshCollidersMesh_CanBeReadByTheRaidsNavMesh`), and the rebuild still reaches the package's own source collection (`TheRaidsNavMesh_ReadsTheCopiesInPlaceOfTheColliders`) | the 55 above |

Looked at and found clean, so not made into tests: serialized fields renamed without `FormerlySerializedAs` (every key in the
prefabs and data checked against its class by reflection: only three fields removed on purpose left old keys behind, which
Unity drops on the next save), events invoked without a null check (none), `async void` (none),
searches in `Update` (all 13 are throttled, cached or once), UI on game time (the HUDs that use it pause with the game on
purpose).

Still a source scan, so still a map: a rule can miss a write through a method it can't follow, and a name used by two types
in one file. When it flags something safe, the fix is a line in its allow-list with the reason, never a weaker rule.

## Fourth round: state with one owner (2026-11-18)

The user, after the bug hunter's three rounds of 2026-11-18: "look for patterns in the bugs. can we create better code so these
bugs dont happen again? better than hard coding fixes on each bug individually". Of the 16 bugs those rounds fixed one by one,
most were the same few mistakes. The worst kind was one piece of shared state with no owner, which every system that needed it
pieced together or switched by itself. The fixes below give each kind of state one owner, and `SharedStateTests` fails on
code that goes around it.

| Pattern | The bugs it made | Now |
|---|---|---|
| **"Where am I" built by hand.** About 250 checks put "at home", "on a raid" or "in a friend's Haven" together from `GameSession.ActiveRaid`, `RaidDirector.Current`, `HavenVisit.IsVisiting` and `HomePortal.Current`; "at home" alone had four meanings | a guest's cargo went into a stash that wasn't theirs and was lost (CargoWindow); rewards held for home were paid in the host's Haven (QuestManager); a raid set out but not yet loaded counted as home | `Core/Place.cs`: each meaning said once (`OnRaid`, `InRaidScene`, `RaidRunning`, `RaidDecided`, `AtHome`, `InFriendsHaven`, `FollowingAHost`, `InAHaven`, `StashUsable`). 21 files moved to it. A line combining two of the parts fails the test, apart from three listed with their reasons (none of them asks where the player is) |
| **A shared switch with no owner.** Ten systems turned `PlayerInputHandler` off and on themselves, each guessing whether it was theirs to turn back on | the Nøkken's grab gave full controls back to a downed player, or to one whose raid had just ended; a cinematic that started while a scene change held the controls didn't take them, and the change handed them back in the middle of it | `Player/PlayerControl.cs`: `Hold(player, owner)`, `Release`, `Set`, `HeldOn`, `HeldBy`. The controls come back only when nobody holds them, and a destroyed holder lets go by itself. All ten moved to it (cinematics, scene changes, the Nøkken, being downed, watching friends, the prologue's ending, a raid's end, a launch, loading, home revival). A write to `.enabled` on the handler anywhere else fails the test |
| **A copy that drops fields.** A stack re-made from its item (`new ItemStack(stack.Item, n)`) loses whatever the constructor isn't given | round 1's `ItemContainer` copy; this round found four more: splitting a stack lost one half's size and an inscribed stack's inscription; merging two stacks lost the inscription; picking up blank parchment topped up an inscribed stack and made it blank; the horse lure's food | Copied from itself (`WithCount`, `WithSize`...). Plain pieces join only stacks of the same kind (`ItemStack.StacksWith`, the one rule). A `new ItemStack(x.Item, ...)` where `x` is a stack fails the test |
| Saved-state gaps | a station's worker crafts; whose a cart is | fixed one by one (round 1's `[NotSaved]` guard covers the settlement, quests, progress and ISaveables' own state; these two lived outside it) |
| Two clocks | a horse brought home from a friend's Haven kept stamps on the host's days | `HavenVisit` shifts them on the one path between Havens (`Horse.ComingHome(state, dayShift)`) |
| Ownership by kind | horses came home from a friend's Haven, carts stayed in the host's world | both carry their owner in their saved state and come home the same way (`Transport.OwnerIn`, `ComingHome`) |
| Derived state not worked out again, a partial result told as whole | the run's seed after the destination changed, the rally key, a stone's watch after its push was settled; the tithe's report | fixed one by one: too different in shape for one rule |

Also from the bug hunter's fourth round (all low): an extension that already holds one of a station's slots keeps it, so a new
one in reach takes only a free slot (`StationExtensions`; ids are random, not the order of placing; after a reload the order is by id again);
perks change only at home inside the ward (`SkillsWindow`; any scene with a portal passed before, a friend's Haven too); a meal
eaten for its boon on a full belly no longer says "You're not hungry." (`PlayerNeeds.WouldHelp`, one rule for both).

`RunTests.ps1` learned one trap more: an Untitled scene with changes (a run cut off by a recompile leaves one) can't be saved
without a file dialog, which stalls the Editor like the save prompt. The script now stops and says so, or, with
`-DiscardUntitled`, reopens the Haven in its place; a "Save Scene" file dialog seen during a run is closed and reported.

## Fifth round: five hunters, a reviewer and the patterns behind 47 findings (2026-11-18)

The user: "have several more agents look for bugs. you analyse them, look for patterns and fix bad checks etc to make the code more
solid". Five read-only hunters split the code (raids and creatures; co-op, saving and quests; the Haven and items; the player, combat
and horses; the UI and the world), the round-5 bug hunter took what earlier rounds hadn't reached, and a reviewer read round 4's own
fixes (it found three of mine wrong). All 47 are fixed. They fall into seven patterns; where one had a cause in common, the cause was
fixed, and a test fails on code that goes around it (`SharedStateTests`), or the compiler does.

| Pattern | What it broke | The one fix |
|---|---|---|
| **A switch with several writers, again.** The Player input map was switched by the input handler, the gameplay suspension and two holds, each undoing the others' reasons | **the Nøkken could never be escaped**: its struggle key was off for the whole grab, so every grab dragged the player under; the map came back on under a name being typed; a downed player crawled through a cinematic | `GameInput.SetPlayerKeys` is the map's one switch (all keys, a few, or none; off while suspended); a hold keeps keys through `PlayerControl.Kept` (a key works only while every holder keeps it, none while paused); a suspension keeps its own through `SuspendGameplay(owner, keep)`. A test fails on a key switched anywhere else |
| | a boss woken by a friend stood up frozen (`ai.enabled` had two writers, CreatureSleep and the boss) | the boss wakes through `CreatureSleep.WakeNow` |
| | sneaking flipped by the horse's gallop click: a galloping rider near-invisible | `PlayerStealth.IsSneaking` is false in the saddle, and the key is the horse's there |
| | "who works at this station" was three rules: the second fisher never learned, an empty second Smithy ran at the first smith's speed | `SettlementState.WorkerAt(stationId, kind)`, used for the worker, the speed and the learning |
| **"The player" meant the host's own, or the start room.** | camps slept around a guest exploring apart; a rockfall could close the only way out of a clearing a friend stood in; a guest's safe pocket was sized by the host's portal, their journal unlocked by the host's people, a quest done from the host's chest | `RaidPlayers` (every player up, friends too) for sleeping and waking; a rockfall closes only a link that leaves every room connected (every game works it out alike from the seed, so it can't go by positions); one's own values read `GameSession.Home` |
| **Gaps between states.** A rule that read "not on a raid" from one part missed the seconds a raid is decided, the results screen (no player) and the turn back | a guest whose visit ended on the results screen went home as they were before the visit, and the save once home wrote that over the raid's end; a guest down at the raid's end died after it; a quit during a turn back ended the run as a death | `RaidDirector.IsDecided` (ended or turned back) behind `Place.RaidRunning`/`RaidDecided`; `HavenVisit.InHostsHaven` is `Place.InFriendsHaven`; a visit ending with no character in the scene goes home from the next scene that has one |
| | a guest joining mid-stage had no rocks where the host had them; companions kept the first stage's strength while enemies grew | a stage's effects on the land are state, done once at that stage or any later one (and caught up on start); companions are toughened again on each `StageChanged` |
| **Cleanup on one way out only.** | a scene load that broke off after it started left a launch's hold, a raid "under way" and saving refused; the results screen's Continue stayed off; a raid's way home was never tried again; a rider whose horse went mid-dismount was left aboard nothing; a meal went on after death by poison | `SceneLoader.Load(scene, caption, ifItFails)`: one callback for a refusal and a later break-off (a test fails on a load's result tested without it); `SceneTransition` raises `Abandoned` on every break-off; the rider's half of getting down needs no horse; eating is an `IPlayerActivity` |
| **Told before it was known, or by a default that lies.** | "-0:45 lost" when the clock kept 15 s; essence "drawn back" past its cap; "a little extra" that didn't fit; "too far off to hear" at 5 m; a recovered arrow came as a new find (collect quests farmed); "out together with 1, 2" | `RaidSession.AddTime` returns the real change; messages say what `Add`/`Give` returned; `ItemInventory.Give` has no default cause, so every caller says why (the compiler asks) |
| **Windows working a rule out again.** | the ability book closed a conversation as "walked away" (a quest offer turned down); a guest's station buttons lit and then refused; the portal named another quest than the tracker; Graphics' reset missed three options; the cheat search flipped the camera at every "v" | `HavenVisit.IsHostsToDecide` (the quiet form of `LeftToHost`); `QuestTrackerView.InTrackerOrder`; `GameOptions.Graphics` reset whole; `KitSearchGuard.On(field)` (a test fails on a text field without a guard); conversations and the pause menu stay open under the book |
| **Copies and orders that dropped what they carried.** | a co-op stash change lost a fish's size; a stabled horse lost its meal on every load; a station's extension slots went by random id after a reload | `SlotSaveData.With` / `SaveMapper.SlotOf` (a test fails on a slot made field by field); stall ids saved with the stable; new ids lead with their placing time (`SaveableEntity.PlacedAt`) |

Fixed one by one, with no rule in common: workers delivered gear Plain (now only what keeps no condition; gear waits for the player's own
collect, where its quality is made); a lost asker's order was answered from the bag and the stash and blocked every other order (it
comes down); saplings planted after the first never reached the other game (restores merge, planting only in one's own Haven); home time
ran on in the pause menu (time away is game time); a strong rally call never ended near an elite; cancelling took a worker's craft;
renown went by list order; crows due in a fight never came; a thief with loot could be tidied away.

## Sixth round: kinds no round had scanned, and the evidence the game leaves (2026-11-18, evening)

The user: "look for more bugs and pattern". Done without a fan-out of agents (the user's limit until 21:20): each kind of mistake
scanned across all runtime scripts at once, then the evidence real runs leave (1,470 playtest logs' Stuck rows, the Editor's console
from the day's test runs) followed to its cause in Play mode.

| Pattern | What it broke | The one fix |
|---|---|---|
| **A game left by the menu hands its statics to the next.** A static is reset when the program starts (and when Play starts in the Editor, where every test is a fresh start, so no test ever saw it), not when the player quits to the menu and starts or loads another game in one sitting. `GameSession.Reset` already forgot three systems' state, each added after its own bug | the prologue's hold left by the menu kept its clock: the next game's tracker showed "0:00" on that quest; the raid's save for home, waiting for a scene with a player, waited through the menu and was then written in the next game (a new game's first save made at once); the builders' names in the next game's message home were the left game's | each a `ForgetGame` called by `GameSession.Reset` (QuestTimers, ResidentBuilders; these run on co-op's `TryPlay` too, so the rowing hint stays once a run of the program: forgotten with the game, it came back after every visit), and the prologue clears its own clock when it goes however it goes. The save for home belongs to the journey: forgotten when a save is loaded or a game begins, kept through `TryPlay` (coming home from a friend's Haven, it is what saves the guest's own Haven), and one waits however often it is asked for. `SharedStateTests.EveryForgetGame_IsCalledWhenAnotherGameBegins` fails on a ForgetGame no one calls; `QuitToMenuLiveTests` plays the way out: a raid left by the pause menu's Quit (saved as a fall, the save for home waiting), a new game (no clock, no builders, nothing written), the left journey loaded (home, at the portal), failing on any error from any thread |
| **A check that can't hear what it is for.** `SceneSmokeLiveTests` listened on the main thread's log (`Application.logMessageReceived`, which Unity raises for main-thread messages only), and the renderer also logs from its culling jobs | the day's whole-suite run passed while the GPU Resident Drawer's "invalid Batch, Mesh, or Material ID" was logged at the end of the raids it played | `LiveTestErrors` hears every thread, and what isn't the game's is listed with its reason beside it. The one listed, measured in Play mode: the Resident Drawer's own check comes only as a raid's scene unloads behind the loading screen (a raid's creatures' skins, the Marsh's lily pads); never at a kill, a corpse sinking or being capped, nor at a scene load of the Haven's own with corpses lying in it |
| **One rule in two places, again: time away.** The day clock and the nodes' regrowth each worked out their own "time away", and their shared `Run` even named the other | round 5 moved the day to game time (an hour in the pause menu on a raid was two dawns at home); the regrowth kept real time, so the pause menu still grew every node in the Haven back | `DayNightCycle.AwayStamp` and `SecondsAwaySince(run, stamp)`, the one clock both read; `SharedStateTests.TimeAway_IsCountedOnTheDaysOwnClock` fails on a saved state reading a clock of its own (real time, `timeAsDouble`, unscaled time); `DayNightSaveTests` |

Also fixed: a raid companion's bond was written to one's own Haven and read back, a line later, from the Haven one stands in
(`RaidCompanions`). Out of reach today (a guest following a host brings no companion), and one co-op feature away from an exception
at the raid's end; both reads are `Home` now.

The round's review (one agent, read-only) found no regression in play and four smaller things, all fixed: the renderer's message listed in `LiveTestErrors` was let through at any moment, though its reason holds only while a scene changes (now only while `SceneTransition.IsBusy`); the rowing hint above; a boss's hold kept by an unnamed instance key (`BossEncounter.holdToken`, now a `LeaseKey`, and the guard reads instance fields too); and `QuitToMenuLiveTests` named more than it checked (it checks the builders now).

From the playtest logs read as data (1,470 runs scanned for once-only events repeated in a session, and negative amounts):

- **Ingrid freed three times on one raid** (the user's runs of 11-14 and 11-15): a rescued traveller's interaction stayed on once she
  followed (only an event's spawned traveller had it switched off, a line after), so pressing it again said her thanks again and raised
  her freeing and its story event again, with "free" still offered over her. Freed once now, and the interaction goes
  (`QuestTests.ATraveller_IsFreedOnce_AndIsNotOfferedFreeingAgain`).
- **The Haven's rank, a row every 2 s** (219 in one session): HavenProgress tells the rank again on purpose, so a quest begun after it
  was reached still finishes; the playtest logger wrote every telling. Written once a rank now.
- Found by that test: the voice player, asked outside Play, made its DontDestroyOnLoad host, threw, and left the host in the open
  scene. It says nothing outside Play.
- **LeaseWatch couldn't say what it had found**: 111 of its 115 Stuck rows read "held by Object" (a plain `new object()` key) or
  "held by PlayerArmature" (any component on the player). A component is told by its kind now ("PlayerArmature's PlayerDodge"), and
  the ten static owners of a lease are `LeaseKey`s with a name ("a finishing blow", "an agent probe from the command line");
  `SharedStateTests.ALeasesOwner_HasAName`. From the rows that could be read: the 80 "Object" ones sat in agent probes standing
  still, so the CLI's invulnerability; the next playtest's rows will say.
- **Kills with no creature** (16) and **"Destroyed" with nothing** (23): deaths of what has no definition (a companion, a traveller, a
  wild horse, the prologue's training dummy) and the Haven's breakables. A kill quest with no creature set counts by side, and only
  the dummy is on the enemies' (Eirik's lesson comes before any quest that counts kills), so none counts today.

**What a test leaves in the open scene comes back into the next Play-mode test.** The whole suite failed once on this round's own new
test: freeing a traveller in edit mode thanked the player through `DialogueBox.Current`, which builds the dialogue box and its portrait
camera on first use. Left in the runner's scene, they came back into the smoke test's Play mode without their wiring (private fields
aren't serialized) and threw every frame. The second time (a bare Main Camera left by a test once took over Play). The test removes
what it made.

Left open (needs a look in the game, so not done blind): `CreatureLook` reads `renderer.materials` (three places), which copies every
material of every creature it touches ("Wolf Main (Instance)"); the copies are never destroyed and are freed only when the raid's
scene goes. Round 3's "made in code" rule looks for `new Material`, not for this getter. Material property blocks would change
nothing on screen and copy nothing, but the eyes need checking close up after.

Scanned across all runtime scripts and found clean, so not made into tests:

- **Written to one's own Haven, read from the one stood in** (every file using both `GameSession.Home` and `Here`, 19): each runs only
  at home or only in the game that keeps the Haven (`HavenDecisions`, the systems that stand down in a friend's Haven), where the two
  are the same, apart from the companion above.
- **Logic that differs in a build** (`Application.isEditor`, `#if UNITY_EDITOR` in runtime code): Steam, cheats, a slot made by an
  autosave and the Quit button, all meant.

- **Numbers written and read in the player's language** (a Norwegian machine writes 1,5): every save string, playtest row and parse is invariant.
- **Enum values shifting under saved data** (Unity and JsonUtility store an enum as its number, so one inserted mid-list re-labels
  every asset and save after it): every commit's enums compared before and after across all 616 commits. Three shifts, all in
  runtime-only enums (a window's tab, a horse's idle).
- **Triggers that never see the exit** (OnTriggerExit isn't called for a destroyed or disabled object): zones go by distance here;
  three triggers, none counting.
- **Random picks that never reach the last** (`Range(0, Count - 1)`): every hit picks a segment (`at`, `at + 1`), as it should.
- **A registry changed while it is walked** (a trap's foreach over `MonsterAI.Active` killing what it walks): a death takes nothing
  out of the registry at once, and a breach's next wave comes from a coroutine.
- **A lease held by an owner that is gone** (invulnerability, a floor, speed): LeaseWatch's Stuck rows in 1,470 runs: none "held by
  something gone". 80 are agent probes' own invulnerability, 13 the probes standing still, 2 a cinematic longer than LeaseWatch's
  20 s (CinematicView lets go in OnDestroy too).
- **The Lost**: a downed player (co-op) is held at 1 health, never "killed", and one bleeding out is counted as its kill only when its
  blow was the last that hurt; the briefing and the raid agree from the seed.
- **Quitting mid-raid**: the raid ends as a fall and is saved before the game goes; the raid's own way to the results screen, refused
  while the menu loads, dies with its scene.

## The tests themselves: green that proves nothing (2026-11-18, night)

The user: "i feel most test positives are fake positives. is there anything we can do to the test to reduce the amount of tests and
reduce the amount of false positives?" The evening's evidence agreed: the suite (about 2,070 test methods) stayed green through every
bug of the sixth round, all of which were found by reading code by kind, the playtest logs and Play-mode probes; and two checks were
green while blind (the smoke test's listener, and a guard the test framework never ran).

Two agents read every test (241 files) against one question: would it turn red if what it names broke for a player? 1,874 would;
194 (9%) wouldn't, or would turn red for nothing:

| Kind | How many | What it looked like | Done |
|---|---|---|---|
| **Mirror** | 85 | data copied into the test: exact prerequisite lists, the rescue order, recipe ingredients, counts ("27 places", "7 torches"), tuning values. Red on every design or balance edit, catching nothing the audits don't | deleted, or kept as the rule they hid, on the shipped data ("every boss steps through each stage as its health falls", "the rescues form one chain from Ingrid") |
| **Weak** | 35 | real, but blind to the likely break: "survives a save" through JsonUtility instead of SaveMapper; fresh code defaults instead of the shipped asset; "Every..." over a hand-picked list; a cap tested by reading the cap | rewritten through the real path (SaveMapper, the Resources assets, every asset of the kind, a ninth item refused), or deleted where SaveCoverageTests already round-trips every saved field |
| **Brittle** | 35 | exact wording, a private field read by reflection, a folder name, HLSL lines: red on a harmless refactor | the behaviour kept through public API, the pin dropped |
| **Vacuous** | 29 | could pass on nothing: loops over assets with no count (all eight model budget tests at once if a folder moved), `Assume` that quietly skips, `x == null || ...`, an `[Ignore]`, a tautology | a count of what was actually checked, Assert for Assume, the precondition set up; pure tautologies deleted |
| **Redundant** | 10 | the same behaviour asserted twice | deleted |

56 tests deleted, 122 rewritten. The guards' own blind spots, fixed by hand: GameLoopLiveTests listened for main-thread exceptions only
(now `LiveTestErrors`); LeaseWatchTests read a private list through a wording filter, so a reworded report made "nothing found" pass
(now `LeaseWatch.Found` and holders the test names); SaveCoverageTests counted components it skipped among those it probed; the "made in
code" rule saw `new Material` but not `renderer.materials`, which copies them (CreatureLook listed, open).

Widening a test can make a new false alarm: the first run after the rewrite turned three red for nothing, each a rule run over every asset that holds only for some. The builder's completeness check run on every creature (a prefab's agent speed, which play sets from the data); every skill's tutorial page, though some also wait for a quest or a technique (Guard and Parry, the parry); every sky's day and night, though the prologue's night at noon is dark by day on purpose. Each now covers exactly what the rule is about, the exceptions listed with their reason.

**Every guard is seen to fail.** `GuardCanaryTests` plants each source-scanning guard's bad case as one more runtime script
(`ConventionTests.Planted`) and requires it red with the plant and green without (19 guards, one test; checked by blinding one).
`AuditCanaryTests` plants faulty assets for the content and quest audits (a recipe nothing teaches and with no id, a quest outside the
content database) and requires each audit to name them. A guard that can't fail looks exactly like a clean codebase.

Kept on purpose, though they turn red on intended change: `RaidLayoutGoldenTests` (the Misty Woods maps pinned while other raids are
built), `WeaponIdentityTests.EachFamilyKeepsItsPace` (a deliberate damage-per-second lock), tests of the user's own written lines.

Left: `CliCommandTests.HealthRunsEveryPage` (needs Health to return a count), `JournalTests.Eyes_...` (ignores errors until CreatureLook
stops copying materials), `CoopWireTests`' fingerprint (hashes field names: a rename turns it red, a reorder of NetworkSerialize's calls,
which does break the wire, doesn't), the "made in code" rule's file-wide `Destroy(` (one Destroy anywhere clears every made thing in the
file), and "a friend's copy doesn't tick its status", now untested (no public path to it).

## Seventh round: the build's own log, then three hunters (2026-11-25)

The user's request: "do a bug hunt in the games systems. fix the cause, not the symptom". Evidence first: `Player.log` from the user's build evening
(2.3 MB) and its playtest CSV, then three read-only hunters over the newest unplayed systems (skills and combat; settlement, keeper and
territory; people, save, crafting and farming). Every finding was checked in the code before it was fixed.

**From the log.**
- An empty skill key threw. `TryCast` read the missing skill's cost before it looked at the refusal it already had, and none of the 2,000
  tests ever pressed an empty key (now `SkillAimTests.AnEmptySlotsKey_IsRefusedAsEmpty`, through both ways a key casts).
- About 40,000 "Particle (Orbital) Velocity curves must all be in the same mode" lines. Unity accepts mixed curve modes without a word,
  then warns on every frame the system simulates. The sources were the loot glitter and snow (both made in code) and the portal motes
  (a prefab). `ParticleCurveModeTests` scans every prefab of the game's own.
- Three LeaseWatch "Stuck" rows were all false. The watch measured in unscaled time, so a pause during a dodge and the prologue's
  ending (a cinematic that holds the game still) read as leaks. It now measures in game time, and an in-game cinematic's hold is a
  lasting hold.

**The patterns behind the hunters' 14 findings.**
1. **A rule kept in two places.** Away and home each had their own copy, and one copy was missing a part:
   - work pace away left out meals, bruises, traits and spirits (now `ResidentLife.WorkPace`, used by both);
   - builders away also ran their bench;
   - a Farmer away waters every plot (open: plot positions are not known away).
   Two clocks inside one rule were the same kind: outposts were judged after the territory turn and parties before it. Before adding a
   rule for away, search for the home rule and call it.
2. **"Now" asked after it has passed.** The homecoming gift asked the live raid who had been out, but the raid was gone once home.
   The worker-made tally counted crafts made at home in "while you were gone". Take the answer where the thing is settled, not when it
   is told: the territory's turn hands over the parties and outposts it settles (`Homecoming.NoteOut`). My first fix took it at
   RaidFinished, and the review found the turn had already run by then. Listeners to one event run in an order nobody chose.
3. **An event that only fires on a crossing.** A bond rank's passion pick was owed only on crossing into the rank. A save loaded at its
   rank never crosses, and a jump over two ranks crossed only one. Owe what is held, guarded by the deed (`PeopleBond.OweReachedRanks`),
   on growth and on load alike.
4. **Cleanup on interruption, again (guard 6).**
   - Aiming a skill registered no cleanup: Esc cast the skill as the paused keys came up, and a cinematic left the marker on the ground.
   - A cancelled cast left its busy time locking swings, and its lunge sliding on.
   - `IsDodging` read any dash, so a skill's lunge counted as a dodge.
   - A skill pressed in a swing's wind-up was cast on top of it.
   The fix is one `CancelCast`, one `LetGoOfKeys`, and `EndsOn` covering pause and windows (which only let go of the keys).
5. **Assign where only setting was meant.** `record.Bruised = !won` healed a breach bruise.

**The review of my own fixes found 5 of the 20 wrong or incomplete** (the fifth round found 3 of mine). These were:
- the homecoming list taken too late;
- builders away kept from their bench for a whole raid (a scaffold's waiting had no end away: now `ScaffoldsWantHands`, which stops
  once the banked hammering covers the work noted at home);
- a skill pressed during a held heavy charge expired;
- a stagger dropped the aim;
- the particle test never built the glitter it was written for.

Narrowing `IsDodging` to the dodge also showed that half its readers meant "the body is dashing" (the feet, the guard, the bow,
sneaking). Those now ask `PlayerMovement.IsDashing`. **When a name is narrowed, read every reader.**

Open:
- A Farmer away waters every plot, at home only those in reach.
- "Failed to create agent because there is no valid NavMesh" at three scene loads (all agents in the scenes as saved stand on a mesh
  for their own type, so the cause is something spawned at load).
- Stations away keep the worker speed set at home.

## Eighth round: the core systems (2026-11-27)

The user's request: "do another systematic bug hunt as well. focus on the core systems for now. same practice as usuall". Evidence first: there were no build runs since the seventh round, the Editor console held only test noise, and `EngineHazardTests` plus the three live tests (`GameLoopLiveTests`, `QuitToMenuLiveTests`, `SceneSmokeLiveTests`) all passed. So this round rests on three read-only hunters: saving, the session and the raid's life; items, crafting and survival; the player and combat. The hunters reported 22 findings. Every one was checked in the code before it was fixed. A review of the moments commit (skill presentation) ran alongside, then one reviewer checked all of my fixes.

**The patterns, and the guard for each.**
1. **An interruption delivered as input.** Taking the keys (a pause, a hold, the world map, a name being typed) reached the bow, the swing and the rod as the attack button let go. The Input System cancels a held action as its map goes off, and the cancel came through as a release:
   - Esc or the map mid-draw loosed the arrow;
   - a charged heavy struck as the game resumed;
   - an aimed skill was cast behind the map.

   The seventh round fixed this for skills only, and only for the pause and holds.
   - Guard: `GameInput.ApplyPlayerKeys`, the one place the Player map is switched, sets `GameInput.KeysGoing` while it switches the map off and raises `PlayerKeysTaken`. `PlayerStateMachine.TakeAttackKey` gives up what the press was doing.
   - Test: `InputActionsTests.TakingTheKeys_ComesAsKeysTaken_NotAsTheAttackLetGo`.
   - Related fixes: a buffered press no longer starts a swing under a hold, and the guard judges the hand each frame (a bow swapped in kept it up).
2. **What a landed blow means, assembled per listener and per path.** Focus, Regain and lifesteal each had their own test for a fight blow, and crates, fences and parried blows paid all three. Swings, bolts and arrows each had their own list of what happens after a hit: a staff bolt never shattered Frozen, and an arrow laid nothing.
   - Guards: `FightBlows.Counts` (a foe on a side, through guard and parry, taking health) and `AttackResolver.Landed`.
   - Focus is now paid per blow that lands (`PlayerFocus.ForBlow`), not per target a swing found.
3. **Pure rules that accept degenerate input.**
   - A blow with no horizontal direction was guarded from every side: a guard raised during a fall parried the fall each frame.
   - `SlowTime` merged overlaps into the slowest speed for the latest end, so a hit stop inside a slow beat froze all of it.
   - A status burst went through armour and the damage-taken multiplier a second time.
   - Tests: `CombatTechniqueTests`, `PauseTests`, `SkillLookTests`.
4. **A handover done when asked, not when done.** A save loaded with F6 was swapped in before the loading screen. The old Haven played on it for the whole load: it raised the loaded Haven's rank, moved settlers in and finished rank quests. A load that broke off left the other save in place, so F5 wrote the old character into it.
   - Guard: `SceneLoader.Load(..., asItCommits)`. It runs once the load can no longer break off, just before the new scene's Awake. `TryLoad` and `TryStartNewGame` swap the game there.
   - `TryPlay` (co-op) stays immediate, because the visit's state is set as it returns.
5. **A rule kept at one exit.**
   - Quitting counted as a fall only from the pause menu, while the window's X or Alt+F4 undid the raid. `RaidDirector.LeaveRaidOnQuit` on `Application.wantsToQuit` now closes this, in builds only.
   - Quest rewards were held only once the raid was decided. Now they are held whenever the player is on a raid.
   - The "raid cut short" flag was cleared only when the launch save went well. It is now cleared in a `finally`.
6. **A piece identified by its item type where the piece matters.**
   - Z and L took out the first matching item in the bag, broken or a lesser copy. Now `FindPiece` and `FindInBag` work on stacks, unbroken first.
   - Body loot was rebuilt from its item type. It now uses `GiveMade`.
   - A same-item swap into the stash counted nothing.
   - Guard: `SharedStateTests` now also fails on `Give(x.Item, x.Count`.
7. **"Carried" has a different scope at each call site.**
   - The bar counted every slot but used only unlocked ones.
   - Wells counted the bag alone and handed out new waterskins for the ones in a chest. My fix counted the stash everywhere, and the review caught that it would leave raids without water. It now counts the stash at home only.
   - A reward the stash took counted for no collect quest.

**The review of my own fixes found 4 of about 25 wrong or incomplete.**
- Keys taken by a gameplay suspension (the world map) still came as a release. This led to the `GameInput` choke point above.
- The wells regression.
- The load commit set the save slot after a step that could throw.
- The new static `committing` was not reset at Play start. `EngineHazardTests` caught it too.

Open:
- A station craft's refund gives back the recipe's list, not what was paid. This matters with Free Costs on, and when the materials were paid from the stash. `HandCraftQueue` already does it right (`MaterialPool.Payment`). The fix is a payment kept and saved with each queued craft.
- The horse and the cart are missing from the save written on coming home, until they are set down, so a crash in those seconds loses them.
- The Sparks and modifiers paid for a launch whose save throws are not refunded.
- End-of-raid listeners run in an order nobody chose: a companion lost on an extraction is mourned one raid less. The fix is one ordered step in `RaidDirector.Finish`.
- A reward's ground share counts for no quest when picked up.
- A downed co-op player's swing already under way still lands, because `CoopDowned` raises no interrupt.
