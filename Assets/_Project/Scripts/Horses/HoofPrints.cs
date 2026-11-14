using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hoof prints in the ground: each of a horse's four hooves presses a shod hoof's print into the terrain's footprint
/// field where it comes down (TerrainFootprints, the field the player's and walkers' prints go into), so a horse leaves a
/// trail that can be followed, and lasts in snow. A hoof comes down when it drops back to the ground after being lifted:
/// its height over the terrain under it, against the lowest it has stood. Only near the player, where the field keeps
/// prints (and never in water: PlayerTerrainFootprints.TryStamp). Added in code by HorseMotion and KnightSteed; a body
/// without the rig's hoof bones leaves none.
/// </summary>
[DisallowMultipleComponent]
public sealed class HoofPrints : MonoBehaviour
{
    // The Malbers horse rig's hoof bones: the front hooves are the "fingers", the hind ones the "toes".
    private static readonly string[] HoofBones = { "L Finger0", "R Finger0", "L Toe0", "R Toe0" };

    [Tooltip("How wide a hoof's print is (metres).")]
    [SerializeField, Min(0.05f)] private float hoofSize = 0.13f;
    [Tooltip("How far a hoof must lift (metres) before its next landing counts as a step.")]
    [SerializeField, Min(0.01f)] private float lift = 0.06f;
    [Tooltip("How close to its standing height a hoof must come back down to press a print (metres).")]
    [SerializeField, Min(0.005f)] private float landing = 0.025f;
    [Tooltip("Metres from the player within which it leaves prints (the field keeps about 12 m round them).")]
    [SerializeField, Min(2f)] private float printsWithin = 14f;

    private Horse horse;
    private Transform[] hooves;
    private float[] standing;
    private bool[] lifted;

    /// <summary>Adds hoof prints to a horse once.</summary>
    public static void AddTo(GameObject horse)
    {
        if (horse != null && !horse.TryGetComponent(out HoofPrints _))
        {
            horse.AddComponent<HoofPrints>();
        }
    }

    /// <summary>For tests: how many hooves it found.</summary>
    public int HoofCount => hooves != null ? hooves.Length : 0;

    private void Awake()
    {
        horse = GetComponent<Horse>();
        Transform[] all = GetComponentsInChildren<Transform>(true);
        var found = new System.Collections.Generic.List<Transform>(4);
        foreach (string name in HoofBones)
        {
            Transform bone = System.Array.Find(all, t => t.name == name);
            if (bone != null)
            {
                found.Add(bone);
            }
        }

        hooves = found.ToArray();
        standing = new float[hooves.Length];
        lifted = new bool[hooves.Length];
        for (int i = 0; i < standing.Length; i++)
        {
            standing[i] = float.MaxValue;
        }

        if (hooves.Length == 0)
        {
            enabled = false;
        }
    }

    private void LateUpdate()
    {
        Player player = Player.Current;
        if (player == null || (player.transform.position - transform.position).sqrMagnitude > printsWithin * printsWithin)
        {
            return;
        }

        Vector3 forward = transform.forward;
        // The player's own prints carry the field along with them; in the saddle their boots don't touch the ground, so
        // the horse they ride carries it instead.
        bool carries = horse != null && horse.Rider != null && horse.Rider.gameObject == player.gameObject;
        for (int i = 0; i < hooves.Length; i++)
        {
            Vector3 at = hooves[i].position;
            if (!TryGround(at, out float ground))
            {
                continue;
            }

            float height = at.y - ground;
            // The lowest it has stood is its height planted (the bone sits inside the hoof); it learns that as it goes,
            // and lets it rise slowly again so a bad reading (a hoof through a slope) doesn't stay.
            standing[i] = Mathf.Min(standing[i] + Time.deltaTime * 0.01f, height);
            if (height > standing[i] + lift)
            {
                lifted[i] = true;
            }
            else if (lifted[i] && height < standing[i] + landing)
            {
                lifted[i] = false;
                PlayerTerrainFootprints.TryStamp(new Vector3(at.x, ground, at.z), forward, carries, FootprintShape.HoofOf(hoofSize));
            }
        }
    }

    private static bool TryGround(Vector3 at, out float ground)
    {
        IReadOnlyList<Terrain> terrains = Terrains.Active;
        for (int i = 0; i < terrains.Count; i++)
        {
            Terrain terrain = terrains[i];
            if (terrain == null || terrain.terrainData == null)
            {
                continue;
            }

            Vector3 local = at - terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            if (local.x >= 0f && local.z >= 0f && local.x <= size.x && local.z <= size.z)
            {
                ground = terrain.SampleHeight(at) + terrain.transform.position.y;
                return true;
            }
        }

        ground = 0f;
        return false;
    }
}
