using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Quick save (F5) and quick load (F6) while playing. Put one in every gameplay scene. It only turns key presses into
/// SaveGameService calls and tells the player how it went; the rules (no saving during raids) live in the service.
/// </summary>
[DisallowMultipleComponent]
public sealed class SaveHotkeys : MonoBehaviour
{
    [Tooltip("Turns the ids in a save file back into items, resources and recipes.")]
    [SerializeField] private ContentDatabase content;
    [Tooltip("Loaded instead when the saved scene no longer exists.")]
    [SerializeField] private string fallbackScene = "SafeHaven";

    private InputAction saveAction;
    private InputAction loadAction;

    private void Awake()
    {
        saveAction = new InputAction("QuickSave", InputActionType.Button, "<Keyboard>/f5");
        loadAction = new InputAction("QuickLoad", InputActionType.Button, "<Keyboard>/f6");
        saveAction.performed += OnSave;
        loadAction.performed += OnLoad;
    }

    private void OnEnable()
    {
        saveAction.Enable();
        loadAction.Enable();
    }

    private void OnDisable()
    {
        saveAction.Disable();
        loadAction.Disable();
    }

    private void OnDestroy()
    {
        saveAction.Dispose();
        loadAction.Dispose();
    }

    private void OnSave(InputAction.CallbackContext context)
    {
        if (SaveGameService.TrySave(out string problem))
        {
            GameMessages.Post("Game saved.", MessageKind.Gain);
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
