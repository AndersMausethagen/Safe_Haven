using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Checks for the World Builder (`unity command run_script --file AgentScripts/ProbeWorld.cs --entry ProbeWorld.<Step>`).</summary>
public static class ProbeWorld
{
    public static string Shaders()
    {
        var sb = new System.Text.StringBuilder();
        foreach (string name in new[] { "Safe Haven/Terrain (World)", "Safe Haven/Water" })
        {
            Shader shader = Shader.Find(name);
            if (shader == null)
            {
                sb.Append(name + ": missing\n");
                continue;
            }

            ShaderMessage[] messages = ShaderUtil.GetShaderMessages(shader);
            sb.Append($"{name}: supported={shader.isSupported} messages={messages.Length}\n");
            foreach (ShaderMessage m in messages.Take(8))
            {
                sb.Append($"  {m.severity} {m.message} ({m.file}:{m.line}) {m.platform}\n");
            }
        }

        return sb.ToString();
    }

    public static string Assets() => WorldAssets.EnsureAll();
}
