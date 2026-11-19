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

    public static PlaceFind Stand(Transform parent, WorldAtlas.Place place, HavenPlaceVerbs.Find find, Vector3 at)
    {
        string key = TakenKey(place.id, find.item.Id);
        if (GameSession.Progress != null && GameSession.Progress.HasUsedStone(key))
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
        return find != null && Player.Current != null && user == Player.Current.gameObject;
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
        if (find == null || player == null || !GameSession.Progress.MarkStoneUsed(TakenKey(place.id, find.item.Id)))
        {
            return;
        }

        int given = player.Bag.Give(find.item, find.amount, ArrivalCause.Found);
        if (find.questTarget != null)
        {
            QuestDeeds.Raise(GameEventKind.Used, find.questTarget, player.gameObject);
        }

        PlaytestLogger.Note(PlaytestLog.PlaceFindTaken, place.id, given, find.item.Id);
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
