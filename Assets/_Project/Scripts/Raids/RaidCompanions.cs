using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// The people with the player on a raid: the resident who came along (walks in beside them, grows with the raid's
/// danger so they stay as much help as in the first fight) and someone lost on an earlier raid, standing where a rumour
/// said, who follows once found. At the end it settles them: the companion's bond grows or they are lost, the lost one
/// comes home only beside the player. Owned by the <see cref="RaidDirector"/>; plain C#.
/// </summary>
public sealed class RaidCompanions
{
    private readonly RaidDirector raid;
    private CompanionBondRank companionRank;

    public RaidCompanions(RaidDirector raid)
    {
        this.raid = raid;
        // Same lifetime as the raid: kept up with each danger stage, as the enemies are (Creatures).
        raid.StageChanged += OnStageChanged;
    }

    /// <summary>Who came along on this raid, once they have walked in.</summary>
    public Companion Companion { get; private set; }

    /// <summary>Someone lost on an earlier raid, standing where a rumour said. Null when nobody is out there or they had nowhere to stand.</summary>
    public Companion LostOne { get; private set; }

    /// <summary>The player has found them, and they are coming along.</summary>
    public bool LostOneFollows { get; private set; }

    /// <summary>The raid's companion walks in beside the player, if one came.</summary>
    public void BringCompanion()
    {
        RaidSession session = raid.Session;
        GameObject player = raid.Player;
        Companion prefab = SettlementSettings.Current.RaidCompanionPrefab;
        if (session.Companion == null || session.Companion.RaidCompanion == null || prefab == null || player == null)
        {
            return;
        }

        Vector3 beside = player.transform.position - player.transform.forward * 2f + player.transform.right * 1.5f;
        if (!NavMesh.SamplePosition(beside, out NavMeshHit hit, 8f, NavMesh.AllAreas))
        {
            Debug.LogWarning($"Nowhere for {session.Companion.DisplayName} to stand; they stayed at home.", raid);
            // Told, not only logged: before, they simply never appeared, and the summary said they had made it home.
            GameMessages.Post($"{session.Companion.DisplayName} found no way in beside you and stayed at home.", MessageKind.Warning);
            return;
        }

        Companion = Companion.SpawnForRaid(prefab, session.Companion, hit.position);
        Companion.FormationOffset = new Vector3(2f, 0f, -3f);
        // One's own companion, from one's own Haven: on a raid followed with a host, the Haven here is still the host's.
        ResidentRecord record = GameSession.Home.Find(session.Companion);
        companionRank = record != null ? CompanionBond.RankFor(record.RaidsAlong, SettlementSettings.Current.BondRanks) : null;
        // Who they are: their talents, traits, marks, an inspired fighting heart and the kit given them (CompanionPeople).
        Companion.KitWeapon = CompanionKit.ShownWeapon(record);
        CompanionPeople.Apply(Companion, record, false, true);
        Toughen(Companion, companionRank);
    }

    /// <summary>Whoever is lost longest stands here, waiting, as a rumour said they would.</summary>
    public void StandLostAt(Vector3 position)
    {
        FallenRecord lost = GameSession.Here.LostToFind;
        Companion prefab = SettlementSettings.Current.RaidCompanionPrefab;
        if (lost == null || prefab == null || LostOne != null)
        {
            return;
        }

        Vector3 beside = position + Vector3.right * 2f;
        if (!NavMesh.SamplePosition(beside, out NavMeshHit hit, 8f, NavMesh.AllAreas))
        {
            Debug.LogWarning($"Nowhere for {lost.Resident.DisplayName} to stand; they weren't found this raid.", raid);
            return;
        }

        LostOne = Companion.SpawnForRaid(prefab, lost.Resident, hit.position);
        LostOne.FormationOffset = new Vector3(-2f, 0f, -3f);
        Toughen(LostOne, null);
        LostOne.StandLost();
    }

    /// <summary>The player has found them. They follow without knowing why.</summary>
    public void LeadLostHome()
    {
        if (LostOne != null)
        {
            LostOneFollows = true;
            LostOne.FollowWhoFoundThem();
            GameMessages.Post(string.Format(SettlementSettings.Current.FoundFollowLine, LostOne.DisplayName), MessageKind.Gain);
        }
    }

