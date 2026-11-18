# The prologue and the quest chain: a critique of the flow (2026-11-18)

The user: "do a critique on the prologue and quest chain. I feel the pacing is off. how can we make it more exciting, while at
the same time teaching the player the basic and setting up the lore? I want a really good Flow and a quest chain that keeps the
player wanting more!"

Read for this: the prologue's nine chapters (`Data/Prologue/ForestPrologue.asset`) and its eleven quests, every quest in
`Data/Quests` with its prerequisites and objectives, `Docs/RescueOrder.md`, the opening cinematic's script
(`Docs/BackstoryCinematic.md`), "The Catch" (`Docs/PrologueEndCinematic.md`), GDD §4 and §21, and the playtest logs since
2026-11-10. Only two human runs reach far into the prologue, so the times below are a sketch, not a measurement.

## 1. The flow as it plays now

| Minute (11-10 run, no errands) | Beat | What the player does | Feeling |
|---|---|---|---|
| −2:00 | **The Fire at the Stones** (painted, 1:35-2:00) | Watches: the pact, the hearth, the fire dying, the hunger | Calm, beautiful; every answer before any question |
| 0:00-1:00 | **A Morning by the Fire** | Talks to Sigrid, Eirik and Runa in turn | Calm |
| (1:00-8:00 with errands) | Wood, then fish, then the boar (chained); Eirik's lesson | Gathers 6 logs (3.7 min in the 11-13 run), catches 2 fish (1.9 min), hunts | Calm, chores. The 11-13 run stopped here, at minute 8 |
| 1:00-4:30 | **The Road East** | Walks at the party's pace while they talk; the day dies early; torches; wolves at the cart; the weapon | The best idea in the prologue (night at noon), reached by a 3-minute escort walk |
| 4:30-6:00 | **Shelter from the Storm** | Finds the shelter, gathers **8 sticks and 12 rocks**, builds a fire | A chore at the moment of greatest mystery |
| 6:00-7:00 | **A Moment of Warmth** | Talks; Sigrid can't remember Liv's name; berries for Eirik | The lore's best beat, quietly |
| 7:00-8:00 | **Eyes Between the Trees** | Two waves of wolves | Action |
| 8:00-9:30 | **Something Is Coming** | Walks the path north, touches the stone; Sigrid's hand fails first | Rising |
| 9:30-10:45 | **The Last Connection** | Holds the stones for a minute; the troll in the last 20 s; its blow | Climax |
| +2:00 | **The Catch** (painted, 1:52) | Watches: why the portal caught them, what to do | Calm, explanation |
| home | The One It Caught, **A Light in the Wood** (a campfire again), the Rootbound Stones (3), the Door Wakes | Listens, builds the same fire, frees three stones, opens the door | Calm, chores again before the first raid |

About 15 minutes of New Game pass before home, and four of them are painted narration. The redesign's target, first raid at
about 22 minutes, has not been reached by a human run yet.

## 2. What already works (keep it)

- **The premise.** An expedition goes in after four of its own; the three you walk with are three you will later have to find.
  Every companion is a future resident, which is rare and strong.
- **Night at noon.** The day running out hours early, the stars at midday, "the forest has moved". This is the hook.
- **The forgetting.** Sigrid can't remember Liv's name by the fire. It is the game's central idea (the forest doesn't kill
  the lost, it forgets them) shown, not told, and it lands in a quiet scene where it can.
- **The troll and the stones.** A slow, huge thing you hear before you see, a countdown, a blow that scatters everyone.
- **The fire as the theme.** The opening's first line ("A fire is a small thing, little one. Until it is all that stands
  between home and the dark"), the shelter fire, the Haven's hearth, the ember that wakes the Heartroot, "you kept their fire".
- **"The problem comes first"** for the rescues (`RescueOrder.md`): each person comes home when their trade is the answer to
  something the player has just felt.
- **The act ending.** The Heartroot is the forest's half of the pact, kept alone for ninety winters after people let the fire
  die: the player learns their own people broke it first.

## 3. Where the flow breaks

1. **The answers come before the questions.** The opening cinematic explains the pact, the ward, the hearth and the hunger
   before the player has a reason to wonder about any of it, and The Catch then explains the portal and the task before the
   portal explains them again at home. Four minutes of telling, and the prologue's own mysteries (why is it night, why can't
   Sigrid remember, what is walking towards us) are answered or overtaken before the player has asked them. GDD §4.2 already
   says the story must not lean on long cutscenes.
2. **The slowest minutes come first.** Nothing threatens the player until the cart, minute 4 to 5 without errands and 10 or
   more with them. Before it: talking, then chained chores (wood, then fish, then the boar), then an escort walk. The 11-13 run
   ended at minute 8, still at the morning camp. The first minute has to make a promise, and right now it makes porridge.
3. **The tension curve dips at its peak.** Night at noon, rain, mist, the forest moved: and the next instruction is "eight
   sticks and twelve small rocks". The order is mystery, chore, talk, fight, run, hold. The chore should be pressure, or it
   should go.
