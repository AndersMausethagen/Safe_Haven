using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Buildings can't be knocked out of place (the user, 2026-11-23, after a breach: "the buildings got all knocked around"): a blow's pull
/// or shove (ForcedMove) moves creatures and players, never what is built.
/// </summary>
public sealed class BuildingsStayPutTests
{
    [Test]
    public void APullOrShove_MovesACreature_ButNeverABuildingOrAStructure()
    {
        var building = new GameObject("Hut");
        building.AddComponent<BuildingObject>();
        var wall = new GameObject("Wall");
        wall.AddComponent<Combatant>();
        Damageable wallBody = wall.AddComponent<Damageable>();
        var so = new SerializedObject(wallBody);
        so.FindProperty("category").intValue = (int)TargetCategory.Structure;
        so.ApplyModifiedPropertiesWithoutUndo();
        var wolf = new GameObject("Wolf");
        wolf.AddComponent<Combatant>();
        wolf.AddComponent<Damageable>();
        try
        {
            Assert.IsTrue(ForcedMove.IsBuilt(building.transform), "a building");
            Assert.IsTrue(ForcedMove.IsBuilt(wall.transform), "anything struck as a structure");
            Assert.IsFalse(ForcedMove.IsBuilt(wolf.transform), "a creature is moved as before");

            ForcedMove.Drag(building.transform, new Vector3(5f, 0f, 0f), -3f, 0.5f);
            ForcedMove.Drag(wall.transform, new Vector3(5f, 0f, 0f), -3f, 0.5f);
            ForcedMove.Drag(wolf.transform, new Vector3(5f, 0f, 0f), -3f, 0.5f);
            Assert.IsNull(building.GetComponent<ForcedMove>(), "no drag starts on a building");
            Assert.IsNull(wall.GetComponent<ForcedMove>(), "nor on a structure");
            Assert.IsNotNull(wolf.GetComponent<ForcedMove>(), "the canary: a creature is dragged");
        }
        finally
        {
            Object.DestroyImmediate(building);
            Object.DestroyImmediate(wall);
            Object.DestroyImmediate(wolf);
        }
    }
}
