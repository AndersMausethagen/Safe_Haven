using UnityEngine;

/// <summary>The poses of fishing, on the Interaction layer (InteractionAnimation.Fish).</summary>
public enum FishingPose
{
    /// <summary>The rod out over the water, waiting (the Fish Wait loop).</summary>
    Waiting = 0,
    /// <summary>A fish on: the rod jerks and bends (Fish Fight).</summary>
    Fighting = 1,
    /// <summary>A light fish swung out of the water on the line (Fish Catch).</summary>
    Catch = 2,
    /// <summary>A heavy fish lifted out with both hands and held up (Fish Haul Begin, Loop, Stop).</summary>
    Haul = 3,
}

/// <summary>
/// The player fishing: a cast is the rod raised (Fish Begin) and swung back (Fish Cast), held at the top of the backswing
/// while the cast is charged and thrown forward when it is let go; the line leaves at the throw's flick
/// (<see cref="CastFlicked"/>). Then the wait, the fight, and the landing (<see cref="FishingPose"/>). FishSpeed is the
/// playback speed of the cast and the fight. Driven by PlayerFishing; part of PlayerAnimationController, in its own file.
/// </summary>
public sealed partial class PlayerAnimationController
{
    private static readonly int FishSpeedHash = Animator.StringToHash("FishSpeed");
    private static readonly int FishBeginHash = Animator.StringToHash("Fish Begin");
    private static readonly int FishCastHash = Animator.StringToHash("Fish Cast");
    private static readonly int FishCatchHash = Animator.StringToHash("Fish Catch");
    private static readonly int FishHaulBeginHash = Animator.StringToHash("Fish Haul Begin");
    private static readonly int FishHaulLoopHash = Animator.StringToHash("Fish Haul Loop");

    private bool fishing;

    /// <summary>True while a fishing pose is being played.</summary>
    public bool IsFishing => fishing;

    /// <summary>Raises the rod and swings it back for a cast; the line is out with the rod until <see cref="EndFishing"/>.</summary>
    public void BeginCast()
    {
        fishing = true;
        SetFloat(FishSpeedHash, 1f);
        SetInt(InteractionStageHash, (int)FishingPose.Waiting);
        SetInt(InteractionHash, (int)InteractionAnimation.Fish);
        SetBool(InteractingHash, true);
        int layer = animator != null ? animator.GetLayerIndex(InteractionLayer) : -1;
        if (layer >= 0 && animator.HasState(layer, FishBeginHash))
        {
            // From wherever the last cast left it (waiting, or still settling after a catch).
            animator.CrossFadeInFixedTime(FishBeginHash, 0.12f, layer, 0f);
        }
    }

    /// <summary>
    /// While a cast is charging: the backswing stops at <paramref name="holdShare"/> of the throw while
    /// <paramref name="held"/>, and goes on (a little quicker) once let go.
    /// </summary>
    public void HoldCast(bool held, float holdShare)
    {
        float share = ClipShare(FishCastHash);
        SetFloat(FishSpeedHash, held && share >= holdShare ? 0f : held ? 1f : 1.35f);
    }

    /// <summary>True once the throw has passed <paramref name="releaseShare"/> of its clip (the rod's forward flick),
    /// where the float leaves the tip. Also true when there is nothing to watch (no fishing states).</summary>
    public bool CastFlicked(float releaseShare)
    {
        int layer = animator != null ? animator.GetLayerIndex(InteractionLayer) : -1;
        if (layer < 0 || !animator.HasState(layer, FishCastHash))
        {
            return true;
        }

        return ClipShare(FishCastHash) >= releaseShare;
    }

    /// <summary>Switches the fishing pose (the wait, the fight, a landing); FishSpeed sets how fast the fight jerks.</summary>
    public void SetFishingPose(FishingPose pose, float speed = 1f)
    {
        SetInt(InteractionStageHash, (int)pose);
        SetFloat(FishSpeedHash, Mathf.Max(0f, speed));
    }

    /// <summary>How far through a landing the clip is, 0 to 1 (the lift of a haul counts as its start), or -1 when no
    /// landing is playing.</summary>
    public float LandingShare
    {
        get
        {
            float share = ClipShare(FishCatchHash);
            if (share >= 0f)
            {
                return share;
            }

            share = ClipShare(FishHaulBeginHash);
            return share >= 0f ? share : ClipShare(FishHaulLoopHash) >= 0f ? 1f : -1f;
        }
    }

    /// <summary>Puts the rod down: the fishing pose ends (from waiting or fighting through Fish Stop).</summary>
    public void EndFishing()
    {
        if (!fishing)
        {
            return;
        }

        fishing = false;
        SetFloat(FishSpeedHash, 1f);
        SetInt(InteractionStageHash, (int)FishingPose.Waiting);
        SetBool(InteractingHash, false);
    }

    // How far through the named Interaction-layer state the animator is (0 to 1, the next state's while blending into
    // it), or -1 when it isn't playing that state.
    private float ClipShare(int stateHash)
    {
        int layer = animator != null ? animator.GetLayerIndex(InteractionLayer) : -1;
        if (layer < 0)
        {
            return -1f;
        }

        AnimatorStateInfo state = animator.IsInTransition(layer) ? animator.GetNextAnimatorStateInfo(layer) : animator.GetCurrentAnimatorStateInfo(layer);
        if (state.shortNameHash != stateHash)
        {
            state = animator.GetCurrentAnimatorStateInfo(layer);
            if (state.shortNameHash != stateHash)
            {
                return -1f;
            }
        }

        return state.loop ? Mathf.Repeat(state.normalizedTime, 1f) : Mathf.Clamp01(state.normalizedTime);
    }
}
