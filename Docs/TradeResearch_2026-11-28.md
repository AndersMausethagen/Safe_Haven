# Trade: research and design (2026-11-28)

The user (2026-11-28): the Trading Post was the most basic system left; "do some research on how other games do it, e.g. RimWorld,
then implement what the research finds".

## 1. What Safe Haven had

A text menu at the Trading Post: four valuables sold at fixed coin, Fine/Masterwork gear at Value × 0.1/0.25 coin, fourteen
materials bought at fixed prices with a fixed stock that came back after every raid survived, gated by the Smithy's tier; and the
horse trade. No trader to meet, no prices that move, no standing, no bargaining, nothing the world asks for. GDD §17.3 asks for
"materials, favors, or cleverly negotiated bargains rather than a huge currency economy" and to "show the likely cost or risk of
bargain options".

## 2. What other games do (sources: wikis; community numbers are marked)

| Game | What we take from it |
|---|---|
| RimWorld ([Trade](https://rimworldwiki.com/wiki/Trade), [Quality](https://rimworldwiki.com/wiki/Quality), [Goodwill](https://rimworldwiki.com/wiki/Goodwill)) | Sell at 60% of value, buy at 140%, a skill narrows the gap (capped so you can never buy below what you can sell). Quality multiplies value (Normal 1, Excellent 1.5, Masterwork 2.5); worn things lose value steeply (full to 90% HP, ~50% at 60%, 10% at 50%). Trader *kinds* with their own stock groups and their own "will buy" lists. Barter in one window, silver only as the balance. Goodwill −100..100, +1 per 500 silver traded, gifts raise it with falling returns. Trade-request quests. Caravans visit a few times a season. |
| Escape from Tarkov ([GGRecon](https://www.ggrecon.com/guides/escape-from-tarkov-traders-level-up/)) | An extraction game's traders: 4 loyalty levels per trader, standing raised mostly by that trader's own tasks; each level opens stock, lowers prices and adds barter offers (items for items). Rare stock capped per restock. |
| Dwarf Fortress ([Trade](https://dwarffortresswiki.org/index.php/Trade)) | Item-for-item barter; merchants refuse a deal at a loss; elves refuse wood and are insulted by it; humans pay 2× for weapons; good deals bring bigger caravans. |
| Valheim (Haldor/Hildir) | Valuables exist to be sold; coin buys unique utility, not bulk materials; more stock after bosses; a trader's lost chests found in the world open her stock. |
| Kenshi / Bannerlord (community) | Prices per town 75-125%; selling floods the local price. Scavenged gear sells low on purpose, or other income is pointless. |
| Slime Rancher (wiki) | Each unit sold pushes that item's price down (15 units ≈ −25%), recovering over days. |
| Project Gorgon ([designer's post](https://wiki.projectgorgon.com/wiki/Elder_Game:_Vendors!)) | A vendor's *purse cap* (how much it will buy) is gentler than wandering prices; better standing raises the cap. |
| Dark and Darker ([Affinity](https://darkanddarker.wiki.spellsandguns.com/Affinity)) | Affinity from a merchant's quests opens items at 25/50/75. |

The rule every source agrees on: no sell price may reach a buy price anywhere, or trade becomes a money loop.

## 3. What was built (the research agent's design, scaled to what fits now)

**Three traders**, each a `TraderDefinition` asset (Data/Trade), listed in `TradeSettings`:

| Trader | Comes | Buys | Sells |
|---|---|---|---|
| Knut the Pedlar | 75% of homecomings (always the first time the post stands) | materials, food, valuables; Fine/Masterwork gear (never plain) | the old fourteen offers by Smithy tier, plus everyday goods; the horse trade stays on the post's menu |
| Hallgerd the Tinker | every 3rd raid survived, once the Smithy stands | gear and tools (plain ones for scrap at half), ores, ingots, junk finds | tools, ingots, repair kits, packs, now and then a weapon of the Smithy's tier |
| The Hidden Folk | 20% of homecomings after the third (35% once Trusted); trade only after dark | the odd and the old: honey, porridge, mead, berries, mist-touched finds, valuables | raid charms and sigils, moss poultices, mist custard, fireflies, now and then moonsilver ore or a wisp pendant. **They refuse iron and coin** (folklore; Dwarf Fortress's elves) |

**Prices**, in *worth* (an item's Value; a coin is worth its own Value, 10):

- Selling to a trader: Value × quality (Plain 1, Fine 1.8, Masterwork 4.5, so Fine/Masterwork pay what they did) × condition (full
  from 90% durability, half at 50%, a quarter when broken) × how much of it they already have (×0.96 per unit sold to them, never
  below ×0.4, a third of it forgotten after each raid survived) × 1.5 if they want it this visit × the standing's sell share
  (0.55 / 0.6 / 0.67 / 0.75). Valuables sell at ×1.8 on top (about their whole Value at first, as before).
- Buying: Value × the standing's buy share (1.45 / 1.35 / 1.25 / 1.15), never below 1.05 × the most any trader would ever pay for
  it (TradeRules.SellCeiling): no loop, whatever the standing, the wants or a won haggle.

**The barter window** (`TradeWindow`, Hearthline kit): their goods and yours as picture cards with names (price and words on
hover), the deal as chips between them, the scales in the middle. Coin only fills the balance: what you take beyond what you give
is paid in coin; what you give beyond is paid back in coin (or, below a coin's worth and with the Hidden Folk, kept as a gift that
raises standing). Each trader's pack takes so much worth a visit (Project Gorgon's purse cap), more with standing.

**Standing** 0-100 per trader, tiers Stranger / Known / Trusted / Friend at 0 / 25 / 50 / 75: better shares, more stock, a bigger
pack. +1 per 120 worth traded, gifts +1 per 40 worth (100 above 50 standing; the Hidden Folk count gifts twice), +15 for an ask.

**Haggling** (once per visit, odds and cost shown first, GDD §17.3): 50% + 0.4% per standing (to 90%): 10% better prices this
visit, or −3 standing and no more haggling until they come again.

**Asks** (RimWorld's trade requests, Tarkov's trader tasks): each trader posts one ask (what, how many, the reward) that stands for
3 homecomings and is handed over at the window; the raid planner shows it beside the Haven's asks.

**Saved** in `SettlementSaveData.traders` through SaveMapper; old saves start with fresh traders.

## 4. Left for later (not built)

- A trader to see at the post (a cart, a person with a portrait): presentation, after a playtest.
- Hidden Folk bargains with a cost other than goods ("your next raid's clock starts a minute short", a resident Mist-touched):
  cross-system, worth doing once the window has been played.
- A trader's lost cart found on a raid opening a stock group (Valheim's Hildir); rescuing a trader in a raid event (+15).
- Calling the Pedlar early for standing.
