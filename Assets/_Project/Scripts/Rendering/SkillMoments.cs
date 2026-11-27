using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// A skill's big moment (SkillMomentLook; the user, 2026-11-27: "make sure that ultimates look, feel and sound satisfying ... Give them
/// that AAA-feel"): the camera pushes in through the wind-up; at the impact the screen punches (an exposure flash, colour fringing, a lens
/// bulge, a tint) on a volume above every other, time slows for a beat after the hit stop, the camera recoils, and the world keeps the
/// skill's colour a while (an afterglow on a second volume). Honours the player's options: Screen Flashes scales the flash and tint,
/// Screen Shake the lens bulge and recoil, Hit Stop the slow time. One runner per scene, made on first use; real time throughout, so
/// slow motion doesn't stretch it.
/// </summary>
public sealed class SkillMoments : MonoBehaviour
{
    /// <summary>Above the raid's grade (RaidGradeVolume, 1000) and every scene volume.</summary>
    public const float Priority = 1100f;
    private const float PunchAttack = 0.035f;
    private const float RecoilSeconds = 0.3f;

    private static SkillMoments runner;

    private Volume punch;
    private Volume glow;
    private ChromaticAberration chroma;
    private LensDistortion lens;
    private ColorAdjustments punchColour;
    private ColorAdjustments glowColour;
    private static readonly LeaseKey ZoomKey = new LeaseKey("a skill's moment");

    private SkillMomentLook punchLook;
    private float punchStart = -1f;
    private SkillMomentLook glowLook;
    private float glowStart = -1f;
    private float zoomHeldUntil = -1f;
    private float recoilUntil = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => runner = null;

    private static SkillMoments Runner
    {
        get
        {
            if (runner == null)
            {
                runner = new GameObject("Skill Moments").AddComponent<SkillMoments>();
            }

            return runner;
        }
    }

    /// <summary>True while a punch or an afterglow is showing (for tests and probes).</summary>
    public static bool IsShowing => runner != null && (runner.punchStart >= 0f || runner.glowStart >= 0f);

    /// <summary>The wind-up: the camera pushes in over <paramref name="seconds"/> and holds until the impact.</summary>
    public static void WindUp(SkillMomentLook moment, float seconds)
    {
        if (moment == null || moment.windUpZoom >= 0.999f || PlayerCameraController.Current == null)
        {
            return;
        }

        SkillMoments self = Runner;
        PlayerCameraController.Current.SetCloseness(ZoomKey, moment.windUpZoom);
        // Let go by the impact; a moment that never lands (the cast broken off) lets go by itself.
        self.zoomHeldUntil = Time.unscaledTime + Mathf.Max(0.2f, seconds) + 0.6f;
    }

    /// <summary>The impact: the punch, slow time, the recoil and the afterglow (after Impact Delay for a skill with no blow).</summary>
    public static void Impact(SkillMomentLook moment)
    {
        if (moment == null || moment.IsEmpty || !Application.isPlaying)
        {
            return;
        }

        SkillMoments self = Runner;
        if (moment.impactDelay > 0f)
        {
            self.StartCoroutine(self.ImpactLater(moment, moment.impactDelay));
        }
        else
        {
            self.Begin(moment);
        }
    }

    private IEnumerator ImpactLater(SkillMomentLook moment, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        Begin(moment);
    }

    private void Begin(SkillMomentLook moment)
    {
        EnsureVolumes();
        if (moment.HasPunch)
        {
            punchLook = moment;
            punchStart = Time.unscaledTime;
        }

        if (moment.afterglowSeconds > 0f && moment.afterglow.a > 0f)
        {
            glowLook = moment;
            glowStart = Time.unscaledTime;
        }

        // Slow time after the hit stop, a beat to see it land (the Hit Stop option and co-op's shared time are honoured there).
        if (moment.slowScale < 0.999f && moment.slowSeconds > 0f && CombatFeedback.Current != null)
        {
            CombatFeedback.Current.SlowTimeAfterStop(moment.slowScale, moment.slowSeconds);
        }

        // The push lets go, and the camera recoils out a little from the blow.
        if (PlayerCameraController.Current != null)
        {
            float shake = GameOptions.ScreenShake.Value;
            PlayerCameraController.Current.SetCloseness(ZoomKey, moment.windUpZoom < 0.999f ? 1f + 0.08f * shake : 1f);
            recoilUntil = Time.unscaledTime + RecoilSeconds;
            zoomHeldUntil = -1f;
        }
    }

