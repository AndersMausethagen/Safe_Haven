# Questline Audit

## 1. What story the player experiences

### Takeaway
The current playable opening is built around a chain of observable mysteries: an unnatural loss of daylight, a road that vanishes, Sigrid forgetting Liv, and ancient stones that answer only the player. The post-prologue cinematic and Portal’s home dialogue then explain the immediate disaster and give the player a durable purpose: restore a hearth, make a home, and bring the seven scattered people back. The first act turns the apparent “hostile forest” premise into a story of a broken reciprocal pact and an inherited debt.

### Cited Findings
- The prologue’s approved design plants three questions in the first 15 minutes: where the sun went, what the forest is doing to people, and why the stones answer the player but not Sigrid. It sets the old troll’s objective as the stones/door, not simply the expedition. — [QuestRedesign.md §“The story of the first act,” lines 106–140](../../Docs/QuestRedesign.md)
- In `Prologue_MistyWoods.unity`, Sigrid says she sent Ingrid, Bjarne, Tor and Liv two days earlier to investigate the forest and old road (around line 28561). The walk east then loses daylight far too early (lines 24989, 25327), and the road disappears behind the party (line 68107).
- At the campfire, Sigrid counts “Ingrid, Bjarne, Tor and...” and cannot retrieve the last name; Runa says “Liv,” and Sigrid recognizes that she had known Liv for eleven years (around lines 102979–102994). Eirik later admits he also had to think for his sister’s name (QuestRedesign.md “As built: step 2”).
- Sigrid asks the player why they came; the choices are “For the coin,” “Someone I know went in there,” and “To see if the stories are true” (`Prologue_MistyWoods.unity`, around line 28627; three response branches follow). Choice-specific dialogue is revisited in Home and in the Heartroot aftermath (`SafeHaven.unity`, around lines 314757–314799 and 393672–393729).
- At the stones, Sigrid’s touch fails; she asks the player to try, then explains that Ingrid knew the stones could transport those standing near them (`Prologue_MistyWoods.unity`, around lines 37023–37074). The player holds while the troll breaks the stones; it is described as walking toward the stones and later coming “right for the stones” (lines 40451, 86081).
- “The Catch” plays after the prologue. It explains that the Portal caught only the player, that seven others remain alive and scattered, and that a tended hearth can be seen through the mist. Its final instruction is to build them a home and return for them. — [PrologueEndCinematic.md §§1–2](../../Docs/PrologueEndCinematic.md)
- At home, “The One It Caught” repeats that seven sparks are alive, the forest forgets the lost, and the Portal is a door with a weak ward after a hundred winters without fire (`SafeHaven.unity`, around lines 491798–491909). The choice-led “The Waking” story explicitly branches by coin, kin or truth (around lines 314757–314799).
- The opening cinematic was replaced on 2026-11-16 with “The Fire at the Stones.” Its notes say it is built, 2:06 long versus its 1:35 target, and had been checked only in captured frames, not watched in motion or heard. It depicts the hearth/pact/ward, the cold fire, changing road and name, Portal’s sleep, the first four, then the player’s group and eastward road. — [BackstoryCinematic.md, status and script/As built](../../Docs/BackstoryCinematic.md)
- First raid entry has its own 54-second “The Misty Woods” cinematic: beyond the Ward the woods are fogbound and grow more dangerous; the player is there for resources/prisoners, but must return before the forest claims them. — [MistyWoodsCinematic.md §§1–3](../../Docs/MistyWoodsCinematic.md)
- The player rescues Ingrid first, followed by Bjarne, Tor, then later Eirik, Yrsa, Gunnar, Liv, Asa, Runa, Hild and finally Sigrid; each person resolves a problem the player has just felt and unlocks a role/workplace. “The Lost Expedition” closes when Sigrid returns. — [RescueOrder.md §§“The rule,” “The order,” lines 6–48](../../Docs/RescueOrder.md)
- The first act escalates from clearing the stones and repelling a breach to the Heartroot. Its roots are linked to the road closure, roots choking the Portal stones, Root Guardians and breach growth. The player carries a Hearth Ember, wakes it, defeats it, and brings the root home. The Portal names it as the forest’s guardian, reveals that people broke the pact first, and calls the player “Hearth-keeper.” The old troll’s route north opens afterward. — [QuestRedesign.md §§“The story of the first act,” “As built: step 3,” lines 106–140, 283–317](../../Docs/QuestRedesign.md); actual dialogue in `SafeHaven.unity` around lines 393588–393744.

