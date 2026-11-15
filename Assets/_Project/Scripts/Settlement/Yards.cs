using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fenced yards as rooms without roofs (Docs/BuildingSystemResearch.md §3.4.4, the user's "A + C4", 2026-11-15): a closed
/// ring of fence pieces (BuildingDefinition › Encloses: Skigard, Palisade, Stone Wall, Hedge, Dry-stone Wall), a house wall
/// closing a side if need be, is a yard once it has a gate. What stands inside gives it a role (YardSettings › Roles: Home
/// Yard, Work Yard, Kitchen Garden, Feast Green, Paddock), a quality 0-5 (dressed, lit, a path to its gate) and a few small
/// things set about it by its role. A broken fence opens the ring until it is mended.
/// <para>
/// Worked out from the pieces on each machine (YardRings), never saved and never sent: co-op friends see the same yards
/// from the same synced fences. Only when something is built, moved, finished or broken; never per frame. On the portal,
/// added by HomePortal with HavenHomes.
/// </para>
/// </summary>
[DisallowMultipleComponent]
public sealed class Yards : MonoBehaviour
{
    private const float BrokenLookSeconds = 2f;

    /// <summary>One yard: its ring, its role (or none yet), what stands in it and how good it is.</summary>
    public sealed class Yard
    {
        public YardRings.Ring Ring;
        public YardRoleDefinition Role;
        public readonly List<BuildingObject> Inside = new List<BuildingObject>();
        public readonly List<BuildingDefinition> Kinds = new List<BuildingDefinition>();
        /// <summary>Its pieces and the houses whose walls close it.</summary>
        public readonly List<BuildingObject> Boundary = new List<BuildingObject>();
        public int Quality;
        /// <summary>What would make it better, or what it lacks for a role.</summary>
        public string Hint;
        public readonly List<GameObject> Clutter = new List<GameObject>();

        public bool HasGate => Ring.HasGate;
        public string Name => !HasGate ? "Needs a gate" : Role != null ? Role.DisplayName : "Yard";
        public Vector3 Centre(float height) => new Vector3(Ring.Centre.x, height, Ring.Centre.y);
        public bool Holds(BuildingObject building) => Inside.Contains(building) || Boundary.Contains(building);
    }

    private static readonly List<Yard> yards = new List<Yard>();
    private static readonly List<YardRings.Edge> edges = new List<YardRings.Edge>();
    private static readonly List<BuildingObject> owners = new List<BuildingObject>();
    private static readonly HashSet<YardRoleDefinition> counted = new HashSet<YardRoleDefinition>();
    private readonly HashSet<BuildingObject> broken = new HashSet<BuildingObject>();
    private bool dirty = true;
    private float nextBrokenLook;
    private Transform clutterRoot;

    public static Yards Current { get; private set; }

    /// <summary>Every ring found, with a gate or not.</summary>
    public static IReadOnlyList<Yard> All => yards;

    /// <summary>Raised when the yards were worked out again.</summary>
    public static event System.Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        yards.Clear();
        edges.Clear();
        owners.Clear();
        counted.Clear();
        Current = null;
    }

    private void OnEnable()
    {
        Current = this;
        BuildingObject.ActiveChanged += MarkDirty;
        Construction.Finished += OnFinished;
        dirty = true;
    }

    private void OnDisable()
    {
        BuildingObject.ActiveChanged -= MarkDirty;
        Construction.Finished -= OnFinished;
        ClearClutter();
        yards.Clear();
        if (Current == this)
        {
            Current = null;
        }
    }

    private void OnFinished(Construction _) => MarkDirty();

    public void MarkDirty() => dirty = true;

    private void LateUpdate()
    {
        // A fence broken in a breach opens its ring; mended, it closes again. Looked at now and then, not per frame.
        if (Time.unscaledTime >= nextBrokenLook)
        {
            nextBrokenLook = Time.unscaledTime + BrokenLookSeconds;
            foreach (BuildingObject building in BuildingObject.Active)
            {
                if (building.Definition == null || !building.Definition.Encloses)
                {
                    continue;
                }

                bool isBroken = building.TryGetComponent(out Damageable health) && health.IsBroken;
                if (isBroken != broken.Contains(building))
                {
                    dirty = true;
                    if (isBroken)
                    {
                        broken.Add(building);
                    }
                    else
                    {
                        broken.Remove(building);
                    }
                }
            }
        }

        if (!dirty)
        {
            return;
        }

        dirty = false;
        Settle();
        HomeLife.Forget();
        Changed?.Invoke();
    }

    // ----- Working them out -----

    private void Settle()
    {
        YardSettings settings = YardSettings.Current;
        edges.Clear();
        owners.Clear();
        foreach (BuildingObject building in BuildingObject.Active)
        {
            BuildingDefinition kind = building.Definition;
            if (kind == null || !building.IsStanding)
            {
                continue;
            }

            if (kind.Encloses && kind.PlaceInLines)
            {
                if (building.TryGetComponent(out Damageable health) && health.IsBroken)
                {
                    continue;
                }

                BuildPlacement.LineEnds(building.transform.position, building.transform.eulerAngles.y, building.LocalBounds, out Vector3 a, out Vector3 b);
                edges.Add(new YardRings.Edge(Flat(a), Flat(b), kind.IsGate, true, owners.Count));
                owners.Add(building);
            }
            else if (settings.HouseWallsClose && building.Beds > 0)
            {
                // A house's walls may close a side of a yard: its footprint's four sides.
                FootprintRect box = building.Footprint;
                Vector2 x = box.AxisX * box.HalfSize.x, z = box.AxisZ * box.HalfSize.y;
                Vector2 p0 = box.Center - x - z, p1 = box.Center + x - z, p2 = box.Center + x + z, p3 = box.Center - x + z;
                int owner = owners.Count;
                owners.Add(building);
                edges.Add(new YardRings.Edge(p0, p1, false, false, owner));
                edges.Add(new YardRings.Edge(p1, p2, false, false, owner));
                edges.Add(new YardRings.Edge(p2, p3, false, false, owner));
                edges.Add(new YardRings.Edge(p3, p0, false, false, owner));
            }
        }

        List<YardRings.Ring> rings = YardRings.Find(edges, settings.JoinDistance, settings.MinArea, settings.MaxArea);
        ClearClutter();
        yards.Clear();
        foreach (YardRings.Ring ring in rings)
        {
            var yard = new Yard { Ring = ring };
            foreach (int owner in ring.Owners)
            {
                yard.Boundary.Add(owners[owner]);
            }

            yards.Add(yard);
        }

        // What stands inside: each building in the smallest ring round it, so a yard inside a yard keeps its own.
        foreach (BuildingObject building in BuildingObject.Active)
        {
            BuildingDefinition kind = building.Definition;
            if (kind == null || kind.PlaceInLines || !building.IsStanding)
            {
                continue;
            }

            Yard home = null;
            Vector2 at = Flat(building.transform.position);
            foreach (Yard yard in yards)
            {
                bool holds = yard.Boundary.Contains(building) || yard.Ring.Contains(at);
                if (holds && (home == null || yard.Ring.Area < home.Ring.Area))
                {
                    home = yard;
                }
            }

            if (home != null)
            {
                home.Inside.Add(building);
                home.Kinds.Add(kind);
            }
        }

        foreach (Yard yard in yards)
        {
            Judge(yard, settings);
            if (yard.Role != null && yard.HasGate)
            {
                Dress(yard);
            }
        }
    }

    // Its role (highest score; ties to the role listed first) and its quality.
    private static void Judge(Yard yard, YardSettings settings)
    {
        int best = -1;
        yard.Role = null;
        foreach (YardRoleDefinition role in settings.Roles)
        {
            int score = role != null ? role.Score(yard.Kinds, yard.Ring.Area) : -1;
            if (score > best)
            {
                best = score;
                yard.Role = role;
            }
        }

        // Quality: dressed (one a kind of decoration, up to three), lit, and a path to its gate.
        var dressings = new HashSet<BuildingDefinition>();
        foreach (BuildingObject building in yard.Inside)
        {
            if (building.Definition.Decoration)
            {
                dressings.Add(building.Definition);
            }
        }

        int dressed = Mathf.Min(settings.MostDressing, dressings.Count);
        bool lit = Lit(yard);
        bool path = PathToGate(yard, settings.PathReach);
        yard.Quality = dressed + (lit ? 1 : 0) + (path ? 1 : 0);
        if (!yard.HasGate)
        {
            yard.Hint = "A gate in its fence makes it a yard";
        }
        else if (yard.Role == null)
        {
            yard.Hint = Lacks(yard, settings);
        }
        else
        {
            // The weakest part first, as RimWorld's room stats do, kept to one line.
            yard.Hint = !lit ? "A Lantern Post or Torch inside lights it"
                : dressed < settings.MostDressing ? "A bench, flower bed or woodpile dresses it"
                : !path ? "A path to its gate"
                : null;
        }
    }

    private static string Lacks(Yard yard, YardSettings settings)
    {
        string nearest = null;
        foreach (YardRoleDefinition role in settings.Roles)
        {
            string missing = role != null ? role.Missing(yard.Kinds, yard.Ring.Area) : null;
            if (missing != null)
            {
                nearest ??= $"{role.DisplayName}: needs {missing}";
            }
        }

        return nearest;
    }

    private static bool Lit(Yard yard)
    {
        foreach (VisibilitySource light in VisibilitySource.Active)
        {
            if (light == null || !light.IsLight || light.GetComponentInParent<BuildingObject>() == null)
            {
                continue;
            }

            Vector2 at = Flat(light.transform.position);
            if (yard.Ring.Contains(at) || Vector2.Distance(at, yard.Ring.Centre) <= light.Radius)
            {
                return true;
            }
        }

        return false;
    }

    private static bool PathToGate(Yard yard, float reach)
    {
        foreach (BuildingObject piece in yard.Boundary)
        {
            if (piece == null || piece.Definition == null || !piece.Definition.IsGate)
            {
                continue;
            }

            Vector2 gate = Flat(piece.transform.position);
            foreach (PathPiece path in PathPiece.All)
            {
                path.Ends(out Vector2 a, out Vector2 b);
                if (HomeQuality.DistanceToSegment(gate, a, b) <= reach)
                {
                    return true;
                }
            }
        }

        return false;
    }

    // ----- What its role sets about it -----

    private void Dress(Yard yard)
    {
        YardRoleDefinition role = yard.Role;
        if (role.Clutter.Count == 0 || role.ClutterCount <= 0)
        {
            return;
        }

        if (clutterRoot == null)
        {
            clutterRoot = new GameObject("Yard Clutter").transform;
            clutterRoot.SetParent(transform, false);
        }

        // Along the inside of its fence, away from the gate and what stands there; the same spots every time.
        List<Vector2> corners = yard.Ring.Corners;
        int seed = Mathf.RoundToInt(yard.Ring.Centre.x * 13f + yard.Ring.Centre.y * 7f);
        var random = new System.Random(seed);
        int placed = 0;
        for (int attempt = 0; attempt < role.ClutterCount * 6 && placed < role.ClutterCount; attempt++)
        {
            int side = random.Next(corners.Count);
            Vector2 a = corners[side], b = corners[(side + 1) % corners.Count];
            Vector2 along = b - a;
            if (along.magnitude < 2f)
            {
                continue;
            }

            Vector2 inward = new Vector2(-along.y, along.x).normalized;
            Vector2 spot = a + along * (0.2f + 0.6f * (float)random.NextDouble()) + inward * 1.1f;
            if (!yard.Ring.Contains(spot) || Crowded(yard, spot))
            {
                continue;
            }

            GameObject prefab = role.Clutter[random.Next(role.Clutter.Count)];
            if (prefab == null)
            {
                continue;
            }

            Vector3 ground = Ground(new Vector3(spot.x, transform.position.y, spot.y));
            float yaw = Mathf.Atan2(along.x, along.y) * Mathf.Rad2Deg + (random.NextDouble() < 0.5 ? 0f : 180f);
            GameObject thing = Instantiate(prefab, ground, Quaternion.Euler(0f, yaw, 0f), clutterRoot);
            thing.name = prefab.name + " (yard)";
            foreach (Collider part in thing.GetComponentsInChildren<Collider>())
            {
                Destroy(part);
            }

            foreach (UnityEngine.AI.NavMeshObstacle part in thing.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>())
            {
                Destroy(part);
            }

            yard.Clutter.Add(thing);
            placed++;
        }
    }

    private static bool Crowded(Yard yard, Vector2 spot)
    {
        foreach (BuildingObject building in yard.Inside)
        {
            FootprintRect box = building.Footprint;
            if ((box.Center - spot).magnitude < box.HalfSize.magnitude + 1.2f)
            {
                return true;
            }
        }

        foreach (BuildingObject piece in yard.Boundary)
        {
            if (piece != null && piece.Definition != null && piece.Definition.IsGate && (Flat(piece.transform.position) - spot).magnitude < 3f)
            {
                return true;
            }
        }

        foreach (GameObject other in yard.Clutter)
        {
            if (other != null && (Flat(other.transform.position) - spot).magnitude < 1.6f)
            {
                return true;
            }
        }

        return false;
    }

    private static Vector3 Ground(Vector3 point)
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null)
        {
            point.y = terrain.SampleHeight(point) + terrain.transform.position.y;
        }

        return point;
    }

    private void ClearClutter()
    {
        foreach (Yard yard in yards)
        {
            foreach (GameObject thing in yard.Clutter)
            {
                if (thing != null)
                {
                    Destroy(thing);
                }
            }

            yard.Clutter.Clear();
        }
    }

    // ----- Asking -----

    /// <summary>The yard (with a gate) this building stands in or closes, or null.</summary>
    public static Yard Of(BuildingObject building)
    {
        foreach (Yard yard in yards)
        {
            if (yard.HasGate && yard.Holds(building))
            {
                return yard;
            }
        }

        return null;
    }

    /// <summary>The yard (with a gate) whose ground holds this point, or null.</summary>
    public static Yard At(Vector3 point)
    {
        Yard found = null;
        foreach (Yard yard in yards)
        {
            if (yard.HasGate && yard.Ring.Contains(Flat(point)) && (found == null || yard.Ring.Area < found.Ring.Area))
            {
                found = yard;
            }
        }

        return found;
    }

    /// <summary>What a home's yard adds to its quality, and the yard's name; 0 when it has none that gives any.</summary>
    public static void ForHome(BuildingObject house, out int points, out string name)
    {
        Yard yard = Of(house);
        points = yard != null && yard.Role != null ? yard.Role.HomeQuality : 0;
        name = yard != null && yard.Role != null ? yard.Role.DisplayName : null;
    }

    /// <summary>How much faster a station works for its yard's role.</summary>
    public static float StationSpeed(BuildingObject station)
    {
        Yard yard = Of(station);
        return yard != null && yard.Role != null ? yard.Role.StationSpeed : 0f;
    }

    /// <summary>The fire of the nearest Feast Green (or other gathering yard) to <paramref name="from"/>, or null.</summary>
    public static BuildingObject GatheringFire(Vector3 from, BuildingDefinition fire)
    {
        BuildingObject best = null;
        float bestDistance = float.MaxValue;
        foreach (Yard yard in yards)
        {
            if (!yard.HasGate || yard.Role == null || !yard.Role.GatheringPlace)
            {
                continue;
            }

            foreach (BuildingObject building in yard.Inside)
            {
                float distance = (building.transform.position - from).sqrMagnitude;
                if (building.Definition == fire && distance < bestDistance)
                {
                    best = building;
                    bestDistance = distance;
                }
            }
        }

        return best;
    }

    /// <summary>Comfort from yards: each kind of role once.</summary>
    public static int Comfort => SumOnce(role => role.Comfort);

    /// <summary>Renown from yards: each kind of role once.</summary>
    public static int Renown => SumOnce(role => role.Renown);

    private static int SumOnce(System.Func<YardRoleDefinition, int> part)
    {
        counted.Clear();
        int total = 0;
        foreach (Yard yard in yards)
        {
            if (yard.HasGate && yard.Role != null && counted.Add(yard.Role))
            {
                total += part(yard.Role);
            }
        }

        return total;
    }

    private static Vector2 Flat(Vector3 point) => new Vector2(point.x, point.z);
}
