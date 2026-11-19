using UnityEngine;

/// <summary>
/// Finds the scene's named places (its WorldAtlas) as the player walks up to them: the first time within a place's radius it goes on
/// the world map and the player is told. What was found is kept in the journal's record, so it stays found. Made for every scene
/// with a player and an atlas (ExperienceTracker); it only checks a few times a second.
/// </summary>
public sealed class PlaceDiscovery : MonoBehaviour
{
    private const float CheckSeconds = 0.5f;

    private WorldAtlas atlas;
    private float nextCheck;

    public static PlaceDiscovery Current { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Current = null;

    /// <summary>The atlas of the scene being played, or null.</summary>
    public WorldAtlas Atlas => atlas;

    /// <summary>Starts finding places in this scene, if it has an atlas.</summary>
    public static void Begin(string sceneName)
    {
        WorldAtlas found = WorldAtlas.For(sceneName);
        if (found == null)
        {
            if (Current != null)
            {
                Destroy(Current.gameObject);
            }

            return;
        }

        if (Current == null)
        {
            Current = new GameObject("Place Discovery").AddComponent<PlaceDiscovery>();
        }

        Current.atlas = found;
    }

    private void OnDestroy()
    {
        if (Current == this)
        {
            Current = null;
        }
    }

    private void Update()
    {
        if (atlas == null || Time.time < nextCheck || Player.Current == null)
        {
            return;
        }

        nextCheck = Time.time + CheckSeconds;
        Vector3 at = Player.Current.transform.position;
        var here = new Vector2(at.x, at.z);
        JournalRecord record = GameSession.Progress.Journal;
        foreach (WorldAtlas.Place place in atlas.Places)
        {
            if ((place.at - here).sqrMagnitude <= place.radius * place.radius && record.NoteDiscovered(place.id))
            {
                WorldAtlas.Region region = atlas.RegionAt(new Vector3(place.at.x, 0f, place.at.y));
                string where = region != null ? $", {region.name}" : "";
                GameMessages.Post(GameInput.WithRebinds($"Found: {place.name}{where}. It is on your map now ({{key:WorldMap}})."));
                PlaytestLogger.Note(PlaytestLog.PlaceFound, place.id, Mathf.RoundToInt(place.at.magnitude), region != null ? region.name : "");
                Territory.PlaceFound(place);
                UIMotion.Sound(AudioManager.Sounds != null ? AudioManager.Sounds.QuestStarted : null);
            }
        }
    }
}
