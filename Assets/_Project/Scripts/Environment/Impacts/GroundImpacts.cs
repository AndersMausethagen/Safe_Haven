using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Heavy blows shake the world (2026-11-14, the user: "make all attacks affect the environment"). For each impact
/// (a GroundImpactDefinition, at a point and a heading, scaled to the body behind it) it:
/// <list type="bullet">
/// <item>rolls a burst of wind through the grass (shader globals _GroundGusts / _GroundGustShapes / _GroundGustCount);</item>
/// <item>throws up dust, clods, grass bits or mud coloured by the ground there (GroundImpactBurst), or a splash in water;</item>
/// <item>presses a crater, furrow or mown arc into the ground for a while (GroundImpactField), with a decal on it;</item>
/// <item>throws loose things outward, shakes the trees near it (which drop leaves) and the camera;</item>
/// <item>makes the player stumble when they stand in it, hit or not.</item>
/// </list>
/// The look follows the Ground Impacts option (Off, Low, High); the stumble is the game and always applies.
/// Called by MonsterAttacks (strikes, charges), LobStrike (landings) and PlayerCombat (heavy swings and weapon arts).
/// </summary>
public static class GroundImpacts
{
    public const int MaxGusts = 6;
    private static readonly int GustsId = Shader.PropertyToID("_GroundGusts");
    private static readonly int GustShapesId = Shader.PropertyToID("_GroundGustShapes");
    private static readonly int GustCountId = Shader.PropertyToID("_GroundGustCount");
    private static readonly Collider[] Overlaps = new Collider[64];
    private static readonly HashSet<Rigidbody> Pushed = new HashSet<Rigidbody>();

    private static Host host;

