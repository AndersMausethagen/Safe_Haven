using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Crafting by hand takes time (the user, 2026-11-15: "Crafting from the inventory should not be instant... Reduce walking
/// speed while crafting"): each craft is paid when it is asked for, as at a station, and made after its seconds
/// (HandCraftingSettings), one after another; meanwhile the player walks at the settings' share of their speed. An attack
/// or a dodge stops it, and what wasn't finished is given back (GiveBack: to the bag, what doesn't fit at the feet); so does
/// leaving the scene. The item-use bar shows the craft under way. Added to the player by PlayerCrafting.
/// </summary>
[DisallowMultipleComponent]
public sealed class HandCraftQueue : MonoBehaviour
{
    private readonly List<RecipeDefinition> waiting = new List<RecipeDefinition>();
    private PlayerCrafting crafting;
    private PlayerMovement movement;
    private PlayerCombat combat;
    private PlayerDodge dodge;
    private RecipeDefinition current;
    private float elapsed;
    private float seconds;
    private bool toldNoRoom;

    /// <summary>The craft under way, or null.</summary>
    public RecipeDefinition Current => current;

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
    }

    /// <summary>How many of <paramref name="recipe"/> are under way or waiting.</summary>
    public int Waiting(RecipeDefinition recipe)
    {
        int count = current == recipe && recipe != null ? 1 : 0;
        foreach (RecipeDefinition queued in waiting)
        {
            if (queued == recipe)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>Pays for one craft and puts it in line; false (and the reason told) when it can't be made.</summary>
    public bool Add(RecipeDefinition recipe)
    {
        if (crafting == null || !crafting.TryPay(recipe))
        {
            return false;
        }

        waiting.Add(recipe);
        if (current == null)
        {
            StartNext();
        }

        return true;
    }

    /// <summary>Stops crafting and gives back what every unfinished craft cost.</summary>
    public void Stop(bool tell = true)
    {
        if (!IsBusy)
        {
            return;
        }

        if (current != null)
        {
            crafting.GiveBack(current);
        }

        foreach (RecipeDefinition queued in waiting)
        {
            crafting.GiveBack(queued);
        }

        if (tell)
        {
            GameMessages.Post("You stop crafting; the materials are back in your bag.", MessageKind.Info);
        }

        current = null;
        waiting.Clear();
        toldNoRoom = false;
        Slow(false);
    }

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
        seconds = HandCraftingSettings.Current.SecondsFor(current);
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
            if (!crafting.HasRoomFor(current))
            {
                if (!toldNoRoom)
                {
                    toldNoRoom = true;
                    GameMessages.Post($"No room in the bag for the {current.OutputName}: make room and it is done.", MessageKind.Warning);
                }

                elapsed = seconds;
                return;
            }

            toldNoRoom = false;
            RecipeDefinition done = current;
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

    private void OnDisable()
    {
        Stop(false);
    }
}
