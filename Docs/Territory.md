# The Haven's Reach: holding the land round home

The Haven's world is split into regions (the world map draws them; `WorldAtlas`). Beyond the meadows, each region can be
claimed for the Haven and has to be held against the forest. This is the loop that ties exploring, raiding and the
corruption together, with goals for the next ten minutes and for the next ten hours.

## The loop

1. **Find** a region's named places (walking up to one finds it: +25 Insight).
2. **Claim** it at its **boundary stone**, which stands beside a path near the region's middle. The pact marked its
   borders with these stones; lighting one says the land is the Haven's. The first claim is free ("the stone answers to
   the hearth"); every later claim costs a **Pact Rune**.
3. **Hold** it. A claimed region has a *hold* from 0 to 100. It starts at 40 and wears away a little every turn, faster
   the more blight there is. Below 25 it is *Slipping*, from 70 *Secure*. At 0 the forest takes it back: its stone goes
   dark, the blight spreads, and it has to be claimed again.
4. **Answer the forest.** Now and then the forest gathers against the weakest of the Haven's lands and says so: in two
   turns it will push in and take 30 hold (more under blight). The player can
   - **stand watch** at that region's boundary stone: what the forest sends comes for the stone; beat it all without
     straying and the push is broken (+15 hold, 60 Insight); or
   - **burn the rot's heart** on a raid: while a push is coming, every raid's briefing has an urgent rumour of it. Burning
     one halves the push and gives a Pact Rune; two stop it; or
   - **let it come**, and lose that much hold.
5. **Grow it.** Each held region asks for a task now and then (hunt, gather or fish there: +10 hold, 60 Insight); a Pact
   Rune adds 25; finding its places adds 5 each.

## What holding land gives

