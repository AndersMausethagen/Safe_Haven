using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ground that dies around an object: the grass within Radius turns dead brown as Amount rises (a cursed stone waking
/// in a story). Up to four spots show at once; the grass shader reads them (_SafeHavenWither). Story graphs add one
/// with the Wither Ground node; it can also be placed by hand. Not saved.
/// </summary>
[DisallowMultipleComponent]
[ExecuteAlways]
public sealed class WitherSpot : MonoBehaviour
{
    public const int Max = 4;
    private static readonly int SpotsId = Shader.PropertyToID("_SafeHavenWither");
    private static readonly int CountId = Shader.PropertyToID("_SafeHavenWitherCount");
    private static readonly List<WitherSpot> active = new List<WitherSpot>();
    private static readonly Vector4[] spots = new Vector4[Max];
    private static int lastFrame = -1;

    [SerializeField, Min(0.5f)] private float radius = 6f;
    [SerializeField, Range(0f, 1f)] private float amount = 1f;

    public float Radius
    {
        get => radius;
        set => radius = Mathf.Max(0.5f, value);
    }

    public float Amount
    {
        get => amount;
        set => amount = Mathf.Clamp01(value);
    }

    private void OnEnable() => active.Add(this);

    private void OnDisable()
    {
        active.Remove(this);
        Push();
    }

    private void LateUpdate()
    {
        if (lastFrame != Time.frameCount)
        {
            lastFrame = Time.frameCount;
            Push();
        }
    }

    private static void Push()
    {
        int count = Mathf.Min(Max, active.Count);
        for (int i = 0; i < count; i++)
        {
            Vector3 at = active[i].transform.position;
            spots[i] = new Vector4(at.x, at.z, active[i].radius, active[i].amount);
        }

        Shader.SetGlobalVectorArray(SpotsId, spots);
        Shader.SetGlobalFloat(CountId, count);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.45f, 0.3f, 0.1f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
