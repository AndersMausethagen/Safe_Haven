using UnityEngine;

/// <summary>
/// Makes the bow in the player's hand look drawn: a string between its tips that the right hand pulls back while
/// drawing and that snaps forward on the shot, and an arrow lying on the string while it is drawn. It reads the held
/// model HeldItemView shows; bows with the archer pack's rig (bones B-bowTip01, B-bowTip02, B-nockPoint) get a string,
/// other models are left as they are.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerArchery), typeof(HeldItemView))]
public sealed class HeldBowView : MonoBehaviour
{
    private const string TipA = "B-bowTip01";
    private const string TipB = "B-bowTip02";
    private const string Nock = "B-nockPoint";
    private const string StringName = "Bowstring";

    [SerializeField] private Material stringMaterial;
    [SerializeField, Min(0.001f)] private float stringWidth = 0.008f;
    [Tooltip("Model of the arrow on the string while drawing: nock at its pivot, tip along +Z. Optional.")]
    [SerializeField] private GameObject nockedArrow;
    [Tooltip("How quickly the string follows the hand back while drawing (per second).")]
    [SerializeField, Min(0.1f)] private float pullSpeed = 5f;

    private PlayerArchery archery;
    private HeldItemView held;
    private Transform rightHand;
    private Transform leftHand;
    private GameObject model;
    private Transform tipA;
    private Transform tipB;
    private Transform nock;
    private LineRenderer line;
    private Transform arrow;
    private float pull;

    private void Awake()
    {
        archery = GetComponent<PlayerArchery>();
        held = GetComponent<HeldItemView>();
        Animator animator = GetComponentInChildren<Animator>();
        if (animator != null && animator.isHuman)
        {
            rightHand = animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
            if (rightHand == null)
            {
                rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            }
            leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        }

        if (nockedArrow != null && rightHand != null)
        {
            arrow = Instantiate(nockedArrow, transform).transform;
            arrow.name = "Nocked Arrow";
            HeldItemView.StripToVisual(arrow.gameObject);
            arrow.gameObject.SetActive(false);
        }
    }

    private void OnDisable()
    {
        if (arrow != null)
        {
            arrow.gameObject.SetActive(false);
        }
    }

    private void LateUpdate()
    {
        GameObject shown = archery.Bow != null ? held.ShownModel : null;
        if (shown != model)
        {
            Bind(shown);
        }

        bool drawing = archery.IsDrawing;
        // The hand reaches the string quickly; the shot lets it go at once.
        pull = drawing ? Mathf.MoveTowards(pull, 1f, pullSpeed * Time.deltaTime) : 0f;
        if (line == null)
        {
            ShowArrow(false, Vector3.zero);
            return;
        }

        Vector3 rest = nock.position;
        Vector3 nockAt = rightHand != null ? Vector3.Lerp(rest, rightHand.position, pull) : rest;
        line.SetPosition(0, tipA.position);
        line.SetPosition(1, nockAt);
        line.SetPosition(2, tipB.position);
        ShowArrow(drawing && pull > 0.3f, nockAt);
    }

    private void ShowArrow(bool show, Vector3 nockAt)
    {
        if (arrow == null)
        {
            return;
        }

        arrow.gameObject.SetActive(show);
        Vector3 towardBow = leftHand != null ? leftHand.position - nockAt : transform.forward;
        if (show && towardBow.sqrMagnitude > 0.0001f)
        {
            arrow.SetPositionAndRotation(nockAt, Quaternion.LookRotation(towardBow, Vector3.up));
        }
    }

    private void Bind(GameObject bow)
    {
        model = bow;
        line = null;
        if (bow == null)
        {
            return;
        }

        tipA = Find(bow.transform, TipA);
        tipB = Find(bow.transform, TipB);
        nock = Find(bow.transform, Nock);
        if (tipA == null || tipB == null || nock == null)
        {
            return;
        }

        Transform existing = bow.transform.Find(StringName);
        line = existing != null ? existing.GetComponent<LineRenderer>() : new GameObject(StringName).AddComponent<LineRenderer>();
        line.transform.SetParent(bow.transform, false);
        line.useWorldSpace = true;
        line.positionCount = 3;
        line.widthMultiplier = stringWidth;
        line.numCapVertices = 0;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.sharedMaterial = stringMaterial;
    }

    private static Transform Find(Transform root, string name)
    {
        if (root.name == name)
        {
            return root;
        }

        foreach (Transform child in root)
        {
            Transform found = Find(child, name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
