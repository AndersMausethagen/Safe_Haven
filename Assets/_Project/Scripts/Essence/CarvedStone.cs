using UnityEngine;

/// <summary>
/// A stone in the deep woods carved with old marks (Docs/EnchantingResearch.md #5). Reading it (its Interactable) is Runecraft
/// practice at the raid's tier; with a Blank Parchment in the bag the player also takes a rubbing, an inscription of one of the
/// stones' enchantments not yet known (EnchantmentSource.CarvedStone, of the raid's tier and below) that has to get home to be read.
/// A stone is read once a raid: raids are laid out anew each time, so there is no lasting place to remember it by, and the practice
/// falls off with level like all practice. For quests it is a Use Object of its Quest Target ("a carved stone"), so one objective
/// serves every stone.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Interactable))]
[AddComponentMenu("Safe Haven/Essence/Carved Stone")]
[InAFriendsHaven(HavenUse.Anyone, "a rubbing is the visitor's own")]
public sealed class CarvedStone : MonoBehaviour, IInteractionName
{
    [Tooltip("The raid tier its marks belong to. 0: the raid's own.")]
    [SerializeField, Range(0, Tiers.Max)] private int tier;
    [Tooltip("What reading it counts as for quests (Use Object).")]
    [SerializeField, QuestIdentity] private QuestTargetDefinition questTarget;
    [Tooltip("Switched off once read: the glow in the marks.")]
    [SerializeField] private GameObject[] litParts = new GameObject[0];

    private Interactable interactable;
    private bool read;

    public string InteractionName => read ? "A carved stone (read)" : HasParchment(Player.Current != null ? Player.Current.gameObject : null) ? "Take a rubbing" : "Read the carved stone";

    public bool IsRead => read;

    /// <summary>The tier its marks belong to: its own, or the raid's.</summary>
    public int Tier => tier > 0 ? tier : RaidDirector.Current != null ? RaidDirector.Current.Session.Tier : 1;

    private void Awake()
    {
        interactable = GetComponent<Interactable>();
    }

    private void OnEnable() => interactable.Completed.AddListener(OnUsed);

    private void OnDisable() => interactable.Completed.RemoveListener(OnUsed);

    private void OnUsed() => Read(interactable.Interactor);

    /// <summary>Reads the stone for <paramref name="reader"/>, and takes a rubbing when they carry a Blank Parchment. False once read.</summary>
    public bool Read(GameObject reader)
    {
        if (read || reader == null)
        {
            return false;
        }

        read = true;
        Show(false);
        int learned = Inscriptions.Practise(Tier, EssenceSettings.Current.StoneWork);
        if (questTarget != null)
        {
            QuestDeeds.Raise(GameEventKind.Used, questTarget, reader);
        }

        if (!TakeRubbing(reader))
        {
            GameMessages.Post(HasParchmentItem()
                ? $"You trace the old marks with your fingers (+{learned} Runecraft). A Blank Parchment would have caught them."
                : $"You trace the old marks with your fingers (+{learned} Runecraft).", MessageKind.Gain);
        }

        return true;
    }

    // With a parchment in the bag: one of the stones' enchantments onto it, one not yet known first.
    private bool TakeRubbing(GameObject reader)
    {
        ItemDefinition blank = EssenceSettings.Current.BlankParchment;
        ItemInventory bag = reader.GetComponentInParent<ItemInventory>();
        if (blank == null || bag == null || !HasParchment(reader))
        {
            return false;
        }

        var random = new System.Random(Random.Range(0, int.MaxValue));
        EnchantmentDefinition found = Inscriptions.Pick(Inscriptions.Pool(EnchantmentSource.CarvedStone, Tier), reader, random);
        if (found == null)
        {
            return false;
        }

        if (bag.Container.Remove(blank, 1) == 0 && (bag.SafePocket == null || bag.SafePocket.Remove(blank, 1) == 0))
        {
            return false;
        }

        GameMessages.Post("You press the parchment to the stone and rub charcoal over it. The marks come up dark.", MessageKind.Gain);
        Inscriptions.Grant(reader, found, "a carved stone");
        return true;
    }

    private static bool HasParchmentItem() => EssenceSettings.Current.BlankParchment != null;

    private static bool HasParchment(GameObject reader)
    {
        ItemDefinition blank = EssenceSettings.Current.BlankParchment;
        ItemInventory bag = reader != null ? reader.GetComponentInParent<ItemInventory>() : null;
        return blank != null && bag != null && bag.CountCarried(blank) > 0;
    }

    private void Show(bool lit)
    {
        interactable.enabled = lit;
        foreach (GameObject part in litParts)
        {
            if (part != null)
            {
                part.SetActive(lit);
            }
        }
    }
}
