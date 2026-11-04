using System;
using UnityEngine;

/// <summary>
/// How scene changes look: fade times, how long the loading screen stays up at least, and what it shows. Lives at
/// Resources/SceneTransitionSettings and is read by SceneTransition; built-in defaults apply when the asset is missing.
/// </summary>
[CreateAssetMenu(menuName = "Safe Haven/Settings/Scene Transition Settings", fileName = "SceneTransitionSettings")]
public sealed class SceneTransitionSettings : GameSettings<SceneTransitionSettings>
{
    /// <summary>What the loading screen shows on the way to one scene. What is left empty comes from the general art and tips.</summary>
    [Serializable]
    public sealed class Destination
    {
        [Tooltip("The scene's name, as in the build settings.")]
        public string scene;
        public Sprite art;
        [TextArea] public string[] tips;
    }

    [Tooltip("Seconds to fade to the loading screen before the old scene goes.")]
    [SerializeField, Min(0f)] private float fadeOutSeconds = 0.35f;
    [Tooltip("Seconds to fade from the loading screen into the new scene once it is set up.")]
    [SerializeField, Min(0f)] private float fadeInSeconds = 0.6f;
    [Tooltip("The loading screen stays at least this long, so a fast load doesn't flash.")]
    [SerializeField, Min(0f)] private float minimumLoadingSeconds = 0.75f;
    [SerializeField] private Color backgroundColor = new Color(0.04f, 0.05f, 0.06f, 1f);
    [Tooltip("Optional artwork behind the loading bar. Fills the screen, cropped to fit.")]
    [SerializeField] private Sprite backgroundArt;
    [SerializeField] private Color accentColor = new Color(0.62f, 0.86f, 0.62f, 1f);
    [Tooltip("One is picked at random for each loading screen. Keys written as [E] or (N) follow a rebind.")]
    [SerializeField, TextArea] private string[] tips =
    {
        "Whatever you carry out of a raid is yours. Whatever you carry when it collapses is not.",
        "Your safe pocket keeps what's in it, even when a raid goes wrong.",
        "Raid modifiers are crafted at home. Bring one to change what waits on the other side.",
        "The woods shift between visits. Caches and creatures are rarely where you left them.",
    };
    [Tooltip("Art and tips for particular scenes, so the screen says something about where you are going.")]
    [SerializeField] private Destination[] destinations;

    public float FadeOutSeconds => fadeOutSeconds;
    public float FadeInSeconds => fadeInSeconds;
    public float MinimumLoadingSeconds => minimumLoadingSeconds;
    public Color BackgroundColor => backgroundColor;
    public Sprite BackgroundArt => backgroundArt;
    public Color AccentColor => accentColor;
    public string[] Tips => tips;

    public Sprite ArtFor(string scene)
    {
        Destination destination = Find(scene);
        return destination != null && destination.art != null ? destination.art : backgroundArt;
    }

    public string[] TipsFor(string scene)
    {
        Destination destination = Find(scene);
        return destination != null && destination.tips != null && destination.tips.Length > 0 ? destination.tips : tips;
    }

    private Destination Find(string scene)
    {
        if (destinations == null || string.IsNullOrEmpty(scene))
        {
            return null;
        }

        foreach (Destination destination in destinations)
        {
            if (destination != null && destination.scene == scene)
            {
                return destination;
            }
        }

        return null;
    }
}
