using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Gives the player the modular human body (Safe Haven › Setup › Characters › Use Modular Body for Player): ModularCharacter
/// builds it from the CharacterCatalog, PlayerLook dresses it in the game's look and gear, CharacterEmotes plays its
/// emotes. Removes the old body under the player prefab (the Starter Assets robot's or Kevin Iglesias' HumanM), shows the
/// catalog's starting look in the prefab, and refits the held-item grip to the new hand. Running it again rebuilds the
/// body.
/// </summary>
public static class PlayerBodySetup
{
    // Children of the player root that made up its older bodies.
    private static readonly string[] OldBodyParts = { "Skeleton", "HumanM_BodyMesh", "Rig" };

    [MenuItem("Safe Haven/Setup/Characters/Use Modular Body for Player", false, 200)]
    public static void UseModularBodyMenu() => Debug.Log(UseModularBody());

    public static string UseModularBody()
    {
        if (RetiredBuilders.Refuse("Use Modular Body for Player")) return "retired (see AgentScripts/RETIRED.md)";
        GameObject root = PrefabUtility.LoadPrefabContents(CombatAnimationSetup.PlayerPrefabPath);
        string result;
        try
        {
            var old = OldBodyParts.Select(part => root.transform.Find(part)).Where(part => part != null).ToList();
            // The robot's "Geometry" is only old when no modular body owns it.
            Transform geometry = root.transform.Find("Geometry");
            if (geometry != null && root.GetComponent<ModularCharacter>() == null)
            {
                old.Add(geometry);
            }

            result = ModularBodySetup.Apply(root, CharacterCatalog.Current.StartingLook, null, old);
            if (root.GetComponent<PlayerLook>() == null)
            {
                root.AddComponent<PlayerLook>();
            }

            PrefabUtility.SaveAsPrefabAsset(root, CombatAnimationSetup.PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        string grip = HeldItemGrips.FitPlayerGrip() + " " + HeldItemGrips.FitCompanionGrips();
        return $"Player body: {result} {grip}";
    }
}