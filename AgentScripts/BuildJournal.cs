using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Writes the journal's text and renders its creature pictures (run with `unity command script run --file AgentScripts/BuildJournal.cs --entry
/// BuildJournal.Apply`, after BuildCreatureModels). Backstory and mechanics go in Resources/JournalSettings; each quest gets its Journal Notes and
/// each creature its Lore, Advice and Portrait. Re-running updates them. Mechanics are from Docs/Combat.md and ARCHITECTURE.md: when a rule changes,
/// change its page here too.
/// </summary>
public static class BuildJournal
{
    private const string Settings = "Assets/_Project/Resources/JournalSettings.asset";
    private const string Portraits = "Assets/_Project/Art/UI/Journal/Creatures/";
    private const string Monsters = "Assets/_Project/Data/Monsters/";
    private const int PortraitSize = 384;
    private const string PaintedTutorials = "Tools/LocalSpawn/TutorialArt/";
    private const string TutorialArtFolder = "Assets/_Project/Art/UI/Tutorials/";
    private const string PaintedCreatures = "Tools/LocalSpawn/CreaturePortraits/";

    // ----- Backstory: group, title, line, body, the quest and the creature that open it -----

    private static readonly (string group, string title, string line, string body, string quest, string creature)[] Backstory =
    {
        ("The old world", "The Pact", "What people and forest once promised",
            "Long ago, people promised to leave the forest enough room to live. Its guardians promised in return to keep older, hungrier things at bay.\n\nNobody living can say when that ended. Some say the pact was broken. Others say it was only forgotten, which may be worse. The trails went first, then the villages, and the forest began to take memories along with the wildlife and the people.",
            null, null),
        ("The old world", "What the Forest Does", "It doesn't kill the lost",
            "The forest does not kill the lost. It forgets them.\n\nFirst the way home goes, then their names, and then there is nobody left to find. People who come back from deep in the trees talk about it the way you talk about a dream going as you wake. Ingrid kept herself by saying one word over and over so it couldn't be taken.",
            "Awakening", null),
        ("The old world", "The Hearth", "The one thing it cannot forget",
            "A hearth that someone tends is the one thing the forest cannot forget.\n\nThat was the old bargain, as far as anyone can make it out. People kept a fire beside the portal and the portal kept the night from their door. No fire had burned there in a hundred winters when you woke beside those stones.\n\nSo this is not a base for its own sake. Each fire feeds the ward and carries further through the mist. The lost can see it and know which way is home.",
            "FirstCamp", null),
        ("The old world", "The Portal", "An old thing, and awake",
            "At the heart of the clearing stand cracked old stones, warm to the touch. They are not quite a door and not quite a person.\n\nThe portal's ward still shelters one clearing, strong enough for daily peace and not much more. Where the old pact has cracked, things can push through, and you will usually hear about it first. It speaks when it has something to say. It is worth listening, and worth remembering that it has its own reasons for wanting a fire lit.",
            "Awakening", null),
        ("The expedition", "Seven Went In", "Why anyone came here",
            "The expedition came for four of its own, and to look into rumours of a bewitched forest.\n\nSigrid led it. Eirik carried a soldier's sense of where a wall is weakest and Runa could read a trail two days old. Ahead of them had gone Ingrid the alchemist, Bjarne the smith, Tor the carpenter and Liv, who reads runes and would rather be right than safe.\n\nYou walked with them. By noon it was the middle of the night.",
            null, null),
        ("The expedition", "The Day the Stones Broke", "How you came to be alone",
            "Something very large came through the trees, and the only way left was the old waystones. Sigrid knew the passage. You held the creatures back while the connection woke, and it opened badly.\n\nWhen the stones broke they threw everyone in a different direction. You were the one the portal caught, and you woke on quiet grass with nobody beside you.\n\nLiv says the stones broke along a line, and that this matters. Nobody has yet got her to say why.",
            "HoldPortal", null),
        ("The expedition", "Ingrid's Word", "The first to come home",
            "She lay under a root for three nights watching your fire and saying the word so the forest couldn't have it. Fire. Fire. Fire.\n\nIngrid knows more about the curse than she lets on. She saw Bjarne thrown north when the stones broke, towards the troll country. The rest she couldn't see.",
            "AStrangerInTheMist", null),
        ("The old world", "The Boundary Stones", "Where the pact's land ended",
            "When the pact was kept, people did not only tend a hearth beside the portal. They walked the land the pact gave them, and where it ended they set tall stones and lit them from that fire.\n\nA lit stone said that this much of the forest remembered a hearth. Inside the ring nobody lost the way home, and nobody lost their name.\n\nWhen the hearth went cold the stones went cold one by one, and the forest took back what they had held: the roads, the villages and everyone on them. The stones are still out there past the meadows, waiting for a hand that keeps a fire.",
            "TheColdStones", null),
        ("The forest's own", "The Guardians", "What was set to keep the woods",
            "The Root Guardians were not always hostile. They were set to keep the forest, on the forest's side of the old promise, and they are still doing it. Whatever twisted them did not tell them to stop.\n\nThat is the uncomfortable part. Nothing you meet on the root paths thinks it has done anything wrong.",
            null, "RootGuardian"),
        ("The forest's own", "The First of Them", "Grown vast under its hollow",
            "The Heartroot was the first of the guardians. It has grown old and enormous under its hollow, keeping a forest that is no longer there to be kept.\n\nNobody has told it. It is not at all clear that it could be told.",
            null, "TheHeartroot"),
        ("The forest's own", "Troll Country", "North, where the valleys start",
            "Trolls are older than the road and pay it no attention. The small ones throw stones and hold grudges. The old ones walk the same round for longer than anyone has kept count, with moss and whole trees growing on their shoulders, and they smell you long before they see you.\n\nStories say a troll can be bargained with, if you have something it wants and the nerve to stand still. This journal does not recommend finding out.",
            null, "OldMossTroll"),
        ("The forest's own", "The Barrow Dead", "Still in their mail",
            "Whoever lies in the barrows was buried with honour once, and in good iron. The draugar don't remember the honour. They remember the iron, and the wall they stood in, and they go on standing in it.\n\nThey are slow and patient and hard to stop. The old stories say fire ends them.",
            null, "Draug"),
    };

    // ----- Found in raids: a searched cache can turn up the next of these, in this order -----

    private static readonly (string title, string line, string body)[] Finds =
    {
        ("A Letter, Unsent", "Folded small, in a dry tin",
            "Dear Sister. They say the road is good as far as the second ford, and that I shall be home before the barley. The people here are kind, though they keep asking me what I am called.\n\nI told them. I am sure I told them.\n\nThe rest of it is the same three lines, written over and over in a hand that gets worse."),
        ("Liv's Notes on the Stones", "Her hand, and three different inks",
            "Waystones broke along a line, not at random. Line runs from the portal to the third barrow to somewhere north I can't get to.\n\nIf I am right, the stones were never meant to carry people. They carry the promise, and people are what falls off.\n\nSigrid will ask if that helps. It does not help. It is still true.\n\nLater, and shakier: Stay in the ring. Stay in the ring until someone comes, and do not answer anything that uses your name."),
        ("The Last Smith's Note", "Scratched on a rusted plate",
            "To the next one at this anvil. Keep the fire in. I don't mean the forge. I mean the one by the stones, the one that matters. I let it go out one winter because I had work on and I thought a night wouldn't hurt.\n\nI can't now remember what it was I was making.\n\nThe iron from the barrows is good. Don't take it from anyone who is still using it."),
        ("A Waystone Rubbing", "Charcoal on birch bark",
            "Someone pressed bark against a stone and rubbed it with charcoal until the marks came up. Beside it, in a careful hand, is a guess at the meaning.\n\nWe keep the fire. You keep the dark.\nWe leave the old trees. You leave the door.\nWhoever forgets first owes the other everything.\n\nThe guess has been crossed out, and under it someone has written: not a guess."),
        ("The Lost Man's Song", "A children's counting song, on a torn page",
            "One for the road and two for the ford,\nThree for the wolf and four for the lord,\nFive for the name that you gave at the door,\nSix for the one who won't ask any more.\n\nSeven's the fire. Don't say what it's for.\nSeven's the fire. Say it once more.\n\nEvery village south of the river knows this one, and nobody there can tell you where it comes from."),
        ("The Troll Toll", "A trader's list, much handled",
            "What the old one on the north road will take, as far as I have found:\n\nA whole cheese: no.\nA good knife: looked at it and gave it back.\nA song: stayed to hear the end, then hit the cart.\nA stone, carried from a long way off, and told where it came from: let us by.\n\nThey say it collects them. I say it misses somewhere. Don't run, and don't lie about the stone."),
        ("Barrow Inscription", "Copied from a door lintel",
            "HERE STOOD THE WALL AT THE NORTH GATE. THEY DID NOT GIVE WAY.\n\nBelow it, in smaller and much later letters:\n\nNobody has told them it is over. We tried. Bring fire and don't stand where they are looking."),
        ("The Hearth Law", "A page from an old rule of the house",
            "Let the hearth be tended by whoever is nearest, and let no one say it was another's turn.\n\nLet no one be turned away from it, for you do not know whose name they have lost.\n\nLet the fire be fed before the household, in hard winters and in good ones, and let it be told each night who is home and who is not, so that it may go on looking for them."),
        ("A Tally on Bark", "Notches, and one word",
            "A long strip of bark, cut with neat rows of notches in fives. Whoever kept it counted days for a long time. The rows get crooked about two thirds of the way along, and after that the notches stop being in fives.\n\nAt the very end, cut deep: FIRE."),
        ("Something Runa Left", "Tied to a branch with a bit of red cord",
            "A scrap with marks on it that only make sense after dark, when you can see which way the moss glows. Runa's way of leaving a trail for someone she expected to come after her.\n\nThere's a line at the bottom, in plain letters, as if she had thought better of being clever.\n\nIf you're reading this and you aren't one of mine, take the left fork anyway. The right one isn't there in the morning."),
    };

