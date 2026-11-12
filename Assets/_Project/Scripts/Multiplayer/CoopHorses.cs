using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Horses in co-op: <b>whoever has a horse in hand runs it; every other game draws it.</b>
/// <list type="bullet">
/// <item>Each horse has one game that runs it: its <i>keeper</i>'s, the player who rides it, holds it on the lasso, has its
/// menu, bags or name open, or has it following or coming to them; with no keeper, the host's (when the host is in that place;
/// otherwise each game runs its own). A player takes a horse in hand with <see cref="TakeInHand"/> (riding, the lasso, its
/// menu, a whistle): at once in their own game, and the host settles it (<c>HorseClaim</c>, <c>HorseKeeper</c>, or
/// <c>HorseRefused</c> when a friend already has it). Let go of for a moment and a guest hands it back with its whole state
/// (<c>HorseLetGo</c>).</item>
/// <item>The game that runs a horse sends where it is, its animation, health and stamina, and who rides it or holds its rope
/// (<c>HorsePoses</c>, unreliable, fast while it moves), and everything else about it whenever that changes
/// (<c>HorseState</c>: <see cref="Horse.CaptureShared"/>, which a copy takes on with <see cref="Horse.RestoreShared"/>).</item>
/// <item>The other games draw it as a puppet (<see cref="Horse.SetPuppet"/>): a horse of the Haven's world is already there
/// (the same save id: its key); a wild horse, or one brought on a raid, is made from its prefab here and goes with its
/// runner (<c>HorseGone</c>). A friend riding is seated on the puppet (<see cref="SeatOf"/>); a friend's lasso is drawn to it.</item>
/// <item>Whose a horse is: its owner (<see cref="Horse.OwnerId"/>). A horse a visitor tames in the host's Haven is theirs and
/// goes home with them (<see cref="HavenVisit.KeepHome"/>); the host's game lets go of it when they leave.</item>
/// </list>
/// HavenSync shares only that a horse was made or is gone (<see cref="SaveableEntity.SyncedElsewhere"/>). Plain C#.
/// </summary>
public sealed class CoopHorses : ICoopPart
{
    private const string StateMessage = "SafeHaven.HorseState";
    private const string PosesMessage = "SafeHaven.HorsePoses";
    private const string ClaimMessage = "SafeHaven.HorseClaim";
    private const string KeeperMessage = "SafeHaven.HorseKeeper";
    private const string RefusedMessage = "SafeHaven.HorseRefused";
    private const string LetGoMessage = "SafeHaven.HorseLetGo";
    private const string GoneMessage = "SafeHaven.HorseGone";
    private const string HelloMessage = "SafeHaven.HorseHello";

    /// <summary>Seconds between poses of a horse that moves (or is ridden, or fights the rope), and of one standing about.</summary>
    public const float FastPoseSeconds = 1f / 15f;
    public const float SlowPoseSeconds = 0.25f;
    private const float StateLookSeconds = 0.5f;
    private const float LetGoSeconds = 1.5f;
    private const float KeepersLookSeconds = 1f;
    private const int PosesPerMessage = 8;

    [System.Flags]
    public enum PoseFlags : byte { None = 0, Seated = 1, Lasso = 2, Dead = 4, Mounting = 8 }

    private sealed class Entry
    {
        public uint Key;
        public Horse Horse;
        public bool Seen;
        public bool SpawnedHere;
        // Who made it, for a copy made here from their game's word (their wild horse, the horse they brought on a raid).
        public ulong Maker = CoopLink.NoOne;
        public bool InWorld;
        public bool RunHere;
        public bool Replaced;
        public string PrefabId = "";
        public string State;
        public readonly PoseBuffer Poses = new PoseBuffer(24);
        public AnimatorMirror Mirror;
        public float NextPose;
        public int SentMark;
        public bool MarkSent;
        public float Health = 1f;
        public float Stamina = 1f;
        public float AnimatorSpeed = 1f;
        public PoseFlags Flags;
        public ulong Runner;
        public float Strain;
        public float LetGoAt = -1f;
        public Vector3 LastPosition;
        public float LastYaw;
        public LineRenderer Rope;
    }

    private struct PoseIn
    {
        public uint Key;
        public PoseSnapshot Pose;
        public byte Health;
        public byte Stamina;
        public PoseFlags Flags;
        public byte Strain;
        public ushort Speed;
    }

    private static CoopHorses current;
    private static Material ropeMaterial;

    private readonly Dictionary<uint, Entry> entries = new Dictionary<uint, Entry>();
    private readonly Dictionary<uint, ulong> keepers = new Dictionary<uint, ulong>();
    private readonly Dictionary<ulong, string> owners = new Dictionary<ulong, string>();
    private readonly List<Entry> scratch = new List<Entry>();
    private readonly List<uint> scratchKeys = new List<uint>();
    private readonly List<PoseIn> received = new List<PoseIn>();
    private readonly List<Entry> outgoing = new List<Entry>();
    private readonly HashSet<ulong> friendsHere = new HashSet<ulong>();
    private readonly HashSet<ulong> friendsSeen = new HashSet<ulong>();
    private MultiplayerSettings settings;
    private CoopLink link;
    private Scene scene;
    private int placeHash;
    private string placeText;
    private uint counter;
    private float nextStateLook;
    private float nextKeepersLook;
    private AnimatorFrame scratchFrame;
    private ContentDatabase content;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => current = null;

