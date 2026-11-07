using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// A friend's side of raid creatures in co-op (<see cref="CoopCreatures"/>): on the host's run this game makes no
/// creatures of its own (<see cref="FromHost"/>) and shows the host's as puppets (<see cref="MonsterAI.MakeRemote"/>):
/// built from what the host says, moved, struck with and killed by its word, and this player's blows sent to it. If the
/// host leaves the run first, the puppets become this game's own creatures. Plain C#.
/// </summary>
public sealed class CreaturePuppets
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
        FromHost = GameSession.ActiveRaid != null;
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
        }

        FromHost = following && GameSession.ActiveRaid != null;
        if (GameSession.ActiveRaid == null)
        {
            following = false;
            return;
        }

        // In the raid: this game's own creatures make way for the host's (a safety net; MonsterAI removes them as they wake).
        if (!FromHost || RaidDirector.Current == null)
        {
            return;
        }

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

        // The host left the run: the creatures here are this game's from now on.
        bool hostHere = false;
        FixedString128Bytes place = CoopPlace.Key;
        foreach (NetworkPlayerAvatar friend in NetworkPlayerAvatar.All)
        {
            hostHere |= friend != null && friend.OwnerClientId == NetworkManager.ServerClientId && friend.Look.Place.Equals(place);
        }

        if (hostHere)
        {
            hostGoneSince = -1f;
        }
        else if (hostGoneSince < 0f)
        {
            hostGoneSince = Time.unscaledTime;
        }
        else if (Time.unscaledTime - hostGoneSince >= HostGoneSeconds)
        {
            HandBack(quiet: false);
        }
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
        FromHost = false;
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
            if (byId.TryGetValue(id, out Puppet puppet) && puppet.Ai != null)
            {
                puppet.Ai.ApplyRemotePose(time, position, yaw * (360f / 256f), speed / 10f, (MonsterAI.State)state);
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
    private static void ResetForNewPlaySession() => FromHost = false;
}