    // ----- The people, each on a page of their own once they are home -----

    private static readonly (string who, string line, string body)[] People =
    {
        ("Sigrid", "Leader of the expedition",
            "Sigrid has led people through worse country than most of them know about, and it shows mostly in what Sigrid doesn't say. It was her voice in the stones. She knew the old passage before any of you reached it, and she has not said how.\n\nShe was carried out of the woods with a wound she treats as a scheduling problem. Ask her about the expedition and you get the facts in order. Ask who taught her to read waystones and the subject changes.\n\nWhile she is home a raid gives you half a minute more: she has told you plainly how long you really have, and you believe her."),
        ("Eirik", "Soldier and warden",
            "Eirik was a soldier and has never entirely stopped. The first thing asked on seeing the Haven was where the wall was weakest, which at the time was all of it.\n\nYou found Eirik fighting, and it took a moment to tell whether help was wanted. Don't expect thanks. Expect to be told about your footwork.\n\nWith Eirik on the wall the ward holds half as long again in a breach. No one has explained how a person can make a ward stubborn, and Eirik sees no need to."),
        ("Runa", "Tracker",
            "Runa reads a trail two days old and counts her arrows when she lends them. She left marks in the woods that only show after dark, for people she wasn't sure were still coming, and she followed them home herself when you came.\n\nShe doesn't like how the forest has started moving. She doesn't like how the boar looked that first morning either, and has never said what she thinks it meant.\n\nWith Runa at home every briefing has one more rumour in it. She hears them from somewhere. It's better not to ask how far she walks at night."),
        ("Bjarne", "Smith",
            "The stones threw Bjarne north, into troll country, and he stayed alive there by being too much trouble to eat. He has a low opinion of your anvil. He'd have a low opinion of any anvil he hadn't made.\n\nHe talks about the trolls with more respect than he talks about most people. He says he struck a bargain with one and won't say what it was for.\n\nWith Bjarne home the forge works faster and repairs cost half. He hates seeing a good edge go to waste, and he hates it loudly."),
        ("Tor", "Carpenter",
            "Tor had to be cut out of the roots, and came out asking who had built your hearth, because it was crooked. It was. Tor went and fixed it before sitting down.\n\nTor doesn't say much about the time in the roots, except that they are very slow and very patient, and that wood remembers being a tree for longer than you would like.\n\nWith Tor home the sawbench works faster, and mending the Haven after a breach costs half."),
        ("Liv", "Rune scholar",
            "Liv reads runes, and would rather be right than safe. You found her in a ring of stones she had laid herself, still taking notes. She had not eaten for some time and was annoyed at being interrupted.\n\nShe says the stones broke along a line and that this matters. She has told you why three times now, and you are nearly sure each explanation was different. She doesn't seem to think that's a problem.\n\nLiv is the only one who knows the raid runes, and with her home the cauldron works faster too."),
    };

    // ----- What people say of a creature, shown on its page once they are home -----

    private static readonly (string who, string creature, string text)[] Notes =
    {
        ("Runa", "ForestBittenWolf", "The pack circles to the side you favour. Stand with a tree behind your weak hand and they run out of places to come from."),
        ("Eirik", "ForestBittenWolf", "Kill the one that's biting. Everyone wants to go for the big one."),
        ("Runa", "BlightBoar", "It only knows straight lines. I've watched one miss me and carry on through a fence."),
        ("Bjarne", "OldMossTroll", "Don't run. It's worse if you run. I stood still for most of a day once, and it only sniffed me."),
        ("Tor", "RootGuardian", "Good timber, once. That's what makes it sad. Go for where the limbs join, same as you would with any bad joint."),
        ("Tor", "Rootling", "They come apart if you hit them along the grain. I don't know why you would be sorry about it, but you are, a bit."),
        ("Liv", "MarshLight", "It isn't lost. It isn't leading you anywhere either. As far as I can tell it's just looking for someone, and you'll do."),
        ("Eirik", "Draug", "I knew what they were before anyone told me. They hold the line better than anyone I ever served with. Don't stand in front of it."),
        ("Eirik", "BarrowShieldbearer", "Same as any shield wall, and it's a wall of one. Go round."),
        ("Liv", "Draug", "The mail is older than the barrow. I would like to know who they were buried by."),
        ("Sigrid", "Huldra", "If she knows your name, you have already stayed too long. Keep walking and don't look at her back."),
        ("Ingrid", "BogSpitter", "The venom keeps very well, if you can bear to carry it. So do I, as it turns out."),
        ("Ingrid", "MossShaman", "Whatever it's saying to the ground, the ground is listening. Reach it first or you will be fighting all afternoon."),
        ("Sigrid", "TheHeartroot", "I've met people like it. It would be easier if it were cruel."),
    };

    // ----- Creatures: where they are found, and how many to know them (default three) -----

    private static readonly Dictionary<string, string> Habitats = new Dictionary<string, string>
    {
        ["Boar"] = "The meadows and woods near home",
        ["WildBoar"] = "The open glades around the Haven, by day",
        ["Deer"] = "Meadows and open glades around the Haven, by day",
        ["Stag"] = "Open glades around the Haven, at dawn and dusk",
        ["Fox"] = "The edges of the meadows, all year round",
        ["ForestBittenWolf"] = "The paths of the Misty Woods and Troll Valley, in packs",
        ["BlightBoar"] = "Blighted thickets and clearings, in both the Misty Woods and Troll Valley",
        ["Rootling"] = "Anywhere the roots are near the surface, usually several at once",
        ["Mistwing"] = "Low over the ground wherever the mist is deep",
        ["SmallMossTroll"] = "Rocky ground, with a good view and a supply of stones",
        ["RootGuardian"] = "The old root paths, standing where it was told to stand",
        ["MarshLight"] = "Wet ground and fog banks, more often the later it gets",
        ["BogSpitter"] = "Squatting in standing water, and it doesn't move for you",
        ["Draug"] = "The old barrows and the ground around them",
        ["BarrowShieldbearer"] = "Barrows, usually near other draugar",
        ["MossShaman"] = "At the back, with whatever it has called up",
        ["Huldra"] = "The deep woods, alone, where you weren't going",
        ["OldMossTroll"] = "Troll Valley, on the same round it has always walked",
        ["TheHeartroot"] = "Under its hollow, at the heart of the Misty Woods",
    };

    private static readonly Dictionary<string, int> KillsToKnow = new Dictionary<string, int>
    {
        ["Boar"] = 1,
        ["WildBoar"] = 1,
        ["Deer"] = 1,
        ["Stag"] = 1,
        ["Fox"] = 1,
        ["OldMossTroll"] = 1,
        ["TheHeartroot"] = 1,
        ["Huldra"] = 1,
        ["Rootling"] = 5,
    };

