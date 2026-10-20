using System;
using UnityEngine;

public enum PlayerState
{
    Free,
    Interacting
}

[DisallowMultipleComponent]
public sealed class PlayerStateMachine : MonoBehaviour
{
    public PlayerState CurrentState { get; private set; } = PlayerState.Free;
    public bool CanMove => CurrentState != PlayerState.Interacting;

    public event Action<PlayerState, PlayerState> StateChanged;
    public event Action InteractionRequested;
    public event Action AttackRequested;
    public event Action ShieldBashRequested;
    public event Action DodgeRequested;
    /// <summary>Raised with the skill bar slot (0-4) the player asked to cast.</summary>
    public event Action<int> SkillRequested;

    public bool BeginInteraction()
    {
        if (CurrentState == PlayerState.Interacting)
        {
            return false;
        }

        ChangeState(PlayerState.Interacting);
        return true;
    }

    public bool EndInteraction()
    {
        if (CurrentState != PlayerState.Interacting)
        {
            return false;
        }

        ChangeState(PlayerState.Free);
        return true;
    }

    public void RequestInteraction()
    {
        if (CurrentState != PlayerState.Interacting)
        {
            InteractionRequested?.Invoke();
        }
        else
        {
            WorkPressed?.Invoke();
        }
    }

    /// <summary>
    /// Raised when Interact or Attack is pressed during an interaction: the timing press of the gathering swing bar
    /// (PlayerGathering).
    /// </summary>
    public event Action WorkPressed;

    /// <summary>Attacks can't start while the player is busy with an interaction; the press goes to WorkPressed instead.</summary>
    public void RequestAttack()
    {
        if (CurrentState != PlayerState.Interacting)
        {
            AttackRequested?.Invoke();
        }
        else
        {
            WorkPressed?.Invoke();
        }
    }

    public void RequestShieldBash()
    {
        if (CurrentState != PlayerState.Interacting)
        {
            ShieldBashRequested?.Invoke();
        }
    }

    /// <summary>Skills, like attacks, can't be cast while the player is busy with an interaction.</summary>
    public void RequestSkill(int slot)
    {
        if (CurrentState != PlayerState.Interacting)
        {
            SkillRequested?.Invoke(slot);
        }
    }

    /// <summary>Dodging while working on something (gathering, crafting) stops the work first.</summary>
    public void RequestDodge()
    {
        if (CurrentState == PlayerState.Interacting)
        {
            EndInteraction();
        }

        DodgeRequested?.Invoke();
    }

    private void ChangeState(PlayerState nextState)
    {
        PlayerState previousState = CurrentState;
        CurrentState = nextState;
        StateChanged?.Invoke(previousState, nextState);
    }
}
