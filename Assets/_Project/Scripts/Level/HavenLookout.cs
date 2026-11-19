using UnityEngine;

/// <summary>
/// A lookout in the Haven's land (Docs/HavenExplorationReview_2026-11-19.md §1): a cairn, a broken tower, the Standing Stones. Press E
/// to look out over the land: the fog lifts round it (Lookout Reveal), and every named place within Lookout Sees becomes seen, on the
/// world map in grey until it is walked to (and only then found and paid). The first look from each gives Insight and names one place
/// it sees that is still to be found. Near it the view pulls out and flattens, so the land below shows: from the top-down camera the
/// map is the long view. RaidLookout's Haven twin; stood at its place by HavenPlaceVerbs, as the cairn's prefab is a raid's landmark too.
/// Its first use is the character's (CharacterProgress' used stones), so in co-op each player has their own.
/// </summary>
[RequireComponent(typeof(Interactable))]
[InAFriendsHaven(HavenUse.Anyone, "a look with the visitor's own fog and map")]
public sealed class HavenLookout : MonoBehaviour, IInteractionName, IInteractionVerb
{
    private WorldAtlas.Place place;
    private HavenPlaceVerbs verbs;
    private Interactable interactable;
    private float weight;

    public string InteractionName => place != null ? place.name : "Lookout";
    public string Verb => "Look out over the land";

    /// <summary>The key its first use is remembered by.</summary>
    public static string UsedKey(string placeId) => "SafeHaven:lookout:" + placeId;

    public static HavenLookout AddTo(GameObject holder, WorldAtlas.Place place, HavenPlaceVerbs verbs)
    {
        // A use, done on the press: reach is to the holder (no collider), so it is generous.
        var interactable = holder.AddComponent<Interactable>();
        interactable.Configure(InteractionAnimation.GatherLow, 0f, 3.5f, false, InteractionKind.Use);
        HavenLookout lookout = holder.AddComponent<HavenLookout>();
        lookout.place = place;
        lookout.verbs = verbs;
        lookout.interactable = interactable;
        interactable.Completed.AddListener(lookout.OnLooked);
        return lookout;
    }

    private void OnDestroy()
    {
        if (interactable != null)
        {
            interactable.Completed.RemoveListener(OnLooked);
        }
    }

    private void OnLooked()
    {
        if (place == null || verbs == null)
        {
            return;
        }

        if (ExplorationMap.Current != null)
        {
            ExplorationMap.Current.RevealAround(transform.position, verbs.LookoutReveal);
        }

        WorldAtlas atlas = PlaceDiscovery.Current != null ? PlaceDiscovery.Current.Atlas : null;
        JournalRecord journal = GameSession.Progress.Journal;
        int newlySeen = 0;
        WorldAtlas.Place unfound = null;
        float nearestUnfound = float.MaxValue;
        if (atlas != null)
        {
            foreach (WorldAtlas.Place other in atlas.Places)
            {
                float distance = Vector2.Distance(other.at, place.at);
                if (other == place || distance > verbs.LookoutSees)
                {
                    continue;
                }

                if (!journal.HasDiscovered(other.id) && journal.NoteSeen(other.id))
                {
                    newlySeen++;
                }

                if (!journal.HasDiscovered(other.id) && distance < nearestUnfound)
                {
                    nearestUnfound = distance;
                    unfound = other;
                }
            }
        }

        bool first = GameSession.Progress.MarkStoneUsed(UsedKey(place.id));
        PlaytestLogger.Note(PlaytestLog.LookoutUsed, place.id, newlySeen, first ? "first" : "again");
        if (!first)
        {
            GameMessages.Post("The land lies open from up here: the map shows what you can see.", MessageKind.Info);
            return;
        }

        ExperienceTracker.GrantInsight(verbs.LookoutInsight);
        string seen = newlySeen > 0 ? $" {newlySeen} place{(newlySeen == 1 ? "" : "s")} marked on your map." : "";
        GameMessages.Post($"From up here the land lies open.{seen}", MessageKind.Info);
        if (unfound != null)
        {
            StartCoroutine(SayWhenFree($"<i>{Sightline(unfound)} To the {HavenPlaceVerbs.Compass(place.at, unfound.at)}. I haven't been there yet.</i>"));
        }
    }

    // Said once the line being spoken (the place's own thought, often) is done: pressing during it lost the first look's hint for good.
    private System.Collections.IEnumerator SayWhenFree(string line)
    {
        float giveUp = Time.unscaledTime + 15f;
        while ((DialogueBox.Current == null || DialogueBox.IsSpeaking) && Time.unscaledTime < giveUp)
        {
            yield return null;
        }

        if (DialogueBox.Current != null && !DialogueBox.IsSpeaking)
        {
            DialogueBox.Current.Bark(DialogueSpeaker.Player(), line, null);
        }
    }

    // What is seen of a place from afar, by its kind.
    private static string Sightline(WorldAtlas.Place seen)
    {
        switch (seen.kind)
        {
            case "Woodland Camp": return $"A thread of old smoke over the trees: {seen.name}.";
            case "Old Quarry": return $"A pale scar in the hillside: {seen.name}.";
            case "Burnt Farmstead": return $"Black roof-beams in a clearing: {seen.name}.";
            case "Old Barrow": return $"A long grassed mound, too regular to be a hill: {seen.name}.";
            case "Wolf Den": return $"Crows over a rock-fall, and something moving under it: {seen.name}.";
            case "Ruined Watchtower": case "Lookout Cairn": return $"Stones stacked on a height: {seen.name}.";
            case "Fisher's Rest": return $"Drying poles by the water: {seen.name}.";
            case "Shepherd's Fold": return $"A ring of low walls: {seen.name}.";
            default: return $"{seen.name}.";
        }
    }

    // Near it the view pulls out and flattens, eased, on real time (a pause doesn't stop it settling).
    private void Update()
    {
        Player player = Player.Current;
        float radius = verbs != null ? verbs.LookoutViewRadius : 6f;
        float wanted = player != null && (player.transform.position - transform.position).sqrMagnitude <= radius * radius ? 1f : 0f;
        if (Mathf.Approximately(weight, wanted) && wanted == 0f)
        {
            return;
        }

        weight = Mathf.MoveTowards(weight, wanted, Time.unscaledDeltaTime / 1.5f);
        float eased = weight * weight * (3f - 2f * weight);
        PlayerCameraController view = PlayerCameraController.Current;
        if (view != null && verbs != null)
        {
            view.SetCloseness(this, Mathf.Lerp(1f, verbs.LookoutViewDistance, eased));
            view.SetTilt(this, verbs.LookoutViewTilt * eased);
        }
    }

    private void OnDisable()
    {
        weight = 0f;
        PlayerCameraController view = PlayerCameraController.Current;
        if (view != null)
        {
            view.SetCloseness(this, 1f);
            view.SetTilt(this, 0f);
        }
    }
}
