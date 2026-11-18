using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The cold, in winter and on hard frosts: out in it the player slowly chills (faster at night, slower in gear with
/// Frost Resistance), and once the cold has got in they are Chilled (slower, less stamina) until they warm up at a fire,
/// a hearth or the sauna. Fires are any burning light out in the world (LightFlicker, not one carried) and the Haven's
/// homes with a hearth (HearthSmoke). Nothing happens in summer. Added in code to the player (Player.Awake).
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerWarmth : MonoBehaviour
{
    public const string ChillId = "season-chilled";
    private static readonly int SeasonId = Shader.PropertyToID("_SafeHavenSeason");
    private static readonly StatBonus[] Chill =
    {
        new StatBonus(StatType.MoveSpeed, -0.08f, StatModifierOp.Percent),
        new StatBonus(StatType.MaxStamina, -0.15f, StatModifierOp.Percent),
    };

    private const float ChillSeconds = 240f;   // out in the full cold, from warm to chilled
    private const float WarmSeconds = 25f;     // by a fire, from chilled to warm
    private const float RecoverSeconds = 120f; // out of the cold (a mild day), from chilled to warm
    private const float Tick = 0.5f;

    private readonly List<Vector3> heat = new List<Vector3>();
    private readonly List<float> heatReach = new List<float>();
    private PlayerBoons boons;
    private Combatant combatant;
    private float exposure;
    private bool chilled;
    private bool warned;
    private float nextTick;
    private float nextHeatScan;

    /// <summary>How far the cold has got in: 0 warm, 1 chilled.</summary>
    public float Exposure => exposure;

    public bool IsChilled => chilled;

    /// <summary>Standing by a fire, a hearth or in the sauna (rescanned every 10 s, in any season).</summary>
    public bool ByAFire => NearHeat();

    public static void AddTo(GameObject player)
    {
        if (player != null && !player.TryGetComponent(out PlayerWarmth _))
        {
            player.AddComponent<PlayerWarmth>();
        }
    }

    /// <summary>
    /// The exposure after <paramref name="seconds"/>: it rises with the cold (less with frost resistance), falls fast by
    /// a fire and slowly anywhere it isn't cold.
    /// </summary>
    public static float Advance(float exposure, float cold, float frostResistance, bool nearHeat, float seconds)
    {
        if (nearHeat)
        {
            return Mathf.Max(0f, exposure - seconds / WarmSeconds);
        }

        if (cold <= 0.05f)
        {
            return Mathf.Max(0f, exposure - seconds / RecoverSeconds);
        }

        float rate = cold * Mathf.Clamp01(1f - frostResistance) / ChillSeconds;
        return Mathf.Clamp(exposure + rate * seconds, 0f, 1.2f);
    }

    /// <summary>How cold it is out of doors now (0-1): breath's cold, and a quarter more at night.</summary>
    public static float ColdNow()
    {
        WeatherSystem weather = WeatherSystem.Active;
        Vector4 season = Shader.GetGlobalVector(SeasonId);
        float cold = ColdBreath.Coldness(weather != null ? weather.Snow : 0f, season.z, AtmosphereConditions.Morning);
        return Mathf.Clamp01(cold * (1f + 0.25f * NightSettings.Darkness));
    }

    private void Start()
    {
        boons = GetComponent<PlayerBoons>();
        combatant = GetComponent<Combatant>();
        // A chill doesn't outlast a reload: the cold has to get in again.
        if (boons != null)
        {
            boons.End(ChillId);
        }
    }

    private void Update()
    {
        if (Time.time < nextTick)
        {
            return;
        }

        nextTick = Time.time + Tick;
        if (Time.time >= nextHeatScan)
        {
            nextHeatScan = Time.time + 10f;
            ScanHeat();
        }

        float resistance = combatant != null && combatant.Stats != null ? combatant.Stats.Get(StatType.FrostResistance) : 0f;
        exposure = Advance(exposure, ColdNow(), resistance, NearHeat(), Tick);
        if (!warned && exposure >= 0.6f && !chilled)
        {
            warned = true;
            GameMessages.Post("You're getting cold.");
        }
        else if (warned && exposure < 0.3f)
        {
            warned = false;
        }

        if (!chilled && exposure >= 1f)
        {
            chilled = true;
            if (boons != null)
            {
                boons.Add(ChillId, "Chilled", Chill, 36000f, false);
            }

            GameMessages.Post("The cold has got into you. Warm yourself at a fire or a hearth.");
        }
        else if (chilled && exposure <= 0.2f)
        {
            chilled = false;
            if (boons != null)
            {
                boons.End(ChillId);
            }

            GameMessages.Post("You're warm again.");
        }
    }

    private bool NearHeat()
    {
        Vector3 at = transform.position;
        for (int i = 0; i < heat.Count; i++)
        {
            if ((heat[i] - at).sqrMagnitude <= heatReach[i] * heatReach[i])
            {
                return true;
            }
        }

        return false;
    }

    private void ScanHeat()
    {
        heat.Clear();
        heatReach.Clear();
        foreach (LightFlicker fire in FindObjectsByType<LightFlicker>())
        {
            // A torch or lantern someone carries isn't a fire to warm by.
            if (fire.GetComponentInParent<Player>() != null || fire.GetComponentInParent<UnityEngine.AI.NavMeshAgent>() != null)
            {
                continue;
            }

            Light light = fire.GetComponent<Light>();
            heat.Add(fire.transform.position);
            heatReach.Add(Mathf.Clamp(light != null ? light.range * 0.5f : 3f, 2.5f, 6f));
        }

        foreach (HearthSmoke hearth in FindObjectsByType<HearthSmoke>())
        {
            if (!hearth.Burning)
            {
                continue;
            }

            heat.Add(hearth.transform.position);
            heatReach.Add(hearth.name.Contains("Sauna") ? 9f : 7f);
        }
    }
}
