using UnityEngine;

/// <summary>
/// Compiles the game's shaders behind the first loading screen instead of the first time each is seen, which hitched
/// in play (the first rain, fire, night or raid). Warms RenderingSettings › Warmup Variants, recorded by playing through
/// those moments (AgentScripts/RecordShaderWarmup.cs), a few variants a frame so the loading screen keeps moving.
/// SceneTransition holds its screen until it is done, once a session, for at most Most Seconds. Builds only: the Editor
/// compiles shaders its own way.
/// </summary>
public static class ShaderWarmup
{
    private const int PerFrame = 24;
    private static bool finished;
    private static float startedAt = -1f;

    /// <summary>Nothing (more) to warm: done, nothing recorded, or in the Editor.</summary>
    public static bool Done
    {
        get
        {
            if (finished)
            {
                return true;
            }

            RenderingSettings settings = RenderingSettings.Current;
            if (Application.isEditor || settings == null || settings.WarmupVariants == null)
            {
                finished = true;
            }

            return finished;
        }
    }

    /// <summary>One frame's share. Gives up after Most Seconds, so a huge collection never keeps the player out.</summary>
    public static void Step()
    {
        if (Done)
        {
            return;
        }

        RenderingSettings settings = RenderingSettings.Current;
        if (startedAt < 0f)
        {
            startedAt = Time.realtimeSinceStartup;
        }

        finished = settings.WarmupVariants.WarmUpProgressively(PerFrame)
            || Time.realtimeSinceStartup - startedAt > settings.WarmupMostSeconds;
        if (finished)
        {
            Debug.Log($"[ShaderWarmup] {settings.WarmupVariants.variantCount} variants warmed in {Time.realtimeSinceStartup - startedAt:0.0} s");
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        finished = false;
        startedAt = -1f;
    }
}
