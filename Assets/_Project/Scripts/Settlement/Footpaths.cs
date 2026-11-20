using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Footpaths that wear in where the residents walk (Docs/BuildingSystemResearch.md §3.4.5; ARCHITECTURE's open "worn paths"
/// TODO): a coarse map of the ground round the portal (a metre a cell) gains wear under every resident on the move and grows
/// back slowly; cells worn enough get a patch of trampled earth (the Trampled Ground decal), more solid the more worn. Never
/// across a stone path. Looked at twice a second; decals laid again every few seconds, from a pool. The map is kept with the
/// game, packed (SettlementState.Footpaths), wear that doesn't show yet too, so paths don't start over on every load or raid.
/// On the portal, with HavenHomes.
/// </summary>
[DisallowMultipleComponent]
public sealed class Footpaths : MonoBehaviour
{
    private const int Cells = 160;
    private const float CellSize = 1f;
    private const float LookSeconds = 0.5f;
    private const float LaySeconds = 3f;
    private const float KeepSeconds = 30f;
    private const float ShowsFrom = 0.45f;
    /// <summary>Wear kept with the game though it doesn't show yet: a cell walked a couple of times. Below it, forgotten.</summary>
    public const float KeptFrom = 0.05f;
    private const float FullAt = 1f;
    private const int MostDecals = 450;
    private const float DecalSize = 1.7f;

    private readonly float[] wear = new float[Cells * Cells];
    private readonly HashSet<int> worn = new HashSet<int>();
    private readonly List<DecalProjector> decals = new List<DecalProjector>();
    private readonly List<int> laid = new List<int>();
    private Vector3 origin;
    private float nextLook;
    private float nextLay;
    private float nextKeep;
    private bool changed;
    private Transform root;
    private SettlementState keptIn;

    private void Start()
    {
        origin = transform.position - new Vector3(Cells * CellSize * 0.5f, 0f, Cells * CellSize * 0.5f);
        // The object itself, not a view: Keep writes back only into the one these paths were read from.
        keptIn = GameSession.HereItself;
        Unpack(keptIn.Footpaths);
        nextLay = 0f;
    }

    private void OnDisable() => Keep();

    private void Update()
    {
        float now = Time.unscaledTime;
        if (now >= nextLook)
        {
            float seconds = nextLook > 0f ? LookSeconds : 0f;
            nextLook = now + LookSeconds;
            Walk();
            Regrow(seconds);
        }

        if (changed && now >= nextLay)
        {
            nextLay = now + LaySeconds;
            changed = false;
            Lay();
        }

        if (now >= nextKeep)
        {
            nextKeep = now + KeepSeconds;
            Keep();
        }
    }

    // Wear under everyone on their way somewhere.
    private void Walk()
    {
        HomeLifeSettings settings = HomeLifeSettings.Current;
        foreach (ResidentNpc npc in ResidentNpc.Present)
        {
            ResidentLife life = npc.Life;
            if (life == null || life.IsIndoors || life.HasArrived)
            {
                continue;
            }

            Vector3 at = npc.transform.position;
            if (HavenPaths.OnPath(at, pavedOnly: true))
            {
                continue;
            }

            int cell = CellAt(at);
            if (cell >= 0)
            {
                float before = wear[cell];
                wear[cell] = Mathf.Min(1.5f, before + settings.WearPerStep);
                if (before < ShowsFrom && wear[cell] >= ShowsFrom)
                {
                    worn.Add(cell);
                    changed = true;
                }
                else if (wear[cell] >= ShowsFrom)
                {
                    changed |= Mathf.FloorToInt(before * 5f) != Mathf.FloorToInt(wear[cell] * 5f);
                }
            }
        }
    }

    // Grass grows back over what nobody walks, slowly: only the worn cells are looked at.
    private void Regrow(float seconds)
    {
        if (seconds <= 0f || worn.Count == 0)
        {
            return;
        }

        float less = HomeLifeSettings.Current.WearRegrowth * seconds;
        laid.Clear();
        foreach (int cell in worn)
        {
            wear[cell] = Mathf.Max(0f, wear[cell] - less);
            if (wear[cell] < ShowsFrom)
            {
                laid.Add(cell);
            }
        }

        foreach (int cell in laid)
        {
            worn.Remove(cell);
            changed = true;
        }
    }

    private void Lay()
    {
        Material material = RenderingSettings.Current.TrampledDecal;
        if (material == null)
        {
            return;
        }

        if (root == null)
        {
            root = new GameObject("Footpaths").transform;
            root.SetParent(transform, false);
        }

        int used = 0;
        foreach (int cell in worn)
        {
            if (used >= MostDecals)
            {
                break;
            }

            DecalProjector decal = Decal(used++, material);
            int x = cell % Cells, z = cell / Cells;
            Vector3 at = origin + new Vector3((x + 0.5f) * CellSize, 0f, (z + 0.5f) * CellSize);
            Terrain terrain = Terrain.activeTerrain;
            at.y = terrain != null ? terrain.SampleHeight(at) + terrain.transform.position.y : transform.position.y;
            decal.transform.position = at + Vector3.up * 0.6f;
            // A turn of its own from where it lies, so the patches don't line up into a grid.
            decal.transform.rotation = Quaternion.LookRotation(Vector3.down, Quaternion.Euler(0f, (cell * 73) % 360, 0f) * Vector3.forward);
            decal.fadeFactor = Mathf.Clamp01((wear[cell] - ShowsFrom) / (FullAt - ShowsFrom)) * 0.85f + 0.15f;
        }

        for (int i = used; i < decals.Count; i++)
        {
            decals[i].gameObject.SetActive(false);
        }
    }

    private DecalProjector Decal(int index, Material material)
    {
        while (decals.Count <= index)
        {
            var holder = new GameObject("Footpath");
            holder.transform.SetParent(root, false);
            var decal = holder.AddComponent<DecalProjector>();
            decal.material = material;
            decal.scaleMode = DecalScaleMode.ScaleInvariant;
            decal.size = new Vector3(DecalSize, DecalSize, 1.6f);
            decal.pivot = Vector3.zero;
            decal.drawDistance = 70f;
            GroundDecals.Paint(decal);
            decals.Add(decal);
        }

        DecalProjector found = decals[index];
        found.gameObject.SetActive(true);
        return found;
    }

    private int CellAt(Vector3 point)
    {
        int x = Mathf.FloorToInt((point.x - origin.x) / CellSize);
        int z = Mathf.FloorToInt((point.z - origin.z) / CellSize);
        return x < 0 || z < 0 || x >= Cells || z >= Cells ? -1 : z * Cells + x;
    }

    // ----- Kept with the game -----

    private void Keep()
    {
        // A friend's game shows its own; the host's is the one kept. Only into the game these paths were read from (leaving the
        // scene for a loaded or new game must not write them into it); not "isActiveAndEnabled", which is false in OnDisable,
        // so the wear since the last lay was lost on every raid (review 2026-11-16).
        if (!HavenVisit.IsVisiting && keptIn != null && keptIn == GameSession.Here)
        {
            keptIn.Footpaths = Pack(wear, KeptFrom);
        }
    }

    /// <summary>
    /// Every cell with at least <paramref name="from"/> wear, in cell order, as "cell:wear;" (wear in hundredths), the rest
    /// left out; to keep it short, a cell after the first is written as "+steps" on from the one before ("+1:12;"). Wear that
    /// doesn't show yet is kept too (review 2026-11-16): a path needs a dozen passes to show, and short stays at home between
    /// raids threw the beginnings away on every trip, so paths never formed.
    /// </summary>
    public static string Pack(float[] map, float from)
    {
        var packed = new StringBuilder();
        int least = Mathf.Max(1, Mathf.RoundToInt(from * 100f));
        int last = -1;
        for (int cell = 0; cell < map.Length; cell++)
        {
            int hundredths = Mathf.RoundToInt(map[cell] * 100f);
            if (hundredths >= least)
            {
                if (last >= 0)
                {
                    packed.Append('+').Append((cell - last).ToString(CultureInfo.InvariantCulture));
                }
                else
                {
                    packed.Append(cell.ToString(CultureInfo.InvariantCulture));
                }

                packed.Append(':').Append(hundredths.ToString(CultureInfo.InvariantCulture)).Append(';');
                last = cell;
            }
        }

        return packed.ToString();
    }

    /// <summary>The given cells as "cell:wear;" (wear in hundredths), the rest left out.</summary>
    public static string Pack(float[] map, IEnumerable<int> cells)
    {
        var packed = new StringBuilder();
        foreach (int cell in cells)
        {
            packed.Append(cell.ToString(CultureInfo.InvariantCulture)).Append(':')
                .Append(Mathf.RoundToInt(map[cell] * 100f).ToString(CultureInfo.InvariantCulture)).Append(';');
        }

        return packed.ToString();
    }

    private void Unpack(string packed)
    {
        Unpack(packed, wear, worn);
        changed = true;
    }

    /// <summary>Reads what <see cref="Pack(float[], float)"/> (or the older <see cref="Pack(float[], IEnumerable{int})"/>) wrote
    /// into <paramref name="map"/>; the cells worn enough to show go into <paramref name="showing"/>.</summary>
    public static void Unpack(string packed, float[] map, ICollection<int> showing)
    {
        if (string.IsNullOrEmpty(packed))
        {
            return;
        }

        int last = -1;
        foreach (string entry in packed.Split(';'))
        {
            int colon = entry.IndexOf(':');
            bool onFrom = entry.Length > 0 && entry[0] == '+';
            int start = onFrom ? 1 : 0;
            if (colon > start && int.TryParse(entry.Substring(start, colon - start), NumberStyles.Integer, CultureInfo.InvariantCulture, out int cell)
                && int.TryParse(entry.Substring(colon + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int hundredths))
            {
                cell = onFrom ? last + cell : cell;
                last = cell;
                if (cell < 0 || cell >= map.Length)
                {
                    continue;
                }

                map[cell] = hundredths / 100f;
                if (map[cell] >= ShowsFrom)
                {
                    showing?.Add(cell);
                }
            }
        }
    }
}
