/// <summary>
/// How an interaction runs. Serialized: add new kinds at the end and never renumber.
/// </summary>
public enum InteractionKind
{
    /// <summary>Work the player does over time, with a work bar and animation: gathering, repairing, hammering.
    /// A Duration of 0 keeps going until Complete() is called (minigames, scaffolds).</summary>
    Work = 0,
    /// <summary>Done on the press itself, with no work bar, animation or player state: talking to someone, opening a
    /// chest, station or menu, lighting an exit.</summary>
    Use = 1,
}
