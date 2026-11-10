# Backstory cinematic: "The Old Story"

**Status:** draft for review, 2026-11-10. Nothing is built yet: no art, no voice, no Unity components.

A painted, narrated opening of about 1 minute 45 seconds. Twelve shots, each a still painting cut into layers and
moved by a slow camera, tell what happened before the game starts: the pact, the hearth, the winter the fire went out, what the
forest does to the lost, and why the player is walking towards it.

## 1. The frame

| | |
|---|---|
| **Plays** | On New Game, before the meadow morning. Replayable from the journal's Backstory tab. Hold a key to skip. |
| **Narrator** | The portal, in first person, speaking to the player ("little one"). It never names itself. The player recognises the voice at home, when the stones say "Listen, little one. Rest your hand on the stone." |
| **Length** | About 1:45 as written (173 spoken words). Real shot lengths come from the recorded lines. |
| **Ends on** | The expedition's morning fire in the meadow, which is where the game begins. |

### What it leaves out on purpose

The first act has reveals this must not spend:

- **Who let the fire go out.** The narration says only "the fire went out". The portal admits at the end of Act I that
  people broke the pact first.
- **What the guardians became.** They appear twice as silent figures, once whole and once overgrown. Nothing is said.
- **The old troll** that breaks the stones, and **the stones breaking**. The prologue plays that.
- **The names of the lost.** The four are told apart by their silhouettes only.
- **Why the stones wake for the player's hand.** The portal itself doesn't know.

## 2. The voice

**Direction.** Old, low and unhurried. Warm, but worn thin, like someone who has not spoken aloud for a long time. It
speaks to one person sitting close, not to a hall: no booming fantasy narrator. Short pauses between sentences, longer
ones where marked. A small warmth on "little one".

**How it is made.** Text-to-speech on this PC, with Qwen3-TTS (Apache 2.0, so a sold game may use it):

1. The *VoiceDesign* model makes a voice from a written description. No real person is cloned, so there is no
   question of whose voice it is.
2. The *Base* model then reuses that one designed clip as its reference for every line, so all lines share a voice.

It would be added to LocalSpawn as a third audio kind, `voice`, beside `sfx` and `music`, running in its own Python
3.12 environment because the model's packages don't fit ComfyUI's Python 3.13 one. This is not installed or tested
yet, and nobody has heard it: the first step is three test voices reading shots 1, 5 and 6 for you to choose from.

Test voices to generate:

- **A. Old woman.** "An old woman's voice, low and dry, warm, slightly hoarse, slow and calm, telling a folktale to
  a child by a fire. Neutral English accent."
- **B. Old man.** "An old man's voice, deep and soft, tired but kind, slow and calm, telling a folktale to a child
  by a fire. Neutral English accent."
- **C. Ageless.** "A soft, low, ageless voice, neither clearly male nor female, calm and distant, a little hollow,
  as if spoken from inside stone."

My pick before hearing them is A: a grandmother's hearth-side voice fits the folktale tone and avoids the stock deep
male narrator. That is a guess until the clips exist.

## 3. The look

- **Style:** LocalSpawn's "Safe Haven - Home" style for the warm shots and "Safe Haven - Raid" for the cold ones,
  1920x1088, with one added line for every painting so they read as a set: *storybook gouache illustration, visible
  brushwork, soft edges, small figures under very large trees*.
- **Colour tells the story.** Pact and hearth: fresh green and amber. After the fire goes out: cold blue, mist, no
  warm light anywhere. The last two shots: sun again.
- **The portal stones** are painted from a render of the in-game model, so the stones in the paintings are the ones
  the player later stands beside.
- **Figures** are small, cloaked, and seen from behind or the side. The player is always from behind and hooded.
- **Layers.** Each painting is cut into 3 to 5 layers (sky, far, middle, subject, foreground) placed at different
  distances from the camera. A camera move then makes near layers slide faster than far ones. "Push in" below means
  the camera moves towards the picture, "pull back" away from it, "track" sideways, "rise" upwards. Percentages are
  of the picture's width.
