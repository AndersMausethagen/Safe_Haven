using UnityEngine;

/// <summary>Starts home quests only at home, preserving legacy saves and direct Editor starts.</summary>
public sealed class PrologueHomecoming : MonoBehaviour
{
    [SerializeField] private PrologueDefinition prologue;
    [SerializeField, RaisedByGame] private GameEventDefinition homeEnteredEvent;
    [SerializeField, RaisedByGame] private GameEventDefinition reunitedTrailEvent;

    private void Start() => SceneTransition.WhenReady(Arrive);

    private void Arrive()
    {
        if (this == null || Player.Current == null) return;
        GameEvents.Raise(homeEnteredEvent);
        if (prologue == null || !GameSession.Quests.HasHappened(prologue.FinishedEvent) ||
            GameSession.Quests.HasHappened(reunitedTrailEvent)) return;
        GameEvents.Raise(reunitedTrailEvent);
        GameMessages.Post(prologue.HomecomingText);
        if (!SaveGameService.TrySave(out string saveProblem))
            GameMessages.Post("Your arrival could not be saved: " + saveProblem, MessageKind.Warning);
    }
}
