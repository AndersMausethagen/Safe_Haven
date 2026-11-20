# Cinematic Lore and Visual Storytelling Audit

## Current opening and visual storytelling

### Takeaway
The new opening, **“The Fire at the Stones,”** is a substantial improvement in lore clarity over the retired “The Old Story”: it shows the pact as a reciprocal system (human hearth → Portal ward → protected homes, with forest guardians holding back an older danger), then links the hearth’s death to weakening protection and the forest’s forgetting. The 11-shot structure is built and integrated, but the project notes say the full cinematic has not yet been watched or heard in motion; its actual pace and emotional effect remain unreviewed.

### Cited Findings
- `Docs/BackstoryCinematic.md` now documents the 2026-11-16 opening, replacing the 12-shot, 1:56 “The Old Story.” The current script has 11 scenes: small fire, bargain, ward, years, dead fire, changing road, erased name, Portal’s clearing/sleep, first four, four followers, and road east. It runs before the prologue and can be replayed from the journal/main menu. [Docs/BackstoryCinematic.md §§Status, The script]
- The current narration explicitly states the reciprocal pact: people leave room for the woods; guardians hold the older hunger; a tended hearth feeds the Portal’s ward; the ward protects homes. It avoids assigning blame for the hearth going dark and does not explain why the stones wake for the player. [Docs/BackstoryCinematic.md §§2–5]
- The visual assets are a consistent storybook-painting set: the fire and ward, guardians at the treeline, seasonal settlement, dead hearth, root-covered road, obscured door mark, isolated clearing, four distinct lost-person silhouettes, and the player’s four-person party. The art is mainly warm amber/green against cold blue/teal mist, consistent with the GDD’s visual identity and palette. [Assets/_Project/Art/Cinematics/Backstory/o01_small_fire.png; o02_bargain.png; o03_ward.png; o04_summer.png, o04_autumn.png, o04_winter.png; o06_road.png; o07_name.png; o08_clearing.png; o09_first_four.png; o10_who_follow.png; Docs/GameDesignDocument.md §§19.1–19.3]
- The planned “fire dies” image model kept painting flames even when asked for a cold hearth. The current implementation therefore layers a separately made unlit hearth over the painted shot as the ember fades. This is a practical workaround; the underlying `o05_fire_dies.png` still contains the bright hearth. [Docs/BackstoryCinematic.md §As built, Shot 5; Tools/LocalSpawn/Backstory/o05_fire_dies.png; Assets/_Project/Art/Cinematics/Backstory/o05x_cold_hearth.png]
- The built art notes say the height marks in the doorpost image do not read clearly; the four lost travellers have three lanterns because the staff-bearer carries none; the final scene uses a capture of the game’s actual first view as a reference. [Docs/BackstoryCinematic.md §As built, Shots 7, 9–11]
- The intended cut is 1:35; the current cut is 2:06, with 62 seconds of narration at about 190 words per minute and carefully arranged rests for the game’s violin phrase. The note records captured frames of every shot, but explicitly says the full motion and audio have not been reviewed. [Docs/BackstoryCinematic.md §As built]

### Inferences
- The new version’s main strength is causal legibility: it establishes what each side of the pact does, rather than only describing a lost village. Repeated hearth/stone/rune imagery should make later ward quests easier to understand.
- The lanterns, road, name-mark and hearth form useful visual motifs that can recur in the prologue and raids. The art does not need more written lore in every scene; repeated images and a clear later payoff can carry it.
- The “lost are still out there” line is useful because it counters an assumption that the vanished expedition is dead. The four silhouette props can support recognition without revealing their names before the questline does.
- As the opening is now 31 seconds longer than its target, long holds intended for music may feel slow in sequence. A motion-and-audio review should decide whether to shorten it; the written duration alone is insufficient evidence of pacing quality.

### Gaps
- The project note says nobody has yet watched/heard the full new opening. There is therefore no user/playtest evidence about comprehension, pace, performance, or whether the “old hunger” reads clearly.
- The current art suggests a well-kept village beside the Portal. How long and how widely that settlement existed is not visually specified; avoid making it look like a large thriving town if later lore needs isolation or collapse.
- The final “broad shadow” is a visual foreshadow. It has not yet been verified in motion to read as a disturbance directed toward the standing stones rather than a generic wipe or eclipse.

