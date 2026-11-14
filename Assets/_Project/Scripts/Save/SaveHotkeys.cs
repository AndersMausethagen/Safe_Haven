using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Quick save (F5) and quick load (F6) while playing. Put one in every gameplay scene. It only turns key presses into
/// SaveGameService calls and tells the player how it went; the rules (no saving or loading during raids) live in the service.
/// </summary>
[DisallowMultipleComponent]
public sealed class SaveHotkeys : MonoBehaviour
{
    [Tooltip("Turns the ids in a save file back into items, resources and recipes.")]
    [SerializeField] private ContentDatabase content;
    [Tooltip("Loaded instead when the saved scene no longer exists.")]
    [SerializeField] private string fallbackScene = "SafeHaven";

    // System › QuickSave and QuickLoad of the project-wide actions (GameInput), shared: subscribed while enabled.
    private void OnEnable()
    {
        if (GameInput.QuickSave != null) GameInput.QuickSave.performed += OnSave;
        if (GameInput.QuickLoad != null) GameInput.QuickLoad.performed += OnLoad;
    }

    private void OnDisable()
    {
        if (GameInput.QuickSave != null) GameInput.QuickSave.performed -= OnSave;
        if (GameInput.QuickLoad != null) GameInput.QuickLoad.performed -= OnLoad;
    }

    private void OnSave(InputAction.CallbackContext context)
    {
        // Mid-conversation a story has begun but not done what it is for, and a once-only one wouldn't start again after
        // loading. Not in CheckSave: the prologue's checkpoints are taken while people talk.
        if (DialogueBox.IsOpen || CinematicScreen.IsPlaying || InGameCinematic.IsPlaying)
        {
            GameMessages.Post("Finish the conversation first.", MessageKind.Warning);
            return;
        }

        if (SaveGameService.TrySave(out string problem))
        {
            GameMessages.Post($"Game saved: {SaveGameService.CurrentName}", MessageKind.Gain);
        }
        else
        {
            GameMessages.Post(problem, MessageKind.Warning);
        }
    }

    private void OnLoad(InputAction.CallbackContext context)
    {
        if (content == null)
        {
            Debug.LogError("SaveHotkeys needs a ContentDatabase to load saves.", this);
            return;
        }

        if (!SaveGameService.TryLoad(content, fallbackScene, out string problem))
        {
            GameMessages.Post(problem, MessageKind.Warning);
        }
    }
}