    public CoopHorses(MultiplayerSettings settings)
    {
        this.settings = settings;
    }

    // ---- What the rest of the game asks

    /// <summary>
    /// Takes the horse in hand before riding, roping, opening its menu or calling it: true when this game may now run it
    /// (always, outside co-op); false, with a word to the player, when a friend has it in hand.
    /// </summary>
    public static bool TakeInHand(Horse horse) => current == null || horse == null || current.Take(horse);

    /// <summary>The friend who has this horse in hand, by name, or null when nobody else does (or outside co-op).</summary>
    public static string HeldByFriend(Horse horse)
    {
        if (current == null || horse == null || current.link == null)
        {
            return null;
        }

        uint key = current.KeyOf(horse);
        return current.keepers.TryGetValue(key, out ulong keeper) && keeper != current.link.LocalId && current.link.IsConnected(keeper)
            ? NetworkPlayerAvatar.NameOf(keeper) : null;
    }

    /// <summary>The puppet horse a friend sits on here (seated, not climbing on or off), or null: their body goes on its saddle.</summary>
    public static Horse SeatOf(ulong clientId)
    {
        if (current == null)
        {
            return null;
        }

        foreach (Entry entry in current.entries.Values)
        {
            if (entry.Horse != null && entry.Horse.IsPuppet && entry.Runner == clientId && (entry.Flags & PoseFlags.Seated) != 0)
            {
                return entry.Horse;
            }
        }

        return null;
    }

    /// <summary>A wild horse this game ran became a tamed one (a new saved copy): friends' copies of the wild one go.</summary>
    public static void Replaced(Horse wild, Horse tamed)
    {
        if (current == null || wild == null)
        {
            return;
        }

        if (current.entries.TryGetValue(current.KeyOf(wild), out Entry entry))
        {
            entry.Replaced = true;
        }

        TakeInHand(tamed);
    }

    // ---- The part's life

    public void Begin(CoopLink session)
    {
        link = session;
        current = this;
        content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        Clear();
        link.On(StateMessage, OnState);
        link.On(PosesMessage, OnPoses);
        link.On(KeeperMessage, OnKeeper);
        link.On(GoneMessage, OnGone);
        if (link.IsHost)
        {
            link.On(ClaimMessage, OnClaim);
            link.On(LetGoMessage, OnLetGo);
            link.On(HelloMessage, (sender, reader) =>
            {
                reader.ReadValueSafe(out string owner);
                owners[sender] = owner ?? "";
            });
        }
        else
        {
            link.On(RefusedMessage, OnRefused);
            using FastBufferWriter writer = CoopLink.Writer();
            writer.WriteValueSafe(GameSession.Playtest.Id ?? "");
            link.ToHost(HelloMessage, writer);
        }
    }

    public void FriendLeft(ulong clientId)
    {
        // What they held is the host's to run again; the horses they tamed here went home with them.
        scratchKeys.Clear();
        foreach (KeyValuePair<uint, ulong> pair in keepers)
        {
            if (pair.Value == clientId)
            {
                scratchKeys.Add(pair.Key);
            }
        }

        foreach (uint key in scratchKeys)
        {
            SetKeeper(key, CoopLink.NoOne);
        }

        if (owners.TryGetValue(clientId, out string owner) && !string.IsNullOrEmpty(owner))
        {
            SendHome(owner);
        }

        owners.Remove(clientId);
        friendsSeen.Remove(clientId);
    }

    public void End()
    {
        // Back to one game: every horse here is this game's own again.
        foreach (Entry entry in entries.Values)
        {
            if (entry.Horse == null)
            {
                continue;
            }

            if (entry.SpawnedHere)
            {
                Object.Destroy(entry.Horse.gameObject);
            }
            else if (entry.Horse.IsPuppet)
            {
                entry.Horse.SetPuppet(false);
            }

            if (entry.Rope != null)
            {
                Object.Destroy(entry.Rope.gameObject);
            }
        }

        Clear();
        link = null;
        if (current == this)
        {
            current = null;
        }
    }

    private void Clear()
    {
        foreach (Entry entry in entries.Values)
        {
            entry.Poses.Clear();
        }

        entries.Clear();
        keepers.Clear();
        owners.Clear();
        friendsSeen.Clear();
        scene = default;
    }

