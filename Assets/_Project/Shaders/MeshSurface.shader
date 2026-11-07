Shader "Safe Haven/Mesh Surface (Tessellated)"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Colour", Color) = (1,1,1,1)
        [Normal][NoScaleOffset] _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Strength", Range(0,3)) = 1
        [NoScaleOffset] _MetallicGlossMap("Metallic (R) Smoothness (A)", 2D) = "white" {}
        _Metallic("Metallic", Range(0,1)) = 0
        _Smoothness("Smoothness", Range(0,1)) = 0.3
        [NoScaleOffset] _OcclusionMap("Occlusion (G)", 2D) = "white" {}
        _OcclusionStrength("Occlusion Strength", Range(0,1)) = 1
        [NoScaleOffset] _EmissionMap("Emission Map", 2D) = "white" {}
        [HDR] _EmissionColor("Emission Colour", Color) = (0,0,0,1)
        [Header(Displacement)]
        [NoScaleOffset] _HeightMap("Height Map (Linear)", 2D) = "gray" {}
        _HeightChannel("Height Channel (R G B A)", Vector) = (1,0,0,0)
        _HeightStrength("Height Range (metres)", Range(0,1)) = 0.05
        _HeightMidpoint("Height Midpoint", Range(0,1)) = 0.5
        _HeightMip("Height Smoothing", Range(0,5)) = 0
        _HeightNormalStrength("Height Normal Strength", Range(0,2)) = 1
        [Header(Tessellation)]
        _TessellationFactor("Maximum Subdivision", Range(1,64)) = 16
        _TessellationEdgeLength("Target Edge Length (metres)", Range(0.005,0.2)) = 0.04
        _TessellationNear("Full Detail Distance", Float) = 10
        _TessellationFar("Fade Distance", Float) = 35
        [Header(Rendering)]
        [Toggle(_ALPHATEST_ON)] _AlphaClip("Alpha Clipping", Float) = 0
        _Cutoff("Alpha Cutoff", Range(0,1)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
        [ToggleOff(_RECEIVE_SHADOWS_OFF)] _ReceiveShadows("Receive Shadows", Float) = 1
        [ToggleOff(_SPECULARHIGHLIGHTS_OFF)] _SpecularHighlights("Specular Highlights", Float) = 1
        [ToggleOff(_ENVIRONMENTREFLECTIONS_OFF)] _EnvironmentReflections("Environment Reflections", Float) = 1
        [Toggle(_SEASON_SNOW)] _SeasonSnow("Weather Settles On It (snow, rain)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" "UniversalMaterialType"="Lit" "DisableBatching"="True" }
        Cull [_Cull]
        HLSLINCLUDE
        #pragma target 4.6
        #define _NORMALMAP 1
        #define _EMISSION 1
        #include "MeshSurfaceInput.hlsl"
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex MeshVertex
            #pragma hull MeshHull
            #pragma domain MeshDomain
            #pragma fragment LitPassFragment
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF
            #pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #pragma shader_feature_local_fragment _ _SEASON_SNOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #define SEASON_SNOW_WRAP_PBR
            #include "Assets/_Project/Shaders/SurfaceWetness.hlsl"
            #include "Assets/_Project/Shaders/SeasonSnow.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
            #define MESH_FORWARD
            #define MESH_PASS_VERTEX LitPassVertex
            #include "MeshSurfaceTessellation.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex MeshVertex
            #pragma hull MeshHull
            #pragma domain MeshDomain
            #pragma fragment ShadowPassFragment
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing
            #pragma multi_compile _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            #define MESH_SHADOW
            #define MESH_PASS_VERTEX ShadowPassVertex
            #include "MeshSurfaceTessellation.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma vertex MeshVertex
            #pragma hull MeshHull
            #pragma domain MeshDomain
            #pragma fragment DepthOnlyFragment
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            #define MESH_DEPTH_ONLY
            #define MESH_PASS_VERTEX DepthOnlyVertex
            #include "MeshSurfaceTessellation.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            HLSLPROGRAM
            #pragma vertex MeshVertex
            #pragma hull MeshHull
            #pragma domain MeshDomain
            #pragma fragment DepthNormalsFragment
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitDepthNormalsPass.hlsl"
            #define MESH_DEPTH_NORMALS
            #define MESH_PASS_VERTEX DepthNormalsVertex
            #include "MeshSurfaceTessellation.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "Meta"
            Tags { "LightMode"="Meta" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex UniversalVertexMeta
            #pragma fragment UniversalFragmentMetaLit
            #pragma shader_feature_local _ALPHATEST_ON
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitMetaPass.hlsl"
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
