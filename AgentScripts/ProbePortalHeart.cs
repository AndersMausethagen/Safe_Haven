using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>
/// Play-mode checks for the portal at the centre of Safe Haven (`unity command script run -- --file AgentScripts/ProbePortalHeart.cs --entry ProbePortalHeart.Asleep`).
/// Never saves the game.
/// </summary>
public static class ProbePortalHeart
{
    private static string Menu(HomePortal home, GameObject user)
    {
        var page = (MenuPage)typeof(HomePortal).GetMethod("BuildPage", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(home, new object[] { user });
        var log = new StringBuilder();
        log.AppendLine($"  [{page.Title}] {page.Body.Replace("\n", " / ")}");
        foreach (MenuAction action in page.Actions)
        {
            log.AppendLine($"    {action.Label} [{action.Detail?.Replace("\n", " / ")}] enabled={action.Enabled}");
        }

        return log.ToString();
    }

    private static string Press()
    {
        Player player = Player.Current;
        var interactor = player.GetComponent<PlayerInteractor>();
        MethodInfo press = typeof(PlayerInteractor).GetMethod("OnInteractionRequested", BindingFlags.NonPublic | BindingFlags.Instance);
        press?.Invoke(interactor, null);
        return press == null ? "no press method" : "pressed";
    }

    public static string Asleep()
    {
        var log = new StringBuilder();
        HomePortal home = HomePortal.Current;
        RaidPortal raid = home.GetComponent<RaidPortal>();
        GameObject user = Player.Current.gameObject;
        log.AppendLine($"at {home.transform.position}, same object as raid portal: {raid.gameObject == home.gameObject}, repaired {raid.IsRepaired}, name {raid.InteractionName}");
        log.AppendLine($"active look {home.transform.Find("Active Look").gameObject.activeSelf}, dormant look {home.transform.Find("Dormant Look").gameObject.activeSelf}, dormant model {home.transform.Find("Dormant Look").GetComponentsInChildren<Renderer>().Length}");
        log.AppendLine($"ward {home.WardRadius:0.0} m, contains origin {home.Contains(Vector3.zero)}, wake point {(PlayerArrivalPoint.Find(PlayerArrivalPoint.HomePortal) != null ? PlayerArrivalPoint.Find(PlayerArrivalPoint.HomePortal).transform.position.ToString() : "MISSING")}, portal point {(PlayerArrivalPoint.Find("Portal") != null ? PlayerArrivalPoint.Find("Portal").transform.position.ToString() : "MISSING")}");
        log.AppendLine($"glow {home.transform.Find("Portal Glow") != null}, ring {home.transform.Find("Ward Ring") != null}, revival {home.GetComponent<HomeRevival>() != null}, light {home.GetComponent<VisibilitySource>() != null}, tree gone {GameObject.Find("Heart Tree") == null}");
        log.AppendLine($"bloom watches: {GameObject.Find("Portal Bloom")?.GetComponent<GameEventTrigger>() != null}; build area: {BuildArea.Current == (IBuildArea)home}");
        log.Append(Menu(home, user));
        return log.ToString();
    }

    /// <summary>A real press of E beside the portal: one menu, and no station window on top of it.</summary>
    public static string PressIt()
    {
        var log = new StringBuilder();
        Player.Current.transform.position = HomePortal.Current.transform.position + new Vector3(0f, 0f, -2f);
        Physics.SyncTransforms();
        log.AppendLine(Press());
        return log.ToString();
    }

    public static string AfterPress()
    {
        StationWindow window = UIStateManager.Instance != null ? UIStateManager.Instance.Find<StationWindow>(UIWindowId.Station) : null;
        return $"action menu open {ActionMenuWindow.Current != null && ActionMenuWindow.Current.IsOpen}; station window open {window != null && UIStateManager.Instance.IsOpen(UIWindowId.Station)}; dialogue {Object.FindAnyObjectByType<DialogueBox>()?.isActiveAndEnabled}";
    }

    public static string Woken()
    {
        var log = new StringBuilder();
        HomePortal home = HomePortal.Current;
        GameSession.Settlement.RepairPortal();
        log.AppendLine($"active look {home.transform.Find("Active Look").gameObject.activeSelf}, dormant look {home.transform.Find("Dormant Look").gameObject.activeSelf}");
        log.Append(Menu(home, Player.Current.gameObject));
        return log.ToString();
    }

    public static string SparksOpen()
    {
        CraftingStation station = HomePortal.Current.GetComponent<CraftingStation>();
        station.OpenMenu(Player.Current.gameObject);
        bool open = UIStateManager.Instance != null && UIStateManager.Instance.IsOpen(UIWindowId.Station);
        return $"station usable {station.Usable}, tier {station.Tier}, window open {open}";
    }

    public static string BreachStart()
    {
        GameSession.Settlement.WarnOfBreach();
        WardBreachDirector.Current.Begin();
        return $"breach {WardBreachDirector.Current.IsActive}, monsters {MonsterAI.Active.Count}";
    }

    public static string BreachStatus()
    {
        var log = new StringBuilder($"breach {WardBreachDirector.Current != null && WardBreachDirector.Current.IsActive}; ");
        foreach (FieldInfo field in typeof(WardBreachDirector).GetFields(BindingFlags.NonPublic | BindingFlags.Instance).Where(one => one.Name.ToLowerInvariant().Contains("ward")))
        {
            log.Append($"{field.Name}={field.GetValue(WardBreachDirector.Current)}; ");
        }

        foreach (MonsterAI monster in MonsterAI.Active.Where(one => one.name.Contains("Clone")).Take(6))
        {
            log.Append($"{monster.name} d={Vector3.Distance(monster.transform.position, HomePortal.Current.transform.position):0.0} {monster.CurrentState}; ");
        }

        return log.ToString();
    }

    public static string Shot(string path)
    {
        ScreenCapture.CaptureScreenshot(path);
        return "shot " + path;
    }
}
