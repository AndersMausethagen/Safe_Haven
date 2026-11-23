using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Points out what is needed when it grows near (the user's playtest, 2026-11-21: "resources which we need for a quest can glow a bit
/// more than usual, or the player should notify that a needed resource is nearby. For example: Life orb right over there, we could use
/// that"). Every second and a half it looks for gathering nodes within <see cref="Reach"/> metres whose yield an open quest step
/// (Collect Item, Gather Resource) or the Haven's wants (WantedAtHome) still need. The nearest is outlined in warm gold (OutlineCopies,
/// as the hover outline is), and the first time a kind of thing is seen it is said aloud: by the companion along, or as the player's own
/// thought; the same thing isn't said again for a while. On the player (Player adds it).
/// </summary>
[DisallowMultipleComponent]
public sealed class NeededNearby : MonoBehaviour
{
    public const float Reach = 16f;
    private const float LookEvery = 1.5f;
    private const float NeedsEvery = 5f;
    private const float SameThingSeconds = 150f;
    // What home wants is often common (wood, stone): said now and then, never outlined.
    private const float HomeWantSeconds = 600f;
    // Another node takes the outline only when it is this much nearer (squared metres), so it doesn't flick between two.
    private const float SwitchCloser = 9f;
    private const string ForHome = "home";
    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly Collider[] Hits = new Collider[256];

    // What is needed, and what for (a quest's name, or "home").
    private readonly Dictionary<Object, string> needed = new Dictionary<Object, string>();
    private readonly Dictionary<Object, float> saidAt = new Dictionary<Object, float>();
    private float nextLook;
    private float nextNeeds;
    private Player player;
    private Material glowMaterial;
    private Material maskMaterial;
    private ResourceNode glowing;
    private List<OutlineCopies.Entry> glow;

    public static void AddTo(GameObject who)
    {
        if (!who.TryGetComponent(out NeededNearby _))
        {
            who.AddComponent<NeededNearby>();
        }
    }

    /// <summary>The node outlined now as needed, or null.</summary>
    public ResourceNode Glowing => glowing;

    private void Start()
    {
        player = GetComponent<Player>();
        var outline = GetComponent<PlayerInteractionOutline>();
        if (outline != null && outline.OutlineMaterial != null && outline.MaskMaterial != null)
        {
            glowMaterial = new Material(outline.OutlineMaterial) { name = "Needed Outline" };
            Color gold = UIStyle.Current.AccentBright;
            gold.a = 0.9f;
            glowMaterial.SetColor(OutlineColorId, gold);
            maskMaterial = outline.MaskMaterial;
        }
    }

    private void OnDisable() => StopGlow();

    private void OnDestroy()
    {
        StopGlow();
        if (glowMaterial != null)
        {
            Destroy(glowMaterial);
        }
    }

    private void Update()
    {
        if (Time.time < nextLook)
        {
            return;
        }

        nextLook = Time.time + LookEvery;
        if (Time.time >= nextNeeds)
        {
            nextNeeds = Time.time + NeedsEvery;
            ReadNeeds();
        }

        Object what = null;
        ResourceNode nearest = needed.Count > 0 ? NearestNeeded(out what) : null;
        if (nearest == null || what == null)
        {
            StopGlow();
            return;
        }

        bool forQuest = needed[what] != ForHome;
        if (forQuest)
        {
            Glow(nearest);
        }
        else
        {
            StopGlow();
        }

        Remark(what, needed[what], forQuest ? SameThingSeconds : HomeWantSeconds);
    }

    // The open steps of every quest under way that ask for something to be gathered or carried, and what home wants.
    private void ReadNeeds()
    {
        needed.Clear();
        QuestManager quests = QuestManager.Current;
        if (quests != null)
        {
            foreach (QuestDefinition quest in quests.Quests)
            {
                QuestLog.Entry entry = quest != null && quests.Log.StatusOf(quest) == QuestStatus.Active ? quests.Log.Get(quest) : null;
                if (entry == null)
                {
                    continue;
                }

                for (int i = 0; i < quest.Objectives.Count; i++)
                {
                    QuestObjective step = quest.Objectives[i];
                    Object target = step == null || !entry.IsObjectiveOpen(i) ? null
                        : step.Type == ObjectiveType.GatherResource ? step.GatherTarget
                        : step.Type == ObjectiveType.CollectItem ? step.Item : null;
                    if (target != null && !needed.ContainsKey(target))
                    {
                        needed[target] = quest.DisplayName;
                    }
                }
            }
        }

        if (player != null)
        {
            foreach (WantedAtHome.Want want in WantedAtHome.Now(player))
            {
                Object target = want.Item != null ? (Object)want.Item : want.Resource;
                if (target != null && !needed.ContainsKey(target))
                {
                    needed[target] = ForHome;
                }
            }
        }
    }

    // The nearest node yielding something needed that the player can gather now (the right tool, its tier): a quest's need before
    // home's, and the one outlined already unless another is clearly nearer.
    private ResourceNode NearestNeeded(out Object what)
    {
        what = null;
        ResourceNode best = null;
        float bestDistance = float.MaxValue;
        bool bestForQuest = false;
        Object kept = null;
        float keptDistance = float.MaxValue;
        int count = Physics.OverlapSphereNonAlloc(transform.position, Reach, Hits, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            ResourceNode node = Hits[i].GetComponentInParent<ResourceNode>();
            if (node == null || node.IsDepleted || node.Definition == null || node.Definition.Yields == null)
            {
                continue;
            }

            Object yields = NeededFrom(node);
            if (yields == null || !node.CanInteract(gameObject, out _))
            {
                continue;
            }

            float distance = (node.transform.position - transform.position).sqrMagnitude;
            bool forQuest = needed[yields] != ForHome;
            if (node == glowing)
            {
                kept = yields;
                keptDistance = distance;
            }

            if ((forQuest && !bestForQuest) || (forQuest == bestForQuest && distance < bestDistance))
            {
                best = node;
                bestDistance = distance;
                bestForQuest = forQuest;
                what = yields;
            }
        }

        // Stay with the node outlined unless the new one is a quest's where it was home's, or clearly nearer.
        if (kept != null && best != glowing && (needed[kept] != ForHome) == bestForQuest && keptDistance - bestDistance < SwitchCloser)
        {
            what = kept;
            return glowing;
        }

        return best;
    }

    private Object NeededFrom(ResourceNode node)
    {
        foreach (ItemDropEntry entry in node.Definition.Yields.Entries)
        {
            // A row this map doesn't give (Only In Raid elsewhere: the Misty Woods' sap on a Haven tree) is no source here.
            if (entry != null && entry.Drop != null && entry.DropsHere && needed.ContainsKey(entry.Drop))
            {
                return entry.Drop;
            }
        }

        return null;
    }

    // Said once in a while for each kind of thing: by the companion along, or the player's own thought.
    private void Remark(Object what, string forWhat, float every)
    {
        if (saidAt.TryGetValue(what, out float at) && Time.time - at < every)
        {
            return;
        }

        saidAt[what] = Time.time;
        string name = what is ItemDefinition item ? item.DisplayName : what is ResourceDefinition resource ? resource.DisplayName : what.name;
        string why = forWhat == ForHome ? "wanted at home" : $"for {forWhat}";
        Companion along = Companion.Active.Count > 0 ? Companion.Active[0] : null;
        string line = along != null
            ? $"{along.DisplayName}: \"{name}, right over there. We could use that.\""
            : $"<i>{name}, right over there. I could use that.</i>";
        GameMessages.Post($"{line}  <color={UIStyle.Current.MutedHex}>({why})</color>", MessageKind.Info);
    }

    private void Glow(ResourceNode node)
    {
        if (node == glowing)
        {
            if (glow != null)
            {
                OutlineCopies.SetShown(glow, true);
            }

            return;
        }

        StopGlow();
        glowing = node;
        if (glowMaterial != null && maskMaterial != null)
        {
            glow = OutlineCopies.Create(node.transform, glowMaterial, maskMaterial, "Needed Outline", "Needed Mask");
            OutlineCopies.SetShown(glow, true);
        }
    }

    private void StopGlow()
    {
        if (glow != null)
        {
            OutlineCopies.Destroy(glow);
            glow = null;
        }

        glowing = null;
    }
}