    // ----- Which quest brings a mechanics page into the journal. The rest are there from the start. -----

    private static readonly Dictionary<string, string> MechanicsOpenedBy = new Dictionary<string, string>
    {
        ["Setting Out"] = "AwakeningThePortal",
        ["The Clock and the Way Out"] = "AwakeningThePortal",
        ["What You Can Lose"] = "AwakeningThePortal",
        ["Carrying It Home"] = "AwakeningThePortal",
        ["Companions"] = "ThroughThePortal",
        ["The Ward and Renown"] = "FirstCamp",
        ["Stations and Work"] = "FirstCamp",
        ["Nights and Breaches"] = "TheWeakeningWard",
        ["Essence"] = "TheAlchemist",
        ["The Haven's Reach"] = "TheColdStones",
    };

    // ----- Which skill, tried for the first time, opens a mechanics page (by the skill's id). -----

    private static readonly Dictionary<string, string> MechanicsTriedBy = new Dictionary<string, string>
    {
        ["Sneaking"] = "sneaking",
        ["Swings and Chains"] = "melee",
        ["Guard and Parry"] = "melee",
        ["Dodging"] = "melee",
        ["Stamina and Focus"] = "melee",
        ["Poise and Stagger"] = "melee",
        ["Backstabs and Finishing Blows"] = "melee",
        ["Getting Hit"] = "melee",
        ["Woodcutting"] = "woodcutting",
        ["Mining"] = "mining",
        ["Foraging"] = "foraging",
        ["Fishing"] = "fishing",
        ["Archery"] = "archery",
        ["Abilities"] = "runecraft",
        ["Building"] = "construction",
    };

    // ----- Mechanics: group, title, line, summary (what it is), body (how it works). Keys in () or [] follow a rebind. -----

