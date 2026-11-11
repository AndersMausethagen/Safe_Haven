using System.Collections;
using System.Text;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Co-op over a real connection: Play in the open Haven scene, host, and a second NetworkManager in the same Editor as the
/// friend over 127.0.0.1 (Netcode allows several in one process). The friend's messages reach the host's game and its
/// answer comes back with the settlement: a Pact Rune at the host's stone, a breach source struck (once a run), one of the
/// host's lost led home. Both games share this Editor's statics, so only what goes over the wire is checked here; the
/// guest's own side is in CoopTests, CoopHomeTests and TerritoryTests. Takes about a minute (loading the Haven). Saves
/// nothing: an Editor session with no journey loaded never autosaves.
/// </summary>
public sealed class CoopLiveTests
{
    private const string Region = "Ravenwood";

    [UnityTest, Timeout(240000)]
    public IEnumerator AFriendsHelp_ReachesTheHost_AndComesBack()
    {
        // The test runner plays in a scene of its own: the Haven is opened (saved, so nothing is lost).
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "SafeHaven")
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Project/Scenes/SafeHaven.unity");
        }

        LogAssert.ignoreFailingMessages = true;
        yield return new EnterPlayMode();
        float waited = 0f;
        while ((Player.Current == null || CoopSession.Current == null) && waited < 90f)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        Assert.IsNotNull(Player.Current, "the Haven never had a player");
        CoopSession coop = CoopSession.Current;
        GameObject holder = null;
        NetworkManager guest = null;
        try
        {
            TerritorySettings settings = TerritorySettings.Current;
            TerritoryState lands = GameSession.Settlement.Territory;
            lands.Claim(Region, settings);
            float hold = lands.HoldOf(Region);
            var tor = AssetDatabase.LoadAssetAtPath<ResidentDefinition>("Assets/_Project/Data/Prologue/Residents/Tor.asset");
            GameSession.Settlement.AddResident(tor);
            GameSession.Settlement.LoseResident(tor, "the Misty Woods");
            float weakening = GameSession.Settlement.BreachWeakening;

            Assert.IsTrue(coop.Host(out string problem), "host: " + problem);
            for (int i = 0; i < 10; i++) yield return null;

            MultiplayerSettings multiplayer = MultiplayerSettings.Current;
            holder = new GameObject("Live Test Guest");
            holder.SetActive(false);
            var transport = holder.AddComponent<UnityTransport>();
            guest = holder.AddComponent<NetworkManager>();
            guest.NetworkConfig = new NetworkConfig { NetworkTransport = transport, PlayerPrefab = multiplayer.PlayerPrefab.gameObject, EnableSceneManagement = false, ConnectionApproval = true };
            guest.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = multiplayer.PlayerPrefab.gameObject });
            guest.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(multiplayer.ProtocolVersion);
            holder.SetActive(true);
            transport.SetConnectionData("127.0.0.1", multiplayer.Port);
            Assert.IsTrue(guest.StartClient());
            waited = 0f;
            while (!guest.IsConnectedClient && waited < 15f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.IsTrue(guest.IsConnectedClient, "the friend never connected");
            float seenHold = -1f;
            guest.CustomMessagingManager.RegisterNamedMessageHandler("SafeHaven.Settlement", (sender, reader) =>
            {
                reader.ReadValueSafe(out byte[] packet);
                foreach (RegionHoldSaveData held in WorldTransfer.Unpack<SettlementSaveData>(packet).territory.holds)
                {
                    if (held.region == Region) seenHold = held.hold;
                }
            });

            void Send(string message, System.Action<FastBufferWriter> write)
            {
                using var writer = new FastBufferWriter(128, Allocator.Temp);
                write(writer);
                guest.CustomMessagingManager.SendNamedMessage(message, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableFragmentedSequenced);
            }

            // A Pact Rune at the host's stone; the host's answer comes back with the settlement.
            Send("SafeHaven.LandWork", w => CoopLands.Write(w, LandWork.Rune, Region, 0));
            // The same breach source struck twice on one run, and one of the host's lost led home.
            Send("SafeHaven.HavenHelp", w => CoopLands.WriteHelp(w, HavenHelpKind.WeakenBreach, "", 0.3f, 0.6f, 4242));
            Send("SafeHaven.HavenHelp", w => CoopLands.WriteHelp(w, HavenHelpKind.WeakenBreach, "", 0.3f, 0.6f, 4242));
            Send("SafeHaven.HavenHelp", w => CoopLands.WriteHelp(w, HavenHelpKind.FoundAgain, tor.Id, 0f, 0f, 0));
            waited = 0f;
            while (!Mathf.Approximately(seenHold, hold + settings.RuneHold) && waited < 8f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.AreEqual(hold + settings.RuneHold, lands.HoldOf(Region), 0.01f, "the friend's rune holds the host's land");
            Assert.AreEqual(hold + settings.RuneHold, seenHold, 0.01f, "and the friend's game hears so with the settlement");
            Assert.AreEqual(Mathf.Min(0.6f, weakening + 0.3f), GameSession.Settlement.BreachWeakening, 0.001f, "one blow a run, however many strike it");
            Assert.IsNotNull(GameSession.Settlement.Find(tor), "the host's lost one is home");
        }
        finally
        {
            if (guest != null) guest.Shutdown();
            if (holder != null) Object.Destroy(holder);
            if (coop != null) coop.Leave();
        }

        yield return null;
        yield return new ExitPlayMode();
        LogAssert.ignoreFailingMessages = false;
    }
}