- **Moving details** are added in Unity, not painted: fire, sparks, snow, mist, light shafts, glow on the runes.

## 4. The shots

| # | Shot | Narration | About |
|---|---|---|---|
| 1 | Embers | "Listen, little one..." | 10 s |
| 2 | The promise | "Long ago, your people made a promise..." | 14 s |
| 3 | The hearth | "Beside my stones they built a hearth..." | 12 s |
| 4 | The seasons | "It held longer than anyone has counted." | 8 s |
| 5 | The fire goes out | "Then, one winter, the fire went out." | 8 s |
| 6a | The way home | "The forest does not kill the lost. It forgets them. First the way home." | 6 s |
| 6b | Their names | "Then their names." | 4 s |
| 6c | No one left | "Then there is no one left to find." | 5 s |
| 7 | One clearing | "A hundred winters I kept one small clearing, alone..." | 11 s |
| 8 | Four went in | "Then four of your people walked in under the trees..." | 11 s |
| 9 | Four more | "So four more came looking. One of them was you." | 7 s |
| 10 | The morning fire | "That is the old story. Yours begins here..." | 10 s |

### 1. Embers

> **"Listen, little one. This is an old story, and I am nearly the last who remembers it."**

- **Picture:** black. A low amber glow at the bottom edge, as from a fire just out of frame.
- **Layers:** glow; two layers of drifting embers at different distances.
- **Camera:** almost still. Rises 2% over the whole shot.
- **Moving details:** embers lift and go out. The glow breathes with the fire.
- **Voice:** quiet, close. A pause after "little one".
- **Sound:** a small fire, close. No music until the last second, when one low held note begins.
- **Into the next:** the glow swells to fill the frame and becomes sunlight.

### 2. The promise

> **"Long ago, your people made a promise with the forest. They would leave it room to live. And its guardians would
> keep older, hungrier things at bay."**

- **Picture:** a summer clearing ringed by enormous pines. The portal stones stand in the middle, runes faintly gold.
  On the left a small group of people in cloaks, one of them a child. On the right, at the tree line, three tall
  guardians made of root and moss, heads bowed. Shafts of sun fall between the two groups, onto the stones.
- **Layers:** sky and light shafts; far wall of trees; guardians and middle trees; stones and people; dark foreground
  trunks and ferns on both edges.
- **Camera:** pushes in 12% towards the stones and rises slightly. The foreground trunks slide apart like a curtain.
- **Moving details:** light shafts shimmer, pollen drifts, moss on the guardians stirs.
- **Voice:** telling, steady. "Older, hungrier things" a little slower.
- **Sound:** summer birds, soft wind in needles. The held note gains a second, warmer one.
- **Into the next:** slow dissolve.

### 3. The hearth

> **"Beside my stones they built a hearth. They kept the fire, and I kept the night from their door. That was the
> pact."**

- **Picture:** close and low. A ring of hearth stones in the grass at the foot of one rune stone. Two hands lay a
  split log on the fire. Behind, soft and out of focus, a timber house with a turf roof and a lit doorway.
- **Layers:** dusk sky; house; rune stone; hearth and hands; grass blades in front.
- **Camera:** tracks right 8% with a slight push in. The fire stays sharp, the house stays soft.
- **Moving details:** flames, rising sparks, firelight moving on the stone. On "I kept the night from their door" the
  rune brightens once, like an answer.
- **Voice:** warmest line in the piece. "That was the pact" plain and final.
- **Sound:** fire close, the log set down, a low warm hum from the stone on the rune's pulse.
- **Into the next:** dissolve.
- **Note:** shot 5 repeats this exact framing. Paint them as a pair.

### 4. The seasons

> **"It held longer than anyone has counted."**