    private static readonly (string group, string title, string line, string summary, string body)[] Mechanics =
    {
        ("Getting about", "Your Windows", "What each key opens",
            "Everything you carry, make and know has a window of its own, each on its own key.",
            "(B) your bag and what you can make by hand\n(C) your character and what you wear\n(K) your abilities and the skill bar\n(N) your skills and perks\n(H) the Haven, from anywhere in it\n(G) the build menu\n(M) the map\n(J) this journal\n(T) the newest tutorial\n[E] use whatever is in front of you\n\nAny of these can be changed under Options, Controls, and this page will follow."),
        ("Getting about", "Your Journal", "It fills in as you go",
            "The journal keeps what you learn: the story so far, your quests, the creatures you have met and how things work.",
            "A dot marks anything you haven't read yet, and a tab's count shows how many of them it holds.\n\nA creature's page starts with little more than its name. It fills in as they fall: first how it fights and what it leaves, then how to live through it (being killed by one teaches you that too), and last what is told of it. People you bring home add what they know.\n\nSearching caches in a raid can turn up lost pages, which go in Backstory. Raids keeps a record of how each one ended.\n\nOn Quests you can pin a quest to the top of your tracker and, at home, go straight to where it wants you."),
        ("Getting about", "Sneaking", "Quiet, slow and worth it",
            "Moving slowly and quietly so the forest's creatures don't notice you, and striking first when they haven't.",
            "Hold Left Ctrl to sneak: half speed, quiet and hard to see. A hit on a creature that hasn't noticed you is an ambush, worth half again as much.\n\nCreatures notice you three ways. They see in a cone in front of them, and cover blocks it. They sense what is very close. And they hear, so running, fighting, chopping and picking locks all carry. Rain muffles you. A heavy bag doesn't."),
        ("Gathering", "Woodcutting", "Fell it, then work it where it lies",
            "Cut trees with an axe for logs and sticks. Well-timed swings bring down more wood.",
            "Walk up to a tree with an axe and hold the attack button, or [E], to raise it. Let go to swing. A glint at the top of the backswing is the perfect moment: let go then for a critical blow. Inside the yellow zone is a sound blow. Too early or too late is a glancing blow, and wears the axe.\n\nCriticals in a row build a combo that widens the perfect moment, and each one adds a log when the tree comes down.\n\nThe last blow fells the tree. Then the work moves to the ground: take the branches off for sticks, and cut the trunk into sections from the butt up, each one more logs.\n\nOn a raid every swing is heard. Chop too long in one place and the forest grows angry, and sends something to see what the noise is."),
        ("Gathering", "Mining", "Read the rock and strike true",
            "Break rock and ore with a pickaxe. Better ore needs a better pick.",
            "Hold the attack button, or [E], at a rock to raise your pickaxe, and let go to strike. Let go at the glint for a critical blow, inside the yellow zone for a sound one. Glancing blows wear the pick.\n\nCriticals in a row widen the perfect moment, and every critical adds ore when the rock breaks.\n\nOre comes in tiers: copper first, then iron, then moonsilver and the stranger stones of the deep woods. A pick below a rock's tier is slow going.\n\nOn a raid the ring of a pick carries a long way. Mine too long in one place and the forest sends something to find out why."),
        ("Gathering", "Foraging", "Berries, herbs, fibre and mushrooms",
            "Pick plants by hand, or cut them with a sickle, for food, medicine and cloth.",
            "Press [E] at a plant to pick it by hand. It takes a moment, and a better Foraging skill makes it quicker.\n\nSome plants are cut with a sickle instead, with the same swing as an axe: hold to raise, let go at the glint.\n\nBerries and mushrooms are food, herbs go into salves and draughts, and nettle and flax become cloth and cord.\n\nForaging is quiet work, the safest there is on a raid."),
        ("Gathering", "Fishing", "Patience, then a fight",
            "Catch fish with a rod and bait. Landing one is a tug of war you win by being steady.",
            "With a rod in hand, hold the attack button to raise it and swing it back, then let go to cast. Each cast takes one bait from your bag: grubs from rotten logs and stumps, or berries.\n\nWait for the float to go under, then strike. Now the fight is on: hold to reel in, let go to give line. When the fish runs to one side, lean the rod against it with the movement keys.\n\nToo much tension for too long and the line snaps. Too much slack and a fresh fish throws the hook. Steady pressure tires it, and a tired fish at the bank is yours.\n\nDifferent fish bite in still water and in rivers. And in still water, not everything that bites is a fish."),
        ("Combat", "Swings and Chains", "Patience beats clicking",
            "Your weapon's attacks come as a chain of swings, and timing matters more than speed.",
            "Tap the left mouse button for a light swing. Press again after it has landed and you go on to the next swing of the weapon's chain, and the last one is a finisher: wider, harder and better at breaking poise.\n\nPressing right as a swing recovers is on the rhythm, a small edge in damage. Swings never get faster, so mashing gains nothing.\n\nHold the button and the swing turns heavy: slow, nearly rooted, and hard enough to break poise. Keep holding to charge it, then let go to strike."),
        ("Combat", "Guard and Parry", "Part of a blow, or none of it",
            "A guard takes part of a blow. A guard raised at the last moment takes none of it and opens the attacker up.",
            "Hold the right mouse button to guard. It stops blows from the front, but part of each still gets through (a lot with bare fists, much less with a shield) and each block costs stamina. Run out and your guard breaks, and you reel for over a second.\n\nRaise the guard just before a blow lands and it is a parry: no damage, no stamina, the attacker staggers and your next swing is a sure critical. A creature's warning on the ground turns pale and the creature glints at exactly that moment.\n\nLowering your guard locks it for a moment, so tapping it is no free parry."),
        ("Combat", "Dodging", "Your way out of almost anything",
            "A quick roll or step out of harm's way, and the surest way out of a bad spot.",
            "Space dodges. A dodge can cut a swing short at any time, though during the wind-up that swing is lost and its stamina spent.\n\nDodging through a blow as it lands is a perfect dodge, and readies a counter. With an enemy in focus, a dodge steered towards or past it curves around to the spot behind it.\n\nWhat you wear decides how you dodge. Light gear gives a short, cheap quickstep, normal gear a roll, and heavy gear a slow shoulder charge that barges enemies at the end."),
        ("Combat", "Stamina and Focus", "One holds you up, the other pays for skills",
            "Two bars run a fight: stamina for everything you do, Focus for your abilities.",
            "Stamina runs everything physical and comes back quickly once you stop spending it. Watch it: an empty bar in the middle of a fight is how most people die.\n\nFocus, the gold bar, is earned by fighting well. Every hit adds some, a perfect dodge more and a parry most of all. It holds for ten seconds after your last gain, then drains. Skills and weapon arts cost Focus, so they are what good fighting buys and not what opens a fight."),
        ("Combat", "Poise and Stagger", "The thin amber bar",
            "Wear a creature down and it staggers, open to your best blows.",
            "Every hit wears down a creature's poise, the thin amber bar under its health. Heavy swings, finishers and counters wear it most. At zero it staggers: its attack is cancelled, it reels, and it takes more damage. A parry staggers it too.\n\nPoise comes back after a couple of seconds without being hit, so keep at it."),
        ("Combat", "Backstabs and Finishing Blows", "The best blow there is",
            "Striking a staggered enemy from behind is the strongest blow there is.",
            "A pale wedge on the ground shows the spot behind an enemy. A staggered enemy struck from there takes a sure critical at far more damage: that is the best blow in the game. So parry, roll behind, strike.\n\nA backstab straight out of a dodge gives back half of what the dodge cost.\n\nBehind a staggered person-shaped creature, such as a draug, a press locks you together for a finishing blow, and nothing can knock you out of it before the blade lands.\n\nEnemies learn. One that has taken a backstab, or seen one nearby, starts to see the next coming, and the wedge warms from pale to red as it does. It forgets given time. Great beasts and elites are only ever open while staggered."),
        ("Combat", "Getting Hit", "Don't trade blows",
            "What happens when a blow gets through, and why trading hits is a losing game.",
            "A hit that gets through while you wind up costs you the swing and leaves you reeling. Trading blows with a monster is a losing game, unless you are swinging something heavy: the wind-up of a heavy swing pushes through hits with a hammer, maul, greatsword or two-handed axe.\n\nSome attacks leave something behind if they get through. Poison drains your health and an Antidote cures it. Chill slows you and a Warming Brew cures it. Roots hold you in place, though you can still fight."),
        ("Combat", "Archery", "Draw, breathe, release",
            "Shoot arrows with a bow. A full draw, let go at the right moment, hits hardest.",
            "With a bow in hand, hold the attack button to draw and let go to loose. The arrow flies at the creature nearest your aim, outlined in red. A part draw is weaker, slower and less accurate.\n\nLet go just as the draw fills for a perfect release: a sure critical that flies true. In a fight the guard button aims instead of guarding: slower, but steadier.\n\nAfter a perfect dodge the next press looses an instant counter shot, even while reloading. A creature that hasn't noticed you takes an ambush bonus from an arrow too.\n\nArrows are used up, so bring enough."),
        ("Combat", "Abilities", "What Focus is for",
            "Abilities are strong moves and spells on keys 1 to 5, paid for with Focus and stamina.",
            "Your abilities are in the skill book (K). Drag one onto the bar to use it, and press (1) to (5) to use what is in each slot.\n\nAn ability costs Focus, the gold bar you earn by fighting well, and stamina, and then has to cool down before it can be used again.\n\nNew abilities come from rune books. At the altar, Purified Essence deepens the ones you know.\n\nEvery spell you cast trains Runecraft, and a better Runecraft makes them hit harder."),
        ("Raids", "Setting Out", "What you decide at the portal",
            "A raid is a trip through the portal into the forest, to find what the Haven needs and bring it home.",
            "The portal's planner shows where you can go and what you will meet. You choose a destination and a tier, and can pay Portal Sparks for more time (three minutes each, up to two) and carry runes that change what waits on the other side.\n\nEach raid has a reason, usually a rumour pointing you at something worth finding. The woods are laid out anew every time, so nothing is where you left it."),
        ("Raids", "The Clock and the Way Out", "Leave before it closes",
            "Every raid runs on a clock, and you must reach an exit and get through it before time runs out.",
            "A raid runs on a clock. As it runs down the woods grow more dangerous in stages, and the way home narrows.\n\nTo leave you have to reach an exit, light it and hold it until it opens. Holding takes time and can draw attention, so light one with time to spare.\n\nThe map (M) and the way line show you where to go."),
        ("Raids", "What You Can Lose", "Before you find out the hard way",
            "Falling on a raid costs what you carry. Knowing what is at stake helps you decide what to bring.",
            "If you fall or time runs out, you wake at the portal. What you carry is lost: materials, food and anything found in the raid.\n\nThe tools, weapons and packs you bring come back damaged, and can be repaired at home.\n\nYour safe pocket keeps what is in it no matter what, so put the one thing you can't bear to lose there. Your home, your storage, your recipes and what you have learned are never at risk from a raid."),
        ("Raids", "Carrying It Home", "Greed has a weight",
            "Everything you carry has weight, and a heavy load slows you down.",
            "Materials are items with weight. Past most of your bag's limit you slow down, tire faster and make more noise, and it gets worse the fuller it is. A Haul Sack carries more, but it is slower and louder still.\n\nCarts carry far more than you can, and can come through the portal with you, but creatures will raid one left alone."),
        ("Raids", "Companions", "Company has a price",
            "Someone from the Haven can come with you on a raid, at a risk.",
            "You can take someone with you. They fight beside you and take orders in conversation: follow, stay, defend, retreat or attack your target.\n\nIf they fall, they may be lost to the forest, and it will start to forget them. You can find them again, but it takes a rumour and a raid to do it, and they may not remember everything when they come home."),
        ("Safe Haven", "The Ward and Renown", "Why a bigger home is a safer one",
            "The portal's ward keeps the clearing safe, and it grows as the Haven grows.",
            "The portal's ward is what keeps the clearing peaceful. Everything you build and clear adds renown, and renown raises the Haven's rank, which widens the ward and lights the way a little further for anyone still out there.\n\nThe Haven window (H) shows what counts, what to do next and how far along you are."),
        ("Safe Haven", "Building", "Each thing opens the next",
            "Raise new buildings anywhere in the Haven's meadows. Each one opens new things to make and do.",
            "Press (G) to build anywhere in the Haven's meadows (inside the ward is safe at night). Most buildings go up as a scaffold you hammer with [E]. Each one opens the next, and every tier of a building opens something new to make, takes a chore off your hands or answers a problem you will meet in the woods.\n\nRecipes you can't make yet still show at the station, with what they need, so you can see what to aim for."),
        ("Safe Haven", "Stations and Work", "It goes on without you",
            "Crafting stations work while you are away, and your people can work them for you.",
            "Stations keep working while you are away. Pay on queuing, leave for a raid, and come back to a finished output slot. Crops on a Farm Plot grow the same way.\n\nGive someone a job from the Haven window and they make things from what is in your stash while you are gone. People want somewhere to sleep, and beds help them, but beds are never what stops you from growing."),
        ("Safe Haven", "Nights and Breaches", "Safe, but not perfectly",
            "Night brings creatures to the edge of the light, and now and then something tests the ward.",
            "After the first breach is won, weak creatures prowl just outside the light at night, so keep your fires lit. Cursed Essence is richer after dark too, if you want to go looking.\n\nNow and then something tests the ward. A breach is always announced ahead of time: you get a warning, time to prepare, the defence itself and a chance to recover. It only happens while you are there, and it can't wipe out the home you have built. Damaged buildings can be repaired."),
        ("Safe Haven", "The Haven's Reach", "Light the stones, then hold the land",
            "Beyond the meadows the land is split into regions, each with a boundary stone. Light a stone and its region is the Haven's; hold it against the forest and it gives back.",
            "Claiming. Find one of a region's named places (walk up to it and it goes on your map (M)), then use its boundary stone [E]. The first stone answers to your fire alone; every one after wants a Pact Rune. Runes turn up in supply caches on raids, and at the rot's heart.\n\nHolding. A held region has a hold from 0 to 100. It starts at 40 and wears a little at every turn (each dawn at home, and the end of each raid), faster under blight. Below 25 it is slipping, from 70 it is Secure, and at nothing the forest takes it back.\n\nWhat firms it: what the region asks now and then (hunting, gathering or fishing there: its stone says which), a Pact Rune offered at its stone (+25), and each of its places you find (+5).\n\nThe forest pushes back. Now and then it gathers against the weakest of your lands, and two turns later pushes in and takes about 30 hold. Stand watch at that region's stone (what the forest sends comes for it: beat it all without straying) to break the push, or burn the rot's heart on a raid: each heart halves it. Or let it come.\n\nWhat it gives. Every held region adds renown. A Secure one sends its goods to the stash at every dawn, and the corrupted nights never come from it.\n\nThe Haven window's Lands tab (H) shows every region, how firmly it is held and what it asks. With friends, the lands are the host's: a friend can lay runes in the host's stones, help with what a land asks, and fight beside the host's watch."),
        ("Keeping going", "Hunger and Thirst", "They won't kill you",
            "You need to eat and drink to stay at your best, and a good meal does more than fill you.",
            "Hunger and thirst wear down what you can do, but they never kill you. Berries and a stream will do to start with.\n\nCooked meals give timed bonuses on top of what they restore. You can have two at once, and a third pushes out the one closest to ending. Tonics from the Cauldron take no meal slot. Both go through the portal with you, so the meal before a raid counts, but a failed raid loses them.\n\nEating stops you for a moment, and a hit that gets through interrupts it, though the food is kept. Its healing arrives over several seconds."),
        ("Keeping going", "Skills and Perks", "Getting better at it",
            "You get better at what you do: thirteen skills, from Woodcutting to Runecraft, each with its own level and perks.",
            "Every skill grows by doing it. Swing an axe and Woodcutting grows, land blows and Melee grows, cook and Cooking grows. Work done on a raid counts half again, but that share only comes home if you do.\n\nEvery ten levels a skill reaches a milestone and you choose one of two perks. You can choose again at home, beside the portal.\n\nQuests, raids, first crafts, catches and new places give Insight instead. Put it into any skill you like at (N).\n\nA skill grows faster once its mentor building at home is upgraded, and after a good night's sleep."),
        ("Keeping going", "Essence", "What the forest leaves behind",
            "Cursed Essence comes from the forest. Purified at the altar, it makes your gear and skills stronger.",
            "Cursed Essence comes from the forest and its creatures. Purify it at the altar and Purified Essence will empower your gear, enchant it and deepen your skills.\n\nEvery offer at the altar says what it costs and what it does, and none of them can fail or waste your essence. Where there is a risk, it is a stated price."),
    };

