using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

/// <summary>
/// Who holds a creature's body still (Docs/BugPatterns.md, ninth round), as PlayerControl holds the player's controls. A system that
/// stops a creature holds the parts it stops in its own name and lets go of its own hold only; a part comes back once nobody holds it.
/// Before, eight systems switched the brain (MonsterAI) and the feet (NavMeshAgent) off and on themselves, each guessing whether it was
/// theirs to switch back: a creature pushed into a ravine that the fall didn't kill stood at the bottom with its brain off for good, as a
/// boss stayed frozen in the fifth round. SharedStateTests fails on a direct write to a creature's brain or feet anywhere else.
/// <para>
/// Letting go turns back on only what that holder held and nobody else still holds: a part switched off some other way (a prefab
/// authored that way) is left as it is. Feet come back on the navmesh where they stand (a body that fell or was moved meanwhile is put
/// on the nearest point), so an agent never wakes off the mesh.
/// </para>
/// </summary>
[DisallowMultipleComponent]
public sealed class CreatureHold : MonoBehaviour
{
    /// <summary>The parts of a creature a hold can stop.</summary>
    [Flags]
    public enum Part
    {
        None = 0,
        /// <summary>Its thinking (MonsterAI).</summary>
        Brain = 1,
        /// <summary>Its path finding (NavMeshAgent).</summary>
        Feet = 2,
        /// <summary>Its animation (Animator, MonsterAnimationDriver): a sleeper's.</summary>
        Body = 4,
        All = Brain | Feet | Body,
    }

    // How far a body may stand from the navmesh to be put back on it as its feet come back.
    private const float BackOnTheMesh = 6f;

    private readonly Dictionary<object, Part> holders = new Dictionary<object, Part>();
    private readonly List<object> gone = new List<object>();
    private MonsterAI brain;
    private NavMeshAgent feet;
    private Animator animator;
    private MonsterAnimationDriver driver;
    private bool found;

    /// <summary>Stops <paramref name="parts"/> of <paramref name="creature"/> in <paramref name="owner"/>'s name (a component, or a static
    /// key object of a static class). Holding again adds to what the owner holds.</summary>
    public static void Hold(Component creature, object owner, Part parts)
    {
        if (creature == null || owner == null || parts == Part.None)
        {
            return;
        }

        CreatureHold hold = creature.TryGetComponent(out CreatureHold existing) ? existing : creature.gameObject.AddComponent<CreatureHold>();
        hold.Change(owner, parts);
    }

    /// <summary>
    /// Lets go of <paramref name="owner"/>'s hold: what it held comes back once nobody else holds it. <paramref name="alsoWake"/> turns on
    /// those parts too, held by nobody, though this owner never stopped them (the story's "a creature hunts", for one authored still).
    /// </summary>
    public static void Release(Component creature, object owner, Part alsoWake = Part.None)
    {
        if (creature == null || owner == null)
        {
            return;
        }

        if (creature.TryGetComponent(out CreatureHold hold))
        {
            hold.Change(owner, Part.None, alsoWake);
        }
        else if (alsoWake != Part.None)
        {
            creature.gameObject.AddComponent<CreatureHold>().Change(owner, Part.None, alsoWake);
        }
    }

    /// <summary>Whether anyone holds <paramref name="part"/> of <paramref name="creature"/>.</summary>
    public static bool IsHeld(Component creature, Part part) =>
        creature != null && creature.TryGetComponent(out CreatureHold hold) && (hold.HeldParts() & part) != 0;

    /// <summary>Whether <paramref name="owner"/> holds any part of <paramref name="creature"/>.</summary>
    public static bool HeldBy(Component creature, object owner) =>
        creature != null && owner != null && creature.TryGetComponent(out CreatureHold hold) && hold.holders.ContainsKey(owner);

    /// <summary>Who holds what now (for the leak watch and tests).</summary>
    public IReadOnlyDictionary<object, Part> Holders => holders;

    private void Change(object owner, Part parts, Part alsoWake = Part.None)
    {
        Find();
        Part before = HeldParts();
        Part freed = holders.TryGetValue(owner, out Part had) ? had : Part.None;
        if (parts == Part.None)
        {
            holders.Remove(owner);
        }
        else
        {
            holders[owner] = had | parts;
            freed = Part.None;
        }

        LetGoOfTheGone();
        Part now = HeldParts();
        // Stopped: what is newly held. Started: what this owner (or the wake asked for) frees and nobody else holds.
        Apply(now & ~before, (freed | alsoWake) & ~now);
    }

    private Part HeldParts()
    {
        Part held = Part.None;
        foreach (Part parts in holders.Values)
        {
            held |= parts;
        }

        return held;
    }

    // A holder that is gone (a destroyed coroutine's owner) holds nothing.
    private void LetGoOfTheGone()
    {
        gone.Clear();
        foreach (object owner in holders.Keys)
        {
            if (owner is Object unity && unity == null)
            {
                gone.Add(owner);
            }
        }

        foreach (object owner in gone)
        {
            holders.Remove(owner);
        }
    }

    private void Apply(Part stop, Part start)
    {
        // The brain goes off first and on last, so it never runs a frame with its feet switched off.
        if ((stop & Part.Brain) != 0 && brain != null)
        {
            brain.enabled = false;
        }

        if ((stop & Part.Feet) != 0 && feet != null)
        {
            feet.enabled = false;
        }

        if ((stop & Part.Body) != 0)
        {
            SetEnabled(driver, false);
            SetEnabled(animator, false);
        }

        if ((start & Part.Body) != 0)
        {
            SetEnabled(animator, true);
            SetEnabled(driver, true);
        }

        if ((start & Part.Feet) != 0 && feet != null && !feet.enabled)
        {
            PutOnTheMesh();
            feet.enabled = true;
        }

        if ((start & Part.Brain) != 0 && brain != null)
        {
            brain.enabled = true;
        }
    }

    // Where the body stands now, on the navmesh: an agent switched on off the mesh isn't placed ("Failed to create agent").
    private void PutOnTheMesh()
    {
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit ground, BackOnTheMesh, feet.areaMask))
        {
            transform.position = ground.position;
        }
    }

    private void Find()
    {
        if (found)
        {
            return;
        }

        found = true;
        brain = GetComponent<MonsterAI>();
        feet = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
        driver = GetComponentInChildren<MonsterAnimationDriver>();
    }

    private static void SetEnabled(Behaviour behaviour, bool value)
    {
        if (behaviour != null)
        {
            behaviour.enabled = value;
        }
    }

    // Holds by owners destroyed without letting go are dropped now and then (cheap: nothing to do while nobody holds it).
    private void Update()
    {
        if (holders.Count > 0 && Time.frameCount % 30 == 0)
        {
            Part before = HeldParts();
            LetGoOfTheGone();
            Part now = HeldParts();
            if (now != before)
            {
                Apply(Part.None, before & ~now);
            }
        }
    }
}
