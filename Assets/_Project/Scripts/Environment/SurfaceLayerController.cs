using System;
using UnityEngine;

/// <summary>Drives the live surface layers of "Safe Haven/Layered Lit" (e.g. moss, rust) per object, without
/// touching the shared material: SetAmount("Moss", 1) shows the layer, 0 hides it, FadeTo grows or recedes it over
/// time. Values go through a MaterialPropertyBlock on every renderer in <see cref="renderers"/> (children when empty).
/// The layer's textures, tiling and growth rules stay on the material.</summary>
[DisallowMultipleComponent]
public sealed class SurfaceLayerController : MonoBehaviour
{
    public const int LayerCount = 2;

    [Serializable]
    private sealed class Layer
    {
        [Tooltip("Name used by SetAmount/FadeTo, e.g. Moss or Rust.")]
        public string name;
        [Range(0f, 1f)] public float amount;
    }

    [Tooltip("Renderers using Layered Lit. Empty: every renderer on this object and its children.")]
    [SerializeField] private Renderer[] renderers = Array.Empty<Renderer>();
    [Tooltip("Layer 1 and Layer 2 of the material, in that order.")]
    [SerializeField] private Layer[] layers = { new Layer { name = "Moss" }, new Layer { name = "Rust" } };

    private static readonly int[] AmountIds =
    {
        Shader.PropertyToID("_L1Amount"), Shader.PropertyToID("_L2Amount"),
    };

    private readonly float[] fadeTargets = new float[LayerCount];
    private readonly float[] fadeSpeeds = new float[LayerCount];
    private MaterialPropertyBlock block;

    /// <summary>Raised after a layer's amount changes: (layer index, new amount).</summary>
    public event Action<int, float> AmountChanged;

    public bool IsFading(string layerName) => IsFading(IndexOf(layerName));

    public bool IsFading(int index) => IsValid(index) && fadeSpeeds[index] > 0f;

    public float GetAmount(string layerName) => GetAmount(IndexOf(layerName));

    public float GetAmount(int index) => IsValid(index) ? layers[index].amount : 0f;

    /// <summary>Index of the layer with this name (case-insensitive), or -1. The material's SurfaceLayer1 /
    /// SurfaceLayer2 tags (set on import for LocalSpawn models) win over this component's names, so a controller
    /// added from code finds "Moss" wherever the model put it.</summary>
    public int IndexOf(string layerName)
    {
        Material material = FirstMaterial();
        for (int i = 0; material != null && i < LayerCount; i++)
        {
            if (string.Equals(material.GetTag(LayerTag(i), false), layerName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        for (int i = 0; i < layers.Length && i < LayerCount; i++)
        {
            if (string.Equals(layers[i].name, layerName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static string LayerTag(int index) => "SurfaceLayer" + (index + 1);

    private Material FirstMaterial()
    {
        Renderer source = renderers != null && renderers.Length > 0 ? renderers[0] : GetComponentInChildren<Renderer>(true);
        return source != null ? source.sharedMaterial : null;
    }

    public void SetAmount(string layerName, float amount) => SetAmount(Require(layerName), amount);

    /// <summary>Sets the amount immediately (0 hidden, 1 fully grown) and cancels a running fade.</summary>
    public void SetAmount(int index, float amount)
    {
        if (!IsValid(index))
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "Layered Lit has layers 0 and 1.");
        }

        fadeSpeeds[index] = 0f;
        Write(index, amount);
    }

    public void FadeTo(string layerName, float amount, float seconds) => FadeTo(Require(layerName), amount, seconds);

    /// <summary>Moves the amount to <paramref name="amount"/> at a constant rate over <paramref name="seconds"/>
    /// (scaled time).</summary>
    public void FadeTo(int index, float amount, float seconds)
    {
        if (!IsValid(index))
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "Layered Lit has layers 0 and 1.");
        }

        amount = Mathf.Clamp01(amount);
        float distance = Mathf.Abs(amount - layers[index].amount);
        if (seconds <= 0f || distance <= 0f)
        {
            SetAmount(index, amount);
            return;
        }

        fadeTargets[index] = amount;
        fadeSpeeds[index] = distance / seconds;
    }

    private void Awake()
    {
        if (renderers == null || renderers.Length == 0)
        {
            renderers = GetComponentsInChildren<Renderer>(true);
        }
    }

    private void OnEnable() => ApplyAll();

    private void Update()
    {
        for (int i = 0; i < LayerCount && i < layers.Length; i++)
        {
            if (fadeSpeeds[i] <= 0f)
            {
                continue;
            }

            float next = Mathf.MoveTowards(layers[i].amount, fadeTargets[i], fadeSpeeds[i] * Time.deltaTime);
            if (Mathf.Approximately(next, fadeTargets[i]))
            {
                next = fadeTargets[i];
                fadeSpeeds[i] = 0f;
            }

            Write(i, next);
        }
    }

    private void Reset()
    {
        renderers = Array.Empty<Renderer>();
        var source = GetComponentInChildren<Renderer>();
        Material material = source != null ? source.sharedMaterial : null;
        for (int i = 0; i < LayerCount && i < layers.Length; i++)
        {
            if (material != null && material.HasFloat(AmountIds[i]))
            {
                layers[i].amount = material.GetFloat(AmountIds[i]);
            }

            string tagged = material != null ? material.GetTag(LayerTag(i), false) : "";
            if (!string.IsNullOrEmpty(tagged))
            {
                layers[i].name = tagged; // the layer names LocalSpawn exported (e.g. Moss, Rust)
            }
        }
    }

    private void OnValidate()
    {
        if (layers == null || layers.Length != LayerCount)
        {
            Array.Resize(ref layers, LayerCount);
            for (int i = 0; i < LayerCount; i++)
            {
                layers[i] ??= new Layer { name = $"Layer {i + 1}" };
            }
        }

        ApplyAll();
    }

    private void Write(int index, float amount)
    {
        amount = Mathf.Clamp01(amount);
        if (Mathf.Approximately(amount, layers[index].amount) && block != null)
        {
            return;
        }

        layers[index].amount = amount;
        ApplyAll();
        AmountChanged?.Invoke(index, amount);
    }

    private void ApplyAll()
    {
        Renderer[] targets = renderers != null && renderers.Length > 0
            ? renderers
            : GetComponentsInChildren<Renderer>(true);
        block ??= new MaterialPropertyBlock();
        foreach (Renderer target in targets)
        {
            if (target == null)
            {
                continue;
            }

            target.GetPropertyBlock(block);
            for (int i = 0; i < LayerCount && i < layers.Length; i++)
            {
                block.SetFloat(AmountIds[i], layers[i].amount);
            }

            target.SetPropertyBlock(block);
        }
    }

    private int Require(string layerName)
    {
        int index = IndexOf(layerName);
        if (index < 0)
        {
            throw new ArgumentException($"No surface layer named '{layerName}' on {name}.", nameof(layerName));
        }

        return index;
    }

    private bool IsValid(int index) => index >= 0 && index < LayerCount && index < layers.Length;
}
