using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Crafting by hand takes time (the user, 2026-11-15: "Crafting from the inventory should not be instant... Reduce walking
/// speed while crafting"): each craft is paid when it is asked for, as at a station, and made after its seconds
/// (HandCraftingSettings), one after another; meanwhile the player walks at the settings' share of their speed. An attack
/// or a dodge stops it, and what wasn't finished is given back where it was paid from (MaterialPool.Payment: the bag, the
/// stash; what doesn't fit at the feet); so does setting out on a raid, coming home from one and leaving the scene. A save
/// written at home keeps the queue, paid, and loading it puts it back (CaptureState, Restore). The item-use bar shows the
/// craft under way. Added to the player by PlayerCrafting.
/// </summary>
[DisallowMultipleComponent]
public sealed class HandCraftQueue : MonoBehaviour, IPlayerActivity
{
    // One paid craft, with what it cost from where.
    private sealed class Craft
    {
        public RecipeDefinition Recipe;
        public MaterialPool.Payment Paid;
    }

    private readonly List<Craft> waiting = new List<Craft>();
    private PlayerCrafting crafting;
    private PlayerMovement movement;
    private PlayerCombat combat;
    private PlayerDodge dodge;
    private Damageable health;
    private Craft current;
    private float elapsed;
    private float seconds;
    private bool toldNoRoom;

    /// <summary>The craft under way, or null.</summary>
    public RecipeDefinition Current => current?.Recipe;

    /// <summary>How far the craft under way has come, 0-1.</summary>
    public float Progress => current != null && seconds > 0f ? Mathf.Clamp01(elapsed / seconds) : 0f;

    /// <summary>Crafts asked for after the one under way.</summary>
    public int Queued => waiting.Count;

    public bool IsBusy => current != null || waiting.Count > 0;

    public static HandCraftQueue For(GameObject player)
    {
        if (player == null)
        {
            return null;
        }

        return player.TryGetComponent(out HandCraftQueue queue) ? queue : player.AddComponent<HandCraftQueue>();
    }

    private void Awake()
    {
        crafting = GetComponent<PlayerCrafting>();
        movement = GetComponent<PlayerMovement>();
        combat = GetComponent<PlayerCombat>();
        dodge = GetComponent<PlayerDodge>();
        health = GetComponent<Damageable>();
    }

    /// <summary>How many of <paramref name="recipe"/> are under way or waiting.</summary>
    public int Waiting(RecipeDefinition recipe)
    {
        int count = current != null && current.Recipe == recipe && recipe != null ? 1 : 0;
        foreach (Craft queued in waiting)
        {
            if (queued.Recipe == recipe)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>Pays for one craft and puts it in line; false (and the reason told) when it can't be made.</summary>
    public bool Add(RecipeDefinition recipe)
    {
        if (crafting == null || !crafting.TryPay(recipe, out MaterialPool.Payment paid))
        {
            return false;
        }

        waiting.Add(new Craft { Recipe = recipe, Paid = paid });
        if (current == null)
        {
            StartNext();
        }

        return true;
    }

    /// <summary>Stops crafting and gives back what every unfinished craft cost, where it was paid from.</summary>
    public void Stop(bool tell = true)
    {
        if (!IsBusy)
        {
            return;
        }

        if (current != null)
        {
            GiveBack(current);
        }

        foreach (Craft queued in waiting)
        {
            GiveBack(queued);
        }

        if (tell)
        {
            GameMessages.Post("You stop crafting; the materials are back.", MessageKind.Info);
        }

        current = null;
        waiting.Clear();
        toldNoRoom = false;
        Slow(false);
    }

    private void GiveBack(Craft craft)
    {
        if (crafting == null)
        {
            return;
        }

        if (craft.Paid != null)
        {
            crafting.GiveBack(craft.Paid);
        }
        else
        {
            crafting.GiveBack(craft.Recipe);
        }
    }

    // ----- Saving (a save written at home: SaveGameService.Capture with keepCrafts) -----

    /// <summary>The queue as a save keeps it: every craft in order, the first under way, each with what it was paid from where.
    /// Null when nothing is being made.</summary>
    public HandCraftSaveData CaptureState()
    {
        if (!IsBusy)
        {
            return null;
        }

        var saved = new HandCraftSaveData { elapsed = current != null ? elapsed : 0f };
        if (current != null)
        {
            saved.crafts.Add(ToSaved(current));
        }

        foreach (Craft queued in waiting)
        {
            saved.crafts.Add(ToSaved(queued));
        }

        return saved;
    }

    /// <summary>Puts a saved queue back, paid already: the first craft goes on from where it was.</summary>
    public void Restore(HandCraftSaveData saved, IContentLookup content)
    {
        current = null;
        waiting.Clear();
        if (saved == null || content == null)
        {
            return;
        }

        foreach (HandCraftEntrySaveData entry in saved.crafts)
        {
            var craft = new Craft { Recipe = content.FindRecipe(entry.recipe), Paid = FromSaved(entry, content) };
            if (craft.Recipe == null)
            {
                // Gone from the game since: what it cost comes back.
                GiveBack(craft);
                continue;
            }

            waiting.Add(craft);
        }

        StartNext();
        if (current != null)
        {
            elapsed = Mathf.Clamp(saved.elapsed, 0f, seconds);
        }
    }

    private static HandCraftEntrySaveData ToSaved(Craft craft)
    {
        var entry = new HandCraftEntrySaveData { recipe = craft.Recipe != null ? craft.Recipe.Id : "" };
        MaterialPool.Payment paid = craft.Paid;
        if (paid == null)
        {
            return entry;
        }

        foreach (ResourceAmount amount in paid.Carried) entry.carried.Add(new AmountSaveData { id = amount.Resource.Id, amount = amount.Amount });
        foreach (ResourceAmount amount in paid.Stored) entry.stored.Add(new AmountSaveData { id = amount.Resource.Id, amount = amount.Amount });
        foreach (ItemAmount amount in paid.FromBag) entry.fromBag.Add(new AmountSaveData { id = amount.Item.Id, amount = amount.Amount });
        foreach (ItemAmount amount in paid.FromStore) entry.fromStore.Add(new AmountSaveData { id = amount.Item.Id, amount = amount.Amount });
        return entry;
    }

    private static MaterialPool.Payment FromSaved(HandCraftEntrySaveData entry, IContentLookup content)
    {
        var paid = new MaterialPool.Payment();
        ReadResources(entry.carried, paid.Carried, content);
        ReadResources(entry.stored, paid.Stored, content);
        ReadItems(entry.fromBag, paid.FromBag, content);
        ReadItems(entry.fromStore, paid.FromStore, content);
        return paid;
    }

    private static void ReadResources(List<AmountSaveData> saved, List<ResourceAmount> into, IContentLookup content)
    {
        foreach (AmountSaveData amount in saved ?? new List<AmountSaveData>())
        {
            ResourceDefinition resource = content.FindResource(amount.id);
            if (resource != null && amount.amount > 0)
            {
                into.Add(new ResourceAmount(resource, amount.amount));
            }
        }
    }

    private static void ReadItems(List<AmountSaveData> saved, List<ItemAmount> into, IContentLookup content)
    {
        foreach (AmountSaveData amount in saved ?? new List<AmountSaveData>())
        {
            ItemDefinition item = content.FindItem(amount.id);
            if (item != null && amount.amount > 0)
            {
                into.Add(new ItemAmount(item, amount.amount));
            }
        }
    }

    // ----- Making -----

    private void StartNext()
    {
        current = null;
        if (waiting.Count == 0)
        {
            Slow(false);
            return;
        }

        current = waiting[0];
        waiting.RemoveAt(0);
        elapsed = 0f;
        seconds = HandCraftingSettings.Current.SecondsFor(current.Recipe);
        Slow(true);
    }

    private void Update()
    {
        if (current == null)
        {
            return;
        }

        // Hands busy with a fight: the work is put down.
        if ((combat != null && combat.IsAttacking) || (dodge != null && dodge.IsDodging))
        {
            Stop();
            return;
        }

        elapsed += Time.deltaTime;
        if (elapsed >= seconds)
        {
            // Made into a full bag it would be lost (the bag was checked when it was paid, not for what was still waiting):
            // it waits, finished, for room. An attack or a dodge still gives the materials back.
            if (!crafting.HasRoomFor(current.Recipe))
            {
                if (!toldNoRoom)
                {
                    toldNoRoom = true;
                    GameMessages.Post($"No room in the bag for the {current.Recipe.OutputName}: make room and it is done.", MessageKind.Warning);
                }

                elapsed = seconds;
                return;
            }

            toldNoRoom = false;
            RecipeDefinition done = current.Recipe;
            current = null;
            crafting.Finish(done);
            StartNext();
        }
    }

    private void Slow(bool slow)
    {
        if (movement != null)
        {
            movement.SetSpeedScale(this, slow ? HandCraftingSettings.Current.WalkShare : 1f);
        }
    }

    private void OnEnable()
    {
        PlayerInterrupts.For(gameObject).Add(this);
    }

    /// <summary>Death alone ends it (PlayerInterrupts tells every activity of a death).</summary>
    public PlayerInterrupt EndsOn => PlayerInterrupt.None;

    // Fallen: nothing is made by the dead (review 2026-11-16); what it cost goes back with the rest of the bag.
    public void Interrupt(PlayerInterrupt reason) => Stop(false);

    private void OnDisable()
    {
        if (TryGetComponent(out PlayerInterrupts interrupts))
        {
            interrupts.Remove(this);
        }

        // Leaving the scene: every way out that keeps the player (a raid, home, a friend's Haven, a save) has stopped or saved the
        // crafts before the player was copied. A refund now would land in a bag already copied, or in the stash of the game a
        // load has put in place (paid for twice).
        if (SceneLoader.IsLeaving)
        {
            current = null;
            waiting.Clear();
            Slow(false);
            return;
        }

        Stop(false);
    }
}
