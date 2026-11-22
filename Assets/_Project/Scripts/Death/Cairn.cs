using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Where the player fell at home (the death rules, DeathRuleSet › Cairn): a pile of stones holding what the fall took from the bag
/// (and, under Harsh, what was worn), and the share of the lessons it gives back once it is emptied. It stays until it is emptied,
/// through saves and raids; a second fall makes a second cairn and nothing is destroyed. Marked on the map (MinimapHud). Only in the
/// player's own Haven: a fall in a friend's leaves nothing behind (HomeFalls).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Interactable))]
[InAFriendsHaven(HavenUse.NotInAHaven, "the player's own: a fall in a friend's Haven leaves no cairn (HomeFalls)")]
public sealed class Cairn : MonoBehaviour, ISaveable, IInteractionName, IInteractionVerb, IInteractionCondition
{
    private const int MostSlots = 96;
    private const float GroundSearch = 6f;

    [Serializable]
    private struct Saved
    {
        public List<SlotSaveData> slots;
        public List<AmountSaveData> lessons;
        public long fellUtc;
    }

    private static readonly List<Cairn> all = new List<Cairn>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => all.Clear();

    private readonly ItemContainer held = new ItemContainer(MostSlots, 1);
    private readonly List<AmountSaveData> lessons = new List<AmountSaveData>();
    private Interactable interactable;
    private long fellUtc;

    /// <summary>Every cairn in the loaded scene.</summary>
    public static IReadOnlyList<Cairn> All => all;

    public string SaveKey => "cairn";
    public string InteractionName => "Your cairn";
    public string Verb => "Take back your things";
    public int Stacks => StacksIn(held);

    private void Awake() => interactable = GetComponent<Interactable>();

    private void OnEnable()
    {
        all.Add(this);
        interactable.Completed.AddListener(OnTaken);
    }

    private void OnDisable()
    {
        all.Remove(this);
        interactable.Completed.RemoveListener(OnTaken);
    }

    /// <summary>
    /// Leaves a cairn where <paramref name="player"/> fell with what the fall takes: the bag's contents but deployed gear (anything that
    /// wears) and the safe pocket; with <paramref name="takeGear"/>, the gear in the bag and what is worn too. Holds
    /// <paramref name="lessonsBack"/> to give back. Null when there is nothing to leave.
    /// </summary>
    public static Cairn Leave(GameObject player, Vector3 fellAt, bool takeGear, IReadOnlyDictionary<ProficiencyDefinition, int> lessonsBack)
    {
        DeathSettings assets = DeathSettings.Current;
        if (player == null || assets.CairnPrefab == null)
        {
            return null;
        }

        var taken = new ItemContainer(MostSlots, 1);
        if (player.TryGetComponent(out ItemInventory inventory))
        {
            ItemContainer bag = inventory.Container;
            for (int i = 0; i < bag.SlotCount; i++)
            {
                ItemStack stack = bag.GetSlot(i);
                if (!stack.IsEmpty && (takeGear || !stack.Item.HasDurability))
                {
                    bag.TransferTo(i, taken);
                }
            }
        }

        if (takeGear && player.TryGetComponent(out PlayerEquipment equipment) && equipment.Gear != null)
        {
            ItemContainer worn = equipment.Gear.Slots;
            for (int i = 0; i < worn.SlotCount; i++)
            {
                if (!worn.GetSlot(i).IsEmpty)
                {
                    worn.TransferTo(i, taken);
                }
            }
        }

        bool anyLessons = false;
        foreach (KeyValuePair<ProficiencyDefinition, int> lesson in lessonsBack)
        {
            anyLessons |= lesson.Value > 0;
        }

        if (StacksIn(taken) == 0 && !anyLessons)
        {
            return null;
        }

        Vector3 at = NavMesh.SamplePosition(fellAt, out NavMeshHit hit, GroundSearch, NavMesh.AllAreas) ? hit.position : fellAt;
        var cairn = SaveableEntity.Spawn(assets.CairnPrefab, at, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f)).GetComponent<Cairn>();
        for (int i = 0; i < taken.SlotCount; i++)
        {
            taken.TransferTo(i, cairn.held);
        }

        foreach (KeyValuePair<ProficiencyDefinition, int> lesson in lessonsBack)
        {
            if (lesson.Key != null && lesson.Value > 0)
            {
                cairn.lessons.Add(new AmountSaveData { id = lesson.Key.Id, amount = lesson.Value });
            }
        }

        cairn.fellUtc = DateTime.UtcNow.Ticks;
        return cairn;
    }

    public bool CanInteract(GameObject user, out string reason)
    {
        reason = user != null && user.GetComponent<ItemInventory>() != null ? null : "Only you can take these back.";
        return reason == null;
    }

    // Everything that fits goes back into the bag (gear too: it is put on again from there); once the cairn is empty, half the lessons
    // come back with it and it is gone.
    private void OnTaken()
    {
        GameObject user = interactable.Interactor;
        if (user == null || !user.TryGetComponent(out ItemInventory inventory))
        {
            return;
        }

        int before = StacksIn(held);
        for (int i = 0; i < held.SlotCount; i++)
        {
            held.TransferTo(i, inventory.Container);
        }

        int left = StacksIn(held);
        if (left > 0)
        {
            GameMessages.Post($"Your bag can't hold the rest: {left} stack{(left == 1 ? "" : "s")} still lie in the cairn.", MessageKind.Warning);
            return;
        }

        var back = new List<string>();
        LevelingSettings settings = LevelingSettings.Current;
        foreach (AmountSaveData lesson in lessons)
        {
            ProficiencyDefinition skill = settings.FindSkill(lesson.id);
            if (skill != null && lesson.amount > 0 && GameSession.Progress.AddExperience(skill, lesson.amount, settings) >= 0)
            {
                back.Add($"{skill.DisplayName} +{lesson.amount}");
            }
        }

        string lines = back.Count > 0 ? $" Some of what you forgot comes back: {string.Join(", ", back)}." : "";
        GameMessages.Post($"You take back what you left{(before > 0 ? "" : " here")}.{lines}", MessageKind.Gain);
        double minutes = fellUtc > 0 ? (DateTime.UtcNow - new DateTime(fellUtc, DateTimeKind.Utc)).TotalMinutes : 0;
        PlaytestLogger.Note(PlaytestLog.CairnRecovered, before.ToString(), Mathf.RoundToInt((float)minutes * 60f), string.Join(", ", back));
        SaveableEntity.RemoveForGood(gameObject);
    }

    private static int StacksIn(ItemContainer container)
    {
        int stacks = 0;
        for (int i = 0; i < container.SlotCount; i++)
        {
            stacks += container.GetSlot(i).IsEmpty ? 0 : 1;
        }

        return stacks;
    }

    public string CaptureState() => JsonUtility.ToJson(new Saved { slots = SaveMapper.ToSlots(held), lessons = lessons, fellUtc = fellUtc });

    public void RestoreState(string state)
    {
        Saved saved = JsonUtility.FromJson<Saved>(state);
        var content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        held.Clear();
        if (saved.slots != null && content != null)
        {
            SaveMapper.ReadSlots(saved.slots, held, content, new List<string>());
        }

        lessons.Clear();
        if (saved.lessons != null)
        {
            lessons.AddRange(saved.lessons);
        }

        fellUtc = saved.fellUtc;
        if (StacksIn(held) == 0 && lessons.Count == 0)
        {
            SaveableEntity.RemoveForGood(gameObject);
        }
    }
}
