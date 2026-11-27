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
    public void ASlotHoldsAnItemOrASkill_AndEachIsInOneSlotAtMost()
    {
        var loadout = new SkillLoadout();
        var potion = ScriptableObject.CreateInstance<ItemDefinition>();
        var bread = ScriptableObject.CreateInstance<ItemDefinition>();
        try
        {
            // a, b and c are powers (the default kind): slots 4 and 5 take them (SkillPriceTests has the kinds).
            loadout.Equip(3, a);
            Assert.IsTrue(loadout.EquipItem(3, potion), "an item takes a skill's slot");
            Assert.IsNull(loadout.Get(3));
            Assert.AreEqual(potion, loadout.GetItem(3));
            Assert.IsFalse(loadout.IsEmpty(3), "a slot with an item isn't empty");

            Assert.IsTrue(loadout.EquipItem(6, potion), "a slotted item moves");
            Assert.IsNull(loadout.GetItem(3));
            Assert.AreEqual(potion, loadout.GetItem(6));

            loadout.EquipItem(3, potion);
            loadout.Equip(4, b);
            Assert.IsTrue(loadout.Move(3, 4), "an item and a skill swap places");
            Assert.AreEqual(b, loadout.Get(3));
            Assert.AreEqual(potion, loadout.GetItem(4));

            Assert.IsTrue(loadout.Equip(4, c), "a skill takes an item's slot");
            Assert.IsNull(loadout.GetItem(4));
            Assert.AreEqual(-1, loadout.IndexOf(potion));

            loadout.EquipItem(5, bread);
            Assert.IsTrue(loadout.Clear(5));
            Assert.IsNull(loadout.GetItem(5));

            // Restoring: skills first, then the items in the slots no skill took (duplicates dropped).
            loadout.SetAll(new[] { null, null, null, a, null });
            loadout.SetItems(new[] { potion, bread, bread, potion, null });
            Assert.AreEqual(a, loadout.Get(3));
            Assert.IsNull(loadout.GetItem(3), "a skill's slot keeps the skill");
            Assert.AreEqual(potion, loadout.GetItem(0));
            Assert.AreEqual(bread, loadout.GetItem(1));
            Assert.IsNull(loadout.GetItem(2), "only the first of a duplicate");
            CollectionAssert.AreEqual(new ItemDefinition[] { potion, bread, null, null, null, null, null, null, null }, loadout.ToItemArray());
        }
        finally
        {
            Object.DestroyImmediate(potion);
            Object.DestroyImmediate(bread);
        }
    }

    [Test]
    public void EquippingASlottedSkillMovesItAndSwaps()
    {
        var loadout = new SkillLoadout();
        loadout.Equip(3, a);
        loadout.Equip(4, b);

        Assert.IsTrue(loadout.Equip(4, a));
        Assert.AreEqual(a, loadout.Get(4));
        Assert.AreEqual(b, loadout.Get(3));
        Assert.AreEqual(4, loadout.IndexOf(a));
    }

    [Test]
    public void MoveSwapsAndClearEmpties()
    {
        var loadout = new SkillLoadout();
        loadout.Equip(3, a);
        int changes = 0;
        loadout.SlotChanged += _ => changes++;

        Assert.IsTrue(loadout.Move(3, 4));
        Assert.IsNull(loadout.Get(3));
        Assert.AreEqual(a, loadout.Get(4));
        Assert.AreEqual(2, changes);

        Assert.IsTrue(loadout.Clear(4));
        Assert.IsFalse(loadout.Clear(4));
        Assert.IsFalse(loadout.Move(2, 4));
    }

    [Test]
    public void SetAllDropsDuplicates()
    {
        var loadout = new SkillLoadout();
        loadout.SetAll(new[] { null, null, null, a, a });
        Assert.AreEqual(a, loadout.Get(3));
        Assert.IsNull(loadout.Get(4), "only the first of a duplicate");
        loadout.SetAll(new[] { null, null, null, b, c });
        Assert.AreEqual(b, loadout.Get(3));
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