    private void EnsureVolumes()
    {
        if (punch != null)
        {
            return;
        }

        punch = MakeVolume("Skill Punch", Priority + 1f, out VolumeProfile punchProfile);
        chroma = punchProfile.Add<ChromaticAberration>();
        lens = punchProfile.Add<LensDistortion>();
        punchColour = punchProfile.Add<ColorAdjustments>();
        glow = MakeVolume("Skill Afterglow", Priority, out VolumeProfile glowProfile);
        glowColour = glowProfile.Add<ColorAdjustments>();
    }

    private Volume MakeVolume(string name, float priority, out VolumeProfile profile)
    {
        var holder = new GameObject(name);
        holder.transform.SetParent(transform, false);
        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = name + " (runtime)";
        profile.hideFlags = HideFlags.DontSave;
        var volume = holder.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = priority;
        volume.weight = 0f;
        volume.sharedProfile = profile;
        return volume;
    }

    private void Update()
    {
        float now = Time.unscaledTime;
        if (zoomHeldUntil >= 0f && now >= zoomHeldUntil)
        {
            zoomHeldUntil = -1f;
            LetCameraGo();
        }

        if (recoilUntil >= 0f && now >= recoilUntil)
        {
            recoilUntil = -1f;
            LetCameraGo();
        }

        if (punch != null)
        {
            UpdatePunch(now);
            UpdateGlow(now);
        }
    }

    // A fast rise and a falling-off tail: the blow's light, the fringe and the bulge all fade together.
    private void UpdatePunch(float now)
    {
        if (punchStart < 0f || punchLook == null)
        {
            punch.weight = 0f;
            return;
        }

        float t = now - punchStart;
        if (t >= punchLook.punchSeconds)
        {
            punchStart = -1f;
            punch.weight = 0f;
            return;
        }

        float rise = Mathf.Clamp01(t / PunchAttack);
        float fall = 1f - Mathf.Clamp01((t - PunchAttack) / Mathf.Max(0.05f, punchLook.punchSeconds - PunchAttack));
        float strength = t < PunchAttack ? rise : fall * fall;
        float flash = GameOptions.ScreenFlash.Value;
        float shake = GameOptions.ScreenShake.Value;
        punch.weight = 1f;
        chroma.intensity.Override(punchLook.chromatic * strength);
        lens.intensity.Override(punchLook.distortion * strength * shake);
        lens.scale.Override(1f);
        punchColour.postExposure.Override(punchLook.exposure * strength * flash);
        punchColour.colorFilter.Override(Color.Lerp(Color.white, punchLook.tint, strength * flash));
    }

    // The world keeps the skill's colour, fading out over Afterglow Seconds.
    private void UpdateGlow(float now)
    {
        if (glowStart < 0f || glowLook == null)
        {
            glow.weight = 0f;
            return;
        }

        float t = (now - glowStart) / Mathf.Max(0.1f, glowLook.afterglowSeconds);
        if (t >= 1f)
        {
            glowStart = -1f;
            glow.weight = 0f;
            return;
        }

        float ease = 1f - Mathf.SmoothStep(0f, 1f, t);
        glow.weight = glowLook.afterglowStrength * ease * Mathf.Lerp(0.5f, 1f, GameOptions.ScreenFlash.Value);
        glowColour.colorFilter.Override(glowLook.afterglow);
        glowColour.saturation.Override(glowLook.afterglowSaturation);
    }

    private void LetCameraGo()
    {
        if (PlayerCameraController.Current != null)
        {
            PlayerCameraController.Current.SetCloseness(ZoomKey, 1f);
        }
    }

    private void OnDestroy()
    {
        if (PlayerCameraController.Current != null)
        {
            PlayerCameraController.Current.SetCloseness(ZoomKey, 1f);
        }

        foreach (Volume volume in new[] { punch, glow })
        {
            if (volume != null && volume.sharedProfile != null)
            {
                Destroy(volume.sharedProfile);
            }
        }
    }
}
