using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// A friend's side of raid creatures in co-op (<see cref="CoopCreatures"/>): on the host's run this game makes no
/// creatures of its own (<see cref="FromHost"/>) and shows the host's as puppets (<see cref="MonsterAI.MakeRemote"/>):
/// built from what the host says, moved, struck with and killed by its word, and this player's blows sent to it. What
/// this player wakes (an extraction alarm, an event's creatures) is asked of the host (<see cref="IRaidCreatureSource"/>).
/// If the host leaves the run first, the puppets become this game's own creatures. Plain C#.
/// </summary>
public sealed class CreaturePuppets : IRaidCreatureSource
{
    /// <summary>Seconds a friend waits, once the host has left the run, before the creatures become their own.</summary>
    private const float HostGoneSeconds = 2f;

    /// <summary>Seconds after which a strike that arrived late (a lost packet sent again) is no longer played.</summary>
    private const double StaleStrike = 0.6;

    // One of the host's creatures as shown here.
    private sealed class Puppet
    {
        public int Id;
        public MonsterAI Ai;
        public Damageable Health;
        public bool HitByMe;
    }

    private readonly Dictionary<int, Puppet> byId = new Dictionary<int, Puppet>();
    private readonly Dictionary<MonsterAI, Puppet> byCreature = new Dictionary<MonsterAI, Puppet>();
    private readonly List<MonsterAI> scratch = new List<MonsterAI>();
    private CoopLink link;
    private ContentDatabase content;
    private Scene scene;
    private bool following;
    private bool handedBack;
    private float hostGoneSince = -1f;

    /// <summary>True on the host's run: the host's game sends the creatures, none are made here.</summary>
    public static bool FromHost { get; private set; }

    public void Begin(CoopLink session)
    {
        link = session;
        content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        HandBack(quiet: true);
        MonsterAI.RemoteClock = () => link != null ? link.Now : 0.0;
        link.On(CoopCreatures.SpawnMessage, OnSpawn);
        link.On(CoopCreatures.PosesMessage, OnPoses);
        link.On(CoopCreatures.StrikeMessage, OnStrike);
        link.On(CoopCreatures.DiedMessage, OnDied);
        link.On(CoopCreatures.GoneMessage, OnGone);
        Damageable.AnyDamaged += OnAnyDamaged;
    }

    /// <summary>The session ended: mid-raid, puppets become this game's own creatures rather than standing still.</summary>
    public void End()
    {
        Damageable.AnyDamaged -= OnAnyDamaged;
        HandBack(quiet: true);
        MonsterAI.RemoteClock = null;
        link = null;
    }

    /// <summary>This friend is going into the host's run: from the raid's start, the host's game sends its creatures.</summary>
    public void FollowHost()
    {
        following = true;
        handedBack = false;
        SetFromHost(GameSession.ActiveRaid != null);
    }

    // The host's game makes the raid's creatures: none here (MonsterAI, spawn groups, events ask the raid's source).
    private void SetFromHost(bool fromHost)
    {
        FromHost = fromHost;
        if (fromHost)
        {
            CreatureSource.Remote = this;
        }
        else if (CreatureSource.Remote == this)
        {
            CreatureSource.Remote = null;
        }
    }

    /// <summary>Ten times a second.</summary>
    public void Tick()
    {
        Scene now = SceneManager.GetActiveScene();
        if (now != scene)
        {
            scene = now;
            byId.Clear();
            byCreature.Clear();
            hostGoneSince = -1f;
            handedBack = false;
        }

        bool raid = GameSession.ActiveRaid != null;
        if (!raid)
        {
            following = false;
        }

        // On the host's run (followed in from its start) or in their Haven, the host's game makes the creatures; unless it
        // handed them over here when the host left.
        SetFromHost((raid ? following : HavenVisit.IsVisiting) && !handedBack);
        // Only once the place is set up (the raid running, the Haven loaded), not while its scene is still coming in.
        bool settled = raid ? RaidDirector.Current != null : HavenVisit.IsVisiting && PersistentScene.For(scene) != null;
        if (!settled)
        {
            hostGoneSince = -1f;
            return;
        }

        if (HostHere())
        {
            hostGoneSince = -1f;
            // Back in their Haven: their game makes the creatures again (these make way below, theirs are sent anew).
            if (handedBack && !raid)
            {
                handedBack = false;
                SetFromHost(true);
            }
        }
        else if (FromHost && hostGoneSince < 0f)
        {
            hostGoneSince = Time.unscaledTime;
        }
        else if (FromHost && Time.unscaledTime - hostGoneSince >= HostGoneSeconds)
        {
            // The host left: the creatures here are this game's from now on.
            HandBack(quiet: !raid);
            return;
        }

        if (!FromHost)
        {
            return;
        }

        // This game's own creatures make way for the host's (a safety net; MonsterAI removes them as they wake).
        scratch.Clear();
        foreach (MonsterAI creature in MonsterAI.All)
        {
            if (creature != null && !creature.IsRemote && creature.gameObject.scene == scene)
            {
                scratch.Add(creature);
            }
        }

        foreach (MonsterAI creature in scratch)
        {
            Object.Destroy(creature.gameObject);
        }
    }

