using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>Edit Mode tests for skill-based combat: the combo chain's timing, guard and parry, poise, and that the
/// project's weapons, animations, effects and sounds are wired up.</summary>
public sealed class CombatTechniqueTests
{
    private CombatTechniqueSettings rules;

    [SetUp]
    public void SetUp() => rules = ScriptableObject.CreateInstance<CombatTechniqueSettings>();

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(rules);

    [Test]
    public void Chain_PressesAreJudgedAgainstTheSwingBefore()
    {
        // A swing that lands at 0.25 s and can chain at 0.45 s.
        Assert.AreEqual(ChainPress.Early, ComboRules.Judge(0.1f, 0.25f, 0.45f, rules), "before it lands: mashing");
        Assert.AreEqual(ChainPress.InTime, ComboRules.Judge(0.26f, 0.25f, 0.45f, rules), "landed, well before recovery");
        Assert.AreEqual(ChainPress.Rhythm, ComboRules.Judge(0.42f, 0.25f, 0.45f, rules), "right as it recovers");
        Assert.AreEqual(ChainPress.Rhythm, ComboRules.Judge(0.45f + rules.RhythmLate * 0.5f, 0.25f, 0.45f, rules));
        Assert.AreEqual(ChainPress.InTime, ComboRules.Judge(0.45f + rules.ChainGrace * 0.9f, 0.25f, 0.45f, rules), "a little late still chains");
        Assert.AreEqual(ChainPress.Late, ComboRules.Judge(0.45f + rules.ChainGrace + 0.05f, 0.25f, 0.45f, rules));
        Assert.AreEqual(ChainPress.Late, ComboRules.Judge(float.PositiveInfinity, 0.25f, 0.45f, rules), "no swing before");
    }

    [Test]
    public void Chain_WalksForward_AndStartsOverAfterTheFinisherAFumbleOrALatePress()
    {
        Assert.AreEqual(0, ComboRules.NextStep(-1, 3, ChainPress.InTime, false), "first swing");
        Assert.AreEqual(1, ComboRules.NextStep(0, 3, ChainPress.InTime, false));
        Assert.AreEqual(2, ComboRules.NextStep(1, 3, ChainPress.Rhythm, false), "to the finisher");
        Assert.AreEqual(0, ComboRules.NextStep(2, 3, ChainPress.InTime, false), "after the finisher");
        Assert.AreEqual(0, ComboRules.NextStep(1, 3, ChainPress.InTime, true), "a fumbled chain starts over");
        Assert.AreEqual(0, ComboRules.NextStep(1, 3, ChainPress.Late, false));
        Assert.AreEqual(0, ComboRules.NextStep(0, 1, ChainPress.InTime, false), "a one-swing weapon");
    }

    [Test]
    public void Mashing_OnlyEverRepeatsTheFirstSwing_WhileTimedPressesReachTheFinisher()
    {
        // Mashing: every swing gets an early press (fumble) before the in-time one that starts it.
        int step = -1;
        for (int i = 0; i < 6; i++)
        {
            step = ComboRules.NextStep(step, 3, ChainPress.InTime, fumbled: i > 0);
            Assert.AreEqual(0, step);
        }

        // Timing: on the rhythm every time.
        step = -1;
        int rhythm = 0;
        for (int i = 0; i < 3; i++)
        {
            step = ComboRules.NextStep(step, 3, ChainPress.Rhythm, false);
            rhythm = step > 0 ? ComboRules.NextRhythm(rhythm, ChainPress.Rhythm, rules) : 0;
        }

        Assert.AreEqual(2, step, "reaches the finisher");
        Assert.AreEqual(2, rhythm);
        Assert.Greater(ComboRules.DamageMultiplier(rhythm, rules), 1f);
        Assert.AreEqual(1f, ComboRules.SpeedMultiplier(rhythm, rules), 1e-5f, "deliberate by default: timing never speeds swings up");
        Assert.AreEqual(0, ComboRules.NextRhythm(rhythm, ChainPress.InTime, rules), "off the rhythm clears it");
        Assert.AreEqual(rules.MaxRhythm, ComboRules.NextRhythm(rules.MaxRhythm, ChainPress.Rhythm, rules), "capped");
    }

