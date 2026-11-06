using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Rivers running high with the thaw: in a season with a River Rise (the Season Cycle; spring), running water stands
/// that much higher, so the fords run deep and are swum rather than waded. Still water is left alone (it freezes and
/// thaws, WinterIce). Picks up water made after the scene starts (generated raid maps), and puts it all back after.
/// <para>Where it belongs: beside the Seasons, in the Haven and the raids (set up by AgentScripts/BuildSeasonalLife.cs).</para>
/// </summary>
public sealed class SpringMelt : MonoBehaviour
{
    [Tooltip("Metres per second the water rises or falls towards the season's level.")]
    [SerializeField, Min(0.001f)] private float speed = 0.05f;

    private readonly Dictionary<WaterSurface, float> rivers = new Dictionary<WaterSurface, float>();
    private float risen;
    private float nextScan;

    private void OnDisable()
    {
        foreach (KeyValuePair<WaterSurface, float> river in rivers)
        {
            if (river.Key != null)
            {
                Vector3 at = river.Key.transform.position;
                river.Key.transform.position = new Vector3(at.x, river.Value, at.z);
            }
        }

        rivers.Clear();
        risen = 0f;
    }

    private void Update()
    {
        if (Time.time >= nextScan)
        {
            nextScan = Time.time + 5f;
            foreach (WaterSurface water in WaterSurface.Surfaces)
            {
                if (water != null && !water.IsStill && !rivers.ContainsKey(water))
                {
                    rivers.Add(water, water.transform.position.y - risen);
                }
            }
        }

        float wanted = Seasons.RiverRise;
        if (Mathf.Approximately(wanted, risen))
        {
            return;
        }

        risen = Mathf.MoveTowards(risen, wanted, speed * Time.deltaTime);
        foreach (KeyValuePair<WaterSurface, float> river in rivers)
        {
            if (river.Key != null)
            {
                Vector3 at = river.Key.transform.position;
                river.Key.transform.position = new Vector3(at.x, river.Value + risen, at.z);
            }
        }
    }
}
