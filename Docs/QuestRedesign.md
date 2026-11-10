# Quest redesign: the first two hours

**Status: approved by the user on 2026-11-10. All three steps are built: the categories and the open door, the prologue, and
the first act's story. None of it has been played by hand.** See "What it takes to build" for what each step holds and the "As built" sections for how they differ from the plan. This covers the
prologue, the first days at home and the first four raids, up to the Heartroot. The rescue order in `Docs/RescueOrder.md`
stays as approved.

## In short

- The portal opens after three short steps at home, not nine. The first raid starts at about minute 22, not after minute 50.
- The main story is 15 quests where something happens in each: a fight, a find, a choice or a reveal. None of them is
  "gather N" or "build X".
- Every building quest becomes an optional Haven quest. Several are open at once, and each one pays with something the next
  raid needs.
- Quests get five categories with their own colours: Main Story, The Lost Expedition, Haven, Tales and Errands.
- The first act gets a boss and a twist: the Heartroot, which already exists but has no quest. Beating it opens Troll Valley.

## What is wrong now

Measured from the three latest real runs in the playtest logs (4, 8 and 9 November):

| What | Measured |
|---|---|
| Prologue with the three morning errands | 20.9 and 24.1 minutes |
| Prologue without them | 11.9 minutes |
| First fight in the main story (wolves on the road) | minute 15 on 9 November. The errands before it took 9.5 minutes and held one fight |
| The walk down the road | 2.7 to 4.6 minutes with nothing to do |
| The forest, from the tree line to the stones | 9 quests in 6.3 minutes. Several lasted under 30 seconds, and each hand-over is a talk with Sigrid |
| The camp defence | over in 12 to 18 seconds |
| The final hold | the player fell in all three runs, once after 18 seconds |
| First raid | not reached. One run stopped at minute 27 in the chest quest. The other was at minute 52, with cheats, and still building |

The causes, read from the 58 quest assets:

1. **49 of 58 quests are ranked Main Story.** Giving Tor a job is gold, and so is building a Kitchen. The main story reads
   as a to-do list.
2. **The portal waits behind nine quests in a row**: listen, 10 sticks and 7 rocks, two tools, 24 oak and 12 stone and a
   workshop, a chest, a club, a stew, three orbs, the repair.
3. **The same lessons are taught twice.** The player gathers sticks and rocks, builds a campfire and stews berries in the
   prologue, then does all three again at home before the first raid.
4. **The forest chapter is cut too fine.** Find shelter, gather fuel, light the fire, cook, rest, defend, run, touch the
   stone: eight quests, each handed over in a conversation. That is the "walking and talking".
5. **Nothing has a face.** The old troll breaks the door and is not seen again. The Heartroot is a boss with no quest: it
   turns up as a rumour after two extractions. Nobody asks a question the player wants answered.
6. **The best line is never picked up.** The portal says "one day you will tell me why these old stones woke for your
   hand", and nothing follows.

## What other games do

| Game | What it does | What we take |
|---|---|---|
| Cult of the Lamb | A short fight opens the game. The first crusade comes right after the first follower and works as part of the tutorial. The base grows from what crusades bring back, and a run takes about ten minutes | Fight first, build second |
| Hades | The story moves on when the player dies. Home between runs is where people react to what happened | Every return gives something new to hear, win or lose |
| Enshrouded | Quests are sorted into Flame (main), survivor, side and lore. The first main quest is one building, the Flame Altar, and it leads straight into the Shroud to fetch the Blacksmith | One building, then out. Rescues are their own category |
| V Rising | The journal is a tutorial that feels optional and pays in recipes. Bosses are the spine: what you can build depends on which ones you have beaten. Players complain when the journal nags | Lessons pay their way and never block. Bosses gate tiers |
| Valheim | Each boss drops what opens the next tier. The first one can be beaten in the first session and teaches how bosses work | The Heartroot ends the first act, and its trophy opens Troll Valley |
| RuneScape: Dragonwilds | About 14 primary quests and 26 secondary. Three or four short tutorial quests, then the main one. Side quests in a region wait for its primary quest | Few main quests, many side quests |
| Gord | The campaign is a long tutorial. Reviewers call the gathering "time-consuming and distracting" and say it "won't allow you to play at your own pace" | A warning: this is where the game is now |
| The Witcher 3 | The first rule its quest designers were given: "We don't do fetch quests" | No main quest is a fetch |

A Microsoft Research study of the first hour calls it a trial period, and argues that intrigue and information matter more
there than fun. That is the reason for planting three questions early (see the story below).

