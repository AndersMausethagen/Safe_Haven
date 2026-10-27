using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Play-mode probe for held extractions and the Old Moss Troll's round in Misty Woods (run each step with
/// `unity command run_script --file AgentScripts/ProbeExtraction.cs --entry ProbeExtraction.<Step>`).
/// </summary>
public static class ProbeExtraction
{
    public static string Troll()
    {
        foreach (MonsterAI ai in MonsterAI.All)
        {
            if (ai != null && ai.Definition != null && ai.Definition.name == "OldMossTroll")
            {
                var agent = ai.GetComponent<NavMeshAgent>();
                return $"troll state={ai.CurrentState} roaming={ai.IsRoaming} at {ai.transform.position} speed={agent.speed:0.0}/{ai.Definition.MoveSpeed} " +
                       $"enabled={ai.enabled} home={ai.Home} stage={ai.Senses.Stage}";
            }
        }

        return "no troll";
    }

    public static string GoTo(string exitName, float offset)
    {
        ExtractionPoint exit = Find(exitName);
        Player player = Player.Current;
        player.Health.AddInvulnerability("ProbeExtraction");
        Vector3 wanted = exit.transform.position + Vector3.right * offset;
        if (NavMesh.SamplePosition(wanted, out NavMeshHit hit, 3f, NavMesh.AllAreas))
        {
            wanted = hit.position;
        }

        var controller = player.GetComponent<CharacterController>();
        controller.enabled = false;
        player.transform.position = wanted + Vector3.up * 0.1f;
        controller.enabled = true;
        return Report(exitName);
    }

    public static string Light(string exitName)
    {
        ExtractionPoint exit = Find(exitName);
        var interactable = exit.GetComponent<Interactable>();
        if (!interactable.TryBegin(Player.Current.gameObject, out string reason))
        {
            return "could not light: " + reason;
        }

        interactable.End(InteractionResult.Completed);
        return Report(exitName);
    }

    /// <summary>Spawns a wolf in the circle (and makes it stay out of the fight) to contest it.</summary>
    public static string Contest(string exitName)
    {
        ExtractionPoint exit = Find(exitName);
        var wolf = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemies/ForestBittenWolf.prefab");
        Vector3 at = exit.transform.position + Vector3.forward * 2f;
        NavMesh.SamplePosition(at, out NavMeshHit hit, 3f, NavMesh.AllAreas);
        GameObject spawned = Object.Instantiate(wolf, hit.position, Quaternion.identity);
        spawned.name = "Probe Wolf";
        return Report(exitName);
    }

    public static string KillContester(string exitName)
    {
        GameObject wolf = GameObject.Find("Probe Wolf");
        if (wolf != null)
        {
            Object.Destroy(wolf);
        }

        return Report(exitName);
    }

    public static string Report(string exitName)
    {
        ExtractionPoint exit = Find(exitName);
        RaidDirector director = RaidDirector.Current;
        string hud = "";
        var view = Object.FindAnyObjectByType<RaidHudView>();
        if (view != null)
        {
            var label = (TMPro.TMP_Text)typeof(RaidHudView).GetField("extractionLabel",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(view);
            hud = label != null ? label.text : "";
        }

        Transform ring = exit.transform.Find("Extraction Ring");
        Light beacon = exit.GetComponentInChildren<Light>();
        return $"{exit.DisplayName}: open={exit.IsOpen} lit={exit.IsLit} state={exit.HoldState} held={exit.HoldFraction:P0} " +
               $"left={exit.TimeLeft:0.0}s inside={exit.Inside(Player.Current.transform.position)} contested={exit.IsContested()} " +
               $"ring={(ring != null)} light={(beacon != null ? beacon.intensity.ToString("0.0") : "none")} " +
               $"raid={(director != null ? (director.Session.IsOver ? director.Session.Outcome.ToString() : "running") : "none")} hud=\"{hud}\"";
    }

    private static ExtractionPoint Find(string exitName)
    {
        foreach (ExtractionPoint point in ExtractionPoint.Active)
        {
            if (point.DisplayName == exitName)
            {
                return point;
            }
        }

        return null;
    }
}
