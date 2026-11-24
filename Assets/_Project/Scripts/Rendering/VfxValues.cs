using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// A skill effect's own look on a shared graph (Docs/CombatSkillsResearch_2026-11-24.md §6): the values it sets on its VisualEffect's
/// exposed properties (Skill Burst's Count, Color, Texture...), applied each time it is played. The graphs are reusable archetypes
/// (Art/Effects/Skills/Graphs); the prefabs that carry this (Art/Effects/Skills/Prefabs, made by SkillFxPrefabs) are a frost burst, an
/// ember spray, a shockwave. A name the graph doesn't expose is skipped.
/// </summary>
[RequireComponent(typeof(VisualEffect))]
public sealed class VfxValues : MonoBehaviour
{
    [Serializable]
    public struct FloatValue
    {
        public string name;
        public float value;
    }

    [Serializable]
    public struct VectorValue
    {
        public string name;
        public Vector3 value;
    }

    [Serializable]
    public struct ObjectValue
    {
        public string name;
        public UnityEngine.Object value;
    }

    [SerializeField] private List<FloatValue> floats = new List<FloatValue>();
    [SerializeField] private List<VectorValue> vectors = new List<VectorValue>();
    [SerializeField] private List<ObjectValue> objects = new List<ObjectValue>();

    public List<FloatValue> Floats => floats;
    public List<VectorValue> Vectors => vectors;
    public List<ObjectValue> Objects => objects;

    /// <summary>Sets every value the graph exposes on <paramref name="effect"/>.</summary>
    public void Apply(VisualEffect effect)
    {
        foreach (FloatValue entry in floats)
        {
            if (effect.HasFloat(entry.name))
            {
                effect.SetFloat(entry.name, entry.value);
            }
        }

        foreach (VectorValue entry in vectors)
        {
            if (effect.HasVector3(entry.name))
            {
                effect.SetVector3(entry.name, entry.value);
            }
            else if (effect.HasVector2(entry.name))
            {
                effect.SetVector2(entry.name, entry.value);
            }
        }

        foreach (ObjectValue entry in objects)
        {
            if (entry.value is Texture texture && effect.HasTexture(entry.name))
            {
                effect.SetTexture(entry.name, texture);
            }
            else if (entry.value is Mesh mesh && effect.HasMesh(entry.name))
            {
                effect.SetMesh(entry.name, mesh);
            }
        }
    }
}
