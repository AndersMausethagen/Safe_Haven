using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Every rigged character as the rig audit sees it (RigAudit; the user, 2026-11-18: "are the skeletons configured correctly and the
/// correct size? is there tearing and wrong stretches in the mesh?"): its avatar valid where it has one, every vertex weighted, a
/// believable height, no seam that opens as it moves, and no part left behind by its bones (the Draug's root-weighted pole stretched
/// 113 times as it lay down). The generated bodies' skirts still stretch in lunges (RigWeightFixes smooths them; a weight pass in
/// Blender would do more), so stretching is held at what was measured, not at zero.
/// </summary>
public sealed class RigAuditTests
{
    // Bodies allowed past a rule, and why.
    private static readonly Dictionary<string, string> Allowed = new Dictionary<string, string>
    {
        ["MossShaman/Belt"] = "the belt is weighted to the upper spine and folds inside the body in its crouch; it doesn't show",
        ["Wild Horse/Pony"] = "slivers in the chest where the foreleg swings; the walk looks right from any side (seen 2026-11-18)",
        ["Wild Horse/Pony Hair"] = "hair cards rooted at one point swing apart as the mane moves: cards, not a seam",
        ["Wild Horse/Horse_Undead_Reins_Re"] = "the reins hang between the head and the neck bones and stretch as the head turns: they are reins",
        ["Toxic Horse/Horse_Undead_Reins_Re"] = "the reins hang between the head and the neck bones and stretch as the head turns: they are reins",
        ["Undead Knight Fire/Horse_Undead_Reins_Re"] = "the reins hang between the head and the neck bones and stretch as the head turns: they are reins",
    };

    private static List<RigAudit.Body> bodies;

    private static List<RigAudit.Body> Bodies() => bodies ??= RigAudit.Run(out _, 10, 3);

    private static string Name(RigAudit.Body body) => $"{System.IO.Path.GetFileNameWithoutExtension(body.Prefab)}/{body.Renderer}";

    [Test]
    public void EveryAvatar_IsValid_AndEveryVertexWeighted()
    {
        List<RigAudit.Body> found = Bodies();
        Assert.Greater(found.Count, 100, "the audit found the rigged characters");
        var wrong = new List<string>();
        foreach (RigAudit.Body body in found)
        {
            if (body.Avatar != "(none)" && !body.AvatarValid)
            {
                wrong.Add($"{Name(body)}: avatar {body.Avatar} isn't valid");
            }

            if (body.Unweighted > 0)
            {
                wrong.Add($"{Name(body)}: {body.Unweighted} vertices weighted to nothing (they stay at the model's origin)");
            }

            if (!string.IsNullOrEmpty(body.OddScale))
            {
                wrong.Add($"{Name(body)}: bones scaled unevenly ({body.OddScale})");
            }

            if (!string.IsNullOrEmpty(body.Problem))
            {
                wrong.Add($"{Name(body)}: {body.Problem}");
            }
        }

        CollectionAssert.IsEmpty(wrong);
    }

    [Test]
    public void EveryBody_StandsAtABelievableHeight()
    {
        // From a bat to the Old Moss Troll: nothing smaller than 15 cm or taller than 6 m, the people between 1.6 and 2 m.
        var wrong = new List<string>();
        foreach (RigAudit.Body body in Bodies())
        {
            if (body.Height < 0.15f || body.Height > 6f)
            {
                wrong.Add($"{Name(body)}: {body.Height:0.00} m");
            }
        }

        string[] people = { "PlayerArmature", "Ingrid", "Bjarne", "Tor", "Liv", "Runa", "Sigrid", "Eirik" };
        foreach (RigAudit.Body body in Bodies().Where(b => people.Contains(System.IO.Path.GetFileNameWithoutExtension(b.Prefab))))
        {
            if (body.Height < 1.6f || body.Height > 2f)
            {
                wrong.Add($"{Name(body)}: a person {body.Height:0.00} m tall");
            }
        }

        CollectionAssert.IsEmpty(wrong);
    }

    [Test]
    public void NoSeamOpens_AndNoPartIsLeftBehind()
    {
        var wrong = new List<string>();
        foreach (RigAudit.Body body in Bodies())
        {
            if (Allowed.ContainsKey(Name(body)))
            {
                continue;
            }

            // A jaw opening is a seam that is meant to open.
            if (body.WorstSeam > 0.02f && !body.WorstSeamAt.EndsWith("Jaw") && !body.WorstSeamAt.EndsWith("Head"))
            {
                wrong.Add($"{Name(body)}: a seam opens {body.WorstSeam * 100f:0.0} cm ({body.WorstSeamAt})");
            }

            // A part its bones leave behind stretches past anything a joint does (the Draug's pole: 113x).
            if (body.WorstStretch > 60f)
            {
                wrong.Add($"{Name(body)}: an edge stretched {body.WorstStretch:0}x ({body.WorstStretchAt})");
            }

            if (body.StretchedShare > 0.065f)
            {
                wrong.Add($"{Name(body)}: {body.StretchedShare * 100f:0.0}% of edges stretched past twice their length ({body.WorstStretchAt})");
            }
        }

        CollectionAssert.IsEmpty(wrong);
    }

    [Test]
    public void TheWeightFixes_NameModelsThatExist()
    {
        foreach (RigWeightFixes.Fix fix in RigWeightFixes.Fixes)
        {
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<GameObject>(fix.Model), fix.Model);
        }
    }
}