    public void Tick()
    {
        if (link == null)
        {
            return;
        }

        Scene active = SceneManager.GetActiveScene();
        if (active != scene)
        {
            ChangedPlace(active);
        }

        // The place's name (a raid's seed with it) can settle a moment after the scene does.
        string here = CoopPlace.Here;
        if (!ReferenceEquals(here, placeText))
        {
            placeText = here;
            placeHash = Fnv(here);
        }

        bool hostHere = link.IsHost || HostIsHere();
        NoteFriendsHere();
        foreach (Horse horse in Horse.All)
        {
            if (horse != null && horse.gameObject.scene == scene)
            {
                EntryFor(horse);
            }
        }

        bool lookAtStates = Time.unscaledTime >= nextStateLook;
        if (lookAtStates)
        {
            nextStateLook = Time.unscaledTime + StateLookSeconds;
        }

        outgoing.Clear();
        scratch.Clear();
        scratch.AddRange(entries.Values);
        foreach (Entry entry in scratch)
        {
            if (entry.Horse == null)
            {
                if (entry.Seen)
                {
                    Vanished(entry);
                }

                continue;
            }

            if (entry.SpawnedHere && entry.Maker != link.LocalId && !link.IsConnected(entry.Maker))
            {
                // Its maker has gone, and their horse with them.
                entries.Remove(entry.Key);
                entry.Poses.Clear();
                Object.Destroy(entry.Horse.gameObject);
                continue;
            }

            ulong runner = RunnerOf(entry, hostHere);
            entry.RunHere = runner == link.LocalId;
            if (entry.RunHere)
            {
                if (entry.Horse.IsPuppet)
                {
                    BecomeLocal(entry);
                }

                Run(entry, lookAtStates);
            }
            else
            {
                if (!entry.Horse.IsPuppet)
                {
                    entry.Horse.SetPuppet(true);
                }

                Draw(entry);
            }
        }

        SendPoses();
        if (link.IsHost && Time.unscaledTime >= nextKeepersLook)
        {
            nextKeepersLook = Time.unscaledTime + KeepersLookSeconds;
            ReleaseAbsentKeepers();
        }
    }

    // ---- Places and keys

    private void ChangedPlace(Scene now)
    {
        // A new scene: what was drawn belonged to the last one (its objects went with it). What this game ran there goes
        // back to the host (kept, in the Haven) or is gone for friends (a wild horse, a raid's).
        scratch.Clear();
        scratch.AddRange(entries.Values);
        foreach (Entry entry in scratch)
        {
            if (entry.Seen && entry.RunHere && link != null)
            {
                Vanished(entry);
            }

            entry.Poses.Clear();
        }

        entries.Clear();
        friendsSeen.Clear();
        scene = now;
        placeText = CoopPlace.Here;
        placeHash = Fnv(placeText);
    }

    // A friend newly in this place is sent everything about the horses this game runs.
    private void NoteFriendsHere()
    {
        friendsHere.Clear();
        foreach (NetworkPlayerAvatar avatar in NetworkPlayerAvatar.All)
        {
            if (avatar != null && avatar.OwnerClientId != link.LocalId && avatar.Look.Place.Equals(CoopPlace.Key))
            {
                friendsHere.Add(avatar.OwnerClientId);
            }
        }

        bool newcomer = false;
        foreach (ulong friend in friendsHere)
        {
            newcomer |= friendsSeen.Add(friend);
        }

        friendsSeen.IntersectWith(friendsHere);
        if (newcomer)
        {
            foreach (Entry entry in entries.Values)
            {
                entry.MarkSent = false;
            }
        }
    }

    private bool HostIsHere()
    {
        foreach (NetworkPlayerAvatar avatar in NetworkPlayerAvatar.All)
        {
            if (avatar != null && avatar.OwnerClientId == NetworkManager.ServerClientId)
            {
                return avatar.Look.Place.Equals(CoopPlace.Key);
            }
        }

        return false;
    }

    // Who runs it: whoever has it in hand; else the game that made a copy drawn here; else the host, when the host is here.
    private ulong RunnerOf(Entry entry, bool hostHere)
    {
        if (keepers.TryGetValue(entry.Key, out ulong keeper) && (keeper == link.LocalId || link.IsConnected(keeper)))
        {
            return keeper;
        }

        if (entry.SpawnedHere && entry.Maker != CoopLink.NoOne)
        {
            return entry.Maker;
        }

        return hostHere ? NetworkManager.ServerClientId : link.LocalId;
    }

    /// <summary>How games name a horse: a saved one by its save id (the same in every copy of the Haven); one made while
    /// playing and not saved (a wild horse), by the game that made it.</summary>
    private uint KeyOf(Horse horse)
    {
        if (horse.CoopKey != 0)
        {
            return horse.CoopKey;
        }

        string id = horse.SaveId;
        uint key = !string.IsNullOrEmpty(id) ? KeyForSaveId(id) : 0x80000000u | (((uint)link.LocalId & 0x7Fu) << 24) | (++counter & 0xFFFFFFu);
        horse.CoopKey = key;
        return key;
    }

    /// <summary>The key of a saved horse (pure): its save id's hash, never one a game gives a horse it made (high bit clear).</summary>
    public static uint KeyForSaveId(string saveId) => (uint)Fnv(saveId) & 0x7FFFFFFFu;

    private static int Fnv(string text)
    {
        unchecked
        {
            uint hash = 2166136261u;
            foreach (char c in text ?? "")
            {
                hash = (hash ^ c) * 16777619u;
            }

            return (int)hash;
        }
    }

