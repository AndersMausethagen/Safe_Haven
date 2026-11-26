using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The combat feel round of 2026-11-26 (Docs/CombatFeelAnalysis_2026-11-26.md; the user: "i agree with most"): the light swing's reach
/// assist, the log's names for moves and whiffs, the reeling hint's count in the journal, and the wolf's open beat before it runs.
/// </summary>
public sealed class CombatFeelTests1126
{
    private static WeaponDefinition Weapon(string name) => AssetDatabase.LoadAssetAtPath<WeaponDefinition>($"{CombatAnimationSetup.WeaponFolder}/{name}.asset");

    [Test]
    public void ReachAssist_ClosesTheGapOnly_NeverPastTheTarget()
    {
        const float lunge = 0.8f, extra = 0.6f, standOff = 0.8f;
        Assert.AreEqual(0.8f, SwingMotion.StepDistance(1.6f, lunge, 0f, standOff), 1e-4f, "without it: the lunge, as before");
        Assert.AreEqual(1.4f, SwingMotion.StepDistance(2.6f, lunge, extra, standOff), 1e-4f, "a target just out of reach: the extra closes it");
        Assert.AreEqual(1.4f, SwingMotion.StepDistance(9f, lunge, extra, standOff), 1e-4f, "never more than the lunge and the extra");
        Assert.AreEqual(0.2f, SwingMotion.StepDistance(1.0f, lunge, extra, standOff), 1e-4f, "a near target: only to the stand-off");
        Assert.AreEqual(0f, SwingMotion.StepDistance(0.5f, lunge, extra, standOff), 1e-4f, "never through the target");
        Assert.AreEqual(0.4f, SwingMotion.StepDistance(-1f, lunge, extra, standOff), 1e-4f, "no target: half the lunge, no extra");
        Assert.Greater(CombatTechniqueSettings.Current.LightReachAssist, 0f, "on in the shipped settings");
    }

    [Test]
    public void TheLogNamesTheMove_AndWhyASwingWhiffed()
    {
        WeaponDefinition sword = Weapon("IronSword");
        SwingInfo Swing(SwingKind kind, AttackDefinition attack, HitTechnique technique = HitTechnique.None) =>
            new SwingInfo(kind, attack, 0, sword.ComboLength, 0, 1f, technique, Vector3.forward);

        Assert.AreEqual(CombatTally.ChargedMove, CombatTally.MoveName(sword, Swing(SwingKind.Light, sword.ChargedAttack)));
        Assert.AreEqual(CombatTally.FollowUpMove, CombatTally.MoveName(sword, Swing(SwingKind.Heavy, sword.HeavyFollowUp)));
        Assert.AreEqual(CombatTally.BackstepMove, CombatTally.MoveName(sword, Swing(SwingKind.Rush, sword.BackstepAttack)));
        Assert.AreEqual(CombatTally.FinisherMove, CombatTally.MoveName(sword, Swing(SwingKind.Light, sword.ComboStep(sword.ComboLength - 1), HitTechnique.Finisher)));
        Assert.IsNull(CombatTally.MoveName(sword, Swing(SwingKind.Light, sword.ComboStep(0))), "a plain light swing is only Swing.Light");
        Assert.IsNull(CombatTally.MoveName(null, Swing(SwingKind.Light, sword.ComboStep(0))));

        Assert.AreEqual(CombatTally.WhiffNoFocus, CombatTally.WhiffName(2.2f, -1f));
        Assert.AreEqual(CombatTally.WhiffInReach, CombatTally.WhiffName(2.2f, 2.5f), "within reach and a body's half width");
        Assert.AreEqual(CombatTally.WhiffOutOfReach, CombatTally.WhiffName(2.2f, 3.5f), "it had backed off");
    }