| Standing | Gives |
|---|---|
| Held | 6 renown (towards the Haven's ranks) |
| Secure | 12 renown, a **tithe** of the region's own goods into the stash at every dawn (Greyfell: stone and copper ore; the Fen: bog venom and cloudberries...), and **the corruption never comes from it**: corrupted nights come from the region the forest is pushing on, or the weakest held land, or anywhere that isn't Secure |

So the long goal is a ring of Secure lands round the Haven: safer nights, a steady trickle of goods, and a big share of
the renown for the top ranks. The short goals are always in reach: a place to find, a stone to light, a task to finish,
a push to answer.

## Turns

Time in this loop goes by in **turns**: a dawn at home, and every raid that ends. A push can't be missed by being away on
raids (each raid is a turn, so it comes even if the player never sleeps at home), and it isn't rushed either: two turns
of warning is two raids, or a raid and a night, or two nights.

## The numbers, and why

All are in `Resources/TerritorySettings` (written by `AgentScripts/BuildTerritory.cs`, which leaves the numbers as tuned).

| Dial | Value | Why |
|---|---|---|
| Claim hold | 40 | One unanswered push (30) nearly takes a fresh claim: claiming is a promise to come back. |
| Erosion | 1 + 4 × blight per turn | Without blight a fresh claim lasts ~40 turns untended (a long evening); under heavy blight about 8. |
| First push | 3 turns after the first claim | Time to learn the stone before the forest tests it. |
| Push chance | 20% + 40% × blight per turn (at most 60%), 3 turns' rest after each | At no blight about one push every 8 turns (~2-3 hours of play); under heavy blight every 4-5. Blight comes from corrupted nights the player let get away, so the pressure answers how well home is defended. |
| Push strength | 30 + 25 × blight, 60% on a Secure region | Secure land is safer, not safe. |
| Rot's heart | halves the push, gives a Pact Rune | Two raids answer a push without going home; the rune feeds the next claim. |
| Watch | 3 + danger creatures, 4 minutes, stay within 40 m | Deadly regions defend harder; the reward is the same, so the player picks their fights. |
| Pact Rune | 5% per supply cache, 1 per rot's heart | About one raid in two finds one by chance; answering pushes on raids is the steady source. Ten more claims and a hold of 70 everywhere is a long goal, not a grind. |
| Renown | 6 held, 12 Secure | All eleven Secure is 132 renown: a large share of the 300 for the top rank, never all of it. |
| Find | 25 Insight | The 27 places are worth about four extracted raids in all. |

## The quest line: how it opens

Until the portal has told of them, every stone is cold: it can be found and read, but not lit, and the Lands tab says
the portal knows what the stones are. Three raids in, the player knows the portal, the clearing and the woods, and
has seen how far a raid goes. That is when the portal calls.

| Quest (Haven, green) | Starts | Asks | Pays |
|---|---|---|---|
| **The Cold Stones** | The portal's call on coming home after the third raid, however the raids ended | Hear the portal out (a conversation at the portal, the next time it is used; only that conversation counts, not opening its menu), then light a boundary stone. The stones open once it has been heard. Every unlit stone is a waypoint, so the tracker points at the nearest; a stone that doesn't know you yet says which way its nearest place lies | A Pact Rune |
| **What the Land Asks** | Right after | Do what a held land asks, offer a Pact Rune at a lit stone, make a land Secure (any order) | A Pact Rune |
| **The Forest Answers** | The first time the forest gathers against a held land | Answer that push: stand watch, or burn the rot's heart (an answer in the seconds before the quest is announced counts too). If it lands first, the next one counts | A Pact Rune |

**Why, in the lore** (the portal's words). When the pact was kept, people did not only tend the hearth beside the portal.
They walked the land the pact gave them, and where it ended they set stones and lit them from that fire. A lit stone
said that this much of the forest remembers a hearth: inside the ring nobody lost the way home, or their name. When the
hearth went cold the stones went cold, and the forest took back the roads and every name on them. That is where the
lost expedition is now. So every land held is a road home for whoever is still out there, and a door shut on the
night (Secure land keeps the corrupted nights out). The forest pushes back because the pact gave it room too, "and that
is the one thing it has not forgotten". The portal never says who broke the pact: that is the Road North's twist.

The portal speaks once more at each step: a stone lit ("Now it must be held"), the first push ("It means to push in"),
a land Secure (the long goal: "Hold a ring of them round me"), a push broken. Its words live in
`AgentScripts/BuildHavensReach.cs`, which builds the quests and the stories. The journal opens **The Haven's Reach**
(Mechanics, with the tutorial notice) and **The Boundary Stones** (Backstory) when the first quest starts.

## In co-op

The quest line is each player's own, as all quests are. A friend's work in the host's lands (a rune, a task's work,
a heart burned on the host's run) moves the host's quests on too, as it moves the land. A visitor's own portal
conversations wait for their own Haven: a friend's portal doesn't spend them, and talking to it doesn't count as
talking to theirs. Their own stones' waypoints stay dark in a friend's Haven.

The lands are the host's. Friends visiting see them as the host holds them (they come with the settlement), and no turn
passes in a friend's game.

| A friend... | What happens |
|---|---|
| offers a Pact Rune at a lit stone | their own rune; the host's hold rises by 25 |
| hunts, gathers or fishes in a held region | counts towards that region's task in the host's game |
| finds a place in a held region | their own Insight; +5 hold for the host |
| burns the rot's heart on the host's run | weakens the host's push, but **once a run**: everyone on the run has the heart's rumour, and it is one heart. Each burner keeps their Pact Rune |
| wants to light a stone or stand watch | can't: those are the host's calls. When the host stands watch, the forest's creatures are shared like any of the host's, and friends fight beside them |

What a friend finds (places, tutorials) is their own and goes home with them. The Haven's geography is the same in every game.

## Where it lives

`Scripts/Territory/`: `TerritoryState` (the rules, plain C#, saved in `SettlementSaveData.territory`), `Territory` (the
game's side: turns, messages, tasks from game events, the corruption's way in), `BoundaryStone` and `StoneDefence` (the
stone and the watch), `TerritorySpawner` (stands the stones at runtime from the atlas, so the scene file never changes),
`TerritorySettings`; in co-op, `CoopLands` (Multiplayer). The Haven window's **Lands** tab (`HavenLands`), the region's name on entering it and above the
minimap (`RegionTitleHud`), the world map (held lands in the accent colour, a pushed one in warning, the stones once
known). The raid side: `RaidRumourDefinition` › Forest Pushing, `WeakenIncursionOutcome`, the Rot's Heart rumour and event.
Tests: `TerritoryTests`.

## Parties (2026-11-22)

Residents go out to work a held region while the player raids (`LandParties`; the Lands tab's "Send Gunnar hunting"): the Hunter
hunts, the Fisher fishes, the Tracker scouts, one land a kind. Each raid of three minutes or more, a hunting or fishing party brings a
small yield home and the land's hold wears half as much that turn; scouts see the forest gathering against it a turn sooner. Never a
raise in hold. Meanwhile the one who goes isn't at their station, so a party costs that station's raid.

## Outposts, and the ring held (2026-11-22)

At a land held Secure, the Lands tab offers "Raise an outpost" (`Outposts`; cost `TerritorySettings` › Outpost Cost: 30 Fine Planks,
20 Fine Stone Blocks, 6 Linen, 4 Iron Ingots). A free resident keeps it (not a builder, not a party's leader, one outpost each; "Send
X to keep the outpost" hands it on) and goes out to it on each raid of three minutes or more, as a party does, so their own station
waits that raid. It sends home the land's own material (`RegionInfo` › Outpost): Greyfell 3-5 Iron Ore and 2-3 Granite, the Hollow
Pines 2-3 Iron Ore, the Wolfwood 1-2 Thick Hide, Ravenwood 1 Deepstone, the Drowning Fen 1-2 Bog Venom. It is the one home source
allowed above tier II, kept small (at most 5 of a thing a raid). It waits while the land isn't Secure or the forest gathers against it;
the land lost, it is given up. The calmer lands have none: their tithe is their flavour. Elderwood stays a raid wood.

Once every land is Secure at once the ring holds: the safe pocket gets a slot more (Ring Pocket Slots), kept from then on.
The Wolfwood's tithe is now Rough Hide (its Thick Hide comes from its outpost): home makes plain goods (`EconomyRulesTests`).

## Not yet

- An outpost's raid hook (a forward camp, an extra exit or a rumour in the land's raid), and a model at its boundary stone.

- A region's own weather and music; creature pages saying where each is found, from the atlas.
- Spreading blight shown on the map as a tint, region by region.