    private Entry EntryFor(Horse horse)
    {
        uint key = KeyOf(horse);
        if (!entries.TryGetValue(key, out Entry entry))
        {
            entry = new Entry { Key = key };
            entries[key] = entry;
        }

        if (entry.Horse != horse)
        {
            entry.Horse = horse;
            entry.Seen = true;
            entry.Mirror = horse.Motion != null && horse.Motion.Animator != null ? new AnimatorMirror(horse.Motion.Animator) : null;
            entry.InWorld = horse.TryGetComponent(out SaveableEntity saved) && !string.IsNullOrEmpty(saved.Id) && saved.IsPersistent;
            entry.PrefabId = saved != null ? saved.PrefabId ?? "" : "";
            entry.LastPosition = horse.transform.position;
            entry.LastYaw = horse.transform.eulerAngles.y;
            // A state that came before the horse did (one a friend tamed, made here by HavenSync a moment later).
            if (entry.State != null && RunnerOf(entry, link.IsHost || HostIsHere()) != link.LocalId)
            {
                horse.SetPuppet(true);
                horse.RestoreShared(entry.State);
            }
        }

        return entry;
    }

    // ---- Running a horse here

    private void Run(Entry entry, bool lookAtState)
    {
        Horse horse = entry.Horse;
        entry.Runner = link.LocalId;
        entry.LastPosition = horse.transform.position;
        entry.LastYaw = horse.transform.eulerAngles.y;
        entry.Health = horse.Health.HealthFraction;
        if (lookAtState)
        {
            int mark = horse.ShareMark();
            if (!entry.MarkSent || mark != entry.SentMark)
            {
                entry.SentMark = mark;
                entry.MarkSent = true;
                entry.State = horse.CaptureShared();
                SendState(entry);
            }
        }

        if (Time.unscaledTime >= entry.NextPose)
        {
            bool fast = horse.Rider != null || (horse.Taming != null && horse.Taming.Current != HorseTaming.Phase.None)
                        || horse.Motion.Velocity.sqrMagnitude > 1f;
            entry.NextPose = Time.unscaledTime + (fast ? FastPoseSeconds : SlowPoseSeconds);
            outgoing.Add(entry);
        }

        // Kept by this player: held while ridden, roped, followed, called or its windows are open; let go a moment after.
        if (keepers.TryGetValue(entry.Key, out ulong keeper) && keeper == link.LocalId)
        {
            if (Held(horse))
            {
                entry.LetGoAt = -1f;
            }
            else if (entry.LetGoAt < 0f)
            {
                entry.LetGoAt = Time.unscaledTime + LetGoSeconds;
            }
            else if (Time.unscaledTime >= entry.LetGoAt)
            {
                LetGo(entry);
            }
        }
        else if (link.IsHost && Held(horse) && !keepers.ContainsKey(entry.Key))
        {
            // The host's own horse in hand (a saved order to follow, say): nobody may take it meanwhile.
            SetKeeper(entry.Key, NetworkManager.ServerClientId);
        }
    }

    private static bool Held(Horse horse)
    {
        if (horse.Rider != null || horse.Order == Horse.Orders.Follow || horse.Motion.IsComing
            || (horse.Taming != null && horse.Taming.Current != HorseTaming.Phase.None))
        {
            return true;
        }

        return (ActionMenuWindow.Current != null && ActionMenuWindow.Current.IsOpenAt(horse.transform))
               || (CargoWindow.Current != null && CargoWindow.Current.IsShowing(horse))
               || HorseNameWindow.IsNaming(horse);
    }

    private bool Take(Horse horse)
    {
        if (link == null)
        {
            return true;
        }

        Entry entry = EntryFor(horse);
        if (keepers.TryGetValue(entry.Key, out ulong keeper) && keeper != link.LocalId && link.IsConnected(keeper))
        {
            GameMessages.Post($"{NetworkPlayerAvatar.NameOf(keeper)} has {horse.HorseName} in hand.", MessageKind.Warning);
            return false;
        }

        entry.LetGoAt = -1f;
        if (keeper == link.LocalId && keepers.ContainsKey(entry.Key))
        {
            return true;
        }

        if (link.IsHost)
        {
            SetKeeper(entry.Key, NetworkManager.ServerClientId);
        }
        else
        {
            keepers[entry.Key] = link.LocalId;
            using FastBufferWriter writer = CoopLink.Writer();
            writer.WriteValueSafe(entry.Key);
            link.ToHost(ClaimMessage, writer);
        }

        if (horse.IsPuppet)
        {
            BecomeLocal(entry);
        }

        return true;
    }

    // Run here from now on: from where the runner's game last had it, as it last was there.
    private void BecomeLocal(Entry entry)
    {
        Horse horse = entry.Horse;
        if (entry.Poses.TryNewest(out PoseSnapshot newest))
        {
            horse.transform.SetPositionAndRotation(newest.Position, Quaternion.Euler(0f, newest.Yaw, 0f));
        }

        entry.Poses.Clear();
        if (entry.Rope != null)
        {
            entry.Rope.enabled = false;
        }

        horse.SetPuppet(false);
        if (horse.IsAlive && (entry.Flags & PoseFlags.Dead) == 0)
        {
            horse.Health.SetHealthFraction(entry.Health);
            horse.Stamina = entry.Stamina * horse.MaxStamina;
        }

        entry.Flags = PoseFlags.None;
        entry.MarkSent = false;
    }

