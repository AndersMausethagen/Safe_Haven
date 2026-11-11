using UnityEngine;

/// <summary>
/// The numbers of taming, apart from any scene so they can be tested. The struggle: while the lassoed horse pulls, strain
/// builds on the rope (at 1 it slips free); each tap of the player's (E or left click), and every moment they lean away from
/// it with the movement keys (on their own stamina), takes some strain off and tires the horse, and at 0 struggle stamina
/// it gives in. A better horse pulls harder, and its temperament pulls harder or softer still. The breaking ride: the horse
/// asks for barley every so often and throws a rider who doesn't give it in time, and between times it veers and bucks:
/// steered against, the rider keeps their balance; let be, they lose it and are thrown.
/// </summary>
public static class HorseTamingRules
{
    /// <summary>How much harder than the weakest of its kind a horse pulls: up to 30% more for the best.</summary>
    public static float PullScale(float quality) => 1f + 0.3f * Mathf.Clamp01(quality);

    /// <summary>One step of the struggle: the rope's strain and the horse's struggle stamina after <paramref name="taps"/>
    /// taps (a lean counts as a share of one) over <paramref name="seconds"/>. <paramref name="pull"/> is its temperament's
    /// pull, <paramref name="hold"/> the player's hold (1 + Horsemanship's Hold on the Rope).</summary>
    public static void Struggle(ref float strain, ref float stamina, float taps, float seconds, float quality, HorseDefinition horse, float pull = 1f, float hold = 1f)
    {
        strain = Mathf.Clamp01(strain + horse.PullStrain * PullScale(quality) * pull * seconds - taps * horse.TapHold * hold);
        stamina = Mathf.Max(0f, stamina - taps * horse.TapDrain * hold);
    }

    /// <summary>Taps a second that just hold the rope where it is against this horse.</summary>
    public static float TapsToHold(float quality, HorseDefinition horse, float pull = 1f, float hold = 1f) =>
        horse.PullStrain * PullScale(quality) * pull / (horse.TapHold * hold);

    /// <summary>How hard the player leans away from the horse, 0-1: the movement keys pointing from it.</summary>
    public static float Lean(Vector3 input, Vector3 fromHorse)
    {
        input.y = 0f;
        fromHorse.y = 0f;
        if (input.sqrMagnitude < 0.01f || fromHorse.sqrMagnitude < 0.0001f)
        {
            return 0f;
        }

        // Straight away from it counts in full; across it, not at all.
        return Mathf.Clamp01((Vector3.Dot(Vector3.ClampMagnitude(input, 1f), fromHorse.normalized) - 0.3f) / 0.7f);
    }

    /// <summary>True when a lean caught the pull: hard against it (at least 0.6) within <paramref name="window"/> seconds of
    /// its start, after the horse's tell.</summary>
    public static bool CaughtPull(float sincePullStarted, float lean, float window) =>
        sincePullStarted >= 0f && sincePullStarted <= window && lean >= 0.6f;

    /// <summary>True when the movement keys go with the horse's pull rather than against it: that gives it rope.</summary>
    public static bool LeansWith(Vector3 input, Vector3 pull)
    {
        input.y = 0f;
        pull.y = 0f;
        return input.sqrMagnitude > 0.01f && pull.sqrMagnitude > 0.0001f && Vector3.Dot(input.normalized, pull.normalized) > 0.4f;
    }

    public static bool Slipped(float strain) => strain >= 1f;

    public static bool GivenIn(float stamina) => stamina <= 0f;

    /// <summary>Seconds until the horse next asks for barley on its breaking ride.</summary>
    public static float NextAsk(HorseDefinition horse, System.Random random)
    {
        Vector2 range = horse.FeedInterval;
        return Mathf.Lerp(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y), (float)random.NextDouble());
    }

    /// <summary>Feeds it takes: its own number and its temperament's, never fewer than one.</summary>
    public static int FeedsNeeded(HorseStats stats, int temperamentFeeds = 0) => Mathf.Max(1, stats.feedsNeeded + temperamentFeeds);

    /// <summary>True once it has been fed as often as it needs: it is tamed.</summary>
    public static bool IsBroken(int fed, HorseStats stats, int temperamentFeeds = 0) => fed >= FeedsNeeded(stats, temperamentFeeds);

    /// <summary>
    /// The rider's balance after <paramref name="seconds"/> of a veer: steering against it (the movement keys away from the
    /// veer) wins balance back at <paramref name="steady"/> a second, letting it be loses <paramref name="loss"/> a second
    /// (times its bucking). With no veer it comes back slowly by itself.
    /// </summary>
    public static float Balance(float balance, Vector3 veer, Vector3 input, float seconds, float loss, float steady, float bucking)
    {
        if (veer.sqrMagnitude < 0.0001f)
        {
            return Mathf.Clamp01(balance + steady * 0.25f * seconds);
        }

        input.y = 0f;
        veer.y = 0f;
        float against = input.sqrMagnitude > 0.01f ? Vector3.Dot(input.normalized, -veer.normalized) : 0f;
        return against > 0.35f
            ? Mathf.Clamp01(balance + steady * against * seconds)
            : Mathf.Clamp01(balance - loss * bucking * seconds);
    }
}
