using UnityEngine;

/// <summary>
/// Where a toll is asked on a generated valley's bridge (Troll Valley's rule, Docs/RaidIdentityResearch_2026-11-17.md §3.1: "pay or
/// fight"): RaidMapGenerator puts one past the end of every bridge but the boss's that a raid comes to first along the trails, turned
/// to face the bridge. When the raid begins RaidDirector stands the valley's toll event there for the whole raid
/// (RaidEventDirector.Stand), in every game: not a rumour, and quiet until the player comes upon it. Pay, answer the riddle, fight, or
/// ford the river somewhere else.
/// </summary>
public sealed class RaidBridgeToll : MonoBehaviour
{
    public RaidEventDefinition Event { get; private set; }

    public void Bind(RaidEventDefinition toll) => Event = toll;
}
