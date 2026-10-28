using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Drives the prologue in Play mode (started with Safe Haven › Quests › Play Prologue (Isolated Saves)) to look at the
/// intro's moments: `--entry ProbeIntro.ToBerries` finishes the first chapters and builds the fire;
/// `ProbeIntro.Teleport` with x, z moves the player; `ProbeIntro.Cook` gives berries and stews them;
/// `ProbeIntro.ToTroll` finishes the rest up to the walk to the stones; `ProbeIntro.State` reports.
/// </summary>
public static class ProbeIntro
{
    private const string Prologue = "Assets/_Project/Data/Prologue/";

    public static string ToBerries()
    {
        CloseDialogue();
        Start("FindShelter");
        Raise("ShelterFound");
        Start("GatherFuel");
        PrologueDefinition definition = PrologueDirector.Current.Definition;
        Player.Current.Bag.Give(definition.Sticks, 4);
        Player.Current.Bag.Give(definition.Stones, 6);
        Start("LightFire");
        var camp = (Transform)new SerializedObject(PrologueDirector.Current).FindProperty("campPoint").objectReferenceValue;
        Object.Instantiate(definition.Campfire.Prefab, camp.position + new Vector3(2f, 0f, 2f), Quaternion.identity);
        GameEvents.Raise(new GameEvent(GameEventKind.Built, definition.Campfire, 1, Player.Current.gameObject));
        return State();
    }

    public static string Cook()
    {
        CloseDialogue();
        var berries = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Materials/Berries.asset");
        Player.Current.Bag.Give(berries, 5);
        var camp = (Transform)new SerializedObject(PrologueDirector.Current).FindProperty("campPoint").objectReferenceValue;
        Teleport(camp.position.x + 1f, camp.position.z + 1f);
        var recipe = AssetDatabase.LoadAssetAtPath<RecipeDefinition>("Assets/_Project/Data/Crafting/Recipes/StewedBerries.asset");
        var crafting = Player.Current.GetComponent<PlayerCrafting>();
        string check = crafting.Check(recipe).ToString();
        bool crafted = crafting.TryCraft(recipe);
        return $"check {check} crafted {crafted}; " + State();
    }

    public static string SkipToTroll()
    {
        ToBerries();
        var berries = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Materials/Berries.asset");
        Player.Current.Bag.Give(berries, 5);
        var stew = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/StewedBerries.asset");
        GameEvents.Raise(new GameEvent(GameEventKind.Crafted, stew, 1, Player.Current.gameObject));
        return ToTroll();
    }

    public static string ToTroll()
    {
        CloseDialogue();
        Start("CatchBreath");
        Raise("Rested");
        return State();
    }

    public static string KillWolves()
    {
        foreach (Damageable enemy in PrologueDirector.Current.Enemies.ToArray())
            if (enemy != null) enemy.ApplyDamage(new DamageInfo(null, null, 9999f, DamageType.Physical, false, enemy.transform.position, Vector3.forward));
        return State();
    }

    public static string Teleport(float x, float z)
    {
        Transform player = Player.Current.transform;
        var body = player.GetComponent<CharacterController>();
        body.enabled = false;
        Vector3 at = new Vector3(x, 0f, z);
        at.y = Terrain.activeTerrain.SampleHeight(at) + Terrain.activeTerrain.transform.position.y + 0.1f;
        player.position = at;
        body.enabled = true;
        return $"player at {at:F0}";
    }

    public static string State()
    {
        string quests = string.Join(", ", PrologueDirector.Current.Definition.Chapters
            .Select(c => $"{c.Quest.name}:{GameSession.Quests.StatusOf(c.Quest)}"));
        var e = Player.Current.Equipment;
        return $"step {PrologueDirector.Current.Step}; {quests}; main {(e.MainHand.IsEmpty ? "-" : e.MainHand.Item.name)} dialogue {DialogueBox.IsOpen}";
    }

    private static void CloseDialogue()
    {
        if (DialogueBox.IsOpen) DialogueBox.Current.Close();
    }

    private static void Start(string quest)
    {
        var definition = AssetDatabase.LoadAssetAtPath<QuestDefinition>(Prologue + "Quests/" + quest + ".asset");
        if (GameSession.Quests.StatusOf(definition) == QuestStatus.NotStarted && !QuestManager.Instance.TryStartQuest(definition, out string reason))
            Debug.LogWarning($"Probe couldn't start {quest}: {reason}");
    }

    private static void Raise(string gameEvent) =>
        GameEvents.Raise(AssetDatabase.LoadAssetAtPath<GameEventDefinition>(Prologue + "Events/" + gameEvent + ".asset"), Player.Current.gameObject);
}