    [Test]
    public void Guard_ParriesJustAfterRaising_BlocksLater_AndBreaksWithoutStamina()
    {
        Vector3 facing = Vector3.forward;
        Vector3 fromFront = Vector3.back; // the hit travels toward the player's face
        float window = rules.ParryWindow;
        Assert.AreEqual(GuardOutcome.None, GuardRules.Resolve(false, 1f, facing, fromFront, rules.BlockArc, window, 100f, 10f));
        Assert.AreEqual(GuardOutcome.Parried, GuardRules.Resolve(true, window * 0.5f, facing, fromFront, rules.BlockArc, window, 100f, 10f));
        Assert.AreEqual(GuardOutcome.Blocked, GuardRules.Resolve(true, window + 0.1f, facing, fromFront, rules.BlockArc, window, 100f, 10f));
        Assert.AreEqual(GuardOutcome.GuardBroken, GuardRules.Resolve(true, window + 0.1f, facing, fromFront, rules.BlockArc, window, 5f, 10f));
        Assert.AreEqual(GuardOutcome.None, GuardRules.Resolve(true, window * 0.5f, facing, Vector3.forward, rules.BlockArc, window, 100f, 10f),
            "a hit from behind isn't covered, not even by a fresh guard");
    }

    [Test]
    public void Poise_BreaksAtZero_Refills_AndRecoversAfterADelay()
    {
        var poise = new PoiseMeter(30f, 2f, 15f);
        Assert.IsFalse(poise.Hit(10f, 0f));
        Assert.IsFalse(poise.Hit(10f, 0.5f));
        Assert.AreEqual(10f, poise.Current, 1e-4f);
        poise.Tick(1f, 1f);
        Assert.AreEqual(10f, poise.Current, 1e-4f, "no recovery right after a hit");
        poise.Tick(3f, 1f);
        Assert.AreEqual(25f, poise.Current, 1e-4f);
        Assert.IsTrue(poise.Hit(40f, 3.1f), "a heavy hit breaks it");
        Assert.AreEqual(30f, poise.Current, 1e-4f, "and it refills");
        Assert.IsFalse(poise.Hit(0f, 4f));
    }

    [Test]
    public void MonsterPose_CoilsThroughTheWindUp_LungesAtTheHit_AndSettles()
    {
        Assert.AreEqual(0f, AttackPose.Attack(0f, 0.6f).Lean, 1e-4f, "starts at rest");
        Assert.Greater(AttackPose.Attack(0.55f, 0.6f).Lean, AttackPose.Attack(0.3f, 0.6f).Lean * 2f, "the coil speeds up near the end");
        Assert.Less(AttackPose.Attack(0.55f, 0.6f).Shift, 0f, "draws back before the hit");
        Assert.Greater(AttackPose.Attack(0.6f + AttackPose.StrikeSeconds * 0.9f, 0.6f).Shift, 0.2f, "lunges at the hit");
        AttackPose.Pose settled = AttackPose.Attack(0.6f + AttackPose.StrikeSeconds + AttackPose.SettleSeconds, 0.6f);
        Assert.AreEqual(0f, settled.Lean, 1e-3f);
        Assert.AreEqual(1f, settled.Height, 1e-3f);
        Assert.AreNotEqual(0f, AttackPose.Stagger(0.1f, 1f).Sway, "staggered monsters sway");
        Assert.AreEqual(0f, AttackPose.Stagger(1f, 1f).Lean, 1e-4f, "and stop when it ends");
    }

    [Test]
    public void Focus_IsEarnedByLandingHits_MoreForFinishersHeavySwingsAndCounters()
    {
        var light = new SwingInfo(SwingKind.Light, null, 0, 3, 0, 1f, HitTechnique.None, Vector3.forward);
        var finisher = new SwingInfo(SwingKind.Light, null, 2, 3, 0, 1f, HitTechnique.Finisher, Vector3.forward);
        var heavy = new SwingInfo(SwingKind.Heavy, null, 0, 3, 0, 1f, HitTechnique.Heavy, Vector3.forward);
        var counter = new SwingInfo(SwingKind.Light, null, 0, 3, 0, 1f, HitTechnique.Counter, Vector3.forward);
        Assert.AreEqual(0f, PlayerFocus.ForSwing(light, 0, rules), "a whiff earns nothing");
        float one = PlayerFocus.ForSwing(light, 1, rules);
        Assert.Greater(one, 0f);
        Assert.AreEqual(one * 2f, PlayerFocus.ForSwing(light, 5, rules), 1e-4f, "at most two targets count");
        Assert.AreEqual(one * 2f, PlayerFocus.ForSwing(finisher, 1, rules), 1e-4f);
        Assert.AreEqual(one * 1.5f, PlayerFocus.ForSwing(heavy, 1, rules), 1e-4f);
        Assert.AreEqual(one * 1.5f, PlayerFocus.ForSwing(counter, 1, rules), 1e-4f);
        Assert.Greater(rules.FocusOnParry, one * 3f, "a parry is worth more than trading hits");
    }

