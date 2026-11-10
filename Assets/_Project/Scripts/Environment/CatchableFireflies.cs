using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The fireflies that can be caught, for a lantern: a few real ones among the swarm (FireflyProfile › Caught), more at
/// night, fewer in rain. They gather round lights (a campfire, a lantern post, the player's own lantern) and now and then
/// one drifts near the player. Each hovers about its spot, blinking, and Interact catches it into the bag. After a while
/// an uncaught one flies off, and a new one turns up somewhere else.
/// <para>
/// Cheap by design: at most a handful, pooled, and this one Update moves and blinks them all and decides where the next
/// one appears every few seconds; nothing else runs per firefly. The interaction stays on a still anchor (the
/// InteractableGrid files it there) while the glow wanders round it. Made by <see cref="Fireflies"/>.
/// </para>
/// </summary>
public sealed class CatchableFireflies : MonoBehaviour
{
    private const float CatchReach = 2.2f;
    private const float LeaveDistance = 40f;
    private const float NearPlayerMin = 3f;
    private const float NearPlayerMax = 6f;
    private const float LightSearch = 22f;

    private sealed class Fly
    {
        public GameObject Root;
        public Transform Glow;
        public Light Light;
        public Renderer Speck;
        public float Until;
        public float Seed;
        public string InteractionName;
    }

    private static readonly List<Vector3> LightSpots = new List<Vector3>();
    private readonly List<Fly> flies = new List<Fly>();
    private FireflyProfile profile;
    private MaterialPropertyBlock block;
    private float nextSpawn;

    public void Initialise(FireflyProfile fireflies)
    {
        profile = fireflies;
        block = new MaterialPropertyBlock();
        nextSpawn = Time.time + 3f;
    }

    /// <summary>A fire, lamp or lantern near <paramref name="point"/> (within <paramref name="radius"/> metres, at random),
    /// for fireflies to gather round. False when there is none.</summary>
    public static bool TryLightNear(Vector3 point, float radius, out Vector3 light)
    {
        LightSpots.Clear();
        float most = radius * radius;
        WorldLights world = WorldLights.Current;
        if (world != null)
        {
            foreach (Light each in world.Lights)
            {
                if (each != null && each.isActiveAndEnabled && Flat(each.transform.position - point).sqrMagnitude <= most)
                {
                    LightSpots.Add(each.transform.position);
                }
            }
        }

        Light lantern = PlayerLantern.Current != null ? PlayerLantern.Current.Glow : null;
        if (lantern != null && Flat(lantern.transform.position - point).sqrMagnitude <= most)
        {
            // The player's own light counts double: it is what they are looking at.
            LightSpots.Add(lantern.transform.position);
            LightSpots.Add(lantern.transform.position);
        }

        light = LightSpots.Count > 0 ? LightSpots[Random.Range(0, LightSpots.Count)] : default;
        return LightSpots.Count > 0;
    }

    private void Update()
    {
        Player player = Player.Current;
        if (profile == null || player == null)
        {
            return;
        }

        Vector3 home = player.transform.position;
        int alive = 0;
        foreach (Fly fly in flies)
        {
            if (!fly.Root.activeSelf)
            {
                continue;
            }

            if (Time.time > fly.Until || Flat(fly.Root.transform.position - home).sqrMagnitude > LeaveDistance * LeaveDistance)
            {
                fly.Root.SetActive(false);
                continue;
            }

            alive++;
            Animate(fly);
        }

        int wanted = Mathf.RoundToInt(profile.CatchableFor(AtmosphereConditions.Night, AtmosphereConditions.WeatherRain) * Seasons.Fireflies);
        if (alive < wanted && Time.time >= nextSpawn)
        {
            nextSpawn = Time.time + Random.Range(4f, 9f);
            Spawn(home);
        }
    }