    private void LetGo(Entry entry)
    {
        entry.LetGoAt = -1f;
        if (link.IsHost)
        {
            SetKeeper(entry.Key, CoopLink.NoOne);
            return;
        }

        // A horse that followed this player stays where they left it.
        entry.Horse.ForgetFollowing();
        keepers.Remove(entry.Key);
        SendLetGo(entry.Key, entry.Horse.CaptureShared(), entry.Horse.Health.HealthFraction, entry.Horse.transform.position, entry.Horse.transform.eulerAngles.y);
        entry.MarkSent = false;
    }

    // Gone from this game: a scene left, a wild horse tamed into a saved one, a raid's horse taken home.
    private void Vanished(Entry entry)
    {
        entries.Remove(entry.Key);
        entry.Poses.Clear();
        if (!entry.RunHere)
        {
            return;
        }

        bool kept = keepers.TryGetValue(entry.Key, out ulong keeper) && keeper == link.LocalId;
        if (link.IsHost && kept)
        {
            SetKeeper(entry.Key, CoopLink.NoOne);
        }

        if (kept && !link.IsHost && !entry.Replaced && entry.InWorld)
        {
            // Still in the Haven for the host: hand it back as it last was here.
            keepers.Remove(entry.Key);
            SendLetGo(entry.Key, entry.State, entry.Health, entry.LastPosition, entry.LastYaw);
            return;
        }

        if (!entry.InWorld)
        {
            if (kept)
            {
                keepers.Remove(entry.Key);
            }

            using FastBufferWriter writer = CoopLink.Writer();
            writer.WriteValueSafe(entry.Key);
            writer.WriteValueSafe(placeHash);
            link.ToOthers(GoneMessage, writer);
        }
    }

    // ---- Drawing a friend's horse

    private void Draw(Entry entry)
    {
        Horse horse = entry.Horse;
        double renderTime = link.Network.ServerTime.Time - settings.InterpolationDelay;
        if (entry.Poses.Sample(renderTime, settings.TeleportDistance, out Vector3 position, out float yaw, out AnimatorFrame animation))
        {
            horse.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            entry.Mirror?.Apply(animation, settings.AnimationBlend);
        }

        if (horse.Motion.Animator != null)
        {
            horse.Motion.Animator.speed = entry.AnimatorSpeed;
        }

        if ((entry.Flags & PoseFlags.Dead) != 0)
        {
            horse.DieAsPuppet();
        }
        else if (horse.IsAlive)
        {
            if (Mathf.Abs(horse.Health.HealthFraction - entry.Health) > 0.01f)
            {
                horse.Health.SetHealthFraction(entry.Health);
            }

            horse.Stamina = entry.Stamina * horse.MaxStamina;
        }

        DrawRope(entry);
    }

    // A friend's lasso, from their hand to the horse's neck (as HorseTaming draws the player's own).
    private void DrawRope(Entry entry)
    {
        RemotePlayerBody body = null;
        if ((entry.Flags & PoseFlags.Lasso) != 0)
        {
            foreach (NetworkPlayerAvatar avatar in NetworkPlayerAvatar.All)
            {
                if (avatar != null && avatar.OwnerClientId == entry.Runner && avatar.IsVisible)
                {
                    body = avatar.Body;
                    break;
                }
            }
        }

        if (body == null)
        {
            if (entry.Rope != null)
            {
                entry.Rope.enabled = false;
            }

            return;
        }

        if (entry.Rope == null)
        {
            var holder = new GameObject("Friend's Lasso Rope");
            holder.transform.SetParent(entry.Horse.transform, false);
            entry.Rope = holder.AddComponent<LineRenderer>();
            entry.Rope.positionCount = 12;
            entry.Rope.widthMultiplier = 0.06f;
            entry.Rope.useWorldSpace = true;
            entry.Rope.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (ropeMaterial == null)
            {
                ropeMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                ropeMaterial.SetColor("_BaseColor", new Color(0.72f, 0.6f, 0.4f));
            }

            entry.Rope.sharedMaterial = ropeMaterial;
        }

        entry.Rope.enabled = true;
        Transform from = body.transform;
        Vector3 hand = from.position + Vector3.up * 1.2f + from.forward * 0.35f;
        Vector3 neck = entry.Horse.Seat.position + entry.Horse.transform.forward * 0.9f + Vector3.up * 0.25f;
        float sag = Mathf.Lerp(0.8f, 0.02f, entry.Strain);
        for (int i = 0; i < entry.Rope.positionCount; i++)
        {
            float t = i / (entry.Rope.positionCount - 1f);
            entry.Rope.SetPosition(i, Vector3.Lerp(hand, neck, t) + Vector3.down * (sag * 4f * t * (1f - t)));
        }
    }

    // ---- Keepers (settled by the host)