    /// <summary>
    /// The raid has ended: the companion's bond grows (or they are lost), and the lost one comes home or stays lost. One who
    /// never walked in (nowhere to stand beside the player, or the raid over before it began) stayed at home: the summary says
    /// nothing of them, where it said they had made it home.
    /// </summary>
    public void Settle(RaidReport report, bool extracted)
    {
        RaidSession session = raid.Session;
        // Walked in: set once they stood beside the player (a plain reference test, so it holds even if their body has gone since).
        if (session.Companion != null && !ReferenceEquals(Companion, null))
        {
            report.Companion = session.Companion;
            // Down when the raid ends is as lost as bleeding out: nobody went back for them.
            report.CompanionLost = Companion != null && (Companion.IsLost || Companion.IsDowned);
            if (report.CompanionLost)
            {
                GameSession.Home.LoseResident(session.Companion, session.Definition.DisplayName);
            }
            else if (Companion != null)
            {
                bool rose = GameSession.Home.RecordRaidAlong(session.Companion, SettlementSettings.Current.BondRanks, extracted, session.BossesBeaten,
                    Companion.TimesHelpedUp, session.TimeRemaining, Companion.Kills, SettlementSettings.Current.MemoryReturnsEvery);
                // Read back from where it was written (Home): from the Haven here, a guest's would be the host's record, or none.
                ResidentRecord record = GameSession.Home.Find(session.Companion);
                report.CompanionNewRank = rose && record != null ? CompanionBond.RankFor(record.RaidsAlong, SettlementSettings.Current.BondRanks)?.name : null;
                SettlePerson(record, session, extracted, rose);
            }
        }

        SettleTheLost(report, extracted);
    }

    // What the raid made of the person (Docs/PeopleResearch_2026-11-23.md): their fighting skill grew, it may have left a mark (said in
    // the homecoming's words), their kit wore, a fighting heart was spent, and a new bond rank lets the player grow a passion.
    private void SettlePerson(ResidentRecord record, RaidSession session, bool extracted, bool rose)
    {
        if (record == null || Companion == null)
        {
            return;
        }

        ResidentTalents.PractiseRaid(record, session.Tier, Companion.Kills);
        var facts = new RaidFacts { Extracted = extracted, TimesDowned = Companion.TimesHelpedUp, BossesBeaten = session.BossesBeaten, KillsBy = Companion.KillsBy };
        foreach (MarkDefinition mark in ResidentMarks.AfterRaid(record, facts, () => UnityEngine.Random.value))
        {
            GameMessages.Post($"{record.Resident.DisplayName} came home {(mark.Good ? "with" : "carrying")} something new: {mark.DisplayName}.",
                mark.Good ? MessageKind.Gain : MessageKind.Info);
        }

        CompanionKit.AfterRaid(record, extracted);
        Inspirations.Use(record, Inspiration.FightingHeart);
        if (rose)
        {
            ResidentTalents.OwePick(record);
        }
    }

    // Home with the player when they get out, alive and still following, however far behind: as a rescued traveller is
    // (RescuableTraveler; the user, 2026-11-16: "same as the traveller"). They had to stand close, and one lagging behind was
    // left in the forest. Never found, nothing has changed.
    private void SettleTheLost(RaidReport report, bool extracted)
    {
        if (LostOne == null || !LostOneFollows)
        {
            return;
        }

        // Co-op: a guest leads the host's lost home to the host's Haven (HavenHelp).
        if (extracted && !LostOne.IsLost && !LostOne.IsDowned && HavenHelp.FoundAgain(LostOne.Resident))
        {
            report.FoundAgain = LostOne.Resident;
        }
        else
        {
            report.LeftBehind = LostOne.Resident;
        }
    }

    // Fights grow with the raid's tier and so do they: their health keeps up with what the enemies deal (Power), their
    // blows with what the enemies can take (Health), so they stay as much help as they are in the first.
    private void Toughen(Companion who, CompanionBondRank rank, bool fill = true)
    {
        StatSheet stats = who.GetComponent<Combatant>().Stats;
        float tougher = rank != null ? rank.healthBonus : 0f;
        float harder = rank != null ? rank.damageBonus : 0f;
        stats.AddModifier(new StatModifier(StatType.MaxHealth, StatModifierOp.Percent, raid.PowerMultiplier * (1f + tougher) - 1f, raid));
        stats.AddModifier(new StatModifier(StatType.DamageBonus, StatModifierOp.Flat, raid.HealthMultiplier * (1f + harder) - 1f, raid));
        Damageable body = who.GetComponent<Damageable>();
        if (fill)
        {
            body.Heal(body.MaxHealth);
        }
    }

    // Each danger stage: toughened again to the raid's rules now (they were set once, at the start, while every stage made the enemies
    // stronger; one lost and found late came stronger than the one who walked in). Only the extra health is given, not a full heal.
    private void OnStageChanged(int stage, RaidStage _)
    {
        Retoughen(Companion, companionRank);
        Retoughen(LostOne, null);
    }

    private void Retoughen(Companion who, CompanionBondRank rank)
    {
        if (who == null || !who.TryGetComponent(out Damageable body) || !body.IsAlive)
        {
            return;
        }

        float before = body.MaxHealth;
        who.GetComponent<Combatant>().Stats.RemoveModifiersFrom(raid);
        Toughen(who, rank, fill: false);
        if (body.MaxHealth > before)
        {
            body.Heal(body.MaxHealth - before);
        }
    }
}
