using UnityEngine;

/// <summary>
/// Base for game-wide tuning assets (survival rates, durability, the settlement's costs...). Each lives in a Resources
/// folder under its class name, e.g. Resources/SurvivalSettings, and systems read it through Current. When the asset is
/// missing, built-in defaults are used so the game still runs.
/// <para>
/// Designers edit these in the Systems Editor (Safe Haven > Systems Editor), which lists every settings asset with
/// derived numbers next to the fields. To add one: subclass GameSettings&lt;YourSettings&gt;, create the asset in
/// Assets/_Project/Resources and add a page for it in SystemsEditorPages.
/// </para>
/// </summary>
public abstract class GameSettings<T> : ScriptableObject where T : GameSettings<T>
{
    private static T current;

    /// <summary>The asset's name in a Resources folder.</summary>
    public static string ResourceName => typeof(T).Name;

    /// <summary>The settings in use: the Resources asset, or defaults when there is none.</summary>
    public static T Current
    {
        get
        {
            if (current == null)
            {
                current = Resources.Load<T>(ResourceName);
                if (current == null)
                {
                    current = CreateInstance<T>();
                    current.hideFlags = HideFlags.HideAndDontSave;
                }
            }

            return current;
        }
    }
}
