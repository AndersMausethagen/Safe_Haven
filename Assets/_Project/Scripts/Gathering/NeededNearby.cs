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
    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly Collider[] Hits = new Collider[96];

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

        Glow(nearest);
        Remark(what, needed[what]);
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
                    needed[target] = "home";
                }
            }
        }
    }

    private ResourceNode NearestNeeded(out Object what)
    {
        what = null;
        ResourceNode best = null;
        float bestDistance = float.MaxValue;
        int count = Physics.OverlapSphereNonAlloc(transform.position, Reach, Hits, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            ResourceNode node = Hits[i].GetComponentInParent<ResourceNode>();
            if (node == null || node.IsDepleted || node.Definition == null || node.Definition.Yields == null)
            {
                continue;
            }

            float distance = (node.transform.position - transform.position).sqrMagnitude;
            if (distance >= bestDistance)
            {
                continue;
            }

            foreach (ItemDropEntry entry in node.Definition.Yields.Entries)
            {
                if (entry != null && entry.Drop != null && needed.ContainsKey(entry.Drop))
                {
                    best = node;
                    bestDistance = distance;
                    what = entry.Drop;
                    break;
                }
            }
        }

        return best;
    }

    // Said once in a while for each kind of thing: by the companion along, or the player's own thought.
    private void Remark(Object what, string forWhat)
    {
        if (saidAt.TryGetValue(what, out float at) && Time.time - at < SameThingSeconds)
        {
            return;
        }

        saidAt[what] = Time.time;
        string name = what is ItemDefinition item ? item.DisplayName : what is ResourceDefinition resource ? resource.DisplayName : what.name;
        string why = forWhat == "home" ? "wanted at home" : $"for {forWhat}";
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
