using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// The Escape menu: Continue, Save, Saved Games, Journal, Co-op (open this game to friends and invite them, answer what friends ask, close it, or leave a friend's), Options and Quit Game. It is the only listener for the Escape key. A press
/// first goes to <see cref="BackHandlers"/> (cancel building placement, close open windows, stop an interaction), and
/// the menu opens only when nothing used it. While open it holds a <see cref="GamePause"/> request, which freezes time
/// in single-player and blocks player input.
/// <para>
/// It lives in the UI Root prefab, so every gameplay scene has one. Saving and loading go through SaveGameService
/// (which refuses both during a raid); quitting asks first, because progress since the last save is lost.
/// Saved Games and Options open their own screens over the menu (SavesScreen, OptionsScreen); this menu shows no page
/// meanwhile and comes back when they close.
/// </para>
/// </summary>
public sealed class PauseMenu : MonoBehaviour
{
    [SerializeField] private UIWindow window;

    [Header("Pages")]
    [SerializeField] private GameObject mainPage;
    [SerializeField] private GameObject confirmPage;

    [Header("Main page")]
    [SerializeField] private Button continueButton;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button loadButton;
    [SerializeField] private Button optionsButton;
    private Button journalButton;
    private Button coopButton;
    private TMP_Text coopLabel;
    private Button coopLeaveButton;
    private TMP_Text coopLeaveLabel;
    private Button askButton;
    private TMP_Text askLabel;
    private Button revokeButton;
    private VisitorAccess hookedAccess;
    [SerializeField] private Button quitButton;
    [Tooltip("Explains why Save or Load is unavailable, and reports how they went.")]
    [SerializeField] private TMP_Text statusLabel;

    [Header("Confirm page")]
    [SerializeField] private TMP_Text confirmMessage;
    [SerializeField] private Button confirmPrimaryButton;
    [SerializeField] private TMP_Text confirmPrimaryLabel;
    [SerializeField] private Button confirmSecondaryButton;
    [SerializeField] private TMP_Text confirmSecondaryLabel;
    [SerializeField] private Button confirmCancelButton;

    [Header("Scenes and content")]
    [Tooltip("Turns the ids in a save file back into items, resources and recipes.")]
    [SerializeField] private ContentDatabase content;
    [Tooltip("Loaded instead when the saved scene no longer exists.")]
    [SerializeField] private string fallbackScene = "SafeHaven";
    [SerializeField] private string mainMenuScene = "MainMenu";

    private Action confirmPrimary;
    private Action confirmSecondary;
    private bool leaving;
    private bool bound;

    private void OnEnable()
    {
        if (window == null || mainPage == null || confirmPage == null)
        {
            Debug.LogError("PauseMenu is missing its window or one of its pages.", this);
            enabled = false;
            return;
        }

        // System › Pause of the project-wide actions (GameInput), shared: subscribed while enabled.
        if (GameInput.Pause != null) GameInput.Pause.performed += OnEscape;
        if (GameInput.Back != null) GameInput.Back.performed += OnBack;
        window.OpenChanged += OnOpenChanged;
        continueButton.onClick.AddListener(Close);
        saveButton.onClick.AddListener(Save);
        loadButton.onClick.AddListener(ShowSaves);
        optionsButton.onClick.AddListener(ShowOptions);
        AddJournalButton();
        AddReportButton();
        AddCoopButton();
        HookAsks(CoopSession.Current != null ? CoopSession.Current.Access : null);
        quitButton.onClick.AddListener(AskQuit);
        confirmPrimaryButton.onClick.AddListener(OnConfirmPrimary);
        confirmSecondaryButton.onClick.AddListener(OnConfirmSecondary);
        confirmCancelButton.onClick.AddListener(ShowMain);
        bound = true;
    }

    private void OnDestroy()
    {
        SceneTransition.Abandoned -= OnLeavingAbandoned;
    }

