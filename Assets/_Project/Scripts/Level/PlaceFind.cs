using UnityEngine;

/// <summary>
/// A thing left at a named place, to take once (Docs/HavenExplorationReview_2026-11-19.md §2): the old masons' cut stone stacked at the
/// Old Quarry, granite at the Grey Quarry. The names promised what the props lacked. Taken once by each character (CharacterProgress'
/// used stones, as an Experience Stone), so in co-op each player takes their own, as raid loot is personal. Stood by HavenPlaceVerbs
/// with a small marker of its own; gone once taken.
/// </summary>
[RequireComponent(typeof(Interactable))]
[InAFriendsHaven(HavenUse.Anyone, "the visitor's own find, once")]
public sealed class PlaceFind : MonoBehaviour, IInteractionName, IInteractionVerb, IInteractionCondition
{
    private HavenPlaceVerbs.Find find;
    private WorldAtlas.Place place;
    private Interactable interactable;
    private ParticleSystem glints;

    public string InteractionName => find != null ? find.name : "";
    public string Verb => find != null ? find.verb : "Take";

    /// <summary>The key its taking is remembered by.</summary>
    public static string TakenKey(string placeId, string item) => $"SafeHaven:once:{placeId}:{item}";

    /// <summary>The key a find is taken by now: once for good, or a work site's yield once in every Every Days days at home (the days
    /// counted in whole spans, so it is the same key all through one span).</summary>
    public static string KeyOf(WorldAtlas.Place place, HavenPlaceVerbs.Find find) =>
        KeyOf(place, find, GameSession.Home != null ? GameSession.Home.DaysPassed : 0);

    /// <summary>The key a find is taken by after <paramref name="daysAtHome"/> days at home. A thing only to use (no item: a lantern to
    /// light, marks to read) is remembered by its name.</summary>
    public static string KeyOf(WorldAtlas.Place place, HavenPlaceVerbs.Find find, int daysAtHome) =>
        find.everyDays <= 0 ? OnceKey(place, find) : SpanKey(place, find, Mathf.Max(0, daysAtHome) / find.everyDays);

    private static string OnceKey(WorldAtlas.Place place, HavenPlaceVerbs.Find find) =>
        TakenKey(place.id, find.item != null ? find.item.Id : find.name);

    // A yield also by its name: two of the same thing at one place (smoked fish and a run of perch) are taken apart.
    private static string SpanKey(WorldAtlas.Place place, HavenPlaceVerbs.Find find, int span) => $"{OnceKey(place, find)}:{find.name}:{span}";

    /// <summary>
    /// How many spans' yield wait at a work site after <paramref name="daysAtHome"/> days at home: a yield left untaken builds up for
    /// Keeps Spans spans at most, so a walk missed isn't a yield lost (the world review's "chore loop"), and a long absence doesn't pile
    /// up more than that. 1 for a find taken once that is still there; 0 when there is nothing to take.
    /// </summary>
    public static int Waiting(WorldAtlas.Place place, HavenPlaceVerbs.Find find, int daysAtHome, System.Func<string, bool> used)
    {
        if (find.everyDays <= 0)
        {
            return used(OnceKey(place, find)) ? 0 : 1;
        }

        int span = Mathf.Max(0, daysAtHome) / find.everyDays, waiting = 0;
        for (int s = Mathf.Max(0, span - Mathf.Max(1, find.keepsSpans) + 1); s <= span; s++)
        {
            if (!used(SpanKey(place, find, s)))
            {
                waiting++;
            }
        }

        return waiting;
    }

    /// <summary>Marks everything waiting as taken (every span it builds up over, for a yield).</summary>
    private static void MarkTaken(WorldAtlas.Place place, HavenPlaceVerbs.Find find, int daysAtHome)
    {
        if (find.everyDays <= 0)
        {
            GameSession.Progress.MarkStoneUsed(OnceKey(place, find));
            return;
        }

        int span = Mathf.Max(0, daysAtHome) / find.everyDays;
        for (int s = Mathf.Max(0, span - Mathf.Max(1, find.keepsSpans) + 1); s <= span; s++)
        {
            GameSession.Progress.MarkStoneUsed(SpanKey(place, find, s));
        }
    }

    private static int DaysAtHome => GameSession.Home != null ? GameSession.Home.DaysPassed : 0;

    /// <summary>True when there is nothing to take there now.</summary>
    public static bool IsTaken(WorldAtlas.Place place, HavenPlaceVerbs.Find find) =>
        GameSession.Progress != null && Waiting(place, find, DaysAtHome, GameSession.Progress.HasUsedStone) == 0;

    /// <summary>A find taken or used: tells the places to look again (a yield it emptied, a stage it opened).</summary>
    public static event System.Action Taken;

    // Entering Play doesn't reload scripts in this project.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Taken = null;

    public static PlaceFind Stand(Transform parent, WorldAtlas.Place place, HavenPlaceVerbs.Find find, Vector3 at)
    {
        // A thing only used (a lantern lit, a cairn added to) stays standing as it is after: only what is taken is gone.
        bool used = IsTaken(place, find);
        if (used && find.item != null)
        {
            return null;
        }

        var holder = new GameObject($"{place.id} {find.name}");
        holder.SetActive(false);
        holder.transform.SetParent(parent, false);
        holder.transform.position = at;
        // First, so the interaction's reach is to the marker's colliders.
        if (find.marker != null)
        {
            GameObject shown = Instantiate(find.marker, holder.transform);
            shown.transform.localPosition = Vector3.zero;
            shown.transform.localRotation = Quaternion.Euler(0f, find.markerTurn, 0f);
            shown.transform.localScale = Vector3.one * find.markerScale;
        }
        else
        {
            Marker(holder.transform, find.item);
        }

        // It glitters, as a body with loot does, while there is something to do there: a find is seen from the game's camera.
        ParticleSystem glints = used ? null : LootableRemains.Sparkles(holder.transform, BodyLootSettings.Current);
        if (glints != null)
        {
            glints.transform.localPosition = new Vector3(0f, 0.4f, 0f);
        }

        var interactable = holder.AddComponent<Interactable>();
        // What is taken goes with the taking; what is only used stays, its marker part of the place.
        interactable.Configure(InteractionAnimation.GatherLow, 1.2f, 2.5f, find.item != null);
        PlaceFind taken = holder.AddComponent<PlaceFind>();
        taken.find = find;
        taken.place = place;
        taken.interactable = interactable;
        taken.glints = glints;
        interactable.Completed.AddListener(taken.OnTaken);
        holder.SetActive(true);
        return taken;
    }