    // The host stands in this place (their avatar's place is this game's).
    private static bool HostHere()
    {
        FixedString128Bytes place = CoopPlace.Key;
        foreach (NetworkPlayerAvatar friend in NetworkPlayerAvatar.All)
        {
            if (friend != null && friend.OwnerClientId == NetworkManager.ServerClientId && friend.Look.Place.Equals(place))
            {
                return true;
            }
        }

        return false;
    }

    private void HandBack(bool quiet)
    {
        bool hadAny = byId.Count > 0;
        foreach (Puppet puppet in byId.Values)
        {
            if (puppet.Ai != null)
            {
                puppet.Ai.MakeLocal();
                if (puppet.Health != null)
                {
                    puppet.Health.HealthFloor = 0f;
                }
            }
        }

        byId.Clear();
        byCreature.Clear();
        following = false;
        handedBack = true;
        hostGoneSince = -1f;
        SetFromHost(false);
        if (hadAny && !quiet)
        {
            GameMessages.Post($"{HavenVisit.HostName} has left the raid. You're on your own out here.", MessageKind.Warning);
        }
    }

    private void OnSpawn(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out int count);
        for (int i = 0; i < count; i++)
        {
            reader.ReadValueSafe(out int id);
            reader.ReadValueSafe(out string prefabId);
            reader.ReadValueSafe(out Vector3 position);
            reader.ReadValueSafe(out float yaw);
            reader.ReadValueSafe(out byte health);
            reader.ReadValueSafe(out string affixes);
            if (!FromHost || byId.ContainsKey(id))
            {
                continue;
            }

            SaveableEntity prefab = content != null ? content.FindPrefab(prefabId) : null;
            if (prefab == null)
            {
                Debug.LogWarning($"Co-op: no creature prefab '{prefabId}' to show the host's creature {id}.");
                continue;
            }

            GameObject copy;
            MonsterAI.SpawningRemote = true;
            try
            {
                copy = Object.Instantiate(prefab.gameObject, position, Quaternion.Euler(0f, yaw, 0f));
            }
            finally
            {
                MonsterAI.SpawningRemote = false;
            }

            if (!copy.TryGetComponent(out MonsterAI ai))
            {
                Object.Destroy(copy);
                continue;
            }

            ai.MakeRemote();
            MakeElite(ai, affixes);
            var puppet = new Puppet { Id = id, Ai = ai, Health = copy.GetComponent<Damageable>() };
            if (puppet.Health != null)
            {
                puppet.Health.HealthFloor = 1f;
                puppet.Health.ShowShare(health / 255f);
            }

            byId[id] = puppet;
            byCreature[ai] = puppet;
        }
    }

    // The same elite in this game: the run's affixes by name, scaled as this raid scales its creatures.
    private static void MakeElite(MonsterAI puppet, string names)
    {
        RaidSession run = GameSession.ActiveRaid;
        RaidDirector director = RaidDirector.Current;
        if (string.IsNullOrEmpty(names) || run == null || director == null)
        {
            return;
        }

        var chosen = new List<EliteAffixDefinition>();
        foreach (string name in names.Split('|'))
        {
            foreach (EliteAffixDefinition affix in run.Definition.EliteAffixes)
            {
                if (affix != null && affix.name == name)
                {
                    chosen.Add(affix);
                    break;
                }
            }
        }

        if (chosen.Count > 0)
        {
            EliteCreature.Make(puppet, chosen, run.Difficulty, director.HealthMultiplier, director.PowerMultiplier);
        }
    }

    private void OnPoses(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out double time);
        reader.ReadValueSafe(out ushort count);
        for (int i = 0; i < count; i++)
        {
            reader.ReadValueSafe(out ushort id);
            reader.ReadValueSafe(out Vector3 position);
            reader.ReadValueSafe(out byte yaw);
            reader.ReadValueSafe(out byte speed);
            reader.ReadValueSafe(out byte state);
            reader.ReadValueSafe(out byte health);
            reader.ReadValueSafe(out byte hunting);
            if (byId.TryGetValue(id, out Puppet puppet) && puppet.Ai != null)
            {
                puppet.Ai.ApplyRemotePose(time, position, yaw * (360f / 256f), speed / 10f, (MonsterAI.State)state, (MonsterAI.RemoteHunt)hunting);
                if (puppet.Health != null)
                {
                    puppet.Health.ShowShare(health / 255f);
                }
            }
        }
    }

    private void OnStrike(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out int id);
        reader.ReadValueSafe(out int action);
        reader.ReadValueSafe(out ulong at);
        reader.ReadValueSafe(out double time);
        reader.ReadValueSafe(out Vector3 from);
        reader.ReadValueSafe(out float yaw);
        if (link == null || link.Now - time > StaleStrike || !byId.TryGetValue(id, out Puppet puppet) || puppet.Ai == null)
        {
            return;
        }

        IDamageable me = at == link.LocalId && Player.Current != null ? Player.Current.GetComponent<IDamageable>() : null;
        puppet.Ai.PlayRemoteStrike(action, me, from, yaw);
    }

    private void OnDied(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out int id);
        if (!byId.TryGetValue(id, out Puppet puppet))
        {
            return;
        }

        Forget(puppet);
        if (puppet.Health != null)
        {
            // Killed by this player when they had a hand in it: the kill counts for them, and the loot is theirs either way.
            Combatant killer = puppet.HitByMe && Player.Current != null ? Player.Current.GetComponent<Combatant>() : null;
            puppet.Health.Finish(killer);
        }
    }

    private void OnGone(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out int id);
        if (byId.TryGetValue(id, out Puppet puppet))
        {
            Forget(puppet);
            if (puppet.Ai != null)
            {
                Object.Destroy(puppet.Ai.gameObject);
            }
        }
    }

    private void Forget(Puppet puppet)
    {
        byId.Remove(puppet.Id);
        if (puppet.Ai != null)
        {
            byCreature.Remove(puppet.Ai);
        }
    }

    /// <summary>An extraction beacon's alarm (a waiting spawn group) this player woke: the host's game fills it.</summary>
    public bool Wake(SpawnGroup group)
    {
        if (link == null || group == null)
        {
            return false;
        }

        string path = RaidGenerator.PathOf(group.transform);
        using var writer = CoopLink.Writer(16 + path.Length * 2);
        writer.WriteValueSafe(path);
        link.ToHost(CoopCreatures.WakeMessage, writer);
        return true;
    }

    /// <summary>A creature an event made for this player: the host's game makes it, by prefab id.</summary>
    public void Spawn(GameObject prefab, Vector3 position, float yaw, bool hunt)
    {
        string prefabId = prefab != null && prefab.TryGetComponent(out SaveableEntity entity) ? entity.PrefabId : null;
        if (link == null || string.IsNullOrEmpty(prefabId))
        {
            Debug.LogWarning($"Co-op: '{(prefab != null ? prefab.name : "nothing")}' has no prefab id, so the host's game can't make it for this player.");
            return;
        }

        using var writer = CoopLink.Writer(32 + prefabId.Length * 2);
        writer.WriteValueSafe(prefabId);
        writer.WriteValueSafe(position);
        writer.WriteValueSafe(yaw);
        writer.WriteValueSafe(hunt);
        link.ToHost(CoopCreatures.CallMessage, writer);
    }

    // A blow this player landed on one of the host's creatures goes to the host.
    private void OnAnyDamaged(Damageable victim, DamageInfo damage, DamageResult result)
    {
        if (!FromHost || link == null || victim == null || damage.Source == null || !Player.IsPlayer(damage.Source.gameObject)
            || !victim.TryGetComponent(out MonsterAI creature) || !byCreature.TryGetValue(creature, out Puppet puppet))
        {
            return;
        }

        puppet.HitByMe = true;
        using var writer = new FastBufferWriter(96, Allocator.Temp);
        writer.WriteValueSafe(puppet.Id);
        writer.WriteValueSafe(damage.Amount);
        writer.WriteValueSafe((byte)damage.Type);
        writer.WriteValueSafe(damage.IsCritical);
        writer.WriteValueSafe(damage.HitPoint);
        writer.WriteValueSafe(damage.Direction);
        writer.WriteValueSafe(damage.Poise);
        writer.WriteValueSafe((int)damage.Technique);
        link.ToHost(CoopCreatures.HitMessage, writer);
    }

    // Entering Play doesn't reload scripts in this project: statics carry over from the last session unless cleared.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        FromHost = false;
        CreatureSource.Remote = null;
    }
}
