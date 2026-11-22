using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Where a region stands with the Haven.</summary>
public enum RegionStanding
{
    /// <summary>Never claimed, or taken back and not claimed again.</summary>
    Wild = 0,
    /// <summary>Claimed, but the hold is thin: one push could take it.</summary>
    Slipping = 1,
    Held = 2,
    /// <summary>Held firmly: it sends a tithe home, and the corruption doesn't come from it.</summary>
    Secure = 3,
}

/// <summary>
/// The Haven's hold on the land round it (Docs/Territory.md), as plain rules: which regions are claimed and how firmly (hold, 0 to
/// 100), the forest's push on one of them, and each held region's task. Time goes by in turns: a dawn at home and every raid that
/// ends (Territory calls AdvanceTurn), so a push can't be missed by being away on raids and isn't rushed by sleeping. Runtime
/// state on SettlementState (saved in SettlementSaveData.territory); Territory runs it in the game, the tests run it alone.
/// </summary>
public sealed class TerritoryState
{
    public sealed class Hold
    {
        public string Region;
        public bool Claimed;
        public bool EverClaimed;
        public float Value;
        public int TaskProgress;
        /// <summary>The turn from which the region asks again after a task (0: asks now).</summary>
        public int TaskFrom;
        public int TasksDone;
    }

    /// <summary>The forest gathering against a region: it comes on DueTurn with Strength, less what was done about it.</summary>
    public sealed class Push
    {
        public string Region;
        public int DueTurn;
        public float Strength;
        /// <summary>Share already taken off it (rot's hearts burned), 0 to 1.</summary>
        public float Weakened;

        public float Remaining => Strength * Math.Max(0f, 1f - Weakened);
    }

    private readonly Dictionary<string, Hold> holds = new Dictionary<string, Hold>();

    public int Turn { get; private set; }
    public int RestUntil { get; private set; }
    /// <summary>The turn the first region was claimed, or -1.</summary>
    public int FirstClaimTurn { get; private set; } = -1;
    public bool FreeClaimUsed { get; private set; }
    public Push Incursion { get; private set; }

    public IEnumerable<Hold> Holds => holds.Values;

    public Hold Of(string region)
    {
        if (!holds.TryGetValue(region, out Hold hold))
        {
            hold = new Hold { Region = region };
            holds[region] = hold;
        }

        return hold;
    }

    public bool IsClaimed(string region) => region != null && holds.TryGetValue(region, out Hold hold) && hold.Claimed;

    public float HoldOf(string region) => region != null && holds.TryGetValue(region, out Hold hold) && hold.Claimed ? hold.Value : 0f;

    public RegionStanding StandingOf(string region, TerritorySettings settings)
    {
        if (!IsClaimed(region))
        {
            return RegionStanding.Wild;
        }

        float value = HoldOf(region);
        return value >= settings.SecureFrom ? RegionStanding.Secure : value < settings.SlippingBelow ? RegionStanding.Slipping : RegionStanding.Held;
    }

    public int ClaimedCount => holds.Values.Count(h => h.Claimed);

    /// <summary>Whether the next claim needs a Pact Rune.</summary>
    public bool ClaimNeedsRune(TerritorySettings settings) => !settings.FirstClaimFree || FreeClaimUsed;

    /// <summary>Lights a region's stone: it is the Haven's at the starting hold.</summary>

    // Co-op: in a guest's game the lands are the host's copy (SettlementState.IsBorrowed): a change there is undone at the
    // host's next word. Territory sends a guest's work to the host instead; this says so loudly if something doesn't.
    private void Borrowed([System.Runtime.CompilerServices.CallerMemberName] string change = null)
    {
        if (HavenVisit.IsVisiting && ReferenceEquals(this, GameSession.Here.Territory))
        {
            UnityEngine.Debug.LogError($"Co-op: TerritoryState.{change} changed the copy of the host's lands in a guest's game; send it with Territory.ForHost.\n" + System.Environment.StackTrace);
        }
    }

    public void Claim(string region, TerritorySettings settings)
    {
        Borrowed();
        Hold hold = Of(region);
        if (hold.Claimed)
        {
            return;
        }

        // Whether this one was the free one or paid with a rune, the free claim is behind the player now.
        FreeClaimUsed = true;
        hold.Claimed = true;
        hold.EverClaimed = true;
        hold.Value = settings.ClaimHold;
        hold.TaskProgress = 0;
        hold.TaskFrom = Turn;
        if (FirstClaimTurn < 0)
        {
            FirstClaimTurn = Turn;
        }
    }

    /// <summary>Strengthens a held region's hold (a rune, a task, a find, a watch held), never past 100.</summary>
    public void Raise(string region, float by)
    {
        Borrowed();
        if (IsClaimed(region))
        {
            Hold hold = Of(region);
            hold.Value = Math.Min(100f, hold.Value + Math.Max(0f, by));
        }
    }

