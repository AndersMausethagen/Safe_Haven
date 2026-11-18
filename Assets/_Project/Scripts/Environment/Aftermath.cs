using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A fight leaves its mark: where a creature fell the grass lies flattened for a while, and before long the crows come
/// down to it. Near enough to the player only (a few spots at a time); nothing is saved.
/// <para>Where it belongs: once in a scene with creatures (the Haven world, raid maps).</para>
/// </summary>
public sealed class Aftermath : MonoBehaviour
{
    [Tooltip("Metres round the body the grass lies flat, and for how many seconds.")]
    [SerializeField, Min(0.3f)] private float flattened = 1.8f;
    [SerializeField, Min(5f)] private float lasts = 150f;
    [Tooltip("Seconds after the fight before the crows come, least and most; and the chance they do.")]
    [SerializeField] private Vector2 crowsAfter = new Vector2(12f, 30f);
    [SerializeField, Range(0f, 1f)] private float crowChance = 0.6f;
    [SerializeField, Min(1)] private int atOnce = 3;

    private readonly List<(GrassInteractor spot, float until)> spots = new List<(GrassInteractor, float)>();
    private readonly List<(Vector3 at, float when)> crows = new List<(Vector3, float)>();

    private void OnEnable() => Damageable.AnyDied += OnDied;

    private void OnDisable() => Damageable.AnyDied -= OnDied;

    private void OnDied(Damageable died, DamageInfo info)
    {
        if (died == null || died.GetComponent<MonsterAI>() == null)
        {
            return;
        }

        Vector3 at = died.transform.position;
        if (spots.Count >= atOnce)
        {
            Remove(0);
        }

        var holder = new GameObject("Trampled Grass");
        holder.transform.SetParent(transform, false);
        holder.transform.position = at;
        GrassInteractor spot = holder.AddComponent<GrassInteractor>();
        spot.Radius = flattened;
        spots.Add((spot, Time.time + lasts));
        if (Random.value < crowChance)
        {
            crows.Add((at + new Vector3(Random.Range(-2f, 2f), 0f, Random.Range(-2f, 2f)), Time.time + Random.Range(crowsAfter.x, crowsAfter.y)));
        }
    }

    private void Update()
    {
        for (int i = spots.Count - 1; i >= 0; i--)
        {
            if (Time.time >= spots[i].until)
            {
                Remove(i);
            }
        }

        for (int i = crows.Count - 1; i >= 0; i--)
        {
            if (Time.time < crows[i].when)
            {
                continue;
            }

            // Only while nothing is fighting there: crows don't land in a fight.
            if (!MonsterAI.AnyHuntingPlayerSide())
            {
                if (Birds.Current != null)
                {
                    Birds.Current.LandAt(crows[i].at);
                }
            }

            crows.RemoveAt(i);
        }
    }

    private void Remove(int index)
    {
        if (spots[index].spot != null)
        {
            Destroy(spots[index].spot.gameObject);
        }

        spots.RemoveAt(index);
    }
}
