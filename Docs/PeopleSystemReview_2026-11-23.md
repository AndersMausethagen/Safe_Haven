# The people system: what to improve, and what the rest of the game must change to make room (review, 2026-11-23)

The agent wrote this as read-only review after building Docs/PeopleResearch_2026-11-23.md (commits 9b728b96 through c2e92646, unplayed by
hand). It answers the user's note of 2026-11-23: "do a review on how we can improve this system further and what we need to change in
order to accommodate this even better in the existing systems". Nothing here is built.

**In short.** The people system is complete as data and rules, but the rest of the game hardly asks for it yet. You can't put a person
where their talents are, choose a companion by them, see a raid's effect on them, or spend anything on them except kit. And most of who
they are is only seen in their window. The three changes that matter most are:

1. Let anyone take any job whose workplace stands (§2.1), so talents and passions decide who does what.
2. Show people where decisions are made: the raid planner, the homecoming, the Keeper's view and the Lands tab (§2.2-2.4).
3. Give the bond with the player more ways to grow than raids along (§1.1), so residents who stay home matter too.

Questions for the user are in §4.

---

## 1. The system itself

### 1.1 The bond with you only grows on raids (most important)
`RaidsAlong` is the only bond. It drives bond ranks, passion picks, memories and the Trusted and Sworn bonuses. A cook, mason or farmer
who never raids never grows closer to the player and never gets a pick. RimWorld's colonists bond through living together; Stardew's
through gifts and talk; Fire Emblem's through time side by side.

**Suggestion: a single "bond" with the player, fed by several things**, with raids along still counting most:
- a lesson taken;
- an ask answered (Haven requests);
- a wish met;
- kit given;
- a hard mark healed;
- a gift they love (§1.3);
- being helped up in a breach;
- their friend brought home from the forest.

Bond ranks then count bond, not raids. Saves would convert `RaidsAlong` into bond once.

### 1.2 Who they are is mostly unseen in play
Traits show as a line in passing, a number and a card on hover. RimWorld's strength is that personality shows in *behaviour*. Our
`ResidentLife` already chooses free-time activities (the fire, outings, scenes), so traits could choose some of them.

**Suggestion: habits as data on a trait** (a place kind, the hours, an emote):
- Loner eats at the edge of the Haven.
- Bookish reads at the stones.
- Night Eyes walks the lit lanes after dark.
- Proud Cook tastes at the cauldron.
- Light Sleeper wakes and walks the fence at night.

Two more behaviours outside traits:
- Friends sit together at meals.
- Rivals take the other side of the fire.

Cheap: no new rules, only where they go and what they do there.

### 1.3 Spirits have few sources you control
Today's lifts are a good breakfast, a content home, a passion job, a scene's cheer, a kind word, a friend near work and an inspired day.
Only a breakfast and a home are things the player does on purpose. Inspirations need Bright (2 steps), and nobody has measured how
often anyone gets there.

**Suggestions:**
- **Gifts:** two loved items per person (data). Given in talk, a gift lifts their spirits for a few days and feeds the bond (§1.1).
  This is the gift Hammer-Gruff's trait was written for.
