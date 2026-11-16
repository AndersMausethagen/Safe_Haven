using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Pings, for playing together without voice: the Ping key (Y, or the mouse's forward button) marks what is under the
/// crosshair, an enemy or a spot, for everyone in the same place (<c>SafeHaven.Ping</c>, passed on by the host). A ping shows
/// as a label in the world ("Astrid: Enemy · 34 m") and on the minimap (<see cref="Active"/>) for a few seconds.
/// Plain C#: <see cref="CoopSession"/> ticks it and owns the connection.
/// </summary>
public sealed class CoopPings : ICoopPart
{
    private const string PingMessage = "SafeHaven.Ping";
    private const float Lasts = 8f;
    private const float Reach = 250f;
    private const float Spacing = 0.5f;

    /// <summary>A mark someone made.</summary>
    public sealed class Ping
    {
        public Vector3 Position;
        public string Who;
        /// <summary>Whose it is, by client id: one ping each, whatever the names (two players may share one).</summary>
        public ulong WhoId;
        public bool Enemy;
        public float Until;
        public PingMarker Marker;
    }

    private static readonly List<Ping> active = new List<Ping>();
    private static readonly RaycastHit[] hits = new RaycastHit[16];
    private CoopLink link;

    private NetworkManager network => link?.Network;
    private float nextPing;

    /// <summary>Pings still showing, for the minimap.</summary>
    public static IReadOnlyList<Ping> Active => active;

    public void Begin(CoopLink session)
    {
        link = session;
        Clear();
        link.On(PingMessage, OnPing);
    }

    public void FriendLeft(ulong clientId)
    {
    }

    public void End()
    {
        Clear();
        link = null;
    }

    public void Clear()
    {
        foreach (Ping ping in active)
        {
            if (ping.Marker != null)
            {
                Object.Destroy(ping.Marker.gameObject);
            }
        }

        active.Clear();
    }

    /// <summary>Every frame in a session: a press marks a spot; old pings fade.</summary>
    public void Tick()
    {
        for (int i = active.Count - 1; i >= 0; i--)
        {
            if (Time.unscaledTime >= active[i].Until)
            {
                if (active[i].Marker != null)
                {
                    Object.Destroy(active[i].Marker.gameObject);
                }

                active.RemoveAt(i);
            }
        }

        InputAction ping = GameInput.Ping;
        if (network == null || ping == null || !GameInput.WasPressed(ping) || Time.unscaledTime < nextPing || Player.Current == null
            || GamePause.IsPaused || !TryAim(out Vector3 at, out bool enemy))
        {
            return;
        }

        nextPing = Time.unscaledTime + Spacing;
        string me = GameSession.Appearance != null ? GameSession.Appearance.DisplayName : CharacterAppearance.DefaultName;
        Show(at, me, link.LocalId, enemy);
        Send(at, me, link.LocalId, enemy, link.IsHost ? link.Guests() : link.Only(NetworkManager.ServerClientId));
    }

    // What lies under the crosshair (the middle of the screen), past the player's own body.
    private static bool TryAim(out Vector3 at, out bool enemy)
    {
        at = default;
        enemy = false;
        Camera view = Camera.main;
        if (view == null)
        {
            return false;
        }

        Ray ray = view.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        int count = Physics.RaycastNonAlloc(ray, hits, Reach, ~0, QueryTriggerInteraction.Ignore);
        float nearest = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            if (hits[i].distance < nearest && !Player.IsPlayer(hits[i].collider.gameObject))
            {
                nearest = hits[i].distance;
                at = hits[i].point;
                enemy = hits[i].collider.GetComponentInParent<MonsterAI>() != null;
            }
        }

        return nearest < float.MaxValue;
    }

    private void OnPing(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out FixedString128Bytes place);
        reader.ReadValueSafe(out Vector3 at);
        reader.ReadValueSafe(out bool enemy);
        reader.ReadValueSafe(out string who);
        reader.ReadValueSafe(out ulong whoId);
        // The host passes a guest's ping on to the others.
        if (network.IsServer)
        {
            Send(at, who, whoId, enemy, link.Guests(sender), place);
        }

        if (place.Equals(CoopPlace.Key))
        {
            Show(at, who, whoId, enemy);
        }
    }

    private void Send(Vector3 at, string who, ulong whoId, bool enemy, IReadOnlyList<ulong> to, FixedString128Bytes? place = null)
    {
        if (to.Count == 0)
        {
            return;
        }

        using var writer = new FastBufferWriter(512, Allocator.Temp);
        writer.WriteValueSafe(place ?? CoopPlace.Key);
        writer.WriteValueSafe(at);
        writer.WriteValueSafe(enemy);
        writer.WriteValueSafe(who);
        writer.WriteValueSafe(whoId);
        network.CustomMessagingManager.SendNamedMessage(PingMessage, to, writer, NetworkDelivery.ReliableFragmentedSequenced);
    }

    private static void Show(Vector3 at, string who, ulong whoId, bool enemy)
    {
        // One ping each: a new one from the same player replaces theirs.
        for (int i = active.Count - 1; i >= 0; i--)
        {
            if (active[i].WhoId == whoId)
            {
                if (active[i].Marker != null)
                {
                    Object.Destroy(active[i].Marker.gameObject);
                }

                active.RemoveAt(i);
            }
        }

        var ping = new Ping { Position = at, Who = who, WhoId = whoId, Enemy = enemy, Until = Time.unscaledTime + Lasts };
        ping.Marker = PingMarker.Create(ping);
        active.Add(ping);
        GameMessages.Post(enemy ? $"{who}: enemy there!" : $"{who}: over here.");
    }

    // Entering Play doesn't reload scripts in this project: statics carry over from the last session unless cleared.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        active.Clear();
    }
}

/// <summary>A ping in the world: a label over the spot, turned to the camera and kept readable at any distance.</summary>
public sealed class PingMarker : MonoBehaviour
{
    private const float Height = 1.6f;
    private CoopPings.Ping ping;
    private TextMeshPro label;
    private int shownMetres = -1;

    public static PingMarker Create(CoopPings.Ping ping)
    {
        var holder = new GameObject("Ping");
        holder.transform.position = ping.Position + Vector3.up * Height;
        holder.AddComponent<FaceCamera>();
        var marker = holder.AddComponent<PingMarker>();
        marker.ping = ping;
        marker.label = holder.AddComponent<TextMeshPro>();
        marker.label.alignment = TextAlignmentOptions.Center;
        marker.label.fontSize = 3f;
        marker.label.textWrappingMode = TextWrappingModes.NoWrap;
        marker.label.rectTransform.sizeDelta = new Vector2(8f, 1f);
        UIStyle style = UIStyle.Current;
        style.OnWorld(marker.label);
        marker.label.color = ping.Enemy ? style.Danger : style.Accent;
        return marker;
    }

    private void LateUpdate()
    {
        Camera view = Camera.main;
        float distance = view != null ? Vector3.Distance(view.transform.position, transform.position) : 10f;
        // The same size on screen near or far.
        transform.localScale = Vector3.one * Mathf.Clamp(distance * 0.06f, 0.4f, 12f);
        float left = ping.Until - Time.unscaledTime;
        label.alpha = Mathf.Clamp01(left);
        int metres = Mathf.RoundToInt(distance);
        if (metres != shownMetres)
        {
            shownMetres = metres;
            label.text = $"{ping.Who}{(ping.Enemy ? ": Enemy" : string.Empty)} · {metres} m";
        }
    }
}