## The rules

1. **Fight first, build second.** A weapon in hand by minute 2, a fight by minute 5, the first raid by minute 25.
2. **Something happens in every main quest.** If a quest only asks for materials or a building, it is a Haven quest.
3. **Nothing optional blocks the portal.** Haven quests help. They never gate.
4. **The problem comes first.** A Haven quest opens when the player has just felt what it answers. This is already the rule
   for rescues.
5. **A boss ends each act**, and what it drops opens the next region.
6. **Coming home is a scene.** Each person brought home remembers one thing about the night the door broke. The story is
   told at the fire, between raids.
7. **Failing a raid still moves the story.** What was found stays found, and the portal has a different line for it.

## Quest categories

`QuestRank` has three values today. This adds two.

| Category | Colour | What it is | Rules |
|---|---|---|---|
| **Main Story** | gold | The pact, the door and what took the expedition | One at a time. Every step is something that happens. A boss ends each act |
| **The Lost Expedition** ("Rescue quest") | new: teal, the colour of raids | One quest per person to bring home | The big progression quests: each opens a building, a job and recipes. Order as in `RescueOrder.md` |
| **Haven** ("Haven quest") | new: green | Build and learn at home | Optional. Two to four open at once, in any order. Given by whoever needs it. The reward helps the next raid |
| **Tales** ("Tale") | silver | Folk tales found out in the woods | Start by finding something. Each has a bargain, a riddle or a twist |
| **Errands** ("Errand") | bronze | Small asks and bounties | Short. They lapse without cost |

Where today's quests go:

