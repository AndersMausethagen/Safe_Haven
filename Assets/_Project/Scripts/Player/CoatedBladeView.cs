using UnityEngine;

/// <summary>
/// Flames on the weapon in hand while a coating's fire is on it (Stat › Melee Fire Damage above 0: Ember Pitch). They smoulder at rest
/// and roar through a swing (Combat Feel Settings › Coated Flames), with a flickering light, so the coating is seen as long as it lasts
/// and every blow reads as a burning one. Only the look: the fire itself is CoatedFire's.
/// </summary>
[DisallowMultipleComponent]
public sealed class CoatedBladeView : MonoBehaviour
{
    private static readonly Color FlameLight = new Color(1f, 0.55f, 0.2f);
    private const float LightAtRest = 0.8f;
    private const float LightSwinging = 2.2f;

    private Combatant combatant;
    private PlayerCombat combat;
    private HeldItemView held;
    private GameObject flames;
    private ParticleSystem[] systems = new ParticleSystem[0];
    private Light flameLight;
    private GameObject on;

    public static void AddTo(GameObject player)
    {
        if (player != null && !player.TryGetComponent(out CoatedBladeView _))
        {
            player.AddComponent<CoatedBladeView>();
        }
    }

    /// <summary>True while the flames are on a weapon.</summary>
    public bool Burning => on != null;

    private void Awake()
    {
        combatant = GetComponent<Combatant>();
        combat = GetComponent<PlayerCombat>();
    }

    private void LateUpdate()
    {
        if (held == null)
        {
            held = GetComponentInChildren<HeldItemView>();
        }

        GameObject weapon = Coated ? WeaponModel() : null;
        if (weapon != on)
        {
            Attach(weapon);
        }

        if (on == null)
        {
            return;
        }

        CombatFeelSettings feel = CombatFeelSettings.Current;
        bool swinging = combat != null && combat.IsAttacking;
        foreach (ParticleSystem system in systems)
        {
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = swinging ? feel.CoatedFlamesSwinging : feel.CoatedFlamesAtRest;
        }

        if (flameLight != null)
        {
            float flicker = 0.75f + 0.25f * Mathf.PerlinNoise(Time.time * 9f, 0.3f);
            flameLight.intensity = (swinging ? LightSwinging : LightAtRest) * flicker;
        }
    }

    // Only hidden here: reparenting while the player is being destroyed or deactivated (a scene change) is refused by Unity.
    private void OnDisable()
    {
        if (flames != null)
        {
            flames.SetActive(false);
        }

        on = null;
    }

    private void OnDestroy()
    {
        if (flames != null)
        {
            Destroy(flames);
        }
    }

    private bool Coated => combatant != null && combatant.Stats != null && combatant.Stats.Get(StatType.MeleeFireDamage) > 0f;

    // The melee weapon in hand, shown: not a bow, a rod or a torch (a torch burns already).
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

    private void Attach(GameObject weapon)
    {
        on = null;
        if (weapon == null)
        {
            if (flames != null)
            {
                flames.SetActive(false);
                // Kept under the player, not the weapon: a weapon put away or dropped mustn't take the flames with it.
                flames.transform.SetParent(transform, false);
            }

            return;
        }

        if (flames == null && !Build())
        {
            return;
        }

        // Along the blade: a box the size of the weapon's mesh, in the mesh's own space, so any weapon burns along its length.
        Renderer body = weapon.GetComponentInChildren<MeshRenderer>();
        Transform holder = body != null ? body.transform : weapon.transform;
        Bounds bounds = body is MeshRenderer mesh && mesh.TryGetComponent(out MeshFilter filter) && filter.sharedMesh != null
            ? filter.sharedMesh.bounds : new Bounds(Vector3.up * 0.4f, new Vector3(0.1f, 0.8f, 0.1f));
        flames.transform.SetParent(holder, false);
        flames.transform.localPosition = Vector3.zero;
        flames.transform.localRotation = Quaternion.identity;
        flames.transform.localScale = Vector3.one;
        foreach (ParticleSystem system in systems)
        {
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.position = bounds.center;
            shape.scale = bounds.size;
            ParticleSystem.MainModule main = system.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
        }

        if (flameLight != null)
        {
            flameLight.transform.localPosition = bounds.center;
        }

        flames.SetActive(true);
        on = weapon;
    }

    private bool Build()
    {
        GameObject prefab = CombatFeelSettings.Current != null ? CombatFeelSettings.Current.CoatedFlames : null;
        if (prefab == null)
        {
            return false;
        }

        flames = Instantiate(prefab, transform);
        flames.name = "Coated Flames";
        systems = flames.GetComponentsInChildren<ParticleSystem>(true);
        var lightHolder = new GameObject("Coated Flame Light");
        lightHolder.transform.SetParent(flames.transform, false);
        flameLight = lightHolder.AddComponent<Light>();
        flameLight.type = LightType.Point;
        flameLight.color = FlameLight;
        flameLight.range = 2.2f;
        flameLight.shadows = LightShadows.None;
        return true;
    }
}
