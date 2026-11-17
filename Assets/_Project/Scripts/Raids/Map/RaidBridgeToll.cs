using UnityEngine;

/// <summary>
/// Where a toll is asked on a generated valley's bridge (Troll Valley's rule, Docs/RaidIdentityResearch_2026-11-17.md §3.1: "pay or
/// fight"): RaidMapGenerator puts one at the near end of every bridge but the boss's, and when the raid begins RaidDirector stands the
/// valley's toll event there for the whole raid (RaidEventDirector.BeginAt), as a rumour's event stands where it was promised. Pay, answer
/// the riddle, fight, or ford the river somewhere else.
/// </summary>
public sealed class RaidBridgeToll : MonoBehaviour
{
    public RaidEventDefinition Event { get; private set; }

    public void Bind(RaidEventDefinition toll) => Event = toll;
}