    private void SetKeeper(uint key, ulong keeper)
    {
        if (keeper == CoopLink.NoOne)
        {
            keepers.Remove(key);
        }
        else
        {
            keepers[key] = keeper;
        }

        if (!link.IsHost)
        {
            return;
        }

        using FastBufferWriter writer = CoopLink.Writer();
        writer.WriteValueSafe(key);
        writer.WriteValueSafe(keeper);
        link.ToGuests(KeeperMessage, writer);
    }

    private void OnClaim(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out uint key);
        if (keepers.TryGetValue(key, out ulong keeper) && keeper != sender && (keeper == link.LocalId || link.IsConnected(keeper)))
        {
            using FastBufferWriter writer = CoopLink.Writer();
            writer.WriteValueSafe(key);
            writer.WriteValueSafe(keeper);
            link.Send(RefusedMessage, sender, writer);
            return;
        }

        SetKeeper(key, sender);
    }

    private void OnKeeper(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out uint key);
        reader.ReadValueSafe(out ulong keeper);
        if (keeper == CoopLink.NoOne)
        {
            // A let-go this game sent comes back as nobody's; one it has just claimed again stays its own.
            if (!(keepers.TryGetValue(key, out ulong mine) && mine == link.LocalId && entries.TryGetValue(key, out Entry entry)
                  && entry.Horse != null && Held(entry.Horse)))
            {
                keepers.Remove(key);
            }
        }
        else
        {
            keepers[key] = keeper;
        }
    }

    // Too late: a friend had it in hand first. Whatever began here stops, and it is drawn as theirs.
    private void OnRefused(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out uint key);
        reader.ReadValueSafe(out ulong keeper);
        keepers[key] = keeper;
        if (!entries.TryGetValue(key, out Entry entry) || entry.Horse == null)
        {
            return;
        }

        Horse horse = entry.Horse;
        if (horse.Rider != null)
        {
            horse.Rider.Dismount(thrown: false);
        }

        if (horse.Taming != null && horse.Taming.Current != HorseTaming.Phase.None)
        {
            horse.Taming.Abandon();
        }

        if (ActionMenuWindow.Current != null && ActionMenuWindow.Current.IsOpenAt(horse.transform))
        {
            ActionMenuWindow.Current.Close();
        }

        GameMessages.Post($"{NetworkPlayerAvatar.NameOf(keeper)} has {horse.HorseName} in hand.", MessageKind.Warning);
    }

    private void SendLetGo(uint key, string state, float health, Vector3 position, float yaw)
    {
        using FastBufferWriter writer = CoopLink.Writer(1024, 128 * 1024);
        writer.WriteValueSafe(key);
        writer.WriteValueSafe(state ?? "");
        writer.WriteValueSafe(health);
        writer.WriteValueSafe(position);
        writer.WriteValueSafe(yaw);
        link.ToHost(LetGoMessage, writer);
    }

    private void OnLetGo(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out uint key);
        reader.ReadValueSafe(out string state);
        reader.ReadValueSafe(out float health);
        reader.ReadValueSafe(out Vector3 position);
        reader.ReadValueSafe(out float yaw);
        if (!keepers.TryGetValue(key, out ulong keeper) || keeper != sender)
        {
            return;
        }

        if (entries.TryGetValue(key, out Entry entry) && entry.Horse != null)
        {
            // As the friend's game last had it, run here again.
            entry.Poses.Clear();
            entry.Horse.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            if (!string.IsNullOrEmpty(state))
            {
                entry.Horse.RestoreShared(state);
                entry.State = state;
            }

            entry.Health = health;
        }

        SetKeeper(key, CoopLink.NoOne);
    }

    // The host's game had horses of a friend who has gone home with them.
    private void SendHome(string owner)
    {
        scratch.Clear();
        scratch.AddRange(entries.Values);
        foreach (Entry entry in scratch)
        {
            Horse horse = entry.Horse;
            if (horse != null && horse.IsTamed && horse.OwnerId == owner && horse.TryGetComponent(out SaveableEntity saved) && saved.IsPersistent)
            {
                saved.MarkDestroyed();
                Object.Destroy(horse.gameObject);
            }
        }
    }

    private void ReleaseAbsentKeepers()
    {
        scratchKeys.Clear();
        foreach (KeyValuePair<uint, ulong> pair in keepers)
        {
            if (pair.Value == link.LocalId)
            {
                continue;
            }

            // Kept by a friend who is somewhere else now (left the Haven with it still in hand, say): the host runs it again.
            bool here = false;
            foreach (NetworkPlayerAvatar avatar in NetworkPlayerAvatar.All)
            {
                here |= avatar != null && avatar.OwnerClientId == pair.Value && avatar.Look.Place.Equals(CoopPlace.Key);
            }

            if (!link.IsConnected(pair.Value) || (!here && entries.TryGetValue(pair.Key, out Entry entry) && entry.Horse != null))
            {
                scratchKeys.Add(pair.Key);
            }
        }

        foreach (uint key in scratchKeys)
        {
            SetKeeper(key, CoopLink.NoOne);
        }
    }

    // ---- States

    private void SendState(Entry entry)
    {
        // Nobody here to draw it: a friend who comes is sent everything then (NoteFriendsHere).
        if (friendsHere.Count == 0)
        {
            return;
        }

        using FastBufferWriter writer = CoopLink.Writer(1024, 128 * 1024);
        WriteState(writer, entry, link.LocalId);
        link.ToOthers(StateMessage, writer);
    }

    private void WriteState(FastBufferWriter writer, Entry entry, ulong runner)
    {
        writer.WriteValueSafe(entry.Key);
        writer.WriteValueSafe(placeHash);
        writer.WriteValueSafe(runner);
        writer.WriteValueSafe(entry.PrefabId ?? "");
        writer.WriteValueSafe(entry.InWorld);
        writer.WriteValueSafe(entry.LastPosition);
        writer.WriteValueSafe(entry.LastYaw);
        writer.WriteValueSafe(entry.Health);
        writer.WriteValueSafe(entry.State ?? "");
    }

    private void OnState(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out uint key);
        reader.ReadValueSafe(out int place);
        reader.ReadValueSafe(out ulong runner);
        reader.ReadValueSafe(out string prefabId);
        reader.ReadValueSafe(out bool inWorld);
        reader.ReadValueSafe(out Vector3 position);
        reader.ReadValueSafe(out float yaw);
        reader.ReadValueSafe(out float health);
        reader.ReadValueSafe(out string state);
        if (link.IsHost)
        {
            using FastBufferWriter relay = CoopLink.Writer(1024, 128 * 1024);
            relay.WriteValueSafe(key);
            relay.WriteValueSafe(place);
            relay.WriteValueSafe(runner);
            relay.WriteValueSafe(prefabId ?? "");
            relay.WriteValueSafe(inWorld);
            relay.WriteValueSafe(position);
            relay.WriteValueSafe(yaw);
            relay.WriteValueSafe(health);
            relay.WriteValueSafe(state ?? "");
            link.ToGuests(StateMessage, relay, sender);
        }

        if (place != placeHash || scene != SceneManager.GetActiveScene())
        {
            return;
        }

        if (!entries.TryGetValue(key, out Entry entry))
        {
            entry = new Entry { Key = key, PrefabId = prefabId ?? "", InWorld = inWorld };
            entries[key] = entry;
        }

        if (entry.Horse != null)
        {
            // Run here, it is this game's word that counts; a puppet takes on its runner's.
            if (entry.Horse.IsPuppet)
            {
                entry.Runner = runner;
                entry.Health = health;
                if (state != entry.State)
                {
                    entry.Horse.RestoreShared(state);
                    entry.State = state;
                }
            }

            return;
        }

        entry.Runner = runner;
        entry.Health = health;
        entry.State = state;
        // One of the Haven's own comes by the world (HavenSync); a wild horse, or one on a raid, is made here.
        if (!inWorld && !entry.Seen)
        {
            Spawn(entry, position, yaw);
        }
    }

    private void Spawn(Entry entry, Vector3 position, float yaw)
    {
        SaveableEntity prefab = content != null ? content.FindPrefab(entry.PrefabId) : null;
        if (prefab == null)
        {
            return;
        }

        // A plain copy, as a wild horse is: no save id, so it is never kept here.
        GameObject copy = Object.Instantiate(prefab.gameObject, position, Quaternion.Euler(0f, yaw, 0f));
        Horse horse = copy.GetComponent<Horse>();
        if (horse == null)
        {
            Object.Destroy(copy);
            return;
        }

        copy.name = $"{horse.Definition.DisplayName} (a friend's)";
        horse.CoopKey = entry.Key;
        entry.SpawnedHere = true;
        entry.Maker = entry.Runner;
        entry.Horse = horse;
        entry.Seen = true;
        entry.Mirror = horse.Motion.Animator != null ? new AnimatorMirror(horse.Motion.Animator) : null;
        horse.SetPuppet(true);
        horse.RestoreShared(entry.State);
    }

    private void OnGone(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out uint key);
        reader.ReadValueSafe(out int place);
        if (link.IsHost)
        {
            using FastBufferWriter relay = CoopLink.Writer();
            relay.WriteValueSafe(key);
            relay.WriteValueSafe(place);
            link.ToGuests(GoneMessage, relay, sender);
            keepers.Remove(key);
        }

        if (place != placeHash || !entries.TryGetValue(key, out Entry entry))
        {
            return;
        }

        entries.Remove(key);
        entry.Poses.Clear();
        if (entry.Horse != null && !entry.InWorld)
        {
            Object.Destroy(entry.Horse.gameObject);
        }
    }

    // ---- Poses

    private void SendPoses()
    {
        if (outgoing.Count == 0 || friendsHere.Count == 0)
        {
            return;
        }

        double now = link.Network.ServerTime.Time;
        for (int from = 0; from < outgoing.Count; from += PosesPerMessage)
        {
            int count = Mathf.Min(PosesPerMessage, outgoing.Count - from);
            using FastBufferWriter writer = CoopLink.Writer(1024, 8 * 1024);
            writer.WriteValueSafe(placeHash);
            writer.WriteValueSafe(link.LocalId);
            writer.WriteValueSafe((byte)count);
            for (int i = from; i < from + count; i++)
            {
                WritePose(writer, outgoing[i], now);
            }

            link.ToOthers(PosesMessage, writer, NetworkDelivery.UnreliableSequenced);
        }
    }

    private void WritePose(FastBufferWriter writer, Entry entry, double now)
    {
        Horse horse = entry.Horse;
        var pose = new PoseSnapshot { Time = now, Position = horse.transform.position, Yaw = horse.transform.eulerAngles.y };
        entry.Mirror?.Capture(ref scratchFrame);
        pose.Animation = entry.Mirror != null ? scratchFrame : default;
        PoseFlags flags = PoseFlags.None;
        if (horse.Rider != null)
        {
            flags |= horse.Rider.IsMounting ? PoseFlags.Mounting : PoseFlags.Seated;
        }

        if (horse.Taming != null && horse.Taming.IsHeld)
        {
            flags |= PoseFlags.Lasso;
        }

        if (!horse.IsAlive)
        {
            flags |= PoseFlags.Dead;
        }

        writer.WriteValueSafe(entry.Key);
        writer.WriteValueSafe(pose.Time);
        writer.WriteValueSafe(pose.Position);
        writer.WriteValueSafe(pose.Yaw);
        writer.WriteValueSafe((byte)Mathf.RoundToInt(Mathf.Clamp01(horse.Health.HealthFraction) * 255f));
        writer.WriteValueSafe((byte)Mathf.RoundToInt(Mathf.Clamp01(horse.MaxStamina > 0f ? horse.Stamina / horse.MaxStamina : 0f) * 255f));
        writer.WriteValueSafe((byte)flags);
        writer.WriteValueSafe((byte)Mathf.RoundToInt(Mathf.Clamp01(horse.Taming != null ? horse.Taming.Strain : 0f) * 255f));
        writer.WriteValueSafe(Mathf.FloatToHalf(horse.Motion.Animator != null ? horse.Motion.Animator.speed : 1f));
        writer.WriteValueSafe(pose.Animation.LayerCount > 0);
        if (pose.Animation.LayerCount > 0)
        {
            writer.WriteNetworkSerializable(pose.Animation);
        }
    }

    private void OnPoses(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out int place);
        reader.ReadValueSafe(out ulong runner);
        reader.ReadValueSafe(out byte count);
        received.Clear();
        for (int i = 0; i < count; i++)
        {
            var pose = new PoseIn();
            reader.ReadValueSafe(out pose.Key);
            reader.ReadValueSafe(out pose.Pose.Time);
            reader.ReadValueSafe(out pose.Pose.Position);
            reader.ReadValueSafe(out pose.Pose.Yaw);
            reader.ReadValueSafe(out pose.Health);
            reader.ReadValueSafe(out pose.Stamina);
            reader.ReadValueSafe(out byte flags);
            pose.Flags = (PoseFlags)flags;
            reader.ReadValueSafe(out pose.Strain);
            reader.ReadValueSafe(out pose.Speed);
            reader.ReadValueSafe(out bool animated);
            if (animated)
            {
                reader.ReadNetworkSerializable(out pose.Pose.Animation);
            }

            received.Add(pose);
        }

        // The host passes a guest's poses on to the others before keeping its own copy (which takes the frames).
        if (link.IsHost && link.Guests(sender).Count > 0)
        {
            using FastBufferWriter relay = CoopLink.Writer(1024, 8 * 1024);
            relay.WriteValueSafe(place);
            relay.WriteValueSafe(runner);
            relay.WriteValueSafe(count);
            foreach (PoseIn pose in received)
            {
                relay.WriteValueSafe(pose.Key);
                relay.WriteValueSafe(pose.Pose.Time);
                relay.WriteValueSafe(pose.Pose.Position);
                relay.WriteValueSafe(pose.Pose.Yaw);
                relay.WriteValueSafe(pose.Health);
                relay.WriteValueSafe(pose.Stamina);
                relay.WriteValueSafe((byte)pose.Flags);
                relay.WriteValueSafe(pose.Strain);
                relay.WriteValueSafe(pose.Speed);
                relay.WriteValueSafe(pose.Pose.Animation.LayerCount > 0);
                if (pose.Pose.Animation.LayerCount > 0)
                {
                    relay.WriteNetworkSerializable(pose.Pose.Animation);
                }
            }

            link.ToGuests(PosesMessage, relay, sender, NetworkDelivery.UnreliableSequenced);
        }

        foreach (PoseIn pose in received)
        {
            if (place != placeHash || !entries.TryGetValue(pose.Key, out Entry entry) || entry.RunHere)
            {
                FramePool.Return(pose.Pose.Animation);
                continue;
            }

            entry.Runner = runner;
            entry.Health = pose.Health / 255f;
            entry.Stamina = pose.Stamina / 255f;
            entry.Flags = pose.Flags;
            entry.Strain = pose.Strain / 255f;
            entry.AnimatorSpeed = Mathf.HalfToFloat(pose.Speed);
            entry.Poses.Add(pose.Pose);
        }

        received.Clear();
    }
}
