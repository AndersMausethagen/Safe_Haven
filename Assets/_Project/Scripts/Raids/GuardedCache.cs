using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A camp's stash (Docs/RaidLayoutAnalysis_2026-11-26.md R4): it can't be searched while the creatures that stood round it when the
/// raid began are alive, so a camp is worth clearing and says why. Its guards are taken once, the creatures within Guard Reach a moment
/// after the raid's spawns are placed; one that wanders off still counts, and a roamer or a trail walker passing by never does. A camp
/// with nobody at the start has an open stash. Only on a friend's copy, whose creatures come from the host later, does a stash with
/// no guards look again, until it finds some.
/// <para>
/// Where it belongs: beside the LootCache and Interactable of a stash prefab, which the raid's camp stash list places at every camp
/// (Spawn Group › Every Site).
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Interactable))]
[OnAFriendsCopy(CopyRule.Same, "personal loot: each game guards its own stash with its own creatures")]
[InAFriendsHaven(HavenUse.NotInAHaven, "a raid camp's stash")]
public sealed class GuardedCache : MonoBehaviour, IInteractionCondition
{
    [Tooltip("Metres round the stash within which a creature at the raid's start is one of its guards.")]
    [SerializeField, Min(1f)] private float guardReach = 12f;

    private readonly List<Damageable> guards = new List<Damageable>();
    private bool taken;
    private float lookAgainAt;

    public float GuardReach => guardReach;

    /// <summary>How many of its guards are still alive.</summary>
    public int GuardsLeft
    {
        get
        {
            int left = 0;
            foreach (Damageable guard in guards)
            {
                if (guard != null && guard.IsAlive)
                {
                    left++;
                }
            }

            return left;
        }
    }

    private void Start()
    {
        TakeGuards();
    }

    public bool CanInteract(GameObject user, out string reason)
    {
        // Only a friend's copy looks again, whose creatures come from the host after its start; asked every frame while it is the
        // player's target, so at most once a second, and never once it has guards. Anywhere else a camp with nobody at the start is open,
        // whoever passes by later (review 2026-11-26: a walker or a chasing wolf made an empty camp's stash "guarded").
        if (!taken || (guards.Count == 0 && CreatureSource.Remote != null && Time.time >= lookAgainAt))
        {
            TakeGuards();
            lookAgainAt = Time.time + 1f;
        }

        int left = GuardsLeft;
        reason = left > 0 ? GuardedText(left) : null;
        return left == 0;
    }

    private static readonly string[] Texts = new string[8];

    /// <summary>What the player is told while guards live (made once per count: asked every frame). Pure, for tests.</summary>
    public static string GuardedText(int left)
    {
        if (left < Texts.Length && Texts[left] != null)
        {
            return Texts[left];
        }

        string text = left == 1 ? "Guarded: one of the camp still stands." : $"Guarded: {left} of the camp still stand.";
        if (left < Texts.Length)
        {
            Texts[left] = text;
        }

        return text;
    }

    private void TakeGuards()
    {
        taken = true;
        guards.Clear();
        // Sleeping creatures have their MonsterAI switched off, so they aren't in MonsterAI.Active: look for every one. A creature that
        // walks a round (a trail walker, a patrolling apex) only passes by.
        foreach (MonsterAI monster in FindObjectsByType<MonsterAI>(FindObjectsInactive.Exclude))
        {
            Vector3 flat = monster.transform.position - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude <= guardReach * guardReach && !monster.TryGetComponent(out MonsterPatrol _)
                && monster.TryGetComponent(out Damageable damageable) && damageable.IsAlive)
            {
                guards.Add(damageable);
            }
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        UnityEditor.Handles.color = new Color(1f, 0.4f, 0.3f, 0.6f);
        UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, guardReach);
    }
#endif
}
