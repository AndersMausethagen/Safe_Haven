using System;
using System.Linq;
using AmplifyShaderEditor;
using UnityEditor;
using UnityEngine;

/// <summary>Builds native ASE node graphs. Existing graphs are never regenerated over a designer's edits.</summary>
public static class ForestGroundShaders
{
    public const string GroundPath = "Assets/_Project/Shaders/Forest Ground ASE.shader";
    public const string StonePath = "Assets/_Project/Shaders/Mossy Stone ASE.shader";

    [MenuItem("Safe Haven/Setup/Art Pipeline/Create Amplify Forest Shaders", false, 200)]
    public static void CreateMissing()
    {
        Build(GroundPath, "Safe Haven/Forest Ground ASE", false);
        Build(StonePath, "Safe Haven/Mossy Stone ASE", true);
    }

    private static void Build(string path, string name, bool slope)
    {
        Shader existing = AssetDatabase.LoadAssetAtPath<Shader>(path);
        if (existing != null && existing.FindPropertyIndex("_GroundColor") >= 0) return;
        string guid = TemplatesManager.URPLitGUID;
        if (TemplatesManager.Instance.GetTemplate(guid) == null)
        {
            guid = AssetDatabase.AssetPathToGUID("Assets/AmplifyShaderEditor/Plugins/EditorResources/Templates/URP/17/Lit.template");
            if (TemplatesManager.Instance.CheckAndLoadTemplate(guid) == null)
                throw new InvalidOperationException("Amplify's URP 17 Lit template is unavailable.");
        }

        if (existing == null) AmplifyShaderEditorWindow.CreateNewTemplateShader(guid, path, name + ".shader");
        else AmplifyShaderEditorWindow.ConvertShaderToASE(existing);
        AmplifyShaderEditorWindow window = UIUtils.CurrentWindow;
        if (existing != null && window.LoadFromDisk(path) != ShaderLoadResult.LOADED)
            throw new InvalidOperationException("Amplify failed to load the unfinished graph: " + path);
        ParentGraph graph = window.CurrentGraph;
        ParentNode master = graph.CurrentMasterNode;
        master.Vec2Position = new Vector2(1250f, 0f);

        var coordinates = Node<TextureCoordinatesNode>(graph, -1500, -200);
        var tiling = Float(graph, "Tiling", 1f, 0.05f, 16f, -1800, -200);
        Connect(graph, coordinates, "Tiling", tiling);
        var primaryHeight = Sample(graph, "Ground Packed", slope ? "Stone" : "Soil", "Packed", coordinates, -1200, 500);
        var parallax = Node<ParallaxMappingNode>(graph, -900, -200);
        var relief = Float(graph, "Relief", 0.018f, 0f, 0.06f, -1200, -400);
        Connect(graph, parallax, "UV", coordinates);
        Connect(graph, parallax, "Height", primaryHeight, "B");
        Connect(graph, parallax, "Scale", relief);
        var view = Node<ViewDirInputsCoordNode>(graph, -1200, -650);
        var viewSettings = new SerializedObject(view);
        viewSettings.FindProperty("m_viewDirSpace").enumValueIndex = (int)ViewSpace.Tangent;
        viewSettings.ApplyModifiedPropertiesWithoutUndo();
        Connect(graph, parallax, "ViewDir (tan)", view);

        var primary = Sample(graph, "Ground Color", slope ? "Stone" : "Soil", "BaseColor", parallax, -600, -300);
        var primaryNormal = Sample(graph, "Ground Normal", slope ? "Stone" : "Soil", "Normal", parallax, -600, 0);
        primaryNormal.AutoUnpackNormals = true;
        primaryNormal.ConfigureInputPorts();
        primaryNormal.ConfigureOutputPorts();
        var primaryPacked = Sample(graph, "Ground Surface", slope ? "Stone" : "Soil", "Packed", parallax, -600, 300);
        var moss = Sample(graph, "Moss Color", "Moss", "BaseColor", parallax, -600, 700);
        var mossNormal = Sample(graph, "Moss Normal", "Moss", "Normal", parallax, -600, 1000);
        mossNormal.AutoUnpackNormals = true;
        mossNormal.ConfigureInputPorts();
        mossNormal.ConfigureOutputPorts();
        var normalStrength = Float(graph, "Ground Normal Strength", 0.65f, 0f, 2f, -900, 100);
        var mossStrength = Float(graph, "Moss Normal Strength", 0.85f, 0f, 2f, -900, 1100);
        Connect(graph, primaryNormal, 5, normalStrength);
        Connect(graph, mossNormal, 5, mossStrength);
        var mossPacked = Sample(graph, "Moss Surface", "Moss", "Packed", parallax, -600, 1300);
        var difference = Node<SimpleSubtractOpNode>(graph, -300, 1600);
        Connect(graph, difference, 0, mossPacked, "B");
        Connect(graph, difference, 1, primaryPacked, "B");
        var contrast = Float(graph, "Edge Contrast", 1.5f, 0f, 8f, -600, 1800);
        var biasedHeight = Node<SimpleMultiplyOpNode>(graph, 0, 1600);
        Connect(graph, biasedHeight, 0, difference);
        Connect(graph, biasedHeight, 1, contrast);
        var coverage = Float(graph, "Moss Coverage", slope ? 1.7f : 0.3f, 0f, 3f, -600, 2100);
        ParentNode maskSource = coverage;
        if (slope)
        {
            var normal = Node<WorldNormalVector>(graph, -900, 2350);
            var up = Node<SaturateNode>(graph, -600, 2350);
            Connect(graph, up, 0, normal, "Y");
            var multiply = Node<SimpleMultiplyOpNode>(graph, -300, 2100);
            Connect(graph, multiply, 0, up);
            Connect(graph, multiply, 1, coverage);
            var threshold = Float(graph, "Slope Threshold", 0.65f, 0f, 1f, -600, 2600);
            var subtract = Node<SimpleSubtractOpNode>(graph, 0, 2100);
            Connect(graph, subtract, 0, multiply);
            Connect(graph, subtract, 1, threshold);
            maskSource = subtract;
        }
        var sum = Node<SimpleAddOpNode>(graph, 250, 1600);
        var coverageMask = Node<SaturateNode>(graph, 0, 2850);
        Connect(graph, coverageMask, 0, maskSource);
        var inverseCoverage = Node<OneMinusNode>(graph, 250, 2850);
        Connect(graph, inverseCoverage, 0, coverageMask);
        var boundary = Node<SimpleMultiplyOpNode>(graph, 500, 2850);
        Connect(graph, boundary, 0, coverageMask);
        Connect(graph, boundary, 1, inverseCoverage);
        var edgeDetail = Node<SimpleMultiplyOpNode>(graph, 250, 1900);
        Connect(graph, edgeDetail, 0, biasedHeight);
        Connect(graph, edgeDetail, 1, boundary);
        Connect(graph, sum, 0, edgeDetail);
        Connect(graph, sum, 1, coverageMask);
        var mask = Node<SaturateNode>(graph, 500, 1600);
        Connect(graph, mask, 0, sum);

        ParentNode color = Blend(graph, primary, "RGB", moss, "RGB", mask, 0);
        var tint = Node<ColorNode>(graph, 250, -400);
        tint.ChangeParameterType(PropertyType.Property);
        tint.RegisterPropertyName(true, "Tint");
        tint.Value = Color.white;
        var tinted = Node<SimpleMultiplyOpNode>(graph, 900, 0);
        Connect(graph, tinted, 0, color);
        Connect(graph, tinted, 1, tint);
        Connect(graph, master, "Base Color", tinted);
        ParentNode normals = Blend(graph, primaryNormal, null, mossNormal, null, mask, 350);
        var normalized = Node<NormalizeNode>(graph, 1000, 350);
        Connect(graph, normalized, 0, normals);
        Connect(graph, master, "Normal", normalized);
        ParentNode roughness = Blend(graph, primaryPacked, "G", mossPacked, "G", mask, 700);
        var smooth = Node<OneMinusNode>(graph, 1000, 700);
        Connect(graph, smooth, 0, roughness);
        Connect(graph, master, "Smoothness", smooth);
        ParentNode ao = Blend(graph, primaryPacked, "R", mossPacked, "R", mask, 1000);
        Connect(graph, master, "Occlusion", ao);
        foreach (TemplateMultiPassMasterNode output in graph.AllNodes.OfType<TemplateMultiPassMasterNode>())
            output.ForceOptionsRefresh();
        if (!window.SaveToDisk(false)) throw new InvalidOperationException("Amplify failed to save " + path);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
    }

