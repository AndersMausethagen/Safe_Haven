using System.Collections.Generic;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Builds of "Safe Haven/Lit (See-Through)" leave out the variants the game never draws, on top of URP's own stripping.
/// Its ForwardLit fragment pass kept 288 variants per material keyword set (4,896 in all, the build's slowest shader to
/// compile): fog 3 x instancing 3 x crossfade 2 x cookies 2 x ambient occlusion 2 x soft shadows 2 x shadow cascades 2.
/// Three of those never happen for it:
/// <list type="bullet">
/// <item>INSTANCING_ON: its renderers are GameObjects drawn by the GPU Resident Drawer (DOTS_INSTANCING_ON, as the recorded
/// warm-up shows) or the SRP Batcher; nothing draws its materials instanced (no terrain tree prototype, no
/// RenderMeshInstanced). A material's "Enable GPU Instancing" tick would otherwise keep a third set of variants.</item>
/// <item>LOD_FADE_CROSSFADE: no LOD group that cross-fades uses it (the cross-fading ones are plants and saplings on their
/// own shaders); SeeThroughVariantTests holds that.</item>
/// <item>Main-light shadows without soft shadows: every shadow-casting light is soft (scenes, prefabs, LightShadowBudget).</item>
/// </list>
/// A variant left out is never an error: a draw that asked for it would take the nearest kept one.
/// </summary>
public sealed class SeeThroughVariantStripper : IPreprocessShaders
{
    public const string ShaderName = "Safe Haven/Lit (See-Through)";

    // Before ShaderVariantReport (int.MaxValue), so its counts show what really compiles.
    public int callbackOrder => 100;

    public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
    {
        if (shader == null || shader.name != ShaderName || data.Count == 0)
        {
            return;
        }

        var instancing = new ShaderKeyword(shader, "INSTANCING_ON");
        var crossfade = new ShaderKeyword(shader, "LOD_FADE_CROSSFADE");
        var mainShadows = new ShaderKeyword(shader, "_MAIN_LIGHT_SHADOWS");
        var cascadeShadows = new ShaderKeyword(shader, "_MAIN_LIGHT_SHADOWS_CASCADE");
        var softShadows = new ShaderKeyword(shader, "_SHADOWS_SOFT");

        for (int i = data.Count - 1; i >= 0; i--)
        {
            ShaderKeywordSet keywords = data[i].shaderKeywordSet;
            bool hardShadows = (keywords.IsEnabled(mainShadows) || keywords.IsEnabled(cascadeShadows)) && !keywords.IsEnabled(softShadows)
                && snippet.shaderType == ShaderType.Fragment && IsLitPass(snippet.passName);
            if (keywords.IsEnabled(instancing) || keywords.IsEnabled(crossfade) || hardShadows)
            {
                data.RemoveAt(i);
            }
        }
    }

    // Only the lit passes read _SHADOWS_SOFT; a ShadowCaster or depth pass never has it on, so it must not count there.
    private static bool IsLitPass(string pass) => pass == "ForwardLit" || pass == "GBuffer";
}
