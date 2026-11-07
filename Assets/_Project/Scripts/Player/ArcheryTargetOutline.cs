using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Outlines in red the monster an arrow loosed now would fly at (<see cref="PlayerArchery.Target"/>): only while a bow
/// is in hand, the monster is in the bow's line of sight and no window is open. The outline is made of copies of the
/// monster's renderers (OutlineCopies), made once per monster and kept until it is gone.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerArchery))]
public sealed class ArcheryTargetOutline : MonoBehaviour
{
    [Tooltip("The red outline (Safe Haven/Interaction Outline shader).")]
    [SerializeField] private Material outlineMaterial;
    [Tooltip("The stencil mask that keeps the outline outside the body (Safe Haven/Interaction Outline Mask).")]
    [SerializeField] private Material maskMaterial;

    private readonly Dictionary<Damageable, List<OutlineCopies.Entry>> outlines = new Dictionary<Damageable, List<OutlineCopies.Entry>>();
    private readonly List<Damageable> gone = new List<Damageable>();
    private PlayerArchery archery;
    private Damageable shown;

    /// <summary>The monster outlined right now, or null.</summary>
    public Damageable Shown => shown;

    private void Awake()
    {
        archery = GetComponent<PlayerArchery>();
        if (outlineMaterial == null || maskMaterial == null)
        {
            Debug.LogError("ArcheryTargetOutline requires outline and mask materials.", this);
            enabled = false;
        }
    }

    private void LateUpdate()
    {
        bool visible = !GamePause.IsPaused && (UIStateManager.Current == null || !UIStateManager.Current.IsAnyWindowOpen);
        Damageable target = visible && archery.isActiveAndEnabled ? archery.Target : null;
        if (target != shown)
        {
            if (shown != null && outlines.TryGetValue(shown, out List<OutlineCopies.Entry> old))
            {
                OutlineCopies.SetShown(old, false);
            }

            shown = target;
            if (target != null && !outlines.ContainsKey(target))
            {
                Forget();
                outlines.Add(target, OutlineCopies.Create(target.transform, outlineMaterial, maskMaterial, "Target Outline", "Target Mask"));
            }
        }

        if (shown != null && outlines.TryGetValue(shown, out List<OutlineCopies.Entry> entries))
        {
            OutlineCopies.SetShown(entries, shown.IsAlive);
        }
    }

    // Drops the entries of monsters that are gone (their copies went with them).
    private void Forget()
    {
        gone.Clear();
        foreach (Damageable key in outlines.Keys)
        {
            if (key == null)
            {
                gone.Add(key);
            }
        }

        foreach (Damageable key in gone)
        {
            outlines.Remove(key);
        }
    }

    private void OnDisable()
    {
        foreach (var pair in outlines)
        {
            OutlineCopies.SetShown(pair.Value, false);
        }

        shown = null;
    }

    private void OnDestroy()
    {
        foreach (var pair in outlines)
        {
            OutlineCopies.Destroy(pair.Value);
        }

        outlines.Clear();
    }
}
