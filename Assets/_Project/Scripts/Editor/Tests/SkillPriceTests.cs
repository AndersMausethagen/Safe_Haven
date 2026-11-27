using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Skills as a choice, not the strongest hit spammed (the user, 2026-11-27: "the meta is to just put all skills on the bar and spam the
/// one that does the most damage"; Docs/SkillChoiceResearch_2026-11-27.md phase 1): techniques free of Focus on their cooldown, powers
/// one or two circles, ultimates the full bar; a bar whose slots each take a kind; and the playtest log saying how each is used.
/// </summary>
public sealed class SkillPriceTests
{
    // A copper weapon's damage, at which the 11-25 payoff was tuned (SkillPayoffTests).
    private const float CopperWeapon = 15f;

    private static List<SkillDefinition> Skills() => AssetDatabase.FindAssets("t:SkillDefinition", new[] { "Assets/_Project/Data/Skills" })
        .Select(g => AssetDatabase.LoadAssetAtPath<SkillDefinition>(AssetDatabase.GUIDToAssetPath(g))).ToList();

    private static SkillDefinition Skill(string name) => AssetDatabase.LoadAssetAtPath<SkillDefinition>("Assets/_Project/Data/Skills/" + name + ".asset");

    private static float DamagePerFoe(SkillDefinition skill)
    {
        AttackDefinition attack = skill.Attack;
        float blow = attack.BaseDamage + CopperWeapon * attack.WeaponDamageRatio;
        return skill.ChannelSeconds > 0f ? blow * Mathf.Floor(skill.ChannelSeconds / skill.ChannelTick) : blow;
    }

    private static SkillDefinition Make(SkillKind kind)
    {
        var skill = ScriptableObject.CreateInstance<SkillDefinition>();
        skill.name = kind.ToString();
        var data = new SerializedObject(skill);
        data.FindProperty("kind").intValue = (int)kind;
        data.ApplyModifiedPropertiesWithoutUndo();
        return skill;
    }

    private static SkillDefinition MakeArt()
    {
        var art = ScriptableObject.CreateInstance<SkillDefinition>();
        var data = new SerializedObject(art);
        data.FindProperty("weaponArt").intValue = (int)WeaponArtSlot.Own;
        data.ApplyModifiedPropertiesWithoutUndo();
        return art;
    }

    [Test]
    public void EachKind_HasItsOwnPrice()
    {
        foreach (SkillDefinition skill in Skills())
        {
            switch (skill.Kind)
            {
                case SkillKind.Technique:
                    Assert.AreEqual(0, skill.FocusCircles, $"{skill.name}: a technique costs no Focus, only its cooldown");
                    Assert.LessOrEqual(skill.Cooldown, 14f, $"{skill.name}: and comes round often");
                    break;
                case SkillKind.Power:
                    Assert.That(skill.FocusCircles, Is.InRange(1, 2), $"{skill.name}: a power spends one circle, two for the strongest");
                    break;
                case SkillKind.Ultimate:
                    Assert.AreEqual(3, skill.FocusCircles, $"{skill.name}: an ultimate takes the full bar");
                    Assert.GreaterOrEqual(skill.Cooldown, 30f, $"{skill.name}: on a long cooldown");
                    break;
                case SkillKind.Art:
                    Assert.AreNotEqual(WeaponArtSlot.None, skill.WeaponArt, $"{skill.name}: only a weapon art's entry is an Art");
                    break;
            }
        }

        Assert.AreEqual(SkillKind.Technique, Skill("WolfsLunge").Kind);
        Assert.AreEqual(SkillKind.Ultimate, Skill("TrollsbaneWrath").Kind);
        Assert.AreEqual(2, Skill("WintersBreath").FocusCircles, "the strongest power costs two");
        Assert.AreEqual(SkillKind.Art, Skill("WeaponArt").Kind);
    }

    [Test]
    public void TheMostDamagePerPress_IsNotTheOnlyAnswer()
    {
        List<SkillDefinition> skills = Skills();
        List<SkillDefinition> techniques = skills.Where(s => s.Kind == SkillKind.Technique && s.Attack != null).ToList();
        List<SkillDefinition> powers = skills.Where(s => s.Kind == SkillKind.Power && s.Attack != null).ToList();
        Assert.GreaterOrEqual(techniques.Count, 14, "the four that came first and a technique for each kind of weapon");
        // Against a plain damage power (Frost Rune): a free technique's own blow is well under it. Its worth is in setting up or paying off
        // (powers that pay off, Wildfire, hit lightly themselves for the same reason).
        float plainPower = DamagePerFoe(Skill("FrostRune"));
        foreach (SkillDefinition technique in techniques)
        {
            Assert.Less(DamagePerFoe(technique), plainPower * 0.75f,
                $"{technique.name}: free techniques hit lighter than a power that spends Focus ({DamagePerFoe(technique):0} against {plainPower:0})");
        }

        // The ultimate slam is worth its full bar, and topples as it looks (it did 10 poise, the least of any slam).
        SkillDefinition wrath = Skill("TrollsbaneWrath");
        Assert.Greater(DamagePerFoe(wrath), powers.Where(p => p.ChannelSeconds <= 0f).Max(DamagePerFoe), "a full bar's blow outdoes a single power's");
        Assert.GreaterOrEqual(wrath.Attack.PoiseDamage, Skill("TrollsHurl").Attack.PoiseDamage);
        Assert.GreaterOrEqual(wrath.Attack.PoiseDamage, Skill("MountainLeap").Attack.PoiseDamage);
    }

    // ----- The new skills (the user, 2026-11-27: "create new skills for each weapon type and a few magic abilities. make them tie nicely
    // into each other. some can set up, while others do the damage") -----

    private static IEnumerable<StatusEffectDefinition> SetsUp(SkillDefinition skill)
    {
        if (skill.Attack != null && skill.Attack.AppliesStatus != null) yield return skill.Attack.AppliesStatus;
        if (skill.Attack != null && skill.Attack.OnRepeatStatus != null) yield return skill.Attack.OnRepeatStatus;
        if (skill.FoesStatus != null) yield return skill.FoesStatus;
        foreach (SkillOption combo in skill.Combos.Where(c => c != null))
        {
            if (combo.addsStatus != null) yield return combo.addsStatus;
            if (combo.consumeInto != null) yield return combo.consumeInto;
        }
    }

    [Test]
    public void EveryKindOfWeapon_TeachesATechniqueOfItsOwn()
    {
        var settings = AssetDatabase.LoadAssetAtPath<PlayerSkillSettings>("Assets/_Project/Data/Player/PlayerSkillSettings.asset");
        foreach (WeaponSignature kind in System.Enum.GetValues(typeof(WeaponSignature)))
        {
            if (kind == WeaponSignature.None)
            {
                continue;
            }

            SkillDefinition technique = settings.TechniqueOf(kind);
            Assert.IsNotNull(technique, $"{kind}: a technique of its own");
            Assert.AreEqual(SkillKind.Technique, technique.Kind);
            Assert.IsTrue(technique.NeedsWeapon, $"{technique.name} is cast with its weapon in hand");
            Assert.IsNotNull(technique.Icon, $"{technique.name} has its icon");
            Assert.AreEqual(1, settings.WeaponTechniques.Count(t => t.Weapons.Contains(kind)), $"{kind}: one technique, not several");
        }

        Assert.AreEqual(2, settings.LearnAtTier, "a copper weapon teaches it, as V Rising's weapons do by tier");
    }

    [Test]
    public void EveryStatusSetUp_HasAPayoff_AndEveryPayoff_ASetUp()
    {
        List<SkillDefinition> skills = Skills();
        var laid = new HashSet<StatusEffectDefinition>(skills.SelectMany(SetsUp));
        var paid = new HashSet<StatusEffectDefinition>(skills.SelectMany(s => s.Combos.Where(c => c != null && c.consumes != null).Select(c => c.consumes)));
        foreach (StatusEffectDefinition status in paid)
        {
            Assert.IsTrue(laid.Contains(status), $"{status.name} is paid off, so something must lay it");
            Assert.GreaterOrEqual(skills.Count(s => SetsUp(s).Contains(status)), 2, $"{status.name}: more than one way to lay it, so the payoff isn't tied to one skill");
        }

        // What the weapons' techniques lay is paid off by another skill (Frozen is shattered by any heavy blow, its own rule).
        foreach (SkillDefinition technique in skills.Where(s => s.NeedsWeapon))
        {
            foreach (StatusEffectDefinition status in SetsUp(technique).Where(s => s.name != "Frozen" && s.name != "Frostbite" && s.name != "Hamstrung"))
            {
                Assert.IsTrue(paid.Contains(status), $"{technique.name} lays {status.name}: some skill must pay it off");
            }
        }

        // Each weapon either sets up or pays off, and both kinds exist among them.
        List<SkillDefinition> weaponSkills = skills.Where(s => s.NeedsWeapon).ToList();
        Assert.GreaterOrEqual(weaponSkills.Count(s => s.Combos.Any(c => c != null && c.consumes != null)), 4, "weapons that pay off");
        Assert.GreaterOrEqual(weaponSkills.Count(s => s.Attack != null && s.Attack.AppliesStatus != null), 4, "weapons that set up");
        foreach (string power in new[] { "Wildfire", "Hoarfrost", "ElfShot" })
        {
            SkillDefinition skill = Skill(power);
            Assert.AreEqual(SkillKind.Power, skill.Kind, power);
            Assert.IsFalse(skill.NeedsWeapon, $"{power}: any weapon");
            Assert.IsTrue(AssetDatabase.FindAssets("t:QuestDefinition").Select(g => AssetDatabase.LoadAssetAtPath<QuestDefinition>(AssetDatabase.GUIDToAssetPath(g)))
                .Any(q => q.RewardSkills.Contains(skill)), $"{power} has a teacher");
        }
    }

    [Test]
    public void AWeaponsOwnTechnique_WantsItsWeapon_AndAGeneralOne_FavoursItsWeapons()
    {
        var sword = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/_Project/Data/Combat/Weapons/CopperSword.asset");
        Assert.AreEqual(WeaponSignature.CounterChain, sword.Signature);
        Assert.IsTrue(Skill("CrossingCut").CastsWith(sword));
        Assert.IsFalse(Skill("OpenTheVein").CastsWith(sword), "a dagger's technique, not with a sword");
        Assert.IsFalse(Skill("OpenTheVein").CastsWith(null), "nor with nothing in hand");
        Assert.IsTrue(Skill("WolfsLunge").CastsWith(null), "a general technique goes with anything");
        Assert.AreEqual(1.25f, Skill("WolfsLunge").WeaponScale(sword), 0.001f, "and strikes harder with a weapon it suits");
        Assert.AreEqual(1f, Skill("MountainLeap").WeaponScale(sword), 0.001f);
        Assert.AreEqual(1f, Skill("CrossingCut").WeaponScale(sword), 0.001f, "a weapon's own technique is already its weapon's");
    }

    [Test]
    public void EachBarSlot_TakesItsKind_AndSaysSo()
    {
        SkillDefinition art = MakeArt(), lunge = Make(SkillKind.Technique), leap = Make(SkillKind.Technique), rune = Make(SkillKind.Power),
            ember = Make(SkillKind.Power), wrath = Make(SkillKind.Ultimate);
        try
        {
            var bar = new SkillLoadout();
            Assert.IsFalse(bar.Equip(0, rune), "slot 1 is the weapon's art");
            Assert.IsTrue(bar.Equip(0, art));
            Assert.IsFalse(bar.Equip(3, lunge), "a technique doesn't go in a power's slot");
            Assert.IsTrue(bar.Equip(1, lunge));
            Assert.IsTrue(bar.Equip(2, leap));
            Assert.IsFalse(bar.Equip(3, wrath), "the ultimate only in slot 5");
            Assert.IsTrue(bar.Equip(3, rune));
            Assert.IsTrue(bar.Equip(4, wrath));
            Assert.IsFalse(bar.Equip(5, ember), "6-9 hold consumables only");
            Assert.IsTrue(bar.Move(1, 2), "two techniques swap");
            Assert.AreSame(leap, bar.Get(1));
            Assert.IsFalse(bar.Move(3, 4), "a power can go in 5, but the ultimate can't come to 4");
            Assert.IsTrue(bar.Equip(4, ember), "slot 5 takes a power too (the ultimate goes back to the book)");
            Assert.AreEqual("Power or Ultimate", SkillLoadout.SlotName(4));
            Assert.AreEqual("Tech", SkillLoadout.SlotBadge(1));
            Assert.AreEqual(-1, bar.FirstFittingEmpty(lunge), "no technique slot free once both are taken");
        }
        finally
        {
            foreach (SkillDefinition skill in new[] { art, lunge, leap, rune, ember, wrath })
            {
                Object.DestroyImmediate(skill);
            }
        }
    }

    [Test]
    public void ABarSavedBeforeSlotsHadKinds_IsSortedIntoThem()
    {
        SkillDefinition art = MakeArt(), lunge = Make(SkillKind.Technique), rune = Make(SkillKind.Power), wrath = Make(SkillKind.Ultimate),
            spin = Make(SkillKind.Technique), cry = Make(SkillKind.Ultimate);
        try
        {
            var bar = new SkillLoadout();
            // An old bar: the ultimate on 2, a power on 1, the art on 4, a technique on 7, a second ultimate on 3.
            bar.SetAll(new[] { rune, wrath, cry, art, null, null, lunge, null, null, });
            Assert.AreSame(art, bar.Get(0), "the art to its slot");
            Assert.AreSame(rune, bar.Get(3), "the power to the power's slot");
            Assert.AreSame(wrath, bar.Get(4), "the first ultimate to slot 5");
            Assert.AreSame(lunge, bar.Get(1), "the technique to a technique's slot");
            Assert.AreEqual(-1, bar.IndexOf(cry), "a second ultimate has no slot left: back to the book, not lost (it stays known)");
            for (int i = 0; i < SkillLoadout.SlotCount; i++)
            {
                Assert.IsTrue(SkillLoadout.Fits(i, bar.Get(i)), $"slot {i + 1} holds what it takes");
            }

            Assert.IsNull(bar.Get(2));
            Assert.IsTrue(bar.Equip(2, spin));
        }
        finally
        {
            foreach (SkillDefinition skill in new[] { art, lunge, rune, wrath, spin, cry })
            {
                Object.DestroyImmediate(skill);
            }
        }
    }

    [Test]
    public void AnOldBar_KeepsItsConsumables_WhileItsSkillsAreSorted()
    {
        SkillDefinition spin = Make(SkillKind.Technique), rune = Make(SkillKind.Power), lunge = Make(SkillKind.Technique),
            hurl = Make(SkillKind.Technique), power = Make(SkillKind.Power), wrath = Make(SkillKind.Ultimate);
        var bandage = ScriptableObject.CreateInstance<ItemDefinition>();
        try
        {
            // The review's case: [Whirling Steel, Frost Rune, Bandage, Wolf's Lunge, Troll's Hurl]; a misplaced skill took slot 3, which looked
            // free, and the bandage saved there was dropped.
            var bar = new SkillLoadout();
            var items = new ItemDefinition[SkillLoadout.SlotCount];
            items[2] = bandage;
            bar.SetAll(new[] { spin, rune, null, lunge, hurl }, items);
            bar.SetItems(items);
            Assert.AreSame(bandage, bar.GetItem(2), "the bandage stays on 3");
            Assert.AreSame(spin, bar.Get(1), "a technique to 2, the one free technique slot");
            Assert.AreSame(rune, bar.Get(3), "the power to 4");
            Assert.AreEqual(-1, bar.IndexOf(lunge), "no technique slot left (the bandage holds 3): it waits in the book, still known");
            Assert.AreEqual(-1, bar.IndexOf(hurl));

            // A skill already on the bar dropped where the swap can't go (a power from 4 onto the ultimate's 5): it moves, the ultimate
            // goes back to the book (the review: nothing happened).
            var two = new SkillLoadout();
            Assert.IsTrue(two.Equip(3, power));
            Assert.IsTrue(two.Equip(4, wrath));
            Assert.IsTrue(two.Equip(4, power));
            Assert.AreSame(power, two.Get(4));
            Assert.IsNull(two.Get(3));
            Assert.AreEqual(-1, two.IndexOf(wrath));
            // And a consumable from 7 onto a technique's slot: it moves there, the technique goes back to the book.
            Assert.IsTrue(two.Equip(1, lunge));
            Assert.IsTrue(two.EquipItem(6, bandage));
            Assert.IsTrue(two.EquipItem(1, bandage));
            Assert.AreSame(bandage, two.GetItem(1));
            Assert.IsNull(two.GetItem(6));
            Assert.AreEqual(-1, two.IndexOf(lunge));
        }
        finally
        {
            foreach (SkillDefinition skill in new[] { spin, rune, lunge, hurl, power, wrath })
            {
                Object.DestroyImmediate(skill);
            }

            Object.DestroyImmediate(bandage);
        }
    }

    private static PlaytestEntry Row(float minutes, string eventName, string subject, int amount, string detail = "") => new PlaytestEntry
    {
        PlayedSeconds = minutes * 60f, Event = eventName, Subject = subject, Amount = amount, Detail = detail,
    };

    [Test]
    public void ThePlaytestLog_SaysHowEachSkillIsUsed_AndFlagsADefault()
    {
        var run = new List<PlaytestEntry>();
        for (int raid = 0; raid < 5; raid++)
        {
            run.Add(Row(raid * 10f, PlaytestLog.Loadout, raid < 4 ? "WeaponArt|WolfsLunge|-|FrostRune|TrollsbaneWrath" : "WeaponArt|-|-|AshladsEmber|-", 3));
            run.Add(Row(raid * 10f + 1f, PlaytestLog.Combat, CombatTally.SkillCast, 3));
            run.Add(Row(raid * 10f + 1f, PlaytestLog.Combat, CombatTally.SkillOf + "FrostRune", 2));
            run.Add(Row(raid * 10f + 1f, PlaytestLog.Combat, CombatTally.SkillDamageOf + "FrostRune", 90));
            run.Add(Row(raid * 10f + 1f, PlaytestLog.Combat, CombatTally.SkillOf + "WolfsLunge", 1));
            run.Add(Row(raid * 10f + 1f, PlaytestLog.Combat, CombatTally.SkillDamageOf + "WolfsLunge", 10));
        }

        PlaytestSkillUse use = PlaytestLog.SkillUse(new[] { (IReadOnlyList<PlaytestEntry>)run });
        Assert.AreEqual(5, use.Loadouts);
        Assert.AreEqual(5, use.FightingMinutes);
        PlaytestSkillUse.Skill frost = use.Skills["FrostRune"];
        Assert.AreEqual(4, frost.InLoadouts);
        Assert.AreEqual(10, frost.Casts, "casts by skill, not the tally's total");
        Assert.AreEqual(0.9f, use.DamageShare(frost), 0.001f);
        Assert.AreEqual("most of the skill damage", use.Flag(frost));
        Assert.AreEqual(2f, use.CastsPerFightingMinute(frost), 0.001f);
        Assert.IsFalse(use.Skills.ContainsKey("Cast"), "Skill.Cast is the total, not a skill");
        Assert.AreEqual(1, use.Skills["AshladsEmber"].InLoadouts);
        Assert.IsNull(use.Flag(use.Skills["AshladsEmber"]), "on one bar in five: chosen sometimes");

        Assert.IsFalse(PlaytestLog.Cheated(run));
        run.Add(Row(60f, PlaytestLog.Cheat, "Endless Focus", 0));
        Assert.IsTrue(PlaytestLog.Cheated(run), "a helped run is left out of the table");
    }
}
