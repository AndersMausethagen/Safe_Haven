using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Keeps every copy of a Haven in step during a co-op visit, through the save system's own objects
/// (<see cref="SaveableEntity"/>): a building placed or taken down, a node gathered or grown back, a tree felled, an item
/// dropped or picked up, a cart loaded.
/// <list type="bullet">
/// <item><see cref="Collect"/>, a couple of times a second: each loaded object's <see cref="SaveableEntity.SharedMark"/>
/// is compared with the last one seen, and the objects that changed (or were created here, or are gone) are packed for
/// sending, in their save format and without the state each player keeps to themselves (<see cref="ISharedState"/>).</item>
/// <item><see cref="Apply"/>: a friend's changes go into this game's remembered world and onto the loaded objects;
/// their marks are noted so they aren't sent back.</item>
/// </list>
/// The host sends everything that changed; a guest sends what changed in their copy, except where the host's world moves
/// things about by itself (only objects placed while playing are moved by guests). Whoever changed something last wins.
/// Plain C#: <see cref="CoopSession"/> does the sending.
/// </summary>
public sealed class HavenSync : IDisposable
{
    /// <summary>Seconds between looks for changes.</summary>
    public const float Interval = 0.5f;

    /// <summary>Packed bytes per message, under the transports' payload limit.</summary>
    public const int PackedLimit = WorldTransfer.PieceSize;

    [Serializable]
    public sealed class Batch
    {
        public string scene;
        public List<EntitySaveData> entities = new List<EntitySaveData>();
    }

    private readonly Dictionary<string, int> marks = new Dictionary<string, int>();
    private int settlementMark;
    private readonly HashSet<string> fresh = new HashSet<string>();
    private readonly HashSet<string> seen = new HashSet<string>();
    private readonly List<string> gone = new List<string>();
    private SceneState watched;

    public HavenSync()
    {
        SaveableEntity.Created += OnCreated;
    }

    public void Dispose()
    {
        SaveableEntity.Created -= OnCreated;
    }

    /// <summary>Forgets what was seen: the next look only takes note (a session starting or ending).</summary>
    public void Reset()
    {
        settlementMark = 0;
        marks.Clear();
        fresh.Clear();
        watched = null;
    }

    /// <summary>
    /// The objects that changed here since the last look, packed into messages (none when nothing changed or no shared
    /// place is loaded). The first look at a scene only takes note. <paramref name="withMovement"/>: the host's view, where
    /// anything that is saved with its position may move; a guest only moves what was placed while playing.
    /// </summary>
    public List<byte[]> Collect(bool withMovement)
    {
        var packets = new List<byte[]>();
        PersistentScene scene = Watchable();
        if (scene == null)
        {
            return packets;
        }

        if (scene.State != watched)
        {
            watched = scene.State;
            marks.Clear();
        }

        var changed = new List<EntitySaveData>();
        Scene unityScene = scene.gameObject.scene;
        foreach (SaveableEntity entity in SaveableEntity.Live)
        {
            if (entity == null || entity.IsGone || !entity.IsPersistent || !entity.HasStarted || entity.gameObject.scene != unityScene || string.IsNullOrEmpty(entity.Id))
            {
                continue;
            }

            seen.Add(entity.Id);
            int mark = entity.SharedMark(withMovement);
            bool known = marks.TryGetValue(entity.Id, out int before);
            marks[entity.Id] = mark;
            if (known ? before != mark : fresh.Remove(entity.Id))
            {
                EntityState state = entity.CaptureNow();
                if (state != null)
                {
                    changed.Add(SaveMapper.ToData(state, entity.IsSharedKey));
                }
            }
        }

        // Gone since the last look: taken down, picked up, broken.
        gone.Clear();
        foreach (string id in marks.Keys)
        {
            if (!seen.Contains(id))
            {
                gone.Add(id);
            }
        }

        foreach (string id in gone)
        {
            marks.Remove(id);
            if (watched.Entities.TryGetValue(id, out EntityState state) && state.Destroyed)
            {
                changed.Add(SaveMapper.ToData(state));
            }
        }

        seen.Clear();
        if (changed.Count > 0)
        {
            Pack(watched.Scene, changed, 0, changed.Count, packets);
        }

        return packets;
    }

