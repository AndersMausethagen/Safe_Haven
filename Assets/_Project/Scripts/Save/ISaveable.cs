/// <summary>
/// A component whose state belongs in the save: a building's health, a gathering node's regrowth timer. Put a
/// SaveableEntity on the same GameObject and it picks up every ISaveable there automatically. State is a short string,
/// usually JsonUtility of a small [Serializable] struct, so each component owns its own format.
/// </summary>
public interface ISaveable
{
    /// <summary>Names this component's state within its entity. Keep it stable: saves look it up by this key.</summary>
    string SaveKey { get; }

    string CaptureState();

    /// <summary>Called once after every Awake in the scene has run, with what CaptureState returned earlier.</summary>
    void RestoreState(string state);
}
