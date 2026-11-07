using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Where worn gear is repaired (GDD §13.4): opens a menu listing every damaged tool, weapon and backpack in the
/// bag and safe pocket, with what each repair costs (see Repairs and DurabilitySettings). Repairs are paid from what is
/// carried and stored. On a crafting station (the Construction Table) it adds "Repair gear" to the station's menu instead
/// of opening on use.
/// <para>
/// Where it belongs: on the Construction Table building prefab (the repair station in DurabilitySettings), next to an
/// Interactable that doesn't deactivate.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Interactable))]
public sealed class RepairBench : MonoBehaviour, IStationMenuExtension
{
    [SerializeField, Min(1f)] private float menuRange = 6f;

    private Interactable interactable;
    private CraftingStation station;

    private void Awake()
    {
        interactable = GetComponent<Interactable>();
        station = GetComponent<CraftingStation>();
    }

    private void OnEnable()
    {
        if (station == null)
        {
            interactable.Completed.AddListener(OnUsed);
        }
    }

    private void OnDisable()
    {
        interactable.Completed.RemoveListener(OnUsed);
    }

    private void OnUsed()
    {
        OpenRepairs(interactable.Interactor);
    }

    public void AddActions(MenuPage page, GameObject user)
    {
        page.Add(new MenuAction("Repair gear…", "Mend worn tools, weapons and packs", true, () => OpenRepairs(user)));
    }

    private void OpenRepairs(GameObject user)
    {
        if (ActionMenuWindow.Current != null)
        {
            ActionMenuWindow.Current.Open(() => BuildPage(user), user, transform, menuRange);
        }
    }

    private MenuPage BuildPage(GameObject user)
    {
        var page = new MenuPage { Title = "Repair Gear" };
        ItemInventory inventory = user != null ? user.GetComponent<ItemInventory>() : null;
        PlayerCrafting crafting = user != null ? user.GetComponent<PlayerCrafting>() : null;
        if (inventory == null || crafting == null)
        {
            return page;
        }

        AddDamaged(page, user, inventory.Container, crafting.Recipes);
        AddDamaged(page, user, inventory.SafePocket, crafting.Recipes);
        PlayerEquipment equipment = user.GetComponent<PlayerEquipment>();
        if (equipment != null)
        {
            AddDamaged(page, user, equipment.Gear.Slots, crafting.Recipes);
        }

        page.Body = page.Actions.Count == 0
            ? "Everything you carry is in good shape. Gear only wears when a raid goes wrong."
            : "Gear brought home from a failed raid is worn. Repairs restore it to full.";
        if (station != null)
        {
            page.Add(new MenuAction("Back to the " + station.Type.DisplayName(), null, true, () => station.OpenMenu(user)));
        }

        return page;
    }

    private static void AddDamaged(MenuPage page, GameObject user, ItemContainer container, IReadOnlyList<RecipeDefinition> recipes)
    {
        if (container == null)
        {
            return;
        }

        for (int i = 0; i < container.SlotCount; i++)
        {
            ItemStack stack = container.GetSlot(i);
            if (!stack.IsDamaged)
            {
                continue;
            }

            int slot = i;
            RepairCost cost = Repairs.Cost(stack, Repairs.RecipeFor(stack.Item, recipes), DurabilitySettings.Current);
            string problem = Repairs.Check(user, container, slot, recipes);
            string detail = $"{stack.Durability:P0}  →  100%    {cost.Describe()}"
                + (problem != null ? $"   <color={UIStyle.Current.DangerHex}>{problem}</color>" : "");
            page.Add(new MenuAction($"Repair {stack.Item.DisplayName}", detail, problem == null,
                () => Repairs.TryRepair(user, container, slot, recipes)));
        }
    }
}
