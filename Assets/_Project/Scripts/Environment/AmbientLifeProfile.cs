using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// What lives in a place's air between fights (AmbientLife, Docs/LookAndFeelResearch_2026-11-18.md V2, V4): swarms of motes, pollen,
/// midges, dragonflies or moths kept round the view, debris a gust carries across it, and now and then a big bird's shadow sliding over
/// the ground. One per biome; purely visual.
/// </summary>
[CreateAssetMenu(menuName = "Safe Haven/Environment/Ambient Life Profile", fileName = "Ambient Life")]
public sealed class AmbientLifeProfile : ScriptableObject
{
    /// <summary>Where a swarm's members are set down.</summary>
    public enum Place
    {
        /// <summary>Anywhere round the view, over the ground.</summary>
        Anywhere,
        /// <summary>Over water that shows (a pond, the fen where it lies open, a river).</summary>
        OverWater,
        /// <summary>Round a fire, a lamp or a lantern near the view (moths).</summary>
        NearLights,
    }

    [Serializable]
    public sealed class Swarm
    {
        public string name = "Motes";
        [Tooltip("An Ambient Mote material: its shape (dot, leaf, dart) and how it glints.")]
        public Material material;
        [Tooltip("Each one's colour is picked between these two; alpha is how solid it is.")]
        public Color colour = new Color(1f, 0.96f, 0.85f, 0.5f);
        public Color colour2 = new Color(1f, 0.9f, 0.7f, 0.35f);
        [Tooltip("Metres across.")]
        public Vector2 size = new Vector2(0.03f, 0.06f);
        [Tooltip("Metres over the ground (or the water, or the light's height for Near Lights).")]
        public Vector2 height = new Vector2(0.5f, 3f);
        [Tooltip("Seconds each lives.")]
        public Vector2 life = new Vector2(5f, 9f);
        [Tooltip("How many at once by day and by night (lerped by the night's darkness).")]
        [Min(0)] public int byDay = 30;
        [Min(0)] public int byNight;
        [Tooltip("The share left in full rain.")]
        [Range(0f, 1f)] public float inRain = 0.2f;
        [Tooltip("Metres round the view they are kept in.")]
        [Min(1f)] public float radius = 16f;
        [Tooltip("How far each wanders by itself (metres a second), and how quickly it changes its mind.")]
        [Min(0f)] public float wander = 0.3f;
        [Min(0.05f)] public float wanderFrequency = 0.4f;
        [Tooltip("The share of the wind's speed it drifts with.")]
        [Range(0f, 1f)] public float withWind = 0.3f;
        public Place place = Place.Anywhere;
        [Tooltip("Above 0: gathered in this many columns (midges over water), each Column Radius across.")]
        [Min(0)] public int columns;
        [Min(0.1f)] public float columnRadius = 0.6f;
        [Tooltip("Drawn lying flat, as seen from above (a dragonfly with its wings out), not facing the camera.")]
        [FormerlySerializedAs("stretch")] public bool lieFlat;
        [Tooltip("Degrees a second it tumbles (leaves and needles).")]
        [Min(0f)] public float spin;
        [Header("Carried by gusts (instead of kept at a count)")]
        [Tooltip("A gust's worth at a time, carried across the view from upwind at the gust's speed, as the grass waves roll.")]
        public bool gusts;
        public Vector2 gustEvery = new Vector2(6f, 14f);
        [Min(1)] public int perGust = 24;
    }

    [Serializable]
    public sealed class Birds
    {
        [Tooltip("A big bird's shadow sliding over the ground now and then: scale from above (the bird itself flies too high to see).")]
        public bool enabled;
        [Tooltip("A shadow-casting material (URP Lit); the bird draws only its shadow.")]
        public Material shadowMaterial;
        public Vector2 every = new Vector2(25f, 70f);
        [Tooltip("Metres over the ground.")]
        public Vector2 altitude = new Vector2(22f, 36f);
        [Tooltip("Metres a second.")]
        public Vector2 speed = new Vector2(8f, 12f);
        [Tooltip("Wingspan, metres.")]
        [Min(0.3f)] public float span = 1.9f;
        [Range(1, 4)] public int mostInFlock = 2;
    }

    [SerializeField] private List<Swarm> swarms = new List<Swarm>();
    [SerializeField] private Birds birds = new Birds();

    public IReadOnlyList<Swarm> Swarms => swarms;
    public Birds BirdShadows => birds;

    public void Configure(IEnumerable<Swarm> with, Birds flying)
    {
        swarms = new List<Swarm>(with);
        birds = flying ?? new Birds();
    }

    /// <summary>How many of a swarm should be up at this darkness (0 day, 1 night) and rain (0-1). Pure, for tests.</summary>
    public static int CountFor(Swarm swarm, float night, float rain)
    {
        if (swarm == null || swarm.gusts)
        {
            return 0;
        }

        float count = Mathf.Lerp(swarm.byDay, swarm.byNight, Mathf.Clamp01(night)) * Mathf.Lerp(1f, swarm.inRain, Mathf.Clamp01(rain));
        return Mathf.RoundToInt(count);
    }
}
