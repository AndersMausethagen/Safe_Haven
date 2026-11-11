using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// A practice duel between a companion and the player (CompanionSpar): it waits until one of them yields. Neither can die.
/// Outputs: the player won, the companion won (both are healed either way). For lessons like Eirik's at the morning camp.
/// </summary>
[Serializable]
[WaitsForThePlayer]
public sealed class SparNode : StoryNode
{
    [Tooltip("Who duels the player: a companion with a melee attack.")]
    public Companion companion;
    [Tooltip("The share of health at which either yields: neither can be brought lower, so nobody dies.")]
    [Range(0.05f, 0.9f)] public float yieldAt = 0.25f;

    public override string Title => "Spar";
    public override string Category => "World";
    public override bool IsFight => true;
    public override int OutputCount => 2;
    public override string OutputName(int index) => index == 0 ? "Player Won" : "Companion Won";

    public override IEnumerator Run(StoryContext context)
    {
        Player player = context.Player;
        CompanionSpar spar = CompanionSpar.Begin(companion, player != null ? player.Health : null, yieldAt);
        if (spar == null)
        {
            yield break;
        }

        while (spar != null && !spar.IsOver)
        {
            yield return null;
        }

        context.Output = spar != null && spar.Result == CompanionSpar.Outcome.CompanionWon ? 1 : 0;
    }

    public override string FindProblem() =>
        companion == null ? "choose the companion" :
        companion.Definition == null || companion.Definition.Attack == null ? "the companion has no melee attack" : null;
}
