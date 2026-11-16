using UnityEngine;

/// <summary>
/// A stone of old memories found while exploring: touching it (its Interactable) gives experience once, then it goes
/// dark and can't be used again. Remembered in GameSession.Progress by its scene and place, so a stone in a raid (whose
/// scene starts fresh every time) still gives its experience only once per game. Its light and particles (optional) are
/// switched off once used.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Interactable))]
[AddComponentMenu("Safe Haven/Progression/Experience Stone")]
[InAFriendsHaven(HavenUse.Anyone, "the visitor's own experience")]
public sealed class ExperienceStone : MonoBehaviour
{
    [SerializeField, Min(1)] private int experience = 60;
    [Tooltip("Switched off once used: the glow, particles, anything that says \"touch me\".")]
    [SerializeField] private GameObject[] litParts = new GameObject[0];

    private Interactable interactable;

    public int Experience => experience;
    public bool Used => GameSession.Progress.HasUsedStone(Id);

    // Its scene and place: stable without any saved id, and different for every stone.
    public string Id
    {
        get
        {
            Vector3 p = transform.position;
            return $"{gameObject.scene.name}:{Mathf.RoundToInt(p.x)},{Mathf.RoundToInt(p.y)},{Mathf.RoundToInt(p.z)}";
        }
    }

    private void Awake()
    {
        interactable = GetComponent<Interactable>();
    }

    private void OnEnable() => interactable.Completed.AddListener(Touch);

    private void OnDisable() => interactable.Completed.RemoveListener(Touch);

    private void Start() => Show(Used);

    private void Touch()
    {
        if (!GameSession.Progress.MarkStoneUsed(Id))
        {
            return;
        }

        Show(true);
        GameMessages.Post("Old memories flow from the stone.", MessageKind.Gain);
        ExperienceTracker.GrantInsight(experience);
    }

    private void Show(bool used)
    {
        interactable.enabled = !used;
        foreach (GameObject part in litParts)
        {
            if (part != null)
            {
                part.SetActive(!used);
            }
        }
    }
}