    private void OnDisable()
    {
        // Never leave the game paused by a menu that is gone (for example when its scene unloads).
        GamePause.Release(this);
        HookAsks(null);
        if (!bound)
        {
            return;
        }

        bound = false;
        if (GameInput.Pause != null) GameInput.Pause.performed -= OnEscape;
        if (GameInput.Back != null) GameInput.Back.performed -= OnBack;
        window.OpenChanged -= OnOpenChanged;
        continueButton.onClick.RemoveListener(Close);
        saveButton.onClick.RemoveListener(Save);
        loadButton.onClick.RemoveListener(ShowSaves);
        optionsButton.onClick.RemoveListener(ShowOptions);
        quitButton.onClick.RemoveListener(AskQuit);
        confirmPrimaryButton.onClick.RemoveListener(OnConfirmPrimary);
        confirmSecondaryButton.onClick.RemoveListener(OnConfirmSecondary);
        confirmCancelButton.onClick.RemoveListener(ShowMain);
    }

    public bool IsOpen => window != null && window.IsOpen;

    public void Open()
    {
        if (!IsOpen && !leaving)
        {
            window.Open();
        }
    }

    public void Close()
    {
        window.Close();
    }

    private void OnEscape(InputAction.CallbackContext context)
    {
        // The screens over this menu answer Escape themselves, and the press that closes one isn't one for this menu. Held,
        // Escape skips a cinematic (CinematicScreen): the menu would open under it.
        if (leaving || SceneTransition.IsBusy || CinematicScreen.IsPlaying || OptionsScreen.HasEscape || SavesScreen.HasEscape)
        {
            return;
        }

        if (IsOpen)
        {
            Back();
        }
        else if (!BackHandlers.TryHandle())
        {
            Open();
        }
    }

    // A controller's B (System › Back): what Escape does, a page back or a window closed, but in play it never opens this
    // menu (B is also stop working and dodge's neighbour). Start opens it.
    private void OnBack(InputAction.CallbackContext context)
    {
        if (leaving || SceneTransition.IsBusy || CinematicScreen.IsPlaying || OptionsScreen.HasEscape || SavesScreen.HasEscape)
        {
            return;
        }

        if (IsOpen)
        {
            Back();
            return;
        }

        BackHandlers.TryHandle();
    }

    private void Back()
    {
        if (mainPage.activeSelf)
        {
            Close();
        }
        else
        {
            ShowMain();
        }
    }

    private void OnOpenChanged(bool open)
    {
        if (open)
        {
            GamePause.Request(this);
            statusLabel.text = "";
            ShowMain();
            // Resume is one press away: Continue is selected, so Enter, A or Start goes back to the game (§7 of the menu research).
            if (UnityEngine.EventSystems.EventSystem.current != null)
            {
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(continueButton.gameObject);
            }
        }
        else
        {
            GamePause.Release(this);
        }
    }

    // ----- Pages -----

    private void ShowMain()
    {
        ShowPage(mainPage);
        Refresh();
    }

    // Beside Options, in its look: from here a controller can reach it too, and anyone who hasn't learned J can find it.
    private void AddJournalButton()
    {
        if (journalButton != null || !JournalWindowExists)
        {
            return;
        }

        journalButton = Instantiate(optionsButton, optionsButton.transform.parent);
        journalButton.name = "Journal Button";
        journalButton.transform.SetSiblingIndex(optionsButton.transform.GetSiblingIndex());
        journalButton.onClick = new Button.ButtonClickedEvent();
        journalButton.onClick.AddListener(OpenJournal);
        TMPro.TMP_Text label = journalButton.GetComponentInChildren<TMPro.TMP_Text>();
        if (label != null)
        {
            label.text = "Journal";
        }
    }

    private static bool JournalWindowExists => Player.Current != null;

    private Button reportButton;

    // A playtest report (F8 too), under the Journal: a controller player has no F8.
    private void AddReportButton()
    {
        if (reportButton != null || Player.Current == null)
        {
            return;
        }

        reportButton = Instantiate(optionsButton, optionsButton.transform.parent);
        reportButton.name = "Report Button";
        reportButton.transform.SetSiblingIndex(optionsButton.transform.GetSiblingIndex());
        reportButton.onClick = new Button.ButtonClickedEvent();
        reportButton.onClick.AddListener(() =>
        {
            Close();
            PlaytestReportWindow.Begin();
        });
        TMPro.TMP_Text label = reportButton.GetComponentInChildren<TMPro.TMP_Text>();
        if (label != null)
        {
            label.text = "Report a Bug or Idea";
        }
    }