    [Test]
    public void GearWeight_DecidesTheDodge()
    {
        var settings = ScriptableObject.CreateInstance<PlayerMovementSettings>();
        try
        {
            Assert.AreEqual(DodgeStyle.Quickstep, settings.StyleFor(0f));
            Assert.AreEqual(DodgeStyle.Roll, settings.StyleFor((settings.LightLoad + settings.HeavyLoad) * 0.5f));
            Assert.AreEqual(DodgeStyle.HeavyRoll, settings.StyleFor(settings.HeavyLoad + 1f));
            DodgeShape quick = settings.ShapeOf(DodgeStyle.Quickstep), heavy = settings.ShapeOf(DodgeStyle.HeavyRoll);
            Assert.Less(quick.distance * quick.stamina, heavy.distance * heavy.stamina, "light gear dodges cheaper");
            Assert.Greater(heavy.duration, quick.duration, "heavy gear dodges slower");
        }
        finally
        {
            Object.DestroyImmediate(settings);
        }
    }

    [Test]
    public void PlayerPrefab_HasAHumanBody_TheNewSystems_AndItsAnimations()
    {
        var player = AssetDatabase.LoadAssetAtPath<GameObject>(CombatAnimationSetup.PlayerPrefabPath);
        var animator = player.GetComponent<Animator>();
        Assert.IsTrue(animator.avatar.isHuman);
        Assert.AreEqual("HumanM_ModelAvatar", animator.avatar.name);
        Assert.IsNotNull(player.GetComponent<PlayerFocus>());
        Assert.IsNotNull(player.GetComponent<PlayerEating>());
        Assert.IsNotNull(player.GetComponent<PlayerGuard>());
        var controller = (UnityEditor.Animations.AnimatorController)animator.runtimeAnimatorController;
        var fullBody = controller.layers.First(l => l.name == CombatAnimationSetup.FullBodyLayer).stateMachine.states.Select(s => s.state.name).ToList();
        CollectionAssert.IsSubsetOf(new[] { "Sword A", "Sword Heavy", "Quickstep", "Roll", "Heavy Roll" }, fullBody);
        var upper = controller.layers.First(l => l.name == "Upper Body").stateMachine.states.Select(s => s.state.name).ToList();
        CollectionAssert.Contains(upper, "Consume");
    }

