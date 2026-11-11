**NORDIC FOLKTALE / ACTION RPG / EXTRACTION**

# Safe Haven Game Design Document

Build a home worth protecting.  
Venture into the darkness to give it a future.

**VERSION** 0.4 • October 30, 2026

**FORMAT** Isometric Action RPG / Survival / Base Building

**PLATFORM** Unity • PC first • Single-player prototype

**STATUS** Living master design document

## Contents

- [1. Game overview and vision](#1-game-overview-and-vision)
- [2. Design pillars and differentiators](#2-design-pillars-and-differentiators)
- [3. Audience, format, and experience goals](#3-audience-format-and-experience-goals)
- [4. World, mythology, and narrative](#4-world-mythology-and-narrative)
- [5. Overall gameplay loops](#5-overall-gameplay-loops)
- [6. Safe Haven: a peaceful home worth defending](#6-safe-haven-a-peaceful-home-worth-defending)
- [7. Raids: the central system](#7-raids-the-central-system)
- [8. Extraction, loss, and recovery](#8-extraction-loss-and-recovery)
- [9. Raid world, discovery, and events](#9-raid-world-discovery-and-events)
- [10. Movement, combat, and enemies](#10-movement-combat-and-enemies)
- [11. Survival, cooking, drinking, and farming](#11-survival-cooking-drinking-and-farming)
- [12. Resources, categories, and inventory](#12-resources-categories-and-inventory)
- [13. Crafting, refining, and recipes](#13-crafting-refining-and-recipes)
- [14. Base building and structures](#14-base-building-and-structures)
- [15. NPC recruitment, roles, and relationships](#15-npc-recruitment-roles-and-relationships)
- [16. Skills, levels, and bosses](#16-skills-levels-and-bosses)
- [17. Quests, trade, and world response](#17-quests-trade-and-world-response)
- [18. UI, UX, and accessibility](#18-ui-ux-and-accessibility)
- [19. Art direction, audio, and atmosphere](#19-art-direction-audio-and-atmosphere)
- [20. Unity technical design](#20-unity-technical-design)
- [21. First 90 minutes: player journey](#21-first-90-minutes-player-journey)
- [22. Worked raid example and balance targets](#22-worked-raid-example-and-balance-targets)
- [23. Minimum viable product (MVP)](#23-minimum-viable-product-mvp)
- [24. Development sequence and milestones](#24-development-sequence-and-milestones)
- [25. Design risks and trade-offs](#25-design-risks-and-trade-offs)
- [26. Open design decisions](#26-open-design-decisions)
- [27. Glossary and content rules](#27-glossary-and-content-rules)
- [28. Design summary](#28-design-summary)

## Document notes

All numerical values, recipe costs, encounter cadences, and content counts are initial prototyping proposals and should be validated during playtesting.

## Purpose and status

This document defines the game fantasy, player promises, gameplay loops, rule sets, content direction, technical foundations, and an achievable initial production scope. It is intended both as a creative north star and as an implementation reference in Unity. All numerical values, names, recipes, and content quantities are **proposed prototyping defaults, not final balancing decisions**. Validate them through playtesting before locking production requirements.

**Version 0.2 design changes:** Cursed Essence and Purified Essence now form a shared magical progression economy; players unlock skills through milestones, develop talents through leveling, and improve magic using Essence. Safe Haven remains peaceful most of the time, but the portal ward can be breached during controlled, announced defense events. Defense structures and NPC defenders now have meaningful roles.

**Version 0.3 design changes:** The decisions behind the first-90-minutes prototype are recorded in §26.1: a one-slot safe pocket that is never lost, durability rules for deployed equipment, Portal Sparks, hunger and thirst that always drain but never kill, the carry budget, free building inside the ward, the first recruit, and the first scripted ward breach.

**Version 0.4 design changes:** Raids now have random events with warnings, places and priced choices (§9.3–9.4). Gear runs Stone → Copper → Iron → Moonsilver, with jewellery and shields (§10.4). Six new creatures and three status effects widen what a fight asks of the player (§10.5–10.6). Cooked meals give lasting bonuses, and crops grow at Farm Plots (§11.3–11.4). More structures from §14.2 exist. The game has a full Options screen (§18.5) and any number of saved games (§20.6). The decisions are recorded in §26.1.

## 1. Game overview and vision

### 1.1 Elevator pitch

The player awakens alone in a peaceful clearing deep inside a cursed forest. At its heart stands a dormant magical portal, the old stones that dropped them here, and its last strength protects this refuge against the forest's darkness. Their first steps are humble: gather stone and wood, light a campfire, and construct a workbench. Before long, they can wake the portal using Life Orbs recovered from the remaining fragments of forest magic.

The portal opens time-limited expeditions—**raids**—into unsafe parts of the world. There, the player fights, harvests resources, discovers strange beings, rescues travelers, and makes difficult decisions: collect more iron, investigate a mysterious cry, or return before the portal closes? Riches mean little if nobody survives the journey home.

Recovered resources expand Safe Haven with workshops, farms, crafted equipment, defensive structures, and a gradually growing community. Cursed Essence collected from hostile creatures and raid victories can be purified at home to empower gear, buildings, and magic. Greater advances open more dangerous biomes, powerful trolls and spirits, and knowledge sealed away by the broken enchantment. The settlement is usually tranquil, but the weakened Portal ward can sometimes be penetrated during foreseeable, player-facing incidents.

### 1.2 Core fantasy

**“Build a home worth protecting—and venture into the darkness to give it a future.”** The protagonist is not primarily a conqueror. They are an explorer, protector, craftsperson, and caretaker of people who need a home.

### 1.3 Genre ingredients

| **System**                     | **Role**                             | **Design intention**                                                           |
|--------------------------------|--------------------------------------|--------------------------------------------------------------------------------|
| Top-down action combat         | Skill-based challenge                | Responsive movement, readable attacks, satisfying mastery                      |
| Extraction raids               | Main source of risk and rare rewards | Tension around when to leave and what to risk                                  |
| Base building                  | Visible long-term progression        | Every successful journey can improve the home                                  |
| Survival                       | Preparation and meaningful decisions | Food, water, and weather influence plans without becoming busywork             |
| NPC recruitment                | Emotional and mechanical development | Rescued people bring abilities, stories, and life to the settlement            |
| Equipment, Essence, and skills | Player progression and builds        | Boss-earned abilities, skills and perk choices, magical enhancements, and varied builds |
| Controlled base defense        | Occasional stakes at home            | Build wards, defenses, and resident preparedness against warned breaches       |
| Nordic folklore                | Distinctive world identity           | Forest spirits, huldras, trolls, fiddlers, old bargains, and mountain myths    |

### 1.4 Player promises

- **Home is a refuge, not an untouchable zone.** Most settlement play is calm and untimed. Rare, clearly telegraphed ward breaches can occur while the player is present, and defenses matter; no surprise attacks occur during raids or while offline.

- **Raids involve player agency.** Choose destination, loadout, time extensions, companions, and cargo.

- **Risk is legible.** Audio, animations, maps, and clocks telegraph danger; losses should follow understandable rules rather than hidden randomness.

- **Successful returns matter.** Resources, recipes, NPCs, and story revelations meaningfully change the persistent world.

- **Folklore shapes play.** Wonder, warmth, danger, cleverness, and strange bargains matter more than generic high fantasy or pure horror.

## 2. Design pillars and differentiators

### 2.1 Pillar A: Safe Haven is worth protecting

The contrast between **home** and **outside** remains central. In Safe Haven, the player normally builds, farms, cooks, socializes, and plans without a raid clock or constant enemies. The **Portal's magical ward** makes the settlement largely peaceful, but it is not invulnerable: rare, controlled breaches create opportunities to defend the home the player has built. Attacks are announced in advance, gated behind clear progression milestones, and resolve only while the player is actively present. Buildings may take repairable damage; the entire settlement, permanent unlocks, and critical progression cannot be arbitrarily erased. Safe Haven must feel like home first and a defense arena second.

### 2.2 Pillar B: Every raid is a small fairy tale

An expedition has an opening (planning and portal), middle (discoveries and complications), turning point (push onward or retreat), and ending (extraction or loss). The ideal outcome is a story the player can tell: “We discovered a cart, but had to choose between the ore and saving the blacksmith.”

### 2.3 Pillar C: Meaningful risks, understandable consequences

Death costs carried inventory, while deployed equipment is retained with durability damage and can be repaired at home. A small safe pocket protects one precious find even on death. Exposed companions remain subject to their separate risk rules. A heavily loaded cart promises enormous rewards but slows escape. All major risks need readable warnings and consistently applied loss rules.

### 2.4 Pillar D: Build a community one person at a time

NPCs are not just efficiency multipliers. They have names, specializations, small personal stories, well-being, and wishes. The recruited smith should inhabit the forge, unlock actual recipes, and be remembered if lost.

### 2.5 Pillar E: Simple beginning, layered depth

The first verbs are gather stone, chop wood, light fire. Subsequent layers introduce food and water, the portal, extraction, NPC job assignments, refining, abilities, boss rewards, and harder destinations. Depth should emerge from interacting mechanics rather than presenting dozens of menus on day one.

### 2.6 Deliberate exclusions

No endless wave-survival or unattended base raids; no conventional grid-and-stack material inventory as the primary resource UI; no total settlement wipe or loss of story progress after a defense failure; and no needlessly elaborate life simulator with dozens of constantly draining meters.

## 3. Audience, format, and experience goals

### 3.1 Audience and release format

The primary audience enjoys top-down combat, exploration, survival crafting, building a home, and extraction/roguelite risk-versus-reward. A secondary audience is attracted to Nordic fairy tales, folklore, and settlement stories. **Recommended first product:** a premium single-player PC title. Co-op may be investigated later but must not dictate MVP architecture.

### 3.2 Emotional journey

| **Moment**                   | **Desired emotion**           | **Required design response**                               |
|------------------------------|-------------------------------|------------------------------------------------------------|
| Awakening                    | Curiosity and comfort         | Warm clearing, simple objectives, mystery at its edge      |
| First portal                 | Wonder and unease             | Strong visual/audio contrast and obvious timer             |
| Full cargo near the deadline | Pressure and greed            | Visible stakes, recognizable way home, one last temptation |
| Successful extraction        | Relief and pride              | Clear debrief and immediate settlement benefit             |
| Rescuing a person            | Attachment and responsibility | A living new resident with a tangible contribution         |
| First troll boss             | Fear, learning, triumph       | Fair telegraphs, clear phases, distinctive rewards         |

### 3.3 Pacing

A typical raid should take roughly 10–20 minutes, plus settlement preparation. A 45–90-minute session can include several raids and substantial building. Planned defensive incidents should be short, occasional punctuation rather than a constant obligation. These are playtest targets, not promises about final session length.

## 4. World, mythology, and narrative

### 4.1 Premise: The forgotten pact

Long ago, people promised to leave the forest enough room to live, while its guardians promised to keep ancient hungering forces at bay. The pact was broken—or perhaps forgotten. Trails disappeared, villages vanished, and the shadowed forest started consuming memories, wildlife, and people. At the center survives the **Portal**, an ancient being whose protective ward still shelters one clearing. The ward is strong enough for daily peace, yet fractures in the old pact permit occasional, foreseeable intrusions.

The protagonist wakes beside its roots with no clear memory of arriving there. Portal runes respond to their touch. Tiny lights guide them toward stones where the first Life Orbs can be released. The protagonist gradually learns that they are connected to the old pact and may influence the terms of a new one.

**Why a haven.** The forest does not kill the lost: it forgets them. First the way home, then their names, then there is no one left to find. A hearth that someone tends is the one thing it cannot forget. That was the old pact: people kept a hearth beside the portal and the portal kept the night from their door, and no fire has burned there in a hundred winters. So the player, the one survivor the portal could catch, doesn't build a base for its own sake. Each fire feeds the ward and carries further through the mist, the seven lost companions can see it and know which way is home, and a home is what there is to bring them back to. The game keeps this true in its rules: renown widens the ward (§6.4), and the portal's Haven page counts who is home and names who is still out there.

### 4.2 Narrative tone rules

Take inspiration from Askeladden: humble beginnings, courage, wit, unexpected helpers, riddles, and ordinary things with magical significance. Creatures should have motivations beyond “hostile monster.” A troll may demand a toll, a forest spirit may bargain, and an apparently worthless object may resolve a conflict. The forest should be beautiful and unsettling, not uniformly bleak. Humor and quiet domestic scenes belong beside danger. The story must not depend on long cutscenes to explain mechanics.

### 4.3 Regions and biomes

Start in the protected Portal Clearing. Early raids explore the **Misty Woods** with wolves, root guardians, abandoned camps, and hidden runestones. Later regions include **Moss Marsh**, where visibility and poisonous creatures challenge preparation; **Troll Valley**, with stony passes, bridges, and powerful guardians; and **Frost Peaks**, where exposure, rare ores, and ancient shrines demand specialized gear. The names, precise ordering, and biome count are provisional.

### 4.4 Narrative arcs

**Act I – Find a home:** awaken, build shelter, restore the portal, and learn to return. *As built (November 10, 2026;
`Docs/QuestRedesign.md`, step 3):* the act ends at the Heartroot, which wakes only for an ember from the player's own fire.
Brought down, it turns out to have been the forest's guardian, its half of the pact, which it kept alone for ninety winters
after people let the hearth go cold. People broke the pact first. Its last word is for the player: "Hearth-keeper". Then
the road north opens, which the old troll that broke the stones walks. **Act II – Gather a community:** recruit survivors, investigate the lost villages, and learn what fractured the pact. **Act III – Face the old powers:** defeat or negotiate with mythical guardians, recover the history of the portal, and confront the force consuming the forest. End-state choices can affect the relationship between settlers and spirits without turning every raid into a branching cinematic narrative.

### 4.5 Storytelling channels

Environmental clues, NPC conversations, songs, short myths, event choices, landmarks, and boss encounters deliver lore. Recovered objects can unlock small stories back at home. Major story revelations are permanently recorded, so a failed extraction does not erase narrative progress already legitimately earned.

## 5. Overall gameplay loops

### 5.1 Meta loop: home to home

1.  Gather or produce safe resources and maintain food supplies.

2.  Craft weapons, armor, potions, tools, and cargo solutions.

3.  Choose a raid destination, objective, companion, and time extension.

4.  Enter the portal; fight, explore, gather, and react to events.

5.  Decide when to return; reach extraction with the loot and survivors.

6.  Purify extracted Cursed Essence and convert rewards into gear, magical skills, building tiers, defenses, and NPC jobs.

7.  Maintain the portal ward and prepare the settlement for rare, signposted breaches.

8.  Unlock more dangerous destinations and repeat with new objectives.

The settlement is the stable center; the raid is the high-risk excursion; progression is the bridge between them.

### 5.2 Combat and exploration micro loop

Notice a threat or opportunity → observe positioning and telegraphs → act with movement, attack, dodge, or ability → collect a reward or make a choice → reassess health, supply, cargo, and remaining time. No single encounter should routinely exhaust the entire raid budget.

### 5.3 Early, mid, and late game

**Early:** harvest wood, stone, food and water; build campfire and construction table; recover Life Orbs and activate the portal. Introduce the ward visually without forcing an early defense event. **Mid:** undertake raids for ore, Cursed Essence, recipes, rare materials, and recruits; purify Essence, choose perks, construct the first defenses, and grow a staffed settlement. **Late:** undertake difficult raids and boss fights, enhance equipment and skills, master mixed Purified/Cursed builds, withstand rarer advanced ward breaches, and deepen the mythology of the forest.

### 5.4 Progression model

Separate permanent progression (base, blueprints, skills, perks, learned abilities and upgrades, boss flags, recruited residents, and safely banked Purified Essence) from at-risk state (unextracted Cursed Essence, raid inventory, cargo, consumables, and accompanying NPCs). Each tier should introduce a novel choice, not only higher numbers. Deployed equipment is retained on death but takes repairable durability damage. Defense damage is a repairable settlement state rather than a reset of permanent progression.

## 6. Safe Haven: a peaceful home worth defending

### 6.1 Starting area

The initial clearing has the sleeping portal at its centre, a stream, fallen branches, loose stones and berries. Resource nodes in this region replenish slowly enough to support recovery after failure. Early construction locations are visible and reachable without combat.

### 6.2 At-home activities

Gather basics; place structures; cook and fill water; craft and refine; tend crops; sort resource storage; assign jobs; interact with residents; prepare and inspect raids. Time should feel relaxed here, with no raid countdown and no constant combat. Occasional ward breaches interrupt this calm only after a visible warning and an opportunity to prepare.

### 6.3 Safety is a design promise, not absolute invulnerability

The default state of Safe Haven is peaceful. No enemies spawn arbitrarily while the player is farming or crafting, and no attacks are simulated while the player is inside a Raid, offline, loading, or absent from the settlement. The portal ward may be breached **only during discrete, communicated defense events**. Before a breach, show its trigger, likely approach, and preparation state. If the player fails, apply bounded, repairable consequences instead of deleting all buildings, permanent unlocks, or irreplaceable NPCs. The portal may be strained but must never be permanently destroyed.

**Night at home (amended October 27, 2026).** Once the first breach has been won, the night outside the ward is no
longer empty. A few weak creatures prowl the dark beyond the ward and the light of campfires and torches. They never
step into light and cannot see or follow anyone standing in it, so the settlement itself stays a refuge; whoever walks
out into the dark is fair game. They come out of sight, close in now and then, and slink away at dawn. One more may prowl for each breach beaten back, up to eight, and after two breaches some are boars and draugar. None come during
a breach, while the player is away, or before the first breach is won. Torches built at the edge of the clearing push
the dark back: this is how the home grows into the night (see Docs/Atmosphere.md).

**What prowlers carry (amended October 31, 2026).** Prowlers drop hide and bone but no Cursed Essence. Essence is the raids' reward: at home it would be free, with nothing at risk and no extraction to survive.

### 6.4 Extending the clearing

The player may expand buildable space through runes, Portal upgrades, specific resources, or story milestones. Expansion should produce a visible change to the settlement and unlock meaningful placement options, rather than simply adding empty ground.

### 6.4.1 Base progression (November 2, 2026)

The Construction Table, Sawbench, Furnace and Forge are replaced by whole buildings with five tiers that match resource tiers: the Workshop, the Woodcutter's Lodge and the Smithy. Each building opens the next, and what the base gives feeds raids through time, Well Rested and knowledge. See [base progression](BaseProgression.md); it overrides older station names in this document.

**Second pass (November 3, 2026).** The Loom is now the Tailor's Shop and the Tanning Rack the Hunter's Lodge, both whole buildings. No upgrade is speed alone. Later tiers ask for other buildings, and new buildings open past copper: Stone Wall, Iron Trap, Runic Ward Totem, Trading Post. Ward levels open raid tiers, the portal can be improved, ranks bring settlers. Beds give a bonus and never gate anyone.

**The portal at the heart (November 3, 2026).** The Heart Tree is gone, as it blocked the view of the base. The portal stands at the centre of Safe Haven and does everything the tree did: it speaks, holds the ward, wakes the fallen, and opens one menu for raids, the ward, Portal Sparks and repairs. Asleep it is a frame of dark stone. The story is the same: the stones that dropped the player here are what caught them. As it speaks, a wordless murmur comes from the arch (`Tools/Audio/portal.py`): the player reads the words, the sound says something is speaking.

### 6.5 Architectural progression

The visual progression should move from campfire and rough timber to huts, workshops, paths, gardens, palisades, protective runestones, and a lively fairy-tale village. Defenses should visually communicate that residents are protecting a valued home, without turning every square into a fortress. Decoration is valuable, but initially subordinate to functional construction.

### 6.6 Controlled ward breaches

A ward breach is an **occasional settlement event**, not a random raid against an unloaded base. Candidate triggers include a story milestone, a deliberate ward expansion, or an announced increase in magical pressure following significant progression. When a threat is pending, the player receives a visible warning and a defined preparation opportunity. The first encounter should be scripted and forgiving; later encounters may vary in direction, enemy composition, and special objectives. Avoid punishing players immediately after a failed expedition.

**Proposed cadence for testing:** the first breach only after core building, crafting, and extraction have been learned; later events no more frequently than roughly every three to five successful raids or an equivalent amount of active progress. This is a pacing target, not an automatic raid counter. A readiness interaction at the portal can start a pending breach after the player finishes preparing; define a bounded postponement rule if pacing requires it. Never trigger an event during save/load or scene transitions.

### 6.7 Defense gameplay

A breach has four steps: **warning → preparation → defense → recovery**. During preparation, the player can repair structures, adjust the build layout, craft traps, assign defenders, and replenish the ward. During defense, enemies follow readable entry routes and threaten structures, storage, or ward anchors. Players fight using the same responsive top-down combat as in Raids while NPCs and defenses support them. Recovery summarizes protected assets, damage, injuries, consumed ammunition, and repairs. A small reward, story clue, or ward resource can make successful defense feel productive without making attacks the dominant Essence farming strategy.

### 6.8 Defensive structures and NPC roles

Start with **palisades/barricades** (routing and cover), **traps** (limited charges), **watch posts** (early warning and ranged support), and **runic ward totems** (magical protection or slowing). Some defenses primarily use Wood, Stone, and Iron; their higher tiers require Purified Essence. A guard assignment can give a recruited resident an active defense role, but essential production should not require large numbers of idle guards. Player combat remains meaningful; fully automated, invulnerable defenses should not trivialize the event.

### 6.9 Consequences and fairness

Protect player agency with clearly marked spawn lanes, predictable pathing, visible structure health, a chance to recover, and limited repair costs. Early failure might damage a wall or temporarily disable a workstation; it should not destroy major progression buildings or permanently kill foundational residents without a separately agreed, clearly warned rule. The ward can weaken temporarily, motivating repairs or an expedition, but must not create a downward spiral where failure makes rebuilding impossible. Save persistent defense structures, damage, ward state, and event resolution. Resolve each defense reward/failure exactly once.

**Implemented (October 31, 2026).** Four breaches in order, the last repeating 20% stronger each time; the next is due three successful raids after a win. A warned breach can be put off for two more raids, then the ward gives way at the next nightfall the player is home. Later breaches come in two or three waves down two or three lanes. Half the attackers go for the ward, half for the nearest building, and they turn on anyone close. Buildings are never destroyed: worn down, they are broken and out of use until mended, for 30% of the damage's build cost, all at once at the portal. The player falling doesn't end the defense. If the ward falls, the attackers leave, the ward draws in by 4 m until two more raids have been won, and the breach can be tried again.

## 7. Raids: the central system

### 7.1 Definition

A raid is an instanced, time-limited visit to an unsafe region through the portal. The player selects a destination and prepares a loadout. The region contains threats, resources, possible NPCs, events, and one or more extraction options. Rewards become persistent only under the relevant successful-extraction rules.

### 7.2 Five raid phases

**1. Briefing:** choose biome and objective; inspect threats and rewards. **2. Loadout:** commit gear, supplies, companion, optional cart equipment, and time-extension resources. **3. Exploration:** manage route, combat, loot, and emergent events. **4. Extraction:** reach an exit in time, while deciding whether to save cargo and companions. **5. Debrief:** show survivors, material yields, losses, progression, and next-step options.

### 7.3 Timer and extensions

The proposed baseline is **12 minutes**. One **Portal Spark** grants **+3 minutes**, with a prototype cap of **two extensions** purchased before departure. A roughly **60-second final danger phase** may intensify enemy pressure and provide a last extraction opportunity; this is a test candidate, not a promise of a free exit. The exact fail state and grace-period behavior must be unambiguous in both UI and implementation.

Time extensions should be a resource-planning tradeoff and never remove all tension. The clock must not secretly scale against the player after purchases. Pausing, dialogues, inventory interactions, and cutscenes need a documented timer policy.

**Time of day (October 27, 2026).** Each run takes place at an hour rolled before the portal opens and shown in the
briefing: mostly day, sometimes dusk or night. Night is the grey dark of the night veil: the player sees little beyond
arm's reach and monsters see less far too, but hear as well as ever. Night pays more (prototype: +40% cache loot, dusk
+15%) so the choice to go in at night is a real one.

**Night is dangerous, and worth it (amended October 31, 2026).** In the dark of night, in a raid or at home, creatures have
10% more health and hit no harder (balance pass, November 6, 2026: it was 30% and 25%, which with a raid's last stage made a geared tier I player lose to a wolf pack); dusk counts for a share of that. In a raid they also carry twice the Cursed Essence; at home they carry none (§6.3). A night
raid holds 30% more creatures, dusk 15%. Breach attackers carry no loot, so a defense is never the way to farm it.

### 7.4 Raid objectives

Resource run, scouting, NPC rescue, missing-person search, lost-carriage recovery, rare-item hunt, shrine restoration, contract, and boss hunt. Side objectives should be optional temptations. A boss raid can be more structured so random events do not compromise encounter fairness.

### 7.5 Planning screen

Display destination, difficulty band, likely resource types, known enemies, objective, base duration, purchased extensions, extraction rules, deployed equipment, companion risk, projected load capacity, and clear “what can be lost” language. Known landmark and scout information should appear here.

**Rumours (added October 31, 2026).** The briefing also says what is out there on this run: "A saw was heard in the deep woods, where nobody saws." Every rumour is true. What it promises stands at its own place on the map, marked by a gold beacon and listed on the HUD with its distance, for the whole raid, so a run has a reason from its first second. Two come up each run, or three with a Tracker at home. They come from the same seed as the hour and the map, so what the briefing says is what the raid holds, and some are only true after dark. A rumour only comes up while it can still matter: nobody is sought who has come home, and no plans are offered for what is already known.

### 7.6 Procedural fairness

Randomness changes the details, not whether the raid is completable. A viable route and reachable extraction must always exist. Avoid placing mutually blocking events in mandatory corridors. Use reproducible seeds and objective validation so difficulty can be tuned and bugs reproduced.

### 7.7 What only raids can give

**The Haven makes more of what it knows; it can't make anything new.** Home is reliable and renewable. What is one of a kind, lost or alive is out there, and raids are how it comes home. Materials alone aren't a reason to go: going out for iron to make gear for going out is a circle. These are worth a raid for their own sake:

| Reward | What it is | Why it has to be a raid |
| --- | --- | --- |
| **Relics** (§13.6) | Gear with a power that changes how it plays | Nobody at home can make one, and it never runs out: any piece of gear with any power that fits |
| **People** (§15.3) | The lost expedition, each with a job that does something | A person not extracted doesn't arrive |
| **Lost knowledge** | Plans for the Wainwright's Yard and the Sauna, the Moonsilver Folio | Taught only by what is found, and read only at home, so it has to survive the way back |
| **Living things** | Bees (the Beehive), wild seed (new crops), an Elder sapling (a tree that stands in the Haven) | The Haven visibly grows from what was carried home |
| **Blows against a breach** (§6.6) | Tearing down the place a coming breach gathers | A strike here means fewer and weaker attackers at home: up to 60% off, and it lasts until that breach is beaten |

Cursed Essence belongs on this list too: it comes from raids alone (§6.3).

## 8. Extraction, loss, and recovery

### 8.1 Normal extraction

Reach an active extraction point before the effective deadline, **light it** with one press and **hold its circle**: the way home fills only while the player stands inside and no hostile creature does. Leaving the circle or a creature inside it pauses the return without resetting it. A lit beacon calls out into the forest every few seconds, so creatures come to the circle: extraction is a short stand, not a button held down (prototype: 30 s in the Misty Woods, an item cost or alarm paid when lit, what was held is lost if the way goes dark first). Cargo to be recovered must be within the designated extraction radius. Clearly signal which companions, loose objects, and carts will be included. A successful return ends the instance and opens a detailed debrief.

### 8.2 What comes home?

Gathered materials, newly looted gear, carried unique items, and Cursed Essence come home if extracted according to their cargo rules. Equipment committed in the departure loadout is retained even on death; newly looted gear remains at-risk inventory until successfully extracted. Essence earned during a Raid remains provisional until extraction succeeds; it cannot be spent at home before being committed. NPCs only join the settlement when successfully brought back. Permanent knowledge may unlock on boss defeat or discovery by deliberate design; this exception should be explicit rather than implied by the general loot rules.

### 8.3 Death and anti-frustration

On raid death, the player revives at the portal. The base, known recipes, levels, boss unlocks, and story progress remain intact. Carried inventory is lost, including gathered materials, unextracted Cursed Essence, unused consumables, and newly looted items. Equipment committed in the departure loadout is retained, takes durability damage, and can be repaired at home.

**Death does not require equipment insurance or a Memory Token. Show inventory losses and equipment damage separately in the debrief, and make home repairs accessible with modest material costs so repeated failed raids do not prevent the player from returning to play.**

**Safe pocket:** a small protected slot (one slot in the prototype) whose contents are never lost, not even on death. It lets the player protect one deliberate choice, such as a rare find or a key item. **Running out of time** counts as a failed raid and follows the same rules as death.

### 8.4 Companion risk

Companions can be downed and given a clear revival window—**45 seconds**, as built (November 3, 2026). If not revived, the forest has them, as it does if they are still down when the raid ends; alive but elsewhere, they find their own way home. Their job then stays open to anyone, so a casualty blocks nothing. Named casualties prompt reactions at home, and cannot permanently block critical crafting because replacement specialists must be discoverable.

**The forest forgets the lost (decided November 3, 2026).** Nobody dies for good: "the forest does not kill the lost: it forgets them." From the next raid the briefing says they have been seen, and they stand somewhere out there, the rumour's rough mark on the map. The player has to find them, walk them to an exit and bring them out alive; if not, they stay out there and the rumour comes up again. They come home as a stranger: no bond, no job, no memory of the player. The cost is the rank and the raid it takes to get them back, not the person, and it can be paid every time. What they have forgotten comes back one memory at a time as the bond regrows, the biggest thing last. A hardcore option with permanent death could come later.

### 8.5 Carts and heavy transport

Transport options include a hand-dragged cart, a wheelbarrow, and a horse cart. The player can deposit both items and resources into transport storage and retrieve them later. Transport increases carrying capacity but occupies physical space and may struggle on narrow routes. While the player drags a cart or pushes a wheelbarrow, movement speed decreases as the total cargo weight increases. The player can release or unequip it to fight, leaving it and its cargo in place, then reattach to continue moving. It can also be abandoned to escape or recovered partially filled. Display cargo weight, capacity, the current movement penalty, whether transport is attached, and whether it is in extraction range. Route width, navigation, and collision are core design constraints, not polish details.

**As built.** A Wainwright's Yard builds a Wheelbarrow, a Handcart and a small Rowboat from planks, as kit items: using the kit sets the transport down, and it packs back into a kit once it is empty, which is how a boat reaches a pond far from the yard. All three are moved by physics, so cargo weight is real mass.

| Transport | Capacity | Overfilled to | Speed empty | At capacity | At the limit |
| --- | --- | --- | --- | --- | --- |
| Wheelbarrow | 100 | 160 | 96% | 86% | 50% |
| Handcart | 200 | 320 | 92% | 78% | 35% |
| Rowboat | 160 | 240 | 95% | 80% | 40% |

Past its capacity a transport is overfilled: it still takes more, but the speed falls away steeply, and the HUD says so in words. A handcart trails behind the player and swings wide on a turn; a barrow is pushed ahead and goes where it is pointed. A loaded cart keeps rolling when the player stops, and a heavy one can be too much for a steep bank: the strain slows the player, and if it stays stuck they let go. It stops at what stops it, so narrow routes really are a choice. The boat floats on real buoyancy, sits lower the more it carries and has to be rowed to the bank to get out; a swimmer can climb back in. A river carries it, fastest in the middle, so going downstream is quick and rowing up is hard. Left alone it is tied up and stays where it was left.

At home a load never has to pass through the bag: a cart brought near a chest sends its cargo straight to storage, and what is in a cart or boat standing near the player counts as materials for building and crafting.

A full cart on the flat costs nothing, but pulling one overfilled or uphill takes stamina, more the heavier it is, and rowing costs about what swimming does. Out of stamina the player only slows down: a cart still moves and a boat still gets home.

Attacking, guarding, dodging or casting lets go at once, with no key to press, and leaves the cart where it stands. A cart in the hands goes through the portal, makes noise as it rolls, and comes home if it is within 12 m of the player at extraction. Otherwise it stays behind, and a failed raid loses it and its cargo. In a raid the HUD keeps showing a cart that was let go, with how far off it is and whether it would come home, and lighting an extraction point warns about a cart out of reach. The portal's briefing names what the cart stakes, its load counts towards what the HUD shows at risk, and the report on the raid says what became of it. A loaded cart left unattended in a raid gives off a scent that draws creatures, and anything that reaches it tears cargo out a bite at a time, so letting go to fight is still right but a cart left for long comes back lighter. A cart in the hands doesn't get in the way of the portal, a chest or anything else in reach: Interact goes to those first. Essences stay in their bars, as they weigh nothing: only items go in the cargo.

## 9. Raid world, discovery, and events

### 9.1 Map assembly

Use **hand-authored terrain modules assembled procedurally**. This provides predictable fight arenas, readable lanes, and visually coherent Nordic environments while preserving variety. Define generation rules for entry, main paths, branches, harvest pockets, event locations, boss arenas, and exits.

**As built (October 31, 2026): generated from rules, not assembled from modules.** The Misty Woods is laid out anew for every run from the raid's seed, the way a roguelike lays out a level, so the same seed always gives the same map:

1. The start goes on the edge of the map and three exits across from it, one ahead and one to either side.
2. Clearings fill the land between, spread evenly.
3. Trails join them all (a spanning tree, so everything can be reached), with a few loops so there is often another way round. The **shape** decides how: a **Hub** where every trail meets at one great clearing, a **Ring** around wild ground nobody crosses, a **Spine** with one long trail and branches, or a **Thicket** that tangles.
4. Sites go where their kind belongs: camps in clearings, what is worth the walk in dead ends, resources and caches in pockets off the trails, patrols on the trails, the raid's great troll in the clearing farthest from the start.
5. The trails run along the valley floors and the wild ground rises between them, in places as cliffs, so what was joined is what can be walked.
6. The **mood** dresses it: Old Growth, Windthrow, The Hollows, Open Glades or Pinewood change the trees, the ground, the fog and the width of the trails. The briefing names the shape and mood before the player commits.

Before the raid starts, a path from the start to every exit is checked on the navmesh, and the map is laid out again if there isn't one (§7.6). The hand-painted map remains as what a raid without layout rules plays.

### 9.2 Points of interest

An abandoned woodcutter camp; an overturned ore cart; a pond where the Nøkk offers a bargain; a troll bridge with riddle, payment, or combat options; a Life Orb altar; a locked old smithy with blueprints; and a cursed tree that may be cleansed for a modest permanent bonus.

### 9.3 Random events

| **Event**                 | **Warning / decision**             | **Potential outcome**                      |
|---------------------------|------------------------------------|--------------------------------------------|
| Help from the Ditch       | Cries from a side path             | Rescue a recruit, or spend time fighting   |
| Three Wishes              | A stranger offers three choices    | A reward with an understandable price      |
| Troll at the Bridge       | Huge tracks and distant laughter   | Detour, bargain, riddle, or fight          |
| Thickening Fog            | Birdsong and visibility change     | Harder navigation, rare spirit sightings   |
| Overturned Trader's Wagon | Broken wheel and scattered parcels | Great loot, an ambush, or repair objective |
| The Living Path           | Runestones shift positions         | Solve for shortcut or lose time            |
| Beacon Light              | A glow on a distant slope          | Reveal the map or a temporary refuge       |

**Implemented events (October 30, 2026):** ten for the Misty Woods. Each gives a warning, then usually a place to reach, marked on the HUD and by a pale beacon, and choices that state their price.

| **Event**            | **Based on**              | **The choice**                                                                 |
|----------------------|---------------------------|--------------------------------------------------------------------------------|
| Cries from the Ditch | Help from the Ditch       | Free a pinned pedlar (time and noise), or take his pack and carry the guilt    |
| Three Wishes         | Three Wishes              | Strength for health, riches for hunters, or time borrowed against health       |
| Overturned Wagon     | Overturned Trader's Wagon | Search slowly, grab it loudly (likely ambush), or mend the wheel for a reward  |
| The Toll Troll       | Troll at the Bridge       | Pay in bones, answer a riddle, or walk through it. Needs no bridge             |
| Thickening Fog       | Thickening Fog            | A fog bank with Marsh Lights around a chest nobody left by accident            |
| Old Beacon           | Beacon Light              | Light it to see the land and be seen, or keep a small fire and heal            |
| The Living Path      | The Living Path           | Three runestones: a rhyme says which, the wrong one costs time                 |
| The Oskorei          | New                       | A horn, then Draug and wolves hunting the player. No site, from the first stage |
| The Open Barrow      | New                       | Take the silver and wake a Draug, or leave an offering for a blessing          |
| The Blighted Tree    | §9.2 cursed tree          | Cut out the rot among Rootlings, or drink the sap: strength and poison         |

The raid clock keeps running while the player chooses, and walking away leaves a site open. The raid report lists what happened and what was chosen.

### 9.4 Event budgets

Limit the number of major events per raid and give each biome its own curated pool. Do not allow two major encounters to block the same choke point. Boss missions prioritize predictable encounter conditions. Threat tiers and resident rumors may influence what spawns.

**Implemented budget:** the first event comes 45 seconds in and the next about 110 seconds apart (±35), at most four in a raid and each at most once. No new event starts while two sites still wait for the player. Events are picked from the raid's seed and each sets the time window it can happen in. All of it is data per raid.

### 9.5 Exploration and mapping

Reveal local maps as the player travels: ground never seen lies in dark fog until the player has been near it
(the fog of war; remembered at home, fresh every raid). At night, everything beyond the reach of sight and light lies
under a grey, colourless veil. Keep major biome discoveries and portal landmarks persistent; vary minor loot and hazards between runs. A recruited scout may reveal a useful point of interest or safe path before departure.

**The minimap (added October 31, 2026).** A round map in the corner shows what has been explored and nothing more: ground still in the fog of war stays dark, at home and on every raid. It turns with the camera. Over it go the player, buildings, carts and dropped items once found, companions, the quest in hand, and creatures close enough to be seen, a much shorter way off at night. In a raid it also shows every exit, open events and, roughly, what rumours point to, as the raid's list of exits always told the player they were there. A ward breach shows its lanes and every attacker, so the player can see where to go. M zooms out and hides it.

## 10. Movement, combat, and enemies

### 10.1 Perspective and controls

Use responsive, isometric/top-down 3D movement and directional attacks, broadly inspired by the fluid feel of V Rising but with original abilities, identity, and systems. PC defaults: **WASD** movement, mouse aiming, mouse attacks, hotkey abilities. Controller defaults: left stick movement, right stick aiming. Camera follows smoothly and can zoom out slightly for large fights. An optional over-the-shoulder action camera (Enshrouded style, toggled with **V**) is available as a player preference; players can force either style per situation (exploration, combat, building, raid). Top-down remains the default and the reference for encounter readability.

### 10.2 Core input actions

| **Action**         | **Proposed input**  | **Purpose**                         |
|--------------------|---------------------|-------------------------------------|
| Move               | WASD / left stick   | 360-degree traversal                |
| Aim                | Mouse / right stick | Attack and ability direction        |
| Basic attack       | Left mouse          | Main weapon sequence                |
| Weapon art         | Middle mouse        | Weapon-specific identity, paid with Focus, or with stamina when Focus is short (also right then left mouse) |
| Dodge              | Space               | Timed avoidance with cooldown       |
| Active skills      | Q, E, R             | Equipped abilities and boss rewards |
| Interact / harvest | F                   | Gather, speak, rescue, activate     |
| Quick consumables  | 1–4                 | Food, potions, tools, and torches   |

### 10.3 Combat feel

Combat should be driven by positioning and readable telegraphs. Enemies have distinguishable anticipation, active hit, and recovery windows. Hit detection must be dependable. Moving while aiming is generally allowed, while heavy weapons commit the player for longer. A swing plants the feet from its wind-up until the blow lands, with only the attack's own lunge carrying the player in, and the body stays facing the blow; once it has landed the player can walk out of the follow-through, though the next attack still waits for the swing to finish, and a dodge can always break off a wind-up. Dodge has a short invulnerability window and cannot be used without a meaningful cooldown. **Prototype dodge target:** approximately **0.35 seconds** duration, with a **0.9-second** cooldown in the build, stamina being what really limits dodging (the prototype target was 2–3 seconds); adjust after enemy timings and animations are playable.

### 10.4 Weapon families

| **Weapon**    | **Playstyle**           | **Identity**                                               |
|---------------|-------------------------|------------------------------------------------------------|
| Axe           | Strong melee / clearing | Wide swings, effective versus wooden guardians and shields |
| Sword         | Flexible melee          | Quick sequences, parries or counters                       |
| Bow           | Range / positioning     | Charged shots, traps, sight lines                          |
| Runic staff   | Magic / control         | Projectiles, area control, support                         |
| Spear (later) | Reach / spacing         | Thrusts, throws, area denial                               |

There are no locked classes; chosen gear and abilities define a build. Gathering should not require endlessly repeating full combat animations against ordinary rocks.

**Implemented gear (October 30, 2026):** weapons now fill the tiers: Copper Sword, Hand Axe and Spear at the Construction Table, so metal arrives before the Forge; the Venom Fang, a quick dagger whose cuts can poison; Iron Greatsword and War Hammer, whose heavy blow breaks poise; the Grave Blade from barrow iron; the Trollstone Maul; the Moonsilver Blade; and the Elderwood Longbow, with Bodkin Arrows.

**Armour** comes in four sets, and weight decides the dodge: Cured Leather (light, for moving and shooting), Copper Scale (the first metal), Iron Mail (heavy, the dodge becomes a shoulder charge) and Moonsilver (light for its strength, against what isn't flesh). Eight amulets and rings fill the jewellery slots.

**Shields** go in the off hand beside a one-handed weapon: Round Shield, Copper-Bound Shield and Iron Kite Shield, each stopping more of a block. Guarding and attacking bashes.

### 10.5 Enemy archetypes

**Forest-Bitten Wolf:** fast melee pressure. **Root Guardian:** slow, heavy, telegraphed sweeps. **Marsh Light:** illusions and ranged harassment. **Stone Thrower:** distance control. **Troll:** slow devastating blows and environmental destruction. Each enemy should communicate its identity visually, support at least one readable counterplay strategy, and reward attentive movement.

**Implemented encounter expansion (October 16, 2026):**

- **Small Moss Troll:** maintains distance and fires three fast, separately aimed, non-homing stones, followed by
  recovery. Stones collide with scenery. A weak close-range shove prevents melee pressure from becoming a free kill.
- **Mistwing:** hovers low over walkable terrain and follows the player at a distance. Its charge has a committed,
  readable wind-up; after either a hit or miss it lands within melee reach for two seconds, then retreats.
- **Wolf packs:** two or three wolves, staggered attack starts and one designated howler. Its damage-interruptible
  howl grants nearby allied monsters, including itself, +5% damage bonus for eight seconds within ten metres.
  Further howls refresh rather than stack. Alarm and escalation spawns retain individual wolves.
- **Enemy authoring:** choose Chase or Keep Distance movement and combine Aggressive, Cowardly and Defensive
  temperaments. Enemies can have multiple actions with independent ranges, cooldowns and priorities. Cowardly
  enemies retreat at low health and re-engage after gaining space; defensive enemies stay near their home.

The new creatures use placeholder visuals and appear at low frequency beyond the Misty Woods entry area.

**Apex creature (October 27, 2026): the Old Moss Troll.** One per Misty Woods raid, walking a slow round through the
middle of the map, around the Hollow Oak. It is heard before it is seen: heavy steps that shake the ground. Its eyes
are weak, but it smells a person close by and hears far (§10.8). It is meant to be avoided, not farmed: huge health,
hardly staggers, slow but crushing sweeps and stomps. Killing it is possible for a prepared player and pays in
Trollstone, the material the Forge needs.
These values remain editable prototyping defaults pending encounter playtesting.

**Implemented creatures (October 30, 2026):** six more for the Misty Woods, each with its own answer.

- **Marsh Light:** a hovering light that keeps its distance, slows with cold bolts and flees when hurt (the §10.5 archetype, without illusions yet).
- **Draug:** a slow armoured dead warrior, hard to stagger, that chills on a hit and lunges from afar. Fire hurts it more; frost less.
- **Barrow Shieldbearer:** a Draug with a shield, slow to turn. Nothing much gets through from the front, so it has to be flanked, caught striking or broken with something heavy.
- **Bog Spitter:** spits poison from range and is weak up close.
- **Blight Boar:** leaves the player alone at a distance, charges inside its ground, then stands open.
- **Rootling:** small, fast and in swarms; a grasp can hold the feet.
- **Moss Shaman:** hangs back, calls up Rootlings and rallies its side. The target to reach first.

Their bodies are placeholders, and they appear through the raid's spawn lists and events.

### 10.6 Status effects

**Poison:** damage over time, cleansed or mitigated by herbs and brews. **Frost:** slows movement/attacks; clothing or warming food helps. **Roots:** brief ground-telegraphed immobilization. **Enchanted:** temporarily weakens healing or abilities and can be cleansed. **Warmth / Courage:** beneficial effects from meals, equipment, and companions. Keep status icons distinct and accessible.

**Implemented (October 30, 2026):** Poison (health drains; Antidote cures it), Chill (slower; Warming Brew cures it) and Roots (held briefly, free to fight). A hit that is blocked or parried leaves nothing. Ticks don't flinch, can't be guarded and don't count as a perfect dodge. A new hit restarts the time; effects don't stack. They show as words above the vitals until they have icons. Enchanted, and Warmth and Courage as effects of their own, are not built; meals cover the beneficial side for now.

### 10.7 Example boss: The Brukam Troll

A massive troll guards a stone bridge in Troll Valley. Phase one teaches a wide club sweep, a ground slam, and thrown boulders. Phase two destroys parts of the bridge, reducing safe ground while creating openings behind the boss. Phase three tempts the player with a valuable side cache as time runs low. Winning grants a permanent Mountain Leap ability (short leap and shockwave) through a boss sigil. Rare trollstone and newly looted equipment still need successful extraction. The permanent skill unlock remains even if the player dies before bringing the physical loot home.

### 10.8 Senses and stealth

Danger should come from creatures that notice the player, not from creatures that simply know. Monsters **see** in a
cone that terrain, trees, rocks and buildings block, **notice anything very close** from any direction, and **hear**
footsteps, dodges, fights and gathering. A glimpse or a sound makes a monster **suspicious** (a "?" over it): it comes
to look and searches the spot. Seeing the player long enough **spots** them (a "!" and a low bell): it hunts, and its
call brings idle monsters nearby to look. A hunted player who breaks line of sight for a few seconds is **lost**: the
monster searches where it last saw them, then goes home. Wolves hear keenly, Mistwings see wide from above, Root
Guardians are slow to notice.

The player can **sneak** (hold Left Ctrl / left bumper): crouched, at half speed, with much quieter footsteps, seen from
closer and more slowly. Standing still helps a little; fighting makes the player stand out. A hit on an unaware monster
deals extra damage (an ambush). Sneaking is a way to avoid or open fights, never a requirement: combat stays as in
§10.3, and ward-breach attackers and hunt waves always know where the player is.

**Noise** is a single number the player can read: the HUD's meter shows how many metres they can be heard right now, with
a word for it (Quiet, Audible, Loud, Deafening). Everything the player does feeds it: footsteps, swimming, hard landings,
dodges, fights, gathering, the bow and picking locks. Heavy gear makes movement louder and rain hides noise. A run of
loud sounds builds on itself, so a long fight carries further than one blow and stopping lets it fade. Cover muffles a
sound, and a far sound only tells a monster roughly where to look: near sounds bring it straight to the player.

**Locked chests:** a chest has to be picked before it can be searched, with a timing bar like gathering's. Each pin set
in the green zone is silent, in the yellow a click; a slip rattles the lock and drops a pin. Too many slips and the lock
gives way, loudly, so a chest can always be opened, only not always quietly. The lid is heard either way. Chests are a
risk to weigh against the noise of opening them, not a free stop on the way to the exit.

Fog hides the player too: in dense fog monsters see only about half as far, so misty weather and foggy hollows are
places to slip past danger.

Prototype values (tunable in the Systems Editor): spotting a plainly visible player takes about 0.6 s close up and
3 s at the edge of sight; a lost target is searched for 6 s after 3 s out of sight; footsteps carry 5 m walking and
1.2 m sneaking; an ambush deals +50%. Night, fog and light will later change visibility and sight range.

### 10.9 Horses (November 11, 2026)

A wild horse comes, rarely, to graze on the land round the Haven (never more than one at a time). It is caught, not bought:
- **Sneak up.** It bolts from a walking player at about 10 m and from a sneaking one at about 6; the **lasso** (4 nettle cord and a rough hide, by hand) reaches 9.
- **Hold it.** Roped, it fights in random directions. Tap E or left click to hold it: the rope strains while it pulls and each tap eases the strain and tires it. At full strain it tears free and runs. Worn out, it stands for a few seconds to be mounted.
- **Break it in.** On its back, it asks for **barley** every 10-30 seconds. Unfed in time, or got off, it throws the rider and bolts. Fed 3-5 times (each horse its own number), it is the player's, and they **name** it.

Every horse rolls its own **health, stamina, gallop speed and carry weight** (and a coat), so one is worth more taming than another. Ridden, it trots, **gallops on its stamina** (Space) or walks; a **Hide Saddle** (10 rough hide at the Workshop) gives it **8 slots of saddle bags**, as much as it can carry before slowing. A cart can be hitched to it. Monsters attack horses as they attack the player's side. Hurt, a horse runs; killed, it drops its saddle and load where it fell. Owned horses show on the minimap and the world map, and interacting with one shows its name, numbers and bags.

## 11. Survival, cooking, drinking, and farming

### 11.1 Design purpose

Survival should make preparation meaningful without competing constantly with combat and exploration. Hunger and thirst decline gradually; poor condition affects performance, especially in raids. The player should not starve to death while peacefully organizing the settlement.

### 11.2 Status and countermeasures

| **Meter**                     | **Well supplied**                  | **Depleted**                            | **Recovery**                  |
|-------------------------------|------------------------------------|-----------------------------------------|-------------------------------|
| Hunger                        | Normal stamina and natural healing | Lower max stamina / weaker regeneration | Berries, soup, meat, bread    |
| Thirst                        | Normal stamina regeneration        | Slower regeneration during combat       | Water bottle, drinks, stews   |
| Temperature (selected biomes) | Comfortable performance            | Cold/heat debuffs after exposure        | Clothing, fire, food, potions |

### 11.3 Farming cycle

Build plots → plant seeds → water as appropriate → wait for growth → harvest. A gardener NPC can automate ordinary maintenance if seeds and water exist in storage. Advanced crops require seeds recovered on raids. A small starting crop set should expand into meaningful cooking recipes rather than a large mandatory farm simulator.

**Implemented (October 30, 2026):** a Farm Plot is a station whose recipes are crops: one Wild Turnip or Barley goes in and several come out minutes later, and they keep growing during raids. Seeds come from wild plants in raids and from supply caches, which always offer at least one of each. A Beehive turns berries into honey the same way. Watering and a gardener NPC are not built.

### 11.4 Meals as preparation

Simple food refills meters; cooked dishes additionally grant time-limited bonuses such as frost protection, extra stamina, or gathering speed. Limited meal slots support loadout planning. Recipes can combine safe ingredients with raid-only herbs and mushrooms.

**Implemented (October 30, 2026):** simple food from the Campfire fills meters, and meals from the Kitchen also give bonuses for several minutes: Strength, Maximum Stamina, Maximum Health, Armor, Move Speed, Frost Resistance, Gather Yield or Damage, some at a price (Honey Mead hits harder and protects less). Two meals work at once; a third pushes out the one closest to ending. Tonics from the Cauldron take no meal slot and are for the fight rather than the road. Bonuses go through the portal, are lost on a failed raid and aren't kept in a save file. A Sauna gives a similar lasting warmth at home.

## 12. Resources, categories, and inventory

### 12.1 Core rule: materials are items, essences are bars

**Physical materials are items.** Grey Stone, Oak Wood, Iron Ore, planks, ingots, hides, berries and herbs stack in the bag grid like any other item. Every item has a **weight**, so materials compete with weapons, loot and supplies for both slots and carrying capacity. This is the core extraction decision: *what do I leave behind?*

**Essences are metered bars.** Life Orbs, Life Motes, Portal Sparks, Cursed and Purified Essence are magical, weightless and shown as bars (e.g. **Life Orb 12/100**). They feed the portal, the portal, purification and special recipes. They have per-essence caps but no weight.

Equipment, weapons, potions and unique quest items remain items with their own slots and quick-access bar.

### 12.2 Data definition

Materials are **ItemDefinitions** with category Material (id, name, icon, tier, value, **weight**, **maximum stack**, and optional hunger/thirst for edible ones). An **Essence** is a **Resource Definition** (id, name, category, tier, icon, color) grouped by a **Resource Category**. A **Gathering Node Definition** describes a harvestable node (tool and tier, seconds, yields, respawn) and drives its prefab. All are edited in the Content Editor without code.

### 12.3 Example category taxonomy

| **Main category** | **Resource examples**                                                | **Typical use**                                            |
|-------------------|----------------------------------------------------------------------|------------------------------------------------------------|
| Raw resources     | Grey Stone, Oak Wood, Iron Ore                                       | Early construction and refining                            |
| Refined resources | Stone Block, Oak Plank, Iron Ingot                                   | Workshops, advanced gear                                   |
| Organic resources | Berries, Mushrooms, Leather, Healing Herb                            | Food, clothing, brewing                                    |
| Magical resources | Life Orb, Portal Spark, Cursed Essence, Purified Essence, Trollstone | Portal, magical progression, raid options, special recipes |
| Quest / special   | Ancient Rune, Sealed Letter                                          | Discovery, contracts, narrative                            |

**Added October 30, 2026:** organic: Chanterelle, Wild Turnip, Barley, Cloudberries, Honey and Boar Meat; refined: Cured Leather (Tanning Rack); from creatures: Boar Tusk, Bog Venom, Wisp Ember, Grave Iron and Rootling Bark. Materials that had little use (Moonsilver, Huldra Silk, Elderwood, Deepstone) now go into late gear.

### 12.4 Capacity and weight

The bag has a 150-point carrying budget (hard cap for now) plus slot limits. Backpacks add slots and carry weight (Bark Pack +10, Woven Satchel +20, Hide Rucksack +40). The safe pocket and worn gear don't count. Heavy raw materials (ore 2, stone 1.5) are cheap per weight, refined goods (ingots, planks) and valuables are dense in value, so the most profitable cargo may not be the most efficient. Essences are weightless and capped per essence. Carts and wheelbarrows have their own cargo storage for items and resources, with capacity and cargo weight tracked separately from the player. A cart has a proposed +200-point capacity; capacity varies by transport type. For hand-dragged carts and wheelbarrows, heavier cargo progressively reduces movement speed while attached. Configure capacity, item/resource weights, and the weight-to-speed curve in Unity assets. Per-resource caps and overall carrying weight serve different purposes and must display separately. Storage structures can hold materials safely at home: every chest opens the same stash, sized by the biggest chest standing (a Small Chest from the start: 12 slots; the Storage Shed: all 60).

### 12.5 Gathering and drops

Harvest nodes (Gathering Node Definitions) define required tools, interaction duration, item yield ranges, and respawn policy. Gathered materials go to the bag; what doesn't fit by slots or weight is left behind and reported. Enemies and special events use loot tables, including quantity ranges, rarity, and conditions. The same definition drives harvesting, loot, UI, recipes, and save data. Cursed Essence is primarily earned through enemy drops, elite encounters, bosses, and extraction bonuses, with stable, configurable loot tables. A node might drop **3–5 Grey Stone** in **1.5 seconds** and slowly respawn in Safe Haven, while raid nodes reset each run.

**Bootstrap progression:** Tier 0 includes hand-picked Sticks, Small Rocks, and Berries. Sticks and Small Rocks make the first stone tools by hand, without requiring harvested wood, Grey Stone, or a station. Bare hands have tool strength 0; Oak Wood requires an equipped Tier I axe and Grey Stone requires an equipped Tier I pickaxe. Stone Hatchet has tool strength I and cannot harvest Hardwood. Stone Pickaxe deliberately retains tool strength II so the player can obtain copper for the first copper tools instead of needing a copper pickaxe to obtain its own ingredients. Item tier and tool strength are separate values. Higher-tier tools can still harvest lower-tier nodes.

**Swinging tools (axe, pickaxe, sickle)** use a timing minigame instead of a hold: a marker rises up a vertical bar as the tool is raised, and each node takes a set number of swings (trees 3, ores 2, fibre plants 5 by default). Timing is a reward, never a chore: left alone, the tool comes down by itself at the top of the bar as a normal swing, at a calm pace (about 2.2 s for an axe). The sweet spot stays in the same place, so it can be felt as a rhythm, like an active reload. Each tool has its own rhythm: the sickle rises faster and is much quieter (quick cuts), the pickaxe is slower and loudest. A press in the **critical** zone grants +1 of the main yield with a brief hit-stop, and a streak widens the zone; the yellow zone is a normal swing; only a press well before the zones is a **glancing blow**, which is loud and costs the tool 2% durability. Every swing makes the node itself react (it rocks away from the hit, chips/sparks fly; harder on a crit) and a gathered node breaks (debris, shrinks and sinks) and pops back when it regrows. This feel comes from shared **Harvest Feedback** profiles (Wood, Stone, Ore, Fibre) picked per node, with a Default profile as fallback; per-prefab one-offs hook the ResourceNodeFeedback events. Hand-picked nodes (berries, herbs, motes) stay hold-to-gather. Consecutive crits widen the crit zone (combo), higher tool tiers widen it too, and taking damage interrupts the swing. An auto-gather accessibility option replaces the bar with a normal swing each time. In raids every swing makes noise (scaled by the tool's loudness) that wakes nearby sleeping monsters and raises the **Forest Annoyance** (crits add only ~25% of a normal swing's noise). Annoyance decays while the player is quiet and escalates through *Stirring* (more roamers, faster spawns), *Angry* (monsters hunt the player) and *Wrath* (one wave of the raid's wrath spawn list). Each raid tunes its own annoyance: the Misty Woods is forgiving (slow rise, fast calm), Troll Valley is touchy (fast rise, slow calm, lower thresholds). The raid summary reports swings, crit rate, glancing blows and peak annoyance, rewarding quiet play.
### 12.6 Unique items

Weapons, armor, potions, carts, and narrative artifacts do not need to behave like anonymous material bars. Their durability, traits, ownership, and loss rules belong in an equipment/item system. Keep the distinction visible and technically consistent.

## 13. Crafting, refining, and recipes

### 13.1 Philosophy

Crafting converts risky expeditions into visible safety and competence. Basic recipes should be understandable and discoverable through stations, blueprints, quests, or NPC knowledge. Keep early production chains short; introduce multiple stages only when the new step creates a meaningful strategic choice.

The bag and every station use one recipe browser (§18.8), so crafting works the same everywhere.

### 13.2 Example chains

**Oak Wood → Oak Plank → advanced building**; **Iron Ore → Iron Ingot → weapon/tool upgrade**; **Grey Stone → Stone Block → sturdy construction**; **Berries + water → basic meal**; **Healing Herb + supporting ingredients → restorative brew**. Life Orbs and Portal Sparks interact with portal progression and raid planning rather than simply acting as generic currency. **Cursed Essence → Purified Essence** forms a separate magical refining chain; the initial prototype exchange is **2 Cursed Essence → 1 Purified Essence**, with a short processing time at a Purification Altar.

### 13.3 Crafting tiers

**Tier 0:** hand-picked starting materials and food; handcraft the first tools from Sticks and Small Rocks. **Tier 1:** stone tools, Oak Wood and Grey Stone, campfire, construction table, sawbench, simple storage. **Tier 2:** furnace, first metals, and upgraded tools. **Tier 3+:** forge and iron work, advanced enchantment, boss materials, specialized NPC workstations. These are progression groupings, not a second station-level system; buildings derive their planner tier from their costs. Recipes should specify required station, ingredients, craft time, output quantity, and unlock conditions.

### 13.4 Repair and economy

Equipment takes durability damage on death and can be repaired at home for a clear, modest material cost. It is retained rather than permanently destroyed. Tune damage and repair costs to create a useful resource sink without punishing experimentation. Avoid systems where one death forces extended dull farming to re-enter a raid. Resource sinks should motivate the next expedition without making ordinary gear prohibitively expensive.

### 13.5 Essence economy and purification

**Cursed Essence** is high-value raid loot and must usually be extracted to become permanent. **Purified Essence** is produced safely in the settlement and is a predictable ingredient for magical upgrades. Introduce a **Purification Altar** after the player has learned the basic Raid loop; it should use a short process or queue rather than a long mandatory wait. The first version can use a 2:1 ratio, with altar upgrades improving throughput rather than creating a convoluted new currency system.

Essence has three main sinks: **equipment tier upgrades/enchantments**, **selected building and ward tiers**, and **skill enhancements**. Basic structures and initial boss-earned skill unlocks should not require Essence. Important tier transitions can additionally require normal materials and a boss-specific token, preventing unlimited farming of easy enemies from bypassing progression gates. Treat drop counts and prices as tunable data; aim for ordinary early upgrades after around one or two successful Raids and larger midgame milestones after a few more, rather than excessive grind.

**Design guardrail:** Essence augments ordinary gathering rather than replacing it. A tier-II iron weapon still requires iron; Purified Essence empowers it.

**The choice at the altar.** Every Cursed Essence that comes home asks one question: purify it at 2:1 into something safe, or spend it raw on something stronger that costs you. Both are deterministic, so the risk is a known price and never a roll: nothing fails, breaks or wastes essence.

| Use | Pays with | What it does |
| --- | --- | --- |
| **Empower** gear, +1 to +3 | Purified Essence by tier, and a share of the item's own recipe materials | Adds a share of the item's stats; weapons hit harder; tools yield more and wear less |
| **Enchant** gear, one per item | Purified Essence | A safe bonus, with no drawback |
| **Enchant** gear, one per item | Cursed Essence, raw | A stronger bonus with a stated price: *Bloodletting* +15% damage, -15 health. Replacing an enchantment costs the new one in full |
| **Deepen** a skill, ranks I to III | Purified Essence | More power (damage, healing, buffs) and a shorter cooldown |
| **Ward and great building tiers** | Purified Essence | Portal ward levels; the top tier of the altar, Cauldron and Construction Table |
| **Draughts** at a Cauldron | Purified or Cursed Essence, and herbs | Repeatable tonics, so essence still matters once everything is upgraded |

Empowering and enchanting belong to the one item, survive a failed raid with it and are shown in its name: *Gravebound Iron Mail Hauberk +1*. In a slot the level stands beside the tier and an enchantment colours the frame and adds a mark; in the hand a weapon sheds motes in its school's colour, stronger the more it has been empowered. Raw Cursed work happens at once, while purifying waits in the queue, which is part of the choice. Two enchantments feed the loop back: *Reaping* finds more Cursed Essence, *Hollow* drains life from what it hits. Upgrading the altar makes it purify faster. All of this happens in one window at the Purification Altar: Purify, Empower, Enchant and Skills.

### 13.6 Relics

A **relic** is a piece of gear carrying a power nobody at home can make. Where an enchantment adds numbers, a relic changes how the game is played, and many state a price:

| Power | Fits | What it does | Its price |
| --- | --- | --- | --- |
| Wolf-Fed | Weapons | Every kill gives back 6% health and 20% stamina | -10 health |
| Hushed | Armor, jewelry | Every sound you make carries half as far | -10 stamina |
| Twice-Buried | Armor, jewelry | Once each raid, a killing blow leaves you standing at 30% health | -5 health |
| Moonbound | Weapons, jewelry | In the dark, 30% more damage and 10% more speed | 15% less damage by day |
| Harefoot | Armor | 40% more speed for 2 seconds after a dodge | |
| Far-Seeing | Armor, jewelry | The map fills in 32 metres around you | |
| Trollblooded | Armor | After 8 seconds unhurt, wounds close by themselves | 6% less speed |
| Rime-Touched | Weapons | 30% of hits leave Chill behind | |

A relic is any piece of gear of up to one tier above the raid with any power that fits it, so finding another stays worthwhile. They come from rumours, from the raid's great troll, and now and then (6%) from a supply cache. A relic found in a raid is at risk like any find until extracted, and comes home in the safe pocket if the player thinks it worth it. Once home it can be empowered like any gear, but the altar can neither make a relic nor replace its power.

## 14. Base building and structures

### 14.1 Building rules

Place buildings within valid safe plots using a ghost preview, snapping rules where appropriate, collision checks, and clear material requirements. Allow repositioning or refunds under defined rules to encourage creativity. Buildings can require prerequisite blueprints, foundations, terrain conditions, or staffing. Defensive structures must declare health, valid placements, attack arcs or trigger zones, repair rules, and persistence behavior.

**The build menu (G)** is a panel docked along the bottom of the screen, so the home stays in view above it. Tabs sort what the player knows how to build into Crafting Stations, Walls & Fences, Structures and Manage (upgrade, repair or move what stands). Each building is a card with its picture, name and cost; cards the player can't afford yet are dimmed with their cost in red. Pointing at a card shows its description, what it brings (renown, levels, a yard to keep clear, built in lines) and each material it needs against what the player has. Clicking a card starts placing it.

### 14.2 Suggested structures

| **Structure**        | **Function**                                           | **Progression tier** |
|----------------------|--------------------------------------------------------|----------------------|
| Campfire             | Light, warmth, basic cooking                           | Starting             |
| Construction Table   | Basic building and recipes                             | Starting             |
| Storage Shed         | Safer material storage / organization                  | Early                |
| Sawbench             | Refine wood into planks                                | Early                |
| Furnace              | Smelt ore                                              | Mid                  |
| Forge                | Craft and repair metal equipment                       | Mid                  |
| Kitchen / Cauldron   | Better food and potions                                | Mid                  |
| Farm Plot            | Repeatable food production                             | Early–mid            |
| NPC House            | Accommodation and village identity                     | Mid                  |
| Scout Post           | Raid information and rumors                            | Mid                  |
| Portal Shrine        | Raid access and modifiers                              | Starting / upgrades  |
| Purification Altar   | Convert extracted Cursed Essence into Purified Essence | Early–mid            |
| Palisade / Barricade | Guide and delay enemies during a ward breach           | Mid                  |
| Watch Post / Trap    | Provide warnings, cover, or limited-use damage         | Mid                  |
| Runic Ward Totem     | Improve defense of a key approach or ward anchor       | Mid–late             |

**Implemented (October 30, 2026):** Farm Plot, Kitchen and Watch Post (a raised fire that lights the night) as in the table. A Spike Trap hurts enemies that walk over it. Added beyond the table: Tanning Rack, Beehive, Well (water inside the ward), Sauna (lasting warmth before a raid) and Cottage (Renown for now, with no one living in it yet). Not built: Scout Post and Runic Ward Totem.

### 14.3 Early recipes

The first campfire should consume locally available wood and stone; the construction table should similarly be attainable without a raid. The portal repair requires initial Life Orbs obtained through nearby tutorial mysteries. Subsequent sawbench, furnace, and accommodation recipes combine safe materials with raid-only resources. Keep exact counts editable in Unity assets, not hardcoded.

### 14.4 Construction and labor

Staffed stations provide convenience, processing access, or efficiency bonuses. NPC activity should be visible so that hiring someone feels like changing the world. Critical progression must remain recoverable even if a specialist dies; another route to essential recipes should exist.

## 15. NPC recruitment, roles, and relationships

### 15.1 Discovering recruits

Find people trapped in abandoned camps, pursued through forest paths, imprisoned by trolls, or hidden behind event choices. NPC opportunities can be advertised by settlement rumors and raid briefing hints. Not every encounter requires fighting; some need food, negotiation, escorts, or clever solutions.

### 15.2 Recruitment loop

Find → make contact → meet conditions or secure rescue → escort to extraction → debrief → welcome at Safe Haven → assign home and job → deepen the relationship over subsequent visits. A person not extracted does not magically arrive at the base.

### 15.3 Roles and useful contributions

**Woodcutter:** processes and occasionally gathers nearby wood. **Miner:** supports stone and ore processing. **Farmer/Gardener:** tends cultivated plots. **Blacksmith:** expands weapon and repair options. **Cook/Herbalist:** offers meals, brews, and recipes. **Scout:** reveals raid opportunities, route intelligence, and impending breach warnings. **Carpenter:** improves building and furniture options. **Guard / Warden:** helps defend against announced ward breaches, maintains traps or runic defenses, and can be assigned from the existing settlement workforce. Some roles are essential to the settlement fantasy, but no individual irreplaceable character should permanently lock the game.

**As built (October 31, 2026): the lost expedition.** The people in the Misty Woods are the expedition, found through rumours once a raid has been survived, each in their own way: Tor must be cut out, Eirik fought beside, Runa followed by marks seen only after dark, Liv freed from a ring of stones (loudly, or slowly), and Sigrid carried at the cost of a wound.

| Person | Job | What it does for the haven |
| --- | --- | --- |
| Tor | Carpenter | Mending buildings after a breach costs half; the sawbench works faster |
| Eirik | Warden | The ward holds half as long again in a breach |
| Runa | Tracker | One more rumour in every briefing |
| Liv | Rune Scholar | Teaches the raid runes, which nobody else knows; the cauldron works faster |
| Sigrid | Leader | 30 more seconds in every raid |
| Bjarne | Smith | Repairs to gear cost half; the forge works faster. Found second, after Ingrid, by his hammer in the deep woods |

**Rescue order (November 9, 2026).** Everyone is found in a designed order, each answering the problem the player has just met, and each rescue
teaches the building they work at: Ingrid, Bjarne, Tor, Eirik, Gunnar, Asa, Yrsa, Runa, Liv, Hild, Sigrid. Nobody arrives with a Haven rank. See
[Who comes home, and when](RescueOrder.md).

### 15.4 Personality and well-being

Start with lightweight differentiation: name, portrait, profession, preferred home, one or two personality traits, basic needs, and dialogue tied to recent events. Well-being can influence work productivity or available side stories, but avoid turning residents into a constant micromanagement chore.

### 15.5 Companions

Suggested launch prototype: **one active companion** per raid. Command options can begin with follow, hold, and context-sensitive interact/revive. A companion's utility must justify the genuine risk of losing them to the forest, and what it costs to get them back. Build readable pathfinding and safe-zone behavior before implementing complex companion combat tactics.

### 15.6 Consequences and memory

The village should respond to a rescued resident, a lost companion, and a major boss victory. Consider memorial objects, short dialogue changes, or vacant workstations. Consequences should create attachment without causing irreversible account-level dead ends.

## 16. Skills, levels, and bosses

### 16.1 Player power model

Separate four sources of progression to avoid making every improvement consume the same currency:

9.  **Skills and perks:** each activity (woodcutting, mining, melee, sneaking, and later crafting, cooking, archery...) is a skill that levels by doing it, with small bonuses every level and a choice of two perks every ten. They replaced character levels and Talent Points (November 7, 2026; see [Skills](Skills.md)). Perks do **not** cost Essence.

10. **Skill unlocks:** generally earned from bosses, quests, discoveries, and story milestones. An initial unlock should **not** also demand a large Essence payment.

11. **Skill enhancements:** optional, predictable upgrades purchased with Purified Essence, sometimes alongside rare materials.

12. **Gear and building upgrades:** crafted using ordinary resources and, at higher tiers, Purified Essence or specific boss materials.

Players should gain useful progress even after some failed Raids through levels and previously confirmed discoveries, without making extraction rewards irrelevant.

### 16.2 Purified and Cursed magic paths

**Purified magic** emphasizes protection, recovery, positioning, nature, and control. Example skills: **Forest's Embrace** (temporary protective barrier), **Ancient Roots** (short immobilization zone), and **Life Bloom** (gradual healing and stamina recovery).

**Cursed magic** emphasizes burst damage, drain, risky trades, and corruption. Example skills: **Cursed Strike** (powerful hit with a small health cost), **Soul Leech** (conditional life drain), and **Corruption Nova** (area burst followed by a temporary healing penalty). Every drawback must be explicit in the skill description and readable during combat.

These are **learnable disciplines, not exclusive classes**. The player can mix and match skills from both paths. The first prototype needs only a small number of representative skills, not a complete parallel tree per discipline.

### 16.3 Equipped abilities, skills and perks

Keep the existing prototype target of **three active combat skill slots**, with a clear role for mobility/defense and limited cooldown clutter. Players may know more skills than they equip and may adjust loadouts in Safe Haven. In the game these active skills are called **abilities**, so that "skills" means what the player gets better at by doing.

**Skills (implemented November 7, 2026):** Woodcutting, Mining, Foraging, Fishing, Weaponcrafting, Armorcrafting, Cooking, Alchemy, Construction, Melee (striking, blocking, parrying), Archery, Runecraft (spells) and Sneaking, levels 1 to 50, trained by doing. Crafted gear can come out Fine or Masterwork. Every level adds a little; the game's tuning is level 10, so a beginner is weaker than the game used to assume (slower and less precise with tools, softer and more tiring blows, louder and easier to see) and a master stronger. Every ten levels the player chooses one of two perks, which can be chosen again for free at home. Experience that isn't practice (quests, raids, discoveries) is **Insight**, put into any skill. Raid tiers count the skill expected at each tier. Numbers and reasons: [Skills](Skills.md).

### 16.4 Skill enhancement and cost rules

Boss victories unlock initial skills or upgrade permissions; Essence improves already unlocked skills. For example, a boss can award **Ancient Roots I**. **Ancient Roots II** might cost **10 Purified Essence** to extend its root duration, and a later upgrade might cost **25 Purified Essence plus a rare boss material** to add a meaningful new effect. These numbers are provisional. Upgrades are deterministic and never randomly fail or destroy accumulated Essence.

Use shared definitions for the unlock requirement, rank, cost, effect parameters, and save state. Keep upgrades optional where possible, so players can prioritize magic, weapons, or settlement improvements rather than being forced to buy every upgrade in a fixed order.

### 16.5 Bosses as progression gates

Each major boss guards a new skill, biome capability, or upgrade path. Bosses need narrative presence, recognizable tells, evolving arena patterns, and a unique reward. Boss victories and physical loot follow explicitly different persistence rules. A boss-specific token or quest milestone may be needed for unlocking a new tier, while ordinary Cursed Essence from combat supports upgrades within an unlocked tier.

### 16.6 Proposed boss ladder

**Early forest guardian:** introduces the first nature skill and basic magical enhancement. **Troll chieftain:** unlocks stronger equipment craftsmanship or defensive ward upgrades. **Marsh spirit:** introduces status control, antidotes, or a Cursed magic option. **Mountain ruler:** unlocks an advanced magical tier, specialized crafting, or a powerful hybrid skill. Preserve the existing boss roster as it develops; these descriptions are broad design targets rather than a fixed encounter list.

### 16.7 Optional later feature: Corruption meter

Consider a **Corruption meter** that rises from repeated use of Cursed abilities. At high levels, powers might strengthen while vulnerability increases; Purified abilities could reduce Corruption. **Defer this separate meter until the base combat/skill loop is proven.** Do not make Corruption a new mandatory currency or cause permanent character progression loss. Test whether simple health costs and debuffs create enough risk before adding another player-facing system.

## 17. Quests, trade, and world response

### 17.1 Quest system

Use concise, contextual objectives rather than a map cluttered with generic tasks. Main quests unlock the next major mechanic or story beat; side quests introduce folk tales, companions, unusual materials, and settlement improvements.

**Five kinds of quest (November 10, 2026; `Docs/QuestRedesign.md`).** Each has its own colour on the tracker, the maps and the givers' marks. **Main Story** (gold): the pact, the door and what took the expedition. One at a time, and something happens in every one: a fight, a find, a choice or a reveal, never only "gather" or "build". **Rescue** (teal): one per person of the lost expedition; each opens a building, a job and recipes. **Haven** (green): building and learning at home. Optional, several open at once, given when the player has just felt the problem. The job or building it sets up is its pay (decided November 11, 2026), so it needs no reward of its own; a reward, when there is one, is something the next raid needs. **Tale** (silver): a folk tale found out in the woods. **Errand** (bronze): a small ask or bounty that lapses without cost. Nothing but the story and rescues may stand before a Main Story or Rescue quest, so the portal never waits for a building.

Quest givers show their state with a small floating marker: "!" when a quest can be taken, "…" while the player is on it, and "?" when it is ready to hand in. Quests can optionally be handed back to their giver; this is off by default. When it is on, rewards arrive at the hand-in, not when the last objective completes. Once a giver's quest is done, the giver says an "after" line, or stops being interactable when it has none.

The story leaves room to breathe. A quest that starts by itself waits a few seconds after the last one, and a fight is never followed straight away by a new quest or a line: the player gets a moment to see they won. Nothing starts over someone talking. A moment that matters can ask for a longer pause of its own; the troll after the wolves at the camp comes eight seconds after the last wolf falls.

### 17.2 Quest types

Restore, discover, rescue, deliver, negotiate, escort, gather, investigate, and defeat. Each objective should have a clear condition, destination hint, reward, and save-state behavior. Events can spawn short raid-specific objectives without demanding a fully bespoke questline.

### 17.3 Barter and bargaining

Trade may rely on materials, favors, or cleverly negotiated bargains rather than a huge currency economy. Askeladden-inspired encounters allow dialogue, riddles, and unexpected item uses. Show the likely cost or risk of bargain options where fairness requires it.

### 17.4 Reactive world

NPC rumors can forecast events, cleansed shrines can affect future raid chances, and boss victories can alter routes or village dialogue. Prioritize reactivity visible within one or two play sessions rather than expensive branching content hidden from most players.

## 18. UI, UX, and accessibility

### 18.1 Information hierarchy

During combat prioritize player health, stamina, equipped skills, enemies, and danger signals. During raids add remaining time, extraction direction, objective, and current carrying load. In Safe Haven emphasize resources, build requirements, production status, and resident tasks. Avoid permanently displaying low-value data.

### 18.2 Safe Haven HUD

Compact health/survival indicators; hotbar; contextual interact prompts; expandable resource bars; access to crafting, build mode, resident overview, and portal planning. Quiet by default and rich on demand.

### 18.3 Raid HUD

Prominent but nonintrusive clock; threat or danger-phase alert; objective tracker; distance/direction to extraction; load weight and capacity; quick consumables; clear companion/downed status. Warnings must trigger early enough for informed choices.

**Text says when and which way; the maps say where (November 3, 2026).** At the top: the clock, one line with an arrow to the nearest open exit, and one line of news at a time when something changes. Every exit, rumour and open event is pinned to the minimap's rim and named on the world map, where direction can be read at a glance. The companion and what is at risk sit above the vitals, with long messages above them; pickups stay beside the character. The quest tracker shows only what can be done on this raid, and says how many wait at home.

The map marks have distinct carved shapes: a doorway for an exit, a broken ring for a rough rumour, a star for an open event, a diamond for a quest and a person for a companion. The world map names each place and writes quest rank and exit status in words, so colour is not the only distinction.

### 18.4 Example resource row

**Grey Stone** \[stone icon\] **43 / 100**, filled to 43%. A category heading groups rows such as Raw Resources, with expandable controls and optional sorting. Distinguish “per-resource cap” from “overall carry capacity” in both labels and visual presentation.

### 18.5 Accessibility requirements

Rebindable input, controller support, readable text scaling, color-independent signals, subtitle and audio cue options, clear interaction highlights, reduced screen-shake choices, and settings for timer readability. Consider combat difficulty assists that do not invalidate the extraction system.

**Added (November 3, 2026):** sound captions with the side a sound came from, rarity letters on item slots, and two assists that leave the extraction system alone (damage taken, parry and dodge timing). Helped runs are marked in the playtest log. Key prompts in text now follow a rebind.

**Implemented (October 30, 2026):** the Options screen, from the main menu and the Escape menu, has Gameplay, Graphics, Audio, Controls and Accessibility tabs. It covers rebindable keys for keyboard and gamepad, UI and text size, screen shake, hit stop and screen flashes, dialogue speed, a large raid timer, toggle sneak and auto-gather. Graphics default to the game as made; lowering quality is the player's choice. Still open: color-independent signals, audio cue options and difficulty assists.

### 18.7 Hearthline: the look of the UI

The main menu sets the style and the whole game follows it. The **hearthmark** is a little flame sheltered inside a waystone: it appears in the menu, home badge, map and loading screen. Its companion marks and the fine carved corners of windows are original, simple linework, inspired by the world's timber and stone rather than decorative runes. The HUD remains mostly type and thin lines held up by a soft shadow, so the art carries the screen. Windows are solid dark panels with a single thin rim, readable over bright grass and dark forest alike. Amber ink is warm at home; decorative ink and rims turn a muted, colder blue-green on raids. Action, warning and danger colours keep their meanings.

Gelasio sets titles and big numbers; Fira Sans sets everything read at a glance, chosen for lining numerals that stay clear at small sizes. The day and weather strip gives its smaller labels more room, and interaction words sit on a dark, almost opaque plate even against bright scenery. Colour is never the only signal: a word or a value goes with it. Health and stamina dim while full and nothing is happening, and come back the moment anything changes, or stay lit with Always Show Vitals.

### 18.8 Crafting windows

The bag and every station share one recipe browser: tabs by what recipes make, a **Can make** filter, a list of named rows, and the selected recipe beside it with what it does and each material against what is held. A row says in words whether it can be made (*Can make 3*) or all that is missing (*Need 2 Sticks, 3 Grey Stone*), so nothing has to be clicked to find out. A shortage is the everyday state of most of a list, so it stays quiet and what can be made stands out; only what gathering can't fix, like a full queue or a station's tier, takes a warning colour. The selected recipe says what the thing is for before its numbers, and how many the player already owns. The material list and the buttons stay in the same place from recipe to recipe, and a button that can't be pressed looks it. **Craft** or **Queue** makes one and a second button makes as many as possible; right-click or double-click on a row makes one. The bag also lists the known recipes made at stations, with their costs, to plan with.

At a station the queue and output stand beside the browser. The queue's heading says how full it is and how long it will take in all, at the station's real speed. The output slot holds one kind at a time, so it is drawn as one cell. When a finished craft has to wait, the window says what for: another kind to collect first, or a full slot; collecting then becomes the button to press. Below that the station says what tier it is, how many known recipes wait for a higher one, and who works there, and an **Upgrade** button leads to what the next tier costs and brings.

### 18.9 The Haven window

Pressing **H** at home, or asking the portal, answers "how is my home doing, and what now?" at a glance. The rank stands large with its line, beside four figures: renown, ward, cleared and buildings. Below it the ranks run as a ladder of stops, each an equal step apart, filled up to where Safe Haven stands, with the renown still needed and what the next rank brings in words. Three columns follow: **What next**, each step with the key that starts it and the renown it brings; **Renown**, largest first with a kind on one line; and the **Expedition**, everyone by name, home or still out there. A Build button goes straight to the build menu, as most of what the window suggests starts there.

### 18.6 First-use comprehension

Teach systems in the order encountered: movement → gathering → building → needs → Life Orbs → portal → timer → extraction → loss → NPCs. Explain loss rules explicitly **before the first raid**, not on the death screen.

## 19. Art direction, audio, and atmosphere

### 19.1 Visual identity

A stylized but tangible Nordic fairy-tale world: mossy pines, birch, timber cabins, weathered stones, rune-carved bridges, firelight, thick mist, mountains, and creatures grounded in local folklore. Avoid copying V Rising's visual assets, silhouettes, or UI layout; use its combat responsiveness only as high-level interaction inspiration.

### 19.2 Color and light

Volumetric fog carries the mood: thin morning mist at home, thick drifting mist in the Misty Woods, thickening and
darkening at night, lit by the sun or moon through the trees and glowing warm around fire. It lies over the ground like
a blanket, rolls in and out in banks, thickens with the weather and gathers in hollows and marshes. Whatever stands
between the camera and the player (a tree crown, a roof) dissolves in a soft, stippled circle, so the player is never
lost from view. Light at night is warm and
local (campfires, torches, a lit beacon); the dark around it is cold and grey.


Safe Haven uses warm amber, fresh greens, soft sunlight, and inviting interiors. Raid regions shift toward colder greens and blues, fog, hard shadows, and supernatural accents. Distinct biome palettes should aid navigation as well as atmosphere.

Rain leaves a mark: the ground soaks and darkens, puddles fill on level ground, and it all dries slowly after. Small birds peck about the Haven and crows the Misty Woods; they go up when the player comes too close, which depends on how loud the player is, so running clears them and sneaking gets near. They are only mood: nothing else reacts to them. Fireflies come out at dusk, green over the Haven and blue in the Misty Woods, so the two places feel different even at night, and they shelter from the rain.

### 19.3 Character and creature design

Humans should feel like ordinary people from a storybook world, not heroic armored demigods. Trolls need expressive silhouettes and a sense of immense weight. Huldras, forest spirits, and Nøkken can be beautiful, mysterious, or unsettling, sometimes within the same encounter.

### 19.4 Sound

At home: wind, birds, crackling fire, streams, hammering, quiet conversation. Outside: distant creaks, muffled birdsong, threatening footsteps, unusual echoes, and event-specific audio warnings. Combat hit sounds should communicate attack weight and successful timing.

Sound is heard from the player's position, not the camera's, and turns with the view, so what is on the left of the screen is heard on the left. A ward breach is heard wherever the player is: a bell when it is coming, a horn when they attack and with every wave.

### 19.5 Music as a system

Use gentle acoustic/folk-inspired motifs in the settlement, sparse atmospheric textures in exploration, and layered intensity in combat and extraction. The portal transition should produce a recognizable emotional shift. Original music may evoke Nordic instrumental traditions without simply imitating existing game scores.

## 20. Unity technical design

### 20.1 Architecture priorities

Develop modular, testable systems with a strong separation between content definitions, runtime state, UI presentation, and save representation. Optimize for a small single-player vertical slice before investing in speculative multiplayer architecture. Prefer straightforward Unity tooling and inspector workflows for designers.

### 20.2 Major runtime systems

| **System**                      | **Responsibility**                            | **State**                             |
|---------------------------------|-----------------------------------------------|---------------------------------------|
| PlayerController / CombatSystem | Movement, aim, attacks, dodge, damage         | Movement and combat state             |
| InventorySystem                 | Resources, carry limits, equipped items       | Counts, gear, cargo weight            |
| HarvestingSystem                | Nodes, tools, yields                          | Node state and loot results           |
| CraftingSystem                  | Recipes, workstations, refining               | Tasks and crafted output              |
| BuildingSystem                  | Placement, construction, validation           | Building instances                    |
| NPCSystem                       | Recruitment, roles, relationships, companions | Unique NPC IDs and jobs               |
| RaidDirector                    | Clock, seed, rooms, events, extraction        | Run configuration and temporary state |
| ProgressionSystem               | XP, skills, bosses, narrative flags           | Permanent unlocks                     |
| SaveSystem                      | Persistence and migration                     | Versioned save data                   |
| UI Presenter layer              | Convert game state to UI                      | View models and UI events             |

### 20.3 ScriptableObjects are definitions, not save files

Provide editable **ResourceDefinition, ResourceCategoryDefinition, LootTableDefinition, HarvestNodeDefinition, RecipeDefinition, ItemDefinition, WeaponDefinition, SkillDefinition, EnemyDefinition, BuildingDefinition, NPCArchetypeDefinition, RaidBiomeDefinition, and RandomEventDefinition** assets. These store static configuration. Actual state—43 units of stone, a named NPC's mood, the raid clock, and placed buildings—belongs in runtime models and serializable save objects. Never mutate shared definition assets to store per-save progress.

### 20.4 Editable resource example

ItemDefinition (material): id = stone, displayName = Grey Stone, category = Material, icon = grey_stone_icon, weight = 1.5, maximumStack = 50, tier = 1, value = 1. ResourceDefinition (essence): id = LifeOrb, displayName = Life Orb, category = Magical, color = green. Harvesting, recipes, loot tables, and saves reference the stable ID.

HarvestNodeDefinition: id = node.grey_stone_small, requiredTool = pickaxe_0, yield = 3–5 raw.grey_stone, harvestDuration = 1.5 s, respawnPolicy = SafeHavenSlow or RaidPerRun. A designer must be able to create another stone type in Unity without writing special-case scripts.

### 20.5 Raid generation and seeds

RaidDirector chooses a seed, assembles compatible room modules and points of interest, fills loot and events, and validates the route to the objective and extraction. Store the seed for debugging. The settlement is permanent state; the raid tracks a temporary instance state for its clock, loot, living enemies, companion positions, and explored sections.

### 20.6 Saving, crashes, and exploit prevention

Autosave in Safe Haven after major actions and upon returning from raids. At deployment, persist a transaction-like snapshot of committed equipment, companions, and the raid seed. A crash must not duplicate gear or corrupt the settlement. For MVP, interrupted raids may deliberately reset to the pre-raid snapshot, as long as the policy is explicit and tested. Before release, decide and test interrupted-raid recovery, save scumming, item duplication, and save version migration.

**New persistent fields:** include purification queues, permanent Essence balances, provisional Raid Essence, learned abilities/ranks, skill experience, chosen perks and Insight, defense placements and health, ward status, upcoming breach state, and resolved defense-event IDs. Extraction commits Essence exactly once, and breach rewards/consequences must also be idempotent. A new game or recovery save must not spawn duplicated defenders or resurrect destroyed defensive objects.

**As built (November 3, 2026):** the game saves as a raid sets out and when the player is home again. An interrupted raid resets to the save it set out from, and the player is told. Its haul is lost and nothing is doubled.

**Saved games (October 30, 2026):** a game can have any number of saves, each its own file, named by the player. A new game gets its own save the first time it saves, so it can never overwrite another. Saving, quick save and prologue checkpoints write to the game being played. The Saved Games screen loads, overwrites, renames and deletes, and Save as New copies the game in hand. Continue loads the newest. Saving and loading during a raid are both refused; quitting to the menu ends the raid, and what the player carried in it stays behind.

### 20.7 AI, navigation, and performance

Use readable finite-state behavior for enemies; companions require follow, hold, downed, and revive modes and must avoid blocking the player. Navigation must accommodate modular raid levels and transport width. Hand-dragged carts and wheelbarrows need attach/release interaction states, cargo transfer, and a weight-dependent movement modifier. Releasing transport for combat leaves its cargo in place; reattaching restores the appropriate movement penalty. Persist transport position, cargo contents, and attachment state. Pool frequent projectiles, effects, and ordinary drops. Prototype performance target: responsive 60 FPS combat on a defined reference PC, with exact minimum hardware specified after a representative biome is playable.

## 21. First 90 minutes: player journey

### 21.0 Prologue: the Unquiet Forest

**As built (November 10, 2026; `Docs/QuestRedesign.md`, step 2).** Seven quests where there were twelve. Where the text below
differs, this wins: the weapon is chosen as the party sets out, not at the tree line; three wolves come out of the trees after
the fleeing boar at the cart, in daylight; the shelter, the fuel and the fire are one quest (Shelter from the Storm) with no
briefings in between; the rest opens with Sigrid unable to remember Liv's name; the berries are Eirik's errand during the rest,
and the wolves wait for whoever took it; the camp is attacked in two waves of three; at the stones Sigrid's hand fails before
the player's, and reaching and waking them is one quest (Something Is Coming).

New Game begins in a green meadow west of the forest, on a sunny early morning, then walks into a dedicated, hand-authored
variant of Misty Woods, before the home progression below.
The expedition is investigating reports of a bewitched forest: trolls and witches are roaming beyond their old
territories, while normally predictable creatures have become aggressive and erratic. The cause remains unknown.

**The morning (before the numbered journey below).** The player wakes at the expedition's fire in the north-west of the meadow, with
armour but no weapon. Nothing starts until they speak to Sigrid; then she, Eirik and Runa say who they are. Until the player tells Sigrid they are ready there is time for optional
errands (bronze marks: gold is the main story, silver side quests), each from whoever would ask: Runa's hunt for blight boar with her spare bow, Eirik's firewood with an axe and his fishing in the
lake with a rod. The tools are the player's to keep; errands left unfinished lapse when the expedition sets out. Then the three walk the old
road east at a steady pace, waiting if the player falls behind and talking on the way. As they near the forest the day runs out, hours too
early, and they notice. They light torches and give the player one. Crossing the tree line the look turns to the raid's own, mist comes in with
rain, and the storm breaks when the wolves reach the camp. They stop just inside the trees, Sigrid has the player choose a Tier I weapon (sword,
axe, spear, club or bow) and sends them on to find shelter; wolves lie in wait on the road. The numbered journey below goes on from there. The
journey no longer starts with a fixed axe and torch, and the old road now runs west to east through the forest, with a path north to the stones.

The player travels with three of seven future recruits: Sigrid (expedition leader), Eirik (Soldier) and Runa (Tracker). Ingrid (Alchemist), Bjarne
(Smith), Tor (Carpenter) and Liv (Rune Scholar) went ahead two days earlier and have not come back, which is why the expedition goes in. Their
tipped handcart lies on the road. Names and dialogue are provisional authored content. Ingrid reuses the existing recruit identity; the other professions' full settlement effects remain future work.

1. A complete set of Woven Hide armor from the start; the torch comes on the road and the chosen weapon at the tree line. The companions carry torches too.
   The torch is a normal craftable weapon (two Sticks, by hand), with a brighter flickering light. An occupied off hand
   rejects two-handed weapons; the player must put the torch away first.
   Speak with Sigrid and accept her briefing before the expedition follows or the shelter quest begins.
2. Find a dry rock shelter; "Return to Sigrid" remains part of that quest until the next briefing is accepted.
   Speak with Sigrid to receive the gathering task: four Sticks and six Small Rocks.
   Return for her building instructions and use the normal build menu (G) to place a campfire beside the shelter.
   Construction pays this cost once; there is no prebuilt fire or additional fuel payment.
3. After cooking, return to Sigrid to rest by the fire. There is no countdown: talk to the others, and the wolves cut the conversation short
   (or come after 45 seconds if the player talks to nobody). Companions remember the errands and why the player came.
   Conversations bring the camera closer and frame the player and speaker; closing or leaving restores the view.
   Companions stow their weapons for peaceful conversations and restore their previous state afterward.
4. Defend the camp against two wolves, as the storm breaks. (On the road in, rootlings ambush the party, and Eirik offers a short optional lesson at the tree line: a dummy, a dodge, a parry or a dodge through a blow, and a rootling.) The encounter counts regardless of who lands the killing blow.
5. A rumbling, approaching presence forces the group onward to ancient waystones. The giant's gait is slow and heavy;
   its walking footfalls produce subtle, distance-attenuated camera shake.
6. Return to Sigrid at the waystones, then touch them to start a visible connection countdown (one minute in the current build),
   using the shared raid/quest countdown HUD. Music starts here,
   not during the opening or camp defence; wind/rain ambience remains throughout. Increasing enemy waves pressure the group;
   the soldier offers limited nearby support and the Alchemist tends the player's wounds.
7. The old troll walks out of the trees towards the stones in the last twenty seconds. Reaching zero opens the connection as its blow lands and separates the expedition. Falling during this final defence
   gives an abbreviated version of the same narrative transition, not an ordinary raid failure.
8. Wake alive at the portal, without a raid report or durability penalty. What the player wears comes through with them
   (armour, the chosen weapon, belt tools, backpack, lantern, and their arrows), by the rule of a failed raid; what they carried
   does not. Listen to the portal, then light a fire and wake the door (§21.1 to §21.3).

Before the final defence, falling returns the player to the current phase's checkpoint with health restored.
Safe phases save automatically and allow manual saving; combat phases do not. Loading restarts the current phase
and its transient actors/timers, not a partially completed wave. The placed campfire keeps its location and state across saves.
Arrival at home is saved once.

The forest scene has no ordinary raid director or random raid generation. Quests and stable story events own
progression; authored chapter behaviours control scene reactions, timing and encounters. Dialogue, chapter quests,
entry/status text, acceptance choices, timed-phase duration, initial enemy counts, building costs, wave interval, enemy cap,
starting armor, dialogue camera distance and companion support settings are designer-editable. Sigrid uses the same
Quest Giver component as other NPCs: ordered offers own briefing text, acceptance choices and waiting lines, and
confirmation starts the quest through the normal Quest Manager. There are no prologue-specific giver rules.
The shared quest system keeps return objectives active until the next briefing is accepted, supports pre-acceptance
tracker guidance, and lets intermediate chain stages suppress announcements without suppressing lifecycle events.
This chain plays the new-quest sound at its initial acceptance and the completion sound only at its conclusion.
Peaceful next objectives
wait for the leader's briefing; attacks, the escape alarm and countdown remain automatic.
The quest/event designer exposes scene-owned reactions for music, weather transitions, object activation and parameter
changes. Quests can raise named events on start and completion. These reactions are authored in the Content Editor's
Scene Reactions panel and saved with the scene; stateful reactions can replay from quest/event history on load.
The field fire does not replace the permanent hearth and
Construction Table needed for home crafting. Times in §21.1–21.5 describe the home/raid journey after this prologue.

### 21.1 Minutes 0–10: awakening

**As built (November 10, 2026): the door opens after three short steps, and the first raid comes before the first workshop.** The times in the headings of §21.1 to §21.5 are the old plan's; the new targets, counted from New Game, are in `Docs/QuestRedesign.md` (awake at about 13 minutes, in the first raid at about 22).

The player wakes at the portal after losing contact with the expedition, in the gear they wore. A sunny clearing contains a stream, loose Small Rocks, fallen Sticks, and Berries, all gathered by hand at Tier 0. A short conversation with the portal gives the reason to build instead of a long opening cutscene: it caught only one, the other seven are alive, the forest is forgetting them, and a tended hearth is what they can see (§4.1). The main story then asks for one thing only: a fire. Never require wood or Grey Stone to make the tool that first unlocks it.

### 21.2 Minutes 10–25: first camp

Light a campfire from the Sticks and Small Rocks lying around the portal (A Light in the Wood). The first fire is the first reward: the portal lights up, the ward wakes, and far out in the mist someone has turned to look. Three nearby glowing stones are bound by enchanted roots; freeing them releases the first Life Orbs (The Rootbound Stones). Tools (a hatchet and a pick, by hand) and food and water are Haven quests that open with the fire: they help, and the portal doesn't wait for them.

### 21.3 Minutes 25–40: awakening the portal

Repair and activate the portal with the three Life Orbs (The Door Wakes): the repair asks for nothing else. Sigrid's voice comes through the stones: Ingrid is hurt and close, find her first. The Workshop, the chest and a second weapon are Haven quests from here on, beside Into the Misty Woods and Bring Ingrid Home. Introduce the Misty Woods destination and a clear briefing: “Carried inventory is lost on death. Your deployed equipment returns damaged and can be repaired at home. Your home remains safe.” The first raid is comparatively controlled and contains no major boss.

### 21.4 Minutes 40–60: first raid

Meet one Forest-Bitten Wolf and a Root Guardian, gather hardwood and ore, and discover a clearly signposted extraction point. A side path leads to Ingrid, marked by the quest; she saw the fire, which proves the plan works. Finding her isn't needed to finish the raid: if she falls, she waits there for the next one. A visible clock teaches the decision to return. The first extraction should be a memorable success.

### 21.5 Minutes 60–90: a growing home

Recovered loot unlocks the sawbench, storage, or early forge progress, each built for someone: Tor's bench, Bjarne's furnace. Ingrid appears at home as the first NPC. The light that reached her reached other things too, which is why the first ward breach comes now. Raising the haven to a Hamlet carries the light further, and answers it: a hammer on stone far to the north, past the troll country, is Bjarne, and the road to Troll Valley is the next goal. A new blueprint reveals missing material from the next destination. End the introduction with two meaningful options: expand the home or attempt a harder expedition.

## 22. Worked raid example and balance targets

### 22.1 “The Cart by the Crag”

**Preparation:** The player equips an axe, bow, leather vest, two soups, and one healing potion, plus scout **Runa**. The default raid duration is 12 minutes; spending one Portal Spark increases it to 15. The mission objective is iron ore; Runa has heard rumors of an abandoned cart.

**Minutes 0–4: Take a safer route, defeat wolves, and gather ore. The resource bar shows 46/100 Iron Ore while the total load approaches 100/150. Minutes 4–8: Runa points out a cart. Freeing it requires time and four Oak Planks brought from home, rewarding advance preparation. Minutes 8–11: The player deposits ore and looted items into the hand-dragged cart. Its growing cargo weight slows movement, and a young troll near the shortcut prompts a slower detour. Minutes 11–14: An ambush downs Runa. The player releases the cart to fight, revives her, unloads some ore to lighten the cargo, reattaches to the cart, pulls it toward the portal, and extracts with 35 seconds remaining.**

**Debrief:** Iron, a rare mushroom, preserved cargo, repair costs, and Runa's exhaustion are shown. The new ore allows construction of a furnace. Alternate choices could have recovered more ore, lost the cart, or cost Runa's life.

### 22.2 Initial prototype values

| **Parameter**          | **Proposed starting value**                                                     | **Playtest purpose**                                            |
|------------------------|---------------------------------------------------------------------------------|-----------------------------------------------------------------|
| Standard raid time     | 12 minutes                                                                      | Pressure without constant rushing                               |
| Time extension         | +3 min per Portal Spark, max two                                                | Meaningful preparation cost                                     |
| Final danger phase     | 60 seconds                                                                      | Dramatic last chance, but fair                                  |
| Material cap           | 100 units per resource                                                          | Readable resource bars                                          |
| Personal carry budget  | 150 weight points                                                               | Force cargo prioritization                                      |
| Cart bonus             | +200 weight points                                                              | High cargo capacity balanced by weight-dependent movement speed |
| Active NPC companions  | 1                                                                               | Manageable controls and AI scope                                |
| Quick consumable slots | 4                                                                               | Preparation without overloaded UI                               |
| Active combat skills   | 3                                                                               | Clear, readable skill-based combat                              |
| Purification ratio     | 2 Cursed : 1 Purified                                                           | Transparent magical resource conversion                         |
| Ward-breach cadence    | First scripted; later roughly every 3–5 successful raids or equivalent progress | Rare and controlled, not a surprise timer                       |
| Base defense downtime  | No attacks during raids, offline, or loading                                    | Preserve the sense of refuge                                    |

### 22.3 Balance questions

Do players voluntarily return before the timer expires? Are equipment damage and home repair costs so punishing that nobody uses interesting items? Do hunger and thirst create strategy or annoyance? Are Essence rewards sufficient for equipment, skills, and defenses without grinding? Do warned breaches make the base feel worth protecting without turning home play into constant anxiety? Does rescuing an NPC feel more meaningful than gaining production percentages? Is a cart both exciting and stressful to extract? Can a player recover after several failed raids without tedious grinding?

## 23. Minimum viable product (MVP)

### 23.1 MVP hypothesis

The first playable product must prove the unique loop: **peaceful home → preparation → timed dangerous raid → extraction risk → persistent home improvement**. After that loop is playable, the vertical slice should prove that **an occasional warned defense event** adds meaning to a growing settlement rather than replacing the calm-home experience. If this loop is not fun with limited content, adding a full world will not fix it.

### 23.2 MVP scope

| **Area**     | **Include in MVP**                                                                            | **Defer**                                                    |
|--------------|-----------------------------------------------------------------------------------------------|--------------------------------------------------------------|
| World        | One ward-protected clearing, one varied raid biome                                            | Complete world map                                           |
| Combat       | One character, axe and bow, dodge, three enemy types                                          | Many weapon types and complex combos                         |
| Raids        | Timer, extraction, loot, one event, death/loss                                                | Extensive modifiers/event chains                             |
| Buildings    | Campfire, table, shed, sawbench, furnace, house, basic Purification Altar                     | Large decoration catalog                                     |
| Base defense | One scripted, warned breach with one simple barricade or trap in the vertical slice           | Recurring assaults, complex sieges, automated tower networks |
| Resources    | Approximately 10–15 materials and bars                                                        | Hundreds of materials                                        |
| Crafting     | Basic tools, one refining chain, one potion                                                   | Vast recipe trees                                            |
| NPCs         | One rescuable resident with one real job                                                      | Full relationship simulation                                 |
| Progression  | Portal unlock, one Purified Essence improvement, one boss-earned skill, minimal talent choice | Comprehensive skill trees, Corruption meter                  |
| Boss         | One miniboss or introductory boss                                                             | Late-game boss ladder                                        |
| Saving       | Save/load base, materials, unlocks, raid outcomes                                             | Cloud sync and multiplayer                                   |

### 23.3 Vertical slice

After the MVP, create a polished **20–40-minute** representative experience with one NPC rescue, cart mechanic, boss, final art direction, audio, UI, debrief, and robust save/load. The slice communicates the finished game's intended quality, not its complete content quantity.

### 23.4 Acceptance criteria

- Gathering updates the correct resource bar, cap, and carry load precisely.

- The player can construct campfire, construction table, and portal through regular in-game UI, without Editor commands.

- A raid can start, extract successfully, or fail; each outcome produces the correct persistent state.

- A time extension costs the configured amount **exactly once** when committed.

- Successfully extracted loot reaches the correct inventory/storage; death removes carried inventory while retaining deployed equipment with durability damage that can be repaired at home.

- A discovered NPC can be rescued, seen in the settlement, and assigned a job; companion loss does not corrupt a save.

- The player can recover after failure using freely attainable baseline resources in Safe Haven.

- Extracted Cursed Essence can be purified and spent on a deterministic upgrade without double-spending or losing permanent state.

- In the vertical slice, a warned base breach can be started after preparation, defended with a basic structure, and resolved with repairable persistent consequences; no base attacks occur while absent.

- All primary systems are playable with mouse/keyboard, with controller integration considered in the input layer.

## 24. Development sequence and milestones

### 24.1 Milestone 0: movement and combat feel

Build a responsive player controller, mouse aiming, basic attack, dodge, one enemy with readable attacks, health, and clean damage feedback. Do not build elaborate crafting before the moment-to-moment action is fun.

### 24.2 Milestone 1: resources and safe settlement

Add harvestable stone and wood, ScriptableObject resource definitions, resource bars, carry limits, campfire, construction table, and persistent building placement. The ward is initially stable; defense events are not a tutorial blocker. Verify that designers can add a resource in the Inspector without code changes.

### 24.3 Milestone 2: first complete raid

Implement portal deployment, raid timer, one modular map, loot, extraction, death/loss rules, debrief, and dependable save transitions. This is the first genuine test of the game's distinctive loop.

### 24.4 Milestone 3: meaningful return

Add refining, the Purification Altar, basic Cursed/Purified Essence flow, one boss-earned skill enhancement, more structures, better equipment, food preparation, and a clear reason to plan another raid. Measure whether successful return materially changes what the player can do.

### 24.5 Milestone 4: NPC and transport

Add one rescuable NPC with employment, companion down/revive states, and a working cargo transport prototype supporting a hand-dragged cart or wheelbarrow. Verify item/resource transfers, weight-based movement speed, releasing transport for combat and reattaching, navigation, extraction radius, save behavior, and emotional consequences.

### 24.6 Milestone 5: polished representative experience

Add a boss, an environmental event, one tightly scripted and warned ward-breach encounter with a buildable defense, narrative framing, art direction, audio, UX polish, tutorials, accessibility basics, and performance verification. Deliver the 20–40-minute vertical slice.

## 25. Design risks and trade-offs

**Risk: raids feel relentlessly rushed. Improve route readability, optional activities, and timer tuning rather than simply granting unlimited time. Risk: loss discourages experimentation. Retain deployed equipment, keep home repair costs modest, and communicate inventory loss clearly. Risk: settlement play feels like a menu. Add visible NPC routines, building effects, and attractive upgrades. Risk: survival becomes repetitive. Keep needs gradual and make prepared food interesting. Risk: randomness becomes unfair. Use event budgets, seeded diagnostics, and reachability validation. Risk: cart and companion AI consume too much scope. Prototype the smallest navigation-rich example early. Risk: feature creep. Do not introduce co-op, dozens of biomes, extensive relationship systems, or a full siege game before a complete extraction loop works. Risk: Essence becomes a bottleneck for every activity. Grant talent points through leveling and new skills through bosses; reserve Essence mainly for selected enhancements and tiers. Risk: ward breaches destroy the sanctuary fantasy. Keep encounters rare, announced, progression-gated, repairable, and player-present only.**

### 25.1 Playtest metrics

Track raid completion rate, average duration, extraction timing, death and loss frequency, cargo returned, optional-event participation, NPC survival, average Essence recovered and purified, number of raids needed per skill/building/gear upgrade, ward-breach frequency, defense repair costs, frequency of survival consumable use, and time spent at home. Pair analytics with qualitative questions about tension, attachment, fairness, and recovery.

## 26. Open design decisions

### 26.1 Decided for the first-90-minutes prototype (v0.3)

These decisions cover §21 and the first ward breach. All numbers are tuning defaults that can be changed in Unity's Systems Editor (Safe Haven > Systems Editor) without code.

- **Scope:** §21, plus the first scripted ward breach, Cursed and Purified Essence through the Alchemist, and the first ward upgrade. Bow, bosses, and talents come later.
- **Death and time-out:** the player revives at the portal. Carried materials, consumables, and raid loot are lost. Deployed equipment (tools, weapons, and backpacks carried through the portal) is kept and loses 25% durability per failed raid. At 0% it is broken: kept, but unusable until repaired. Durability is lost only this way, never through use. Running out of time counts as a failed raid.
- **Repairs:** done at the Construction Table. A repair costs 50% of the item's recipe × the share of durability missing, with at least 1 of each material.
- **Safe pocket:** one protected slot that is never lost (§8.3).
- **Raid timer:** 12 minutes. A Portal Spark adds 3 minutes; at most 2 per raid, paid once at departure. Sparks are crafted at the portal from Life Orbs.
- **Hunger and thirst:** they always drain, slowly at home and 2.5× faster in raids, and they are never lethal. Empty hunger lowers maximum stamina and slows its regeneration; empty thirst slows stamina regeneration in combat. Sources: berries, stewed berries (campfire), the stream, and waterskins.
- **Resources and carrying:** materials are resource bars in four categories (Raw, Refined, Organic, Magical). Each resource holds up to 100, within a 150 weight budget. At home, crafting, building, and repairs also draw from storage.
- **Building:** complete structures are placed freely, with a preview, inside the portal's ward radius. Building is instant, and structures can be moved for free.
- **Tools and weapons:** weapons are crafted, equippable items (the Stone Axe first). The tool ladder is Stone → Copper → Iron, each gathering one tier further. Hardwood and iron ore need only stone tools. Copper and iron gear are made at the Forge.
- **First recruit:** Ingrid, stranded on a side path in the Misty Woods. Enemies can attack her; if she falls, she stays behind for that raid. If she is extracted with the player, she becomes a resident. Her first job, Alchemist, teaches the Purification Altar: 2 Cursed Essence become 1 Purified Essence, in a queue that keeps working while she holds the job.
- **First Essence upgrade:** strengthening the portal's ward grows the buildable area.
- **First ward breach:** offered after the first successful extraction, with a warning; the player starts it at the portal. A few weak Forest-Bitten Wolves come along one marked path and attack only the player; buildings can't be damaged. The Palisade is the first defense. Winning gives 2 Life Orbs. If the player falls, they revive at the portal and can try again.
- **Next destination:** Troll Valley is shown but locked. A Forge Blueprint is found in the Misty Woods; building the Forge needs Trollstone.
- **Legacy prototype content** (extra ores, woods, fibers, the loom, valuables, sigils, the human enemy) is kept and renamed to fit the theme.

**Prototype assumptions made during implementation (revisit in playtests):**
- Blueprints are learned by reading them (right-click), which uses them up.
- Purification continues during raids.
- Storage counts toward costs even before a Storage Shed is built.
- A new game starts with hunger at 70% and thirst at 60%.
- At home, hunger drains 2.5 points per minute and thirst 3.5.
- A purification batch takes 20 seconds.
- Repairing the ruined portal costs 3 Life Orbs.
- A Portal Spark costs 2 Life Orbs.

**Decided October 30, 2026 (all numbers are tuning defaults):**
- **Raid events:** the clock keeps running while the player chooses, and an event happens at most once a raid.
- **Meals:** two meal slots; tonics take none. Bonuses survive the portal, not a failed raid, and aren't saved to file.
- **Status effects:** a blocked hit leaves none, and they never count as blows.
- **Farming:** crops are station recipes, so they grow during raids. Seeds come from raids.
- **Copper gear** is made at the Construction Table, so it comes before the Forge.
- **Graphics defaults** stay at the game as made. Trade-offs are the player's, in Options.
- **Saved games:** unlimited, one per game, named by the player. A save stays when a new game starts.

### 26.2 Still open

- How long should a raid be? The design says 12 minutes; Misty Woods now runs at 16:40 (1000 s), with its danger stages and exits written for 12, and a large raid (four times the ground, chosen in the planner) at 29:10. No 12-minute raid has been played yet, so its pacing with events is untested.

- How strong should the new gear, creatures and meals be? Their numbers are first estimates and none have been playtested.

- Should any lost inventory be recoverable through a corpse or rescue raid?

- Is the extraction point fixed, selectable, unlocked, or variable by biome?

- Does the final danger phase permit extraction, or only warn of an unavoidable failure boundary?

- What capacity and weight-to-speed curve should a horse cart use?

- How many NPCs may join raids in later tiers, and can an NPC be permanently captured?

- Should portal time extensions have biome-specific limits or scaling prices?

- Which boss achievements are granted on defeat versus successful extraction?

- Later, should players place individual architectural pieces, or only complete structures?

- What is the replayability target? Maps are generated from rules (§9.1); a hand-authored module system is not built.

- Will the narrative have authored alternate endings, or a largely linear resolution with local choices?

- How often should later ward breaches become available?

- Which structures can enemies damage or temporarily disable in later breaches, and what is the repair/recovery budget?

- Can any NPC die permanently during a settlement defense event, or are injuries the highest allowed consequence?

- Beyond the Palisade, should later defenses be traditional barricades, magical totems, or a combination?

- Is the Corruption meter a meaningful later addition, or are skill-specific costs enough?

- What fraction of Essence do players actually allocate to skills, equipment, and structures during a typical progression window?

- Which secondary systems must be removed if MVP development becomes slower than expected?

Resolve these through targeted prototypes and documented decisions, not untested assumptions.

## 27. Glossary and content rules

**Safe Haven:** the largely peaceful, ward-protected, permanently evolving settlement that can face controlled breaches. **Ward Breach:** a progression-gated, announced settlement-defense encounter while the player is present. **Purification Altar:** workstation that converts extracted Cursed Essence to Purified Essence. **Cursed Essence:** high-value, normally unextracted magical raid loot obtained from enemies and raid successes. **Purified Essence:** stable magical upgrade material processed in Safe Haven. **Portal:** ancient protector of the clearing. **Raid:** a time-limited portal expedition to unsafe territory. **Extraction:** successfully returning through an authorized exit. **Life Orb:** magical resource associated with the portal and forest magic. **Portal Spark:** crafted at the portal from Life Orbs; each adds 3 minutes to a raid (at most 2, paid at departure). **Safe Pocket:** a small protected inventory slot whose contents are never lost. **Resource Bar:** the non-grid material inventory display. **Carry Budget:** weight-based limit in addition to per-resource capacities. **Companion:** an NPC deployed into a dangerous raid. **RaidDirector:** system assembling and supervising individual raids. **Vertical Slice:** short, polished example that reflects final experience quality.

### 27.1 Criteria for new ideas

A new feature belongs in Safe Haven when it strengthens at least one pillar: the comforting home, risky fairy-tale raids, meaningful consequences, a growing community, or approachable systems with emerging depth. Reject additions that turn settlement life into perpetual combat, make extraction arbitrary, or inflate scope without reinforcing the central loop. A controlled breach belongs when it supports the feeling of protecting a beloved home, not because a survival game is expected to have endless waves.

## 28. Design summary

**Safe Haven is a Nordic fairy-tale survival action game built around an unusual emotional contrast: peace and belonging at home, meaningful danger beyond the portal, and rare moments when the darkness threatens that home.** Players gather, build, farm, recruit companions, purify dangerous magic, choose talents and skill builds, and become stronger in a lasting settlement. Timed raids offer meaningful extraction risk and deliver the materials, Essence, people, and knowledge needed to progress. Periodic, controlled ward breaches give defensive buildings and residents a purpose without replacing the calm of ordinary settlement life. Every return should make Safe Haven feel more alive—and more worth protecting.