    // Co-op, beside the Journal: open this game to friends (then invite them, through Steam), and close it or leave a friend's.
    private void AddCoopButton()
    {
        if (coopButton != null || Player.Current == null || CoopSession.Current == null)
        {
            return;
        }

        coopButton = MenuButton("Coop Button", OnCoop, out coopLabel);
        askButton = MenuButton("Coop Ask Button", ShowAsk, out askLabel);
        revokeButton = MenuButton("Coop Revoke Button", AskRevoke, out TMP_Text revokeLabel);
        revokeLabel.text = "Take Back Permissions";
        coopLeaveButton = MenuButton("Coop Leave Button", LeaveCoop, out coopLeaveLabel);
    }

    private void HookAsks(VisitorAccess access)
    {
        if (hookedAccess != null)
        {
            hookedAccess.AsksChanged -= Refresh;
        }

        hookedAccess = access;
        if (hookedAccess != null)
        {
            hookedAccess.AsksChanged += Refresh;
        }
    }

    // A friend's question (to build, to use the stash), on the confirm page; answered one at a time, oldest first.
    private void ShowAsk()
    {
        VisitorAccess access = CoopSession.Current != null ? CoopSession.Current.Access : null;
        if (access == null || access.Asks.Count == 0)
        {
            ShowMain();
            return;
        }

        FriendAsk ask = access.Asks[0];
        string detail = ask.Right == VisitorRight.Build
            ? "They build with their own materials, and can also move, upgrade or take down what stands here."
            : "They can put things in and take things out.";
        Confirm($"{ask.Question}\n{detail} The answer holds while they stay.", "Allow", () => AnswerAsk(ask, true), "Refuse", () => AnswerAsk(ask, false));
    }

    private void AskRevoke()
    {
        Confirm("Take back what friends were allowed?\nThey can no longer build or use your stash, unless they ask again.", "Take Back", () =>
        {
            if (CoopSession.Current != null)
            {
                CoopSession.Current.Access.RevokeAll();
            }

            ShowMain();
        });
    }

    private void AnswerAsk(FriendAsk ask, bool yes)
    {
        if (CoopSession.Current != null)
        {
            CoopSession.Current.Access.Answer(ask, yes);
        }

        ShowAsk();
    }

    private Button MenuButton(string buttonName, UnityEngine.Events.UnityAction action, out TMP_Text label)
    {
        Button button = Instantiate(optionsButton, optionsButton.transform.parent);
        button.name = buttonName;
        button.transform.SetSiblingIndex(optionsButton.transform.GetSiblingIndex());
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(action);
        label = button.GetComponentInChildren<TMP_Text>();
        return button;
    }

    // Offline: open to friends. Hosting through Steam: invite them.
    private void OnCoop()
    {
        CoopSession coop = CoopSession.Current;
        if (coop == null)
        {
            return;
        }

        if (coop.IsHost)
        {
            if (!coop.InviteFriends())
            {
                statusLabel.text = $"<color={UIStyle.Current.DangerHex}>Steam's invite window isn't available yet.</color>";
            }
        }
        else if (coop.Host(out string problem))
        {
            statusLabel.text = coop.ViaSteam ? $"<color={UIStyle.Current.GoodHex}>Open to friends. Invite them, or they can join you from Steam.</color>"
                : $"<color={UIStyle.Current.GoodHex}>Open to friends on your network.</color>";
        }
        else
        {
            statusLabel.text = $"<color={UIStyle.Current.DangerHex}>{problem}</color>";
        }

        Refresh();
    }

    private void LeaveCoop()
    {
        if (CoopSession.Current != null)
        {
            CoopSession.Current.Leave();
        }

        statusLabel.text = "";
        Refresh();
    }

