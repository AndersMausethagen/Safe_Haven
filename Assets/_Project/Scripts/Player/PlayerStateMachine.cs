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
    /// <summary>In deep water (PlayerSwimming): no attacks, guard, skills, dodges or interactions, only swimming.</summary>
    public bool IsSwimming { get; private set; }
    /// <summary>At the oars (PlayerTransport): like swimming, no fighting; interacting means getting out.</summary>
    public bool IsAboard { get; private set; }
    private bool HandsTaken => IsSwimming || IsAboard;
    // In the saddle of a horse of their own the player may still swing a weapon and draw a bow (PlayerRiding); at the oars,
    // the cart's handles or a horse being broken in, not.
    private bool ArmsTaken => IsSwimming || IsAboard && !CanFightAboard;

    /// <summary>Set by PlayerRiding while the player rides a horse of their own: attacks and the bow's aim are let through.</summary>
    public bool CanFightAboard { get; set; }

    /// <summary>Raised when Interact is pressed at the oars.</summary>
    public event Action AboardInteraction;
    /// <summary>Raised when Interact is pressed while swimming: a boat in reach can be climbed into.</summary>
    public event Action SwimmingInteraction;

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
    /// <summary>A skill's key held with Aim Skills First on: its area shows until the key is let go (SkillReleased).</summary>
    public event Action<int> SkillAimed;
    public event Action<int> SkillReleased;
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
        if (IsAboard)
        {
            AboardInteraction?.Invoke();
            return;
        }

        if (IsSwimming)
        {
            SwimmingInteraction?.Invoke();
            return;
        }

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
        if (ArmsTaken)
        {
            return;
        }

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
        if (!raised || CurrentState != PlayerState.Interacting && !ArmsTaken)
        {
            GuardRequested?.Invoke(raised);
        }
    }

    public void RequestShieldBash()
    {
        if (CurrentState != PlayerState.Interacting && !HandsTaken)
        {
            ShieldBashRequested?.Invoke();
        }
    }

    /// <summary>Skills, like attacks, can't be cast while the player is busy with an interaction.</summary>
    public void RequestSkill(int slot)
    {
        if (CurrentState != PlayerState.Interacting && !HandsTaken)
        {
            SkillRequested?.Invoke(slot);
        }
    }

    /// <summary>Aim Skills First: the key went down; the skill waits for it to come up.</summary>
    public void AimSkill(int slot)
    {
        if (CurrentState != PlayerState.Interacting && !HandsTaken)
        {
            SkillAimed?.Invoke(slot);
        }
    }

    /// <summary>Aim Skills First: the key came up (PlayerSkills casts what was being aimed).</summary>
    public void ReleaseSkill(int slot) => SkillReleased?.Invoke(slot);

    public void RequestArrowCycle() => ArrowCycleRequested?.Invoke();

    /// <summary>Whether an attack may start now, as <see cref="RequestAttack"/> lets one: not at the oars, a cart's handles or swimming,
    /// nor in the middle of an interaction. The middle button's weapon art asks it (it shares the arrow key's request).</summary>
    public bool CanStartAttack => !ArmsTaken && CurrentState != PlayerState.Interacting;

    /// <summary>Dodging while working on something (gathering, crafting) stops the work first.</summary>
    public void RequestDodge()
    {
        if (HandsTaken)
        {
            return;
        }

        if (CurrentState == PlayerState.Interacting)
        {
            EndInteraction();
        }

        DodgeRequested?.Invoke();
    }

    /// <summary>Set by PlayerSwimming. Going into the water lowers a raised guard and stops any work.</summary>
    public void SetSwimming(bool swimming)
    {
        if (IsSwimming == swimming)
        {
            return;
        }

        IsSwimming = swimming;
        if (swimming)
        {
            GuardRequested?.Invoke(false);
            EndInteraction();
        }
    }

    /// <summary>Set by PlayerTransport. Sitting down at the oars lowers a raised guard and stops any work.</summary>
    public void SetAboard(bool aboard)
    {
        if (IsAboard == aboard)
        {
            return;
        }

        IsAboard = aboard;
        if (aboard)
        {
            GuardRequested?.Invoke(false);
            EndInteraction();
        }
    }

    private void ChangeState(PlayerState nextState)
    {
        PlayerState previousState = CurrentState;
        CurrentState = nextState;
        StateChanged?.Invoke(previousState, nextState);
    }
}
