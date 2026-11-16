using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A place that leaves the player better off for a while: a hot sauna before a raid. Using it (its Interactable) gives its
/// bonuses as a boon, with a rest before it can be used again. Where it belongs: on a building prefab, next to an
/// Interactable that doesn't deactivate.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Interactable))]
[InAFriendsHaven(HavenUse.Anyone, "a sauna's or a meal's boon is the visitor's own")]
public sealed class BoonSource : MonoBehaviour, IInteractionVerb
{
    [SerializeField] private string boonName = "Warmed Through";
    [SerializeField] private string verb = "Rest";
    [SerializeField] private List<StatBonus> bonuses = new List<StatBonus>();
    [SerializeField, Min(1f)] private float seconds = 600f;

    private Interactable interactable;

    public string Verb => verb;

    private void Awake()
    {
        interactable = GetComponent<Interactable>();
    }

    private void OnEnable()
    {
        interactable.Completed.AddListener(OnUsed);
    }

    private void OnDisable()
    {
        interactable.Completed.RemoveListener(OnUsed);
    }

    private void OnUsed()
    {
        PlayerBoons boons = interactable.Interactor != null ? interactable.Interactor.GetComponentInParent<PlayerBoons>() : null;
        if (boons != null && boons.Add("place_" + boonName, boonName, bonuses, seconds, false))
        {
            GameMessages.Post($"{boonName}: {ItemTooltipView.DescribeBonuses(bonuses)} for {RaidStats.FormatTime(seconds)}", MessageKind.Gain);
        }
    }
}
