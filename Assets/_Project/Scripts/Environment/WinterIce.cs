using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Still water freezes over in a snowing season: ponds and the lake turn to ice that can be walked on (to islands the
/// boat used to reach, or away from creatures, which keep to the shore), and thaw again when the snow goes. The ice
/// forms over a minute or so; a surface someone is swimming in waits until they are out. Rivers keep running. Frozen
/// water is not waded, swum, fished or drunk from (WaterSurface.IsFrozen), and is silent. A crack sounds as it sets
/// and as it goes, and now and then it creaks under the player.
/// <para>Where it belongs: once in the Haven scene, beside the Seasons (set up by AgentScripts/BuildWinter.cs).</para>
/// </summary>
public sealed class WinterIce : MonoBehaviour
{
    private static readonly int IceId = Shader.PropertyToID("_IceAmount");

    [Tooltip("Lying snow (0-1) from which still water freezes, in a snowing season.")]
    [SerializeField, Range(0f, 1f)] private float freezeFromSnow = 0.3f;
    [Tooltip("Seconds the ice takes to form, or to thaw.")]
    [SerializeField, Min(1f)] private float formSeconds = 60f;
    [SerializeField] private SoundDefinition crack;
    [SerializeField] private SoundDefinition creak;
    [Tooltip("Seconds between creaks while the player walks on ice.")]
    [SerializeField] private Vector2 creakEvery = new Vector2(6f, 14f);

    private readonly List<Frozen> waters = new List<Frozen>();
    private MaterialPropertyBlock block;
    private float nextCreak;

    private sealed class Frozen
    {
        public WaterSurface Water;
        public Renderer Renderer;
        public IceSheet Sheet;
        public float Amount;
    }

    public void Configure(SoundDefinition cracking, SoundDefinition creaking)
    {
        crack = cracking;
        creak = creaking;
    }

    /// <summary>How frozen still water is after <paramref name="deltaTime"/>: it sets while it is cold enough, else thaws.</summary>
    public static float Advance(float amount, bool cold, float deltaTime, float formSeconds) =>
        Mathf.MoveTowards(amount, cold ? 1f : 0f, deltaTime / Mathf.Max(formSeconds, 1e-3f));

    private float nextScan;

    private void Start()
    {
        block = new MaterialPropertyBlock();
        Scan();
    }

    // Still water made after the scene starts (a generated raid map's ponds and marsh pools) freezes too.
    private void Scan()
    {
        foreach (WaterSurface water in WaterSurface.Surfaces)
        {
            if (water != null && water.IsStill && !waters.Exists(frozen => frozen.Water == water))
            {
                waters.Add(new Frozen { Water = water, Renderer = water.GetComponent<Renderer>() });
            }
        }
    }

    private void OnDisable()
    {
        foreach (Frozen frozen in waters)
        {
            Apply(frozen, 0f);
        }
    }

    private void Update()
    {
        if (Time.time >= nextScan)
        {
            nextScan = Time.time + 5f;
            Scan();
        }

        WeatherSystem weather = WeatherSystem.Active;
        bool cold = weather != null && weather.Snowing && weather.Snow >= freezeFromSnow;
        Player player = Player.Current;
        PlayerSwimming swimming = player != null ? player.GetComponent<PlayerSwimming>() : null;
        foreach (Frozen frozen in waters)
        {
            if (frozen.Water == null)
            {
                continue;
            }

            bool someoneIn = cold && frozen.Amount < 0.8f && swimming != null && swimming.IsSwimming
                && frozen.Water.Contains(player.transform.position, 1f, out _);
            float next = someoneIn ? frozen.Amount : Advance(frozen.Amount, cold, Time.deltaTime, formSeconds);
            if (next != frozen.Amount)
            {
                bool wasSolid = frozen.Amount >= 0.8f;
                Apply(frozen, next);
                if (wasSolid != frozen.Amount >= 0.8f && player != null
                    && (frozen.Water.NearestPoint(player.transform.position) - player.transform.position).sqrMagnitude < 60f * 60f)
                {
                    AudioManager.Play(crack, frozen.Water.NearestPoint(player.transform.position));
                }
            }
        }

        Creak(player);
    }

    private void Apply(Frozen frozen, float amount)
    {
        frozen.Amount = amount;
        bool solid = amount >= 0.8f;
        frozen.Water.SetFrozen(solid);
        if (solid && frozen.Sheet == null)
        {
            frozen.Sheet = IceSheet.Make(frozen.Water);
        }

        if (frozen.Sheet != null)
        {
            frozen.Sheet.Solid = solid;
        }

        if (frozen.Renderer != null)
        {
            frozen.Renderer.GetPropertyBlock(block);
            block.SetFloat(IceId, Mathf.SmoothStep(0f, 1f, amount));
            frozen.Renderer.SetPropertyBlock(block);
        }
    }

    private void Creak(Player player)
    {
        if (creak == null || player == null || Time.time < nextCreak)
        {
            return;
        }

        nextCreak = Time.time + 1f;
        if (!Physics.Raycast(player.transform.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 1.5f, ~0, QueryTriggerInteraction.Ignore)
            || hit.collider.GetComponentInParent<IceSheet>() == null)
        {
            return;
        }

        nextCreak = Time.time + Random.Range(creakEvery.x, creakEvery.y);
        AudioManager.Play(creak, hit.point);
    }
}