    private static T Node<T>(ParentGraph graph, float x, float y) where T : ParentNode
    {
        return (T)graph.CreateNode(typeof(T), false, new Vector2(x, y));
    }

    private static RangedFloatNode Float(ParentGraph graph, string name, float value, float min, float max, int x, int y)
    {
        var node = Node<RangedFloatNode>(graph, x, y);
        node.ChangeParameterType(PropertyType.Property);
        node.RegisterPropertyName(true, name);
        node.SetTypeChangeRange(new Vector2(min, max));
        node.Value = value;
        return node;
    }

    private static SamplerNode Sample(ParentGraph graph, string name, string kind, string map, ParentNode uv, int x, int y)
    {
        var node = Node<SamplerNode>(graph, x, y);
        node.RegisterPropertyName(true, name);
        node.DefaultValue = AssetDatabase.LoadAssetAtPath<Texture2D>(ForestGroundTextures.Root + "/" + kind + "_" + map + ".png");
        Connect(graph, node, "UV", uv);
        return node;
    }

    private static ParentNode Blend(ParentGraph graph, ParentNode a, string aPort, ParentNode b, string bPort, ParentNode mask, int y)
    {
        var lerp = Node<LerpOp>(graph, 650, y);
        Connect(graph, lerp, 0, a, aPort);
        Connect(graph, lerp, 1, b, bPort);
        Connect(graph, lerp, 2, mask);
        return lerp;
    }

    private static void Connect(ParentGraph graph, ParentNode input, string port, ParentNode output, string outputPort = null)
    {
        Connect(graph, input, input.InputPorts.Single(p => p.Name == port).PortId, output, outputPort);
    }

    private static void Connect(ParentGraph graph, ParentNode input, int port, ParentNode output, string outputPort = null)
    {
        int id = outputPort == null ? output.OutputPorts[0].PortId : output.OutputPorts.Single(p => p.Name == outputPort).PortId;
        graph.CreateConnection(input.UniqueId, port, output.UniqueId, id, false);
    }
}
