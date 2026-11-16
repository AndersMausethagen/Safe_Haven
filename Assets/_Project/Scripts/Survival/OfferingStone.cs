using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// An old stone by the path where something was left for the forest, as the pact once asked. Leaving a bite of food (the
/// least filling in the bag) earns the forest's favour for a while; it can be given once a day. Folklore with a small
/// use, not a chore: nothing goes wrong if it is passed by.
/// <para>Where it belongs: on a waystone or shrine prefab, beside an Interactable that doesn't deactivate.</para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Interactable))]
[InAFriendsHaven(HavenUse.Anyone, "the offering and its favour are the visitor's own")]
public sealed class OfferingStone : MonoBehaviour, IInteractionVerb
{
    [SerializeField] private string favourName = "The Forest's Favour";
    [SerializeField] private string verb = "Leave an offering";
    [SerializeField] private List<StatBonus> bonuses = new List<StatBonus>
    {
        new StatBonus(StatType.GatherYield, 0.1f, StatModifierOp.Percent),
        new StatBonus(StatType.MoveSpeed, 0.05f, StatModifierOp.Percent),
    };
    [SerializeField, Min(1f)] private float seconds = 480f;
    [Tooltip("Seconds of play before this stone takes another offering (about a day).")]
    [SerializeField, Min(1f)] private float restSeconds = 1200f;
    [SerializeField] private SoundDefinition accepted;
    [TextArea] [SerializeField] private string[] acceptedLines =
    {
        "The wind turns in the trees, and the path seems easier.",
        "Something small takes it while you aren't looking.",
        "The moss on the stone is warm under your hand.",
    };
    [SerializeField] private string emptyHanded = "The stone wants something to eat. You have nothing to leave.";
    [SerializeField] private string alreadyGiven = "Your last offering still lies on the stone.";

    private Interactable interactable;
    private float restsUntil = -1f;

    public string Verb => verb;

    public void Configure(SoundDefinition sound) => accepted = sound;

    private void Awake() => interactable = GetComponent<Interactable>();

    private void OnEnable() => interactable.Completed.AddListener(OnUsed);

    private void OnDisable() => interactable.Completed.RemoveListener(OnUsed);

    private void OnUsed()
    {
        GameObject user = interactable.Interactor;
        if (user == null)
        {
            return;
        }

        if (restsUntil > Time.time)
        {
            GameMessages.Post(alreadyGiven);
            return;
        }

        ItemInventory bag = user.GetComponentInParent<ItemInventory>();
        PlayerBoons boons = user.GetComponentInParent<PlayerBoons>();
        ItemDefinition food = bag != null ? LeastFood(bag.Container) : null;
        if (food == null || boons == null)
        {
            GameMessages.Post(emptyHanded, MessageKind.Warning);
            return;
        }

        bag.Container.Remove(food, 1);
        restsUntil = Time.time + restSeconds;
        boons.Add("offering", favourName, bonuses, seconds, false);
        if (accepted != null)
        {
            AudioManager.Play(accepted, transform.position + Vector3.up);
        }

        GameMessages.Post(acceptedLines.Length > 0 ? acceptedLines[Random.Range(0, acceptedLines.Length)] : favourName);
        GameMessages.Post($"{favourName}: {ItemTooltipView.DescribeBonuses(bonuses)} for {RaidStats.FormatTime(seconds)}", MessageKind.Gain);
    }

    // The smallest bite in the bag: a berry before a smoked boar.
    private static ItemDefinition LeastFood(ItemContainer bag)
    {
        ItemDefinition best = null;
        for (int i = 0; i < bag.SlotCount; i++)
        {
            ItemStack stack = bag.GetSlot(i);
            if (stack.Item != null && stack.Count > 0 && stack.Item.HungerRestore > 0f && (best == null || stack.Item.HungerRestore < best.HungerRestore))
            {
                best = stack.Item;
            }
        }

        return best;
    }
}
