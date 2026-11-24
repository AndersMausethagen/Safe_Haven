# Improving the skills: research (2026-11-24)

The user: "research if there are ways we can improve skills". Nothing here is built. It follows Docs/CombatSkillsResearch_2026-11-24.md
(the V Rising-style plan, slices 1-10 built) and looks at what the skills still lack, against how other action RPGs make abilities deep
and satisfying. Sources are wikis and guides unless marked; claims I couldn't verify are marked.

## Where the skills stand

Built: 9 abilities on the 1-5 bar plus the weapon's art in slot 1, each with its own animation and VFX Graph look; school statuses
(Frostbite, Frozen and Shatter, Burning, Rend, Sundered, Cowed) shown on what bears them; ground left behind (embers, rime); a war cry;
teachers for every ability; co-op showing all of it.

What the code shows is missing or inconsistent:

1. **Every ability is a spell.** `PlayerSkills` hits with `HitTechnique.Spell` and multiplies by Spell Damage (Runecraft), so Trollsbane
   Wrath, Whirling Steel, Wolf's Lunge and Mountain Leap train Runecraft, grow with spell gear, and can't Shatter a frozen foe though
   they are the heaviest blows in the game. The physical abilities should be Melee/Heavy and scale with the weapon side.
2. **Ranks are numbers only.** Deepening at the altar adds flat power and a cooldown cut (`EssenceSettings.PowerAt/CooldownCutAt`). No
   choice, so two players' Frost Runes are the same Frost Rune.
3. **A skill pressed while busy is dropped.** Weapon swings buffer one press (`PlayerCombat.Input`); abilities answer "busy" and forget it.
4. **No sound yet** (slots exist; takes wait on the picks page).
5. **One shape of delivery.** Every ability is an arc around the caster (a lance is a narrow arc); nothing travels, channels or is
   placed at the cursor. Aim is the facing at the cast, shown by the area mark during the wind-up, not held and released.
6. **Statuses rarely talk to each other.** Shatter is the only consume. Burning, Rend, Sundered and Cowed each stand alone.
7. **Weapon arts are one press.** Slot 1 has no follow-up or charge.

## What other games do

