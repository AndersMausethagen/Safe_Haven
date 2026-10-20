using UnityEngine;

/// <summary>One shared wind source per scene for Safe Haven vegetation shaders. Does not move terrain or physics objects.</summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class MossWind : MonoBehaviour
{
    [Tooltip("Horizontal direction in degrees: 0 is world +Z, 90 is world +X.")]
    [SerializeField, Range(0f, 360f)] private float direction = 35f;
    [Tooltip("Maximum sideways tip displacement in metres before the material's wind multiplier.")]
    [SerializeField, Range(0f, 0.2f)] private float strength = 0.045f;
    [SerializeField, Min(0f)] private float speed = 1.2f;
    [SerializeField, Range(0f, 1f)] private float gusts = 0.35f;

    private static MossWind active;
    private static readonly int WindId = Shader.PropertyToID("_SafeHavenWind");
    private static readonly int GustId = Shader.PropertyToID("_SafeHavenWindGusts");

    private void OnEnable()
    {
        if (active != null && active != this)
        {
#if UNITY_EDITOR
            // Scenes opened read-only by editor tools (e.g. the Sound Editor) are only flagged as preview
            // scenes after OnEnable, so decide one editor tick later.
            if (!Application.isPlaying)
            {
                UnityEditor.EditorApplication.delayCall += ReportDuplicate;
                return;
            }
#endif
            ReportDuplicate();
            return;
        }

        active = this;
        Publish();
    }

    private void Update()
    {
        if (active == this)
        {
            Publish();
        }
    }

    private void OnDisable()
    {
        if (active == this)
        {
            Shader.SetGlobalVector(WindId, Vector4.zero);
            Shader.SetGlobalFloat(GustId, 0f);
            active = null;
        }
    }

    private void ReportDuplicate()
    {
        if (this == null || !isActiveAndEnabled || active == null || active == this)
        {
            return;
        }
#if UNITY_EDITOR
        if (UnityEditor.SceneManagement.EditorSceneManager.IsPreviewSceneObject(this))
        {
            return;
        }
#endif
        Debug.LogError("Only one enabled Moss Wind may drive the shared vegetation wind. Disable the extra source.", this);
    }

    private void Publish()
    {
        float angle = direction * Mathf.Deg2Rad;
        Shader.SetGlobalVector(WindId, new Vector4(Mathf.Sin(angle), Mathf.Cos(angle), strength, speed));
        Shader.SetGlobalFloat(GustId, gusts);
    }
}
