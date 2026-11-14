using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The HUD gives way to windows: while a window covers the screen (any but a conversation), the vitals, quest tracker,
/// noise meter, day line, skill bar, minimap, camp line and the rest fade out, and come back when it closes. Drawn over
/// windows, they covered their foot and sides (the user's workshop picture, 2026-11-13). Messages stay: they answer what
/// is done in a window ("Not enough materials").
/// <para>Each HUD part is moved into a holder of its own with a CanvasGroup (a part's own group runs its own showing and
/// hiding, and an object takes only one), stretched over its canvas so nothing moves. A part that shares a canvas with
/// windows is held alone; one with its own canvas has everything on that canvas held. Parts made later (built in code
/// when first needed) are found as they appear. Runs by itself from the first scene.</para>
/// </summary>
public sealed class HudGivesWay : MonoBehaviour
{
    private const string HolderName = "HUD Gives Way";
    private const float FadeSpeed = 7f;
    private const float ScanSeconds = 1f;

    // What is HUD. Messages (MessageFeedView) and the player's own item bar are left out on purpose.
    private static readonly Type[] HudParts =
    {
        typeof(PlayerVitalsView), typeof(QuestTrackerView), typeof(NoiseMeterView), typeof(EnvironmentHudView), typeof(SkillBarView),
        typeof(MinimapHud), typeof(HavenHud), typeof(TutorialNoticeHud), typeof(ExperienceHud), typeof(QuiverHud), typeof(RegionTitleHud),
        typeof(FishingHud), typeof(QuestWaypointHud), typeof(PartyHud), typeof(RaidHudView), typeof(TransportHud),
    };

    private readonly List<CanvasGroup> holders = new List<CanvasGroup>();
    // Per look: whether a root canvas holds windows (searching the shared UI root for one, part by part, was most of a look).
    private readonly Dictionary<Canvas, bool> sharesWithWindows = new Dictionary<Canvas, bool>();
    private float shown = 1f;
    private float written = -1f;
    private int writtenTo = -1;
    private float nextScan;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        var host = new GameObject("HUD Gives Way");
        host.hideFlags = HideFlags.HideInHierarchy;
        DontDestroyOnLoad(host);
        host.AddComponent<HudGivesWay>();
    }

    /// <summary>True while a window covers the screen, so the HUD stands aside (a conversation doesn't).</summary>
    public static bool Covered
    {
        get
        {
            UIStateManager ui = UIStateManager.Current;
            return ui != null && ui.IsAnyWindowOpen && !ui.OnlyOpen(UIWindowId.Dialogue);
        }
    }

    private void Update()
    {
        // The parts are only looked for while a window covers the HUD (the moment one opens, then every second): uncovered,
        // a part not yet held shows as it is, which is what showing means. Every second in every scene, the 16 searches of
        // the whole scene were a steady cost.
        bool covered = Covered;
        if (!covered)
        {
            nextScan = 0f;
        }
        else if (Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + ScanSeconds;
            Scan();
        }

        shown = Mathf.MoveTowards(shown, covered ? 0f : 1f, Time.unscaledDeltaTime * FadeSpeed);
        if (shown == written && holders.Count == writtenTo)
        {
            return;
        }

        for (int i = holders.Count - 1; i >= 0; i--)
        {
            if (holders[i] == null)
            {
                holders.RemoveAt(i);
                continue;
            }

            holders[i].alpha = shown;
            holders[i].blocksRaycasts = shown > 0.5f;
        }

        written = shown;
        writtenTo = holders.Count;
    }

    private void Scan()
    {
        sharesWithWindows.Clear();
        foreach (Type type in HudParts)
        {
            foreach (UnityEngine.Object found in FindObjectsByType(type, FindObjectsInactive.Include))
            {
                if (found is Component part && part.GetComponentInParent<Canvas>(true) is Canvas canvas)
                {
                    Hold(part.transform, canvas.rootCanvas);
                }
            }
        }
    }

    private void Hold(Transform part, Canvas root)
    {
        var rootRect = (RectTransform)root.transform;
        if (!sharesWithWindows.TryGetValue(root, out bool shared))
        {
            shared = root.GetComponentInChildren<UIWindow>(true) != null;
            sharesWithWindows[root] = shared;
        }
        if (!shared)
        {
            HoldCanvas(rootRect);
            return;
        }

        if (IsHeld(part))
        {
            return;
        }

        RectTransform holder = MakeHolder(rootRect);
        {
            // Only this part: its own branch under the canvas, where the windows are too.
            Transform branch = part;
            while (branch.parent != null && branch.parent != rootRect)
            {
                branch = branch.parent;
            }

            if (branch.parent != rootRect || IsHeld(branch))
            {
                Destroy(holder.gameObject);
                return;
            }

            holder.SetSiblingIndex(branch.GetSiblingIndex());
            branch.SetParent(holder, false);
        }

        holders.Add(holder.GetComponent<CanvasGroup>());
    }

    // A canvas of its own: one holder for everything on it, and whatever it makes later (markers, notices) is moved in
    // at the next look.
    private void HoldCanvas(RectTransform root)
    {
        Transform existing = root.Find(HolderName);
        RectTransform holder = existing != null ? (RectTransform)existing : MakeHolder(root);
        if (existing == null)
        {
            holders.Add(holder.GetComponent<CanvasGroup>());
        }

        var loose = new List<Transform>();
        foreach (Transform child in root)
        {
            if (child != holder)
            {
                loose.Add(child);
            }
        }

        foreach (Transform child in loose)
        {
            child.SetParent(holder, false);
        }
    }

    private static bool IsHeld(Transform part)
    {
        for (Transform t = part; t != null; t = t.parent)
        {
            if (t.name == HolderName)
            {
                return true;
            }
        }

        return false;
    }

    private static RectTransform MakeHolder(RectTransform canvas)
    {
        var holder = (RectTransform)new GameObject(HolderName, typeof(RectTransform), typeof(CanvasGroup)).transform;
        holder.SetParent(canvas, false);
        holder.anchorMin = Vector2.zero;
        holder.anchorMax = Vector2.one;
        holder.offsetMin = holder.offsetMax = Vector2.zero;
        holder.gameObject.layer = canvas.gameObject.layer;
        var group = holder.GetComponent<CanvasGroup>();
        group.interactable = true;
        return holder;
    }
}
