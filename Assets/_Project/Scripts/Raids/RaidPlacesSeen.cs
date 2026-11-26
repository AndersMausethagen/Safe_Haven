using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Writes a playtest row the first time the player comes upon each place of a generated raid map: its sites (RaidMap.Places), its set
/// pieces and its landmarks (Docs/RaidLayoutAnalysis_2026-11-26.md R9). It shows which places draw players and which are passed by, the
/// heatmap aimed at places. Added by RaidDirector; reads the places once the raid runs, and does nothing on a painted map.
/// </summary>
[DisallowMultipleComponent]
public sealed class RaidPlacesSeen : MonoBehaviour
{
    /// <summary>Metres within which the player has come upon a place.</summary>
    public const float Reach = 14f;

    private const float CheckEvery = 0.5f;

    private readonly List<(string name, Vector3 position)> places = new List<(string, Vector3)>();
    private RaidDirector director;
    private Vector3 start;
    private bool gathered;
    private float nextCheck;

    private void Awake()
    {
        director = GetComponent<RaidDirector>();
    }

    private void Update()
    {
        // Once the raid runs: the map is laid out for good then (a failed attempt is laid out again while loading), and nothing is
        // written during the loading screen.
        if (Time.time < nextCheck || director == null || director.Player == null || !director.IsRunning)
        {
            return;
        }

        nextCheck = Time.time + CheckEvery;
        if (!gathered)
        {
            Gather();
        }

        if (places.Count == 0)
        {
            return;
        }

        Vector3 at = director.Player.transform.position;
        for (int i = places.Count - 1; i >= 0; i--)
        {
            if (!Within(places[i].position, at, Reach))
            {
                continue;
            }

            Vector3 flat = places[i].position - start;
            flat.y = 0f;
            PlaytestLogger.Note(PlaytestLog.CameUpon, places[i].name, Mathf.RoundToInt(director.Session.Elapsed), $"{Mathf.RoundToInt(flat.magnitude)} m from the start");
            places.RemoveAt(i);
        }
    }

    // Once, as the raid runs. A painted map has no places: nothing more is looked for.
    private void Gather()
    {
        gathered = true;
        RaidMap map = FindAnyObjectByType<RaidMap>();
        if (map == null || map.Places.Count == 0)
        {
            return;
        }

        start = map.PlayerStart != null ? map.PlayerStart.position : director.Player.transform.position;
        foreach (RaidPlace place in map.Places)
        {
            places.Add((NameOf(place), place.Position));
        }

        foreach (RaidSetPieceSite piece in FindObjectsByType<RaidSetPieceSite>(FindObjectsInactive.Exclude))
        {
            places.Add(("Set piece: " + (piece.Definition != null ? piece.Definition.DisplayName : piece.name), piece.transform.position));
        }

        foreach (RaidMapLandmark landmark in RaidMapLandmark.Active)
        {
            places.Add(("Landmark: " + landmark.name, landmark.transform.position));
        }
    }

    /// <summary>The row's subject for a site: its kind, and the group that took it where groups share the kind. Pure, for tests.</summary>
    public static string NameOf(RaidPlace place) => place.Label == place.Kind.ToString() ? place.Label : $"{place.Kind}: {place.Label}";

    /// <summary>True when <paramref name="at"/> is within <paramref name="reach"/> of <paramref name="place"/>, measured flat. Pure, for tests.</summary>
    public static bool Within(Vector3 place, Vector3 at, float reach)
    {
        Vector3 flat = place - at;
        flat.y = 0f;
        return flat.sqrMagnitude <= reach * reach;
    }
}
