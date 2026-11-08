using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// What a build compiles, for cutting it down: when <see cref="Enabled"/>, every shader variant left after URP's own
/// stripping is counted per shader, per pass and per keyword, and written to Logs/ShaderVariants.txt when the build
/// ends. Runs last among the stripping steps, so it sees what really compiles. Off by default (AgentScripts/BuildBenchmark.cs
/// can switch it on for one build); it never strips anything itself.
/// </summary>
public sealed class ShaderVariantReport : IPreprocessShaders, IPostprocessBuildWithReport
{
    private sealed class Tally
    {
        public int Variants;
        public readonly Dictionary<string, int> Passes = new Dictionary<string, int>();
        public readonly Dictionary<string, int> Keywords = new Dictionary<string, int>();
    }

    private static readonly Dictionary<string, Tally> Shaders = new Dictionary<string, Tally>();

    /// <summary>Counts the next build's variants (cleared at the start, written at the end).</summary>
    public static bool Enabled { get; set; }

    public int callbackOrder => int.MaxValue;

    public static void Begin()
    {
        Shaders.Clear();
        Enabled = true;
    }

    public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
    {
        if (!Enabled || data.Count == 0)
        {
            return;
        }

        if (!Shaders.TryGetValue(shader.name, out Tally tally))
        {
            tally = Shaders[shader.name] = new Tally();
        }

        tally.Variants += data.Count;
        string pass = $"{snippet.passName} ({snippet.shaderType})";
        tally.Passes[pass] = (tally.Passes.TryGetValue(pass, out int p) ? p : 0) + data.Count;
        foreach (ShaderCompilerData variant in data)
        {
            foreach (ShaderKeyword keyword in variant.shaderKeywordSet.GetShaderKeywords())
            {
                string name = keyword.name;
                tally.Keywords[name] = (tally.Keywords.TryGetValue(name, out int k) ? k : 0) + 1;
            }
        }
    }

    public void OnPostprocessBuild(BuildReport report)
    {
        if (!Enabled)
        {
            return;
        }

        Enabled = false;
        var text = new StringBuilder();
        int total = Shaders.Values.Sum(t => t.Variants);
        text.AppendLine($"Shader variants left after stripping: {total} in {Shaders.Count} shaders ({report.summary.platform})");
        var keywordTotals = new Dictionary<string, int>();
        foreach (Tally tally in Shaders.Values)
        {
            foreach (KeyValuePair<string, int> pair in tally.Keywords)
            {
                keywordTotals[pair.Key] = (keywordTotals.TryGetValue(pair.Key, out int k) ? k : 0) + pair.Value;
            }
        }

        text.AppendLine("Keywords by how many variants carry them, all shaders:");
        foreach (KeyValuePair<string, int> pair in keywordTotals.OrderByDescending(p => p.Value).Take(60))
        {
            text.AppendLine($"  {pair.Key}: {pair.Value} ({100f * pair.Value / total:0}%)");
        }

        foreach (KeyValuePair<string, Tally> shader in Shaders.OrderByDescending(s => s.Value.Variants).Take(25))
        {
            text.AppendLine();
            text.AppendLine($"{shader.Key}: {shader.Value.Variants}");
            foreach (KeyValuePair<string, int> pass in shader.Value.Passes.OrderByDescending(p => p.Value).Take(8))
            {
                text.AppendLine($"  pass {pass.Key}: {pass.Value}");
            }

            foreach (KeyValuePair<string, int> keyword in shader.Value.Keywords.OrderByDescending(p => p.Value).Take(25))
            {
                text.AppendLine($"  {keyword.Key}: {keyword.Value} ({100f * keyword.Value / shader.Value.Variants:0}%)");
            }
        }

        string path = Path.Combine("Logs", "ShaderVariants.txt");
        File.WriteAllText(path, text.ToString());
        Debug.Log($"[ShaderVariantReport] {total} variants, written to {path}");
    }
}