    // ----- Quests: the notes the journal adds, by asset name -----

    private static readonly Dictionary<string, string> QuestNotes = new Dictionary<string, string>
    {
        ["MorningCamp"] = "Bjarne, Tor, Liv and Ingrid went on ahead two days ago to see what they could of the forest. Nobody here will say they are worried. Everybody has found a reason to stay near Sigrid this morning.",
        ["EirikLesson"] = "Eirik was a soldier before this and teaches the way soldiers do: briefly, and assuming you'll need it by nightfall. What he is really showing you is that a staggered enemy struck from behind takes the most. The Mechanics tab has the rest.",
        ["FishForBreakfast"] = "Nobody has ever been talked out of porridge on an expedition, but Eirik keeps trying.",
        ["HuntForThePot"] = "Runa doesn't like it. Boar don't come this far onto open grass. She hasn't said what she thinks it means, but she has stopped lending out arrows without counting them.",
        ["WoodForTheRoad"] = "Any tree can be felled with an axe. Bigger trees give more logs, and now and then something better than plain wood.",
        ["RoadToForest"] = "The old road runs straight at the trees. Somebody built it to go somewhere, and nobody at the camp knows where.\n\nChoose your weapon by how you want to fight rather than by its numbers. You will be making others soon enough.",
        ["FindShelter"] = "It really is noon. Everyone agrees on that and no one can explain the stars. Runa says the forest has moved since the others came through, which is not something forests do.\n\nSticks and small rocks lie about on the forest floor, and you can pick them up as you walk: this is how every fire you ever build starts. Press (G) to open the build menu. It is the first fire anyone has lit in here for a very long time, though you won't know that yet.",
        ["BerriesForThePot"] = "Eirik is right about this, annoyingly. Hunger won't kill you, but it will wear you down. A cooked meal does more than fill you: it gives you a bonus for a while.",
        ["CatchBreath"] = "If you have anything to ask anyone, this is when to do it. They are more honest around a fire.",
        ["DefendCamp"] = "Wolves in a pack take turns at you. Watch for the lunge, and don't let yourself be surrounded. The rest of the expedition will fight beside you.",
        ["ReachPortal"] = "Whatever it is, the trees are moving out of its way. Don't stop to look.\n\nSigrid has seen writing like this once before, and she doesn't say where. The stones are older than the road, and much older than anyone who should know how to use them.",
        ["HoldPortal"] = "Keep moving and keep them off the stones. Falling here isn't the end of it, as the connection will take you whether you are ready or not.",
        ["Awakening"] = "You remember the stones breaking and very little after that. The grass is dry and the air is quiet, and nothing here is trying to kill you, which already feels strange.",
        ["FirstCamp"] = "This is the old bargain, though you are only starting to understand it. A fire inside the ward feeds the ward. It also shows, a long way off, to anyone who is looking for it.\n\nSticks and small rocks lie on the ground around the portal: walk up to them and pick them up (E). The build menu (G) shows what the fire costs.",
        ["FoodAndWater"] = "You'll be walking people home through bad country, and the meal before a raid goes through the portal with you. Get in the habit of it.",
        ["FirstWeapon"] = "Nettles grow at the clearing's edge: pick them by hand for the fibre. Z swaps between the weapon in your hand and the one you held before it.",
        ["ARoofForTheWork"] = "The Workshop wants oak, grey stone and sticks: fell the oaks at the clearing's edge with a hatchet and break the grey boulders with a pick. Repairs are made at its bench.",
        ["PrimitiveTools"] = "Stone tools are made from your bag (B), with no station needed, and go on your belt by themselves. Without them a raid still brings people and loose finds home, but no wood and no ore.",
        ["RootboundStones"] = "The roots reached inside the ward while no one was watching it. Cutting them free gives the portal back a little of what it has lost, and it is the first time you will hear it sound pleased.",
        ["AwakeningThePortal"] = "Once it opens it opens both ways. The portal will tell you so itself, in its own fashion. Read the raid planner carefully the first time, and note what you stand to lose.",
        ["ThroughThePortal"] = "Your first raid. Put anything precious in your safe pocket, and turn back earlier than feels necessary. A hatchet and a pick let you bring hardwood and ore home as well as people.\n\nFinding Ingrid counts even when the raid goes wrong: she waits where you found her.",
        ["AStrangerInTheMist"] = "She is hurt and won't be quick. Clear the way before you lead her out. If she falls she doesn't die, but she will wait where you found her, and the forest is good at making people wait.",
        ["KnowYourEnemy"] = "Every wolf you kill is one fewer on the path home. They are not quite wolves any more, but they still hunt like wolves.",
        ["AGrowingHome"] = "Tor will say your hearth was built crooked, Bjarne will want an anvil, and both will be right. Each building here opens the next, so the H window is the place to plan from.",
        ["TheAlchemist"] = "Ingrid knows the old ways of cleansing cursed things and is cagey about where she learned them. Give her a job from the H window. Cursed Essence is useless raw; the altar is where it starts to pay.",
        ["TheWeakeningWard"] = "A breach is announced, it comes while you are here to meet it, and it can't undo what you have built. You still don't want to meet it unprepared. Palisades, traps and a full stomach all help, and you choose when to begin.",
        ["ALightTheyCanSee"] = "Everyone left out there is looking for the same thing Ingrid saw. Each rank of the Haven carries the light further, and that may be the difference between someone finding their way and someone forgetting there was a way to find.",
        ["EchoesInTheWilds"] = "Runa would say that anything calling to you from deep in the woods wants something from you. She would probably also go and look.",
        ["SmithsRequest"] = "There was a smith here before, and the note is older than it looks. Nobody knows who wrote it, or who they thought would read it.",
        ["BringBjarneHome"] = "Ingrid heard him working one-handed, so he is hurt and slower than he will admit. Clear the way before you lead him out. Once home he mends what the woods break, at half the cost.",
        ["BringTorHome"] = "Bjarne blazed the trees on the way in and found his marks on the wrong side of them coming out: the roots move the road at night. Tor's leg is broken, so he is slow. Planks start with him: palisades, the shed and the watch post all want them.",
        ["BringEirikHome"] = "Ingrid saw something walk past her root in Granheim mail that didn't know its name. If it was Eirik, the forest has been at him longer than at anyone. He will fight beside you on the way out, and be too proud to say he is tired.",
        ["BringGunnarHome"] = "Gunnar isn't one of the expedition. He has lived in the woods by choice for longer than he will say, and he may not think he needs rescuing. Leather comes home with him: copper armour wants it for lining, and the Dock is his too.",
        ["BringAsaHome"] = "Asa stayed with her seed while the farmstead burned around her. She is stubborn, and she is right: a haven full of people can't live on berries and game. With her home the Farm Plot opens.",
        ["BringYrsaHome"] = "Anyone who cooks well enough to draw wolves can cook for a haven. Mind the wolves: they found her first. Her meals keep their bonuses for longer than anything off a campfire.",
        ["BringRunaHome"] = "Runa marks a trail so that only those who know her can read it, and only at night. Go after dark. She was the one who said the forest had moved since the others came through; she will want to hear she was right.",
        ["BringLivHome"] = "Liv went ahead with the first four, to read the stones. If a ring of them is humming with someone inside, it is her, and she has probably been taking notes. She cuts runes nobody else at home can.",
        ["BringHildHome"] = "Nobody knows what Hild was walling in, or out. She is a mason, not one of the expedition, and she builds in stone while everyone else builds in wood.",
        ["BringSigridHome"] = "Sigrid led the expedition, and it was her voice in the stones the day they broke. She has been out there longest of all. The horn is the expedition's old signal: answer it at dusk.",
        ["HeavyLoads"] = "Carts carry what a pack can't. The Wainwright's Yard wants Tor's lodge at Tier II first; he will tell you so, more than once.",
        ["TheCook"] = "The Kitchen wants Bjarne's Smithy standing first. Yrsa's meals give their bonuses for longer than a stew from the campfire, and she will tell you which jarl she cooked them for.",
        ["TheFarmer"] = "Crops grow while you are away, like everything else your people tend. Asa weaves too: give her the Weaver's work instead and she runs the Tailor's Shop.",
        ["TheHunter"] = "Gunnar cures hides into leather while you raid. As a Fisher instead, he works the Dock.",
        ["TheLeader"] = "With Sigrid as Leader every raid has more time on its clock. The banner is for the rest of you: people work better under one.",
        ["TheMason"] = "Hild's Cottage sleeps four and has a proper hearth. Stone walls come later, at Tier III of her work.",
        ["TheRuneScholar"] = "Runes cut at the Cauldron go with you on a raid. Liv will explain what each one does, at length, whether you ask or not.",
        ["TheSmith"] = "With Bjarne at the Smithy, repairs cost half as much and copper ore becomes ingots while you are away. At Tier II the Smithy makes copper tools.",
        ["TheTracker"] = "With Runa as Tracker, every raid's briefing names one more thing that is out there before you go.",
        ["TheWarden"] = "With Eirik as Warden the ward holds half as long again in a breach. The Watch Post wants the Woodcutter's Lodge standing first.",
        ["SafeKeeping"] = "A chest at home is the one place nothing can take from you. Before every raid, ask what you could stand to lose, and leave the rest here.",
        ["TheWakingStone"] = "Whatever is in the stone has been there a long time, and it has noticed you.",
    };

