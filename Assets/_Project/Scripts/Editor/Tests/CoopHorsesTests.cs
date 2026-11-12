using NUnit.Framework;
using UnityEngine;

/// <summary>Horses in co-op (CoopHorses): how games name them, whose they are, and a visitor's horse going home with them.</summary>
public sealed class CoopHorsesTests
{
    private static string HorseState(string owner, string name, string stable = "") =>
        $"{{\"name\":\"{name}\",\"owner\":\"{owner}\",\"ownerName\":\"{owner} player\",\"stabledIn\":\"{stable}\",\"order\":1,\"job\":-1}}";

    private static EntitySaveData Horse(string id, string owner, string stable = "") => new EntitySaveData
    {
        id = id, prefab = "horse_wild", spawned = true, hasTransform = true, position = new Vector3(4f, 0f, 9f),
        components = { new ComponentSaveData { key = "horse", state = HorseState(owner, id, stable) } },
    };

    private static SaveData Game(string runId, params EntitySaveData[] haven)
    {
        var data = new SaveData { scene = HavenVisit.HavenScene, playtest = new PlaytestSaveData { runId = runId } };
        var scene = new SceneSaveData { scene = HavenVisit.HavenScene };
        scene.entities.AddRange(haven);
        data.world.Add(scene);
        return data;
    }

    [Test]
    public void ASavedHorse_HasTheSameKeyInEveryGame_AndNeverAKeyAGameGivesAHorseItMade()
    {
        uint key = CoopHorses.KeyForSaveId("4f1c2a9e-horse");
        Assert.AreEqual(key, CoopHorses.KeyForSaveId("4f1c2a9e-horse"), "The host's copy and a guest's copy name it alike.");
        Assert.AreNotEqual(key, CoopHorses.KeyForSaveId("4f1c2a9e-hors3"));
        Assert.AreEqual(0u, key & 0x80000000u, "Made-here horses (wild ones) have the high bit; saved ones never do.");
    }

    [Test]
    public void AHorseAVisitorTamed_GoesHomeWithThem_AndTheHostsStays()
    {
        SaveData home = Game("guest-run");
        SaveData visit = Game("guest-run", Horse("hosts-horse", "host-run"), Horse("tamed-on-the-visit", "guest-run", "host-stable"));

        SaveData kept = HavenVisit.KeepHome(home, visit);
        SceneSaveData haven = kept.world.Find(scene => scene.scene == HavenVisit.HavenScene);
        Assert.AreEqual(1, haven.entities.Count, "Only the visitor's own horse comes home.");
        EntitySaveData horse = haven.entities[0];
        Assert.AreEqual("tamed-on-the-visit", horse.id);
        Assert.IsTrue(horse.spawned, "Made from its prefab when the Haven loads.");
        string state = horse.components[0].state;
        Assert.AreEqual("guest-run", global::Horse.OwnerIn(state));
        StringAssert.Contains("\"arriving\":true", state, "It comes to graze near the portal, wherever the host's Haven had it.");
        StringAssert.Contains("\"stabledIn\":\"\"", state, "Not in the host's stable any more.");

        SaveData again = HavenVisit.KeepHome(kept, visit);
        Assert.AreEqual(1, again.world.Find(scene => scene.scene == HavenVisit.HavenScene).entities.Count, "Saving again doesn't make two.");
        Assert.AreEqual(0, home.world[0].entities.Count, "The kept home game isn't changed.");
    }

    [Test]
    public void AHorseFromBeforeOwners_IsTheHavensOwn_AndStays()
    {
        SaveData kept = HavenVisit.KeepHome(Game("guest-run"), Game("guest-run", Horse("old-horse", "")));
        Assert.AreEqual(0, kept.world.Find(scene => scene.scene == HavenVisit.HavenScene).entities.Count);
        Assert.AreEqual("", global::Horse.OwnerIn("{\"name\":\"Old\"}"));
    }

    [Test]
    public void AHorseKeptInStepByCoopHorses_IsntSentByHavenSync_WhenItMoves()
    {
        var holder = new GameObject("Shared Horse");
        try
        {
            var entity = holder.AddComponent<SaveableEntity>();
            int before = entity.SharedMark(withMovement: true);
            holder.transform.position += new Vector3(12f, 0f, 3f);
            Assert.AreNotEqual(before, entity.SharedMark(withMovement: true), "An ordinary thing moved is a change.");

            entity.SyncedElsewhere = true;
            int kept = entity.SharedMark(withMovement: true);
            holder.transform.position += new Vector3(-30f, 0f, 8f);
            holder.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            Assert.AreEqual(kept, entity.SharedMark(withMovement: true), "Its runner's game sends where it is, live.");
        }
        finally
        {
            Object.DestroyImmediate(holder);
        }
    }
}