    // Hovering round its spot and blinking, as the swarm does: lit, dark, lit.
    private void Animate(Fly fly)
    {
        float t = Time.time * 0.45f + fly.Seed;
        float x = Mathf.PerlinNoise(t, 0.3f) - 0.5f;
        float y = (Mathf.PerlinNoise(0.7f, t) - 0.5f) * 0.6f;
        float z = Mathf.PerlinNoise(t * 0.8f, t) - 0.5f;
        fly.Glow.localPosition = new Vector3(x, y, z) * 1.6f;
        float blink = Mathf.SmoothStep(0.15f, 1f, Mathf.PerlinNoise(Time.time * 1.7f, fly.Seed * 2f));
        fly.Light.intensity = 0.7f * blink;
        fly.Speck.GetPropertyBlock(block);
        block.SetColor("_BaseColor", profile.Colour * (0.6f + 5f * blink));
        fly.Speck.SetPropertyBlock(block);
    }

    private void Spawn(Vector3 player)
    {
        Vector3 spot;
        if (Random.value >= profile.NearPlayerShare && TryLightNear(player, LightSearch, out Vector3 light))
        {
            Vector2 round = Random.insideUnitCircle.normalized * Random.Range(profile.LightDistance.x, profile.LightDistance.y);
            spot = light + new Vector3(round.x, 0f, round.y);
        }
        else
        {
            Vector2 round = Random.insideUnitCircle.normalized * Random.Range(NearPlayerMin, NearPlayerMax);
            spot = player + new Vector3(round.x, 0f, round.y);
        }

        spot.y = Fireflies.GroundUnder(spot, player.y) + Random.Range(0.9f, 1.6f);
        Fly fly = Take();
        fly.Root.transform.position = spot;
        fly.Until = Time.time + Random.Range(profile.CatchableLife.x, profile.CatchableLife.y);
        fly.Seed = Random.value * 100f;
        fly.InteractionName = profile.Catchable.DisplayName;
        fly.Root.SetActive(true);
        Animate(fly);
    }

    private Fly Take()
    {
        foreach (Fly idle in flies)
        {
            if (!idle.Root.activeSelf)
            {
                return idle;
            }
        }

        var root = new GameObject("Firefly (catch)");
        root.SetActive(false);
        root.transform.SetParent(transform, false);
        var fly = new Fly { Root = root };
        root.AddComponent<FlyName>().Fly = fly;

        GameObject speck = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(speck.GetComponent<Collider>());
        speck.name = "Glow";
        speck.transform.SetParent(root.transform, false);
        speck.transform.localScale = Vector3.one * 0.07f;
        fly.Glow = speck.transform;
        fly.Speck = speck.GetComponent<Renderer>();
        fly.Speck.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (profile.CatchableGlow != null)
        {
            fly.Speck.sharedMaterial = profile.CatchableGlow;
        }

        fly.Light = speck.AddComponent<Light>();
        fly.Light.type = LightType.Point;
        fly.Light.color = profile.Colour;
        fly.Light.range = 2.5f;
        fly.Light.shadows = LightShadows.None;

        var interactable = root.AddComponent<Interactable>();
        interactable.Configure(InteractionAnimation.GatherHigh, 0.5f, CatchReach, true);
        interactable.Completed.AddListener(() => Caught(interactable));
        flies.Add(fly);
        return fly;
    }

    private void Caught(Interactable interactable)
    {
        GameObject catcher = interactable.Interactor;
        ItemInventory bag = catcher != null ? catcher.GetComponent<ItemInventory>() : null;
        if (bag == null)
        {
            return;
        }

        if (bag.Give(profile.Catchable, 1) < 1)
        {
            GameMessages.Post($"No room for the {profile.Catchable.DisplayName.ToLowerInvariant()}. It drifts away.", MessageKind.Warning);
        }
    }

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    /// <summary>Gives a catchable firefly its name and verb in the interaction prompt ("[E] Catch Firefly").</summary>
    private sealed class FlyName : MonoBehaviour, IInteractionName, IInteractionVerb
    {
        [System.NonSerialized] public Fly Fly;
        public string InteractionName => Fly != null ? Fly.InteractionName : "Firefly";
        public string Verb => "Catch";
    }

    // Entering Play doesn't reload scripts in this project: statics carry over from the last session unless cleared.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        LightSpots.Clear();
    }
}
