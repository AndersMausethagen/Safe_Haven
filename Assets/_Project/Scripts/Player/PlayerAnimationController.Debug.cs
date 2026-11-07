using UnityEngine;

/// <summary>
/// Inspector context-menu actions that play each one-shot animation, for checking a controller by hand. Part of
/// PlayerAnimationController, in its own file.
/// </summary>
public sealed partial class PlayerAnimationController
{
    // Test hooks: right-click the component header while playing.
    [ContextMenu("Debug/Play Attack")]
    private void DebugAttack()
    {
        if (Application.isPlaying)
        {
            PlayAttack();
        }
    }

    [ContextMenu("Debug/Play Shield Bash")]
    private void DebugShieldBash()
    {
        if (Application.isPlaying)
        {
            PlayShieldBash();
        }
    }

    [ContextMenu("Debug/Play Hit")]
    private void DebugHit()
    {
        if (Application.isPlaying)
        {
            PlayHit();
        }
    }

    [ContextMenu("Debug/Play Jump")]
    private void DebugJump()
    {
        if (Application.isPlaying)
        {
            PlayJump();
        }
    }

    [ContextMenu("Debug/Toggle Dead")]
    private void DebugToggleDead()
    {
        if (Application.isPlaying)
        {
            SetDead(!animator.GetBool(DeadHash));
        }
    }
}
