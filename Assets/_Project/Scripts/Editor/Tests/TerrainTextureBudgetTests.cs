using System;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

/// <summary>
/// The World terrain's one-pass variant (all painted layers, the Haven and the prologue) must bind no more than the 64 textures a
/// D3D11 device takes. Over it, the build warns once ("uses 66 texture parameters, more than the 64 supported") and the textures
/// past the limit go unbound: the terrain went grey once (2026-11-14) and drew over the limit in the Haven's normal light
/// (2026-11-24: the sun's cookie, reflection probe blending and screen-space irradiance). Counted in the compiled D3D11 code with
/// every feature the game turns on together (GenerateWorldTerrainShader.py leaves the probe volumes and blending out of it).
/// </summary>
public sealed class TerrainTextureBudgetTests
{
    private const int Limit = 64;

    // The features on while the Haven renders on PC (checked live: the sun's cookie, Forward+, screen-space irradiance and
    // occlusion; decals are screen-space, not DBuffer), on the all-layers variant as the Haven's terrain material sets it.
    private static readonly string[] Live =
    {
        "_WORLD_ALL_LAYERS", "_TERRAIN_BLEND_HEIGHT", "_NORMALMAP", "_MASKMAP", "_TERRAIN_INSTANCED_PERPIXEL_NORMAL", "INSTANCING_ON",
        "_MAIN_LIGHT_SHADOWS_CASCADE", "_CLUSTER_LIGHT_LOOP", "_ADDITIONAL_LIGHT_SHADOWS", "_SHADOWS_SOFT", "_SCREEN_SPACE_OCCLUSION",
        "_LIGHT_COOKIES", "_REFLECTION_PROBE_BLENDING", "_REFLECTION_PROBE_BOX_PROJECTION", "_SCREEN_SPACE_IRRADIANCE",
        "PROBE_VOLUMES_L1",
    };

    [Test]
    public void TheAllLayersTerrain_BindsNoMoreThan64Textures_WithEverythingTheGameTurnsOn()
    {
        Shader shader = Shader.Find("Safe Haven/Terrain (World)");
        Assert.IsNotNull(shader, "the World terrain shader");
        ShaderData.Subshader subshader = ShaderUtil.GetShaderData(shader).GetSerializedSubshader(0);
        ShaderData.Pass forward = null;
        for (int i = 0; i < subshader.PassCount; i++)
        {
            if (subshader.GetPass(i).Name == "ForwardLit")
            {
                forward = subshader.GetPass(i);
            }
        }

        Assert.IsNotNull(forward, "its forward pass");
        int plain = Textures(forward, new[] { "_WORLD_ALL_LAYERS", "_NORMALMAP", "_MASKMAP" });
        Assert.Greater(plain, 40, "canary: the count reads the compiled code (14 layers of three maps alone are 42)");
        int live = Textures(forward, Live);
        Assert.LessOrEqual(live, Limit, $"the Haven's terrain binds {live} textures with the game's lighting on; past {Limit} D3D11 " +
            "leaves the rest unbound. Free some in GenerateWorldTerrainShader.py (leave a feature out of _WORLD_ALL_LAYERS, as the " +
            "probe volumes and probe blending are) before adding one.");
    }

    private static int Textures(ShaderData.Pass pass, string[] keywords)
    {
        ShaderData.VariantCompileInfo compiled = pass.CompileVariant(ShaderType.Fragment, keywords, ShaderCompilerPlatform.D3D,
            BuildTarget.StandaloneWindows64);
        Assert.IsTrue(compiled.Success, "the variant compiles: " + (compiled.Messages.Length > 0 ? compiled.Messages[0].message : ""));
        return DeclaredTextures(compiled.ShaderData);
    }

    /// <summary>
    /// Textures a D3D11 program declares: Unity's blob holds a DXBC container (its reflection stripped); in its SHEX chunk each
    /// dcl_resource (opcode 88) declares one texture register.
    /// </summary>
    private static int DeclaredTextures(byte[] bytes)
    {
        int dxbc = -1;
        for (int i = 0; i + 4 < bytes.Length && dxbc < 0; i++)
        {
            if (bytes[i] == 'D' && bytes[i + 1] == 'X' && bytes[i + 2] == 'B' && bytes[i + 3] == 'C')
            {
                dxbc = i;
            }
        }

        Assert.GreaterOrEqual(dxbc, 0, "a DXBC container in the compiled variant");
        int chunks = BitConverter.ToInt32(bytes, dxbc + 28);
        for (int c = 0; c < chunks; c++)
        {
            int at = dxbc + BitConverter.ToInt32(bytes, dxbc + 32 + c * 4);
            string tag = System.Text.Encoding.ASCII.GetString(bytes, at, 4);
            if (tag != "SHEX" && tag != "SHDR")
            {
                continue;
            }

            int end = at + 8 + BitConverter.ToInt32(bytes, at + 4);
            int textures = 0;
            // Past the version and length tokens: each instruction's opcode is in bits 0-10 and its length in dwords in bits 24-30;
            // custom data (53) gives its length in the next dword.
            for (int i = at + 16; i < end;)
            {
                uint token = BitConverter.ToUInt32(bytes, i);
                int opcode = (int)(token & 0x7FF);
                int length = opcode == 53 ? BitConverter.ToInt32(bytes, i + 4) : (int)((token >> 24) & 0x7F);
                if (opcode == 88)
                {
                    textures++;
                }

                if (length <= 0)
                {
                    break;
                }

                i += length * 4;
            }

            return textures;
        }

        Assert.Fail("no shader code chunk in the DXBC container");
        return 0;
    }
}