| Today | Becomes |
|---|---|
| Prologue, 12 main quests | 7 Main Story quests (see below). Berries for the Pot becomes an Errand |
| Prologue errands (wood, fish, hunt, Eirik's lesson) | Errands, as now |
| Awakening, A Light in the Wood (fire only), The Rootbound Stones, Awakening the Portal, Into the Misty Woods, The Weakening Ward | Main Story |
| They Are Still Out There | Removed: the shorter line makes it unnecessary |
| Gifts of the Clearing + Tools of the Clearing | One Haven quest: Tools of the Clearing |
| The workshop half of A Light in the Wood | Haven: A Roof for the Work |
| A Safe Place, Something to Fight With, Strength for the Road, Raise the Walls, A Growing Home, Heavy Loads | Haven |
| A Light They Can See (reach Hamlet) | Haven, as its milestone |
| The 11 Hands at Home quests | Haven, each given by its person when they come home |
| Bring Ingrid Home and the nine other rescues | The Lost Expedition |
| Know Your Enemy, The Smith's Request | Errands |
| Echoes in the Wilds | Tales |
| New | Main Story: What Breathes Under the Roots, The Road North |

## The story of the first act

It builds on what the GDD and the portal already say: the forest forgets the lost, a tended hearth is what it cannot
forget, and the old pact was a hearth for a ward.

**Three questions, planted in the first fifteen minutes:**

1. **Where did the sun go?** It turns to night at noon as the expedition reaches the trees.
2. **What is the forest doing to us?** By the fire Sigrid counts the four who went ahead: "Ingrid, Bjarne, Tor and..." She
   can't find the name. Runa says "Liv." Sigrid has known her eleven years. Then the wolves come. When the portal later
   says "the forest does not kill the lost, it forgets them", it answers something the player already saw.
3. **Why me?** At the waystones Sigrid puts her own hand on the centre stone first. Nothing happens. It wakes for the
   player's.

**Something with a face.** The old troll doesn't attack the expedition at the stones: it goes for the stones. Ingrid says
so when she comes home: "It didn't come for us. It came for the door." In raids its footfalls are heard and felt at a
distance, with the rumble the prologue already has. It is the second act's boss, in Troll Valley.

**The first act's boss is the Heartroot**, and three things the player has already met turn out to be the same creature:
the roots that closed the road behind the expedition, the roots choking the three stones at home, and the Root Guardians.
Bjarne supplies it: "I blazed the trees going in. Coming back, my marks were on the wrong side. The road hadn't moved. The
roots moved it."

**The twist.** The Heartroot was the forest's guardian: its half of the pact. When it falls and its heartroot is brought to
the fire, the portal says what it has kept back: "It kept its half for ninety winters after your kind stopped keeping
yours." People broke the pact first, by letting the hearth go cold. The haven stops being a shelter from a hostile forest
and becomes a debt being paid. This fits the GDD's "broken, or perhaps forgotten" and leaves the later acts open.

**Answers by the end of the act:** the sun went into the Heartroot's canopy, and the road north is open and lit once it
falls. What the forest does to the forgotten is seen in raid 1 (below). "Why me?" stays open: the Heartroot's last word to
the player is "Hearth-keeper", a title nobody has used.

**The choice by the fire pays off.** The player already says why they came: coin, truth or kin. The portal's line at the
end of the act differs for each.

## The new line

### Prologue: The Unquiet Forest (target 12 to 14 minutes, 7 quests)

| # | Quest | What the player does | What changes |
|---|---|---|---|
| 1 | A Morning by the Fire | Meets the three. Sigrid arms them: the weapon choice moves here from the tree line | A weapon in hand at minute 2 |
| 2 | The Road East | Walks with the party. The tipped handcart is a stop with a find. The boar run past, and what they run from comes after them: the first fight, in daylight. Then the day goes out | Takes in Where the Sun Went. A fight by minute 5 |
| 3 | Shelter from the Storm | One quest, three steps without going back to Sigrid: fight down the road to the rock shelter, gather a few sticks, build the fire | Takes in A Spark in the Dark and A Fire of Our Own. Sigrid talks in asides while the player works |
| 4 | A Moment of Warmth | Talks by the fire: the Liv moment, and why the player came | Kept. It has one beat nobody should miss |
| 5 | Eyes Between the Trees | Defends the camp: two waves, with a pack leader in the second | Was two wolves and 15 seconds. Target 60 to 90 seconds |
| 6 | Something Is Coming | Runs north from the troll. Sigrid's hand fails on the stone, the player's wakes it | Takes in Wake the Waystones |
| 7 | The Last Connection | Holds the stones for a minute. The troll goes for the door | Kept. Tune it so that standing to the end is possible |

Berries for the Pot becomes Eirik's Errand during the rest, closed by the wolves: the small trolls still ambush the berry
clearing, and a warm meal helps in the fight that follows. Cooking as a lesson moves home.

The morning errands stay optional. They are 9.5 minutes with one fight, before the story starts, and they must be done in
order. See the open questions.

### At home: the door (target 7 to 9 minutes, 4 quests)

| # | Quest | What the player does | The moment |
|---|---|---|---|
| 8 | The One It Caught (today: Awakening) | Listens to the portal | It caught only one. The forest forgets. And a rule taught as story: what you wore came through, what you carried did not |
| 9 | A Light in the Wood | Builds a campfire from what lies around the stones | The ward wakes and someone far off turns to look (built). New: the light shows three stones choked in roots, and the roots move |
| 10 | The Rootbound Stones | Frees three Life Orbs | Each stone gives one sentence of the pact. Rootlings come out of the last: the ward is still too thin |
| 11 | The Door Wakes (today: Awakening the Portal) | Feeds the orbs to the portal | Sigrid's voice (built): Ingrid is hurt and close. The portal: "You can go now. You would go better with a hatchet in your belt" |

Two things make this possible:

- **The player keeps the weapon and armour from the prologue.** Today the forest's gear is wiped and the first weapon
  waits for a workshop. Keeping what was worn matches the raid rule (deployed gear comes home, carried things don't), so
  the prologue's ending teaches that rule.
- **The repair asks only for the fire and three orbs.** Tools, the workshop, the chest, the club and the stew are Haven
  quests, open from the moment the fire burns.

### Raids one to four (to about minute 120)

| # | Quest | Category | What happens |
|---|---|---|---|
| 12 | Into the Misty Woods | Main Story | Find Ingrid and come back. She is cornered by wolves when found, and heals the player afterwards. On the way: a figure in old expedition mail that doesn't answer to anything, the first sight of what the forgotten become. The troll's footfalls, far off. The quest counts when the player returns, extracted or not: what was found stays found |
| | Bring Ingrid Home | Lost Expedition | Runs beside it and completes when she is extracted. If she falls she waits (built) |
| 13 | The Weakening Ward | Main Story | The first breach, warned after the first return (built). New: a fallen attacker sprouts the same root as the stones. The briefing's Breach Source rumour is the optional way to weaken it first (built) |
| | The Smith in the Woods, Planks Not Logs | Lost Expedition | Bjarne and Tor, as approved. Each brings one memory of the night home |
| 14 | What Breathes Under the Roots | Main Story | Starts when the Heartroot rumour first comes up, after two extractions (built). Find the Guardian's Hollow. Carry an ember from your own hearth to it: the one thing the forest can't forget is what wakes the guardian. Bring it down. Bring the Guardian Heartroot home |
| 15 | The Road North | Main Story | Lay the heartroot at the fire. The twist. Troll Valley opens (built), and the first nature skill with it. Sigrid's horn sounds from past the troll country |

The ember is the Askeladden part: an ordinary thing that matters. It is a small craft at the campfire, and it gives the
fire a use in a raid.

### Haven quests in the first two hours

| Quest | Opens when | From | Asks | Pays with |
|---|---|---|---|---|
| Tools of the Clearing | The fire burns | The portal | A hatchet and a pick, made by hand | Hardwood and ore come home from the first raid |
| Strength for the Road | The fire burns | The portal, in Eirik's words | A stew and a drink | The Waterskin, and a meal that lasts into the raid |
| A Roof for the Work | The door wakes | The portal | The Workshop | Repairs, bandages and the recipes below |
| A Safe Place | The Workshop stands | The portal | A chest with something in it | What is in it can't be lost |
| Something to Fight With | The Workshop stands | The portal | A Knotted Club | A weapon that stuns, beside the one from the prologue |
| The Alchemist | Ingrid is home | Ingrid | Her job and the altar | Healing draughts, and a use for Cursed Essence |
| A Smith's Hearth | Bjarne is home | Bjarne | His job, the Smithy and one repair | Repairs at half cost, copper |
| The Woodcutter's Lodge | Tor is home | Tor | His job and the lodge | Planks, the Round Shield |
| Raise the Walls | The lodge stands: a palisade starts with a plank | The portal | Four palisades | Fewer attackers reach the ward |

## The first two hours, now and proposed

The proposed times are estimates from the measured step lengths. None of it has been played.

| Minutes | Now | Proposed |
|---|---|---|
| 0 to 10 | Errands, no fight in the story | Armed at 2, a fight on the road at 5, night at noon |
| 10 to 20 | The walk, shelter, fuel, fire, berries, wolves, the stones | Shelter and fire, the Liv moment, the camp defence, the troll, the hold. Home at about 13 |
| 20 to 30 | Home: sticks, tools, 24 oak and 12 stone | Fire, the three stones, the door. In the first raid at about 22 |
| 30 to 45 | The workshop, the chest, the club | Ingrid found and brought home. The first Haven quests |
| 45 to 60 | The stew, the stones, the repair | The second raid: Bjarne |
| 60 to 90 | The first raid | The first breach. The third raid: Tor, and the Hollow found |
| 90 to 120 | Home, the first breach | The Heartroot. The road north |

## What it takes to build

Three steps, each playable by itself.

1. **Categories and the open door** (mostly data). Two new `QuestRank` values with colours and tracker headings. Every
   quest re-ranked. Prerequisites cut so the Haven quests stand beside the story. The portal's repair gate asks only for
   the fire and the orbs. The prologue's weapon and armour come home. `RescueOrderTests`, `JournalTests` and the home quest
   tests change with it. This alone moves the first raid to about minute 30.
2. **The prologue** (the builder scripts and the scene). Chapters merged from 12 to 7 in `ForestPrologue.asset` and
   `BuildPrologueMeadow.cs`, the weapon choice moved to the morning, a fight added on the road, the Liv moment, Sigrid's
   hand on the stone, a bigger camp defence. `PrologueTests` changes. This is the riskiest step, since the prologue is the
   most hand-tuned part of the game.
3. **The first act's story** (new content). The three stones' lines and rootlings, the figure in raid 1, the homecoming
   memories, the Heartroot quest with the ember, the twist at the fire, the three endings by motive. All of it fits the
   story graph nodes and objective types that exist: Reach Place, Craft Item, Kill Monster, Collect Item.

After any step: update GDD §21 and the status table in `ARCHITECTURE.md`.

## As built: step 1 (2026-11-10)

`AgentScripts/BuildQuestCategories.cs` sets it up; `QuestCategoryTests` and `HomeStoryTests` hold it.

- **Kinds.** `QuestRank` has Rescue and Haven beside Main Story, Side Quest ("Tale") and Optional ("Errand"). At home six
  quests are gold: The One It Caught, A Light in the Wood, The Rootbound Stones, The Door Wakes, Into the Misty Woods and The
  Weakening Ward. The 11 rescues are teal and 20 building and job quests are green.
- **The door.** A Light in the Wood asks for the campfire only (8 Sticks, 12 Small Rocks). The repair still asks for three
  Life Orbs and nothing else. Gifts of the Clearing and They Are Still Out There are gone.
- **Haven quests.** Tools of the Clearing and Strength for the Road open with the fire. A Roof for the Work (new: the
  Workshop) opens when the door wakes, and A Safe Place and Something to Fight With follow it.
- **Gear.** What the player wears at the end of the prologue comes home, with their arrows. What they carried does not.
- **The portal** speaks for every open quest: the story's line first, and Listen goes through the others.
- **Into the Misty Woods** asks to find Ingrid and to extract once. Finding her is kept if the raid fails, but the quest waits
  for an extraction: the first breach, which follows it, is only offered after one.
- **Not checked by hand.** The flow was stepped in Play on throwaway saves by completing quests from the CLI: each step
  started what it should, the portal said the right lines, and gear given in the prologue arrived at home. Nobody has played
  it from New Game yet.

## As built: step 2 (2026-11-10)

`AgentScripts/BuildPrologueMeadow.cs` (`Data`, then `Moments`) sets it up; `PrologueTests`, `PrologueMeadowTests` and
`QuestGiverIntegrationTests` hold it.

- **Seven quests in nine chapters.** A Morning by the Fire, The Road East, Shelter from the Storm, A Moment of Warmth, Eyes
  Between the Trees, Something Is Coming, The Last Connection. Where the Sun Went, A Spark in the Dark, A Fire of Our Own and
  Wake the Waystones are gone. A chapter can now end at an objective (`PrologueChapter.EndsAtObjective`), so one quest runs
  through several chapters: Shelter from the Storm is the shelter, then the fire beside it; Something Is Coming is the
  flight, then the hand on the stone. A later part's line is said in passing, not as a briefing.
- **Sigrid briefs twice**, in the morning and before the road. Every quest after that starts by itself.
- **The weapon** is chosen when the player says they are ready, and the party sets off once it is in hand.
- **The first fight is at the cart, in daylight.** The boar run past it, and three wolves come after them.
- **A Name Gone Missing**: the rest opens with Sigrid counting the four who went ahead and losing Liv's name. Eirik admits he
  had to think for his sister's.
- **Berries for the Pot** is Eirik's errand during the rest. The wolves wait for whoever took it, two and a half minutes at
  most.
- **The camp is attacked twice**: three wolves, then three more once they have fallen (`PrologueChapter.Waves`).
- **At the stones** Sigrid lays her hand on the centre stone first, and nothing happens.
- **Played through in the Editor** on throwaway saves, as far as the arrival at home, stepping from the CLI with the player
  made invulnerable. The real fights were fought by the companions and the probe. Two fixes came out of it: the wolves
  arrived as Sigrid's lost name was being said (the rest's clock now waits for that scene), and two lines repeated
  themselves.
- **Not checked:** how long it takes a real player, and whether the two camp waves and the road wolves are too hard for a
  player who skipped Eirik's lesson. The target is 12 to 14 minutes without the errands.

## As built: step 3 (2026-11-10)

`AgentScripts/BuildFirstAct.cs` sets it up; `FirstActTests` holds it. All of it is quest data and story graphs under Home
Moments in SafeHaven, plus one rule in the quest code.

- **The roots move.** When The Rootbound Stones starts, the camera turns to the nearest stone and the portal says the roots
  are moving towards it (Story - The Roots Move).
- **The stones remember.** Each of the three stones by the portal says one piece of the pact as it is freed: the fire, the
  door, and the guardian under the roots (Story - The Stones Remember). The stones the world spawns elsewhere say nothing.
- **Up through the ward.** Once all three are free, two rootlings come up beside the portal (Story - Up Through the Ward).
- **The root in them.** After the first breach, the portal says the root that grew out of the dead is the same root, and that
  one thing pushes it (Story - The Root in Them).
- **What Breathes Under the Roots** (Main Story, after The Weakening Ward): make a Hearth Ember at a campfire (3 Sticks, known
  from the start) and bring down the Heartroot. Waking it costs the ember: it doesn't stir for steel. The briefing, the
  site and the choice say so.
- **A boss already beaten counts.** A Kill objective with Count Already Owned counts a boss whose defeat is saved
  (`QuestObjective.Owned`), so a player who beat the Heartroot first is not stuck. Checked in Play.
- **Its last word**, in the defeat line: "Hearth-keeper." The trophy line at home asks for it by the fire.
- **The Road North** (Main Story): the portal's conversation (Story - The Road North). The Heartroot was the forest's
  guardian; people let the hearth go cold first; a choice ("we broke the pact first" or "why didn't you tell me?"); a line for
  the reason the player gave in the prologue; its last word; and the road north, which the old troll walks. Troll Valley
  opens on the Heartroot's fall, as before.
- **What they remember.** Ingrid's welcome line: the troll came for the door, and something in Granheim mail walked past her
  and didn't know its name. Bjarne's: his blazes were on the wrong side of the trees, because the roots moved the road.
  Tor's: the hollow where the ground breathes.
- **Played in the Editor** on throwaway saves from the CLI: the roots line, the three stones' lines, the rootlings, the
  breach's aftermath, the hunt completing at once with the Heartroot already beaten, and the whole twist. The Heartroot was
  not fought: the waking in a raid has not been seen.

**Left out, for later:** the figure in mail and the troll's footfalls in the first raid itself (the figure is in Ingrid's
memory instead); a Reach Place step at the Guardian's Hollow; Sigrid's horn at the road north, which belongs to her rescue
(she comes home last).

