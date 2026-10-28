using UnityEngine;

/// <summary>
/// A live picture of whoever is speaking, for the dialogue box: a small camera in front of their face (the humanoid head
/// bone, else the top of their model) renders into a texture while the conversation lasts. Nothing needs to be authored
/// for a character to get a portrait. The player's own portrait looks at their face the same way.
/// </summary>
public sealed class DialoguePortrait : MonoBehaviour
{
    private const int Size = 256;
    private const float FieldOfView = 24f;

    private Camera view;
    private Transform subject;
    private Transform head;
    // For creatures without a head bone: the middle of their front, and how far back to stand to see all of it.
    private Vector3 frontOffset;
    private float distance;

    public RenderTexture Texture { get; private set; }

    public static DialoguePortrait Create()
    {
        var go = new GameObject("Dialogue Portrait Camera");
        var portrait = go.AddComponent<DialoguePortrait>();
        portrait.Texture = new RenderTexture(Size, Size, 24) { name = "Dialogue Portrait" };
        portrait.view = go.AddComponent<Camera>();
        portrait.view.targetTexture = portrait.Texture;
        portrait.view.fieldOfView = FieldOfView;
        portrait.view.nearClipPlane = 0.05f;
        portrait.view.farClipPlane = 60f;
        portrait.view.clearFlags = CameraClearFlags.SolidColor;
        portrait.view.backgroundColor = new Color(0.12f, 0.1f, 0.08f, 1f);
        portrait.view.enabled = false;
        return portrait;
    }

    /// <summary>Looks at <paramref name="speaker"/>'s face from now on; null stops rendering.</summary>
    public void Show(Transform speaker)
    {
        subject = speaker;
        head = null;
        if (speaker != null)
        {
            Animator animator = speaker.GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman)
            {
                head = animator.GetBoneTransform(HumanBodyBones.Head);
            }

            if (head == null)
            {
                Bounds bounds = new Bounds(speaker.position, Vector3.zero);
                foreach (Renderer part in speaker.GetComponentsInChildren<Renderer>())
                {
                    if (!(part is ParticleSystemRenderer))
                    {
                        bounds.Encapsulate(part.bounds);
                    }
                }

                Vector3 forward = Flat(speaker.forward);
                Vector3 front = bounds.center + forward * (Vector3.Dot(bounds.extents, new Vector3(Mathf.Abs(forward.x), 0f, Mathf.Abs(forward.z))) * 0.6f)
                                + Vector3.up * bounds.extents.y * 0.3f;
                frontOffset = Quaternion.Inverse(speaker.rotation) * (front - speaker.position);
                distance = Mathf.Max(1.2f, bounds.extents.magnitude * 1.4f);
            }
        }

        view.enabled = speaker != null;
    }

    private void LateUpdate()
    {
        if (subject == null)
        {
            view.enabled = false;
            return;
        }

        Vector3 face = head != null ? head.position + Vector3.up * 0.05f : subject.position + subject.rotation * frontOffset;
        Vector3 forward = Flat(subject.forward);
        // A little to the side, as portraits are: the face in three-quarter view.
        Vector3 from = face + (Quaternion.Euler(0f, 25f, 0f) * forward) * (head != null ? 0.9f : distance) + Vector3.up * 0.05f;
        transform.SetPositionAndRotation(from, Quaternion.LookRotation(face - from));
    }

    private static Vector3 Flat(Vector3 direction)
    {
        direction.y = 0f;
        return direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward;
    }

    private void OnDestroy()
    {
        if (Texture != null)
        {
            Texture.Release();
            Destroy(Texture);
        }
    }
}
