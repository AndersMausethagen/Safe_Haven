using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Beasts' feet keep up with the ground (Docs/QualityLevers_2026-11-24.md §3, BeastStrides): every beast that walks and gallops has its
/// Locomotion thresholds and stride speeds at its clips' measured feet speeds, and plays its gallop fast enough for its chase.
/// </summary>
public sealed class BeastStrideTests
{
    [Test]
    public void TheStrideRate_KeepsTheFeetOnTheGround()
    {
        // Fitted: the blend keeps up by itself to the gallop's speed, then the gallop plays faster.
        Assert.AreEqual(1f, MonsterAnimationDriver.StrideRate(2f, 3.4f, 1.2f), 1e-4f);
        Assert.AreEqual(5.5f / 3.4f, MonsterAnimationDriver.StrideRate(5.5f, 3.4f, 1.2f), 1e-4f);
        Assert.AreEqual(MonsterAnimationDriver.FastestBeastStride, MonsterAnimationDriver.StrideRate(20f, 2f, 0.7f), 1e-4f);
        // One clip, as before.
        Assert.AreEqual(2f, MonsterAnimationDriver.StrideRate(5.2f, 2.6f, 0f), 1e-4f);
        Assert.AreEqual(1f, MonsterAnimationDriver.StrideRate(0.1f, 2.6f, 0f), 1e-4f, "standing still: the idle at its own pace");
    }

    [Test]
    public void EveryGallopingBeast_IsFittedToItsClips_AndKeepsUpWithItsChase()
    {
        int fitted = 0;
        var problems = new List<string>();
        foreach (BeastStrides.Fit fit in BeastStrides.MeasureAll())
        {
            if (!fit.WalksAndGallops)
            {
                continue;
            }

            ChildMotion[] children = fit.Tree.children;
            for (int i = 1; i < children.Length; i++)
            {
                if (Mathf.Abs(children[i].threshold - fit.Feet[i]) > 0.1f * fit.Feet[i])
                {
                    problems.Add($"{fit.Controller.name}: {children[i].motion.name} at {children[i].threshold}, its feet cover {fit.Feet[i]:0.00} m/s");
                }
            }

            foreach (string path in fit.Prefabs)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var driver = new SerializedObject(prefab.GetComponentInChildren<MonsterAnimationDriver>(true));
                float stride = driver.FindProperty("strideSpeed").floatValue;
                float walk = driver.FindProperty("walkStrideSpeed").floatValue;
                if (Mathf.Abs(stride - fit.Fastest) > 0.1f * fit.Fastest || Mathf.Abs(walk - fit.Feet[fit.Walk]) > 0.1f * fit.Feet[fit.Walk])
                {
                    problems.Add($"{path}: strides {walk}/{stride}, its clips' feet {fit.Feet[fit.Walk]:0.00}/{fit.Fastest:0.00}");
                }

                MonsterAI ai = prefab.GetComponentInChildren<MonsterAI>(true);
                var definition = ai != null ? new SerializedObject(ai).FindProperty("definition").objectReferenceValue as MonsterDefinition : null;
                float chase = definition != null ? new SerializedObject(definition).FindProperty("moveSpeed").floatValue : 0f;
                float feetAtChase = fit.Fastest * MonsterAnimationDriver.StrideRate(chase, stride, walk);
                if (chase > 0f && feetAtChase < chase * 0.95f)
                {
                    problems.Add($"{path}: chases at {chase} m/s, its feet reach {feetAtChase:0.00}");
                }

                fitted++;
            }
        }

        Assert.IsEmpty(problems, string.Join("\n", problems));
        Assert.Greater(fitted, 4, "wolves, foxes, deer and stags walk and gallop");
    }
}