4. **The same lessons twice, the vital ones not at all.** The campfire and its sticks and rocks are taught in the prologue
   and again at home (A Light in the Wood). Parry and dodge, which the whole combat economy rests on (parry then backstab,
   the dodge refund), are in Eirik's lesson, which is an optional errand: a player can meet the wolves never having parried.
5. **The walk.** About 3 minutes at the party's steady pace. Walk-and-talk works for a minute; past that it's waiting.
6. **The four missing are abstract.** The player has never met Ingrid, Bjarne, Tor or Liv, so "find them" has no face.
   The tipped cart is their first trace, but it is set dressing, not a discovery.
7. **The home stretch repeats the prologue's chores** before the first raid: listen, fire, three stones, door, with tools,
   food and water beside. It is the second calm stretch in 25 minutes, right before the part of the game that is the game.
8. **After the first raid, the chain is one shape repeated.** The Lost Expedition is eleven rescues in a strict line, each
   "Find X in the Misty Woods" then "Bring X home safely", each followed by "Give X their work" and "Build Y". If the raid makes
   every rescue its own situation, the quest text doesn't say so; on paper it is the same quest eleven times. (Sigrid's rescue
   is still in the Misty Woods in the data, though Act II was to end with it in Troll Valley.)
9. **Few open loops.** After the stones the player knows what to do next, but rarely wonders what comes next. The troll that
   broke the stones isn't seen again until the road north opens; the old road "nobody knows where it goes" is never paid off.

## 4. A new flow

The rule for all of it: **every two minutes something changes** (a threat, a discovery, a choice, a reveal), **every lesson is
taught once, at the moment it is needed**, and **every reveal answers one question and raises the next**.

### 4.1 The prologue (target 12-14 minutes, cinematics included)