- **Festivals lift everyone:** `Festivals` exist but don't touch spirits.
- **A favourite dish per person:** breakfast on it counts as a second lift (Proud Cook already doubles the meals' work bonus).
- **Their kit named:** a small, lasting lift.
- **Measure first:** a `Spirits` playtest row each dawn (§2.9), then tune Bright At and Inspiration Chance against real play.

### 1.4 Rivals matter too little, friends a little more
- **Rivals** only cost spirits when they live next door, and argue at the fire. There's no reason to keep them apart at work or on a
  foray.
- **Suggested, symmetric with friends:** a rival near at work −5%, and rivals on one foray −10%. Show both on the foray card, so it is a
  choice, not a hidden cost.
- **Friends:** add a mentor rule. A Friend of a higher level in the same skill makes their friend learn it a little faster (the
  "masters teach apprentices" story).

### 1.5 Marks come from one place
All 9 marks come from raids along. Breaches (everyone fights now), forays gone wrong and corruption nights leave nothing, and healing is
either an item or a friend. Suggestions:
- **More causes:**
  - Defended the Ward (good, after a breach won);
  - Lost in the Mire (hard, a failed foray to the marsh);
  - Night Terrors (hard, from a corruption night).
- **More ways back:** tie them to jobs and buildings.
  - The Alchemist tends a wound for a draught's ingredients.
  - The sauna or a festival eases nightmares.
  - A Friend's scene already works.
- **A memorial:** losing someone already darkens their house (`EmptyHouses`). A carved stone for the lost with their marks and named kit
  would answer GDD §15.6 ("memorial objects").

### 1.6 Kit is two pieces apart from the rest
- **What's there now:** worker gear (one item for carrying and speed) and companion kit (weapon and chest) are separate.
- **Unify the panel:** "Their kit" could show three slots: a worker tool, a weapon and armour. The worker tool would keep its old effect.
- **A trinket slot:** an amulet or ring with its enchantment would let Runecraft and enchanting reach people too.

### 1.7 New people (the user's answer 4: later)
With 11 written people, talents and traits can't make roster choices yet: everyone has a fixed place. The planned rolled strangers are
what turns RimWorld's "who do I take?" on. When they come, the generator should:
- write two backstory fragments;
- roll talents around them (offsets −5 to +9, as RimWorld does);
- give one good and one double-edged trait from the general list;
- give one passion.

Rescues and the Notice Board are the natural doors.

### 1.8 Balance is unmeasured
None of these numbers has been checked against play:
- the day-one boost (stations 15-24% faster);
- crafts to the next rank;
- the daily cap;
- how often anyone is Bright;
- how fast fighting skill grows on raids.

The player's own skills have `SkillLevel` and `SkillXp` rows and a Systems Editor page; people have neither (§2.9).

---

## 2. The rest of the game: what to change so it makes room

### 2.1 Jobs: anyone, any job (the big one)
`SettlementState.JobsOpenTo` gives only a person's written jobs plus vacant ones. So Tor's Burning Fishing can never become his work,
and talents can't decide who does what. The research doc's best case (§3.2, "keep them where they are good, or move them where they are
happy") can't happen.

**Change:** any person may take any job whose workplace may be learned (`BuildingGate`, unchanged), and a job may be held by more than
one where there is more than one workplace (two docks already work). The written jobs stay "their trade": first in the list, and their
quest lines unchanged. The job list shows each job with their level and hearts, best first. The rescue order still gates when each
building is learned, so the story's pacing holds.

### 2.2 The raid planner: choose a companion by who they are
- **Today:** the planner shows the companion's name, bond and bleed-out time.
- **Add:**
  - their fighting skill and level;
  - their fighting traits (Brave, Wary, Steadfast);
  - marks (Troll-wary on a troll raid is a real reason to bring them);
  - kit and its wear;
  - spirits;
  - a Fighting Heart if they have one;
  - for a raid with a known foe kind, the marks that matter against it, lit.

### 2.3 The homecoming: show what the raid did to them
- **Today:** the report says "made it home, and is Trusted now". The rest (levels gained, a mark, kit wear, a pick owed) comes as loose
  messages.
- **Change:** a companion card on the results screen, made of `KitPictureCard`s as the lessons are:
  - their fighting skill's gain;
  - a new mark;
  - their kit's wear;
  - "a passion to grow: talk to them".

### 2.4 The Keeper's view and the Haven window: a work grid
- **Today:** people are a column of portraits with a heart and a sun or drop.
- **Add a "Work" page:** people down the side, the Haven's jobs across, each cell their level in that job's skill with hearts. Click a
  cell to assign. It is one screen for "who should do what", as RimWorld's Work tab is.
- **The Lands tab:** foray parties are picked automatically today. Let the player choose who goes, showing:
  - each one's level in the land's skill;
  - friends and rivals in the party;
  - who is inspired with a Good Find.

### 2.5 Quests and asks: let stories use people
- **New objective kinds** (Docs/QuestRedesign kinds):
  - a resident reaches a rank;
  - two become Friends;
  - heal a mark;
  - give someone kit.
- **New rewards:**
  - a passion;
  - a mark (Giant-felled after a story boss);
  - skill experience for a resident.
- **Haven asks from who they are:**
  - Old Wound asks for a salve.
  - Proud Cook asks for a rare spice.
  - Bookish asks for an inscription.

### 2.6 Homes, meals, festivals
- **Homes:** each person's home liking (`HomeLifeSettings` › Likings) lives apart from their traits. Move it onto the person (their
  definition) with their other facts, so one place says who they are. The "Move them" list could warn "next door to a rival".
- **Meals:** favourite dish (§1.3).
- **Festivals:** lift spirits, and friends' scenes at festivals cheer the whole gathering.

### 2.7 Breaches and the ward
- **Today:** everyone fights, and kit, traits and fighting skill now count. But a defender's fall isn't recorded.
- **Add:**
  - defenders downed and helped up, which grows the standing with whoever helped (the research doc's "helping up a friend");
  - breach marks (§1.5);
  - the guard post picker showing Melee and Archery levels.

### 2.8 Horses
Residents have Horsemanship, but nothing uses it. A stable hand job (Horsemanship) could feed, groom and train horses. Riders on forays
(a horse and a rider with Horsemanship) could make trips quicker. Both fit the horse systems already built.

### 2.9 Tools for tuning and testing
- **Playtest rows:**
  - `Inspired` (who, kind);
  - `Mark` (who, gained or healed);
  - `Standing` (pair, level change);
  - `PassionPick`;
  - `PersonRank` (who, skill, rank);
  - a dawn `Spirits` row (counts of Low, Steady and Bright).
- **The F1 menu:** inspire someone, give or heal a mark, set a standing, raise a skill. The playtest must reach these states quickly.
- **The Systems Editor:** a People page, as the Skills page does for the player: crafts to each rank by job, Bright share, inspirations
  a week.
- **The Content Editor:** sections for Traits and Marks (today they are only in the Project window).

### 2.10 Messages and voice
- **Messages:** rank-ups, inspirations, marks, arguments and missed friends each post a message, so a busy morning can flood the screen.
  Route people news into the homecoming digest (`Homecoming`) and the Keeper's alerts. Only the moment the player sees (an argument
  overheard, a gift) speaks at once.
- **Voice:** about 80 new lines (trait lines, 13 scenes, pick and kit replies) are unvoiced. Speak them with the Voice Lines tool (one
  batch, LocalSpawn).

### 2.11 Co-op
- **Visiting a friend:** a guest can open the host's people read-only, but kit, picks and gifts are refused. That is right.
- **Show more:** a friend's people could show their traits and spirits, to make a visit about the host's people too.
- **The guest's own companion:** on a raid with the host, the guest's own companion counts their own talents (already true: it is read
  from `GameSession.Home`).

---

## 3. Suggested order

| Step | What | Why first | Size |
|---|---|---|---|
| 1 | Measure: playtest rows, F1 people cheats, a Systems Editor page (§2.9) | Every later number depends on it; the next playtest feeds it | Small |
| 2 | Anyone, any job; the work grid (§2.1, §2.4) | Makes talents and passions a choice | Medium |
| 3 | Companion picker and homecoming card (§2.2, §2.3) | Makes raids along a choice and shows their effect | Medium |
| 4 | Bond with the player from more than raids, and gifts (§1.1, §1.3) | Home-staying residents grow too | Medium |
| 5 | Habits (§1.2) | Personality in play, cheaply | Medium (content) |
| 6 | Message digest, voice batch (§2.10) | Polish before the playtest builds | Small |
| 7 | Rivals at work and forays, mentors; marks from breaches, forays and nights; a memorial (§1.4, §1.5) | Depth | Medium |
| 8 | Quest kinds and asks (§2.5), homes and meals (§2.6), horses (§2.8) | Weaving people into stories | Large, spread out |
| 9 | Rolled strangers (§1.7) | The user's "later" | Large |

---

## 4. Questions for the user

1. **Any job for anyone** (§2.1): may anyone take any job whose building is learned (recommended), or only their written trades plus
   vacant ones as now?
2. **Bond from more than raids** (§1.1): should lessons, asks, wishes, gifts and kit also grow a person's bond with you (recommended)?
3. **Gifts** (§1.3): two loved items each, given in talk for spirits and bond (recommended), or none?
4. **Rivals with costs** (§1.4): rivals slower near each other at work and weaker on a foray together (recommended, shown on the cards),
   or keep rivals to spirits and arguments only?
5. **Order:** start with measuring (step 1) before the next playtest (recommended), or with jobs for anyone (step 2)?
