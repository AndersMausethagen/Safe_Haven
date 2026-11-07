using System;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
public sealed class GameStateManager : MonoBehaviour
{
    [SerializeField] private GameState initialState = GameState.Exploration;

    private bool isNotifying;

    /// <summary>The manager of the current scene, or null in scenes without one (the main menu, the raid results).</summary>
    public static GameStateManager Current { get; private set; }

    /// <summary>True while the player is in Combat mode. False where there is no manager, so it can be read from anywhere.</summary>
    public static bool InCombat => Current != null && Current.CurrentState == GameState.Combat;

    public GameState CurrentState { get; private set; }

    /// <summary>Raised after the mode changes. Arguments: previous state, new state.</summary>
    public event Action<GameState, GameState> GameStateChanged;

    private void Awake()
    {
        if (Current != null && Current != this)
        {
            Debug.LogError("Only one GameStateManager can exist at a time.", this);
            Destroy(this);
            return;
        }

        Current = this;
        CurrentState = initialState;
    }

    // Listeners unsubscribe through the reference they took when they subscribed, so none of them need this one to outlive the scene.
    private void OnDestroy()
    {
        if (Current == this)
        {
            Current = null;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Current = null;

    /// <summary>Returns true if the mode changed; false if already in that mode or called from a GameStateChanged handler.</summary>
    public bool TrySetState(GameState nextState)
    {
        if (isNotifying)
        {
            Debug.LogError("GameState cannot be changed from a GameStateChanged handler. Defer the request to a later frame.", this);
            return false;
        }

        if (nextState == CurrentState)
        {
            return false;
        }

        GameState previousState = CurrentState;
        CurrentState = nextState;
        NotifyListeners(previousState, nextState);
        return true;
    }

    // Each listener is invoked in isolation so one failing system cannot stop the others from reacting.
    private void NotifyListeners(GameState previousState, GameState nextState)
    {
        Action<GameState, GameState> handlers = GameStateChanged;
        if (handlers == null)
        {
            return;
        }

        isNotifying = true;
        foreach (Delegate handler in handlers.GetInvocationList())
        {
            try
            {
                ((Action<GameState, GameState>)handler)(previousState, nextState);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        isNotifying = false;
    }
}
