using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Where things are broken down (Salvage): adds "Break down…" to its station's menu, listing everything in the bag and safe pocket
/// that would give something, with exactly what it gives (and what is lost with it: an enchantment, empowering) before it is broken.
/// One at a time, or a whole stack of junk at once. Gear you have on isn't listed: take it off first.
/// <para>
/// Where it belongs: on the Construction Table (the Workshop) building prefab, beside its RepairBench.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[InAFriendsHaven(HavenUse.Anyone, "breaks down the visitor's own things")]
public sealed class SalvageBench : MonoBehaviour, IStationMenuExtension
{
    [SerializeField, Min(1f)] private float menuRange = 6f;

    private CraftingStation station;

    private void Awake()
    {
        station = GetComponent<CraftingStation>();
    }

    public void AddActions(MenuPage page, GameObject user)
    {
        page.Add(new MenuAction("Break down…", "Junk finds and gear you don't need, into what they are made of", true, () => Open(user)));
    }

    private void Open(GameObject user)
    {
        if (ActionMenuWindow.Current != null)
        {
            ActionMenuWindow.Current.Open(() => BuildPage(user), user, transform, menuRange);
        }
    }

    private MenuPage BuildPage(GameObject user)
    {
        var page = new MenuPage { Title = "Break Down" };
        ItemInventory inventory = user != null ? user.GetComponent<ItemInventory>() : null;
        PlayerCrafting crafting = user != null ? user.GetComponent<PlayerCrafting>() : null;
        if (inventory == null || crafting == null)
        {
            return page;
        }

        Add(page, user, inventory.Container, crafting.Recipes);
        Add(page, user, inventory.SafePocket, crafting.Recipes);
        page.Body = page.Actions.Count == 0
            ? "Nothing you carry would give anything. Junk from raids (a broken sickle, barrow goods) and gear you don't need break down here."
            : "What each gives is shown before you break it. It goes to your bag, then the stash.";
        if (station != null)
        {
            page.Add(new MenuAction("Back to the " + station.Type.DisplayName(), null, true, () => station.OpenMenu(user)));
        }

        return page;
    }

    private static void Add(MenuPage page, GameObject user, ItemContainer container, IReadOnlyList<RecipeDefinition> recipes)
    {
        if (container == null)
        {
            return;
        }

        for (int i = 0; i < container.SlotCount; i++)
        {
            ItemStack stack = container.GetSlot(i);
            List<ItemAmount> yield = Salvage.YieldOf(stack, recipes, DurabilitySettings.Current);
            if (yield.Count == 0)
            {
                continue;
            }

            int slot = i;
            string lost = Salvage.LostWith(stack);
            string detail = "→  " + AmountText.Describe(yield)
                + (lost != null ? $"   <color={UIStyle.Current.DangerHex}>loses {lost}</color>" : "");
            page.Add(new MenuAction($"Break down {stack.Item.DisplayName}", detail, true,
                () => Salvage.TryBreakDown(user, container, slot, 1, recipes)));
            if (stack.Count > 1)
            {
                var all = new List<ItemAmount>();
                foreach (ItemAmount part in yield)
                {
                    all.Add(new ItemAmount(part.Item, part.Amount * stack.Count));
                }

                page.Add(new MenuAction($"Break down all {stack.Count} {stack.Item.DisplayName}", "→  " + AmountText.Describe(all), true,
                    () => Salvage.TryBreakDown(user, container, slot, stack.Count, recipes)));
            }
        }
    }
}