    [Test]
    public void TheReelingHint_ShowsThreeTimesASave_ThenNoMore()
    {
        var progress = new CharacterProgress();
        for (int i = 0; i < ReelingHint.MostShows; i++)
        {
            Assert.AreEqual(i, ReelingHint.ShowsHad(progress.Journal));
            Assert.IsTrue(ReelingHint.NoteShow(progress.Journal), $"show {i + 1}");
        }

        Assert.IsFalse(ReelingHint.NoteShow(progress.Journal), "the save has had them all");
        Assert.AreEqual(ReelingHint.MostShows, ReelingHint.ShowsHad(progress.Journal));
        Assert.IsTrue(progress.Journal.HasNoticed(ReelingHint.ShowKey(0)), "kept in the journal's noticed set, which the save keeps");
    }

    /// <summary>
    /// The wolf's own unused clips (§3.5): the pounce is one jump (Jump_ToIdle) whose crouch plays over the wind-up and whose leap plays
    /// over the run, so the driver's take-off and landing marks must sit where the body leaves and meets the ground; and a flinch each way.
    /// </summary>
    [Test]
    public void TheWolfPouncesWithItsOwnLeap_AndFlinchesBothWays()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemies/ForestBittenWolf.prefab");
        var animator = prefab.GetComponentInChildren<Animator>();
        var controller = (UnityEditor.Animations.AnimatorController)animator.runtimeAnimatorController;
        var motions = new System.Collections.Generic.Dictionary<string, Motion>();
        foreach (var child in controller.layers[0].stateMachine.states)
        {
            motions[child.state.name] = child.state.motion;
        }

        foreach (string state in new[] { "Pounce", "Hit Left", "Hit Right", "Attack" })
        {
            Assert.IsTrue(motions.ContainsKey(state) && motions[state] != null, $"the wolf has a {state} clip");
        }

        Assert.AreNotSame(motions["Attack"], motions["Pounce"], "the pounce isn't the bite");
        Assert.AreNotSame(motions["Hit Left"], motions["Hit Right"], "a flinch each way");

        var driver = new SerializedObject(prefab.GetComponentInChildren<MonsterAnimationDriver>());
        float launch = driver.FindProperty("pounceLaunchAt").floatValue, land = driver.FindProperty("pounceLandAt").floatValue;
        var clip = (AnimationClip)motions["Pounce"];
        Assert.Less(launch, land);
        Assert.Less(land, clip.length, "it lands before its clip ends, and settles after");

        GameObject model = Object.Instantiate(animator.gameObject);
        try
        {
            Transform body = FindBone(model.transform, "Body");
            Assert.IsNotNull(body, "the wolf's Body bone");
            float HeightAt(float t)
            {
                clip.SampleAnimation(model, t);
                return model.transform.InverseTransformPoint(body.position).y;
            }

            float atLaunch = HeightAt(launch), atLand = HeightAt(land), midLeap = HeightAt((launch + land) * 0.5f);
            Assert.Greater(midLeap - Mathf.Max(atLaunch, atLand), 0.4f, "airborne between the marks, near the ground at them");
        }
        finally
        {
            Object.DestroyImmediate(model);
        }
    }

    private static Transform FindBone(Transform at, string name)
    {
        if (at.name == name)
        {
            return at;
        }

        foreach (Transform child in at)
        {
            Transform found = FindBone(child, name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    [Test]
    public void TheWolfStandsOpenAfterItsBite_BeforeItRunsOff()
    {
        var wolf = AssetDatabase.LoadAssetAtPath<MonsterDefinition>("Assets/_Project/Data/Monsters/ForestBittenWolf.asset");
        MonsterAttack bite = null;
        foreach (MonsterAttack attack in wolf.Attacks)
        {
            if (attack.attack != null && attack.attack.name == "WolfHamstringBite")
            {
                bite = attack;
            }
        }

        Assert.IsNotNull(bite, "the wolf bites");
        Assert.Greater(bite.breakAway, 0f, "it still bites and runs");
        // A reflex answer (about 0.25 s to react) plus the axe's quickest swing (about 0.25 s) has to land before it runs.
        float openAfterBlow = bite.attack.RecoveryTime - bite.attack.HitDelay + bite.breakAwayDelay;
        Assert.GreaterOrEqual(openAfterBlow, 0.7f, $"it stands open {openAfterBlow:0.00} s after its bite lands");
    }
}
