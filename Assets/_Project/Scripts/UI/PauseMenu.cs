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
        window.OpenChanged += OnOpenChanged;
        continueButton.onClick.AddListener(Close);
        saveButton.onClick.AddListener(Save);
        loadButton.onClick.AddListener(ShowSaves);
        optionsButton.onClick.AddListener(ShowOptions);
        AddJournalButton();
        AddCoopButton();
        HookAsks(CoopSession.Current != null ? CoopSession.Current.Access : null);
        quitButton.onClick.AddListener(AskQuit);
        confirmPrimaryButton.onClick.AddListener(OnConfirmPrimary);
        confirmSecondaryButton.onClick.AddListener(OnConfirmSecondary);
        confirmCancelButton.onClick.AddListener(ShowMain);
        bound = true;
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
        // The screens over this menu answer Escape themselves, and the press that closes one isn't one for this menu.
        if (leaving || SceneTransition.IsBusy || OptionsScreen.HasEscape || SavesScreen.HasEscape)
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

    // Co-op, beside the Journal: open this game to friends (then invite them, through Steam), and close it or leave a friend's.
    private void AddCoopButton()
    {
        if (coopButton != null || Player.Current == null || CoopSession.Current == null)
        {
            return;
        }

        coopButton = MenuButton("Coop Button", OnCoop, out coopLabel);
        askButton = MenuButton("Coop Ask Button", ShowAsk, out askLabel);
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
                statusLabel.text = "<color=#FF7B6B>Steam's invite window isn't available yet.</color>";
            }
        }
        else if (coop.Host(out string problem))
        {
            statusLabel.text = coop.ViaSteam ? "<color=#9BE37B>Open to friends. Invite them, or they can join you from Steam.</color>"
                : "<color=#9BE37B>Open to friends on your network.</color>";
        }
        else
        {
            statusLabel.text = $"<color=#FF7B6B>{problem}</color>";
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
        bool canChange = !leaving && GameSession.ActiveRaid == null;
        bool invite = coop.IsHost && coop.ViaSteam;
        coopButton.gameObject.SetActive(!coop.IsActive || invite);
        coopButton.interactable = canChange || invite;
        coopLabel.text = invite ? "Invite Friends" : "Open to Friends";
        int asks = coop.IsHost ? coop.Access.Asks.Count : 0;
        askButton.gameObject.SetActive(asks > 0);
        askButton.interactable = !leaving;
        askLabel.text = asks == 1 ? "A Friend Asks" : $"Friends Ask ({asks})";
        coopLeaveButton.gameObject.SetActive(coop.IsActive);
        coopLeaveButton.interactable = !leaving;
        coopLeaveLabel.text = coop.IsHost ? "Close to Friends" : "Leave Session";
    }

    private void OpenJournal()
    {
        Close();
        JournalWindow.Create();
        if (UIStateManager.Instance != null)
        {
            UIStateManager.Instance.RequestToggle(UIWindowId.Journal);
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
        string saveProblem = SaveGameService.CheckSave();
        saveButton.interactable = !leaving && saveProblem == null;
        loadButton.interactable = !leaving && SaveGameService.HasSave && SaveGameService.CheckLoad() == null;
        continueButton.interactable = !leaving;
        optionsButton.interactable = !leaving;
        quitButton.interactable = !leaving;
        RefreshCoop();

        if (string.IsNullOrEmpty(statusLabel.text))
        {
            statusLabel.text = saveProblem ?? DescribeLastSave();
        }
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
        if (SaveGameService.TrySave(out string problem))
        {
            statusLabel.text = "<color=#9BE37B>Game saved.</color>";
            GameMessages.Post("Game saved.", MessageKind.Gain);
        }
        else
        {
            statusLabel.text = $"<color=#FF7B6B>{problem}</color>";
        }

        Refresh();
    }

    private void AskQuit()
    {
        Confirm(GameSession.ActiveRaid != null ? "Quit the game?\nThis raid ends here, and nothing from it comes home. You will be back at the portal as you set out."
            : "Quit the game?\nProgress since your last save will be lost.", "Main Menu", QuitToMainMenu, "Desktop", QuitToDesktop);
    }

    private void QuitToMainMenu()
    {
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
        Refresh();
    }
}
