using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The door of a house someone is asleep in (review 2026-11-16: residents went in at 22:00 and couldn't be spoken to until
/// dawn, so a quest couldn't be handed in at night). Knocking wakes them: they come out of the door to talk and go back in
/// afterwards (ResidentLife.Wake); whoever has something for the player (a quest to give or take in) is the one who answers.
/// The prompt says who is asleep: "Tor, asleep until dawn — Knock to wake".
/// <para>
/// A child of the house at its door, made the first time someone goes in (<see cref="For"/>), and usable only while
/// anyone is inside (HavenHomes.Indoors): it looks a few times a second rather than following everyone in and out.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Interactable))]
[InAFriendsHaven(HavenUse.Anyone, "a knock at a door")]
public sealed class HouseDoor : MonoBehaviour, IInteractionName, IInteractionVerb
{
    private const float Reach = 2.2f;
    private const float LookSeconds = 0.25f;

    private static readonly List<ResidentLife> inside = new List<ResidentLife>();

    // Entering Play doesn't reload scripts in this project, so statics are cleared when it starts.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => inside.Clear();

    private BuildingObject house;
    private Interactable interactable;
    private float nextLook;
    private string sleepers = "";

    public string InteractionName => string.IsNullOrEmpty(sleepers) ? "Asleep until dawn" : $"{sleepers}, asleep until dawn";
    public string Verb => "Knock to wake";

    /// <summary>The door of this house, made the first time it is needed.</summary>
    public static HouseDoor For(BuildingObject house)
    {
        if (house == null)
        {
            return null;
        }

        HouseDoor door = house.GetComponentInChildren<HouseDoor>(true);
        if (door != null)
        {
            return door;
        }

        var holder = new GameObject("Door");
        holder.transform.SetParent(house.transform, false);
        holder.layer = house.gameObject.layer;
        door = holder.AddComponent<HouseDoor>();
        door.house = house;
        door.interactable.Configure(InteractionAnimation.None, 0f, Reach, false, InteractionKind.Use);
        door.Place();
        return door;
    }

    private void Awake()
    {
        interactable = GetComponent<Interactable>();
        house = GetComponentInParent<BuildingObject>();
        interactable.enabled = false;
    }

    private void OnEnable() => interactable.Completed.AddListener(Knock);

    private void OnDisable() => interactable.Completed.RemoveListener(Knock);

    // Just outside the door, at a knock's height.
    private void Place()
    {
        if (house != null)
        {
            transform.position = house.Door(out _) + Vector3.up * 1.2f;
        }
    }

    private void Update()
    {
        if (Time.unscaledTime < nextLook)
        {
            return;
        }

        nextLook = Time.unscaledTime + LookSeconds;
        bool anyone = house != null && HavenHomes.Indoors(house) > 0;
        if (interactable.enabled != anyone)
        {
            if (anyone)
            {
                Place();
            }

            interactable.enabled = anyone;
        }

        if (anyone)
        {
            sleepers = Names();
        }
    }

    private string Names()
    {
        Inside(house, inside);
        var names = new List<string>(inside.Count);
        foreach (ResidentLife life in inside)
        {
            ResidentNpc npc = life.GetComponent<ResidentNpc>();
            names.Add(npc != null && npc.Resident != null ? npc.Resident.DisplayName : life.name);
        }

        return names.Count <= 1 ? string.Join("", names) : string.Join(", ", names.GetRange(0, names.Count - 1)) + " and " + names[names.Count - 1];
    }

    /// <summary>Everyone asleep in this house now.</summary>
    public static void Inside(BuildingObject house, List<ResidentLife> found)
    {
        found.Clear();
        foreach (ResidentNpc npc in ResidentNpc.Present)
        {
            ResidentLife life = npc != null ? npc.Life : null;
            if (life != null && life.Inside == house)
            {
                found.Add(life);
            }
        }
    }

    // Everyone inside comes to the door; the one with something for the player (a quest marker over them) talks first.
    private void Knock()
    {
        GameObject by = interactable.Interactor;
        Inside(house, inside);
        if (inside.Count == 0)
        {
            return;
        }

        ResidentLife speaker = inside[0];
        foreach (ResidentLife life in inside)
        {
            if (HasWordForThePlayer(life))
            {
                speaker = life;
                break;
            }
        }

        foreach (ResidentLife life in inside.ToArray())
        {
            life.Wake(by, life == speaker);
        }
    }

    private static bool HasWordForThePlayer(ResidentLife life)
    {
        foreach (QuestMarker marker in life.GetComponentsInChildren<QuestMarker>(true))
        {
            if (marker.State == QuestGiverState.Available || marker.State == QuestGiverState.ReadyToTurnIn)
            {
                return true;
            }
        }

        return false;
    }
}
