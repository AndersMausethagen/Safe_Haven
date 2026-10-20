# Safe Haven – TODO

Open work and ideas. Tick items off (`[x]`) or delete them when done. Architecture notes for each system live in
[ARCHITECTURE.md](../Assets/_Project/Scripts/ARCHITECTURE.md).

## Raid maps (Misty Woods forest terrain)

### Art
- [ ] Replace the placeholder forest kit (spruces, pine, dead spruce, boulders, log in `Assets/_Project/Art/Forest`)
      with real tree and rock art. Swap the prefab mesh/material, then "Raid Map" › Bake Map. No layout changes needed.

### Designer tools
- [ ] Add a "Raid Maps" page to the designer window: preview of the marker image over the height map, legend,
      site counts per group and a Bake button.

### Content
- [ ] Paint a second layout for Misty Woods (new Height/Mask/Markers PNGs + `RaidMapDefinition`). With the four
      rotations this gives eight variants.
- [ ] More event-site variety (new prefab in `Prefabs/Raids/Events` + an entry in `MistyWoods_Events`).
- [x] Dedicated wolf pack prefab for enemy camps (one howler + one or two packmates), with staggered attacks.

### Polish and fixes
- [ ] Ground event prefab children individually; pieces sit at local y = 0 and may float on slopes.
- [ ] Check that the Campfire in the Abandoned Camp event doesn't carry Safe Haven station/building logic into raids.

### Verification still to do
- [ ] Play-test the 180° and 270° map rotations (only 0° in the editor and 90° in play mode are verified).
- [ ] Play a full raid from the Safe Haven portal through the loading screen into the rotated map and back.

## Materials as items (weight and carrying)

- [ ] Soft encumbrance: above ~80% carry weight slow movement and raise stamina cost, instead of only a hard cap at 100%.
- [ ] Haul options for raids: a haul sack (extra weight, but slows you or makes noise), and later a pack animal, cart or wheelbarrow (GDD §12.4).
- [ ] Refine at home pays off: tune item value per weight so ingots/planks/blocks are clearly better to carry than raw ore/logs.
- [ ] Quick-deposit: "Deposit all materials" button at the stash, and stations pulling from storage (already via MaterialPool).
- [ ] Decide essence loss on a failed raid (e.g. lose the essences gathered this raid, keep banked ones).
- [ ] Ground drop prefabs for materials (a generic sack/bundle prefab) so dropping a material spawns something to pick up.
- [ ] Per-material stack size tuning after playtest (hides 20, trollstone 10, the rest 50).
- [ ] Show the weight of a full stack in the raid preparation window, so the loadout's weight is visible before leaving.
