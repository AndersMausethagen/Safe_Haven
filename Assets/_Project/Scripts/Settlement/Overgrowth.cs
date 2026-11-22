using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Overgrowth choking Safe Haven's clearing at the start: brambles, fallen logs, old stumps, boulders. Each is a
/// gathering node that never grows back (Respawn Seconds 0), gathered like any other (by hand, or with an axe or
/// pickaxe of the right tier: the big stumps and boulders wait for better tools). While it stands it blocks building;
/// clearing it gives its materials and the ground heals with a breath of green motes. (A flat patch of dead ground under
/// each piece read as a see-through square on the grass and was removed, 2026-11-08.)
/// How much of the clearing is cleared adds to Safe Haven's renown (HavenRenown).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ResourceNode))]
[AddComponentMenu("Safe Haven/Settlement/Overgrowth")]
public sealed class Overgrowth : MonoBehaviour
{
    private static readonly List<Overgrowth> all = new List<Overgrowth>();

    // Entering Play doesn't reload scripts in this project, so statics are cleared when it starts.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => all.Clear();

    [Tooltip("Metres round it where green motes rise when it is cleared.")]
    [SerializeField, Min(0.3f)] private float blightRadius = 1.6f;

    private ResourceNode node;
    private ParticleSystem bloom;

    /// <summary>Raised when a piece of overgrowth is cleared.</summary>
    public static event Action<Overgrowth> Cleared;

    public static IReadOnlyList<Overgrowth> All => all;
    public bool IsCleared => node != null && node.IsDepleted;

    /// <summary>Share of the overgrowth in the loaded scene that is cleared, 0..1 (1 with none).</summary>
    public static float ClearedShare
    {
        get
        {
            if (all.Count == 0)
            {
                return 1f;
            }

            int cleared = 0;
            foreach (Overgrowth piece in all)
            {
                if (piece.IsCleared)
                {
                    cleared++;
                }
            }

            return cleared / (float)all.Count;
        }
    }

    private void Awake()
    {
        node = GetComponent<ResourceNode>();
    }

    private void OnEnable()
    {
        all.Add(this);
        node.Harvested += OnHarvested;
    }

    private void OnDisable()
    {
        all.Remove(this);
        node.Harvested -= OnHarvested;
    }

    private void OnHarvested()
    {
        // Cleared while the player was away (a mason's quarry ring): it counts, without the bloom on arrival.
        if (node == null || !node.BreaksQuietly)
        {
            Bloom();
        }

        Cleared?.Invoke(this);
    }

    // A ring of green motes rising where the ground heals.
    private void Bloom()
    {
        if (bloom == null)
        {
            var go = new GameObject("Healing Bloom");
            go.transform.SetParent(transform, false);
            bloom = go.AddComponent<ParticleSystem>();
            bloom.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = bloom.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 1.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.45f, 0.85f, 0.3f), new Color(0.85f, 0.95f, 0.45f));
            main.gravityModifier = -0.08f;
            ParticleSystem.EmissionModule emission = bloom.emission;
            emission.rateOverTime = 0f;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = ConstructionSettings.Current.DustMaterial;
        }

        for (int i = 0; i < 36; i++)
        {
            Vector2 ring = UnityEngine.Random.insideUnitCircle * blightRadius;
            bloom.Emit(new ParticleSystem.EmitParams
            {
                position = transform.position + new Vector3(ring.x, 0.1f, ring.y),
                velocity = Vector3.up * UnityEngine.Random.Range(0.3f, 0.9f),
                applyShapeToPosition = false,
            }, 1);
        }
    }
}
