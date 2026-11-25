using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.VFX;
using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// Builds and edits Visual Effect Graph assets from code for the skill effects (Docs/CombatSkillsResearch_2026-11-24.md §6). VFX Graph's
/// editor model is internal: this assembly is named Unity.Testing.VisualEffectGraph.Editor, one of the names the package grants internal
/// access, so the tooling here can read a graph's systems and blocks, set their values and expose them as parameters. Editor-only; the
/// game reads the graphs through ordinary VisualEffect components and their exposed properties.
/// </summary>
public static class VfxGraphAuthoring
{
    /// <summary>The first block input of that name holding a direction, as a vector (for tests: the graph's types are internal). Null when none.</summary>
    public static Vector3? DirectionInput(string path, string slotName)
    {
        VisualEffectResource resource = VisualEffectResource.GetResourceAtPath(path);
        VFXGraph graph = resource?.GetGraph();
        if (graph == null)
        {
            return null;
        }

        foreach (VFXBlock block in graph.children.OfType<VFXContext>().SelectMany(context => context.children))
        {
            foreach (VFXSlot slot in block.inputSlots)
            {
                if (slot.name == slotName && slot.value is DirectionType direction)
                {
                    return direction.direction;
                }
            }
        }

        return null;
    }

    /// <summary>A graph's contexts, their blocks, and each one's settable inputs, for finding what to edit.</summary>
    public static string Describe(string path)
    {
        var sb = new StringBuilder();
        VisualEffectResource resource = VisualEffectResource.GetResourceAtPath(path);
        if (resource == null)
        {
            return "no VFX resource at " + path;
        }

        VFXGraph graph = resource.GetGraph() ?? resource.CreateGraph();
        foreach (VFXModel child in graph.children)
        {
            sb.AppendLine($"{child.GetType().Name} \"{(child as VFXContext)?.label}\"");
            if (child is VFXContext context)
            {
                foreach (VFXSlot slot in context.inputSlots)
                {
                    sb.AppendLine($"    in {slot.name}: {slot.property.type.Name} = {Value(slot)}");
                }

                foreach (VFXBlock block in context.children)
                {
                    sb.AppendLine($"  {block.GetType().Name} \"{block.name}\"" + string.Join("", block.inputSlots.Select(s => $"\n    in {s.name}: {s.property.type.Name} = {Value(s)}")));
                }
            }
            else if (child is VFXParameter parameter)
            {
                sb.AppendLine($"    exposed {parameter.exposedName}: {parameter.type.Name}");
            }
        }

        return sb.ToString();
    }

    private static string Value(VFXSlot slot)
    {
        if (slot.HasLink())
        {
            return "(linked)";
        }

        object value = slot.value;
        return value != null ? value.ToString() : "null";
    }
}

/// <summary>Lists what VFXLibrary offers (contexts, blocks, outputs), for writing the builder against this package version.</summary>
public static class VfxLibraryProbe
{
    public static string List(string filter)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var d in VFXLibrary.GetContexts())
        {
            if (string.IsNullOrEmpty(filter) || d.name.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0) sb.AppendLine("context: " + d.name + " | " + d.modelType.Name);
        }

        foreach (var d in VFXLibrary.GetBlocks())
        {
            if (string.IsNullOrEmpty(filter) || d.name.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0) sb.AppendLine("block: " + d.name + " | " + d.modelType.Name);
        }

        return sb.ToString();
    }
}
