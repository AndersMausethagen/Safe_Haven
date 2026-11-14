using UnityEditor;
using UnityEngine;

/// <summary>
/// The bulk builders are retired (the user, 2026-11-14). Each one rebuilt or restyled a whole section from its own stored
/// copy, so running it again silently put back older work over anything changed since (the prologue's blight boar, key
/// prompts, quest texts, hand-placed props). The scenes and assets are the source of truth now: changes are small, targeted
/// edits checked with a before/after diff, and earlier states come back from git. AgentScripts/RETIRED.md has the list.
/// A retired menu entry calls <see cref="Refuse"/> and does nothing else. It says so in the console and the Scene view,
/// without a modal dialog (a dialog would block the Editor for command-line tools).
/// </summary>
public static class RetiredBuilders
{
    public static bool Refuse(string what)
    {
        string message = $"\"{what}\" is retired: it rebuilt its section from a stored copy and could overwrite newer work. " +
                         "Make a targeted edit instead, and restore earlier states from git (AgentScripts/RETIRED.md).";
        Debug.LogWarning(message);
        SceneView view = SceneView.lastActiveSceneView;
        if (view != null)
        {
            view.ShowNotification(new GUIContent($"\"{what}\" is retired (see the console)"), 4f);
        }

        return true;
    }
}
