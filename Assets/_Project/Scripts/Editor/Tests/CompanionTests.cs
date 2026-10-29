using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class CompanionTests
{
    private static readonly CompanionRanges Ranges = new CompanionRanges(engage: 9f, defend: 3.5f, hold: 6f, retreat: 10f, slack: 1.5f);

    private static CompanionSituation Situation(CompanionOrder order, params CompanionThreat[] threats) => new CompanionSituation
    {
        Order = order,
        Self = new Vector3(0f, 0f, -2f),
        Leader = Vector3.zero,
        FollowPoint = new Vector3(0f, 0f, -2f),
        HoldPoint = new Vector3(20f, 0f, 0f),
        LeaderTarget = -1,
        Threats = new List<CompanionThreat>(threats),
    };

    private static CompanionThreat At(float x, float z, bool huntsLeader = false, bool huntsSelf = false) =>
        new CompanionThreat(new Vector3(x, 0f, z), huntsLeader, huntsSelf);

    [Test]
    public void Follow_JoinsFightsNearTheLeader_AndStaysInFormationOtherwise()
    {
        Assert.AreEqual(CompanionAction.Idle, CompanionTactics.Decide(Situation(CompanionOrder.Follow), Ranges).Action);

        CompanionIntent fight = CompanionTactics.Decide(Situation(CompanionOrder.Follow, At(30f, 0f), At(5f, 0f)), Ranges);
        Assert.AreEqual(CompanionAction.Attack, fight.Action);
        Assert.AreEqual(1, fight.Target, "Only the wolf near the leader.");

        CompanionIntent far = CompanionTactics.Decide(Situation(CompanionOrder.Follow, At(30f, 0f)), Ranges);
        Assert.AreEqual(CompanionAction.Idle, far.Action, "An enemy far from the leader is left alone.");
    }

    [Test]
    public void Fighting_PicksWhatHuntsItselfFirst_ThenWhatHuntsTheLeader()
    {
        var situation = Situation(CompanionOrder.Follow, At(1f, 0f), At(4f, 0f, huntsLeader: true), At(6f, 0f, huntsSelf: true));
        Assert.AreEqual(2, CompanionTactics.Decide(situation, Ranges).Target);

        situation = Situation(CompanionOrder.Follow, At(1f, 0f), At(4f, 0f, huntsLeader: true));
        Assert.AreEqual(1, CompanionTactics.Decide(situation, Ranges).Target);
    }

    [Test]
    public void Follow_ComesBack_WhenLedTooFarFromTheLeader()
    {
        CompanionSituation situation = Situation(CompanionOrder.Follow, At(26f, 0f, huntsSelf: true));
        situation.Self = new Vector3(25f, 0f, 0f);
        CompanionIntent intent = CompanionTactics.Decide(situation, Ranges);
        Assert.AreEqual(CompanionAction.Move, intent.Action);
        Assert.AreEqual(situation.FollowPoint, intent.Point);
    }

    [Test]
    public void Defend_FightsOnlyWhatThreatensTheLeaderOrItself()
    {
        Assert.AreEqual(CompanionAction.Idle, CompanionTactics.Decide(Situation(CompanionOrder.Defend, At(6f, 0f)), Ranges).Action);
        Assert.AreEqual(CompanionAction.Attack, CompanionTactics.Decide(Situation(CompanionOrder.Defend, At(6f, 0f, huntsLeader: true)), Ranges).Action);
        Assert.AreEqual(CompanionAction.Attack, CompanionTactics.Decide(Situation(CompanionOrder.Defend, At(2f, 0f)), Ranges).Action);
    }

    [Test]
    public void Stay_HoldsItsSpot_AndFightsOnlyWhatComesClose()
    {
        CompanionIntent walk = CompanionTactics.Decide(Situation(CompanionOrder.Stay, At(1f, 0f)), Ranges);
        Assert.AreEqual(CompanionAction.Move, walk.Action, "An enemy by the leader is not near the held spot.");
        Assert.AreEqual(new Vector3(20f, 0f, 0f), walk.Point);

        Assert.AreEqual(CompanionAction.Attack, CompanionTactics.Decide(Situation(CompanionOrder.Stay, At(23f, 0f)), Ranges).Action);
    }

    [Test]
    public void Retreat_NeverAttacks_AndMovesBehindTheLeaderAwayFromEnemies()
    {
        CompanionIntent intent = CompanionTactics.Decide(Situation(CompanionOrder.Retreat, At(0f, 4f, huntsSelf: true)), Ranges);
        Assert.AreEqual(CompanionAction.Move, intent.Action);
        Assert.Less(intent.Point.z, -5f, "Away from the enemy to the north, past the leader.");

        Assert.AreEqual(CompanionAction.Idle, CompanionTactics.Decide(Situation(CompanionOrder.Retreat, At(40f, 0f)), Ranges).Action);
    }

    [Test]
    public void AttackTarget_GoesForTheLeadersTarget_WhereverItIs()
    {
        CompanionSituation situation = Situation(CompanionOrder.AttackTarget, At(2f, 0f, huntsSelf: true), At(40f, 0f));
        situation.LeaderTarget = 1;
        Assert.AreEqual(1, CompanionTactics.Decide(situation, Ranges).Target);

        situation.LeaderTarget = -1;
        Assert.AreEqual(0, CompanionTactics.Decide(situation, Ranges).Target, "Without a target it fights like following.");
    }

    [Test]
    public void Factions_PlayerAndAlliesAreOneSide()
    {
        Assert.IsTrue(Faction.Enemy.WillAttack(Faction.Player));
        Assert.IsTrue(Faction.Enemy.WillAttack(Faction.Ally));
        Assert.IsTrue(Faction.Ally.WillAttack(Faction.Enemy));
        Assert.IsFalse(Faction.Ally.WillAttack(Faction.Player));
        Assert.IsFalse(Faction.Neutral.WillAttack(Faction.Player));
        Assert.IsTrue(Faction.Player.AreAllies(Faction.Ally), "No friendly fire between the player and companions.");
        Assert.IsFalse(Faction.Player.AreAllies(Faction.Neutral), "Neutral props can still be broken.");
        Assert.IsFalse(Faction.Enemy.AreAllies(Faction.Ally));
    }

    [Test]
    public void Swings_NeverHitTheirOwnSide()
    {
        var bodies = new List<GameObject>();
        var attack = ScriptableObject.CreateInstance<AttackDefinition>();
        try
        {
            Damageable Body(Faction faction, Vector3 at)
            {
                var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                bodies.Add(body);
                body.transform.position = at;
                var combatant = body.AddComponent<Combatant>();
                var so = new UnityEditor.SerializedObject(combatant);
                so.FindProperty("faction").intValue = (int)faction;
                so.ApplyModifiedPropertiesWithoutUndo();
                return body.AddComponent<Damageable>();
            }

            Damageable player = Body(Faction.Player, new Vector3(100f, 0f, 1f));
            Damageable ally = Body(Faction.Ally, new Vector3(100.6f, 0f, 1f));
            Damageable wolf = Body(Faction.Enemy, new Vector3(99.4f, 0f, 1f));
            Physics.SyncTransforms();
            var origin = new Vector3(100f, 0f, 0f);

            var hits = new List<HitDetector.Hit>();
            HitDetector.FindTargets(origin, Vector3.forward, attack, Faction.Ally, ~0, hits);
            CollectionAssert.DoesNotContain(hits.ConvertAll(h => h.Target), player, "A companion's swing passes through the player.");
            CollectionAssert.Contains(hits.ConvertAll(h => h.Target), wolf);

            hits.Clear();
            HitDetector.FindTargets(origin, Vector3.forward, attack, Faction.Player, ~0, hits);
            CollectionAssert.DoesNotContain(hits.ConvertAll(h => h.Target), ally, "The player's swing passes through companions.");
        }
        finally
        {
            foreach (GameObject body in bodies) Object.DestroyImmediate(body);
            Object.DestroyImmediate(attack);
        }
    }

    [Test]
    public void Monsters_PreferThePlayer_WhileTheyCanReachThem()
    {
        // Hunting a companion, the player comes into sight and can be reached: switch to the player.
        Assert.AreEqual(TargetPriority.Choice.Player, TargetPriority.Decide(false, true, true, false, false, false));
        // The player stands on a boulder out of reach: keep on the companion.
        Assert.AreEqual(TargetPriority.Choice.Keep, TargetPriority.Decide(false, true, false, false, false, false));
        // Hunting the player who can be reached: a nearer companion doesn't tempt it.
        Assert.AreEqual(TargetPriority.Choice.Keep, TargetPriority.Decide(true, true, true, true, true, true));
        // The player climbs out of reach and a companion can be reached: take the companion.
        Assert.AreEqual(TargetPriority.Choice.Other, TargetPriority.Decide(true, true, false, true, true, false));
        // Hunting a companion with the player out of sight: a clearly nearer companion is taken.
        Assert.AreEqual(TargetPriority.Choice.Other, TargetPriority.Decide(false, false, false, true, true, true));
    }
}