    // ----- Creatures: lore and advice, by asset name -----

    private static readonly (string creature, string lore, string advice)[] Creatures =
    {
        ("ForestBittenWolf", "Runa says these were ordinary wolves once. The curse got into them through the prey they ate, and they kept the pack and lost the fear of fire. They hunt the same paths your people must walk home.",
            "They come in packs and take turns at you, so keep them in front of you and watch for the lunge. A tier II wolf learns to pounce. Parry the bite, and don't chase one that backs away, as that is how the others get behind you."),
        ("BlightBoar", "Boar gone sour in the blighted thickets. They graze where nothing should grow any more, and Eirik won't eat the meat until it has been cooked twice.",
            "It leaves you alone at a distance. Step too close and it charges straight at you, then stands blown and open when the charge is spent. Roll aside, and strike it then."),
        ("Boar", "An ordinary wild boar, the kind the old villages hunted every autumn. There are fewer of them than there were, and the ones left are wary.",
            "It will run if it can and fight if it can't. Come at it from where it can't see you, and keep out of the way of the tusks once it turns."),
        ("WildBoar", "A big old boar, grey about the snout, that has lived through enough winters to trust nothing on two legs.",
            "It bolts at the first sight or sound of you. Creep close, or bring a bow: one good arrow before it runs is worth three after."),
        ("Deer", "Roe deer, grazing the meadows and the glades by day. Where there are deer the forest is still itself, and Gunnar watches how many there are.",
            "It runs at the first sight or sound of you. Sneak, or shoot from further off than you think you need to."),
        ("Stag", "A red deer stag, carrying its crown through the glades. Seeing one at dawn was a good omen once.",
            "Quick to bolt and quick to spot you. Come from downwind, from cover, and make the first arrow count."),
        ("Fox", "A red fox, sharp-eared and never far from the meadow's edge. In winter its coat goes white.",
            "Small, fast and off at the first sound. Not much meat, but a fine pelt."),
        ("Rootling", "A knee-high scrap of root and moss that has learned to walk. They are not very clever, and Liv thinks there may be only one of them, in a sense.",
            "One is nothing, but they are never one. Don't let them gather around you, and if something is calling them up, go for it first."),
        ("Mistwing", "A low-flying thing of the deep mist, all leather and hunger. Birds go quiet when they pass.",
            "It holds off, then commits to one long dive. Dodge the dive and hit it during the two seconds it spends on the ground getting up again. An arrow will also find it while it circles."),
        ("SmallMossTroll", "The young of their kind, if trolls have young. They collect stones and throw them with great accuracy and no apparent malice, and they carry grudges for years.",
            "It throws three stones, then has to recover. Dodge all three and close in during the recovery. Up close it has hands enough to parry a blow it sees coming from behind."),
        ("RootGuardian", "Old guardians of the forest, set there to keep it. They are still keeping it. They just no longer know what from.",
            "Slow and heavy, and its sweeping blows are telegraphed long before they land, so there is time to read them. It can call roots from the ground. Only open to a backstab while staggered, so wear down its poise first."),
        ("MarshLight", "A pale light drifting over wet ground. Every marsh story has one, and in all of them somebody follows it. Nobody knows what they are, and it doesn't seem to have a body to find out with.",
            "Follow it and it leads you off the path. Get close and it runs. Its cold bolts slow you down for whatever else is waiting, so deal with it quickly, carry a Warming Brew, and don't chase it into anything."),
        ("BogSpitter", "A bloated thing the size of a calf, squatting where the ground is wet. It has been there a long time and is in no hurry at all.",
            "It spits venom that keeps working long after it lands. Stay out of the line, close the distance, and carry an Antidote."),
        ("Draug", "The dead of the barrows, still in their mail. They were buried with honour and in good iron, and they remember only the iron. Old stories say fire ends them.",
            "Slow, patient and hard to stop, and its touch is grave-cold. It will raise a war cry to rally others. Staggered and struck from behind it can be finished outright, but it has hands enough to parry what it sees coming."),
        ("BarrowShieldbearer", "A draug that still remembers its shield wall. Whoever it was standing beside has been gone for centuries, and it has not noticed.",
            "Nothing gets through from the front. Get behind it, wait for it to strike, or break its guard with something heavy. Once staggered it can be finished like any draug."),
        ("MossShaman", "An old troll with a staff of living wood. It doesn't want to fight you. It wants the ground to.",
            "Rootlings rise wherever it points, and it would rather stand back and throw stones while they work. Kill it first, if you can reach it. It will run when badly hurt, so don't let it go and start over."),
        ("Huldra", "A forest spirit in the shape of a beautiful woman, with a hollow back like a rotten tree. Every valley has a story of someone who followed her, and they all end the same way.",
            "Never follow her into the trees. She is only ever open to a backstab while staggered, so wear her down and watch for the claws."),
        ("OldMossTroll", "A troll as old as the hills, walking its round through Troll Valley with moss on its back and a tree on its shoulders. Its eyes are weak, but it smells you long before it sees you and hears every step.",
            "Best avoided. You can feel it coming through the ground, so step off its round and let it pass. If you must fight, expect wide sweeps and a stomp that reaches all around it, and only hit it from behind once it is staggered. Being heard calling its kin is a good reason to leave."),
        // The tusse (BuildTusse) and the folk-tale creatures (BuildFolkCreatures), 2026-11-13: the same words as those builders.
        ("Tusse", "The tusser live under the hills and in the roots of old trees, and they have always taken what was left out. Grandmothers set out porridge to keep them sweet. Nobody sets out porridge now, so they come for the rest.",
            "It runs for your bag or a cart, snatches one thing and is off. Kill it and it drops what it took; let it get far enough and that is gone. It only fights when it has nowhere to run, and then not for long: corner it against rock or water."),
        ("Fossegrim", "Every waterfall has one, the old folk say: a grey man sitting in the spray with a fiddle, playing a tune that makes the trees dance. Bring him a fat ram and he will teach you to play. Bring him nothing and you will hear the tune anyway, and follow it into the water.",
            "It keeps away and flings the cold spray of the falls, and its tune stirs the creatures near it. It is all bone and wet hair: close in and it breaks easily."),
        ("Trollkjerring", "Where the trolls go, an old one goes with them: the trollkjerring, who brews in a pot under the hill and remembers every slight. The small trolls fear her more than they fear you.",
            "She hangs back behind the others, throwing stones, rallying them and calling rootlings out of the ground. Reach her first: without her the rest scatter."),
        ("Bergjutul", "The jutuls were here before the trees. They sleep for a hundred years at a time and wake up cross, with moss on their shoulders, and walk until they find whatever woke them.",
            "Slow, and stone all over: blades skid off it. Stay out of its stamp and sweep and hit it as it recovers. Blunt weapons and fire do better than edges."),
        ("TheHeartroot", "The first of the guardians, grown old and vast under its hollow. It was set here to keep the forest. Nobody has told it the forest is gone.",
            "It wakes slowly, and the fight changes as it goes on: it learns new things at each stage. Roots rise at its call, so don't stand still, and bring food. What it gives when it falls is knowledge, and you keep that even if you die on the way home."),
    };

