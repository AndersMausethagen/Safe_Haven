# Who comes home, and when

**Status: built (2026-11-09), approved by the user.** `AgentScripts/BuildRescueOrder.cs` sets it up; `RescueOrderTests` holds it. Not yet
playtested in a real raid: the four new rescue sites (Gunnar, Asa, Yrsa, Hild) are placeholder shapes like the others.

## The rule

Every person is found on a raid and brought home. Each one answers a problem the player has **just felt**, and their rescue
opens the building they work at: coming home, they teach the player the workplaces of every job they can do
(`ResidentNpc.TeachWhatTheyKnow`), and giving them the job teaches it again for a co-op friend. The building is not known from
the start. Their rumour waits for the person before them (Raid Rumour › After Home) and is urgent, so the next person is
always in the briefing. A quest per rescue ("Bring X Home", The Lost Expedition) says what problem they answer; a quest after it
("Hands at Home") gives them work and builds their workplace. Existing saves keep every building they already knew.

## What other games do

- **Enshrouded**: the craftspeople are rescued one at a time, and each brings a station and its recipes. The Blacksmith comes
  first, right after the opening quests, because better gear is the first thing the player needs. Quests point to where
  the next survivor is.
- **Valheim**: each problem comes before its answer. Copper ore is heavy, so the cart and the boat matter, and the player
  has already carried ore the hard way before they arrive. Each boss drops what opens the next tier.
- **Dragon Quest Builders 2**: each villager brings a trade and asks for something. The base grows around the people.

What we take from them: show the problem first and the person second, so a rescue feels like relief rather than a checklist.

## The order

| # | When | Who | The problem just felt | Their rescue opens |
|---|---|---|---|---|
| – | Opening, alone | – | Learning the basics | Tools, Workshop I, Small Chest, Knotted Club, food, the portal (**built**) |
| 1 | Raid 1, story | **Ingrid**, Alchemist | You come home hurt, with cursed things you can't use | **Cauldron** (healing draughts) and Purification Altar; she heals you on raids. Her welcome names Bjarne's hammer, which points to the next rescue |
| 2 | Raid 2 | **Bjarne**, Smith | Gear breaks on raids, and a club is all there is | **Smithy**: repairs at half cost, copper smelted from ore |
| 3 | Raid 3–4 | **Tor**, Carpenter | Logs are heavy, and everything better needs planks | **Woodcutter's Lodge**: planks, Round Shield; this opens the Palisade, Storage Shed and Watch Post |
| 4 | Smithy II | (Bjarne) | Stone tools are slow, and deep ore needs a better pick | Copper tools. No new person: the Smithy's upgrade answers it |
| 5 | Lodge II | (Tor) | You can't carry enough home: ore and planks weigh too much | **Wainwright's Yard** (Wheelbarrow, Handcart, Rowboat), taught by Tor at Lodge II instead of found plans |
| 6 | Smithy II–III | (Bjarne) | The woods hit harder than wood and stone can take | Copper weapons and armour, with the ore the carts brought home |
| 7 | Before the first breach | **Eirik**, Warden | Something is testing the ward | Watch Post; the ward holds half as long again |
| 8 | After Eirik | **Yrsa**, Cook | Plain food only keeps you alive, and raids now run long | Kitchen: meals with lasting bonuses (moved up from tenth on 2026-11-14, the design review's 8.10: the spec puts the cook third, and the portal's third tier asks for her) |
| 9 | After Yrsa | **Gunnar**, Hunter and Fisher | Hides pile up, and copper is the last armour without leather | Hunter's Lodge (leather); the Dock |
| 10 | After Gunnar (moved up 2026-11-15) | **Liv**, Rune Scholar | Elites start carrying inscriptions, and nobody can read them | The raid runes, and enchanting: she teaches the altar's marks, takes inscriptions and carves past your Runecraft (Docs/EnchantingResearch.md) |
| 11 | After Liv | **Asa**, Farmer and Weaver | Food runs out on long raids; iron gear needs linen | Farm Plot and Tailor's Shop |
| 12 | Mid game | **Runa**, Tracker | Raids feel random | One more rumour in every briefing |
| 13 | Village rank | **Hild**, Mason | Palisades burn and huts are cramped | Stone Wall and Cottage (Workshop III) |
| 14 | Last | **Sigrid**, Leader | The expedition's leader is still out there | Haven Banner; more time in every raid. This ends The Lost Expedition |

Steps 2 to 6 follow the outline from the playtest of 2026-11-08: gear and repair, refining, better tools, moving goods, copper. Ingrid stays first because
the story already sends the player after her, and she leads to Bjarne. That's roughly one person every two or three raids
early, slower later.

## The problem comes first (2026-11-09)

Each person answers a problem, and the raid summary now shows the problem before they are home (`HavenAnswers`, read from the
recipes, buildings and jobs, so no hint is written by hand):

- **Left behind**: what the bag couldn't take, with its weight, and what would have carried it: "A Wheelbarrow would have
  carried it. Bring Tor home: the Carpenter knows how."
- **Nobody at home can use these yet**: a find no known recipe or building takes, and who would know: copper ore before
  Bjarne, thick hides before whoever works them first.
- **Worn on the way** (and gear kept after a failed raid): "A smith would do it for 50% less. Bring Bjarne home."

What came home and what held the player back lead the summary; lessons and the way there follow, with a scrollbar.

Quests teach an answer by doing it: A Safe Place asks for something put in the chest (Store Item), A Smith's Hearth for a repair
(Repair Item), Heavy Loads for the Lodge at Tier II before the Yard (Upgrade Building).

Whoever is home teaches their workplaces whenever the player appears (`ResidentNpc.TeachEveryoneHome`): a rescue at the end
of a raid taught only the raid's player, after their state had been taken home, so the plans were lost on the way back.

## As built

- Each job teaches its workplace, and those buildings are not known from the start. The Smithy and the Cauldron need only the
  Workshop; the Wainwright's Yard needs Lodge II.
- Rumours: none waits for a building; each waits for the person before (After Home). Gunnar, Asa, Yrsa and Hild have rumours
  and sites of their own: a hunter's pit, a burned farmstead, a cookfire that draws wolves, a dry-stone wall round nothing.
- The Woodcutter's Lodge quest gives Tor work; Raise the Walls, A Growing Home (now a Storage Shed and a Hut) and Heavy Loads
  (Wainwright's Yard and a Wheelbarrow) follow it.
- The portal has a line for each rescue. Its old moment at the Hamlet rank, Bjarne's hammer "past the troll country", is
  Sigrid's horn now, since she is found last.
- Not done: tier I weapons stay at the Workshop (moving them to the Smithy would change the base tiers); Hild's rank-free
  arrival only affects new games, and a save where she already came keeps her.

## Sources

- [Enshrouded wiki: Survivors](https://enshrouded.wiki.gg/wiki/Survivors)
- [Pindrop: Enshrouded quest order](https://pindrop.gg/enshrouded/guides/quest-order)
- [XGamingServer: Enshrouded NPC locations](https://xgamingserver.com/blog/enshrouded-npc-craftspeople-locations-rescue-survivors/)
- [AllThings.How: Valheim progression](https://allthings.how/valheim-progression-guide-every-biome-boss-and-key-drop/)
- [Supercraft: Valheim boss order and gear](https://supercraft.host/wiki/valheim/progression_guide_boss_order_gear/)
- [Twinfinite: Dragon Quest Builders 2 tips](https://twinfinite.net/guides/dragon-quest-builders-2-tips-and-tricks-for-beginners/3/)
