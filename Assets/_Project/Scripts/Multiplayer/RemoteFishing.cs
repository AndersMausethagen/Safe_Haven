using UnityEngine;

/// <summary>
/// A friend's fishing line as you see it: the same float and line as your own (<see cref="FishingLine"/>), held by their
/// body and placed from what their game tells (<see cref="FishingLineState"/>). The reel's progress arrives in steps, so
/// the float is drawn towards the bank smoothly between them. Plain C#, owned by <see cref="NetworkPlayerAvatar"/>.
/// </summary>
public sealed class RemoteFishing
{
    // How fast the float follows the told reel progress, in shares of the way to the bank per second.
    private const float CatchFollow = 0.6f;

    private readonly FishingLine look;
    private float shownCatch;

    public RemoteFishing(Transform body)
    {
        look = new FishingLine(body);
    }

    /// <summary>The float, or null before a friend's line was first seen.</summary>
    public Transform Bobber => look.Bobber;

    /// <summary>Draws the line as <paramref name="state"/> tells, or takes it away when none is out.</summary>
    public void Show(in FishingLineState state, double serverNow, float localNow, float deltaTime)
    {
        if (!state.IsOut)
        {
            Hide();
            return;
        }

        shownCatch = state.FishingPhase == PlayerFishing.Phase.Reeling ? Mathf.MoveTowards(shownCatch, state.CatchShare, CatchFollow * deltaTime) : 0f;
        look.Show();
        look.Tick(state.CastHere(serverNow, localNow), state.FishingPhase, shownCatch);
    }

    public void Hide()
    {
        shownCatch = 0f;
        look.Hide();
    }

    public void Destroy() => look.Destroy();
}
