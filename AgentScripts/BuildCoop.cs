#error Retired 2026-11-14: this script rebuilt content from its own stored copy and could silently overwrite newer work (AgentScripts/RETIRED.md). The scenes and assets are the source of truth: make a small targeted edit with a before/after diff, and restore earlier states from git.
using System.IO;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Co-op (`unity command script run --file AgentScripts/BuildCoop.cs --entry BuildCoop.Apply`): the Network Player prefab
/// (Prefabs/Multiplayer: a NetworkObject with NetworkPlayerAvatar; it sends its own poses, so Netcode's transform sync is
/// off) and Resources/MultiplayerSettings pointing at it, with the friend's body animated by the player's own Animator
/// Controller and holding items with the player's grip. Re-runnable: keeps the prefab (and its network id) and settings.
/// </summary>
public static class BuildCoop
{
    private const string Folder = "Assets/_Project/Prefabs/Multiplayer";
    private const string PrefabPath = Folder + "/Network Player.prefab";
    private const string SettingsPath = "Assets/_Project/Resources/MultiplayerSettings.asset";
    private const string PlayerPath = "Assets/_Project/Prefabs/Characters/PlayerArmature.prefab";

    public static string Apply()
    {
        Directory.CreateDirectory(Folder);
        NetworkObject prefab = Prefab();
        var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
        if (player == null)
        {
            return "no PlayerArmature prefab";
        }

        Animator animator = player.GetComponent<Animator>();
        HeldItemView held = player.GetComponent<HeldItemView>();
        var settings = AssetDatabase.LoadAssetAtPath<MultiplayerSettings>(SettingsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<MultiplayerSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }

        settings.EditorSet(prefab, animator != null ? animator.runtimeAnimatorController : null,
            held != null ? held.GripPose : new Pose(Vector3.zero, Quaternion.identity));
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        return $"Network Player prefab (id {prefab.PrefabIdHash}); settings: {settings.MaxPlayers} players, body {settings.BodyAnimations?.name}, grip {settings.GripPose.position}";
    }

    private static NetworkObject Prefab()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        GameObject root = existing != null ? PrefabUtility.LoadPrefabContents(PrefabPath) : new GameObject("Network Player");
        try
        {
            NetworkObject network = root.GetComponent<NetworkObject>();
            if (network == null)
            {
                network = root.AddComponent<NetworkObject>();
            }

            // Poses go in NetworkPlayerAvatar's own messages; it lives across scene changes and goes with its owner.
            network.SynchronizeTransform = false;
            network.AutoObjectParentSync = false;
            network.ActiveSceneSynchronization = false;
            network.SceneMigrationSynchronization = false;
            network.DontDestroyWithOwner = false;
            if (root.GetComponent<NetworkPlayerAvatar>() == null)
            {
                root.AddComponent<NetworkPlayerAvatar>();
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            if (existing != null)
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            else
            {
                Object.DestroyImmediate(root);
            }
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<NetworkObject>();
    }
}
