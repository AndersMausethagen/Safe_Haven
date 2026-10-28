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
    /// <summary>Raised when the attack button is let go (a held press is the heavy attack).</summary>
    public event Action AttackReleased;
    public event Action ShieldBashRequested;
    /// <summary>Raised when the guard button goes down (true) or up (false).</summary>
    public event Action<bool> GuardRequested;
    public event Action DodgeRequested;
    /// <summary>Raised with the skill bar slot (0-4) the player asked to cast.</summary>
    public event Action<int> SkillRequested;
    /// <summary>Raised when the player asks for the next kind of arrow (a bow's quiver).</summary>
    public event Action ArrowCycleRequested;

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

    public void ReleaseAttack()
    {
        AttackReleased?.Invoke();
    }

    /// <summary>Raising the guard is refused while busy with an interaction; lowering it always goes through.</summary>
    public void RequestGuard(bool raised)
    {
        if (!raised || CurrentState != PlayerState.Interacting)
        {
            GuardRequested?.Invoke(raised);
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

    public void RequestArrowCycle() => ArrowCycleRequested?.Invoke();

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
