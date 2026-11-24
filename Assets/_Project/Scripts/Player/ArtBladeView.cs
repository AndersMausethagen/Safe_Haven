using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// Light along the weapon in hand through a weapon art (Docs/CombatSkillsResearch_2026-11-24.md 6: "a hand or weapon glow" at the cast):
/// from the art's wind-up until its blow lands the blade sheds sparks of light in the art's colour, with a small light, then lets them
/// fade. So an art reads as one before it lands, the way V Rising's weapon skills do. Only the look; fitted to the weapon's own mesh as
/// the coated flames are (CoatedBladeView).
/// </summary>
[DisallowMultipleComponent]
public sealed class ArtBladeView : MonoBehaviour
{
    private const float LightIntensity = 2.6f;
    private const float LightRange = 2.4f;
    // How long the light takes to die once the blow has landed.
    private const float FadeSeconds = 0.3f;
    // How much brighter than the art's slash colour the sparks are drawn (they bloom).
    private const float SparkBrightness = 3f;

    private PlayerCombat combat;
    private PlayerSkills skills;
    private Color shownColour;
    private HeldItemView held;
    private GameObject glow;
    private VisualEffect visual;
    private Light glowLight;
    private GameObject on;
    private float fadeFrom = -1f;

    public static void AddTo(GameObject player)
    {
        if (player != null && !player.TryGetComponent(out ArtBladeView _))
        {
            player.AddComponent<ArtBladeView>();
        }
    }

    /// <summary>True while the blade is lit (an art under way, or its light still fading).</summary>
    public bool Lit => on != null;

    private void Awake()
    {
        combat = GetComponent<PlayerCombat>();
        skills = GetComponent<PlayerSkills>();
    }

    private void LateUpdate()
    {
        if (held == null)
        {
            held = GetComponentInChildren<HeldItemView>();
        }

        // An art under way, or a dodge's readied blow waiting (PlayerSkills.ReadiedStatus): the blade shows it.
        StatusEffectDefinition readied = skills != null ? skills.ReadiedStatus : null;
        bool arting = combat != null && combat.ArtBlowPending;
        if (arting || readied != null)
        {
            GameObject weapon = WeaponModel();
            Color want = arting ? ArtColour(combat.ActiveAttack) : readied.Color;
            // Nothing to light (bare hands, a bow): nothing attached, rather than attaching and dropping it again every frame.
            if (weapon == null)
            {
                if (on != null)
                {
                    Detach();
                }
            }
            else if (weapon != on || want != shownColour)
            {
                Attach(weapon, want);
            }

            fadeFrom = -1f;
            if (glowLight != null)
            {
                glowLight.intensity = LightIntensity * (0.85f + 0.15f * Mathf.Sin(Time.time * 30f));
            }

            return;
        }

        if (on == null)
        {
            return;
        }

        // The blow has landed (or the art was cut short): no new sparks, and the light dies.
        if (fadeFrom < 0f)
        {
            fadeFrom = Time.time;
            if (visual != null)
            {
                visual.Stop();
            }
        }

        float left = 1f - (Time.time - fadeFrom) / FadeSeconds;
        if (glowLight != null)
        {
            glowLight.intensity = LightIntensity * Mathf.Max(0f, left);
        }

        if (left <= -2f)
        {
            Detach(); // the last sparks have had their time too
        }
    }

    // Only hidden here: reparenting while the player is being destroyed or deactivated (a scene change) is refused by Unity.
    private void OnDisable()
    {
        if (glow != null)
        {
            glow.SetActive(false);
        }

        on = null;
    }

    private void OnDestroy()
    {
        if (glow != null)
        {
            Destroy(glow);
        }
    }

    // The melee weapon in hand, shown: not a bow, a rod or a torch.
    private GameObject WeaponModel()
    {
        GameObject model = held != null ? held.Current : null;
        WeaponDefinition weapon = combat != null ? combat.Weapon : null;
        if (model == null || !model.activeInHierarchy || weapon == null || weapon.TakesAttackInput || weapon.Kind == WeaponKind.Torch)
        {
            return null;
        }

        return model;
    }

    private void Detach()
    {
        on = null;
        if (glow != null)
        {
            glow.SetActive(false);
            // Kept under the player, not the weapon: a weapon put away or dropped mustn't take the glow with it.
            glow.transform.SetParent(transform, false);
        }
    }

    private void Attach(GameObject weapon, Color colour)
    {
        Detach();
        if (weapon == null || (glow == null && !Build()))
        {
            return;
        }

        // Along the blade: the weapon's mesh, in the mesh's own space, so any weapon is lit along its length.
        Renderer body = weapon.GetComponentInChildren<MeshRenderer>();
        Transform holder = body != null ? body.transform : weapon.transform;
        Bounds bounds = body is MeshRenderer mesh && mesh.TryGetComponent(out MeshFilter filter) && filter.sharedMesh != null
            ? filter.sharedMesh.bounds : new Bounds(Vector3.up * 0.4f, new Vector3(0.1f, 0.8f, 0.1f));
        glow.transform.SetParent(holder, false);
        glow.transform.localPosition = bounds.center;
        glow.transform.localRotation = Quaternion.identity;
        // The effect simulates in its own space: undo the mesh's import scale, so sizes and Radius stay in metres.
        Vector3 scale = holder.lossyScale;
        float metres = Mathf.Max(0.0001f, Mathf.Abs(scale.x));
        glow.transform.localScale = Vector3.one / metres;
        float halfLength = Mathf.Max(bounds.extents.x, Mathf.Max(bounds.extents.y, bounds.extents.z)) * metres;

        shownColour = colour;
        glow.SetActive(true);
        if (visual != null)
        {
            if (visual.TryGetComponent(out VfxValues values))
            {
                values.Apply(visual);
            }

            if (visual.HasVector3("Color"))
            {
                visual.SetVector3("Color", new Vector3(colour.r, colour.g, colour.b) * SparkBrightness);
            }

            if (visual.HasFloat("Radius"))
            {
                visual.SetFloat("Radius", Mathf.Clamp(halfLength, 0.15f, 1.2f));
            }

            visual.Reinit();
            visual.Play();
        }

        if (glowLight != null)
        {
            glowLight.color = colour;
            glowLight.intensity = LightIntensity;
        }

        on = weapon;
    }

    // The art's own light if its look has one, else the colour its slash is drawn in.
    private static Color ArtColour(AttackDefinition art)
    {
        if (art == null)
        {
            return new Color(1f, 0.62f, 0.3f);
        }

        Color light = art.Look != null ? art.Look.light : Color.clear;
        Color colour = light.a > 0f ? light : art.SlashColor;
        colour.a = 1f;
        return colour;
    }

    private bool Build()
    {
        GameObject prefab = CombatFeelSettings.Current != null ? CombatFeelSettings.Current.ArtBladeGlow : null;
        if (prefab == null)
        {
            return false;
        }

        glow = Instantiate(prefab, transform);
        glow.name = "Art Blade Glow";
        visual = glow.GetComponentInChildren<VisualEffect>();
        var lightHolder = new GameObject("Art Blade Light");
        lightHolder.transform.SetParent(glow.transform, false);
        glowLight = lightHolder.AddComponent<Light>();
        glowLight.type = LightType.Point;
        glowLight.range = LightRange;
        glowLight.shadows = LightShadows.None;
        return true;
    }
}