    private void RefreshCoop()
    {
        CoopSession coop = CoopSession.Current;
        if (coopButton == null || coop == null)
        {
            return;
        }

        // Sessions open and close at home: a raid is no place to start one.
        bool canChange = !leaving && !Place.OnRaid;
        bool invite = coop.IsHost && coop.ViaSteam;
        coopButton.gameObject.SetActive(!coop.IsActive || invite);
        coopButton.interactable = canChange || invite;
        coopLabel.text = invite ? "Invite Friends" : "Open to Friends";
        int asks = coop.IsHost ? coop.Access.Asks.Count : 0;
        askButton.gameObject.SetActive(asks > 0);
        askButton.interactable = !leaving;
        revokeButton.gameObject.SetActive(coop.IsHost && coop.Access.AnyAllowed);
        askLabel.text = asks == 1 ? "A Friend Asks" : $"Friends Ask ({asks})";
        coopLeaveButton.gameObject.SetActive(coop.IsActive);
        coopLeaveButton.interactable = !leaving;
        coopLeaveLabel.text = coop.IsHost ? "Close to Friends" : "Leave Session";
    }

    private void OpenJournal()
    {
        Close();
        JournalWindow.Create();
        if (UIStateManager.Current != null)
        {
            UIStateManager.Current.RequestToggle(UIWindowId.Journal);
        }
    }

    private void ShowOptions()
    {
        ShowPage(null);
        OptionsScreen.Open(ShowMain);
    }

    private void ShowSaves()
    {
        if (content == null)
        {
            Debug.LogError("PauseMenu needs a ContentDatabase to load saves.", this);
            return;
        }

        ShowPage(null);
        SavesScreen.Open(content, fallbackScene, ShowMain, BeginLeaving);
    }

    private void ShowPage(GameObject page)
    {
        mainPage.SetActive(page == mainPage);
        confirmPage.SetActive(page == confirmPage);
    }

    private void Refresh()
    {
        string saveProblem = SaveGameService.CheckPlayerSave();
        saveButton.interactable = !leaving && saveProblem == null;
        loadButton.interactable = !leaving && SaveGameService.HasSave && SaveGameService.CheckLoad() == null;
        continueButton.interactable = !leaving;
        optionsButton.interactable = !leaving;
        quitButton.interactable = !leaving;
        RefreshCoop();
        RefreshNow();

        if (string.IsNullOrEmpty(statusLabel.text))
        {
            statusLabel.text = saveProblem ?? DescribeLastSave();
        }
    }

    // ----- Now: what the player was doing, and the other windows a click away -----
    // UX pass 2026-11-28 (Docs/MenuUXResearch_2026-11-28.md §7, Hunt: Showdown's 2025 menu): beside the buttons, the quest on top of the
    // tracker with its next step, and picture tiles to the other windows with their keys, so the pause menu is a place to pick up from.

    private RectTransform nowCard;
    private TMP_Text nowGoal;
    private TMP_Text nowStep;
    private TMP_Text nowKicker;
    private readonly System.Collections.Generic.List<(KitPictureCard card, UIWindowId id, Func<bool> shown, InputAction key)> jumps =
        new System.Collections.Generic.List<(KitPictureCard, UIWindowId, Func<bool>, InputAction)>();

    private void BuildNow()
    {
        if (nowCard != null || !(mainPage.transform is RectTransform page) || Player.Current == null)
        {
            return;
        }

        UIStyle style = UIStyle.Current;
        const float width = 360f;
        nowCard = HearthKit.Card(page, "Now", 0f, 0f, width, 420f, 0.45f, style.Panel);
        // Beside the page's buttons, outside whatever lays them out.
        nowCard.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        nowCard.anchorMin = nowCard.anchorMax = new Vector2(1f, 1f);
        nowCard.pivot = new Vector2(0f, 1f);
        nowCard.anchoredPosition = new Vector2(48f, 0f);
        HearthKit.Ground(nowCard);
        nowKicker = CodeUI.Label("Kicker", nowCard, 20f, 18f, width - 40f, 16f, 12f, style.Accent);
        TMP_Text kicker = nowKicker;
        kicker.characterSpacing = 8f;
        nowGoal = CodeUI.Label("Goal", nowCard, 20f, 38f, width - 40f, 30f, 21f, style.Text);
        style.Title(nowGoal);
        nowGoal.enableAutoSizing = true;
        nowGoal.fontSizeMin = 15f;
        nowGoal.fontSizeMax = 21f;
        nowStep = CodeUI.Paragraph("Step", nowCard, 20f, 70f, width - 40f, 44f, 15f, style.Muted);
        nowStep.richText = true;
        HearthKit.Rule(nowCard, 20f, 122f, width - 40f);
        TMP_Text go = CodeUI.Label("Go", nowCard, 20f, 132f, width - 40f, 16f, 12f, style.Accent);
        go.text = "GO TO";
        go.characterSpacing = 8f;

        // The menu's box and the card centred as one pair (design review: the box stayed centred and the card hung off to the right).
        foreach (UIRole role in page.GetComponentsInParent<UIRole>(true))
        {
            if (role.Role == UIRole.Kind.Window && role.transform is RectTransform box && box != page)
            {
                box.anchoredPosition -= new Vector2((48f + width) * 0.5f, 0f);
                break;
            }
        }

        // Continue is the one to press: the primary look (design review: it looked like the other seven).
        if (continueButton.TryGetComponent(out UIRole continueRole))
        {
            continueRole.Set(UIRole.Kind.PrimaryButton);
        }

        Jump("Bag", "Bags", UIWindowId.Items, GameInput.Inventory, () => true);
        Jump("Character", "Heart", UIWindowId.Character, GameInput.Character, () => true);
        Jump("Skills", "Spark", UIWindowId.Skills, GameInput.Skills, () => true);
        Jump("Skill levels", "Stats", UIWindowId.Proficiencies, GameInput.Talents, () => true);
        Jump("Haven", "House", UIWindowId.Haven, GameInput.Haven, () => Place.AtHome);
        Jump("Journal", "Book", UIWindowId.Journal, GameInput.Journal, () => true);
    }