- **V Rising:** one Travel (Veil) slot, two spells, one ultimate, two weapon skills; six schools, one signature status each. *Spell jewels*
  add modifiers to one spell, many of them *consuming* a status for a payoff (consume Chill to Freeze, a shield on hitting a Chilled
  foe). Veils make the next basic attack apply the school's status. School passives (18, three tiers each) since 1.0.
  ([TechRaptor](https://techraptor.net/gaming/guides/v-rising-spells-guide), [itemlevel](https://itemlevel.net/v-rising-all-passives-guide-explained-update-1-0/),
  [gameplay.tips](https://gameplay.tips/guides/v-rising-list-of-all-skill-jewels.html); jewel tier counts conflict between sources.)
- **Lost Ark tripods:** three tiers per skill, one pick each; tier 1 small comfort (speed, cost), tier 2 damage or area, tier 3 two
  options that redefine the skill; swaps free. ([Fextralife](https://lostark.wiki.fextralife.com/Tripods), [Maxroll](https://maxroll.gg/lost-ark/resources/lost-ark-skill-point-guide))
- **Diablo IV:** shared keywords (Vulnerable: +20% damage taken) that many skills apply and others pay out on; the 2026 tree gives
  each skill a major upgrade that changes its element or shape. ([Game8](https://game8.co/games/Diablo-4/archives/411006),
  [Xbox Wire](https://news.xbox.com/en-us/2026/04/22/diablo-4-skill-tree-overhaul/))
- **Last Epoch:** a skill you specialise gains levels by use, and each level a point in that skill's own tree, whose nodes can change its
  element or behaviour. ([Maxroll](https://maxroll.gg/last-epoch/resources/passives-and-skills))
- **Path of Exile 2:** support gems in a skill's own sockets; the weapon swap is invisible, each skill remembers its weapon set.
  ([Maxroll](https://maxroll.gg/poe2/resources/skills-in-path-of-exile-2))
- **Hades / Hades II:** one boon per slot (attack, special, cast, dash), *duo boons* for holding two gods' boons at once (28 in Hades);
  Hades II holds a move to channel a stronger version for magick. ([Fextralife](https://hades.wiki.fextralife.com/Boons),
  [Prima](https://primagames.com/tips/hades-what-are-duo-boons-how-get-duo-boons))
- **Grim Dawn:** *transmuters*, sidegrades that change a skill's role without adding power ("do not represent an obvious gain in power",
  [Crate](https://forums.crateentertainment.com/t/grim-misadventure-49-skills-be-changin/28601)); devotion powers bound to a skill
  that level by use.
- **Elden Ring:** weapon arts with follow-up presses (Double Slash: 6 FP, then 3 FP per follow-up, up to two) and charged arts.
  ([Fextralife](https://eldenring.wiki.fextralife.com/Double+Slash))
- **Feel:** buffer a press during recovery, cancel only in marked windows ([Game Developer](https://www.gamedeveloper.com/game-platforms/combat-canceled-i-god-of-war-i-action-game-design));
  hit-stop grows with the blow up to a cap ([Sakurai](https://sourcegaming.info/2015/11/11/thoughts-on-hitstop-sakurais-famitsu-column-vol-490-1/));
  "quick cast with indicator": hold to see the area, release to cast at the cursor ([LoL wiki](https://wiki.leagueoflegends.com/en-us/Controls_and_Hotkeys));
  on a pad, line skills follow the right stick, not the nearest foe (Diablo IV's auto-target is criticised for this,
  [Dot Esports](https://dotesports.com/?p=922953)); a skill's sound in layers: cast, travel, impact, tail.

## Recommendations, most worth it first

Each with its cost for us. Balance-touching ones say so: they want playtests, as the user asked to keep balance work until there's data.

1. **Physical abilities hit as physical** (fix 1). Trollsbane Wrath, Mountain Leap, Whirling Steel and Wolf's Lunge as Melee and Heavy
   blows that scale with the weapon and train Melee; then they Shatter as heavies do. *Low cost; a bug in kind more than a feature.*
2. **Buffer a skill press** (fix 3). A press up to ~0.2 s before the current cast or swing ends casts then, as swings already do. *Low.*
3. **Choices at the altar, tripod-style** (fix 2). Rank II: one of three small tweaks (cost, cooldown, reach); rank IV: one of three that
   change behaviour; the top rank: one of two that redefine it. Free to swap at the altar. Start with three abilities (Frost Rune, Wolf's
   Lunge, War Cry), about 8 options each. *Medium; data-driven modifiers on SkillDefinition; touches balance.*
4. **Status consumers as altar options** (fix 6, V Rising's jewels). Examples: Frost Rune "on a Frozen foe, the lance shatters it";
   Wolf's Lunge "biting a bleeding foe heals you"; Trollsbane Wrath "doubles Sundered's time"; War Cry "Cowed foes also bleed out
   faster". One consumer per ability. *Low to medium; reuses ConsumedBy and the statuses.*
5. **A dodge that sets up the next blow** (V Rising's veils). After a dodge, the next swing lays the school's status of the last ability
   cast (Frostbite, Burning, Rend, Sundered, Cowed). Gives every fight a rhythm of dodge, hit, skill. *Low; touches balance.*
6. **Hit-stop and camera kick by skill size.** Light, heavy and ultimate tiers in each skill's look (shake exists; hit-stop and a kick
   away from the aim don't), capped. *Low.*
7. **Hold to aim, release to cast** (fix 5). Holding the key shows the area mark at the cursor; releasing casts there; a tap casts as
   now. On a pad, the right stick aims line skills. *Medium.*
8. **Shared keywords.** Say Sundered and Cowed as "takes more" and "deals less" on the card, and let options pay out on them ("+25%
   against Sundered"), so schools combine without designing every pair. *Low.*
9. **Two or three cross-school reactions** (Hades' duo boons): Ember on a Frozen foe bursts in steam, Rend on a Burning foe cauterises
   (a stagger). Taught by mentors, found in the journal. *Medium; only a few pairs.*
10. **New deliveries:** a thrown or travelling skill (a spear, a boulder; OverhandThrow is unused) and one channel (Spell_*_Channel loops
   are unused). *Medium; new code paths.*
11. **Weapon-art follow-ups** (Elden Ring): a second press within a window continues the art for a little Focus. *Medium; needs clips.*
12. **Use-based mastery** (Last Epoch): an ability's altar ranks open as it is used, not only with essence. *Low to medium; touches
   progression pacing.*

Plus the four sound layers per skill once the picks are in (cast, travel, impact, tail), in the slots that exist.

## Questions for the user

- Physical abilities as physical (1) and the buffer (2) look like plain fixes: build them now?
- Altar choices (3, 4): start with the three abilities named, and is swapping at the altar free?
- The dodge that sets up a blow (5) and hold-to-aim (7): wanted, given they change how every fight is played?
- Which of 9-12 to keep for later?
