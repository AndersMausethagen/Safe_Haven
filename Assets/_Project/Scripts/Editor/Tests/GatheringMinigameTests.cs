using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class GatheringMinigameTests
{
    [Test]
    public void MinedNodes_BreakApart_AndGiveMoreOfWhatTheyHold_AndSmallRocks()
    {
        var rocks = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Materials/SmallRocks.asset");
        foreach (string guid in AssetDatabase.FindAssets("t:GatheringNodeDefinition"))
        {
            var node = AssetDatabase.LoadAssetAtPath<GatheringNodeDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (node.GatherTool != ToolType.Pickaxe)
            {
                Assert.AreEqual(0, node.BurstPieces, node.name + " isn't mined.");
                continue;
            }

            Assert.Greater(node.BurstPieces, 0, node.name);
            Assert.AreSame(node.Yields.Entries[0].Item, node.BurstYields.Entries[0].Item, node.name);
            Assert.AreSame(rocks, node.BurstYields.Entries[1].Item, node.name);
            Assert.LessOrEqual(node.BurstYields.Entries[0].MaxQuantity, 2, node.name + ": a few extra, not a second yield.");
            Assert.Greater(node.Feedback.BurstSpeed, 0f, node.name);
            Assert.NotNull(node.Feedback.PieceKnock, node.name);
        }
    }

    [Test]
    public void NodePrefabs_GiveWhatTheirDefinitionSays()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:GatheringNodeDefinition"))
        {
            var node = AssetDatabase.LoadAssetAtPath<GatheringNodeDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (node.WorldPrefab == null || !node.WorldPrefab.TryGetComponent(out ResourceSource source)) continue;
            Assert.AreEqual(node.Yields.Entries.Count, source.Drops.Entries.Count, node.name);
            for (int i = 0; i < node.Yields.Entries.Count; i++)
            {
                Assert.AreSame(node.Yields.Entries[i].Drop, source.Drops.Entries[i].Drop, node.name);
                Assert.AreEqual(node.Yields.Entries[i].MinQuantity, source.Drops.Entries[i].MinQuantity, node.name);
                Assert.AreEqual(node.Yields.Entries[i].MaxQuantity, source.Drops.Entries[i].MaxQuantity, node.name);
            }
        }
    }

    // Closed if an edge is met once from each side; the volume of a closed mesh is the sum of tetrahedra from any point.
    private static float Volume(Mesh mesh, Vector3 from, out int openEdges)
    {
        Vector3[] points = mesh.vertices;
        var edges = new Dictionary<(Vector3Int, Vector3Int), int>();
        Vector3Int Key(Vector3 p) => new Vector3Int(Mathf.RoundToInt(p.x * 2000f), Mathf.RoundToInt(p.y * 2000f), Mathf.RoundToInt(p.z * 2000f));
        float volume = 0f;
        for (int sub = 0; sub < mesh.subMeshCount; sub++)
        {
            int[] triangles = mesh.GetTriangles(sub);
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = points[triangles[i]], b = points[triangles[i + 1]], c = points[triangles[i + 2]];
                volume += Vector3.Dot(a - from, Vector3.Cross(b - from, c - from)) / 6f;
                foreach ((Vector3 p, Vector3 q) in new[] { (a, b), (b, c), (c, a) })
                {
                    Vector3Int kp = Key(p), kq = Key(q);
                    if (kp == kq) continue;
                    edges.TryGetValue((kq, kp), out int back);
                    if (back > 0) edges[(kq, kp)] = back - 1;
                    else { edges.TryGetValue((kp, kq), out int here); edges[(kp, kq)] = here + 1; }
                }
            }
        }

        openEdges = 0;
        foreach (int left in edges.Values) openEdges += left;
        return Mathf.Abs(volume);
    }

    [TestCase(PrimitiveType.Sphere, 5)]
    [TestCase(PrimitiveType.Cube, 3)]
    public void MeshCracker_MakesSolidPieces_ThatAddUpToTheWhole(PrimitiveType shape, int count)
    {
        GameObject primitive = GameObject.CreatePrimitive(shape);
        Mesh source = primitive.GetComponent<MeshFilter>().sharedMesh;
        float whole = Volume(source, Vector3.zero, out _);
        List<Mesh> pieces = MeshCracker.Crack(source, count, 12345);
        try
        {
            Assert.AreEqual(count, pieces.Count);
            float total = 0f;
            foreach (Mesh piece in pieces)
            {
                float here = Volume(piece, Vector3.zero, out int open);
                Assert.AreEqual(here, Volume(piece, new Vector3(3f, -2f, 5f), out _), here * 0.02f + 0.001f, "Only a closed shape has one volume from everywhere.");
                Assert.AreEqual(0, open, "A piece has no holes.");
                Assert.AreEqual(2, piece.subMeshCount, "Surface and broken faces.");
                Assert.Greater(piece.GetTriangles(1).Length, 0, "It has broken faces.");
                Assert.Greater(here, whole * 0.02f, "A real piece, not a sliver.");
                total += here;
            }

            Assert.AreEqual(whole, total, whole * 0.12f, "Together they are the whole.");
        }
        finally
        {
            foreach (Mesh piece in pieces) Object.DestroyImmediate(piece);
            Object.DestroyImmediate(primitive);
        }
    }

    [Test]
    public void MinedNodes_HaveTheirModelsCracked()
    {
        foreach (string file in new[] { "GreyStone", "IronOre", "Overgrowth/GreatBoulder" })
        {
            var node = AssetDatabase.LoadAssetAtPath<GatheringNodeDefinition>("Assets/_Project/Data/Gathering/" + file + ".asset");
            Assert.NotNull(node.Fracture, file);
            int cracked = 0;
            foreach (MeshRenderer part in NodeFracture.DrawnParts(node.WorldPrefab.transform))
            {
                Mesh[] pieces = node.Fracture.PiecesOf(part.GetComponent<MeshFilter>().sharedMesh);
                if (pieces != null) cracked += pieces.Length;
            }

            Assert.GreaterOrEqual(cracked, 3, file + ": the main rock comes apart.");
            foreach (GameObject variant in node.ModelVariants)
            {
                bool any = false;
                foreach (MeshRenderer part in NodeFracture.DrawnParts(variant.transform))
                    any |= node.Fracture.PiecesOf(part.GetComponent<MeshFilter>().sharedMesh) != null;
                Assert.IsTrue(any, file + " " + variant.name);
            }
        }
    }

    [Test]
    public void GrantExtra_GivesTheTable_AndNothingWithoutOne()
    {
        var rocks = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Materials/SmallRocks.asset");
        var grey = AssetDatabase.LoadAssetAtPath<GatheringNodeDefinition>("Assets/_Project/Data/Gathering/GreyStone.asset");
        var user = new GameObject("Miner");
        var rock = new GameObject("Rock");
        try
        {
            ItemInventory bag = user.AddComponent<ItemInventory>();
            ResourceSource source = rock.AddComponent<ResourceSource>();
            Assert.IsTrue(source.GrantExtra(user, grey.BurstYields));
            Assert.GreaterOrEqual(bag.Container.Count(rocks), 2);
            Assert.GreaterOrEqual(bag.Container.Count(grey.Yields.Entries[0].Item), 1);
            Assert.IsFalse(source.GrantExtra(user, new ItemDropTable()));
            Assert.IsFalse(source.GrantExtra(null, grey.BurstYields));
        }
        finally
        {
            Object.DestroyImmediate(user);
            Object.DestroyImmediate(rock);
        }
    }

    private readonly List<Object> created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (Object item in created)
        {
            if (item != null) Object.DestroyImmediate(item);
        }
        created.Clear();
    }

    [Test]
    public void HarvestFeedbackFallsBackPerFieldToTheDefaultProfile()
    {
        var sound = ScriptableObject.CreateInstance<SoundDefinition>();
        var effect = new GameObject("Test Effect");
        var own = ScriptableObject.CreateInstance<HarvestFeedbackProfile>();
        var fallback = ScriptableObject.CreateInstance<HarvestFeedbackProfile>();
        created.AddRange(new Object[] { sound, effect, own, fallback });

        var fallbackData = new SerializedObject(fallback);
        fallbackData.FindProperty("critical.sound").objectReferenceValue = sound;
        fallbackData.FindProperty("critical.effect").objectReferenceValue = effect;
        fallbackData.ApplyModifiedPropertiesWithoutUndo();
        var ownData = new SerializedObject(own);

        Assert.AreSame(sound, HarvestFeedbackProfile.SoundFor(own, fallback, HarvestMoment.Critical));
        Assert.AreSame(effect, HarvestFeedbackProfile.EffectFor(null, fallback, HarvestMoment.Critical));
        Assert.IsNull(HarvestFeedbackProfile.SoundFor(own, fallback, HarvestMoment.Normal));
        Assert.IsNull(HarvestFeedbackProfile.SoundFor(null, null, HarvestMoment.Harvested));

        var ownSound = ScriptableObject.CreateInstance<SoundDefinition>();
        created.Add(ownSound);
        ownData.FindProperty("critical.sound").objectReferenceValue = ownSound;
        ownData.ApplyModifiedPropertiesWithoutUndo();
        Assert.AreSame(ownSound, HarvestFeedbackProfile.SoundFor(own, fallback, HarvestMoment.Critical));
    }

    [Test]
    public void SwingResultsMapToHarvestMoments()
    {
        Assert.AreEqual(HarvestMoment.Critical, HarvestFeedbackProfile.MomentOf(SwingResult.Critical));
        Assert.AreEqual(HarvestMoment.Normal, HarvestFeedbackProfile.MomentOf(SwingResult.Normal));
        Assert.AreEqual(HarvestMoment.Glancing, HarvestFeedbackProfile.MomentOf(SwingResult.Glancing));
    }

    [Test]
    public void ResourceNodeAddsFeedbackAndRaisesSwingAndHarvestEvents()
    {
        var go = new GameObject("Test Node");
        created.Add(go);
        Interactable interactable = go.AddComponent<Interactable>();
        ResourceNode node = go.AddComponent<ResourceNode>();
        Invoke(interactable, "Awake");
        Invoke(node, "Awake");
        Invoke(node, "OnEnable");
        Assert.IsNotNull(go.GetComponent<ResourceNodeFeedback>());

        SwingResult swung = SwingResult.Glancing;
        int harvested = 0;
        node.Swung += (result, from) => swung = result;
        node.Harvested += () => harvested++;
        node.NotifySwing(SwingResult.Critical, Vector3.zero);
        interactable.Completed.Invoke(); // the gather finishing, as the player's interaction ends it

        Assert.AreEqual(SwingResult.Critical, swung);
        Assert.AreEqual(1, harvested);
        Invoke(node, "OnDisable");
    }

    private static void Invoke(object target, string method)
    {
        target.GetType().GetMethod(method, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Invoke(target, null);
    }

    [Test]
    public void EveryRaidResourceSpawnIsAGatherableNode()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:RaidDefinition"))
        {
            var raid = AssetDatabase.LoadAssetAtPath<RaidDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            foreach (RaidSpawnList list in raid.SpawnLists)
            {
                if (list.category != SpawnCategory.Resources)
                {
                    continue;
                }

                foreach (SpawnTable.Entry entry in list.entries)
                {
                    Assert.IsNotNull(entry.prefab, $"{raid.name}/{list.name} has an empty entry");
                    Assert.IsNotNull(entry.prefab.GetComponentInChildren<ResourceNode>(true),
                        $"{raid.name}/{list.name}: {entry.prefab.name} has no ResourceNode, so it can't be gathered");
                }
            }
        }
    }

    [Test]
    public void ZonesJudgeCriticalNormalAndGlancing()
    {
        var zones = new SwingZones(0.5f, 0.4f, 0.1f);
        Assert.AreEqual(SwingResult.Critical, zones.Evaluate(0.52f));
        Assert.AreEqual(SwingResult.Normal, zones.Evaluate(0.65f));
        Assert.AreEqual(SwingResult.Glancing, zones.Evaluate(0.9f));
    }

    [Test]
    public void ZonesStayOnTheBarAndCriticalNeverOutgrowsNormal()
    {
        var zones = new SwingZones(0.95f, 0.4f, 0.6f);
        Assert.AreEqual(0.8f, zones.Center, 1e-5f);
        Assert.AreEqual(0.4f, zones.CriticalWidth, 1e-5f);
    }

    [Test]
    public void LeftAlone_TheToolComesDownAsANormalSwing_WithNothingLost()
    {
        var session = Session(swings: 2, jitter: 0f);
        Assert.IsFalse(session.Tick(0.9f));
        Assert.IsTrue(session.Tick(0.2f));
        Assert.AreEqual(1, session.SwingsDone);
        Assert.AreEqual(0, session.Glancing);
        Assert.AreEqual(0f, session.Position, 1e-5f);
    }

    [Test]
    public void PressAtTheTopOfTheBarCentreIsCritical()
    {
        var session = Session(swings: 3, jitter: 0f);
        session.Tick(0.5f); // marker at 0.5, the zone centre
        Assert.AreEqual(SwingResult.Critical, session.Press());
        Assert.AreEqual(1, session.Criticals);
        Assert.AreEqual(1, session.Combo);
    }

    [Test]
    public void ComboWidensTheCriticalZoneUpToTheCapAndResetsOnAMiss()
    {
        var session = Session(swings: 10, jitter: 0f, criticalWidth: 0.1f, perCombo: 0.02f, maxCombo: 2);
        for (int i = 0; i < 3; i++) session.Resolve(SwingResult.Critical);
        Assert.AreEqual(3, session.Combo);
        Assert.AreEqual(0.14f, session.Zones.CriticalWidth, 1e-5f);

        session.Resolve(SwingResult.Normal);
        Assert.AreEqual(0, session.Combo);
        Assert.AreEqual(0.1f, session.Zones.CriticalWidth, 1e-5f);
    }

    [Test]
    public void SessionCompletesAfterItsSwingsAndIgnoresMore()
    {
        var session = Session(swings: 2, jitter: 0f);
        session.Resolve(SwingResult.Critical);
        session.Resolve(SwingResult.Glancing);
        session.Resolve(SwingResult.Critical);
        Assert.IsTrue(session.IsComplete);
        Assert.AreEqual(2, session.SwingsDone);
        Assert.AreEqual(1, session.Criticals);
        Assert.IsFalse(session.Tick(5f));
    }

    [Test]
    public void BetterToolTiersWidenTheCriticalZoneCappedAtTheNormalZone()
    {
        var settings = ScriptableObject.CreateInstance<GatheringMinigameSettings>();
        created.Add(settings);
        GatheringToolProfile axe = settings.Profile(ToolType.Axe);
        float baseWidth = settings.CriticalWidthFor(axe, 1, 1);
        Assert.AreEqual(axe.CriticalZoneWidth, baseWidth, 1e-5f);
        Assert.AreEqual(baseWidth + settings.CriticalWidthPerTier * 2f, settings.CriticalWidthFor(axe, 3, 1), 1e-5f);
        Assert.AreEqual(axe.NormalZoneWidth, settings.CriticalWidthFor(axe, 100, 1), 1e-5f);
        Assert.IsTrue(settings.UsesMinigame(ToolType.Axe));
        Assert.IsTrue(settings.UsesMinigame(ToolType.Pickaxe));
        Assert.IsTrue(settings.NoiseRadius(SwingResult.Critical) < settings.NoiseRadius(SwingResult.Glancing));
    }

    [Test]
    public void TheSickleHasAQuickQuietProfileAndHandGatheringHasNone()
    {
        var settings = ScriptableObject.CreateInstance<GatheringMinigameSettings>();
        created.Add(settings);
        GatheringToolProfile sickle = settings.Profile(ToolType.Sickle);
        GatheringToolProfile axe = settings.Profile(ToolType.Axe);
        Assert.IsNotNull(sickle);
        Assert.Less(sickle.RiseSeconds, axe.RiseSeconds);
        Assert.Less(sickle.Loudness, axe.Loudness);
        Assert.IsNull(settings.Profile(ToolType.None));
        Assert.IsFalse(settings.UsesMinigame(ToolType.None));
    }

    [Test]
    public void LoudnessScalesAnnoyanceAndThePeakIsKeptAfterCalming()
    {
        var annoyance = new ForestAnnoyance(new ForestAnnoyanceSettings());
        annoyance.Add(SwingResult.Normal, 0.5f);
        Assert.AreEqual(new ForestAnnoyanceSettings().AnnoyanceFor(SwingResult.Normal) * 0.5f, annoyance.Value, 1e-4f);
        annoyance.Add(SwingResult.Glancing);
        float peak = annoyance.Peak;
        annoyance.Tick(1000f);
        annoyance.Tick(1000f);
        Assert.AreEqual(0f, annoyance.Value, 1e-4f);
        Assert.AreEqual(peak, annoyance.Peak, 1e-4f);
    }

    [Test]
    public void ThePeakIsTheWrathThresholdOnceAWaveFires()
    {
        var annoyance = new ForestAnnoyance(new ForestAnnoyanceSettings());
        for (int i = 0; i < 20; i++)
        {
            annoyance.Add(SwingResult.Glancing);
        }

        Assert.AreEqual(1f, annoyance.PeakFill, 1e-4f);
    }

    [Test]
    public void GatheringStatsCountCriticalRateAndKeepThePeak()
    {
        var stats = new GatheringStats();
        Assert.IsNull(RaidReportText.Gathering(stats));
        stats.Record(SwingResult.Critical);
        stats.Record(SwingResult.Normal);
        stats.Record(SwingResult.Glancing);
        stats.Record(SwingResult.Critical);
        Assert.AreEqual(4, stats.Swings);
        Assert.AreEqual(0.5f, stats.CriticalRate, 1e-5f);
        Assert.AreEqual(1, stats.Glancing);
        Assert.IsFalse(stats.HasAnnoyance);
        stats.RecordAnnoyance(0.6f, 0);
        stats.RecordAnnoyance(0.2f, 0);
        Assert.AreEqual(0.6f, stats.PeakAnnoyance, 1e-5f);
        StringAssert.Contains("50", RaidReportText.Gathering(stats));
        StringAssert.Contains("60", RaidReportText.Gathering(stats));
    }

    [Test]
    public void AnnoyanceClimbsThroughTheLevels()
    {
        var annoyance = new ForestAnnoyance(new ForestAnnoyanceSettings());
        var levels = new List<AnnoyanceLevel>();
        annoyance.LevelChanged += levels.Add;

        annoyance.Add(SwingResult.Critical);
        Assert.AreEqual(2.5f, annoyance.Value, 1e-5f);
        annoyance.Add(30f);
        Assert.AreEqual(AnnoyanceLevel.Stirring, annoyance.Level);
        annoyance.Add(30f);
        Assert.AreEqual(AnnoyanceLevel.Angry, annoyance.Level);
        CollectionAssert.AreEqual(new[] { AnnoyanceLevel.Stirring, AnnoyanceLevel.Angry }, levels);
    }

    [Test]
    public void AnnoyanceDecaysOnlyAfterTheQuietDelay()
    {
        var annoyance = new ForestAnnoyance(new ForestAnnoyanceSettings());
        annoyance.Add(40f);
        annoyance.Tick(4f);
        Assert.AreEqual(40f, annoyance.Value, 1e-5f);
        annoyance.Tick(2f);
        Assert.Less(annoyance.Value, 40f);

        annoyance.Add(SwingResult.Normal);
        float value = annoyance.Value;
        annoyance.Tick(1f);
        Assert.AreEqual(value, annoyance.Value, 1e-5f, "a swing restarts the quiet delay");
    }

    [Test]
    public void WrathFiresOnceThenAnnoyanceIsCapped()
    {
        var annoyance = new ForestAnnoyance(new ForestAnnoyanceSettings());
        int wraths = 0;
        annoyance.Wrath += () => wraths++;

        annoyance.Add(100f);
        Assert.AreEqual(1, wraths);
        Assert.AreEqual(annoyance.Settings.AngryAt, annoyance.Value, 1e-5f);
        Assert.AreEqual(AnnoyanceLevel.Angry, annoyance.Level);

        annoyance.Add(500f);
        Assert.AreEqual(1, wraths);
        Assert.AreEqual(annoyance.Settings.WrathAt, annoyance.Value, 1e-5f);
        Assert.AreEqual(1f, annoyance.Fill, 1e-5f);
    }

    // Review 2026-11-17 (S2): the swing that tipped an angry forest into its wrath also said "The forest is angry" again, and the
    // driver hunted a second time two frames after the wrath's own hunt: falling back to Angry was told as a change of its own.
    [Test]
    public void AWrathIsToldOnce_ThenTheForestIsAngryAgainQuietly()
    {
        var annoyance = new ForestAnnoyance(new ForestAnnoyanceSettings());
        var levels = new List<AnnoyanceLevel>();
        annoyance.LevelChanged += levels.Add;
        annoyance.Add(annoyance.Settings.AngryAt);
        CollectionAssert.AreEqual(new[] { AnnoyanceLevel.Angry }, levels);

        annoyance.Add(annoyance.Settings.WrathAt);
        CollectionAssert.AreEqual(new[] { AnnoyanceLevel.Angry, AnnoyanceLevel.Wrath }, levels, "no second Angry after the wrath");
        Assert.AreEqual(AnnoyanceLevel.Angry, annoyance.Level, "the forest is still angry");

        annoyance.Tick(1000f);
        Assert.AreEqual(AnnoyanceLevel.Calm, levels[levels.Count - 1], "calming down is still told");
    }

    [Test]
    public void BestToolIndexPicksTheHighestUnbrokenTool()
    {
        var container = new ItemContainer(4, 1);
        container.AddStack(new ItemStack(Tool(ToolType.Axe, 1), 1, 1f));
        container.AddStack(new ItemStack(Tool(ToolType.Axe, 3), 1, 0f));
        container.AddStack(new ItemStack(Tool(ToolType.Axe, 2), 1, 0.5f));
        container.AddStack(new ItemStack(Tool(ToolType.Pickaxe, 4), 1, 1f));

        int best = container.BestToolIndex(ToolType.Axe);
        Assert.AreEqual(2, container.GetSlot(best).Item.ToolTier);
        Assert.AreEqual(-1, container.BestToolIndex(ToolType.None));
    }

    // Hold to raise, on Session(): rise 1 s, critical zone 0.45-0.55, normal zone 0.3-0.7.
    [Test]
    public void HoldToRaise_RisesOnlyWhileHeld_AndTheReleaseIsJudgedWhereTheToolIs()
    {
        GatheringSession session = Session(10, 0f);
        Assert.IsNull(session.Hold(0.3f, false, out _), "nothing happens until the button is held");
        Assert.IsFalse(session.IsRaising);

        Assert.IsNull(session.Hold(0.5f, true, out _));
        Assert.IsTrue(session.IsRaising);
        Assert.IsTrue(session.InCriticalZone, "half way up is the glint");
        Assert.AreEqual(SwingResult.Critical, session.Hold(0f, false, out bool late));
        Assert.IsFalse(late);

        session.Hold(0.35f, true, out _);
        Assert.AreEqual(SwingResult.Normal, session.Hold(0f, false, out _), "near the glint is a normal swing");

        session.Hold(0.1f, true, out _);
        Assert.AreEqual(SwingResult.Glancing, session.Hold(0f, false, out late), "let go too soon");
        Assert.IsFalse(late);
    }

    [Test]
    public void HoldToRaise_HeldToTheTop_ComesDownGlancing_AndWaitsForTheButtonToBeLetGo()
    {
        GatheringSession session = Session(10, 0f);
        session.Hold(0.6f, true, out _);
        Assert.AreEqual(SwingResult.Glancing, session.Hold(0.5f, true, out bool late));
        Assert.IsTrue(late, "held too long");
        Assert.IsNull(session.Hold(0.5f, true, out _), "still held: no new rise");
        Assert.IsFalse(session.IsRaising);
        Assert.IsNull(session.Hold(0f, false, out _), "letting go after the top is not another swing");
        session.Hold(0.5f, true, out _);
        Assert.IsTrue(session.IsRaising);
    }

    [Test]
    public void HoldToRaise_ThePressThatBeganTheGatherDoesNotRaiseTheTool()
    {
        GatheringSession session = Session(10, 0f);
        session.WaitForRelease();
        Assert.IsNull(session.Hold(0.5f, true, out _));
        Assert.IsFalse(session.IsRaising);
        session.Hold(0f, false, out _);
        session.Hold(0.2f, true, out _);
        Assert.IsTrue(session.IsRaising);
    }

    [Test]
    public void ResultsCountForTheirShareOfTheNodesSwings()
    {
        var weights = new GatheringSession(4, 1f, 0.5f, 0.4f, 0.1f, 0f, 0, 0f, new System.Random(1),
            r => r == SwingResult.Critical ? 2f : r == SwingResult.Normal ? 1f : 0.5f);
        weights.Resolve(SwingResult.Critical);
        Assert.AreEqual(2f, weights.Progress, 1e-4f);
        weights.Resolve(SwingResult.Glancing);
        Assert.AreEqual(2.5f, weights.Progress, 1e-4f);
        Assert.IsFalse(weights.IsComplete);
        weights.Resolve(SwingResult.Critical);
        Assert.IsTrue(weights.IsComplete, "two criticals and a glancing blow finish a four-swing node");
        Assert.AreEqual(3, weights.SwingsDone);

        // The shipped weights keep their order, whatever they are tuned to: a critical cuts deeper than a normal swing, a glancing
        // blow less, and none cuts nothing.
        GatheringMinigameSettings shipped = GatheringMinigameSettings.Current;
        Assert.Greater(shipped.ProgressFor(SwingResult.Critical), shipped.ProgressFor(SwingResult.Normal));
        Assert.Greater(shipped.ProgressFor(SwingResult.Normal), shipped.ProgressFor(SwingResult.Glancing));
        Assert.Greater(shipped.ProgressFor(SwingResult.Glancing), 0f);
    }

    private static GatheringSession Session(int swings, float jitter, float criticalWidth = 0.1f, float perCombo = 0f, int maxCombo = 0)
    {
        return new GatheringSession(swings, 1f, 0.5f, 0.4f, criticalWidth, perCombo, maxCombo, jitter, new System.Random(1));
    }

    private ItemDefinition Tool(ToolType type, int tier)
    {
        var item = ScriptableObject.CreateInstance<ItemDefinition>();
        created.Add(item);
        item.name = type + " " + tier;
        var data = new SerializedObject(item);
        data.FindProperty("toolType").intValue = (int)type;
        data.FindProperty("toolTier").intValue = tier;
        data.ApplyModifiedPropertiesWithoutUndo();
        return item;
    }

    [Test]
    public void HoldToRaise_ATapDropsTheToolInsteadOfChopping()
    {
        var session = new GatheringSession(3, 1f, 0.8f, 0.3f, 0.1f, 0f, 1, 0f, new System.Random(1), r => 1f) { MinimumRaise = 0.35f };
        Assert.IsNull(session.Hold(0.1f, true, out _), "rising");
        Assert.IsNull(session.Hold(0.02f, false, out _), "let go at 10%: no swing");
        Assert.IsTrue(session.DroppedRaise, "the raise was dropped");
        Assert.AreEqual(0, session.SwingsDone);

        Assert.IsNull(session.Hold(0.8f, true, out _), "a proper raise");
        SwingResult? swung = session.Hold(0.02f, false, out _);
        Assert.IsTrue(swung.HasValue, "let go near the top: a swing");
        Assert.IsFalse(session.DroppedRaise);
    }
}
