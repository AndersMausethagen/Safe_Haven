using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>Skills that go somewhere are held to aim; the rings their effects draw lie flat for the top-down camera (the user, 2026-11-25).</summary>
public sealed class SkillAimTests
{
    private static SkillDefinition Skill(string name) => AssetDatabase.LoadAssetAtPath<SkillDefinition>($"Assets/_Project/Data/Skills/{name}.asset");

    [TestCase("WolfsLunge")]
    [TestCase("MountainLeap")]
    [TestCase("FrostRune")]
    [TestCase("AshladsEmber")]
    [TestCase("HeartrootsGrasp")]
    [TestCase("TrollsHurl")]
    public void ASkillThatGoesSomewhere_IsHeldToAim(string name)
    {
        Assert.IsTrue(Skill(name).IsAimed, name);
    }

    [TestCase("WhirlingSteel")]
    [TestCase("TrollsbaneWrath")]
    [TestCase("WarCry")]
    [TestCase("HuldrasBlessing")]
    [TestCase("WeaponArt")]
    [TestCase("WintersBreath")]
    public void ARingRoundThePlayer_ABlessingOrAChannel_CastsOnThePress(string name)
    {
        Assert.IsFalse(Skill(name).IsAimed, name);
    }

    [Test]
    public void TheSkillRing_LiesFlatOnTheGround()
    {
        Vector3? facing = VfxGraphAuthoring.DirectionInput("Assets/_Project/Art/Effects/Skills/Graphs/Skill Ring.vfx", "AxisZ");
        Assert.IsTrue(facing.HasValue, "the ring's Orient block");
        Assert.Greater(Mathf.Abs(facing.Value.normalized.y), 0.99f, "the ring's quad faces up or down, not along the ground: " + facing.Value);
    }

    // The user's build, 2026-11-25: keys 2 and 6 pressed with nothing on them threw in TryCast (it read the missing skill's
    // cost) and the bar never said the slot was empty. Both ways a key casts (held to aim, and straight away) are pressed.
    [TestCase(true)]
    [TestCase(false)]
    public void AnEmptySlotsKey_IsRefusedAsEmpty(bool aimFirst)
    {
        var player = new GameObject("Empty Bar Player");
        try
        {
            var state = player.AddComponent<PlayerStateMachine>();
            var skills = player.AddComponent<PlayerSkills>();
            Call(skills, "Awake");
            Call(skills, "OnEnable");
            var refusals = new System.Collections.Generic.List<SkillCastResult>();
            skills.CastRefused += (slot, result) => refusals.Add(result);
            const int empty = 5;
            Assert.IsNull(skills.Loadout.Get(empty), "the slot starts empty");

            if (aimFirst)
            {
                state.AimSkill(empty);
            }
            else
            {
                state.RequestSkill(empty);
            }

            CollectionAssert.AreEqual(new[] { SkillCastResult.EmptySlot }, refusals, "refused once, as empty");
            Call(skills, "OnDisable");
        }
        finally
        {
            Object.DestroyImmediate(player);
        }
    }