### Inferences
- The current experience has a strong “mystery → lived evidence → explanation → moral reversal” structure. The intro now plants the pact, while the prologue gives the player direct evidence of the forest’s effect before the Portal explains it.
- The individual rescues provide a second, slower story loop: each found person both returns a face to the community and converts a felt survival/crafting constraint into a new capability.

### Gaps
- The quest redesign says no one has played the whole revised opening from New Game by hand; the new 2:06 opening is also explicitly unwatched/unheard in its current build notes. Player comprehension, emotional pacing and whether the repeated explanations feel satisfying remain unverified.

## 2. What works, what is unclear, and continuity constraints

### Takeaway
The strongest material is the concrete name-loss beat, the repeated fire/door/root symbols, and the Heartroot reversal. The main continuity risk is front-loading the answers or assigning player motives in the cinematic: later quests depend on players witnessing Liv’s name vanish, choosing their own reason for coming, and discovering that people broke the pact before the forest’s guardian did.

### Cited Findings
- The project explicitly frames the first act around three setup questions and the evidence/payoff order: early noon-to-night change; Sigrid’s failure to remember Liv, followed by the Portal’s explanation; Sigrid’s failed touch before the player’s successful one. — [QuestRedesign.md lines 111–119, 257–281](../../Docs/QuestRedesign.md)
- The “Stones Remember” scene delivers one pact fragment per freed Portal stone—fire, door, guardian under roots—then rootlings emerge through the ward. A later scene links the root from a fallen breach attacker to the Portal roots and road closure. — [QuestRedesign.md “As built: step 3,” lines 283–294](../../Docs/QuestRedesign.md); `SafeHaven.unity` around lines 35317, 51540–51555, 88725–88781.
- The Heartroot reveal is deliberately withheld: it is a guardian, not a monster; it kept its half of the pact for ninety winters after the hearth went cold; the humans broke the pact first. The player may react with “we broke the pact first” or “why didn’t you tell me?”; the answer varies with the prologue motive. — [QuestRedesign.md lines 124–140, 301–317](../../Docs/QuestRedesign.md); `SafeHaven.unity` lines 393603–393729.
- “The Catch” is intentionally not to explain why the troll came, who let the hearth go cold, what the guardians became, or why the stones woke for the player. Those are reserved for Ingrid / Act I / later uncertainty. — [PrologueEndCinematic.md “What it leaves out on purpose,” lines 28–34](../../Docs/PrologueEndCinematic.md)
- The Portal repeats key information across the end-cinematic, first home conversation and first-fire story: it caught one; seven are alive; fire feeds the ward and gives the lost a way home. The cinematic notes explicitly call this repetition out as something to judge in play. — [PrologueEndCinematic.md “What it repeats,” lines 36–40](../../Docs/PrologueEndCinematic.md); `SafeHaven.unity` lines 491798–491909 and 83141–83156.
- A third milestone at home teaches the wider geographic purpose of the pact: the player can light outlying stones; a lit stone marks land where nobody loses the way home or their name, and a ring of held stones keeps the forest from taking it back (`SafeHaven.unity`, around lines 172823–172946 and 284875–284890). This goes beyond the short opening and is an important connective rule for later acts.
- The rescue-order document labels all 14 rescues “built,” but also states the sites are not yet tested in a real raid and four new rescue locations are placeholder shapes; the First Act doc similarly says the Heartroot was not fought in the validation run. — [RescueOrder.md lines 1–4, 69–82](../../Docs/RescueOrder.md); [QuestRedesign.md lines 305–317](../../Docs/QuestRedesign.md).

### Inferences
- The opening should establish the reciprocal rules and recurring symbols, not explain the full political/moral history. In particular, keep “the fire went out” unassigned; do not imply that the forest is simply evil; do not resolve why the player is chosen.
- The name-loss scene is the emotional proof of the memory curse. If the opening explains the mechanism in too much detail, the campfire scene can feel like a repeated example instead of a disturbing first discovery.
- The four missing people should be visually distinct but not introduced by names too early: the scene and later dialogue already make their names matter, especially Liv’s. The second group’s motivation must stay player-authored because the game branches on it.
- The root recurrence is an effective visual clue, but the story should distinguish “forest guardians” as the guardians’ pact role from the post-corruption Root Guardians / Heartroot. Otherwise, the player may infer that the old guardian was evil from the start.
- There is a possible timeline ambiguity to audit: the new opening says a hundred winters pass before the Portal sleeps; home dialogue says the hearth went cold a hundred winters ago; “The Catch” says the Portal had slept a hundred winters; and Heartroot held its half for ninety winters after the hearth went cold. These statements may be intended as rough or overlapping durations, but read literally they imply different intervals and should be reconciled or made deliberately approximate.
- The main cinematic is 2:06 although its script targets 1:35. This is a practical pacing risk before a 12–14 minute prologue, especially as The Catch and the first-raid intro follow it later.

