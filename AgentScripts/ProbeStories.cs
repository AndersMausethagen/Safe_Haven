using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Plays the StoryDemo stories (`unity command run_script --file AgentScripts/ProbeStories.cs --entry ProbeStories.<Step>`):
/// Rain walks the player to the joke spot; Stone uses the waking stone; Next presses "continue" in the dialogue box;
/// KillWolves kills what the stone spawned; Report shows where everything stands.
/// </summary>
public static class ProbeStories
{
    public static async Task<string> Rain()
    {
        Player player = Object.FindAnyObjectByType<Player>();
        player.GetComponent<Damageable>().AddInvulnerability(typeof(ProbeStories));
        Move(player, GameObject.Find("Where the joke is made").transform.position + Vector3.back);
        await Task.Delay(800);
        return Report();
    }

    public static async Task<string> Stone()
    {
        Player player = Object.FindAnyObjectByType<Player>();
        player.GetComponent<Damageable>().AddInvulnerability(typeof(ProbeStories));
        GameObject stone = GameObject.Find("The Waking Stone");
        Move(player, stone.transform.position + Vector3.left * 2.5f);
        await Task.Delay(300);
        var use = stone.GetComponent<Interactable>();
        bool began = use.TryBegin(player.gameObject);
        use.Completed.Invoke();
        await Task.Delay(3500);
        return $"began={began}; " + Report();
    }

    public static async Task<string> Next()
    {
        typeof(DialogueBox).GetMethod("Continue", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(DialogueBox.Current, null);
        await Task.Delay(1500);
        return Report();
    }

    public static async Task<string> Wait(float seconds)
    {
        await Task.Delay(Mathf.RoundToInt(seconds * 1000f));
        return Report();
    }

    public static async Task<string> KillWolves()
    {
        var zone = GameObject.Find("Out of the Stone").GetComponent<MobSpawnZone>();
        var alive = (System.Collections.Generic.List<GameObject>)typeof(MobSpawnZone).GetField("alive", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(zone);
        foreach (GameObject wolf in alive.ToList())
        {
            if (wolf != null && wolf.TryGetComponent(out Damageable health))
            {
                health.ApplyDamage(new DamageInfo(null, null, 9999f, DamageType.Physical, false, wolf.transform.position, Vector3.forward));
            }
        }

        await Task.Delay(4000);
        return Report();
    }

    public static string Report()
    {
        string line = DialogueBox.IsOpen
            ? ((TMPro.TMP_Text)typeof(DialogueBox).GetField("lineLabel", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(DialogueBox.Current)).text
            : "-";
        WeatherSystem weather = Object.FindAnyObjectByType<WeatherSystem>();
        PlayerCameraController camera = Object.FindAnyObjectByType<PlayerCameraController>();
        var focus = (Transform)typeof(PlayerCameraController).GetField("focusTarget", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(camera);
        GameObject stone = GameObject.Find("The Waking Stone");
        Light glow = stone.GetComponentInChildren<Light>();
        WitherSpot wither = stone.GetComponent<WitherSpot>();
        var zone = GameObject.Find("Out of the Stone").GetComponent<MobSpawnZone>();
        QuestDefinition quest = Resources.FindObjectsOfTypeAll<QuestDefinition>().FirstOrDefault(q => q.name == "TheWakingStone");
        string questState = QuestManager.Current != null && quest != null ? QuestManager.Current.Log.StatusOf(quest).ToString() : "?";
        return $"dialogue: \"{line}\" | weather: {(weather != null ? "rain " + weather.Current.Rain.ToString("F2") : "none")} | camera focus: {(focus != null ? focus.name : "-")} | " +
               $"stone glow {(glow != null ? glow.intensity.ToString("F1") : "-")}, wither {(wither != null ? wither.Amount.ToString("F2") : "-")}, usable {stone.GetComponent<Interactable>().enabled} | " +
               $"wolves alive {zone.AliveCount} | quest {questState}";
    }

    private static void Move(Player player, Vector3 to)
    {
        var controller = player.GetComponent<CharacterController>();
        controller.enabled = false;
        player.transform.position = to;
        controller.enabled = true;
    }
}
