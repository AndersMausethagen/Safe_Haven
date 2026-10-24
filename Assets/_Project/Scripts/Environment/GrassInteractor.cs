using System.Collections.Generic;
using UnityEngine;

/// <summary>Makes "Safe Haven/Grass" (terrain grass and bushes) bend away from this object: the player, monsters, a
/// rolling log... Up to <see cref="Max"/> at a time, the ones nearest the main camera when there are more. Positions
/// go to the shader as globals once per frame (_GrassInteractors = position + radius, _GrassInteractorCount).</summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class GrassInteractor : MonoBehaviour
{
    public const int Max = 8;

    [Tooltip("Metres around the point in which grass bends away (the shader's Push Away and Flatten set how far).")]
    [SerializeField, Min(0.05f)] private float radius = 0.6f;
    [Tooltip("Point that pushes the grass, relative to this object (e.g. the feet).")]
    [SerializeField] private Vector3 offset = Vector3.zero;

    private static readonly List<GrassInteractor> Active = new List<GrassInteractor>();
    private static readonly Vector4[] Data = new Vector4[Max];
    private static readonly int InteractorsId = Shader.PropertyToID("_GrassInteractors");
    private static readonly int CountId = Shader.PropertyToID("_GrassInteractorCount");
    private static int uploadedFrame = -1;

    public float Radius
    {
        get => radius;
        set => radius = Mathf.Max(0.05f, value);
    }

    public Vector3 Point => transform.TransformPoint(offset);

    /// <summary>Interactors currently sent to the shader (after <see cref="Upload"/>).</summary>
    public static int UploadedCount { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Active.Clear();
        uploadedFrame = -1;
        UploadedCount = 0;
        Shader.SetGlobalFloat(CountId, 0f);
    }

    private void OnEnable()
    {
        if (!Active.Contains(this))
        {
            Active.Add(this);
        }

        Upload();
    }

    private void OnDisable()
    {
        Active.Remove(this);
        Upload();
    }

    private void LateUpdate()
    {
        if (uploadedFrame != Time.frameCount)
        {
            uploadedFrame = Time.frameCount;
            Upload();
        }
    }

    /// <summary>Sends the active interactors to the grass shader; returns how many were sent.</summary>
    public static int Upload()
    {
        Active.RemoveAll(interactor => interactor == null);
        if (Active.Count > Max)
        {
            Camera camera = Camera.main;
            Vector3 eye = camera != null ? camera.transform.position : Vector3.zero;
            Active.Sort((a, b) => (a.Point - eye).sqrMagnitude.CompareTo((b.Point - eye).sqrMagnitude));
        }

        int count = Mathf.Min(Active.Count, Max);
        for (int i = 0; i < Max; i++)
        {
            Data[i] = i < count ? (Vector4)Active[i].Point : Vector4.zero;
            Data[i].w = i < count ? Active[i].radius : 0f;
        }

        Shader.SetGlobalVectorArray(InteractorsId, Data);
        Shader.SetGlobalFloat(CountId, count);
        UploadedCount = count;
        return count;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 0.9f, 0.3f, 0.6f);
        Gizmos.DrawWireSphere(Point, radius);
    }
}
