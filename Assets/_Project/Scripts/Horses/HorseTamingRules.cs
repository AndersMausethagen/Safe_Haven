using UnityEngine;

/// <summary>
/// The numbers of taming, apart from any scene so they can be tested. The struggle: while the lassoed horse pulls, strain
/// builds on the rope (at 1 it slips free); each tap of the player's (E or left click) takes some strain off and tires the
/// horse, and at 0 struggle stamina it gives in. A better horse pulls harder. The breaking ride: the horse asks for barley
/// every so often and throws a rider who doesn't give it in time.
/// </summary>
public static class HorseTamingRules
{
    /// <summary>How much harder than the weakest of its kind a horse pulls: up to 30% more for the best.</summary>
    public static float PullScale(float quality) => 1f + 0.3f * Mathf.Clamp01(quality);

    /// <summary>One step of the struggle: the rope's strain and the horse's struggle stamina after <paramref name="taps"/>
    /// taps over <paramref name="seconds"/>.</summary>
    public static void Struggle(ref float strain, ref float stamina, int taps, float seconds, float quality, HorseDefinition horse)
    {
        strain = Mathf.Clamp01(strain + horse.PullStrain * PullScale(quality) * seconds - taps * horse.TapHold);
        stamina = Mathf.Max(0f, stamina - taps * horse.TapDrain);
    }

    /// <summary>Taps a second that just hold the rope where it is against this horse.</summary>
    public static float TapsToHold(float quality, HorseDefinition horse) => horse.PullStrain * PullScale(quality) / horse.TapHold;

    public static bool Slipped(float strain) => strain >= 1f;

    public static bool GivenIn(float stamina) => stamina <= 0f;

    /// <summary>Seconds until the horse next asks for barley on its breaking ride.</summary>
    public static float NextAsk(HorseDefinition horse, System.Random random)
    {
        Vector2 range = horse.FeedInterval;
        return Mathf.Lerp(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y), (float)random.NextDouble());
    }

    /// <summary>True once it has been fed as often as it needs: it is tamed.</summary>
    public static bool IsBroken(int fed, HorseStats stats) => fed >= Mathf.Max(1, stats.feedsNeeded);
}