### Gaps
- Keep the counts distinct: “The Catch” says seven people are scattered (the four sent ahead plus the three companions); the rescue-order table has 14 progression rows, not 14 unique people, and later adds survivors beyond that original group. A full-story pitch should distinguish the seven scattered from the broader community recruits rather than treating every rescue as one of those seven.
- The state of the Portal, ward and outlying pact stones across the two centuries/century references is not fully reconciled in the cited story notes.
- Long-term consequences of rescue outcomes (lost companions waiting in raids, final reunion, or failed/abandoned rescues) are described partly as rules but not yet as a complete narrative arc.

## 3. Implications for a complete storyline proposal

### Takeaway
A complete pitch should preserve the strong first act, then make the wider campaign answer the next questions already promised by the game: why the old troll attacked the door, what caused the hunger in the roots, what the old pact actually demanded, and what kind of renewed relationship the player wants between people and forest. The current GDD sketches these acts but does not yet settle their causal chain or ending.

### Cited Findings
- The GDD’s Act I is “Find a home”; Act II gathers community, investigates lost villages and learns what fractured the pact; Act III faces or bargains with ancient guardians and the force consuming the forest. End choices are meant to affect settlers’ relationship with spirits, and the story should avoid long exposition cutscenes. — [GameDesignDocument.md §4.4–4.5](../../Docs/GameDesignDocument.md)
- The player’s core fantasy is caretaker/explorer rather than conqueror; “home is worth protecting,” raids should create small fairy-tale stories, and the community should grow one person at a time. — [GameDesignDocument.md §§1.2–1.4 and 2.1–2.5](../../Docs/GameDesignDocument.md)
- The troll is explicitly set up as an intentional pursuer of the “door” and intended Act II boss in Troll Valley, while the Heartroot ends Act I. — [QuestRedesign.md lines 120–136, 301–304](../../Docs/QuestRedesign.md)
- The visible spine of progress is the portal’s tier; outlying ward land erodes only under blight; raids and defense events need warning/telegraph; no surprise offline attacks or total settlement wipe. — [GameDesignDocument.md §6.3–6.7 and v0.5 notes](../../Docs/GameDesignDocument.md)
- The game’s first raid and later raid stories support folklore with motives, bargains and choices rather than treating every creature as a generic enemy. The Misty Woods first-visit cinematic presents its inhabitants as dangers in the fog, while the GDD narrative-tone rules explicitly call for beings with motives beyond hostility. — [MistyWoodsCinematic.md §§1–3](../../Docs/MistyWoodsCinematic.md); [GameDesignDocument.md §4.2](../../Docs/GameDesignDocument.md)

### Inferences
- A coherent full story can be staged as three moral turns: (1) build the refuge and save the first people; (2) learn the refuge is a debt and discover the troll sought the door; (3) uncover the force that corrupted the guardian and choose whether to renew, renegotiate or end the pact. The endings should resolve the conflict through an act that reflects the player’s choices and community, not simply a larger boss kill.
- The first act can remain a tragedy with an apology (“someone had to stop it”), while later acts offer a real alternative to killing/boss progression through bargains, restoring a corrupted guardian, or changing the boundary pact. This would pay off the GDD’s non-hostile folklore intent.
- Rescue arcs should converge on Sigrid’s return, then let her distinguish the expedition’s goal from the player’s own reason. Her horn and leadership can make the rescue order climax emotional, while the player chooses the final pact’s terms.
- The story’s recurring evidence system should be explicit: fire → Portal/ward; road/name → forgetting; roots → Heartroot/corruption; three old stones → pact; horn/light → reunion. Each new lore line should answer a question the player has already experienced.

### Gaps
- GDD outlines Act II and Act III but does not identify the final consuming force, the reason the old troll wants the door, the exact breach between the old pact’s two halves, or a canonical ending. These remain proposal territory rather than current canon.
- No full quest chain beyond “The Road North” has been established in the sources reviewed here; avoid describing a proposed Act II/III as already built.