- **Picture:** the village from a little above: the hearth by the stones at the centre, a handful of turf-roofed
  houses, trails leading out into the trees. Three versions of the same painting, summer, autumn and winter, fading
  one into the next. The fire is in the same place in all three, and lit in all three.
- **Layers:** sky; forest; houses and trails; stones and hearth; a foreground branch.
- **Camera:** one continuous pull back of 10% across all three seasons.
- **Moving details:** falling leaves in autumn, snow in winter, chimney smoke throughout.
- **Voice:** the line sits over autumn. Unhurried, a little proud.
- **Sound:** the season beds cross-fade under it; far-off children; one phrase on a fiddle.
- **Into the next:** the winter version darkens to night and dissolves into shot 5.

### 5. The fire goes out

> **"Then, one winter, the fire went out."**

- **Picture:** the framing of shot 3, in a winter night. Blue. Ash in the hearth with one ember left. Snow settling
  on the hearth stones. No hands. The doorway behind is dark.
- **Layers:** as shot 3.
- **Camera:** still for three seconds, then pushes in 6% on the ember.
- **Moving details:** snow falls. The ember dims and dies as the line ends. The rune fades to nothing.
- **Voice:** flat, almost matter-of-fact. No blame in it. A long pause afterwards.
- **Sound:** the music stops on the first word. Wind, then a soft hiss as the ember goes. Two seconds of near
  silence.
- **Into the next:** cut.

### 6a. The way home

> **"The forest does not kill the lost. It forgets them. First the way home."**

- **Picture:** a forest trail with a carved waymarker post. Roots have grown across the path. Pale mist stands at the
  far end.
- **Layers:** mist; far trunks; trail and post; roots; near trunks.
- **Camera:** pushes in 10% along the trail.
- **Moving details:** the mist comes forward and closes the trail behind the post.
- **Voice:** "It forgets them" quieter than the sentence before it.
- **Sound:** cold wind, a creak of wood. The held note returns, lower and colder.
- **Into the next:** cut on "Then".

### 6b. Their names

> **"Then their names."**

- **Picture:** close on a doorpost. A name carved in runes, and under it notches marking a child's height, year by
  year.
- **Layers:** dark doorway; doorpost; moss; a drifting wisp of mist.
- **Camera:** tilts down 6%, from the name to the notches.
- **Moving details:** moss creeps over the carving until the name can't be read.
- **Sound:** the wind alone.
- **Into the next:** cut on "Then".

### 6c. No one left

> **"Then there is no one left to find."**

- **Picture:** the village of shot 4 at night. Roofs sagging, doorways black, mist at knee height between the houses.
  The three guardians of shot 2 stand among the houses now, larger, overgrown, and still.
- **Layers:** sky; forest; houses; guardians; mist in two layers; foreground branch as in shot 4.
- **Camera:** tracks left 8%.
- **Moving details:** mist drifts through the doorways. Nothing else moves.
- **Voice:** slow. Let the last word fall.
- **Sound:** a far-off low groan, as of a big tree leaning.
- **Into the next:** dissolve.

### 7. One clearing

> **"A hundred winters I kept one small clearing, alone. I grew thin. And at last, I slept."**

- **Picture:** from high above, the angle the game is played from. Dark forest to every edge, and in the middle one
  small round clearing, still green, with the cracked stones in it. Roots have begun to climb three of the stones.
- **Layers:** forest canopy; clearing; stones; the ward as a faint gold rim; passing mist above.
- **Camera:** starts close over the stones and pulls back and up by 35%, the largest move in the piece, until the
  clearing is a coin of green in the dark.
- **Moving details:** the ward's rim pulses, slower each time. Fireflies thin out. On "I slept" the runes go dark.
  The clearing stays green.
- **Voice:** tired. "I grew thin" almost to itself. A breath before "I slept".
- **Sound:** the stone's hum slowing, then gone. Wind over treetops.
- **Into the next:** dissolve.

### 8. Four went in

> **"Then four of your people walked in under the trees, to learn what the woods had become. They did not walk out."**

