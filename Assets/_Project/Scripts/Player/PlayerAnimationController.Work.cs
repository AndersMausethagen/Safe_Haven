using UnityEngine;

/// <summary>
/// The player working and eating: an interaction's pose and stages, the work swings (chopping, mining) timed to their
/// contact, and eating. Part of PlayerAnimationController, in its own file.
/// </summary>
public sealed partial class PlayerAnimationController
{
    private void OnEatingStarted(ItemDefinition item) => CrossFadeUpper(ConsumeStateHash, 0.15f);

    private void OnEatingEnded(ItemDefinition item, bool eaten)
    {
        if (!eaten && upperBodyLayer >= 0 && animator.HasState(upperBodyLayer, EmptyStateHash))
        {
            animator.CrossFadeInFixedTime(EmptyStateHash, 0.12f, upperBodyLayer);
        }
    }

    /// <summary>
    /// Selects a phase of the current interaction for animations with several (Fish: 0 waiting, 1 fighting,
    /// 2 catch, 3 hauling). The stage persists until set again; return it to 0 when the phase is over.
    /// </summary>
    public void SetInteractionStage(int stage)
    {
        SetInt(InteractionStageHash, stage);
    }

    private void OnInteractionStarted(Interactable interactable)
    {
        SetInt(InteractionHash, (int)interactable.AnimationType);
        SetInt(InteractionStageHash, 0);
        SetBool(InteractingHash, true);
    }

    private void OnInteractionEnded(Interactable interactable, InteractionResult result)
    {
        SetBool(InteractingHash, false);
        workContact = 0f;
        SetFloat(WorkSpeedHash, 1f);
    }

    /// <summary>Hands this interaction's swings to the player: the tool rises and waits instead of swinging by itself.
    /// Does nothing for an animation that isn't a swing.</summary>
    public void HoldWorkSwings(InteractionAnimation animation)
    {
        GatheringMinigameSettings settings = GatheringMinigameSettings.Current;
        workContact = animator != null ? settings.SwingContact(animation) : 0f;
        workHold = Mathf.Max(0f, workContact - settings.SwingWindUp);
        workPhase = WorkPhase.Rising;
        workTime = 0f;
        workRaiseSpeed = 1f;
    }

    /// <summary>How fast the tool rises to the raised point (1 = as the clip was made, 0 = it waits where it is).</summary>
    public void SetWorkRaise(float speed)
    {
        workRaiseSpeed = Mathf.Max(0f, speed);
        if (workPhase == WorkPhase.Rising)
        {
            SetFloat(WorkSpeedHash, workRaiseSpeed);
        }
    }

    /// <summary>Brings the tool down. Returns the seconds until it lands, or 0 when swings aren't being held.</summary>
    public float StrikeWork()
    {
        if (workContact <= 0f)
        {
            return 0f;
        }

        int layer = animator.GetLayerIndex(InteractionLayer);
        float wait = workContact - workHold;
        if (workPhase != WorkPhase.Holding && layer >= 0)
        {
            // Still on its way back up: to the raised point at once, so a quick swing isn't made to wait for the last one.
            int state = animator.GetCurrentAnimatorStateInfo(layer).shortNameHash;
            if (System.Array.IndexOf(WorkStates, state) >= 0)
            {
                animator.CrossFadeInFixedTime(state, WorkReturnFade, layer, workHold);
                wait += WorkReturnFade;
            }
        }

        SetFloat(WorkSpeedHash, 1f);
        workPhase = WorkPhase.Following;
        workTime = workHold;
        return wait;
    }

    private void UpdateWorkSwing()
    {
        // A held clip has stopped, and reports its length as infinite: there is nothing to watch until it is released.
        if (workContact <= 0f || workPhase == WorkPhase.Holding)
        {
            return;
        }

        int layer = animator.GetLayerIndex(InteractionLayer);
        if (layer < 0 || animator.IsInTransition(layer))
        {
            return;
        }

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(layer);
        if (System.Array.IndexOf(WorkStates, state.shortNameHash) < 0)
        {
            return;
        }

        float time = state.normalizedTime % 1f * state.length;
        if (workPhase == WorkPhase.Following && time < workTime)
        {
            workPhase = WorkPhase.Rising;
            SetFloat(WorkSpeedHash, workRaiseSpeed);
        }

        if (workPhase == WorkPhase.Rising && time >= workHold)
        {
            workPhase = WorkPhase.Holding;
            SetFloat(WorkSpeedHash, 0f);
        }

        workTime = time;
    }
}