    /// <summary>0 off, 1 low, 2 high (Options › Graphics › Ground Impacts).</summary>
    private static int Quality => GameOptions.GroundImpacts.Index;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        host = null;
        Shader.SetGlobalFloat(GustCountId, 0f);
        GroundImpactField.ClearGlobals();
    }

    /// <summary>The player's heavy swing or weapon art landing (it struck the ground whether or not it hit anyone).</summary>
    public static void PlayerSwing(AttackDefinition attack, Combatant player, Vector3 origin, Vector3 forward, bool twoHanded, float charge)
    {
        if (attack == null || player == null)
        {
            return;
        }

        GroundImpactSettings settings = GroundImpactSettings.Current;
        GroundImpactDefinition impact = attack.GroundImpact;
        if (impact == null)
        {
            impact = StrikeKindExtensions.Of(player, attack) switch
            {
                StrikeKind.Blunt => twoHanded && settings.HeavyTwoHandedBlunt != null ? settings.HeavyTwoHandedBlunt : settings.HeavyBlunt,
                StrikeKind.Stab => settings.HeavyStab,
                _ => settings.HeavySlash,
            };
        }

        if (impact == null)
        {
            return;
        }

        // A slash is felt along its reach; a blunt blow where it comes down, ahead.
        bool sweeping = impact.Mark == GroundMarkShape.Mow || impact.Mark == GroundMarkShape.None;
        Vector3 at = sweeping ? origin : origin + forward * Mathf.Min(attack.Range * 0.75f, 2.2f);
        Play(impact, at, forward, player, Mathf.Lerp(0.75f, 1.15f, charge), player.gameObject);
    }

    /// <summary>A creature's blow landing: its attack's Ground Impact, if it has one, at its body's size.</summary>
    public static void Strike(AttackDefinition attack, Combatant source, Vector3 at, Vector3 forward, bool trail = false, float damage = 0f)
    {
        GroundImpactDefinition impact = attack != null ? attack.GroundImpact : null;
        if (impact == null || source == null)
        {
            return;
        }

        float scale = Mathf.Clamp(BodyHeight(source) / impact.ReferenceHeight, 0.5f, 2.5f);
        // A round blow's crater is its warning circle (the attack's Range): what the ground shows is what it reached.
        float circle = attack.Arc >= 300f && impact.Mark == GroundMarkShape.Crater ? attack.Range : 0f;
        Play(impact, at, forward, source, scale, source.gameObject, trail, circle, damage);
    }

    /// <summary>Plays <paramref name="impact"/> at <paramref name="at"/>. <paramref name="trail"/>: a mark along a charge,
    /// with no stumble, shake or thrown things.</summary>
    public static void Play(GroundImpactDefinition impact, Vector3 at, Vector3 forward, Combatant source, float scale, GameObject self, bool trail = false,
        float circle = 0f, float damage = 0f)
    {
        // Only in play: what it makes (decals, the host) would otherwise be left in the open scene.
        if (impact == null || !Application.isPlaying)
        {
            return;
        }

        forward.y = 0f;
        forward = forward.sqrMagnitude > 1e-4f ? forward.normalized : Vector3.forward;
        if (!trail)
        {
            Stumble(impact, at, scale, source);
            Kin(impact, at, scale, source, damage);
        }

        // What it means to the world, whatever the look is set to: the noise carries, fire and frost meet what lies there.
        float noise = impact.Noise * Mathf.Sqrt(scale) * (trail ? 0.4f : 1f);
        if (noise > 0f)
        {
            if (source != null && Player.Current != null && source.gameObject == Player.Current.gameObject && Player.Current.Noise != null)
            {
                Player.Current.Noise.Make(at, noise);
            }
            else
            {
                WorldNoise.Emit(at, noise);
            }
        }

        if (impact.Element != HazardElement.None)
        {
            GroundHazard.Meet(impact.Element, at, Mathf.Max(impact.MarkRadius, impact.DustRadius) * scale, source);
        }

        Camera view = Camera.main;
        if (Quality <= 0 || view == null || (view.transform.position - at).sqrMagnitude > Sq(GroundImpactSettings.Current.MaxDistance))
        {
            return;
        }

        Host runner = Runner();
        bool high = Quality >= 2;
        if (!Ground(at, out Vector3 ground, out GroundImpactBurst.Ground surface, out bool water))
        {
            return;
        }

        if (impact.GustStrength > 0f && impact.GustRadius > 0f)
        {
            runner.Gust(ground, forward, impact.GustRadius * scale, impact.GustStrength * Mathf.Sqrt(scale), impact.GustSpeed, trail ? 360f : impact.GustArc);
        }

        GroundImpactSettings settings = GroundImpactSettings.Current;
        if (water)
        {
            if (settings.Splash != null)
            {
                Object.Instantiate(settings.Splash, ground, Quaternion.identity).transform.localScale = Vector3.one * Mathf.Clamp(scale, 0.6f, 2f);
            }
        }
        else
        {
            float share = trail ? 0.35f : high ? 1f : 0.5f;
            if (settings.Burst != null)
            {
                GameObject made = PrefabPool.Spawn(settings.Burst, ground, Quaternion.identity);
                if (made.TryGetComponent(out GroundImpactBurst burst))
                {
                    burst.Throw(surface, Mathf.RoundToInt(impact.Dust * share), high ? Mathf.RoundToInt(impact.Debris * share) : 0,
                        Mathf.RoundToInt(impact.Clippings * share), impact.DustRadius * scale, scale, forward, impact.GustArc);
                }
            }

            if (impact.Mark != GroundMarkShape.None)
            {
                Terrain under = TerrainAt(ground);
                if (under != null && under.TryGetComponent(out TerrainFootprints prints))
                {
                    prints.Include(ground);
                }

                float depth = impact.MarkDepth * Mathf.Min(scale, 1.8f);
                float radius = circle > 0f ? circle * 0.85f : impact.MarkRadius * scale;
                runner.Field.Press(impact.Mark, ground, forward, radius, depth, impact.RimHeight * Mathf.Min(scale, 1.8f),
                    impact.FurrowLength * scale, impact.GrassFlatten, impact.MarkSeconds, impact.GustArc);
                // Snow is thrown off where it lands, so the dark ground shows (the terrain's own snow follows the
                // field through its footprint mask).
                if (impact.Decal != null && !trail && (high || runner.DecalCount < 4))
                {
                    runner.Decal(impact.Decal, ground, forward, circle > 0f ? circle * 2f : impact.DecalSize * scale, impact.MarkSeconds);
                }
            }
        }

        if (trail)
        {
            return;
        }

        if (impact.Push > 0f)
        {
            Throw(ground, impact.Push, impact.PushRadius * scale, self);
        }

        if (high && impact.GustStrength > 0.3f)
        {
            runner.Pulse(ground, impact.GustRadius * scale, impact.GustStrength * 12f);
        }

        if (impact.TreeShake > 0f)
        {
            runner.ShakeTrees(ground, impact.PushRadius * scale * 1.5f, impact.TreeShake, high);
        }

        if (impact.Shake > 0f && Player.Current != null)
        {
            float distance = Vector3.Distance(Player.Current.transform.position, ground);
            float fall = Mathf.Clamp01(1f - distance / Mathf.Max(impact.GustRadius * scale * 2.5f, 1f));
            if (fall > 0f)
            {
                CameraShake.Add(impact.Shake * fall);
            }
        }

        if (impact.Sound != null)
        {
            AudioManager.Play(impact.Sound, ground);
        }
    }

    // A big blow among its own kind (a giant's stomp in its pack): a little of the blow, tossed outward, dazed a moment.
    private static void Kin(GroundImpactDefinition impact, Vector3 at, float scale, Combatant source, float damage)
    {
        if (impact.StumbleRadius <= 0f || source == null || !source.TryGetComponent(out MonsterAI thrower))
        {
            return;
        }

        GroundImpactSettings settings = GroundImpactSettings.Current;
        float reach = impact.StumbleRadius * scale;
        var near = new List<MonsterAI>();
        foreach (MonsterAI other in MonsterAI.Active)
        {
            if (other == thrower || other.CurrentState == MonsterAI.State.Dead || other.IsRemote || !other.TryGetComponent(out Combatant side)
                || side.Faction != source.Faction)
            {
                continue;
            }

            Vector3 offset = other.transform.position - at;
            if (Mathf.Abs(offset.y) <= 2f && new Vector2(offset.x, offset.z).magnitude <= reach)
            {
                near.Add(other);
            }
        }

        foreach (MonsterAI other in near)
        {
            Vector3 away = other.transform.position - at;
            away.y = 0f;
            if (away.sqrMagnitude < 1e-3f)
            {
                Vector2 any = Random.insideUnitCircle.normalized;
                away = new Vector3(any.x, 0f, any.y);
            }

            away.Normalize();
            if (damage > 0f && settings.FriendlyDamage > 0f && other.TryGetComponent(out Damageable body))
            {
                body.ApplyDamage(new DamageInfo(source, null, damage * settings.FriendlyDamage, DamageType.Physical, false,
                    other.transform.position + Vector3.up, away));
            }

            if (other.CurrentState == MonsterAI.State.Dead)
            {
                continue;
            }

            ForcedMove.Drag(other.transform, other.transform.position - away, -settings.FriendlyToss, 0.35f); // out from where it landed
            other.StaggerAtLeast(settings.DazeSeconds);
            AudioManager.Play(settings.DazeSound, other.transform.position);
        }
    }

    /// <summary>What the ground is like at <paramref name="at"/> (its colour, grass, wet, snow), and whether it is open water.</summary>
    public static GroundImpactBurst.Ground SurfaceAt(Vector3 at, out bool water)
    {
        Ground(at, out _, out GroundImpactBurst.Ground surface, out water);
        return surface;
    }

    /// <summary>True where grass grows (the ground layer there reads as grass or moss), for fire to spread through.</summary>
    public static bool IsGrassy(Vector3 at)
    {
        return Ground(at, out _, out GroundImpactBurst.Ground surface, out bool water) && !water && surface.Grassy && !surface.Wet && !surface.Snowy;
    }

    /// <summary>Burnt ground: the grass burnt short and a scorch on the earth for <paramref name="seconds"/>.</summary>
    public static void Scorch(Vector3 at, float radius, float seconds)
    {
        if (Quality <= 0 || !Application.isPlaying)
        {
            return;
        }

        Host runner = Runner();
        runner.Field.Press(GroundMarkShape.Mow, at, Vector3.forward, radius, 0f, 0f, 0f, 0.9f, seconds, 360f);
        Material scorch = GroundImpactSettings.Current.Scorch;
        if (scorch != null)
        {
            runner.Decal(scorch, at, Vector3.forward, radius * 2.2f, seconds);
        }
    }

    // In it, the player loses their footing for a moment, hit or not; a dodge (its untouchable moment) gets through.
    private static void Stumble(GroundImpactDefinition impact, Vector3 at, float scale, Combatant source)
    {
        Player player = Player.Current;
        if (impact.StumbleRadius <= 0f || player == null || (source != null && source.gameObject == player.gameObject)
            || player.Health == null || !player.Health.IsAlive || player.Health.IsInvulnerable)
        {
            return;
        }

        Vector3 offset = player.transform.position - at;
        if (Mathf.Abs(offset.y) > 1.5f)
        {
            return;
        }

        offset.y = 0f;
        if (offset.magnitude <= impact.StumbleRadius * scale && player.TryGetComponent(out PlayerCombat combat))
        {
            combat.Stumble(impact.StumbleSeconds);
        }
    }

    private static void Throw(Vector3 at, float push, float radius, GameObject self)
    {
        Pushed.Clear();
        int count = Physics.OverlapSphereNonAlloc(at, radius, Overlaps, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Rigidbody body = Overlaps[i].attachedRigidbody;
            if (body == null || body.isKinematic || !Pushed.Add(body) || (self != null && body.transform.IsChildOf(self.transform))
                || body.GetComponentInParent<CharacterController>() != null)
            {
                continue;
            }

            body.AddExplosionForce(push, at, radius, 0.5f, ForceMode.VelocityChange);
        }
    }

    // What lies under the point: the ground (terrain or anything solid), its colour, grass, wet or snow; or open water.
    private static bool Ground(Vector3 at, out Vector3 point, out GroundImpactBurst.Ground surface, out bool water)
    {
        GroundImpactSettings settings = GroundImpactSettings.Current;
        surface = new GroundImpactBurst.Ground { Colour = settings.Dirt };
        point = at;
        water = false;
        if (WaterSurface.OpenAt(at + Vector3.up * 0.5f, 0.3f, out float waterHeight) != null && waterHeight > at.y - 0.6f)
        {
            point = new Vector3(at.x, waterHeight, at.z);
            water = true;
            return true;
        }

        if (Physics.Raycast(at + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 4f, ~0, QueryTriggerInteraction.Ignore))
        {
            point = hit.point;
        }

        Terrain terrain = TerrainAt(point);
        string layer = terrain != null ? DominantLayer(terrain, point) : null;
        string name = layer != null ? layer.ToLowerInvariant() : "";
        if (Has(name, "snow")) { surface.Colour = settings.Snow; surface.Snowy = true; }
        else if (Has(name, "mud", "bog", "swamp", "marsh", "wet")) { surface.Colour = settings.Mud; surface.Wet = true; }
        else if (Has(name, "sand", "beach", "shore")) surface.Colour = settings.Sand;
        else if (Has(name, "rock", "stone", "cliff", "gravel", "scree", "pebble")) surface.Colour = settings.Rock;
        else if (Has(name, "grass", "moss", "meadow", "clover", "fern", "lawn")) { surface.Colour = Color.Lerp(settings.Grass, settings.Dirt, 0.5f); surface.Grassy = true; }
        else if (terrain == null) surface.Colour = settings.Rock;

        WeatherSystem weather = WeatherSystem.Active;
        if (weather != null)
        {
            if (weather.Snow > 0.4f && !surface.Wet)
            {
                surface.Colour = settings.Snow;
                surface.Snowy = true;
            }
            else if (weather.Wetness > settings.WetFromRain && !surface.Snowy)
            {
                surface.Wet = true;
                surface.Colour = Color.Lerp(surface.Colour, settings.Mud, 0.5f);
            }
        }

        return true;
    }

    private static bool Has(string name, params string[] words)
    {
        foreach (string word in words)
        {
            if (name.Contains(word))
            {
                return true;
            }
        }

        return false;
    }

    private static Terrain TerrainAt(Vector3 point)
    {
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            if (terrain == null || terrain.terrainData == null)
            {
                continue;
            }

            Vector3 local = point - terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            if (local.x >= 0 && local.z >= 0 && local.x <= size.x && local.z <= size.z)
            {
                return terrain;
            }
        }

        return null;
    }

    private static string DominantLayer(Terrain terrain, Vector3 point)
    {
        TerrainData data = terrain.terrainData;
        TerrainLayer[] layers = data.terrainLayers;
        if (layers == null || layers.Length == 0 || data.alphamapLayers == 0)
        {
            return null;
        }

        Vector3 local = point - terrain.transform.position;
        int x = Mathf.Clamp(Mathf.RoundToInt(local.x / data.size.x * (data.alphamapWidth - 1)), 0, data.alphamapWidth - 1);
        int z = Mathf.Clamp(Mathf.RoundToInt(local.z / data.size.z * (data.alphamapHeight - 1)), 0, data.alphamapHeight - 1);
        float[,,] weights = data.GetAlphamaps(x, z, 1, 1);
        int best = 0;
        for (int i = 1; i < weights.GetLength(2); i++)
        {
            if (weights[0, 0, i] > weights[0, 0, best])
            {
                best = i;
            }
        }

        TerrainLayer chosen = best < layers.Length ? layers[best] : null;
        return chosen == null ? null : chosen.diffuseTexture != null ? chosen.name + " " + chosen.diffuseTexture.name : chosen.name;
    }

    private static float BodyHeight(Combatant source)
    {
        if (source.TryGetComponent(out CapsuleCollider capsule))
        {
            return capsule.height * source.transform.lossyScale.y;
        }

        if (source.TryGetComponent(out UnityEngine.AI.NavMeshAgent agent))
        {
            return agent.height;
        }

        return 2f;
    }

    private static float Sq(float value) => value * value;

    private static Host Runner()
    {
        if (host == null)
        {
            var go = new GameObject("Ground Impacts");
            host = go.AddComponent<Host>();
        }

        return host;
    }

    /// <summary>The per-scene part: the mark field, the wind bursts in flight, the decals and the shaken trees.</summary>
    private sealed class Host : MonoBehaviour
    {
        private struct GustState
        {
            public Vector4 Where;  // x, z, start time (shader time), strength
            public Vector4 Shape;  // radius, speed, cos of half the arc, heading in radians
            public float Ends;
        }

        private struct DecalState
        {
            public DecalProjector Projector;
            public float Born;
            public float Lasts;
        }

        private struct ShakeState
        {
            public TreeBender Bender;
            public float Born;
            public float Lean;
        }

        public readonly GroundImpactField Field = new GroundImpactField();
        private readonly List<GustState> gusts = new List<GustState>();
        private readonly List<DecalState> decals = new List<DecalState>();
        private readonly List<ShakeState> shakes = new List<ShakeState>();
        private readonly Vector4[] where = new Vector4[MaxGusts];
        private readonly Vector4[] shape = new Vector4[MaxGusts];
        private readonly Dictionary<Terrain, Vector3[]> trees = new Dictionary<Terrain, Vector3[]>();

        public int DecalCount => decals.Count;

        public void Gust(Vector3 at, Vector3 forward, float radius, float strength, float speed, float arc)
        {
            int limit = Mathf.Min(MaxGusts, GroundImpactSettings.Current.MaxGusts);
            if (gusts.Count >= limit)
            {
                gusts.RemoveAt(0);
            }

            float now = Time.timeSinceLevelLoad;
            gusts.Add(new GustState
            {
                Where = new Vector4(at.x, at.z, now, strength),
                Shape = new Vector4(radius, speed, arc >= 359f ? -2f : Mathf.Cos(arc * 0.5f * Mathf.Deg2Rad), Mathf.Atan2(forward.x, forward.z)),
                Ends = now + radius / Mathf.Max(speed, 0.1f) + 1.6f,
            });
        }

        public void Decal(Material material, Vector3 at, Vector3 forward, float size, float lasts)
        {
            if (decals.Count >= GroundImpactSettings.Current.MaxDecals)
            {
                Remove(0);
            }

            var go = new GameObject("Impact Decal");
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(at + Vector3.up * 0.8f, Quaternion.LookRotation(Vector3.down, forward) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)));
            DecalProjector projector = go.AddComponent<DecalProjector>();
            projector.material = material;
            projector.size = new Vector3(size, size, 2f);
            projector.pivot = Vector3.zero;
            projector.fadeFactor = 1f;
            decals.Add(new DecalState { Projector = projector, Born = Time.time, Lasts = lasts });
        }

        // Falling leaves, fireflies and snow near it are blown aside (the particle effects that already follow the wind:
        // their External Forces module), by a force field pushing out for a moment.
        private ParticleSystemForceField pulse;
        private float pulseEnds;

        public void Pulse(Vector3 at, float radius, float strength)
        {
            if (pulse == null)
            {
                var go = new GameObject("Impact Pulse");
                go.transform.SetParent(transform, false);
                pulse = go.AddComponent<ParticleSystemForceField>();
                pulse.shape = ParticleSystemForceFieldShape.Sphere;
            }

            pulse.transform.position = at + Vector3.up;
            pulse.endRange = radius;
            pulse.gravity = -strength;
            pulse.gravityFocus = 0f;
            pulse.gameObject.SetActive(true);
            pulseEnds = Time.time + 0.35f;
        }

        public void ShakeTrees(Vector3 at, float radius, float lean, bool leaves)
        {
            var go = new GameObject("Impact Tree Shake");
            go.transform.SetParent(transform, false);
            go.transform.position = at;
            TreeBender bender = go.AddComponent<TreeBender>();
            bender.Radius = radius;
            bender.Lean = lean;
            shakes.Add(new ShakeState { Bender = bender, Born = Time.time, Lean = lean });
            GameObject fall = GroundImpactSettings.Current.Leaves;
            if (!leaves || fall == null)
            {
                return;
            }

            // A few of the trees in reach drop their leaves.
            int dropped = 0;
            Terrain terrain = TerrainAt(at);
            if (terrain != null)
            {
                foreach (Vector3 tree in TreesOf(terrain))
                {
                    Vector3 offset = tree - at;
                    offset.y = 0f;
                    if (offset.sqrMagnitude <= radius * radius)
                    {
                        PrefabPool.Spawn(fall, tree + Vector3.up * Random.Range(4f, 7f), Quaternion.identity);
                        if (++dropped >= 4)
                        {
                            break;
                        }
                    }
                }
            }
        }

        private Vector3[] TreesOf(Terrain terrain)
        {
            if (trees.TryGetValue(terrain, out Vector3[] cached))
            {
                return cached;
            }

            TerrainData data = terrain.terrainData;
            TreeInstance[] instances = data.treeInstances;
            var positions = new Vector3[instances.Length];
            for (int i = 0; i < instances.Length; i++)
            {
                positions[i] = Vector3.Scale(instances[i].position, data.size) + terrain.transform.position;
            }

            trees[terrain] = positions;
            return positions;
        }

        private bool mirrored;

        private void Update()
        {
            Field.Advance(Time.deltaTime);
            if (pulse != null && pulse.gameObject.activeSelf && Time.time >= pulseEnds)
            {
                pulse.gameObject.SetActive(false);
            }

            float now = Time.timeSinceLevelLoad;
            gusts.RemoveAll(gust => now >= gust.Ends);
            for (int i = 0; i < gusts.Count; i++)
            {
                where[i] = gusts[i].Where;
                shape[i] = gusts[i].Shape;
            }

            Shader.SetGlobalVectorArray(GustsId, where);
            Shader.SetGlobalVectorArray(GustShapesId, shape);
            Shader.SetGlobalFloat(GustCountId, gusts.Count);

            // Decals hold, then fade over the last third of their time.
            for (int i = decals.Count - 1; i >= 0; i--)
            {
                DecalState decal = decals[i];
                float age = Time.time - decal.Born;
                if (decal.Projector == null || age >= decal.Lasts)
                {
                    Remove(i);
                    continue;
                }

                decal.Projector.fadeFactor = Mathf.Clamp01((decal.Lasts - age) / (decal.Lasts * 0.33f));
            }

            // A shaken tree swings over and back, settling in about two seconds.
            for (int i = shakes.Count - 1; i >= 0; i--)
            {
                ShakeState shake = shakes[i];
                float age = Time.time - shake.Born;
                if (shake.Bender == null || age >= 2.2f)
                {
                    if (shake.Bender != null) Destroy(shake.Bender.gameObject);
                    shakes.RemoveAt(i);
                    continue;
                }

                shake.Bender.Lean = shake.Lean * Mathf.Exp(-age * 2.2f) * (0.5f + 0.5f * Mathf.Cos(age * 9f));
            }
        }

        // After everything has moved this frame (and any footprints were pressed): the marks into the terrains' fields,
        // while they heal and once more when they are gone, so none is left behind.
        private void LateUpdate()
        {
            if (Field.IsActive || mirrored)
            {
                Field.Mirror();
                mirrored = Field.IsActive;
            }
        }

        private void Remove(int index)
        {
            if (decals[index].Projector != null)
            {
                Destroy(decals[index].Projector.gameObject);
            }

            decals.RemoveAt(index);
        }

        private void OnDestroy()
        {
            Field.Release();
            Shader.SetGlobalFloat(GustCountId, 0f);
            if (host == this)
            {
                host = null;
            }
        }
    }
}