## The Catch and The Misty Woods

### Takeaway
The prologue-end cinematic and first-raid cinematic turn the opening’s symbols into immediate instructions: the player was caught, seven people remain alive, a hearth is visible through the forest, and a raid is dangerous because the woods worsen and attract stronger threats. These sequences have strong emotional images, but the home conversation repeats some of “The Catch,” and the raid cinematic’s menace risks making the forest seem uniformly evil despite the GDD’s more nuanced folklore tone.

### Cited Findings
- **“The Catch”** is built as a 1:52 bridge after the prologue. It shows the hand waking the stones, the old troll approaching, the stones breaking, seven people scattered, the Portal catching one, the dark clearing, and the need to light a hearth and bring the others home. The doc says screenshots and an Editor run were checked, but nobody had listened to it. [Docs/PrologueEndCinematic.md §§Status, The frame, The shots, As built]
- “The Catch” intentionally withholds why the troll came, who let the hearth go cold, why the player woke the stones, and the names of the seven. It notes that the first Portal conversation at home repeats “I caught you. I could only catch one,” the seven sparks, and “So light one”; the doc says that conversation may be trimmed if the repeat feels excessive. [Docs/PrologueEndCinematic.md §§What it leaves out on purpose, What it repeats]
- In the built version, the Catch’s narrator voice is documented as `portal_f_british_northern` through one acting model. The current opening doc instead documents `portal_g_mysterious` via Qwen3-TTS and says it uses the Portal’s dialogue voice. The older Catch note says the opening used a different voice model and asks whether the two sound like one narrator. Voice continuity across these documents needs verification after the opening revision. [Docs/PrologueEndCinematic.md §As built; Docs/BackstoryCinematic.md §As built, Voice]
- **“The Misty Woods”** is built to play once on the first raid, before the raid clock starts. It is 54 seconds long (34 seconds speech), with a cold six-shot sequence: ward boundary, swallowed woods, increasing corruption, resources/prisoners, an unseen large threat, and the route back to Haven. It gives lore rather than explaining the timer/UI. [Docs/MistyWoodsCinematic.md §§Status, The frame, The shots]
- The raid cinematic’s own unresolved review list says speech pace, music/effect levels, and whether 54 seconds is too long against the initial 26-second sketch have not been judged by ear and eye. [Docs/MistyWoodsCinematic.md §Left to judge by ear and eye]
- The GDD says the forest should be beautiful and unsettling rather than uniformly bleak; folklore beings should have motives beyond hostility, and a troll may demand a toll or a spirit may bargain. Wonder, warmth, cleverness, and quiet domestic life should coexist with danger. [Docs/GameDesignDocument.md §§2.4–2.5, 4.2, 19.3]

### Inferences
- The Catch has a strong emotional center in “I caught one. I could only catch one.” Its nine-shot sequence can be tightened around that moment, since the home conversation already has to establish the quest and explain the fire.
- The two intro pieces should have separate jobs: “The Fire at the Stones” establishes the old pact and the missing-person problem; “The Catch” explains the specific prologue disaster and gives the player a home-building objective; the first raid cinematic should make the woods feel alluring and strange as well as dangerous.
- The opening and Catch descriptions are currently out of sync about the narrator’s voice model. This is likely documentation/implementation drift rather than a confirmed in-game mismatch, but it should be checked before voice production is considered final.
- The Misty Woods cinematic’s “claims you” language and escalating threat montage communicate risk efficiently, but a small beat of beauty, bargain, or curiosity would better match the world’s storybook identity and make future spirit/non-hostile encounters feel earned.

### Gaps
- The project notes do not confirm whether the new opening’s voice clips are sonically continuous with “The Catch,” the home Portal dialogue, or “The Misty Woods.”
- No full audio/visual review is documented for the Catch or Misty Woods sequences. Current status supports “built and integrated,” not “the pacing and mix work.”
- It is unclear whether the introductory Catch is skipped on loading a save resumed at the end of the prologue; its notes say this may happen each time the prologue ends. That repeat condition could affect the perceived redundancy with the home conversation.

