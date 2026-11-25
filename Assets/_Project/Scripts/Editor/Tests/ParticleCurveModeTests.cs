using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// A particle system's velocity-over-lifetime axes must share one curve mode (all constants, all two-constant ranges...),
/// and so must its orbital axes: Unity accepts a mix without complaint in the Inspector, then warns on every frame the
/// system simulates. In the user's build of 2026-11-25 the portal's motes and the loot glitter wrote ~40 000 such lines in
/// one evening. Every prefab of the game's own is checked here; a system made in code keeps its axes in one mode by hand
/// (LootableRemains.BuildSparkles).
/// </summary>
public sealed class ParticleCurveModeTests
{
    [Test]
    public void EveryPrefabParticleSystem_KeepsEachVelocityGroupInOneMode()
    {
        var mixed = new List<string>();
        int checkedSystems = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                continue;
            }

            foreach (ParticleSystem system in prefab.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
                if (!velocity.enabled)
                {
                    continue;
                }

                checkedSystems++;
                if (!SameMode(velocity.x, velocity.y, velocity.z))
                {
                    mixed.Add($"{path} › {system.name}: velocity {velocity.x.mode}/{velocity.y.mode}/{velocity.z.mode}");
                }

                if (!SameMode(velocity.orbitalX, velocity.orbitalY, velocity.orbitalZ))
                {
                    mixed.Add($"{path} › {system.name}: orbital {velocity.orbitalX.mode}/{velocity.orbitalY.mode}/{velocity.orbitalZ.mode}");
                }
            }
        }

        Assert.That(checkedSystems, Is.GreaterThan(0), "No prefab particle system with velocity over lifetime was found: the scan looks in the wrong place.");
        Assert.That(mixed, Is.Empty, "Give each group's axes one mode (a constant c becomes the range c..c):\n" + string.Join("\n", mixed));
    }

    // The loot glitter is made in code (no prefab to scan) and was most of the 40 000 lines: built here as a body builds it.
    [Test]
    public void TheLootGlitter_KeepsItsVelocityInOneMode()
    {
        BodyLootSettings settings = BodyLootSettings.Current;
        Assert.IsNotNull(settings?.SparkleMaterial, "the shipped body-loot settings have a glitter material, or no glitter is made");
        var body = new GameObject("Glitter Probe");
        try
        {
            var remains = body.AddComponent<LootableRemains>();
            typeof(LootableRemains).GetMethod("BuildSparkles", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(remains, new object[] { settings });
            ParticleSystem glitter = body.GetComponentInChildren<ParticleSystem>(true);
            Assert.IsNotNull(glitter, "the glitter was made");
            ParticleSystem.VelocityOverLifetimeModule velocity = glitter.velocityOverLifetime;
            Assert.IsTrue(SameMode(velocity.x, velocity.y, velocity.z), $"velocity {velocity.x.mode}/{velocity.y.mode}/{velocity.z.mode}");
            Assert.IsTrue(SameMode(velocity.orbitalX, velocity.orbitalY, velocity.orbitalZ), "orbital");
        }
        finally
        {
            Object.DestroyImmediate(body);
        }
    }

    private static bool SameMode(ParticleSystem.MinMaxCurve x, ParticleSystem.MinMaxCurve y, ParticleSystem.MinMaxCurve z) =>
        x.mode == y.mode && y.mode == z.mode;
}
