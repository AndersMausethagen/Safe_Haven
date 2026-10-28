using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Drives fishing in Play mode in Safe Haven (start with ProbeArchery.PlayIn): `Setup` gives and equips the rod and
/// bait and stands the player on the tarn's bank; `Cast` aims at the tarn and casts; `Bite` makes a fish bite now;
/// `Strike` strikes; `Land` lands it; `Nokken` starts the Nøkken; `Break` hammers free; `State` reports.
/// </summary>
public static class ProbeFishing
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private static PlayerFishing Fishing => PlayerFishing.Current;

    public static string Setup()
    {
        var rod = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/FishingRod.asset");
        var grubs = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Materials/Grubs.asset");
        Player.Current.Bag.Give(rod, 1);
        Player.Current.Bag.Give(grubs, 6);
        Player.Current.Equipment.EquipFromBag(rod);
        Transform tarn = GameObject.Find("Tarn").transform;
        Transform player = Player.Current.transform;
        var body = player.GetComponent<CharacterController>();
        body.enabled = false;
        var at = new Vector3(tarn.position.x + 3f, 0f, tarn.position.z - 9.5f);
        at.y = Terrain.activeTerrain.SampleHeight(at) + Terrain.activeTerrain.transform.position.y + 0.1f;
        player.SetPositionAndRotation(at, Quaternion.LookRotation(Vector3.forward));
        body.enabled = true;
        return State();
    }

    public static async Task<string> Cast()
    {
        Transform tarn = GameObject.Find("Tarn").transform;
        Vector3 screen = Camera.main.WorldToScreenPoint(tarn.position + Vector3.right * 3f);
        Mouse.current?.WarpCursorPosition(new Vector2(screen.x, screen.y));
        Invoke("OnAttackPressed");
        await Task.Delay(900);
        Invoke("OnAttackReleased");
        return State();
    }

    public static string Bite()
    {
        typeof(PlayerFishing).GetField("biteAt", Private).SetValue(Fishing, Time.time);
        return State();
    }

    public static string Strike()
    {
        Invoke("OnAttackPressed");
        return State();
    }

    public static string Land()
    {
        typeof(PlayerFishing).GetMethod("Land", Private).Invoke(Fishing, null);
        return State();
    }

    public static string Nokken()
    {
        Transform tarn = GameObject.Find("Tarn").transform;
        Vector3 landing = tarn.position;
        float height = tarn.position.y;
        Fishing.EndLine(null);
        NokkenEncounter.Begin(Fishing, landing, height, null);
        return State();
    }

    public static string Break()
    {
        typeof(NokkenEncounter).GetField("presses", Private).SetValue(NokkenEncounter.Current, 100f);
        return State();
    }

    public static string State()
    {
        PlayerFishing fishing = Fishing;
        string reel = fishing.Reel != null ? $" reel fish {fishing.Reel.Fish:0.00} zone {fishing.Reel.Zone:0.00} catch {fishing.Reel.Catch:0.00}" : "";
        string bag = $" grubs {Player.Current.Bag.Container.Count(AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Materials/Grubs.asset"))}";
        return $"{fishing.State}{reel}{bag} hooked {fishing.Hooked?.item?.DisplayName ?? "-"} nokken {(NokkenEncounter.Current != null)} health {Player.Current.Health.CurrentHealth:0}";
    }

    private static void Invoke(string method) => typeof(PlayerFishing).GetMethod(method, Private).Invoke(Fishing, null);
}