## Lore continuity, strengths and improvements

### Takeaway
The cinematics now have a promising visual spine—fire, ward, road, names, roots, lanterns—but the full game’s strongest lore is revealed through quests, not cutscenes. The clearest improvement is to make each cinematic plant a visual question that a specific quest later answers, while preserving the first-act reversal that humans broke the pact first and the open question of why the stones chose the player.

### Cited Findings
- The quest redesign defines three early questions: why the sun goes dark at noon, what the forest does to people, and why the stones wake for the player. The prologue pays off the memory question by having Sigrid forget Liv’s name and Runa restore it; it pays off the chosen-player question by showing Sigrid’s hand fail before the player’s works. [Docs/QuestRedesign.md §The story of the first act]
- The first-act reveal identifies the moving road roots, roots choking the three home stones, Root Guardians and Heartroot as connected. The Heartroot was the forest’s guardian; it kept its half of the pact for ninety winters after people stopped keeping theirs. The player is called “Hearth-keeper,” while why the stones chose them remains open. [Docs/QuestRedesign.md §§The story of the first act, As built: step 3; Docs/GameDesignDocument.md §4.4]
- The opening’s current broad shadow at the end could plant the “sun went into the Heartroot’s canopy” payoff, but the actual boss explanation says this is learned at the end of Act I. The visual can foreshadow the anomaly without naming or explaining its cause. [Docs/BackstoryCinematic.md §As built, Shot 11; Docs/QuestRedesign.md §The story of the first act]
- The cinematic’s current voiceover and the three home stones directly establish hearth/fire, door/ward and guardian/roots as lore elements. The questline explicitly has each of the three stones remember one of those three pact fragments. [Docs/BackstoryCinematic.md §§2–3; Docs/QuestRedesign.md §As built: step 3]
- The first-raid cinematic’s subjects are resources and prisoners, but the broader quest line aims for rescued people with names, trades, memories and distinct uses at home. Ingrid, Bjarne and Tor each offer personal memories; the later rescue order makes each person answer a need the player has just felt. [Docs/MistyWoodsCinematic.md §The shots; Docs/QuestRedesign.md §Raids one to four; Docs/RescueOrder.md §§The rule, The order]

### Inferences
- A consistent “lore ladder” will improve comprehension without adding exposition: **Intro** shows the hearth’s reciprocal function and traces of forgetting; **prologue** lets the player experience the sun failure and Sigrid’s missing name; **home** teaches the fire/ward rule; **three stones** recall the pact; **Heartroot** reframes the apparently hostile forest as a guardian still keeping its side of an old debt.
- Let the first four’s distinctive tools recur when rescued, in their journal entries and in the memories they tell at home. This gives the player a visual recognition payoff while keeping the opening’s silhouettes unnamed.
- Make roots a repeated but changing sign: roots closing the road, roots tightening around the stones, rootlings after the ward, roots in Root Guardians, then Heartroot. The current design already supports this chain; ensure the cinematic shots do not present the forest as a single evil force before the reveal.
- Keep three questions intentionally unanswered until their planned moments: why the old troll targets the door; why the stone chooses the player; and the old humans’ responsibility for the fire going out. Resolve them at Ingrid’s testimony, the late Heartroot scene/“Hearth-keeper,” and the Road North conversation, respectively.
- Use the raiding cutscene to offer one seductive detail (a light that answers, a strange offering, or evidence that an enemy is guarding something) before its threat escalation. That supports the GDD’s folklore/bargain tone without weakening extraction stakes.

### Gaps
- The available cinematic docs document the opening, prologue-end, and first-raid sequences, but they do not provide an ending for the whole multi-act game. A final saga ending must be identified as a proposal, not existing canon.
- The visual documentation of the three-stone iconography and its match to exact later quest dialogue has not been inspected in a rendered playthrough.
- The project’s cinematic notes describe several unrehearsed voices and mixes. Do not infer a polished narrative experience from “built” status alone.
