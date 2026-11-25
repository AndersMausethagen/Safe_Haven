using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.VFX;
using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// Writes a Visual Effect Graph from code (VfxGraphAuthoring explains the internal access): contexts and blocks by their library names
/// ("Spawn", "|Set|_Lifetime"), flows between contexts, inputs set by name, inputs exposed as named parameters (what a VisualEffect
/// component then overrides per skill), and saving. A graph made here is a reusable archetype (a burst, a mist, a ring); each skill's
/// look is its exposed values, set on its prefab (SkillFxBuilder).
/// </summary>
internal sealed class VfxBuilder
{
    private readonly VisualEffectResource resource;
    private readonly string path;
    private int nextParameterY;

    public VFXGraph Graph { get; }

    private VfxBuilder(string path, VisualEffectResource resource, VFXGraph graph)
    {
        this.path = path;
        this.resource = resource;
        Graph = graph;
    }

    /// <summary>A new, empty graph at <paramref name="path"/> (an existing one is replaced, keeping its GUID so prefabs keep it).</summary>
    public static VfxBuilder Create(string path)
    {
        if (AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(path) == null)
        {
            VisualEffectAssetEditorUtility.CreateNewAsset(path);
            AssetDatabase.ImportAsset(path);
        }

        VisualEffectResource resource = VisualEffectResource.GetResourceAtPath(path);
        VFXGraph graph = resource.GetGraph() ?? resource.CreateGraph();
        foreach (VFXModel child in graph.children.ToList())
        {
            graph.RemoveChild(child);
        }

        return new VfxBuilder(path, resource, graph);
    }

    /// <summary>A context by its library name ("Spawn", "Initialize Particle", "Update Particle", "Output Particle|Unlit|Quad").</summary>
    public VFXContext Context(string name, params (string setting, object value)[] settings)
    {
        var descriptor = VFXLibrary.GetContexts().FirstOrDefault(d => d.name == name)
                         ?? throw new ArgumentException("No VFX context named " + name);
        var context = (VFXContext)descriptor.CreateInstance();
        foreach ((string setting, object value) in settings)
        {
            context.SetSettingValue(setting, value);
        }

        Graph.AddChild(context);
        return context;
    }

    /// <summary>A block by its library name, added to <paramref name="context"/> with these settings.</summary>
    public VFXBlock Block(VFXContext context, string name, params (string setting, object value)[] settings)
    {
        var descriptor = VFXLibrary.GetBlocks().FirstOrDefault(d => d.name == name)
                         ?? throw new ArgumentException("No VFX block named " + name);
        VFXBlock block = descriptor.CreateInstance();
        foreach ((string setting, object value) in settings)
        {
            block.SetSettingValue(setting, value);
        }

        context.AddChild(block);
        return block;
    }

    /// <summary>Sets an enum setting by its value's name ("blendMode", "Additive"), whatever internal enum type it has.</summary>
    public static void SetEnum(VFXModel model, string setting, string value)
    {
        System.Reflection.FieldInfo field = null;
        for (Type type = model.GetType(); type != null && field == null; type = type.BaseType)
        {
            field = type.GetField(setting, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        }

        if (field == null || !field.FieldType.IsEnum)
        {
            throw new ArgumentException($"No enum setting {setting} on {model.GetType().Name}");
        }

        model.SetSettingValue(setting, Enum.Parse(field.FieldType, value));
    }

    /// <summary>Particles flow from <paramref name="from"/> to <paramref name="to"/>.</summary>
    public static void Flow(VFXContext from, VFXContext to) => from.LinkTo(to);

    /// <summary>The input named <paramref name="name"/> (searched through sub-slots: "radius" inside a sphere).</summary>
    public static VFXSlot Slot(VFXModel model, string name)
    {
        IEnumerable<VFXSlot> slots = model is VFXContext context ? context.inputSlots : model is VFXBlock block ? block.inputSlots : Enumerable.Empty<VFXSlot>();
        foreach (VFXSlot slot in slots)
        {
            VFXSlot found = Find(slot, name);
            if (found != null)
            {
                return found;
            }
        }

        throw new ArgumentException($"No input {name} on {model.name} (has {string.Join(", ", slots.Select(s => s.name))})");
    }

    private static VFXSlot Find(VFXSlot slot, string name)
    {
        if (string.Equals(slot.name, name, StringComparison.OrdinalIgnoreCase))
        {
            return slot;
        }

        foreach (VFXSlot child in slot.children)
        {
            VFXSlot found = Find(child, name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>Sets an input's value.</summary>
    /// <summary>Sets an input slot's value. Throws when the slot ignored it (a Vector3 given to a DirectionType slot is dropped silently).</summary>
    public static void Set(VFXModel model, string slot, object value)
    {
        VFXSlot target = Slot(model, slot);
        target.value = value;
        // Value types only: a slot keeps its own copy of a curve or gradient.
        if (value is ValueType && !Equals(target.value, value))
        {
            throw new ArgumentException($"{model.GetType().Name}.{slot} kept {target.value} instead of {value}: give it a {target.property.type.Name}");
        }
    }

    /// <summary>Exposes a named parameter of <paramref name="value"/>'s type, linked to these inputs, so a VisualEffect can override it.</summary>
    public VFXParameter Expose(string name, object value, params VFXSlot[] targets)
    {
        Type type = value.GetType();
        var parameter = ScriptableObject.CreateInstance<VFXParameter>();
        parameter.Init(type);
        parameter.SetSettingValue("m_ExposedName", name);
        parameter.SetSettingValue("m_Exposed", true);
        parameter.value = value;
        parameter.position = new Vector2(-400f, nextParameterY);
        nextParameterY += 80;
        Graph.AddChild(parameter);
        foreach (VFXSlot target in targets)
        {
            parameter.outputSlots[0].Link(target);
        }

        return parameter;
    }

    /// <summary>Lays the contexts out top to bottom (for anyone opening the graph) and writes the asset.</summary>
    public void Save()
    {
        int y = 0;
        foreach (VFXContext context in Graph.children.OfType<VFXContext>())
        {
            context.position = new Vector2(0f, y);
            y += 220 + 60 * context.children.Count();
        }

        resource.WriteAssetWithSubAssets();
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
    }
}