    private static void Call(object target, string method) =>
        target.GetType().GetMethod(method, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.Invoke(target, null);

    // The user, 2026-11-25: "shorter jumps and lunge if the player wants it, but not longer than maximum".
    [Test]
    public void ALungeGoesAsFarAsAimed_NeverPastItsOwnDistance()
    {
        float most = Skill("MountainLeap").LungeDistance;
        Assert.AreEqual(3f, PlayerSkills.LungeReach(most, 3f), 1e-4f, "aimed nearer: that far");
        Assert.AreEqual(most, PlayerSkills.LungeReach(most, most + 10f), 1e-4f, "aimed past it: its own distance");
        Assert.AreEqual(most, PlayerSkills.LungeReach(most, float.PositiveInfinity), 1e-4f, "no point aimed at (a stick): all of it");
        Assert.Greater(PlayerSkills.LungeReach(most, 0f), 0f, "aimed at the feet: still a step");
    }

    // The user, 2026-11-27: "keep the player centered in the runic circle until after the effect is done".
    [Test]
    public void ACastsRuneCircle_HoldsTheCasterInItUntilItIsGone()
    {
        var look = new SkillLook();
        look.cast.Add(new SkillFxCue { prefab = new GameObject("A test's circle"), place = SkillFxPlace.CasterGround, delay = 0.2f, seconds = 1f });
        look.cast.Add(new SkillFxCue { prefab = look.cast[0].prefab, place = SkillFxPlace.Ring, seconds = 5f });
        look.release.Add(new SkillFxCue { prefab = look.cast[0].prefab, place = SkillFxPlace.CasterGround, seconds = 6f });
        try
        {
            Assert.AreEqual(1.2f, look.CasterGroundSeconds, 1e-4f, "only the cast's marks at the feet: not round the reach, not the release's");
        }
        finally
        {
            Object.DestroyImmediate(look.cast[0].prefab);
        }

        SkillDefinition winter = Skill("Fimbulwinter");
        Assert.Greater(PlayerSkills.HoldsStillFor(winter), winter.Attack.HitDelay, "still in the circle when the blow lands");
        Assert.AreEqual(0f, PlayerSkills.HoldsStillFor(Skill("SurtrsBrand")), "a lunge is the move: never held");
        Assert.AreEqual(0f, PlayerSkills.HoldsStillFor(Skill("ReapingArc")), "nothing drawn at the feet: free to walk");
        SkillDefinition blessing = Skill("HuldrasBlessing");
        Assert.Greater(blessing.Look.CasterGroundSeconds, 2f, "the pack's circle dissolves for seconds (else this checks nothing)");
        Assert.LessOrEqual(PlayerSkills.HoldsStillFor(blessing), 1.6f, "but a quick blessing never stands the player still that long");
    }

    // The hold on a real player: it plants the feet, outlasts a quicker circle, and is let go when the circle is gone, by a dodge, and
    // by a lunge (review 2026-11-27: a lunge carried the caster out and left it frozen where it landed).
    [Test]
    public void TheCirclesHold_PlantsTheFeet_AndIsLetGo()
    {
        var player = new GameObject("Held Player");
        try
        {
            var skills = player.AddComponent<PlayerSkills>();
            var movement = player.GetComponent<PlayerMovement>();
            Call(skills, "Awake");
            SkillDefinition winter = Skill("Fimbulwinter"), rune = Skill("FrostRune"), lunge = Skill("WolfsLunge");
            float hold = PlayerSkills.HoldsStillFor(winter);
            Assert.Greater(hold, PlayerSkills.HoldsStillFor(rune), "the test needs a quicker circle than the ultimate's");
            bool Held() => System.Linq.Enumerable.Contains(movement.SpeedScaleHolders, skills);

            Plant(skills, winter, 10f);
            Assert.IsTrue(Held(), "cast: the feet are planted");
            Plant(skills, rune, 10.1f);
            Keep(skills, 10f + hold - 0.05f);
            Assert.IsTrue(Held(), "a quicker circle cast inside it doesn't cut the first one short");
            Keep(skills, 10f + hold + 0.05f);
            Assert.IsFalse(Held(), "the circle gone: free to walk");

            Plant(skills, winter, 20f);
            Call(skills, "CancelCast", true);
            Assert.IsFalse(Held(), "a dodge breaks free");

            Plant(skills, winter, 30f);
            Plant(skills, lunge, 30.2f);
            Assert.IsFalse(Held(), "a lunge out of the circle lets go");

            Plant(skills, winter, 40f);
            Call(skills, "OnDisable");
            Assert.IsFalse(Held(), "put away: let go");
        }
        finally
        {
            Object.DestroyImmediate(player);
        }
    }

    private static void Plant(PlayerSkills skills, SkillDefinition skill, float now) => Call(skills, "PlantFeet", skill, now);

    private static void Keep(PlayerSkills skills, float now) => Call(skills, "KeepFeet", now);

    private static void Call(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(target, args);

    // The user, 2026-11-25: "Weapon art should change description based on which weapon we are holding": every art has its own words.
    [Test]
    public void EveryWeaponArtAndRuneArt_HasItsOwnWords()
    {
        var missing = new System.Collections.Generic.List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:WeaponDefinition"))
        {
            var weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (weapon.WeaponArt != null && string.IsNullOrWhiteSpace(weapon.WeaponArt.Description))
            {
                missing.Add($"{weapon.name}'s {weapon.WeaponArt.name}");
            }
        }

        foreach (string guid in AssetDatabase.FindAssets("t:ItemDefinition"))
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (item.RuneArt != null && string.IsNullOrWhiteSpace(item.RuneArt.Description))
            {
                missing.Add($"{item.name}'s {item.RuneArt.name}");
            }
        }

        Assert.IsEmpty(missing, "these arts would show the generic Weapon's Art text");
    }
}