- **Picture:** dusk at the forest's edge, from behind. Four figures with lanterns walk into pale mist between huge
  trunks. Each is told apart by an outline: a satchel of bottles, a smith's hammer, a carpenter's axe and saw, a
  rune-reader's staff.
- **Layers:** mist; far trunks; the four figures, each on its own layer and each deeper than the last; near trunks.
- **Camera:** pushes in 10% after them.
- **Moving details:** the lanterns go out one at a time, the farthest first. The last goes on "They did not walk
  out".
- **Voice:** even. No drama on the last sentence.
- **Sound:** footsteps on needles, fading. A lantern handle creaking. Then nothing.
- **Into the next:** hard cut to daylight, the only hard cut to brightness in the piece.

### 9. Four more

> **"So four more came looking. One of them was you."**

- **Picture:** open country in late-afternoon sun. Wildflowers, a big sky, a path over a low hill. Four travellers
  seen from behind walk towards a dark line of trees on the horizon. A tall woman leads. The last and nearest figure
  is hooded: the player.
- **Layers:** sky and clouds; the tree line; hill and path; the four travellers; foreground grass and flowers.
- **Camera:** tracks right 6% with the walkers, then on "One of them was you" pushes gently towards the nearest
  figure.
- **Moving details:** clouds drift, grass bends in the wind, a bird crosses.
- **Voice:** lighter. The small warmth again on "you".
- **Sound:** birdsong back all at once, wind in grass. One fiddle note joins the held note.
- **Into the next:** dissolve.

### 10. The morning fire

> **"That is the old story. Yours begins here: a bright morning, a small fire, and the trees waiting."**

- **Picture:** dawn at the meadow's edge. A small campfire in front, bedrolls, the four packing up around it. Beyond
  them the first big trees, dark, with mist between them.
- **Layers:** dawn sky; forest edge; meadow; camp and figures; fire; foreground grass.
- **Camera:** pulls back and rises 15% towards the game's own camera angle.
- **Moving details:** flames, smoke going straight up, mist moving between the far trunks.
- **Voice:** settled, near a smile. A pause before "and the trees waiting".
- **Sound:** the small fire of shot 1 again, birds, and the music resolving.
- **Into the game:** fade through white into the meadow morning, where the same fire is burning.

## 5. Sound and music

- **Effects** come from `Tools/Audio` scripts, as the rest of the game's do. Several beds already exist there (fire,
  birds, seasons, wind, footsteps, the portal's hum). New for this piece: the ember hiss, the moss creep, the
  lanterns going out, the far tree groan.
- **Music** is one piece in three parts that follow the colour: warm (shots 1 to 4), cold and nearly empty (5 to 8),
  warm again (9 and 10). One held low note throughout, a fiddle or nyckelharpa over it. It must stay under the voice.
  Whether this is a new piece or an existing track is open.
- **Mix:** the voice leads. Music and effects duck under each line. The silence after shot 5 is part of the score.

## 6. Subtitles

On by default, one sentence at a time, in the game's Hearthline style. No speaker name. They follow the game's
Dialogue Speed option for how long each stays up.

## 7. Decisions for review

1. **Where it plays.** As written it opens a new game. The alternative is to play it at the Awakening, when the
   player first rests a hand on the stone and the portal tells this story: it would then replace text the portal
   speaks there now, and shots 9 and 10 would be rewritten to end on the seven sparks in the mist. The opening is
   my recommendation, because it explains why the expedition is walking into the forest and also serves as trailer
   material. It does mean the home scene repeats the pact, which could then be trimmed.
2. **The voice.** A, B or C above, chosen by ear from test clips.
3. **Length.** 1:45 as written. For about 1:15, drop shot 4 and make 6a to 6c one painting.
4. **The portal in the game.** Its lines at home are text only. After a voiced opening a silent portal may feel
   odd. Voicing its main lines with the same voice is possible later.
5. **Music.** A new piece, or an existing track.
