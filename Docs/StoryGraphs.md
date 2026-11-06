# Story graphs

A **Story Script** tells a little story in the scene: something starts it, then things happen one after another. You
draw it as nodes in **Safe Haven › Story Graph** instead of wiring listeners and events by hand.

## Making one

1. **GameObject › Safe Haven › Story Script** puts one in the scene (with a SaveableEntity, so once-only stories stay
   told after loading) and opens the graph.
2. Right-click the canvas (or press Space) › **Add** › pick a node. Start with a **trigger**.
3. Drag from a node's output (right) to the next node's input (left). One output can lead to several nodes: they run
   side by side. Delete removes nodes and links; everything can be undone.
4. Fill in each node's settings on the node itself. Where a node needs something in the scene and the field is empty,
   the **+** beside it makes it where the Scene view is looking: a Mob Spawn Zone (draw its area), a usable object
   (a cube with an Interactable; swap the model as you like) or a point (to look at, to glow, to wither around).
5. Problems are listed at the bottom of the window. In Play mode the nodes light up as they run.

## Nodes

| Group | Nodes |
|---|---|
| Triggers | When Scene Starts, When Quest Event, When Quest Starts / Is Completed, When Structure Is Built, When Object Is Used, When Object Is Hit, When Player Does (dodge, parry, dodge through a blow), When Player Comes Near, When Enemies Are Defeated (each fires once unless Once is off) |
| Talk | Say (a line in the dialogue box: the player or someone in the scene, with their live portrait; with **Aside** on it is talk in passing, a subtitle that leaves the controls and the camera to the player), Choice (a line with answers; each answer has its own output; Must Answer asks again if the box is closed) |
| Flow | Wait, Wait For Quiet, Wait Until Enemies Are Defeated, Wait Until The Walk Reaches (a share of the party's walk: talk that belongs to a place on the road), Wait For Quest Event (or a set time, so nothing optional can hold the story up), Has It Happened? (Yes / No: a Quest Event raised in this game, saved), Check Quest (Not Started / Active / Completed; a quest that lapsed counts as Not Started) |
| Quest | Raise Quest Event (completes objectives waiting for it, starts quests and other stories), Start Quest, Add Quest Progress (something done earlier makes a quest shorter), Give Items (Equip puts it on at once; a two-handed weapon puts the torch away) |
| World | Spawn Enemies (a Mob Spawn Zone, optionally waiting until they are dead), Remove Enemies (what a zone spawned goes away, and it stops respawning), Show / Hide Object, Move Object (walks something to a point over a few seconds; Animation Speed controls gait independently), Make Usable / Stop Being Usable, Start / Stop Glowing (a light fades in at an object and lifts the dark around it), Wither / Restore Ground (the grass dies brown around a point), Change Weather, Change Time of Day (the clock moves forward to an hour, over Seconds or following the party's walk), Change Look (fades the scene's Scene Look: a second set of post-processing and lighting), Reveal Ground (lifts the fog of war around a point, as if the player had been there), Walk The Party (companions walk a route at a steady pace and wait for the player), Do Something (any component's method, picked in the Inspector) |
| Camera & Sound | Shake Camera, Focus Camera (the view turns to something for a few seconds, lifting the dark around it so it is seen at night), Lightning, Play Sound, Play / Silence / Resume Music |

## Quests and stories

Quests stay data (objectives, rewards, chains); stories make the moments around them. A story moves a quest on with
**Start Quest** and **Raise Quest Event** (give the quest a Quest Event objective), and reacts to quests with the
**When Quest** triggers. Rewards come from the quest when it completes. The quickest way in is from the quest: the Content
Editor's **Scene Reactions** panel has **+ Story When It Starts** and **+ Story When It Completes** (Quest Events: **+ Story
When Raised**), which make the story in the open scene with its trigger already set, and the quest's **In the Scenes** panel
then lists it. Save the scene to keep it.

A story that the game can't go on without has to survive loading: a once-only trigger stays spent, so the walk or the talk
would never finish. Give **When Quest Starts** the option **Also After Loading**, turn **Once** off and check with **Has It
Happened?** that the moment hasn't passed (see The Morning Fire below).

## Timing

A line right after a fight feels rushed, so **Say** and **Choice** wait out the pause after a fight by themselves (**After
Fight Pause**, Systems Editor › World › Story Pacing); turn it off for a line that must land at once. A line said during a
fight isn't held: it shows as a subtitle. **Wait For Quiet** waits for the end of a fight and the same pause, for steps that
have no line. Stories that start from a quest wait with it, as quests leave room for themselves (see the quest's **Start
Delay**).

## Examples

`Scenes/Demo/StoryDemo.unity` (built by `AgentScripts/BuildStoryDemo.cs`) has both:

- **At least it's not raining:** When Player Comes Near › Say "At least it's not raining." › Change Weather (Rain) ›
  Wait › Say "Oh, well. At least it can't get worse." › Shake Camera (+ Play Sound at the troll) › Focus Camera on the
  troll for 3 s › Say "I better shut up now. Run!" › Play Music (combat).
- **The Waking Stone:** When Object Is Used (the stone) › Start Quest › Start Glowing (+ Play Sound) › Wither Ground ›
  Say "The grass... it's dying." › Spawn Enemies (wait until defeated) › Stop Glowing › Stop Being Usable › Raise
  Quest Event (the quest's objective: it completes and rewards).

The morning in the meadow (`Prologue_MistyWoods`, under **Prologue Meadow › Morning Moments**, built by
`AgentScripts/BuildPrologueMeadow.cs`):

- **The Morning Fire:** the player takes the quest from Sigrid, then When Quest Starts (A Morning by the Fire, Also After Loading) › Has It Happened? (Met the Expedition)
  › No › Wait › Say (Sigrid, Eirik, Runa, Sigrid) › Raise Quest Event (Met the Expedition). Everyone says who they are.
- **Talk by the Fire:** When Quest Event (Met the Expedition) › Wait › Say Aside, four times.
- **The Road East:** When Quest Starts (The Road East, Also After Loading) › side by side: Walk The Party to the handcart; the day
  going by (Wait Until The Walk Reaches › Change Time of Day with Fade Straight There, so shadows don't race over the ground); talk
  (Wait Until The Walk Reaches › Say Aside). A Say with no speaker set is the narrator: no name or face, for what the player finds. At the cart: Say Aside › Wait For Quest Event (Looked
  Over the Cart, or 25 s) › Say Aside › Walk The Party on to the trees › Raise Quest Event (Entered the Forest), with the day running
  out, boar running past (Show, Move, Hide Object), the birds gone, torches, and the grass dying (Wither Ground) along the way.
- **The Road Behind:** the player a little way inside the trees › Raise Quest Event (The Road Behind Is Gone), whose Scene Reaction turns
  on a wall and a Fog Area, so nobody walks back to the meadow. **A Lesson from Eirik** counts a dummy's hits and the player's moves
  (When Object Is Hit, When Player Does) towards the bronze quest. **A Moment of Warmth** ends the rest 9 s after the player talks to
  someone (or after 45 s) with Start Quest, so the wolves cut the conversation short. **They Remember** has the companions mention the
  errands and why the player came, and shortens later quests. **What Sigrid Knows** answers at the stones what was asked by the fire.
  **The Old Troll Returns** walks it to the stones during the portal hold, its blow landing as the minute ends (40 s wait, 19 s walk, 0.8 s swing). Change its Waits with the
  hold's length.
- **The Forest Closes In:** When Quest Event (Entered the Forest) › Lightning, Shake Camera, Play Sound, Say Aside. What stays
  changed (the look, the mist and rain, the night, the torches, the open way in) is not here but in the event's **Scene
  Reaction**, so it comes back at once after loading.
- **Choose a Weapon:** When Quest Starts (Where the Sun Went, Also After Loading) › Has It Happened? (Weapon Chosen) › No › Wait ›
  Say › Choice (Sigrid, must answer: sword, axe, spear, club, bow) › Give Items (equipped) › Raise Quest Event › Say.
- **Wolves on the Road:** When Player Comes Near (every time) › Has It Happened? › Check Quest (Shelter from the Storm) › Active
  › Raise Quest Event + Spawn Enemies + Say. When Enemies Are Defeated › Say.
- **The Storm Breaks:** When Quest Starts (Eyes Between the Trees) › Change Weather. When Scene Starts › Wait › Check Quest ›
  Active or Completed › Change Weather at once, for loading. **Morning Music**, **Not Without Us** and the three that hand
  over the errands' tools are one or two nodes each.

The intro in the forest (under **Intro Moments**, built by `AgentScripts/BuildIntroPart3.cs`; don't run it on the current scene):

- **Troll Ambush:** When Player Comes Near (the berry clearing, every time) › Has It Happened? (Troll Ambush) › No ›
  Check Quest (Berries for the Pot) › Active › Raise Quest Event (Troll Ambush) › Wait › Say (Runa) + Spawn Enemies.
  When Enemies Are Defeated › Say (Runa).
- **The Old Troll:** When Quest Starts (Something Is Coming) › Show Object (the troll) › Shake Camera + Play Sound +
  Lightning + Start Glowing + Move Object (it walks towards the fire) + Focus Camera › Say (Sigrid, Runa, Sigrid);
  its `CreatureFootfalls` produces sound and smaller shakes as it actually walks. Move Object's Animation Speed slows its
  gait, and the disabled AI's animation driver does not overwrite the story movement.

The home quest line (`SafeHaven`, under **Home Moments**, built by `AgentScripts/BuildHomeStory.cs`):

- **The Waking:** When Quest Event (the portal's Moment) › Choice › Say › Choice › Say › Raise Quest Event (Talked To Portal). Why the player builds a haven.
- **First Fire:** When Structure Is Built (Campfire) › Wait › Start Glowing + Play Sound + Say › Say › Stop Glowing.
- **The Door Wakes:** When Quest Is Completed (Awakening the Portal) › Wait › Focus Camera + Say (Sigrid, through the stones) › Say.
- **Bjarne's Hammer:** When Quest Is Completed (A Light They Can See) › Wait › Say › Say (the player) › Say.

## Notes

- What a story changes in the world (an object hidden, a gate opened) is kept after loading by that object's own
  SaveableEntity; the story itself only remembers which triggers have fired.
- Withered ground and glows are not saved.
- New node kinds: subclass `StoryNode` (or `StoryTrigger`) in `Scripts/Story/StoryNodes.cs`, mark it `[Serializable]`,
  give it a Title, a Category and Run (or Listen). Don't rename node classes once graphs use them.
