using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A quest's news, shown where the eye is (Hearthline 3, the user's picture: "Quest Updated / Wake the portal"): a kit card
/// sliding down at the top of the screen with a mark, what happened ("New Quest", "Quest Updated", "Quest Complete") in the
/// title face, the quest's name and the step now open. A step that counts up (3/10 wood) updates the card already shown for
/// that quest rather than stacking new ones; several quests take their turn. Runs by itself from the first scene; it reads
/// GameSession.Quests and changes nothing.
/// </summary>
public sealed class QuestToast : MonoBehaviour
{
    private const float Hold = 3.2f;
    private const float Slide = 0.3f;
    private const float Width = 520f;
    private const float Height = 96f;

    private enum Kind
    {
        Started,
        Updated,
        Complete,
    }

    private readonly Queue<(QuestDefinition quest, Kind kind)> waiting = new Queue<(QuestDefinition, Kind)>();
    private RectTransform card;
    private CanvasGroup group;
    private Image mark;
    private TMP_Text heading;
    private TMP_Text title;
    private TMP_Text line;
    private QuestLog watched;
    private QuestDefinition shownQuest;
    private Kind shownKind;
    private float shownAt = -100f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        var root = new GameObject("Quest Toast");
        root.hideFlags = HideFlags.HideInHierarchy;
        DontDestroyOnLoad(root);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 212;
        UIRoot.ConfigureScaler(root.AddComponent<CanvasScaler>());
        root.AddComponent<QuestToast>().Build(root.transform);
    }

    private void Build(Transform root)
    {
        UIStyle style = UIStyle.Current;
        card = CodeUI.Box("Card", root, new Vector2(0.5f, 1f), new Vector2(Width, Height), new Vector2(0f, Height));
        card.pivot = new Vector2(0.5f, 1f);
        HearthKit.Sliced(card.gameObject.AddComponent<Image>(), style.Card, 1.3f).color = style.Panel;
        card.GetComponent<Image>().raycastTarget = false;
        HearthKit.Rim(card, 0.85f, 1.3f);
        group = card.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        // A gold ring with the mark inside, as in the picture.
        Image ring = CodeUI.At("Ring", card, 20f, 18f, 60f, 60f).gameObject.AddComponent<Image>();
        ring.sprite = UISprites.Disc;
        ring.color = HearthKit.Faded(style.AccentDeep, 0.9f);
        ring.raycastTarget = false;
        mark = HearthKit.Icon(card, "Star", 32f, 30f, 36f, style.AccentBright);
        heading = CodeUI.Label("Heading", card, 96f, 10f, Width - 116f, 26f, 18f, style.Accent);
        style.Title(heading);
        title = CodeUI.Label("Title", card, 96f, 34f, Width - 116f, 30f, 22f, style.Text);
        title.fontStyle = FontStyles.Bold;
        line = CodeUI.Label("Line", card, 96f, 64f, Width - 116f, 24f, 16f, style.Muted);
    }

    private void Update()
    {
        QuestLog log = GameSession.Quests;
        if (log != watched)
        {
            if (watched != null)
            {
                watched.Started -= OnStarted;
                watched.Progressed -= OnProgressed;
                watched.Completed -= OnCompleted;
            }

            watched = log;
            if (watched != null)
            {
                watched.Started += OnStarted;
                watched.Progressed += OnProgressed;
                watched.Completed += OnCompleted;
            }
        }

        float age = Time.unscaledTime - shownAt;
        if (age > Hold + Slide && waiting.Count > 0)
        {
            (QuestDefinition quest, Kind kind) = waiting.Dequeue();
            Show(quest, kind);
            age = 0f;
        }

        // Down into place, held, then up and away.
        float t = age < Slide ? age / Slide : age < Hold ? 1f : 1f - Mathf.Clamp01((age - Hold) / Slide);
        float eased = 1f - (1f - t) * (1f - t);
        group.alpha = eased;
        // Under the day line and its hours (it lay over them).
        card.anchoredPosition = new Vector2(0f, Mathf.Lerp(Height, -178f, eased));
    }

    private void OnStarted(QuestDefinition quest) => Tell(quest, Kind.Started);

    private void OnProgressed(QuestDefinition quest, int objective) => Tell(quest, Kind.Updated);

    private void OnCompleted(QuestDefinition quest) => Tell(quest, Kind.Complete);

    private void Tell(QuestDefinition quest, Kind kind)
    {
        if (quest == null)
        {
            return;
        }

        bool showing = Time.unscaledTime - shownAt < Hold;
        // The card already up for this quest is brought up to date (counting up wood doesn't stack cards).
        if (showing && quest == shownQuest && (kind == Kind.Updated || kind >= shownKind))
        {
            Show(quest, kind >= shownKind ? kind : shownKind, keepTime: kind == Kind.Updated);
            return;
        }

        foreach ((QuestDefinition queued, Kind _) in waiting)
        {
            if (queued == quest && kind == Kind.Updated)
            {
                return;
            }
        }

        if (showing || waiting.Count > 0)
        {
            waiting.Enqueue((quest, kind));
            return;
        }

        Show(quest, kind);
    }

    private void Show(QuestDefinition quest, Kind kind, bool keepTime = false)
    {
        UIStyle style = UIStyle.Current;
        shownQuest = quest;
        shownKind = kind;
        if (!keepTime)
        {
            shownAt = Time.unscaledTime;
        }
        else
        {
            // Kept up while it counts, without sliding again.
            shownAt = Mathf.Max(shownAt, Time.unscaledTime - Slide);
        }

        heading.text = kind == Kind.Started ? "New Quest" : kind == Kind.Complete ? "Quest Complete" : "Quest Updated";
        heading.color = kind == Kind.Complete ? style.Good : style.Accent;
        mark.sprite = style.Icon(kind == Kind.Complete ? "Star" : kind == Kind.Started ? "Book" : "Lead");
        mark.enabled = mark.sprite != null;
        title.text = quest.DisplayName;
        line.text = GameInput.WithRebinds(HearthKit.ToneNumbers(StepOf(quest, kind)));
        if (!keepTime)
        {
            UIMotion.Pop(card, 0.85f, 0.3f);
        }
    }

    private static string StepOf(QuestDefinition quest, Kind kind)
    {
        if (kind == Kind.Complete)
        {
            return "Done.";
        }

        foreach (QuestLog.Entry entry in GameSession.Quests.Entries)
        {
            if (entry.Quest != quest)
            {
                continue;
            }

            if (entry.Status == QuestStatus.ReadyToTurnIn)
            {
                return $"Return to {quest.TurnInTo}";
            }

            for (int i = 0; i < entry.Counts.Length; i++)
            {
                if (entry.IsObjectiveOpen(i))
                {
                    QuestObjective objective = quest.Objectives[i];
                    return objective.Amount > 1 ? $"{objective.Describe()}  {entry.Counts[i]}/{objective.Amount}" : objective.Describe();
                }
            }
        }

        return "";
    }
}
