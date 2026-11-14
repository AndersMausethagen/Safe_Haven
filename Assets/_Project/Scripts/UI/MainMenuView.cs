using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// The start menu: start a new game, carry on with the newest save (shown with what it is), pick another from the saved
/// games, change the options, watch a cinematic seen before again, or quit. It only reads saves to describe them; starting and loading go through
/// SaveGameService. A new game gets a save of its own, so it never touches the others.
/// </summary>
public sealed class MainMenuView : MonoBehaviour
{
    [Tooltip("Turns the ids in a save file back into items, resources and recipes.")]
    [SerializeField] private ContentDatabase content;
    [Tooltip("Where a new game starts.")]
    [SerializeField] private string newGameScene = "Prologue_MistyWoods";
    [Tooltip("Played when a new game starts, before its first scene: the backstory. Empty goes straight to the scene.")]
    [SerializeField] private CinematicDefinition openingCinematic;
    [Tooltip("Loads the first scene in the background while the cinematic plays, so the wait after it is short.")]
    [SerializeField] private bool loadDuringCinematic = true;
    [Tooltip("Where an existing save goes when its own scene no longer exists. Never restart its prologue.")]
    [SerializeField] private string fallbackLoadScene = "SafeHaven";
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button loadGameButton;
    [SerializeField] private Button exitButton;
    [Tooltip("Empty: made at start as a copy of the Exit button, which moves down to make room.")]
    [SerializeField] private Button savesButton;
    [Tooltip("Empty: made at start as a copy of the Exit button, which moves down to make room.")]
    [SerializeField] private Button optionsButton;
    [Tooltip("Opens the cinematics seen on this computer, to watch again. Empty: made at start as a copy of the Exit button.")]
    [SerializeField] private Button cinematicsButton;
    [Tooltip("How far the Exit button moves down for each button made.")]
    [SerializeField, Min(0f)] private float buttonSpacing = 72f;
    [Tooltip("Describes the save under the Load Game button.")]
    [SerializeField] private TMP_Text saveInfoLabel;
    [Tooltip("Reports problems, such as a damaged save.")]
    [SerializeField] private TMP_Text statusLabel;

    private SaveSlotInfo latest;

    private void Awake()
    {
        savesButton = savesButton != null ? savesButton : AddButton("Saved Games Button", "SAVED GAMES");
        optionsButton = optionsButton != null ? optionsButton : AddButton("Options Button", "OPTIONS");
        cinematicsButton = cinematicsButton != null ? cinematicsButton : AddButton("Cinematics Button", "CINEMATICS");
    }

    // A copy of the Exit button in its place; Exit moves down one step.
    private Button AddButton(string objectName, string text)
    {
        Button button = Instantiate(exitButton, exitButton.transform.parent);
        button.name = objectName;
        TMP_Text label = button.GetComponentInChildren<TMP_Text>();
        if (label != null)
        {
            label.text = text;
        }

        var exitRect = (RectTransform)exitButton.transform;
        ((RectTransform)button.transform).anchoredPosition = exitRect.anchoredPosition;
        exitRect.anchoredPosition += Vector2.down * buttonSpacing;
        return button;
    }

    private void OnEnable()
    {
        newGameButton.onClick.AddListener(NewGame);
        loadGameButton.onClick.AddListener(LoadGame);
        exitButton.onClick.AddListener(Exit);
        savesButton.onClick.AddListener(OpenSaves);
        optionsButton.onClick.AddListener(OpenOptions);
        cinematicsButton.onClick.AddListener(OpenCinematics);
    }

    private void OnDisable()
    {
        newGameButton.onClick.RemoveListener(NewGame);
        loadGameButton.onClick.RemoveListener(LoadGame);
        exitButton.onClick.RemoveListener(Exit);
        savesButton.onClick.RemoveListener(OpenSaves);
        optionsButton.onClick.RemoveListener(OpenOptions);
        cinematicsButton.onClick.RemoveListener(OpenCinematics);
    }

    private void Start()
    {
        statusLabel.text = "";
        RefreshSave();
    }

    private void RefreshSave()
    {
        savesButton.interactable = SaveGameService.HasSave;
        // Every save began with a new game, and so with the opening.
        if (SaveGameService.HasSave)
        {
            CinematicArchive.Remember(openingCinematic);
        }

        cinematicsButton.interactable = CinematicsScreen.HasAny;
        latest = SaveGameService.LatestSave();
        loadGameButton.interactable = latest != null;
        if (latest != null)
        {
            saveInfoLabel.text = !string.IsNullOrEmpty(latest.RaidCutShort) ? $"{latest.Name}: a raid into {latest.RaidCutShort} was cut short. Continue goes back to the portal."
                : $"{latest.Name}, last saved {latest.SavedAt.ToString("d MMM yyyy, HH:mm", CultureInfo.InvariantCulture)}";
        }
        else
        {
            saveInfoLabel.text = SaveGameService.HasSave
                ? $"<color={UIStyle.Current.DangerHex}>No save can be loaded. See Saved Games.</color>"
                : "No saved game yet. The prologue saves safe checkpoints; press F5 at home to save.";
        }
    }

    // A new game starts by making its character.
    private void NewGame() => CharacterCreationScreen.Open(StartNewGame);

    // The backstory first, when there is one; the new game itself begins as it ends or is skipped.
    private void StartNewGame(CharacterAppearance look)
    {
        if (openingCinematic == null)
        {
            BeginNewGame(look);
            return;
        }

        SetButtons(false);
        if (loadDuringCinematic)
        {
            SceneTransition.Preload(newGameScene);
        }

        CinematicScreen.Play(openingCinematic, () => BeginNewGame(look));
    }

    private void BeginNewGame(CharacterAppearance look)
    {
        bool started = SaveGameService.TryStartNewGame(newGameScene, look, out string problem);
        SetButtons(!started);
        if (!started)
        {
            statusLabel.text = problem;
        }
    }

    private void LoadGame()
    {
        if (latest == null)
        {
            return;
        }

        if (SaveGameService.TryLoad(latest.Slot, content, fallbackLoadScene, out string problem))
        {
            SetButtons(false);
        }
        else
        {
            statusLabel.text = problem;
            RefreshSave();
        }
    }

    private void OpenSaves() => SavesScreen.Open(content, fallbackLoadScene, RefreshSave, () => SetButtons(false));

    private static void OpenOptions() => OptionsScreen.Open();

    private void OpenCinematics() => CinematicsScreen.Open(RefreshSave);

    private static void Exit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void SetButtons(bool interactable)
    {
        newGameButton.interactable = interactable;
        loadGameButton.interactable = interactable;
        exitButton.interactable = interactable;
        savesButton.interactable = interactable;
        optionsButton.interactable = interactable;
        cinematicsButton.interactable = interactable && CinematicsScreen.HasAny;
    }
}