    public static string Apply()
    {
        var log = new List<string>();
        var settings = AssetDatabase.LoadAssetAtPath<JournalSettings>(Settings);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<JournalSettings>();
            AssetDatabase.CreateAsset(settings, Settings);
        }

        var so = new SerializedObject(settings);
        SerializedProperty backstory = so.FindProperty("backstory");
        backstory.ClearArray();
        foreach ((string group, string title, string line, string body, string quest, string creature) in Backstory)
        {
            backstory.InsertArrayElementAtIndex(backstory.arraySize);
            SerializedProperty page = backstory.GetArrayElementAtIndex(backstory.arraySize - 1);
            Write(page, group, title, line, body);
            page.FindPropertyRelative("unlockedByQuest").objectReferenceValue = quest != null ? Quest(quest, log) : null;
            page.FindPropertyRelative("unlockedByCreature").objectReferenceValue = creature != null ? Monster(creature) : null;
            page.FindPropertyRelative("unlockedByResident").objectReferenceValue = null;
            page.FindPropertyRelative("foundInRaids").boolValue = false;
            page.FindPropertyRelative("art").objectReferenceValue = PageArt(title);
        }

        foreach ((string who, string line, string body) in People)
        {
            backstory.InsertArrayElementAtIndex(backstory.arraySize);
            SerializedProperty page = backstory.GetArrayElementAtIndex(backstory.arraySize - 1);
            Write(page, "The people", who, line, body);
            page.FindPropertyRelative("unlockedByQuest").objectReferenceValue = null;
            page.FindPropertyRelative("unlockedByCreature").objectReferenceValue = null;
            page.FindPropertyRelative("unlockedByResident").objectReferenceValue = Resident(who);
            page.FindPropertyRelative("foundInRaids").boolValue = false;
            // A person's page shows their portrait (BuildUIArt.Residents).
            page.FindPropertyRelative("art").objectReferenceValue = Resident(who).Portrait;
        }

        foreach ((string title, string line, string body) in Finds)
        {
            backstory.InsertArrayElementAtIndex(backstory.arraySize);
            SerializedProperty page = backstory.GetArrayElementAtIndex(backstory.arraySize - 1);
            Write(page, "Found in the woods", title, line, body);
            page.FindPropertyRelative("unlockedByQuest").objectReferenceValue = null;
            page.FindPropertyRelative("unlockedByCreature").objectReferenceValue = null;
            page.FindPropertyRelative("unlockedByResident").objectReferenceValue = null;
            page.FindPropertyRelative("foundInRaids").boolValue = true;
            page.FindPropertyRelative("art").objectReferenceValue = PageArt(title);
        }

        SerializedProperty mechanics = so.FindProperty("mechanics");
        mechanics.ClearArray();
        int painted = 0;
        foreach ((string group, string title, string line, string summary, string body) in Mechanics)
        {
            mechanics.InsertArrayElementAtIndex(mechanics.arraySize);
            SerializedProperty page = mechanics.GetArrayElementAtIndex(mechanics.arraySize - 1);
            Write(page, group, title, line, body);
            page.FindPropertyRelative("unlockedByQuest").objectReferenceValue = MechanicsOpenedBy.TryGetValue(title, out string opener) ? Quest(opener, log) : null;
            page.FindPropertyRelative("unlockedByCreature").objectReferenceValue = null;
            page.FindPropertyRelative("unlockedByResident").objectReferenceValue = null;
            page.FindPropertyRelative("foundInRaids").boolValue = false;
            page.FindPropertyRelative("unlockedBySkill").objectReferenceValue = MechanicsTriedBy.TryGetValue(title, out string skill) ? Skill(skill, log) : null;
            page.FindPropertyRelative("summary").stringValue = summary;
            Sprite art = TutorialArt(title);
            page.FindPropertyRelative("art").objectReferenceValue = art;
            painted += art != null ? 1 : 0;
        }

        log.Add($"{painted} of {Mechanics.Length} mechanics pages painted.");

        SerializedProperty listed = so.FindProperty("creatures");
        listed.ClearArray();
        Directory.CreateDirectory(Portraits);
        foreach ((string creature, string lore, string advice) in Creatures)
        {
            MonsterDefinition definition = Monster(creature);
            listed.InsertArrayElementAtIndex(listed.arraySize);
            listed.GetArrayElementAtIndex(listed.arraySize - 1).objectReferenceValue = definition;
            var entry = new SerializedObject(definition);
            entry.FindProperty("journalLore").stringValue = lore;
            entry.FindProperty("journalAdvice").stringValue = advice;
            entry.FindProperty("journalHabitat").stringValue = Habitats.TryGetValue(creature, out string habitat) ? habitat : string.Empty;
            entry.FindProperty("journalKillsToKnow").intValue = KillsToKnow.TryGetValue(creature, out int kills) ? kills : 3;
            entry.FindProperty("portrait").objectReferenceValue = Portrait(definition);
            entry.ApplyModifiedPropertiesWithoutUndo();
        }

        SerializedProperty residentNotes = so.FindProperty("residentNotes");
        residentNotes.ClearArray();
        foreach ((string who, string creature, string text) in Notes)
        {
            residentNotes.InsertArrayElementAtIndex(residentNotes.arraySize);
            SerializedProperty note = residentNotes.GetArrayElementAtIndex(residentNotes.arraySize - 1);
            note.FindPropertyRelative("who").objectReferenceValue = Resident(who);
            note.FindPropertyRelative("about").objectReferenceValue = Monster(creature);
            note.FindPropertyRelative("text").stringValue = text;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        int notes = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:QuestDefinition"))
        {
            var quest = AssetDatabase.LoadAssetAtPath<QuestDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (QuestNotes.TryGetValue(quest.name, out string note))
            {
                var entry = new SerializedObject(quest);
                entry.FindProperty("journalNotes").stringValue = note;
                entry.ApplyModifiedPropertiesWithoutUndo();
                notes++;
            }
        }

        AssetDatabase.SaveAssets();
        return $"{Backstory.Length} backstory pages, {People.Length} people, {Finds.Length} finds, {Notes.Length} notes, {Mechanics.Length} mechanics pages, {Creatures.Length} creatures, {notes} of {QuestNotes.Count} quest notes. " + string.Join(" ", log);
    }

    private static void Write(SerializedProperty page, string group, string title, string line, string body)
    {
        page.FindPropertyRelative("group").stringValue = group;
        page.FindPropertyRelative("title").stringValue = title;
        page.FindPropertyRelative("line").stringValue = line;
        page.FindPropertyRelative("body").stringValue = body;
    }