    /// <summary>
    /// Takes on a friend's changes: into the remembered world (also when that place isn't loaded, say the host is on a
    /// raid), and onto the loaded objects, creating what was placed and removing what is gone.
    /// </summary>
    /// <param name="noteMarks">
    /// False on the host for a guest's changes: its marks are left as they were, so its next look sends the result to everyone,
    /// the sender included. When two players change the same thing at once, both games end with the host's.
    /// </param>
    public int Apply(byte[] packed, bool withMovement, bool noteMarks = true)
    {
        Batch batch = WorldTransfer.Unpack<Batch>(packed);
        if (batch?.entities == null || string.IsNullOrEmpty(batch.scene))
        {
            return 0;
        }

        if (!GameSession.World.Scenes.TryGetValue(batch.scene, out SceneState sceneState))
        {
            sceneState = GameSession.World.GetOrAdd(batch.scene, true);
        }

        PersistentScene live = PersistentScene.For(SceneManager.GetActiveScene());
        bool loaded = live != null && live.State == sceneState;
        Dictionary<string, SaveableEntity> here = loaded ? LoadedIn(live.gameObject.scene) : null;
        int applied = 0;
        foreach (EntitySaveData data in batch.entities)
        {
            if (string.IsNullOrEmpty(data?.id))
            {
                continue;
            }

            SaveMapper.ReadInto(data, sceneState.GetOrAdd(data.id));
            applied++;
            if (!loaded)
            {
                continue;
            }

            if (here.TryGetValue(data.id, out SaveableEntity entity))
            {
                // Only what was sent: components it didn't mention keep their own state.
                var incoming = new EntityState(data.id);
                SaveMapper.ReadInto(data, incoming);
                entity.ApplyShared(incoming);
                if (data.destroyed)
                {
                    marks.Remove(data.id);
                }
                else if (noteMarks && entity.HasStarted && sceneState == watched)
                {
                    marks[data.id] = entity.SharedMark(withMovement);
                }
            }
            else if (data.spawned && !data.destroyed)
            {
                live.Recreate(sceneState.Entities[data.id]);
            }
        }

        return applied;
    }

    // ---- The settlement: the host's, mirrored in each guest's game

    /// <summary>
    /// The host's settlement, packed, when it changed since the last look (null otherwise): residents and their jobs, the
    /// portal and ward, rank, workshop queues. Progress that only ticks (purifying, a workshop's current craft) is left out
    /// of the comparison, so it is sent with the next real change.
    /// </summary>
    public byte[] CollectSettlement()
    {
        SettlementSaveData data = SaveMapper.ToData(GameSession.Here);
        float purifying = data.purificationProgress;
        var progress = new List<float>();
        foreach (StationSaveData station in data.stations)
        {
            progress.Add(station.progress);
            station.progress = 0f;
        }

        data.purificationProgress = 0f;
        int mark = JsonUtility.ToJson(data).GetHashCode();
        if (mark == settlementMark)
        {
            return null;
        }

        settlementMark = mark;
        data.purificationProgress = purifying;
        for (int i = 0; i < progress.Count; i++)
        {
            data.stations[i].progress = progress[i];
        }

        return WorldTransfer.Pack(data);
    }

    /// <summary>A guest takes on the host's settlement, in place, so what listens to it (residents, the portal) redraws.</summary>
    public static void ApplySettlement(byte[] packed, IContentLookup content)
    {
        SettlementSaveData data = WorldTransfer.Unpack<SettlementSaveData>(packed);
        if (data != null)
        {
            GameSession.Here.TakeOn(SaveMapper.SettlementFrom(data, content));
            Territory.MirrorArrived();
        }
    }

    // The persistent scene being played, when its state is the session's world (not a scene left behind by a load).
    private static PersistentScene Watchable()
    {
        if (SceneLoader.IsLeaving || GameSession.ActiveRaid != null)
        {
            return null;
        }

        PersistentScene scene = PersistentScene.For(SceneManager.GetActiveScene());
        return scene != null && scene.State != null && GameSession.World.Scenes.TryGetValue(scene.State.Scene, out SceneState current) && current == scene.State
            ? scene
            : null;
    }

    private static Dictionary<string, SaveableEntity> LoadedIn(Scene scene)
    {
        var byId = new Dictionary<string, SaveableEntity>();
        foreach (SaveableEntity entity in SaveableEntity.Live)
        {
            if (entity != null && !string.IsNullOrEmpty(entity.Id) && entity.gameObject.scene == scene)
            {
                byId[entity.Id] = entity;
            }
        }

        return byId;
    }

    // Halves a batch until each part fits one message.
    private static void Pack(string scene, List<EntitySaveData> entities, int from, int count, List<byte[]> into)
    {
        byte[] packed = WorldTransfer.Pack(new Batch { scene = scene, entities = entities.GetRange(from, count) });
        if (packed.Length <= PackedLimit)
        {
            into.Add(packed);
        }
        else if (count > 1)
        {
            int half = count / 2;
            Pack(scene, entities, from, half, into);
            Pack(scene, entities, from + half, count - half, into);
        }
        else
        {
            Debug.LogWarning($"Co-op: '{entities[from].id}' is too big to share ({packed.Length} bytes packed).");
        }
    }

    private void OnCreated(SaveableEntity entity)
    {
        if (entity != null && !string.IsNullOrEmpty(entity.Id))
        {
            fresh.Add(entity.Id);
        }
    }
}
