# Story graphs

A **Story Script** tells a little story in the scene: something starts it, then things happen one after another. You
draw it as nodes in **Safe Haven › Quests › Story Graph** instead of wiring listeners and events by hand.

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
| Triggers | When Scene Starts, When Quest Event, When Quest Starts / Is Completed, When Structure Is Built, When Object Is Used, When Player Comes Near, When Enemies Are Defeated (each fires once unless Once is off) |
| Talk | Say (a line in the dialogue box: the player or someone in the scene, with their live portrait), Choice (a line with answers; each answer has its own output; Must Answer asks again if the box is closed) |
| Flow | Wait, Wait For Quiet, Wait Until Enemies Are Defeated, Has It Happened? (Yes / No: a Quest Event raised in this game, saved), Check Quest (Not Started / Active / Completed) |
| Quest | Raise Quest Event (completes objectives waiting for it, starts quests and other stories), Start Quest, Give Items (Equip puts it on at once; a two-handed weapon puts the torch away) |
| World | Spawn Enemies (a Mob Spawn Zone, optionally waiting until they are dead), Show / Hide Object, Move Object (walks something to a point over a few seconds; Animation Speed controls gait independently), Make Usable / Stop Being Usable, Start / Stop Glowing (a light fades in at an object and lifts the dark around it), Wither / Restore Ground (the grass dies brown around a point), Change Weather, Do Something (any component's method, picked in the Inspector) |
| Camera & Sound | Shake Camera, Focus Camera (the view turns to something for a few seconds, lifting the dark around it so it is seen at night), Lightning, Play Sound, Play / Silence / Resume Music |

## Quests and stories

Quests stay data (objectives, rewards, chains); stories make the moments around them. A story moves a quest on with
**Start Quest** and **Raise Quest Event** (give the quest a Quest Event objective), and reacts to quests with the
**When Quest** triggers. Rewards come from the quest when it completes. The quickest way in is from the quest: the Content
Editor's **Scene Reactions** panel has **+ Story When It Starts** and **+ Story When It Completes** (Quest Events: **+ Story
When Raised**), which make the story in the open scene with its trigger already set, and the quest's **In the Scenes** panel
then lists it. Save the scene to keep it.

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

The intro (`Prologue_MistyWoods`, under **Intro Moments**, built by `AgentScripts/BuildIntroPart3.cs`):

- **Bow or Axe:** When Quest Event (Started, every time) › Has It Happened? (Weapon Chosen) › No › Wait › Choice (Sigrid,
  must answer) › Give Items (bow and arrows, or the axe; equipped) › Raise Quest Event (Weapon Chosen) › Say (Runa or
  Eirik). Asking only while the event hasn't happened makes it safe to quit before answering.
- **Troll Ambush:** When Player Comes Near (the berry clearing, every time) › Has It Happened? (Troll Ambush) › No ›
  Check Quest (Berries for the Pot) › Active › Raise Quest Event (Troll Ambush) › Wait › Say (Runa) + Spawn Enemies.
  When Enemies Are Defeated › Say (Runa).
- **The Old Troll:** When Quest Starts (Something Is Coming) › Show Object (the troll) › Shake Camera + Play Sound +
  Lightning + Start Glowing + Move Object (it walks towards the fire) + Focus Camera › Say (Sigrid, Runa, Sigrid);
  its `CreatureFootfalls` produces sound and smaller shakes as it actually walks. Move Object's Animation Speed slows its
  gait, and the disabled AI's animation driver does not overwrite the story movement.

The home quest line (`SafeHaven`, under **Home Moments**, built by `AgentScripts/BuildHomeStory.cs`):

- **The Waking:** When Quest Event (the Heart Tree's Moment) › Choice › Say › Choice › Say › Raise Quest Event (Talked To Heart Tree). Why the player builds a haven.
- **First Fire:** When Structure Is Built (Campfire) › Wait › Start Glowing + Play Sound + Say › Say › Stop Glowing.
- **The Door Wakes:** When Quest Is Completed (Awakening the Portal) › Wait › Focus Camera + Say (Sigrid, through the stones) › Say.
- **Bjarne's Hammer:** When Quest Is Completed (A Light They Can See) › Wait › Say › Say (the player) › Say.

## Notes

- What a story changes in the world (an object hidden, a gate opened) is kept after loading by that object's own
  SaveableEntity; the story itself only remembers which triggers have fired.
- Withered ground and glows are not saved.
- New node kinds: subclass `StoryNode` (or `StoryTrigger`) in `Scripts/Story/StoryNodes.cs`, mark it `[Serializable]`,
  give it a Title, a Category and Run (or Listen). Don't rename node classes once graphs use them.
