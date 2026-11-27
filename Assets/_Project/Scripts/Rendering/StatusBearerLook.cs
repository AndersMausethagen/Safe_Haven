using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// Shows each status on whatever bears it while it lasts (Docs/CombatSkillsResearch_2026-11-24.md: a status you can read at a glance, as in
/// V Rising): the status's Look (StatusEffectDefinition.Look, a looping effect) at the middle of the body, sized to it, and let fade
/// when the status ends; and an outline in the colour of the status laid last (as the loot glow is drawn: OutlineCopies), which is what
/// reads from the high camera. Only presentation: it follows StatusEffects' Applied and Changed and never changes a status. Added by itself to
/// a bearer the first time a status with a look takes hold. A status without a look shows nothing here.
/// </summary>
[DisallowMultipleComponent]
[OnAFriendsCopy(CopyRule.Same, "only a look: it shows the statuses this game's StatusEffects holds (on a friend's copy, only those its player laid: the host's own aren't sent yet)")]
public sealed class StatusBearerLook : MonoBehaviour
{
    // How long an ended status's last particles have to fade before its effect is taken away.
    private const float FadeSeconds = 1.5f;
    // How long a burst (Shatter) is left to play out.
    private const float BurstSeconds = 2.5f;
    private const float OutlinePixels = 2.5f;
    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int OutlinePixelsId = Shader.PropertyToID("_OutlinePixels");
    // One outline material a status, shared by everything bearing it.
    private static readonly Dictionary<StatusEffectDefinition, Material> OutlineMaterials = new Dictionary<StatusEffectDefinition, Material>();

    private readonly Dictionary<StatusEffectDefinition, GameObject> shown = new Dictionary<StatusEffectDefinition, GameObject>();
    // The statuses shown, in the order they were laid: the outline takes the last one's colour.
    private readonly List<StatusEffectDefinition> order = new List<StatusEffectDefinition>();
    private List<OutlineCopies.Entry> outline;
    private StatusEffectDefinition outlined;
    private StatusEffects effects;
    private Damageable body;
    private bool measured;
    private Vector3 middle;
    private float radius = 0.5f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Hook()
    {
        // Entering Play doesn't reload scripts here: last session's outline materials go.
        foreach (Material material in OutlineMaterials.Values)
        {
            if (material != null)
            {
                DestroyImmediate(material);
            }
        }

        OutlineMaterials.Clear();
        StatusEffects.Applied -= OnApplied;
        StatusEffects.Applied += OnApplied;
        StatusEffects.Consumed -= OnConsumed;
        StatusEffects.Consumed += OnConsumed;
        StatusEffects.Reacted -= OnReacted;
        StatusEffects.Reacted += OnReacted;
    }

    // Two schools met on something (steam, a cauterised wound): the reaction's look and sound, once, at its middle.
    private static void OnReacted(Damageable target, StatusEffectDefinition holder, StatusReaction reaction)
    {
        if (!Application.isPlaying || target == null || reaction == null)
        {
            return;
        }

        Vector3 at = target.transform.position + Vector3.up;
        if (reaction.sound != null)
        {
            AudioManager.Play(reaction.sound, at);
        }

        if (reaction.look == null)
        {
            return;
        }

        GameObject burst = Instantiate(reaction.look, at, Quaternion.identity);
        foreach (VisualEffect visual in burst.GetComponentsInChildren<VisualEffect>())
        {
            if (visual.TryGetComponent(out VfxValues values))
            {
                values.Apply(visual);
            }

            visual.Reinit();
            visual.Play();
        }

        Destroy(burst, BurstSeconds);
    }

    // A status burst by a blow (Shatter): its Consume Look, once, where the blow landed.
    private static void OnConsumed(Damageable target, StatusEffectDefinition effect, Vector3 at)
    {
        // Only in a game being played: an edit-mode test bursting a status leaves nothing in the open scene.
        if (!Application.isPlaying || effect == null)
        {
            return;
        }

        if (effect.ConsumeSound != null)
        {
            AudioManager.Play(effect.ConsumeSound, at);
        }

        if (effect.ConsumeLook == null)
        {
            return;
        }

        GameObject burst = Instantiate(effect.ConsumeLook, at, Quaternion.identity);
        foreach (VisualEffect visual in burst.GetComponentsInChildren<VisualEffect>())
        {
            if (visual.TryGetComponent(out VfxValues values))
            {
                values.Apply(visual);
            }

            visual.Reinit();
            visual.Play();
        }

        Destroy(burst, BurstSeconds);
    }

    private static void OnApplied(Damageable target, StatusEffectDefinition effect, Combatant source)
    {
        if (!Application.isPlaying || target == null || effect == null || effect.Look == null)
        {
            return;
        }

        if (!target.TryGetComponent(out StatusBearerLook look))
        {
            look = target.gameObject.AddComponent<StatusBearerLook>();
        }

        look.Show(effect);
    }

    private void OnEnable()
    {
        if (effects == null)
        {
            effects = GetComponent<StatusEffects>();
        }

        if (effects != null)
        {
            effects.Changed += OnChanged;
        }

        if (body == null)
        {
            body = GetComponent<Damageable>();
        }

        if (body != null)
        {
            body.Died += OnDied;
        }
    }

    // Dead: everything goes at once, before the body becomes a ragdoll or loot (their bounds must not take in the effects).
    private void OnDied(Damageable victim, DamageInfo blow)
    {
        foreach (GameObject look in shown.Values)
        {
            if (look != null)
            {
                Destroy(look);
            }
        }

        shown.Clear();
        order.Clear();
        DropOutline();
    }

