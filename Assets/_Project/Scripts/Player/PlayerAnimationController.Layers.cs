using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hand-overs between the swing layers (Upper Body, Attack Arms and, on top and unmasked, Combat Full Body). Fading one layer in
/// while another fades out over the same time let the pose under both, Base's ready pose, show through by t(1-t), a quarter at
/// mid-blend: heavies sagged as they grew, the planted blow fell back at contact, a counter or bash out of the guard first moved
/// backwards (combat responsiveness review, 2026-11-20, measured on the live animator: a 0.89 dip at the planted strike, 0.04 with
/// the hand-over below). So:
/// <list type="bullet">
/// <item>into Combat Full Body, it crossfades in over the layers below, which stay as they are until it covers them, and are then
/// cleared under it (<see cref="IntoFullBody"/>);</item>
/// <item>out of Combat Full Body into a layer below it, that layer's state goes in at once, hidden beneath, and only Full Body fades
/// out, so the blend runs from one pose straight to the other (<see cref="UnderFullBody"/>).</item>
/// </list>
/// Part of PlayerAnimationController, in its own file.
/// </summary>
public sealed partial class PlayerAnimationController
{
    private readonly List<int> clearWhenCovered = new List<int>();
    private int clearArmedFrame = -1;

    // A layer showing something (a state, or on its way somewhere).
    private bool LayerBusy(int layer) =>
        layer >= 0 && (animator.IsInTransition(layer) || animator.GetCurrentAnimatorStateInfo(layer).shortNameHash != EmptyStateHash);

    /// <summary>Plays <paramref name="state"/> on Combat Full Body over <paramref name="fade"/>; the swing layers below it are cleared
    /// once it covers them.</summary>
    private void IntoFullBody(int state, float fade, float stateTimeOffset)
    {
        animator.CrossFadeInFixedTime(state, fade, fullBodyLayer, stateTimeOffset);
        foreach (int other in new[] { upperBodyLayer, attackArmsLayer })
        {
            if (other >= 0 && other != fullBodyLayer && LayerBusy(other) && !clearWhenCovered.Contains(other))
            {
                clearWhenCovered.Add(other);
            }
        }

        clearArmedFrame = Time.frameCount;
    }

    /// <summary>Plays <paramref name="state"/> on a layer under Combat Full Body: with Full Body showing, the state goes in at once
    /// beneath it and Full Body fades out over <paramref name="fullBodyFade"/>; otherwise an ordinary crossfade over
    /// <paramref name="fade"/>.</summary>
    private void UnderFullBody(int layer, int state, float fade, float stateTimeOffset, float fullBodyFade)
    {
        clearWhenCovered.Remove(layer);
        if (fullBodyLayer >= 0 && layer != fullBodyLayer && LayerBusy(fullBodyLayer))
        {
            animator.PlayInFixedTime(state, layer, stateTimeOffset);
            animator.CrossFadeInFixedTime(EmptyStateHash, fullBodyFade, fullBodyLayer);
            // Full Body is leaving: nothing waits to be covered by it.
            clearWhenCovered.Clear();
            return;
        }

        animator.CrossFadeInFixedTime(state, fade, layer, stateTimeOffset);
    }

    // Start of LateUpdate (the animator has run this frame's crossfades): the layers under a Full Body action that now covers them
    // are cleared beneath it; if it left before covering them, they go with a short fade.
    private void ClearUnderFullBody()
    {
        if (clearWhenCovered.Count == 0 || Time.frameCount == clearArmedFrame)
        {
            return;
        }

        if (fullBodyLayer < 0)
        {
            clearWhenCovered.Clear();
            return;
        }

        bool changing = animator.IsInTransition(fullBodyLayer);
        bool covered = !changing && animator.GetCurrentAnimatorStateInfo(fullBodyLayer).shortNameHash != EmptyStateHash;
        bool left = changing ? animator.GetNextAnimatorStateInfo(fullBodyLayer).shortNameHash == EmptyStateHash : !covered;
        if (!covered && !left)
        {
            return;
        }

        foreach (int layer in clearWhenCovered)
        {
            if (covered)
            {
                animator.Play(EmptyStateHash, layer, 0f);
            }
            else
            {
                animator.CrossFadeInFixedTime(EmptyStateHash, 0.1f, layer);
            }
        }

        clearWhenCovered.Clear();
    }

    // A swing (a state the arms layer also has) is showing on a swing layer: a new swing now is a chain's next step, not one from
    // the ready pose (AttackDefinition.BlendIn against BlendInFromReady).
    private bool SwingShowing()
    {
        foreach (int layer in new[] { fullBodyLayer, upperBodyLayer, attackArmsLayer })
        {
            if (layer < 0 || attackArmsLayer < 0)
            {
                continue;
            }

            int now = animator.GetCurrentAnimatorStateInfo(layer).shortNameHash;
            if (now != EmptyStateHash && animator.HasState(attackArmsLayer, now))
            {
                return true;
            }
        }

        return false;
    }
}
