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
    public static string KeyOf(WorldAtlas.Place place, HavenPlaceVerbs.Find find, int daysAtHome)
    {
        string key = TakenKey(place.id, find.item != null ? find.item.Id : find.name);
        // A yield also by its name: two of the same thing at one place (smoked fish and a run of perch) are taken apart.
        return find.everyDays <= 0 ? key : $"{key}:{find.name}:{Mathf.Max(0, daysAtHome) / find.everyDays}";
    }

    /// <summary>True when the find has been taken (this span, for a yield).</summary>
    public static bool IsTaken(WorldAtlas.Place place, HavenPlaceVerbs.Find find) =>
        GameSession.Progress != null && GameSession.Progress.HasUsedStone(KeyOf(place, find));

    public static PlaceFind Stand(Transform parent, WorldAtlas.Place place, HavenPlaceVerbs.Find find, Vector3 at)
    {
        if (IsTaken(place, find))
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

        var interactable = holder.AddComponent<Interactable>();
        interactable.Configure(InteractionAnimation.GatherLow, 1.2f, 2.5f, true);
        PlaceFind taken = holder.AddComponent<PlaceFind>();
        taken.find = find;
        taken.place = place;
        taken.interactable = interactable;
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

        // Taken whole or not at all: a full bag lost the stack for good, and the quest still counted it. A thing only to use needs no room.
        if (find.item != null && Player.Current.Bag.Container.SpaceFor(find.item) < find.amount)
        {
            reason = $"Your bag has no room for {find.amount} {find.item.DisplayName}.";
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
        if (find == null || player == null || (find.item != null && player.Bag.Container.SpaceFor(find.item) < find.amount)
            || !GameSession.Progress.MarkStoneUsed(KeyOf(place, find)))
        {
            return;
        }

        // What is taken for good counts as this span's yield of the same thing at the same place: the clamp just lit was raked out
        // with it, and has more in a day or two, not at once.
        if (find.item != null && find.everyDays <= 0 && HavenPlaceVerbs.Current != null)
        {
            foreach (HavenPlaceVerbs.Find yield in HavenPlaceVerbs.Current.Finds)
            {
                if (yield != null && yield.everyDays > 0 && yield.placeId == place.id && yield.item == find.item)
                {
                    GameSession.Progress.MarkStoneUsed(KeyOf(place, yield));
                }
            }
        }

        int given = find.item != null ? player.Bag.Give(find.item, find.amount, ArrivalCause.Found) : 0;
        if (find.questTarget != null)
        {
            QuestDeeds.Raise(GameEventKind.Used, find.questTarget, player.gameObject);
        }

        PlaytestLogger.Note(PlaytestLog.PlaceFindTaken, place.id, given, find.item != null ? find.item.Id : find.name);
        if (!string.IsNullOrEmpty(find.taken) && DialogueBox.Current != null && !DialogueBox.IsSpeaking)
        {
            DialogueBox.Current.Bark(DialogueSpeaker.Player(), $"<i>{find.taken}</i>", null);
        }
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
