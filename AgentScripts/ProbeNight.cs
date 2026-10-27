using System.Reflection;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Play-mode probe for nights at home (run each step with `unity command run_script --file AgentScripts/ProbeNight.cs
/// --entry ProbeNight.<Step>`): torches, night prowlers and the light they avoid. Changes only the running game.
/// </summary>
public static class ProbeNight
{
    private static readonly Vector3 Spot = new Vector3(-34f, 0f, -10f);

    /// <summary>Night, prowlers allowed without the breach and quick to come, the player beside a fresh torch.</summary>
    public static string Setup()
    {
        var prowlers = Object.FindAnyObjectByType<NightProwlers>();
        Set(prowlers, "requireBreachWon", false);
        Set(prowlers, "spawnInterval", 3f);
        Player.Current.Health.AddInvulnerability("ProbeNight");

        var torch = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Structures/Torch.prefab");
        NavMesh.SamplePosition(Spot, out NavMeshHit hit, 10f, NavMesh.AllAreas);
        Object.Instantiate(torch, hit.position, Quaternion.identity).name = "Probe Torch";
        Move(hit.position + Vector3.right * 2f);
        return Report();
    }

    /// <summary>Walks the player out into the dark, away from the torch and the ward.</summary>
    public static string IntoTheDark()
    {
        NavMesh.SamplePosition(Spot + new Vector3(-22f, 0f, 6f), out NavMeshHit hit, 10f, NavMesh.AllAreas);
        Move(hit.position);
        return Report();
    }

    public static string BackToTheLight()
    {
        NavMesh.SamplePosition(Spot, out NavMeshHit hit, 10f, NavMesh.AllAreas);
        Move(hit.position + Vector3.right * 1.5f);
        return Report();
    }

    public static string Report()
    {
        var prowlers = Object.FindAnyObjectByType<NightProwlers>();
        Vector3 player = Player.Current.transform.position;
        var sb = new System.Text.StringBuilder(
            $"night={prowlers.IsProwlingTime} playerLit={VisibilitySource.IsLit(player)} sources={VisibilitySource.Active.Count} " +
            $"fogLights={AtmosphereLight.Active.Count} prowlers={prowlers.Alive.Count}: ");
        foreach (MonsterAI ai in prowlers.Alive)
        {
            sb.Append($"[{ai.CurrentState} d={Vector3.Distance(ai.transform.position, player):0.0} lit={VisibilitySource.IsLit(ai.transform.position)} " +
                      $"target={(ai.Target != null)} stage={ai.Senses.Stage}] ");
        }

        return sb.ToString();
    }

    private static void Move(Vector3 position)
    {
        Player player = Player.Current;
        var controller = player.GetComponent<CharacterController>();
        controller.enabled = false;
        player.transform.position = position + Vector3.up * 0.1f;
        controller.enabled = true;
    }

    private static void Set(object target, string field, object value)
    {
        target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
    }
}
