using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// The start menu: start a new game, load the saved one (shown with when it was saved) or quit. It only reads the save
/// to describe it; starting and loading go through SaveGameService.
/// </summary>
public sealed class MainMenuView : MonoBehaviour
{
    [Tooltip("Turns the ids in a save file back into items, resources and recipes.")]
    [SerializeField] private ContentDatabase content;
    [Tooltip("Where a new game starts.")]
    [SerializeField] private string newGameScene = "Prologue_MistyWoods";
    [Tooltip("Where an existing save goes when its own scene no longer exists. Never restart its prologue.")]
    [SerializeField] private string fallbackLoadScene = "SafeHaven";
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button loadGameButton;
    [SerializeField] private Button exitButton;
    [Tooltip("Describes the save under the Load Game button.")]
    [SerializeField] private TMP_Text saveInfoLabel;
    [Tooltip("Reports problems, such as a damaged save.")]
    [SerializeField] private TMP_Text statusLabel;

    private void OnEnable()
    {
        newGameButton.onClick.AddListener(NewGame);
        loadGameButton.onClick.AddListener(LoadGame);
        exitButton.onClick.AddListener(Exit);
    }

    private void OnDisable()
    {
        newGameButton.onClick.RemoveListener(NewGame);
        loadGameButton.onClick.RemoveListener(LoadGame);
        exitButton.onClick.RemoveListener(Exit);
    }

    private void Start()
    {
        statusLabel.text = "";
        RefreshSave();
    }

    private void RefreshSave()
    {
        if (!SaveGameService.HasSave)
        {
            loadGameButton.interactable = false;
            saveInfoLabel.text = "No saved game yet. The prologue saves safe checkpoints; press F5 at home to save.";
            return;
        }

        bool readable = SaveGameService.TryReadSave(out SaveData save, out string problem);
        loadGameButton.interactable = readable;
        saveInfoLabel.text = readable ? $"Last saved {Describe(save.savedAtUtc)}" : $"<color=#FF7B6B>{problem}</color>";
    }

    private void NewGame()
    {
        if (SaveGameService.TryStartNewGame(newGameScene, out string problem))
        {
            SetButtons(false);
        }
        else
        {
            statusLabel.text = problem;
        }
    }

    private void LoadGame()
    {
        if (SaveGameService.TryLoad(content, fallbackLoadScene, out string problem))
        {
            SetButtons(false);
        }
        else
        {
            statusLabel.text = problem;
            RefreshSave();
        }
    }

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
    }

    private static string Describe(string savedAtUtc)
    {
        return DateTime.TryParse(savedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime saved)
            ? saved.ToLocalTime().ToString("d MMM yyyy, HH:mm", CultureInfo.InvariantCulture)
            : "at an unknown time";
    }
}
