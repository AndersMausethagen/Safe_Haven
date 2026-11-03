using System;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// A second look for one scene, faded in and out as the story asks: walking out of the sunlit meadow into the raid's
/// forest. Each pair holds a volume of the scene's first look and its counterpart of the second. The sky rig goes on
/// driving the first volume of each pair (night, golden hour, overcast), and this splits that weight between the two by
/// how far the fade has come, so the second look follows the hour and the weather just as the first one does. It can also
/// fade to a second day and night profile (sun, ambient light, fog). Designers change it with the story's Change Look node
/// or the Change Look scene reaction; it isn't saved, so keep the reaction on the Quest Event to restore it after loading.
/// </summary>
[ExecuteAlways]
[DefaultExecutionOrder(100)] // after DayNightCycle and WeatherSystem, which set the first volumes' weights
[DisallowMultipleComponent]
[AddComponentMenu("Safe Haven/World/Scene Look")]
public sealed class SceneLook : MonoBehaviour
{
    [Serializable]
    public sealed class VolumePair
    {
        [Tooltip("The volume of the first look.")]
        public Volume first;
        [Tooltip("The same role in the second look. Give it a higher priority than the first look's volumes.")]
        public Volume second;
        [Tooltip("The sky rig sets the first volume's weight (night, golden hour, overcast). Off: a constant base look.")]
        public bool drivenBySky;
    }

    [SerializeField] private VolumePair[] volumes = new VolumePair[0];
    [SerializeField] private DayNightCycle dayNight;
    [Tooltip("The second look's day and night: sun, ambient light and fog. Empty: the cycle's own profile throughout.")]
    [SerializeField] private DayNightProfile secondDayNight;
    [Tooltip("0: the first look, 1: the second, at the start of the scene.")]
    [SerializeField, Range(0f, 1f)] private float startAmount;

    private float amount;
    private float target;
    private float speed;

    /// <summary>How far the second look has come in, 0 to 1.</summary>
    public float Amount => amount;

    private void OnEnable()
    {
        amount = target = startAmount;
        Apply();
    }

    private void OnValidate()
    {
        if (isActiveAndEnabled && !Application.isPlaying)
        {
            amount = target = startAmount;
        }
    }

    /// <summary>Fades to <paramref name="to"/> (0 first look, 1 second) over <paramref name="seconds"/>; 0 is at once.</summary>
    public void FadeTo(float to, float seconds)
    {
        target = Mathf.Clamp01(to);
        speed = seconds > 0f ? Mathf.Abs(target - amount) / seconds : 0f;
        if (seconds <= 0f)
        {
            amount = target;
        }
    }

    private void LateUpdate()
    {
        if (Application.isPlaying && !Mathf.Approximately(amount, target))
        {
            amount = Mathf.MoveTowards(amount, target, speed * Time.deltaTime);
        }

        Apply();
    }

    private void Apply()
    {
        foreach (VolumePair pair in volumes)
        {
            if (pair == null || pair.first == null || pair.second == null)
            {
                continue;
            }

            float weight = pair.drivenBySky ? pair.first.weight : 1f;
            pair.first.weight = weight * (1f - amount);
            pair.second.weight = weight * amount;
        }

        if (dayNight != null)
        {
            dayNight.BlendProfile(secondDayNight, amount);
        }
    }
}
