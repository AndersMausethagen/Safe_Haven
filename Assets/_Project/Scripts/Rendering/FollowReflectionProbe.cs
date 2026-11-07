using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// One realtime reflection probe above the player, re-rendered when they have moved on or the light has changed (a face a
/// frame), so reflections show the clearing, the trees and the sky as they are now. Its box is far larger than anything
/// near, so it stands in for the sky everywhere and needs no box projection. Made by WorldRendering.
/// </summary>
[RequireComponent(typeof(ReflectionProbe))]
public sealed class FollowReflectionProbe : MonoBehaviour
{
    private RenderingSettings settings;
    private ReflectionProbe probe;
    private Vector3 renderedAt = new Vector3(float.MaxValue, 0f, 0f);
    private float renderedHour = -100f;
    private float nextRender;
    private int rendering = -1;

    public void Initialise(RenderingSettings rules)
    {
        settings = rules;
        probe = GetComponent<ReflectionProbe>();
        probe.mode = ReflectionProbeMode.Realtime;
        probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
        probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
        probe.resolution = settings.ReflectionResolution;
        probe.hdr = true;
        probe.size = new Vector3(2000f, 600f, 2000f);
        probe.boxProjection = false;
        probe.blendDistance = 0f;
        probe.importance = 0;
        probe.nearClipPlane = 1f;
        probe.farClipPlane = 160f;
        probe.cullingMask = settings.ReflectionLayers;
        probe.clearFlags = ReflectionProbeClearFlags.Skybox;
        probe.shadowDistance = 20f;
    }

    /// <summary>Renders again on the next frame (a new scene, a teleport).</summary>
    public void Refresh()
    {
        renderedAt = new Vector3(float.MaxValue, 0f, 0f);
        nextRender = 0f;
    }

    private void LateUpdate()
    {
        Vector3 at = Where();
        float hour = AtmosphereConditions.Hour;
        transform.position = rendering >= 0 ? transform.position : at;
        if (rendering >= 0 && !probe.IsFinishedRendering(rendering))
        {
            return;
        }

        rendering = -1;
        float hours = Mathf.Abs(Mathf.DeltaAngle(hour * 15f, renderedHour * 15f)) / 15f;
        bool moved = (at - renderedAt).sqrMagnitude > settings.ReflectionMove * settings.ReflectionMove;
        if (Time.unscaledTime < nextRender || (!moved && hours < settings.ReflectionHours))
        {
            return;
        }

        transform.position = at;
        renderedAt = at;
        renderedHour = hour;
        nextRender = Time.unscaledTime + settings.ReflectionInterval;
        rendering = probe.RenderProbe();
    }

    private Vector3 Where()
    {
        Transform target = Player.Current != null ? Player.Current.transform : Camera.main != null ? Camera.main.transform : null;
        return target != null ? target.position + Vector3.up * settings.ReflectionHeight : transform.position;
    }
}
