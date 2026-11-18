using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Puts the player's settings to work: when the game starts, on every scene change, and at once when one changes. Most
/// settings are read where they matter; this does what has to be pushed: graphics, the window, UI size, and the look
/// volume, an override on top of each scene's own that turns effects off and sets the brightness without touching the
/// scenes' profiles. Also keeps a chosen Preset and its members in step. A persistent system started by GameSystems.
/// </summary>
public sealed class OptionsSystem : MonoBehaviour, IGameSystem
{
    // Above every scene volume, so an effect turned off stays off.
    private const float LookVolumePriority = 1000f;

    private VolumeProfile lookProfile;
    private Bloom bloom;
    private MotionBlur motionBlur;
    private FilmGrain grain;
    private LiftGammaGain gamma;
    private bool applyingPreset;
    private bool echoingPreset;

    /// <summary>Called once by GameSystems when the game starts.</summary>
    public void Initialise()
    {
        InputRebinds.Load();
        BuildLookVolume();
        DisplayOptions.Apply();
        GraphicsOptions.Apply();
        ApplyLook();
        GroundMarks.SetHighContrast(GameOptions.HighContrastWarnings.On);
        GameOption.AnyChanged += OnChanged;
        Player.Spawned += ApplyAssists;
    }

    private void OnDestroy()
    {
        GameOption.AnyChanged -= OnChanged;
        Player.Spawned -= ApplyAssists;
        if (lookProfile != null)
        {
            Destroy(lookProfile);
        }
    }

    public void OnSceneEntered(Scene scene)
    {
        GraphicsOptions.ForgetUnloadedTerrains();
        GraphicsOptions.ApplyToScene();
    }

    private void OnApplicationFocus(bool focused)
    {
        AudioListener.volume = !focused && GameOptions.MuteUnfocused.On ? 0f : 1f;
    }

    private static void ApplyAssists(Player player) => player.Health.DamageTakenMultiplier = GameOptions.DamageTakenShare;

    private void OnChanged(GameOption option)
    {
        // A preset sets its members and is applied once (SetPreset); a member changed by hand moves the preset to the one it now
        // matches without that applying again. Each member set by a preset ran GraphicsOptions.Apply (about ten times, each with
        // scene searches, terrain quality and a shadow map change).
        if (option == GameOptions.Preset)
        {
            if (!echoingPreset)
            {
                SetPreset(option.Index);
            }
        }
        else if (IsMember(option) && !applyingPreset)
        {
            echoingPreset = true;
            try
            {
                GameOptions.Preset.Value = GameOptions.MatchingPreset();
            }
            finally
            {
                echoingPreset = false;
            }
        }

        if (option == GameOptions.DamageTaken && Player.Current != null)
        {
            ApplyAssists(Player.Current);
        }
        else if (option == GameOptions.UiScale)
        {
            UIRoot.ApplyScaleEverywhere();
        }
        else if (option == GameOptions.HighContrastWarnings)
        {
            GroundMarks.SetHighContrast(option.On);
        }
        else if (option == GameOptions.VSync || option == GameOptions.FrameLimit)
        {
            DisplayOptions.ApplyPacing();
        }
        else if (option == GameOptions.Brightness || option == GameOptions.Bloom || option == GameOptions.MotionBlur || option == GameOptions.FilmGrain)
        {
            ApplyLook();
        }
        else if ((IsMember(option) && !applyingPreset) || option == GameOptions.Upscaling)
        {
            GraphicsOptions.Apply();
        }
    }

    private void SetPreset(int preset)
    {
        if (preset == GameOptions.PresetCustom || applyingPreset)
        {
            return;
        }

        applyingPreset = true;
        GameOptions.ApplyPreset(preset);
        applyingPreset = false;
        GraphicsOptions.Apply();
    }

    private static bool IsMember(GameOption option)
    {
        foreach (GameOption member in GameOptions.PresetMembers)
        {
            if (member == option)
            {
                return true;
            }
        }

        return false;
    }

    private void BuildLookVolume()
    {
        var holder = new GameObject("Look Volume");
        holder.transform.SetParent(transform, false);
        var volume = holder.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = LookVolumePriority;
        lookProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        lookProfile.hideFlags = HideFlags.HideAndDontSave;
        volume.sharedProfile = lookProfile;

        bloom = lookProfile.Add<Bloom>();
        bloom.intensity.Override(0f);
        motionBlur = lookProfile.Add<MotionBlur>();
        motionBlur.intensity.Override(0f);
        grain = lookProfile.Add<FilmGrain>();
        grain.intensity.Override(0f);
        // Scene profiles leave Lift Gamma Gain alone, so this adds to their grading instead of replacing it.
        gamma = lookProfile.Add<LiftGammaGain>();
        gamma.gamma.Override(new Vector4(1f, 1f, 1f, 0f));
    }

    // An effect that is on is simply left to the scene: its override here is switched off.
    private void ApplyLook()
    {
        bloom.active = !GameOptions.Bloom.On;
        motionBlur.active = !GameOptions.MotionBlur.On;
        grain.active = !GameOptions.FilmGrain.On;
        float brightness = GameOptions.Brightness.Value;
        gamma.active = !Mathf.Approximately(brightness, 0f);
        gamma.gamma.Override(new Vector4(1f, 1f, 1f, brightness));
    }
}