    private static MonsterDefinition Monster(string name)
    {
        var definition = AssetDatabase.LoadAssetAtPath<MonsterDefinition>($"{Monsters}{name}.asset");
        if (definition == null)
        {
            throw new FileNotFoundException("No monster definition " + name);
        }

        return definition;
    }

    private static ResidentDefinition Resident(string name)
    {
        foreach (string guid in AssetDatabase.FindAssets($"{name} t:ResidentDefinition"))
        {
            var resident = AssetDatabase.LoadAssetAtPath<ResidentDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (resident.name == name)
            {
                return resident;
            }
        }

        throw new FileNotFoundException("No resident " + name);
    }

    private static QuestDefinition Quest(string name, List<string> log)
    {
        foreach (string guid in AssetDatabase.FindAssets($"{name} t:QuestDefinition"))
        {
            var quest = AssetDatabase.LoadAssetAtPath<QuestDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (quest.name == name)
            {
                return quest;
            }
        }

        log.Add($"No quest {name}.");
        return null;
    }

    private static ProficiencyDefinition Skill(string id, List<string> log)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:ProficiencyDefinition"))
        {
            var skill = AssetDatabase.LoadAssetAtPath<ProficiencyDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (skill != null && skill.Id == id)
            {
                return skill;
            }
        }

        log.Add($"No skill {id}.");
        return null;
    }

    /// <summary>A file name from a page's or creature's name: "The Clock and the Way Out" -> the_clock_and_the_way_out.</summary>
    public static string Slug(string name) => System.Text.RegularExpressions.Regex.Replace(name.ToLowerInvariant(), "[^a-z0-9]+", "_").Trim('_');

    // A backstory page's or find's picture (Tools/LocalSpawn/generate_ui_art.py journal), copied into the game; none until it is painted.
    private static Sprite PageArt(string title)
    {
        string source = "Tools/LocalSpawn/JournalArt/" + Slug(title) + ".png";
        return File.Exists(source) ? CopyIn(source, "Assets/_Project/Art/UI/Journal/Pages/" + Slug(title) + ".png", 2048) : null;
    }

    // The page's painting from LocalSpawn (Tools/LocalSpawn/generate_ui_art.py tutorials), copied into the game; none until it is painted.
    private static Sprite TutorialArt(string title)
    {
        string source = $"{PaintedTutorials}{Slug(title)}.png";
        if (!File.Exists(source))
        {
            return null;
        }

        string path = $"{TutorialArtFolder}{Slug(title)}.png";
        return CopyIn(source, path, 2048);
    }

    // A painting copied into the project as a sprite, unless the same picture is there already.
    private static Sprite CopyIn(string source, string path, int maxSize, bool mipmaps = true)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        if (!File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(File.ReadAllBytes(source)))
        {
            File.Copy(source, path, true);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer.textureType != TextureImporterType.Sprite || importer.maxTextureSize != maxSize || importer.mipmapEnabled != mipmaps)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            // Mips for what is also drawn small (a portrait in a list row, a tutorial's thumbnail): none for full-screen art.
            importer.mipmapEnabled = mipmaps;
            importer.filterMode = mipmaps ? FilterMode.Trilinear : FilterMode.Bilinear;
            importer.maxTextureSize = maxSize;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    /// <summary>
    /// Every creature with a LocalSpawn portrait (Tools/LocalSpawn/generate_ui_art.py creatures) gets it, the journal's listed ones and the
    /// animals beside them, which had no picture of their own (run with --entry BuildJournal.PaintedPortraits).
    /// </summary>
    public static string PaintedPortraits()
    {
        int done = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:MonsterDefinition"))
        {
            var creature = AssetDatabase.LoadAssetAtPath<MonsterDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            Sprite painted = creature != null ? PaintedPortrait(creature) : null;
            if (painted == null)
            {
                continue;
            }

            var entry = new SerializedObject(creature);
            entry.FindProperty("portrait").objectReferenceValue = painted;
            entry.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(creature);
            done++;
        }

        return $"{done} creatures given their painted portrait.";
    }

    private static Sprite PaintedPortrait(MonsterDefinition creature)
    {
        string source = $"{PaintedCreatures}{creature.name}.png";
        return File.Exists(source) ? CopyIn(source, $"{Portraits}{creature.name}.png", 512) : null;
    }

    // Head and shoulders, in a preview scene so nothing lands in the open one, on the window's own colour. A creature painted with
    // LocalSpawn keeps its painting instead.
    private static Sprite Portrait(MonsterDefinition creature)
    {
        Sprite painted = PaintedPortrait(creature);
        if (painted != null)
        {
            return painted;
        }

        string path = $"{Portraits}{creature.name}.png";
        Scene scene = EditorSceneManager.NewPreviewScene();
        var sheet = new Texture2D(PortraitSize, PortraitSize, TextureFormat.RGB24, false, false);
        var target = new RenderTexture(PortraitSize, PortraitSize, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        try
        {
            var sunObject = new GameObject("Sun");
            SceneManager.MoveGameObjectToScene(sunObject, scene);
            var sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 2.2f;
            sun.color = new Color(1f, 0.93f, 0.82f);
            sun.transform.rotation = Quaternion.Euler(38f, -40f, 0f);
            var fillObject = new GameObject("Fill");
            SceneManager.MoveGameObjectToScene(fillObject, scene);
            var fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.7f;
            fill.color = new Color(0.65f, 0.75f, 0.9f);
            fill.transform.rotation = Quaternion.Euler(20f, 150f, 0f);
            var cameraObject = new GameObject("Camera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.AddComponent<Camera>();
            camera.scene = scene;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.112f, 0.1f, 0.085f);
            camera.fieldOfView = 24f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 400f;
            camera.targetTexture = target;

            var body = (GameObject)PrefabUtility.InstantiatePrefab(creature.Prefab, scene);
            body.transform.rotation = Quaternion.Euler(0f, 150f, 0f);
            foreach (Canvas canvas in body.GetComponentsInChildren<Canvas>(true))
            {
                canvas.gameObject.SetActive(false);
            }

            // A standing pose rather than the T of the bind pose.
            var animator = body.GetComponentInChildren<Animator>();
            AnimationClip clip = animator != null ? StandingClip(animator.runtimeAnimatorController) : null;
            if (clip != null)
            {
                clip.SampleAnimation(animator.gameObject, clip.length * 0.25f);
            }

            var bounds = new Bounds();
            bool any = false;
            foreach (Renderer renderer in body.GetComponentsInChildren<Renderer>())
            {
                if (renderer is ParticleSystemRenderer || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!any)
                {
                    bounds = renderer.bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            float radius = Mathf.Max(0.3f, bounds.extents.magnitude);
            camera.transform.position = bounds.center + new Vector3(0f, radius * 0.2f, -radius * 3f);
            camera.transform.LookAt(bounds.center);
            camera.Render();
            RenderTexture.active = target;
            sheet.ReadPixels(new Rect(0, 0, PortraitSize, PortraitSize), 0, 0);
            sheet.Apply();
            RenderTexture.active = null;
            camera.targetTexture = null;
            File.WriteAllBytes(path, sheet.EncodeToPNG());
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
            Object.DestroyImmediate(sheet);
            Object.DestroyImmediate(target);
        }

        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = false;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static AnimationClip StandingClip(RuntimeAnimatorController runtime)
    {
        var over = runtime as AnimatorOverrideController;
        var controller = (over != null ? over.runtimeAnimatorController : runtime) as AnimatorController;
        if (controller == null)
        {
            return null;
        }

        AnimatorState[] states = controller.layers[0].stateMachine.states.Select(s => s.state).ToArray();
        AnimatorState locomotion = states.FirstOrDefault(s => s.name == "Locomotion") ?? states.FirstOrDefault();
        AnimationClip clip = locomotion != null ? FirstClip(locomotion.motion) : null;
        return over != null && clip != null ? over[clip] : clip;
    }

    private static AnimationClip FirstClip(Motion motion)
    {
        if (motion is AnimationClip clip)
        {
            return clip;
        }

        if (motion is BlendTree tree)
        {
            foreach (ChildMotion child in tree.children)
            {
                AnimationClip inner = FirstClip(child.motion);
                if (inner != null)
                {
                    return inner;
                }
            }
        }

        return null;
    }
}
