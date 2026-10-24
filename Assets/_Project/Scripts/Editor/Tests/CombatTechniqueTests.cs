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
