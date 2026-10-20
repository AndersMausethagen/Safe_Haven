using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

/// <summary>
/// The Play Test panel of the Content Editor, shown while the game runs: try the selected content straight away instead
/// of finding it in the world. Items and resources can be given to the player, recipes taught (with their ingredients),
/// anything with a prefab spawned in front of the player, raid modifier items handed out, and raids entered directly.
/// It only calls the game's own systems, so what happens is what a player would get.
/// </summary>
public static class PlaytestTools
{
    private const float SpawnDistance = 3f;
    private static int amount = 1;

    public static void Draw(IContentEditorHost host, ContentSection section, Object asset)
    {
        if (!EditorApplication.isPlaying)
        {
            return;
        }

        ContentUtility.BeginPanel("Play Test", "The game is running: try this out right now.");
        Player player = Player.Current;
        if (player == null)
        {
            EditorGUILayout.LabelField("There is no player in the running scene (the start menu, perhaps).", ContentUtility.WrappedMiniLabel);
            ContentUtility.EndPanel();
            return;
        }

        bool any = false;
        switch (asset)
        {
            case ItemDefinition item:
                any = true;
                DrawGive(host, item.DisplayName, count => player.Bag.Give(item, count), item.MaximumStack);
                break;
            case ResourceDefinition resource:
                any = true;
                DrawGive(host, resource.DisplayName, count => player.Resources.Storage.Add(resource, count), 10);
                break;
            case RecipeDefinition recipe:
                any = true;
                DrawRecipe(host, player, recipe);
                break;
            case RaidModifierDefinition modifier:
                any = true;
                DrawModifier(host, player, modifier);
                break;
            case RaidDefinition raid:
                any = true;
                if (GUILayout.Button($"Enter {raid.DisplayName} Now (no modifiers)"))
                {
                    host.Notify(RaidLauncher.TryLaunch(raid, player.gameObject, null, out string problem) ? $"Entering {raid.DisplayName}..." : problem);
                }

                break;
        }

        GameObject prefab = asset as GameObject ?? section.LinkedPrefab(asset);
        if (prefab != null)
        {
            any = true;
            if (GUILayout.Button(new GUIContent($"Spawn {prefab.name} in Front of the Player", "Placed on the ground (or the NavMesh, for creatures) and selected.")))
            {
                Spawn(host, prefab, player.transform);
            }
        }

        if (!any)
        {
            EditorGUILayout.LabelField("Nothing to try for this kind of content.", ContentUtility.WrappedMiniLabel);
        }

        ContentUtility.EndPanel();
    }

    private static void DrawGive(IContentEditorHost host, string name, System.Func<int, int> give, int suggested)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            amount = Mathf.Max(1, EditorGUILayout.IntField("Amount", amount));
            if (GUILayout.Button("1", EditorStyles.miniButtonLeft, GUILayout.Width(28f)))
            {
                amount = 1;
            }

            if (GUILayout.Button(suggested.ToString(), EditorStyles.miniButtonRight, GUILayout.Width(36f)))
            {
                amount = suggested;
            }
        }

        if (GUILayout.Button($"Give {amount} {name} to the Player"))
        {
            int given = give(amount);
            host.Notify(given == amount ? $"Gave {given} {name}." : $"Gave {given} {name}; {amount - given} didn't fit.");
        }
    }

    private static void DrawRecipe(IContentEditorHost host, Player player, RecipeDefinition recipe)
    {
        var crafting = player.Crafting;
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(crafting == null || crafting.IsKnown(recipe)))
            {
                if (GUILayout.Button(crafting != null && crafting.IsKnown(recipe) ? "Already Known" : "Teach to the Player"))
                {
                    crafting.Unlock(new[] { recipe });
                    host.Notify($"Learned {recipe.DisplayName}.");
                }
            }

            if (GUILayout.Button(new GUIContent("Give Ingredients", "Everything the recipe costs, so it can be crafted at its station.")))
            {
                var bag = player.Bag;
                ResourceStorage storage = player.Resources.Storage;
                int missing = 0;
                foreach (ResourceAmount cost in recipe.ResourceCost)
                {
                    missing += cost.Resource != null ? cost.Amount - storage.Add(cost.Resource, cost.Amount) : 0;
                }

                foreach (ItemAmount cost in recipe.ItemCost)
                {
                    missing += cost.Item != null ? cost.Amount - bag.Give(cost.Item, cost.Amount) : 0;
                }

                host.Notify(missing == 0 ? $"Ingredients for {recipe.DisplayName} given." : $"Ingredients given; {missing} didn't fit.");
            }
        }

        if (!recipe.IsHandcrafted)
        {
            EditorGUILayout.LabelField($"Queued at {recipe.Station.WithArticle()} ({recipe.CraftSeconds:0.#} s per craft): use the station in play.", ContentUtility.WrappedMiniLabel);
        }
    }

    private static void DrawModifier(IContentEditorHost host, Player player, RaidModifierDefinition modifier)
    {
        List<ItemDefinition> items = ResourceCatalogTools.LoadAll<ItemDefinition>().FindAll(item => item.RaidModifier == modifier);
        if (items.Count == 0)
        {
            EditorGUILayout.LabelField("No item uses this modifier yet, so there is nothing to give.", ContentUtility.WrappedMiniLabel);
            return;
        }

        foreach (ItemDefinition item in items)
        {
            if (GUILayout.Button($"Give 1 {item.DisplayName} (take it to a raid portal)"))
            {
                host.Notify(player.Bag.Give(item, 1) > 0 ? $"Gave {item.DisplayName}." : "The bag is full.");
            }
        }
    }

    private static void Spawn(IContentEditorHost host, GameObject prefab, Transform player)
    {
        Vector3 position = player.position + player.forward * SpawnDistance;
        if (Physics.Raycast(position + Vector3.up * 10f, Vector3.down, out RaycastHit ground, 30f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            position = ground.point;
        }

        if (prefab.GetComponentInChildren<NavMeshAgent>() != null && NavMesh.SamplePosition(position, out NavMeshHit onMesh, 5f, NavMesh.AllAreas))
        {
            position = onMesh.position;
        }

        GameObject spawned = Object.Instantiate(prefab, position, Quaternion.LookRotation(-player.forward, Vector3.up));
        spawned.name = prefab.name;
        Selection.activeObject = spawned;
        host.Notify($"Spawned {prefab.name}.");
    }
}