    // A copy only shows while its original does: renderers hidden or shown since (a defender stepping out of sight) follow.
    private void LateUpdate()
    {
        if (outline != null)
        {
            OutlineCopies.SetShown(outline, true);
        }
    }

    private void OnDisable()
    {
        if (effects != null)
        {
            effects.Changed -= OnChanged;
        }

        if (body != null)
        {
            body.Died -= OnDied;
        }

        // Statuses are cleared when their bearer is put away (StatusEffects.OnDisable): so are their looks, at once.
        foreach (GameObject look in shown.Values)
        {
            if (look != null)
            {
                Destroy(look);
            }
        }

        shown.Clear();
        order.Clear();
        DropOutline();
    }

    private void OnChanged(StatusEffectDefinition effect, bool on)
    {
        if (on)
        {
            Show(effect);
        }
        else
        {
            Hide(effect);
        }
    }

    private void Show(StatusEffectDefinition effect)
    {
        if (effect == null || effect.Look == null || (body != null && !body.IsAlive) || (shown.TryGetValue(effect, out GameObject already) && already != null))
        {
            return;
        }

        Measure();
        GameObject look = Instantiate(effect.Look, transform);
        look.transform.localPosition = middle;
        look.transform.localRotation = Quaternion.identity;
        // The effects simulate in their own space: undo the creature's scale (an elite is grown), so Radius and sizes stay in metres.
        // The look's own scale kept: the VFX pack's copies are sized by their root (Pack Bleeding at 0.4 bled 2.5 times too big).
        look.transform.localScale = effect.Look.transform.localScale / Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x));
        foreach (VisualEffect visual in look.GetComponentsInChildren<VisualEffect>())
        {
            if (visual.TryGetComponent(out VfxValues values))
            {
                values.Apply(visual);
            }

            // Spread over the body: a fox's bleed stays on the fox, a troll's frost covers the troll.
            if (visual.HasFloat("Radius"))
            {
                visual.SetFloat("Radius", radius);
            }

            visual.Reinit();
            visual.Play();
        }

        shown[effect] = look;
        order.Remove(effect);
        order.Add(effect);
        UpdateOutline();
    }

    private void Hide(StatusEffectDefinition effect)
    {
        if (effect == null || !shown.TryGetValue(effect, out GameObject look))
        {
            return;
        }

        shown.Remove(effect);
        order.Remove(effect);
        UpdateOutline();
        if (look == null)
        {
            return;
        }

        // No new particles; the last ones finish their lives (particle systems too: the VFX pack's looks).
        foreach (VisualEffect visual in look.GetComponentsInChildren<VisualEffect>())
        {
            visual.Stop();
        }

        foreach (ParticleSystem system in look.GetComponentsInChildren<ParticleSystem>())
        {
            system.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }

        Destroy(look, FadeSeconds);
    }

    private void UpdateOutline()
    {
        StatusEffectDefinition last = order.Count > 0 ? order[order.Count - 1] : null;
        if (last == null)
        {
            DropOutline();
            return;
        }

        if (last == outlined && outline != null)
        {
            return;
        }

        Material material = OutlineFor(last, out Material mask);
        if (material == null)
        {
            return; // no player outline to draw it with (a test, a menu scene)
        }

        if (outline == null || OutlineCopies.AllGone(outline))
        {
            outline = OutlineCopies.Create(transform, material, mask, "Status Outline", "Status Outline Mask", IsBody);
            OutlineCopies.SetShown(outline, true);
        }
        else
        {
            OutlineCopies.SetOutlineMaterial(outline, material);
        }

        outlined = last;
    }

    // The creature itself: not particles, effects, or a telegraph disc on the ground under it (an elite's ring).
    private static bool IsBody(Renderer part)
    {
        if (part is ParticleSystemRenderer || part.GetComponentInParent<VisualEffect>() != null)
        {
            return false;
        }

        Material telegraph = CombatFeelSettings.Current != null ? CombatFeelSettings.Current.TelegraphMaterial : null;
        return telegraph == null || part.sharedMaterial != telegraph;
    }

    private void DropOutline()
    {
        if (outline != null)
        {
            OutlineCopies.Destroy(outline);
            outline = null;
        }

        outlined = null;
    }

    // The player's hover outline in the status's colour (the loot glow is made so too: LootableRemains).
    private static Material OutlineFor(StatusEffectDefinition effect, out Material mask)
    {
        mask = null;
        PlayerInteractionOutline hover = Player.Current != null ? Player.Current.GetComponent<PlayerInteractionOutline>() : null;
        if (hover == null || hover.OutlineMaterial == null || hover.MaskMaterial == null)
        {
            return null;
        }

        mask = hover.MaskMaterial;
        if (!OutlineMaterials.TryGetValue(effect, out Material material) || material == null)
        {
            material = new Material(hover.OutlineMaterial) { name = effect.DisplayName + " Outline", hideFlags = HideFlags.DontSave };
            Color colour = effect.Color;
            colour.a = 0.85f;
            material.SetColor(OutlineColorId, colour);
            material.SetFloat(OutlinePixelsId, OutlinePixels);
            OutlineMaterials[effect] = material;
        }

        return material;
    }

    // The body's middle and girth, from its collider (or, without one, what it draws), in its own space: measured once.
    private void Measure()
    {
        if (measured)
        {
            return;
        }

        measured = true;
        Bounds bounds;
        if (TryGetComponent(out Collider body) && body.enabled)
        {
            bounds = body.bounds;
        }
        else
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                middle = Vector3.up;
                return;
            }

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
        }

        middle = transform.InverseTransformPoint(bounds.center);
        radius = Mathf.Clamp(Mathf.Max(bounds.extents.x, bounds.extents.z) * 0.8f, 0.25f, 2.5f);
    }
}