The open questions below were answered with the recommendations: the errands stay as they are until step 2, the weapon is
kept, there is no chest before the first raid, the twist stands, and the Heartroot comes at raid three or four.

## Open questions

1. **The morning errands.** They were made a chain on 8 November. All three before the story is 9.5 minutes with one
   fight. Options: offer them all at once and let each take under 90 seconds; put the hunt first; or leave them, since
   they are optional.
2. **Keeping the prologue's weapon.** It makes the Knotted Club a choice and not a need. Is that acceptable?
3. **Going into the first raid with no chest.** The loss lesson then comes from the first return. Or should the chest stay
   before the portal, as the only building step?
4. **The twist.** People broke the pact first. Is that the story you want the later acts to stand on?
5. **How many raids before the Heartroot?** It is tuned against what a third raid's kit holds. This plan puts it at raid
   three or four.

## Sources

- [TechRaptor: Cult of the Lamb guide hub](https://techraptor.net/gaming/guides/cult-of-lamb-guides-guide-hub)
- [EarlyGuides: Cult of the Lamb beginner's guide](https://earlyguides.pages.dev/cult-of-the-lamb/beginners-guide)
- [Ludo: Darkwood, the initial crusade](https://www.ludo.guide/guide/cult-of-the-lamb/walkthrough/darkwood-dungeon-1-initial-crusade)
- [Inlander: Greg Kasavin on how deaths drive the story of Hades](https://inlander.com/culture/hades-writer-greg-kasavin-on-how-he-made-video-game-deaths-drive-a-feel-good-story-22725237)
- [Game Developer: narrative rewards in Hades](https://gamedeveloper.com/design/how-supergiant-weaves-narrative-rewards-into-i-hades-i-cycle-of-perpetual-death)
- [Deltia's Gaming: all the Flame quests in Enshrouded](https://deltiasgaming.com/all-the-flame-quests-in-enshrouded-guide)
- [Gamer Guides: how to unlock the Blacksmith in Enshrouded](https://gamerguides.com/enshrouded/guide/quests/blacksmith/how-to-unlock-the-blacksmith)
- [BisectHosting: V Rising castle and journal quests](https://bisecthosting.com/blog/v-rising-beginners-guide-castle-journal-quests-blood-weapons)
- [Steam discussion: V Rising's journal nagging](https://steamcommunity.com/app/1604030/discussions/0/3841053719669085615)
- [GuildOrder: V Blood bosses and progression](https://guildorder.com/games/v_rising/wiki/v-blood-bosses-and-progression)
- [Nodecraft: Valheim boss guide, Eikthyr](https://nodecraft.com/support/games/valheim/general/valheim-boss-guide-eikthyr)
- [XGamingServer: Valheim boss order](https://xgamingserver.com/blog/valheim-boss-order-all-bosses/)
- [Dragonwilds wiki: quests](https://dragonwilds.runescape.wiki/w/Quests)
- [Metabot: Dragonwilds quests](https://metabot.gg/en/dragonwilds/quests)
- [TheXboxHub: Gord review](https://www.thexboxhub.com/gord-review/)
- [God is a Geek: Gord review](https://www.godisageek.com/reviews/gord-review)
- [GamesRadar: "We don't do fetch quests"](https://www.gamesradar.com/games/the-witcher/the-witcher-4s-narrative-lead-is-holding-onto-some-of-his-earliest-witcher-3-feedback-for-the-rpgs-sequel-we-dont-do-fetch-quests)
- [Microsoft Research: The First Hour Experience](https://www.microsoft.com/en-us/research/?p=167075)