| # | Beat (target minute) | Exciting part | Taught | Lore set up |
|---|---|---|---|---|
| 0 | **Cold open (0:00-0:30).** In the game, not painted: rain at the stones, the trees bending away (TreeBender), the troll's footfalls, Sigrid shouting, the player's hand on the stone, a hand closing round the light. Black. *"That morning."* | The promise: this is where it is going | – | A giant, the stones, a catch. Three questions |
| 1 | **The morning (0:30-3:00).** Sigrid, Eirik and Runa met by doing, not by queueing: Eirik's spar is the introduction (strike, dodge, parry, a short duel: **mandatory, about 60 s**, framed as teasing) | A fight in the first minute, safe | Attack, dodge, **parry** | Who they are; the four ahead |
| 2 | **One provision (optional, 1-2 min).** Sigrid: "We leave when you're ready; bring something useful." One choice, not a chain: fish (a meal for the road), Runa's hunt (her spare bow and arrows), or firewood (torches that last in the storm). Each is a verb and a small advantage later | A choice that pays off | One of fish, hunt, chop | – |
| 3 | **The road (3:00-5:00).** 90 s of walking at most, and the player may run ahead. The day dies while they walk. The cart: **search it**, a personal trace of the four (Tor's plane, Ingrid's satchel with a draught in it, Liv's rubbing of a stone with the same rune as the waystones). Wolves come out after a fleeing boar | Night at noon, then teeth | Torch, picking up, the weapon | The four become people; the rune is the same one |
| 4 | **The storm and the fire (5:00-7:00).** The rain drowns the torches one by one (the wetness and torch systems): a fire, fast, before the last goes out, while eyes shine at the edge of the light. Half the sticks and rocks (4 and 6, as the GDD first had it) | Building as pressure, not a chore | Gathering by hand, the build menu | "A fire is all that stands between home and the dark", played |
| 5 | **By the fire (7:00-8:30).** The talk, short and honest. Sigrid can't remember Liv's name, and doesn't notice that she can't. The player may ask about the stones. The wolves cut it off | The forgetting, felt | – | **The forgetting**: question 2 |
| 6 | **The wolves (8:30-9:30).** Two waves; the parry the player learned is what wins it | Action that rewards the lesson | Using the parry under pressure | Something drives them |
| 7 | **The chase (9:30-11:00).** The troll walks, trees bend, the path north to the stones, the party running ahead and behind. Sigrid's hand fails on the stone, the player's doesn't | Flight | – | The stones answer the player, not Sigrid: question 3 |
| 8 | **The hold and the blow (11:00-12:30).** The minute at the stones, the troll in the last 20 s, the blow. The cold open again, now understood | The climax, set up at minute 0 | – | – |
| 9 | **The catch (12:30-13:00).** 30-45 s: the light, the hand, the clearing, "wake up". The explanation moves to the portal, in the player's own time | – | – | Who caught them, nothing more |

**The Fire at the Stones** (the user's script) is the right story told at the wrong moment: it answers everything the
prologue is about to ask. The proposal is to keep it, word for word, and **move it to the moment it answers**: the portal tells
it at home when the player lights the Haven's fire (beat 1 of 4.2), or it is offered once from the journal after the
prologue. A player who has just lived the night, the forgetting and the fire hears "your people made a bargain with the woods"
as the answer they were waiting for. (Option B in §6 keeps it as the opening.)

### 4.2 Home to the first raid (target: first raid by minute 20)

1. **The fire you kept.** No second campfire lesson: the player wakes holding an ember from the shelter's fire (it came
   through the catch with what they wore). They set it in the cold hearth, and the portal wakes. *Here* the portal tells the old
   story (the painted cinematic, or its words over the clearing): the pact, the hearth, why no fire burned for a hundred
   winters, why it caught this one. "You kept their fire" is earned, not announced.
2. **The three stones, with resistance.** The roots fight back: a rootling bursts from each stone, so freeing them is a short
   fight per stone, not three clicks. The Life Orbs drift to the door on their own.
3. **The door wakes, and Sigrid's voice comes through.** As now: Ingrid is hurt and close, find her first.
4. **Tools by need, not by list.** Into the Misty Woods opens at once; the hatchet and pick are taught on the way out (the
   portal: "you will want an edge out there") and the food and water quest moves after the first raid, when hunger has been
   felt (the rescue rule, "the problem comes first", applied to the player's own needs).

### 4.3 The chain after the first raid: keeping them wanting more

1. **Every rescue its own situation.** Same objective types, different stories in the raid, chosen so each is a different
   verb, and most are about the forgetting:
   - *Ingrid*: hurt, under a fallen tree, must be carried out while wolves follow (escort under pressure).
   - *Bjarne*: forging alone in a ruined camp, and he doesn't know who you are. Bring him **his hammer from the cart** and he
     remembers (the forgetting, solved by a thing: the Askeladden "worthless object" rule from GDD §4.2).
   - *Tor*: shut in a root cage; free him with an axe while rootlings come (a timed break-out).
   - *Liv*: copying a carved stone deep in the woods; she won't leave until the rubbing is done (defend her for a minute),
     and it is the rubbing that later opens enchanting.
   - *Eirik*: holding a choke point alone; arrive and fight beside him (a stand).
   - *Runa*: tracking something that is tracking her; find her by her marks (a trail to read).
   - *Sigrid*, last, in Troll Valley as decided: she remembers nothing, not even the expedition, until the player brings
     every name home (the act's emotional payoff).
2. **Always one open loop on the horizon.** At home the player should always see the next big thing before they can reach
   it: the road north behind the ward from day one, the troll's footprints by the river, a light in the Moss Marsh at night.
   Each homecoming line ends on a question ("the hammer was warm when I found it, as if someone had just put it down").
3. **A cadence.** Alternate small and large: rescue, home change, rescue, **story event** (a breach, a reveal, a boss), and
   never more than two "build X" steps between events (the user's rule from 2026-11-10: main quests are events).
4. **Pay off the old road.** The road "nobody knows where it goes" goes to the portal: the Haven was its end. A carved road
   stone found near home in the first days says so.
5. **Reveals on a schedule.** One question answered per act step, one raised:

   | When | Answered | Raised |
   |---|---|---|
   | Prologue | – | Why night at noon? Why can't Sigrid remember? What walks? |
   | The fire at home | The pact; the forgetting; why the portal caught you | Who let the fire die? |
   | Raid 1 (Ingrid) | The four are alive and the forest moved them | Who carved fresh marks on old stones (the man in grey)? |
   | Bjarne's hammer | Things remember what people forget | Why did the stones answer you? |
   | The Heartroot | People broke the pact first; "Hearth-keeper" | What is the hunger the guardians held back? |
   | The road north | The troll that broke the stones | Who sent it? |

## 5. How we know it worked

- **Targets:** something changes at least every 2 minutes in the prologue; the first fight before minute 1:30; the first
  threat with stakes before minute 5; the prologue done by minute 13-14; the first raid by minute 20; never more than two chore
  objectives in a row on the main path.
- **A pacing report from the playtest logs:** the logger already writes QuestStarted and QuestCompleted with minutes. A small
  tool can draw each run's beats as a timeline and flag any gap over two minutes without a new beat, and where runs stop. That
  makes "the pacing is off" a number after each playtest.
- **Play it by hand.** None of the prologue's current flow has been played by hand end to end since the redesign
  (`Docs/TODO.md`); the next build should be.

## 6. Decisions for the user

1. **The opening cinematic:** (A, recommended) open with the in-game cold open, and the portal tells The Fire at the Stones at
   home when the first fire is lit; (B) keep it first, followed by the cold open; (C) keep it as it is.
2. **Eirik's spar mandatory** (about 60 s, it teaches the parry), and the morning errands made **one choice** of three instead of
   a chain? Recommended: yes to both.
3. **The ember from the shelter fire** lights the Haven's hearth, so the home campfire lesson goes? Recommended: yes.
4. **Rescue situations:** design each rescue as its own situation (§4.3.1), starting with Ingrid, Bjarne and Tor, the three
   most players will meet? Recommended: yes, three first, then play them.
5. **Sigrid's rescue** moved to Troll Valley, as decided for Act II, and made the act's emotional ending? Recommended: yes,
   when Act II is built.
