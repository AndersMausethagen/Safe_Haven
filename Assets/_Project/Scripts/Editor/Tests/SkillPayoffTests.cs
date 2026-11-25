using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

/// <summary>
/// What an ability is worth (the user's playtest, 2026-11-25: "the skills are incredibly underwhelming and deal almost no damage"):
/// a Focus circle took about a dozen landed swings to earn and bought back one and a half. Every ability's blow now strikes well
/// above a light swing, grows with the weapon, costs no stamina (the dodge's, when surrounded), and grants a buff of its own.
/// </summary>
public sealed class SkillPayoffTests
{
    // A light swing's worth, against what an ability's blow strikes each foe for: the old Whirling Steel was 1.6.
    private const float LeastOverALightSwing = 2.5f;

    private static IEnumerable<SkillDefinition> Abilities() =>
        AssetDatabase.FindAssets("t:SkillDefinition", new[] { "Assets/_Project/Data/Skills" })
            .Select(guid => AssetDatabase.LoadAssetAtPath<SkillDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(skill => skill != null && skill.WeaponArt == WeaponArtSlot.None);

    private static WeaponDefinition CopperSword() =>
        AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/_Project/Data/Combat/Weapons/CopperSword.asset");

    private static float Blow(AttackDefinition attack, float weaponDamage) => attack.BaseDamage + weaponDamage * attack.WeaponDamageRatio;

    [Test]
    public void TheAbilitiesAreFound()
    {
        Assert.GreaterOrEqual(Abilities().Count(), 8, "the abilities in Data/Skills");
        Assert.IsNotNull(CopperSword(), "the first sword, to measure against");
    }

    [Test]
    public void EveryAbilitysBlow_StrikesWellAboveALightSwing()
    {
        WeaponDefinition sword = CopperSword();
        float light = Blow(sword.ComboStep(0), sword.Damage);
        Assert.Greater(light, 0f);
        foreach (SkillDefinition skill in Abilities().Where(skill => skill.Attack != null && skill.ChannelSeconds <= 0f))
        {
            float worth = Blow(skill.Attack, sword.Damage) / light;
            Assert.GreaterOrEqual(worth, LeastOverALightSwing, $"{skill.name} strikes each foe for {worth:0.0} light swings");
        }
    }

    // A channel strikes every tick: what one breath deals in all is measured instead.
    [Test]
    public void AChannel_DealsWellAboveALightSwingOverItsBreath()
    {
        WeaponDefinition sword = CopperSword();
        float light = Blow(sword.ComboStep(0), sword.Damage);
        foreach (SkillDefinition skill in Abilities().Where(skill => skill.Attack != null && skill.ChannelSeconds > 0f))
        {
            int strikes = 1 + UnityEngine.Mathf.FloorToInt(skill.ChannelSeconds / skill.ChannelTick);
            float worth = Blow(skill.Attack, sword.Damage) * strikes / light;
            Assert.GreaterOrEqual(worth, LeastOverALightSwing, $"{skill.name} deals {worth:0.0} light swings over a full breath");
        }
    }

    // Frost Rune and Ember dealt a fixed number, so a better weapon left them further behind each tier.
    [Test]
    public void EveryAbilitysBlow_GrowsWithTheWeapon()
    {
        foreach (SkillDefinition skill in Abilities().Where(skill => skill.Attack != null))
        {
            Assert.Greater(skill.Attack.WeaponDamageRatio, 0f, $"{skill.name} grows with the weapon in hand");
        }
    }

    [Test]
    public void NoAbility_CostsStamina_FocusIsItsPrice()
    {
        foreach (SkillDefinition skill in Abilities())
        {
            Assert.AreEqual(0f, skill.StaminaCost, $"{skill.name}: stamina is the dodge's");
            Assert.GreaterOrEqual(skill.FocusCircles, 1, $"{skill.name} still takes a Focus circle");
        }
    }

    [Test]
    public void EveryAbility_GrantsABuffOfItsOwn()
    {
        var seen = new Dictionary<string, string>();
        var names = new HashSet<string>();
        foreach (SkillDefinition skill in Abilities())
        {
            Assert.IsTrue(skill.HasBuffs, $"{skill.name} grants a buff");
            Assert.AreNotEqual(skill.DisplayName, skill.BuffName, $"{skill.name}'s buff has its own name");
            Assert.IsTrue(names.Add(skill.BuffName), $"{skill.name}'s buff name {skill.BuffName} is taken");
            foreach (SkillStatBuff buff in skill.Buffs)
            {
                Assert.AreNotEqual(0f, buff.value, $"{skill.name}: a buff does something");
                Assert.IsFalse(StatNames.IsPenalty(buff.stat, buff.value), $"{skill.name}: a buff helps ({buff.stat} {buff.value})");
            }

            string kinds = string.Join(",", skill.Buffs.Select(buff => buff.stat).OrderBy(stat => stat));
            Assert.IsFalse(seen.TryGetValue(kinds, out string other), $"{skill.name} grants what {other} grants ({kinds})");
            seen[kinds] = skill.name;
        }
    }

    [Test]
    public void TheCard_NamesTheBuff_InTheStatsOwnUnits()
    {
        SkillDefinition steel = AssetDatabase.LoadAssetAtPath<SkillDefinition>("Assets/_Project/Data/Skills/WhirlingSteel.asset");
        string card = SkillText.Describe(steel, -1);
        StringAssert.Contains(steel.BuffName, card);
        StringAssert.Contains(new StatBonus(steel.Buffs[0].stat, steel.Buffs[0].value, steel.Buffs[0].op).Describe(), card);
        StringAssert.DoesNotContain("Stamina", card, "no stamina cost is shown");
    }

    // Focus from being hit, on a real player body: a blow from something earns by the health it took; a tick of burning, the
    // player's own doing and a blow from nowhere earn nothing; refunds (Gain) are not counted as earned.
    [Test]
    public void ABlowTaken_EarnsFocus_ButNotATickTheirOwnOrASourcelessOne()
    {
        var body = new UnityEngine.GameObject("Focus Probe Player");
        var striker = new UnityEngine.GameObject("Focus Probe Striker");
        try
        {
            body.AddComponent<Combatant>();
            var health = body.AddComponent<Damageable>();
            var focus = body.AddComponent<PlayerFocus>();
            Call(health, "Awake");
            Call(focus, "Awake");
            Call(focus, "OnEnable");
            var source = striker.AddComponent<Combatant>();
            float earned = 0f;
            focus.Earned += amount => earned += amount;

            DamageInfo Blow(Combatant from, float amount, HitTechnique technique) => new DamageInfo(from, null, amount, DamageType.Physical,
                false, UnityEngine.Vector3.zero, UnityEngine.Vector3.forward, 0f, technique);

            float tenth = health.MaxHealth * 0.1f;
            health.ApplyDamage(Blow(source, tenth, HitTechnique.OverTime));
            health.ApplyDamage(Blow(body.GetComponent<Combatant>(), tenth, HitTechnique.None));
            health.ApplyDamage(Blow(null, tenth, HitTechnique.None));
            Assert.AreEqual(0f, focus.Current, 1e-4f, "a tick, the player's own blow and a sourceless one earn nothing");

            DamageResult struck = health.ApplyDamage(Blow(source, tenth, HitTechnique.None));
            Assert.Greater(struck.HealthLost, 0f, "the probe blow got through");
            float expected = PlayerFocus.ForHurt(struck.HealthLost, health.MaxHealth, CombatTechniqueSettings.Current);
            Assert.Greater(expected, 0f);
            Assert.AreEqual(expected, focus.Current, 1e-3f, "a blow from something earns by the share of health it took (no crowd here)");
            Assert.AreEqual(expected, earned, 1e-3f, "and is counted as earned");

            focus.Gain(10f);
            Assert.AreEqual(expected, earned, 1e-3f, "a refund or gift is not counted as earned");
            Call(focus, "OnDisable");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(body);
            UnityEngine.Object.DestroyImmediate(striker);
        }
    }

    private static void Call(object target, string method) =>
        target.GetType().GetMethod(method, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.Invoke(target, null);

    [Test]
    public void AnAbilitysBlow_IsMarkedAsAnAbility_SoTheLogCanTellItFromASwing()
    {
        SkillDefinition steel = AssetDatabase.LoadAssetAtPath<SkillDefinition>("Assets/_Project/Data/Skills/WhirlingSteel.asset");
        SkillDefinition frost = AssetDatabase.LoadAssetAtPath<SkillDefinition>("Assets/_Project/Data/Skills/FrostRune.asset");
        HitTechnique physical = PlayerSkills.AbilityTechnique(steel), spell = PlayerSkills.AbilityTechnique(frost);
        Assert.AreEqual(HitTechnique.Melee | HitTechnique.Heavy | HitTechnique.Ability, physical, "a heavy melee blow, marked");
        Assert.AreEqual(HitTechnique.Spell | HitTechnique.Ability, spell, "a spell, marked");

        var hit = new DamageInfo(null, steel.Attack, 10f, DamageType.Fire, false, UnityEngine.Vector3.zero, UnityEngine.Vector3.forward, 0f, spell);
        Assert.AreEqual("skill", BossTryTally.KindOf(hit), "an ability's fire is the ability's, not the fire's");
        Assert.AreEqual("skill", BossTryTally.Kinds[BossTryTally.Kinds.Length - 1], "added last, so old readers keep their columns");
    }
}