    [Test]
    public void QuaterniusLibraries_UseTheirOwnSetup_SoBodiesStayUpright()
    {
        foreach (string path in new[] { CombatAnimationSetup.LibraryPath, CombatAnimationSetup.Library1Path })
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            Assert.IsTrue(importer.bakeAxisConversion, path);
            Assert.AreEqual("Armature/root", importer.motionNodeName, path);
            SkeletonBone armature = importer.humanDescription.skeleton.First(b => b.name == "Armature");
            Assert.Less(Quaternion.Angle(armature.rotation, Quaternion.identity), 1f, "the avatar's stored pose matches the converted axes");
        }
    }

    [Test]
    public void Footsteps_AreSoft_AndGrassHasItsOwn()
    {
        var sounds = Resources.Load<AudioSettingsDefinition>(AudioSettingsDefinition.ResourceName);
        Assert.IsNotNull(sounds.FootstepGrass);
        foreach (SoundDefinition step in new[] { sounds.FootstepGrass, sounds.Footstep, sounds.FootstepOn("Forest Soil") })
        {
            Assert.IsTrue(step.HasClips, step.name);
            Assert.LessOrEqual(step.Volume, 0.35f, $"{step.name} is meant to be barely heard");
            Assert.IsTrue(step.Clips.All(c => AssetDatabase.GetAssetPath(c).Contains("Safe Haven Footsteps")), $"{step.name} uses the soft steps");
        }
    }

    [Test]
    public void SlashArc_IsAFlatSectorOfTheAttacksReach_AndShared()
    {
        Mesh mesh = SlashEffect.MeshFor(120f, 2f, false);
        Assert.AreEqual(2f, mesh.bounds.max.z, 0.01f);
        Assert.AreEqual(Mathf.Sin(60f * Mathf.Deg2Rad) * 2f, mesh.bounds.max.x, 0.01f);
        Assert.AreEqual(0f, mesh.bounds.size.y, 1e-4f);
        Assert.AreSame(mesh, SlashEffect.MeshFor(120f, 2f, false));
        Assert.AreNotSame(mesh, SlashEffect.MeshFor(120f, 2f, true), "the reverse sweep has its own uvs");
        Assert.AreEqual(0f, mesh.uv.Min(u => u.x), 1e-4f);
        Assert.AreEqual(1f, mesh.uv.Max(u => u.x), 1e-4f);
    }

    [Test]
    public void ProjectWeapons_HaveTimedChains_WhoseAnimationsExist()
    {
        var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(CombatAnimationSetup.PlayerControllerPath);
        var states = controller.layers.First(l => l.name == "Upper Body").stateMachine.states.Select(s => s.state.name).ToList();
        foreach (string name in new[] { "IronSword", "Fists", "StoneAxe", "Greataxe", "Spear" })
        {
            var weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>($"{CombatAnimationSetup.WeaponFolder}/{name}.asset");
            Assert.IsNotNull(weapon, name);
            Assert.GreaterOrEqual(weapon.ComboLength, 2, $"{name} has a chain");
            foreach (AttackDefinition attack in weapon.Combo.Append(weapon.HeavyAttack).Append(weapon.CounterAttack).Where(a => a != null))
            {
                Assert.IsNotEmpty(attack.AnimationState, attack.name);
                CollectionAssert.Contains(states, attack.AnimationState, $"{attack.name} plays a state the controller has");
                Assert.Less(attack.HitDelay, attack.RecoveryTime, $"{attack.name} lands before it recovers");
                Assert.Greater(attack.PoiseDamage, 0f, attack.name);
            }
        }

        var sword = AssetDatabase.LoadAssetAtPath<WeaponDefinition>($"{CombatAnimationSetup.WeaponFolder}/IronSword.asset");
        Assert.Greater(sword.HeavyAttack.PoiseDamage, sword.ComboStep(0).PoiseDamage, "the heavy swing breaks poise faster");
        Assert.Greater(sword.ComboStep(2).WeaponDamageRatio, sword.ComboStep(0).WeaponDamageRatio, "the finisher hits harder");
        CollectionAssert.IsSubsetOf(new[] { "Guard", "Guard Shield", "Parry", "Guard Break", "Knockback" }, states);
    }

    [Test]
    public void ProjectFeel_HasEffectsAndSounds_ForEveryTechnique()
    {
        CombatFeelSettings feel = CombatFeelSettings.Current;
        foreach (GameObject effect in new[] { feel.HitEffect, feel.CriticalHitEffect, feel.BlockEffect, feel.ParryEffect, feel.StaggerEffect, feel.PerfectDodgeEffect })
        {
            Assert.IsNotNull(effect);
            Assert.IsNotNull(effect.GetComponent<PooledEffect>(), $"{effect.name} returns itself to the pool");
        }

        Assert.IsNotNull(feel.SlashMaterial);
        var sounds = Resources.Load<AudioSettingsDefinition>(AudioSettingsDefinition.ResourceName);
        foreach (SoundDefinition sound in new[] { sounds.Swing, sounds.Hit, sounds.Block, sounds.Parry, sounds.GuardBreak, sounds.Stagger, sounds.Footstep, sounds.RainLoop })
        {
            Assert.IsNotNull(sound);
            Assert.IsTrue(sound.HasClips, sound.name);
        }

        Assert.AreNotSame(sounds.Footstep, sounds.FootstepOn("Forest Grass"), "grass layers get their own steps");
        Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<CombatTechniqueSettings>(CombatAnimationSetup.SettingsPath));
    }
}