    /// <summary>One turn: every held region wears a little (less where a party works it: <paramref name="tended"/>), a push due comes,
    /// and the forest may gather for the next (seen a turn sooner where a party scouts: <paramref name="scouted"/>). Returns what
    /// happened, in words, and how much the blight spread (the caller spreads it).</summary>
    public List<string> AdvanceTurn(TerritorySettings settings, float blight, Func<float> roll, out float blightSpread,
        ICollection<string> tended = null, ICollection<string> scouted = null)
    {
        Borrowed();
        Turn++;
        blightSpread = 0f;
        var news = new List<string>();
        float wear = settings.ErosionAt(blight);
        foreach (Hold hold in holds.Values.Where(h => h.Claimed).ToList())
        {
            hold.Value -= tended != null && tended.Contains(hold.Region) ? wear * settings.PartyWearShare : wear;
            if (hold.Value <= 0f)
            {
                Lose(hold, settings, news, ref blightSpread);
            }
        }

        if (Incursion != null && Turn >= Incursion.DueTurn)
        {
            Push push = Incursion;
            Incursion = null;
            RestUntil = Turn + settings.RestAfterPush;
            if (IsClaimed(push.Region))
            {
                Hold hold = Of(push.Region);
                float taken = push.Remaining;
                if (taken <= 0.01f)
                {
                    news.Add($"The forest's push on {push.Region} came to nothing.");
                }
                else
                {
                    hold.Value -= taken;
                    if (hold.Value <= 0f)
                    {
                        Lose(hold, settings, news, ref blightSpread);
                    }
                    else
                    {
                        news.Add($"The forest pushed into {push.Region}, and it was held, just: its hold is down to {hold.Value:0}.");
                    }
                }
            }
        }
        else if (Incursion == null && FirstClaimTurn >= 0 && Turn >= FirstClaimTurn + settings.FirstPushAfter && Turn >= RestUntil
                 && ClaimedCount > 0 && roll() < settings.PushChanceAt(blight))
        {
            Hold target = holds.Values.Where(h => h.Claimed).OrderBy(h => h.Value).ThenBy(h => h.Region, StringComparer.Ordinal).First();
            float strength = settings.PushStrengthAt(blight);
            if (target.Value >= settings.SecureFrom)
            {
                strength *= settings.SecureShare;
            }

            // Scouts out there see it gathering sooner: more turns to answer it.
            bool seen = scouted != null && scouted.Contains(target.Region);
            int warning = settings.WarningTurns + (seen ? settings.ScoutWarningTurns : 0);
            Incursion = new Push { Region = target.Region, DueTurn = Turn + warning, Strength = strength };
            news.Add((seen ? "The scouts saw it first: the forest" : "The forest") + $" is gathering against {target.Region}. In {warning} turns it will " +
                     "push in: stand watch at its boundary stone, or burn the rot's heart on a raid.");
        }

        return news;
    }

    private static void Lose(Hold hold, TerritorySettings settings, List<string> news, ref float blightSpread)
    {
        hold.Claimed = false;
        hold.Value = 0f;
        hold.TaskProgress = 0;
        blightSpread += settings.LostBlight;
        news.Add($"The forest has taken {hold.Region} back. Its boundary stone has gone dark.");
    }

    /// <summary>A rot's heart burned on a raid: the push loses a share. Returns false when no push is coming.</summary>
    public bool Weaken(TerritorySettings settings)
    {
        Borrowed();
        if (Incursion == null)
        {
            return false;
        }

        Incursion.Weakened = Math.Min(1f, Incursion.Weakened + settings.HeartShare);
        return true;
    }

    /// <summary>The watch at the stone was held: the push on that region is broken and the hold grows.</summary>
    public bool Defended(string region, TerritorySettings settings)
    {
        Borrowed();
        if (Incursion == null || Incursion.Region != region)
        {
            return false;
        }

        Incursion = null;
        RestUntil = Turn + settings.RestAfterPush;
        Raise(region, settings.DefenceHold);
        return true;
    }

    /// <summary>Whether a held region is asking for its task now.</summary>
    public bool TaskOpen(string region) => IsClaimed(region) && Turn >= Of(region).TaskFrom;

    /// <summary>Counts one more towards a region's task. Returns true when that finished it (the caller rewards it).</summary>
    public bool CountTask(string region, int needed, TerritorySettings settings)
    {
        Borrowed();
        if (!TaskOpen(region))
        {
            return false;
        }

        Hold hold = Of(region);
        hold.TaskProgress++;
        if (hold.TaskProgress < needed)
        {
            return false;
        }

        hold.TaskProgress = 0;
        hold.TasksDone++;
        hold.TaskFrom = Turn + settings.TaskRest;
        Raise(region, settings.TaskHold);
        return true;
    }

    /// <summary>Renown the land brings: so much for each held region, more for each Secure one.</summary>
    public int Renown(TerritorySettings settings) =>
        holds.Values.Where(h => h.Claimed).Sum(h => h.Value >= settings.SecureFrom ? settings.SecureRenown : settings.HeldRenown);

    public void Restore(int turn, int restUntil, int firstClaimTurn, bool freeClaimUsed, IEnumerable<Hold> saved, Push push)
    {
        holds.Clear();
        Turn = turn;
        RestUntil = restUntil;
        FirstClaimTurn = firstClaimTurn;
        FreeClaimUsed = freeClaimUsed;
        foreach (Hold hold in saved ?? Enumerable.Empty<Hold>())
        {
            if (!string.IsNullOrEmpty(hold.Region))
            {
                holds[hold.Region] = hold;
            }
        }

        Incursion = push;
    }
}
