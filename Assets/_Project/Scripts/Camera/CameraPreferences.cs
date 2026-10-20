using System;
using UnityEngine;

/// <summary>
/// The player's camera choices: the style toggled with the camera key, and an optional fixed style per situation.
/// They are preferences, so they live in PlayerPrefs rather than the save file and apply to every save.
/// </summary>
public static class CameraPreferences
{
    private const string StyleKey = "Camera.Style";
    private const string OverrideKey = "Camera.Override.";

    private static readonly CameraStyleOverride[] overrides = new CameraStyleOverride[CameraStyleRules.ContextCount];
    private static CameraStyle chosen;
    private static bool loaded;

    public static event Action Changed;

    public static CameraStyle Chosen
    {
        get
        {
            Load();
            return chosen;
        }
        set
        {
            Load();
            if (chosen == value)
            {
                return;
            }

            chosen = value;
            PlayerPrefs.SetInt(StyleKey, (int)value);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }

    public static CameraStyleOverride GetOverride(CameraContext context)
    {
        Load();
        return overrides[(int)context];
    }

    public static void SetOverride(CameraContext context, CameraStyleOverride value)
    {
        Load();
        if (overrides[(int)context] == value)
        {
            return;
        }

        overrides[(int)context] = value;
        PlayerPrefs.SetInt(OverrideKey + context, (int)value);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    public static CameraStyle Resolve(CameraSituation situation, out CameraContext? decidedBy)
    {
        Load();
        return CameraStyleRules.Resolve(chosen, overrides, situation, out decidedBy);
    }

    private static void Load()
    {
        if (loaded)
        {
            return;
        }

        loaded = true;
        chosen = PlayerPrefs.GetInt(StyleKey, 0) == (int)CameraStyle.Action ? CameraStyle.Action : CameraStyle.TopDown;
        for (int i = 0; i < overrides.Length; i++)
        {
            int value = PlayerPrefs.GetInt(OverrideKey + (CameraContext)i, 0);
            overrides[i] = value >= 0 && value <= (int)CameraStyleOverride.Action ? (CameraStyleOverride)value : CameraStyleOverride.PlayerChoice;
        }
    }
}
