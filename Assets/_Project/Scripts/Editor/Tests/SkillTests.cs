using NUnit.Framework;
using UnityEngine;

public sealed class SkillTests
{
    private SkillDefinition a;
    private SkillDefinition b;
    private SkillDefinition c;

    [SetUp]
    public void SetUp()
    {
        a = ScriptableObject.CreateInstance<SkillDefinition>();
        b = ScriptableObject.CreateInstance<SkillDefinition>();
        c = ScriptableObject.CreateInstance<SkillDefinition>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(a);
        Object.DestroyImmediate(b);
        Object.DestroyImmediate(c);
    }

    [Test]
    public void LoadoutHasFiveEmptySlots()
    {
        var loadout = new SkillLoadout();
        Assert.AreEqual(5, SkillLoadout.SlotCount);
        Assert.AreEqual(0, loadout.FirstEmptySlot());
        Assert.IsNull(loadout.Get(4));
        Assert.IsNull(loadout.Get(5));
    }

    [Test]
    public void EquippingASlottedSkillMovesItAndSwaps()
    {
        var loadout = new SkillLoadout();
        loadout.Equip(0, a);
        loadout.Equip(1, b);

        Assert.IsTrue(loadout.Equip(1, a));
        Assert.AreEqual(a, loadout.Get(1));
        Assert.AreEqual(b, loadout.Get(0));
        Assert.AreEqual(1, loadout.IndexOf(a));
    }

    [Test]
    public void MoveSwapsAndClearEmpties()
    {
        var loadout = new SkillLoadout();
        loadout.Equip(0, a);
        int changes = 0;
        loadout.SlotChanged += _ => changes++;

        Assert.IsTrue(loadout.Move(0, 3));
        Assert.IsNull(loadout.Get(0));
        Assert.AreEqual(a, loadout.Get(3));
        Assert.AreEqual(2, changes);

        Assert.IsTrue(loadout.Clear(3));
        Assert.IsFalse(loadout.Clear(3));
        Assert.IsFalse(loadout.Move(2, 4));
    }

    [Test]
    public void SetAllDropsDuplicates()
    {
        var loadout = new SkillLoadout();
        loadout.SetAll(new[] { a, a, b, null, c });
        Assert.AreEqual(a, loadout.Get(0));
        Assert.IsNull(loadout.Get(1));
        Assert.AreEqual(b, loadout.Get(2));
        Assert.AreEqual(c, loadout.Get(4));
    }

    [Test]
    public void CooldownBelongsToTheSkillAndRunsOut()
    {
        var cooldowns = new SkillCooldowns();
        cooldowns.Start(a, 10f, 4f);

        Assert.IsFalse(cooldowns.IsReady(a, 11f));
        Assert.IsTrue(cooldowns.IsReady(b, 11f));
        Assert.AreEqual(3f, cooldowns.Remaining(a, 11f), 0.001f);
        Assert.AreEqual(0.5f, cooldowns.Fraction(a, 12f), 0.001f);
        Assert.IsTrue(cooldowns.IsReady(a, 14f));
        Assert.AreEqual(0f, cooldowns.Fraction(a, 15f));
    }
}