    private void Jump(string name, string icon, UIWindowId id, InputAction key, Func<bool> shown)
    {
        var card = new KitPictureCard(nowCard, name, 100f, 64f, 1, 14f, null, null);
        card.ShowPicture(null, icon);
        card.SetName(name);
        card.SetBadge(key != null ? GameInput.KeyLabel(key) : "");
        Button button = card.Root.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        // Lit when pointed at or selected on a pad (design review: the tiles gave no sign of either).
        UIHighlight.On(card.Root.gameObject);
        button.onClick.AddListener(() =>
        {
            Close();
            if (id == UIWindowId.Journal)
            {
                JournalWindow.Create();
            }

            UIStateManager.Current?.RequestToggle(id);
        });
        jumps.Add((card, id, shown, key));
    }

    private void RefreshNow()
    {
        BuildNow();
        if (nowCard == null)
        {
            return;
        }

        // The quest on top of the tracker (pinned, else by its order: the main story first), and its first open step.
        QuestLog.Entry top = null;
        string pinned = GameSession.Progress != null ? GameSession.Progress.Journal.PinnedQuest : null;
        foreach (QuestLog.Entry entry in GameSession.Quests.Entries)
        {
            if ((entry.Status == QuestStatus.Active || entry.Status == QuestStatus.ReadyToTurnIn)
                && (top == null || QuestTrackerView.InTrackerOrder(entry, top, pinned) < 0))
            {
                top = entry;
            }
        }

        // Where and when, then the quest: a raid's time left, or home (design review: the kicker named a place and showed a quest).
        nowKicker.text = RaidDirector.Current != null && RaidDirector.Current.Session != null
            ? $"ON A RAID  ·  {RaidStats.FormatTime(RaidDirector.Current.Session.TimeRemaining)} LEFT"
            : Place.OnRaid ? "ON A RAID" : HavenVisit.IsVisiting ? "IN A FRIEND'S HAVEN" : "AT HOME";
        nowGoal.text = top != null ? top.Quest.DisplayName : "No quest on hand";
        string step = "";
        if (top != null && top.Status == QuestStatus.ReadyToTurnIn)
        {
            step = $"Return to {top.Quest.TurnInTo}.";
        }
        else if (top != null)
        {
            for (int i = 0; i < top.Counts.Length && step.Length == 0; i++)
            {
                if (!top.IsObjectiveDone(i) && top.IsObjectiveOpen(i))
                {
                    QuestObjective objective = top.Quest.Objectives[i];
                    step = objective.Amount > 1 ? $"{objective.Describe()}  {top.Counts[i]}/{objective.Amount}" : objective.Describe();
                }
            }
        }

        nowStep.text = string.IsNullOrEmpty(step) ? GameInput.Keys("The Journal ({key:Journal}) holds what has been told.") : HearthKit.ToneNumbers(GameInput.Keys(step));
        int shown = 0;
        foreach ((KitPictureCard card, UIWindowId _, Func<bool> show, InputAction key) in jumps)
        {
            // The key as bound now; on a pad none (the pad cursor picks the tile): a rebind or a pad showed the old letter.
            card.SetBadge(key != null && !InputDevices.UsingGamepad ? GameInput.KeyLabel(key) : "");
            bool on = show();
            card.SetActive(on);
            if (on)
            {
                card.Place(20f + shown % 3 * 112f, 156f + shown / 3 * 108f);
                shown++;
            }
        }

        nowCard.sizeDelta = new Vector2(nowCard.sizeDelta.x, 156f + Mathf.CeilToInt(shown / 3f) * 108f + 8f);
    }