    public bool CanInteract(GameObject user, out string reason)
    {
        reason = null;
        if (find == null || Player.Current == null || user != Player.Current.gameObject)
        {
            return false;
        }

        // Nothing there now (used already, or a yield emptied by what was taken beside it): no prompt, nothing to do.
        if (IsTaken(place, find))
        {
            return false;
        }

        // Kept to its hours (a lantern lit after dark): there by day, but not yet.
        if (!PlaceBeacon.PlaysAt(find.when, AtmosphereConditions.Hour))
        {
            reason = find.when == HavenPlaceVerbs.When.Night ? "Not yet: it wants the dark." : "Not now: it wants the day.";
            return false;
        }

        // Taken whole or not at all: a full bag lost the stack for good, and the quest still counted it. A thing only to use needs no room.
        if (find.item != null && Player.Current.Bag.Container.SpaceFor(find.item) < find.amount)
        {
            reason = $"Your bag has no room for {find.amount} {find.item.DisplayName}.";
            return false;
        }

        // What it takes (planks to lay), carried in the bag.
        if (find.costItem != null && Player.Current.Bag.Container.Count(find.costItem) < find.costAmount)
        {
            reason = $"It takes {find.costAmount} {find.costItem.DisplayName}.";
            return false;
        }

        return true;
    }

    private void OnDestroy()
    {
        if (interactable != null)
        {
            interactable.Completed.RemoveListener(OnTaken);
        }
    }

    private void OnTaken()
    {
        Player player = Player.Current;
        if (find == null || player == null || GameSession.Progress == null)
        {
            return;
        }

        int days = DaysAtHome;
        // As many spans' worth as built up (a yield), else one.
        int waiting = Waiting(place, find, days, GameSession.Progress.HasUsedStone);
        int amount = find.amount * Mathf.Max(1, waiting);
        if (waiting == 0 || (find.item != null && player.Bag.Container.SpaceFor(find.item) < amount)
            || (find.costItem != null && player.Bag.Container.Count(find.costItem) < find.costAmount))
        {
            return;
        }

        MarkTaken(place, find, days);
        if (find.costItem != null)
        {
            player.Bag.Container.Remove(find.costItem, find.costAmount);
        }

        // What is taken for good counts as the yield of the same thing at the same place: the clamp just lit was raked out with it,
        // and has more in a day or two, not at once (and nothing built up from the days before it burned).
        if (find.item != null && find.everyDays <= 0 && HavenPlaceVerbs.Current != null)
        {
            foreach (HavenPlaceVerbs.Find yield in HavenPlaceVerbs.Current.Finds)
            {
                if (yield != null && yield.everyDays > 0 && yield.placeId == place.id && yield.item == find.item)
                {
                    MarkTaken(place, yield, days);
                }
            }
        }

        if (glints != null)
        {
            Destroy(glints.gameObject);
            glints = null;
        }

        int given = find.item != null ? player.Bag.Give(find.item, amount, ArrivalCause.Found) : 0;
        if (find.questTarget != null)
        {
            QuestDeeds.Raise(GameEventKind.Used, find.questTarget, player.gameObject);
        }

        PlaytestLogger.Note(PlaytestLog.PlaceFindTaken, place.id, given, find.item != null ? find.item.Id : find.name);
        if (!string.IsNullOrEmpty(find.taken) && DialogueBox.Current != null && !DialogueBox.IsSpeaking)
        {
            DialogueBox.Current.Bark(DialogueSpeaker.Player(), $"<i>{find.taken}</i>", null);
        }

        Taken?.Invoke();
    }

    // What it is, seen from above: three of the item's world mesh, stacked, copied as bare meshes (its prefab is a pickup: an
    // instance of it would be three free items).
    private static void Marker(Transform holder, ItemDefinition item)
    {
        GameObject model = item != null ? item.Prefab : null;
        MeshFilter shape = model != null ? model.GetComponentInChildren<MeshFilter>(true) : null;
        MeshRenderer look = shape != null ? shape.GetComponent<MeshRenderer>() : null;
        if (shape == null || shape.sharedMesh == null || look == null)
        {
            return;
        }

        for (int i = 0; i < 3; i++)
        {
            var piece = new GameObject("Stack", typeof(MeshFilter), typeof(MeshRenderer));
            piece.transform.SetParent(holder, false);
            piece.transform.localPosition = new Vector3((i - 1) * 0.35f, 0.1f + (i == 1 ? 0.22f : 0f), (i % 2) * 0.2f);
            piece.transform.localRotation = Quaternion.Euler(0f, i * 37f, 0f);
            piece.transform.localScale = shape.transform.lossyScale;
            piece.GetComponent<MeshFilter>().sharedMesh = shape.sharedMesh;
            piece.GetComponent<MeshRenderer>().sharedMaterials = look.sharedMaterials;
        }
    }
}