    private static string DescribeLastSave()
    {
        if (!SaveGameService.HasSave || !SaveGameService.TryReadSave(out SaveData save, out _))
        {
            return "No saved game yet.";
        }

        return DateTime.TryParse(save.savedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime saved)
            ? "Last saved " + saved.ToLocalTime().ToString("d MMM yyyy, HH:mm", CultureInfo.InvariantCulture)
            : "";
    }

    private void Confirm(string message, string primaryLabel, Action primary, string secondaryLabel = null, Action secondary = null)
    {
        confirmMessage.text = message;
        confirmPrimaryLabel.text = primaryLabel;
        confirmPrimary = primary;
        confirmSecondary = secondary;
        confirmSecondaryButton.gameObject.SetActive(secondary != null);
        if (secondary != null)
        {
            confirmSecondaryLabel.text = secondaryLabel;
        }

        ShowPage(confirmPage);
    }

    private void OnConfirmPrimary() => confirmPrimary?.Invoke();

    private void OnConfirmSecondary() => confirmSecondary?.Invoke();

    // ----- Actions -----

    private void Save()
    {
        string busy = SaveGameService.CheckPlayerSave();
        if (busy != null)
        {
            statusLabel.text = busy;
            return;
        }

        if (SaveGameService.TrySave(out string problem))
        {
            statusLabel.text = $"<color={UIStyle.Current.GoodHex}>Game saved.</color>";
            GameMessages.Post("Game saved.", MessageKind.Gain);
        }
        else
        {
            statusLabel.text = $"<color={UIStyle.Current.DangerHex}>{problem}</color>";
        }

        Refresh();
    }

    private void AskQuit()
    {
        Confirm(GameSession.ActiveRaid != null ? "Quit the game?\nYou leave this raid as if you had fallen: everything you carry is lost and your gear wears. You will wake at the portal."
            : "Quit the game?\nProgress since your last save will be lost.", "Main Menu", QuitToMainMenu, "Desktop", QuitToDesktop);
    }

    // A raid left by quitting ends as a fall, saved before the game goes (RaidDirector.Abandon).
    private static void AbandonRaid()
    {
        if (Place.InRaidScene)
        {
            RaidDirector.Current.Abandon();
        }
    }

    private void QuitToMainMenu()
    {
        AbandonRaid();
        // Straight to the menu: a guest's visit ends without the trip home (their journey is on disk as last saved).
        if (CoopSession.Current != null)
        {
            CoopSession.Current.Leave(false);
        }

        if (SceneLoader.Load(mainMenuScene, "Returning to the main menu"))
        {
            BeginLeaving();
        }
    }

    private static void QuitToDesktop()
    {
        AbandonRaid();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void BeginLeaving()
    {
        leaving = true;
        confirmPrimaryButton.interactable = false;
        confirmSecondaryButton.interactable = false;
        confirmCancelButton.interactable = false;
        SceneTransition.Abandoned -= OnLeavingAbandoned;
        SceneTransition.Abandoned += OnLeavingAbandoned;
        Refresh();
    }

    // The way out broke off and the game goes on here: the menu works again.
    private void OnLeavingAbandoned()
    {
        SceneTransition.Abandoned -= OnLeavingAbandoned;
        if (this == null)
        {
            return;
        }

        leaving = false;
        confirmPrimaryButton.interactable = true;
        confirmSecondaryButton.interactable = true;
        confirmCancelButton.interactable = true;
        Refresh();
    }
}
