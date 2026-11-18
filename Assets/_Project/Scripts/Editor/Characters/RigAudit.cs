using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

/// <summary>
/// A technical pass over every rigged character (the user, 2026-11-18: "are the skeletons configured correctly and the correct size?
/// is there tearing and wrong stretches in the mesh?"). For each prefab with an Animator and a skinned mesh: the avatar (valid,
/// humanoid where it should be), the rig's scales, the body's height in its standing pose, how the mesh is weighted (vertices with
/// no weight, weights to bones far from the vertex), and the clips its controller plays, each sampled and baked: edges stretched or
/// crushed against the mesh as modelled (median-normalised, so the body's scale cancels) and seams that open (vertices that share a
/// place in the model and come apart in a pose). Safe Haven › Characters › Rig Audit writes the report to Logs/RigAudit.txt.
/// </summary>
public static class RigAudit
{
    public sealed class Body
    {
        public string Prefab;
        public string Renderer;
        public string Avatar;
        public bool AvatarValid;
        public bool Human;
        public float Height;
        public int Vertices;
        public int Unweighted;
        public int FarWeighted;
        public string OddScale;
        public int Clips;
        /// <summary>The worst edge stretch over every sampled pose (1 = as modelled), and the clip and bone it is at.</summary>
        public float WorstStretch = 1f;
        public string WorstStretchAt = "";
        /// <summary>Share of edges stretched past 2x in the worst pose.</summary>
        public float StretchedShare;
        /// <summary>The widest a seam opens in any pose (world metres), and where.</summary>
        public float WorstSeam;
        public string WorstSeamAt = "";
        public string Problem = "";
    }

    private const float Stretched = 2f;

    [MenuItem("Safe Haven/Characters/Rig Audit")]
    public static void RunMenu()
    {
        List<Body> bodies = Run(out string report);
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/RigAudit.txt", report);
        Debug.Log($"Rig audit: {bodies.Count} bodies, report in Logs/RigAudit.txt");
    }

    /// <summary>
    /// The same pass a prefab per Editor tick, so the Editor stays responsive: each body's line is appended to <paramref name="file"/>
    /// as it is done, and "done" is written to <paramref name="file"/>.done at the end.
    /// </summary>
    public static void Start(string file, string folder = "Assets/_Project", int clipsPerBody = 14, int samplesPerClip = 4)
    {
        var queue = new Queue<string>(AssetDatabase.FindAssets("t:Prefab", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath));
        var seen = new HashSet<string>();
        var bodies = new List<Body>();
        File.WriteAllText(file, "");
        File.Delete(file + ".done");
        var clock = System.Diagnostics.Stopwatch.StartNew();
        EditorApplication.CallbackFunction step = null;
        step = () =>
        {
            var tick = System.Diagnostics.Stopwatch.StartNew();
            while (queue.Count > 0 && tick.ElapsedMilliseconds < 400)
            {
                string path = queue.Dequeue();
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Animator animator = prefab != null ? prefab.GetComponentInChildren<Animator>(true) : null;
                SkinnedMeshRenderer[] skins = prefab != null ? prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true) : new SkinnedMeshRenderer[0];
                if (animator == null || skins.Length == 0)
                {
                    continue;
                }

                string identity = string.Join("|", skins.Select(s => Identity(s.sharedMesh))) + "|" + Identity(animator.avatar) + "|" + Identity(animator.runtimeAnimatorController);
                if (!seen.Add(identity))
                {
                    continue;
                }

                try
                {
                    List<Body> found = Audit(path, prefab, clipsPerBody, samplesPerClip);
                    bodies.AddRange(found);
                    File.AppendAllText(file, string.Join("", found.Select(b => Report(new List<Body> { b }).Split('\n')[1] + "\n")));
                }
                catch (System.Exception e)
                {
                    File.AppendAllText(file, $"{path} | FAILED: {e.GetType().Name} {e.Message}\n");
                }
            }

            if (queue.Count == 0)
            {
                EditorApplication.update -= step;
                File.WriteAllText(file + ".sorted", Report(bodies));
                File.WriteAllText(file + ".done", $"{bodies.Count} bodies in {clock.ElapsedMilliseconds / 1000f:0} s");
            }
        };
        EditorApplication.update += step;
    }

    /// <summary>Every rigged prefab under the project's folders, each distinct body once.</summary>
    public static List<Body> Run(out string report, int clipsPerBody = 14, int samplesPerClip = 4, string folder = "Assets/_Project")
    {
        var bodies = new List<Body>();
        var seen = new HashSet<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                continue;
            }

            Animator animator = prefab.GetComponentInChildren<Animator>(true);
            SkinnedMeshRenderer[] skins = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (animator == null || skins.Length == 0)
            {
                continue;
            }

            // A variant with the same body and controller as one already looked at adds nothing.
            string identity = string.Join("|", skins.Select(s => Identity(s.sharedMesh))) + "|" + Identity(animator.avatar) + "|" + Identity(animator.runtimeAnimatorController);
            if (!seen.Add(identity))
            {
                continue;
            }

            bodies.AddRange(Audit(path, prefab, clipsPerBody, samplesPerClip));
        }

        report = Report(bodies);
        return bodies;
    }

    private static string Identity(Object asset) => asset == null ? "-" : AssetDatabase.GetAssetPath(asset) + "#" + asset.name;

    public static List<Body> Audit(string path, GameObject prefab, int clipsPerBody, int samplesPerClip)
    {
        var found = new List<Body>();
        Scene scene = EditorSceneManager.NewPreviewScene();
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        try
        {
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Animator animator = instance.GetComponentInChildren<Animator>(true);
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            List<AnimationClip> clips = Pick(animator.runtimeAnimatorController, clipsPerBody);
            SkinnedMeshRenderer[] skins = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(s => s.sharedMesh != null).ToArray();
            var states = new List<(Body body, SkinnedMeshRenderer skin, Vector3[] rest, int[] edges, float[] restLength, int[][] seams)>();
            foreach (SkinnedMeshRenderer skin in skins)
            {
                Mesh mesh = skin.sharedMesh;
                var body = new Body
                {
                    Prefab = path,
                    Renderer = skin.name,
                    Avatar = animator.avatar != null ? animator.avatar.name : "(none)",
                    AvatarValid = animator.avatar != null && animator.avatar.isValid,
                    Human = animator.avatar != null && animator.avatar.isHuman,
                    Vertices = mesh.vertexCount,
                    Clips = clips.Count,
                };
                if (!mesh.isReadable && !Application.isEditor)
                {
                    body.Problem += "mesh not readable; ";
                }

                Weights(skin, body);
                body.OddScale = OddScales(animator.transform, skin);
                Vector3[] rest = mesh.vertices;
                Edges(mesh, rest, out int[] edges, out float[] restLength);
                states.Add((body, skin, rest, edges, restLength, Seams(rest)));
                found.Add(body);
            }

            // The standing height: the rest pose (the prefab as it lies, upright). An idle can be a crouch (the Lost Ones' first idle
            // read them a metre tall).
            AnimationClip stand = null;
            var baked = new Mesh();
            PlayableGraph graph = default;
            try
            {
                float lowest = float.MaxValue, highest = float.MinValue;
                if (stand != null)
                {
                    graph = Sample(animator, stand, 0f);
                }

                foreach (var state in states)
                {
                    // Baked with the renderer's scale, its vertices are in the renderer's own space: its matrix takes them to the world.
                    state.skin.BakeMesh(baked, true);
                    Matrix4x4 toWorld = state.skin.transform.localToWorldMatrix;
                    foreach (Vector3 v in baked.vertices)
                    {
                        float y = toWorld.MultiplyPoint3x4(v).y;
                        lowest = Mathf.Min(lowest, y);
                        highest = Mathf.Max(highest, y);
                    }
                }

                foreach (var state in states)
                {
                    state.body.Height = highest - lowest;
                }

                if (graph.IsValid())
                {
                    graph.Destroy();
                }

                foreach (AnimationClip clip in clips)
                {
                    for (int s = 0; s < samplesPerClip; s++)
                    {
                        float t = clip.length * (s + 0.5f) / samplesPerClip;
                        graph = Sample(animator, clip, t);
                        foreach (var state in states)
                        {
                            state.skin.BakeMesh(baked, false);
                            Measure(state.body, baked.vertices, state.rest, state.edges, state.restLength, state.seams, state.skin, $"{clip.name} @{t:0.00}s");
                        }

                        if (graph.IsValid())
                        {
                            graph.Destroy();
                        }
                    }
                }
            }
            finally
            {
                if (graph.IsValid())
                {
                    graph.Destroy();
                }

                Object.DestroyImmediate(baked);
            }
        }
        finally
        {
            Object.DestroyImmediate(instance);
            EditorSceneManager.ClosePreviewScene(scene);
        }

        return found;
    }

    /// <summary>
    /// Renders a prefab posed at a clip's time from two sides (and its rest pose beside it), to see what a number in the report looks
    /// like: Safe Haven's own lit shaders, a sun and the ambient as in the game, written as a PNG.
    /// </summary>
    public static string Snapshot(string prefabPath, string clipName, float time, string outPng, int size = 768, string focusBone = null)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            return "no prefab " + prefabPath;
        }

        Scene scene = EditorSceneManager.NewPreviewScene();
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        var lightObject = new GameObject("Sun");
        var cameraObject = new GameObject("Camera");
        SceneManager.MoveGameObjectToScene(lightObject, scene);
        SceneManager.MoveGameObjectToScene(cameraObject, scene);
        RenderTexture target = RenderTexture.GetTemporary(size, size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var sheet = new Texture2D(size * 3, size, TextureFormat.RGB24, false);
        PlayableGraph graph = default;
        try
        {
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Animator animator = instance.GetComponentInChildren<Animator>(true);
            Light sun = lightObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.6f;
            lightObject.transform.rotation = Quaternion.Euler(40f, 150f, 0f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.scene = scene;
            camera.targetTexture = target;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.32f, 0.34f, 0.36f);
            camera.fieldOfView = 30f;
            AnimationClip clip = animator != null && animator.runtimeAnimatorController != null
                ? animator.runtimeAnimatorController.animationClips.FirstOrDefault(c => c != null && c.name == clipName) : null;
            for (int view = 0; view < 3; view++)
            {
                if (graph.IsValid())
                {
                    graph.Destroy();
                }

                // The rest pose (the prefab as it lies) first, then the pose from the front and from the side.
                if (view > 0 && clip != null)
                {
                    graph = Sample(animator, clip, time);
                }

                // Drawn from the CPU-baked skin (what the report measures): a skinned renderer in a preview scene draws its bind pose.
                List<GameObject> stand = StandIns(instance, scene);
                Bounds bounds = BodyBounds(instance);
                // A close-up of one bone's part of the body: a third of the body's size round it.
                Transform focus = string.IsNullOrEmpty(focusBone) ? null : instance.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == focusBone);
                if (focus != null)
                {
                    bounds = new Bounds(focus.position, bounds.size * 0.33f);
                }

                float reach = Mathf.Max(bounds.extents.magnitude, 0.15f) / Mathf.Sin(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
                Vector3 from = view == 2 ? Vector3.right : new Vector3(0.35f, 0.25f, 1f).normalized;
                cameraObject.transform.position = bounds.center + from * reach;
                cameraObject.transform.LookAt(bounds.center);
                camera.nearClipPlane = Mathf.Max(0.01f, reach * 0.05f);
                camera.farClipPlane = reach * 4f;
                camera.Render();
                RenderTexture.active = target;
                sheet.ReadPixels(new Rect(0, 0, size, size), view * size, 0, false);
                RenderTexture.active = null;
                foreach (GameObject one in stand)
                {
                    Object.DestroyImmediate(one.GetComponent<MeshFilter>().sharedMesh);
                    Object.DestroyImmediate(one);
                }

                foreach (SkinnedMeshRenderer skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    skin.enabled = true;
                }
            }

            sheet.Apply(false);
            File.WriteAllBytes(outPng, sheet.EncodeToPNG());
            return clip != null ? "ok" : "no clip " + clipName + " (rest pose only)";
        }
        finally
        {
            if (graph.IsValid())
            {
                graph.Destroy();
            }

            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(target);
            Object.DestroyImmediate(sheet);
            Object.DestroyImmediate(instance);
            Object.DestroyImmediate(lightObject);
            Object.DestroyImmediate(cameraObject);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    // Each skinned mesh baked as it is posed, put in the world as a plain mesh with its materials; the skinned renderers are hidden.
    private static List<GameObject> StandIns(GameObject instance, Scene scene)
    {
        var made = new List<GameObject>();
        foreach (SkinnedMeshRenderer skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (skin.sharedMesh == null || !skin.gameObject.activeInHierarchy)
            {
                continue;
            }

            var baked = new Mesh { indexFormat = skin.sharedMesh.indexFormat };
            skin.BakeMesh(baked, true);
            Matrix4x4 toWorld = skin.transform.localToWorldMatrix;
            Vector3[] vertices = baked.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = toWorld.MultiplyPoint3x4(vertices[i]);
            }

            baked.vertices = vertices;
            baked.RecalculateNormals();
            baked.RecalculateBounds();
            var one = new GameObject(skin.name + " (baked)");
            SceneManager.MoveGameObjectToScene(one, scene);
            one.AddComponent<MeshFilter>().sharedMesh = baked;
            one.AddComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
            skin.enabled = false;
            made.Add(one);
        }

        return made;
    }

    private static Bounds BodyBounds(GameObject instance)
    {
        var baked = new Mesh();
        Bounds bounds = default;
        bool any = false;
        foreach (SkinnedMeshRenderer skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (skin.sharedMesh == null)
            {
                continue;
            }

            skin.BakeMesh(baked, true);
            Matrix4x4 toWorld = skin.transform.localToWorldMatrix;
            foreach (Vector3 v in baked.vertices)
            {
                Vector3 world = toWorld.MultiplyPoint3x4(v);
                if (!any)
                {
                    bounds = new Bounds(world, Vector3.zero);
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(world);
                }
            }
        }

        Object.DestroyImmediate(baked);
        return bounds;
    }

    private static Vector3 Inverse(Vector3 v) => new Vector3(1f / Mathf.Max(Mathf.Abs(v.x), 1e-6f), 1f / Mathf.Max(Mathf.Abs(v.y), 1e-6f), 1f / Mathf.Max(Mathf.Abs(v.z), 1e-6f));

    // The clip's pose at a time, written onto the body's bones. AnimationClip.SampleAnimation, not a playable graph: in a preview scene
    // a graph's first evaluations left some humanoid bodies (the Bog Troll) in their bind pose. Returns no graph (kept for the callers).
    private static PlayableGraph Sample(Animator animator, AnimationClip clip, float time)
    {
        clip.SampleAnimation(animator.gameObject, time);
        return default;
    }

    // The controller's clips, the ones a body is seen in most first (idle, walk, run, attacks, hits, death), each once.
    private static List<AnimationClip> Pick(RuntimeAnimatorController controller, int most)
    {
        if (controller == null)
        {
            return new List<AnimationClip>();
        }

        string[] first = { "idle", "walk", "run", "attack", "hit", "death", "die", "dodge", "jump", "roar", "swing", "cast" };
        return controller.animationClips.Where(c => c != null && c.length > 0.05f).Distinct()
            .OrderBy(c =>
            {
                string name = c.name.ToLowerInvariant();
                for (int i = 0; i < first.Length; i++)
                {
                    if (name.Contains(first[i]))
                    {
                        return i;
                    }
                }

                return first.Length;
            })
            .ThenBy(c => c.name)
            .Take(most).ToList();
    }

    private static void Weights(SkinnedMeshRenderer skin, Body body)
    {
        Mesh mesh = skin.sharedMesh;
        Transform[] bones = skin.bones;
        if (bones.Length == 0)
        {
            body.Problem += "no bones; ";
            return;
        }

        if (bones.Any(b => b == null))
        {
            body.Problem += $"{bones.Count(b => b == null)} missing bones; ";
        }

        Matrix4x4[] binds = mesh.bindposes;
        Vector3[] vertices = mesh.vertices;
        var counts = mesh.GetBonesPerVertex();
        var weights = mesh.GetAllBoneWeights();
        // Each bone as a segment from its origin to its children's (in the mesh's space at bind): a vertex mid-thigh is near the
        // thigh, far from its origin at the hip.
        var origins = new Vector3[bones.Length];
        for (int b = 0; b < bones.Length; b++)
        {
            origins[b] = b < binds.Length ? binds[b].inverse.MultiplyPoint3x4(Vector3.zero) : Vector3.zero;
        }

        var children = new List<int>[bones.Length];
        for (int b = 0; b < bones.Length; b++)
        {
            children[b] = new List<int>();
            for (int c = 0; c < bones.Length; c++)
            {
                if (bones[b] != null && bones[c] != null && bones[c].parent == bones[b])
                {
                    children[b].Add(c);
                }
            }
        }

        Vector3 low = origins.Aggregate(Vector3.positiveInfinity, Vector3.Min), high = origins.Aggregate(Vector3.negativeInfinity, Vector3.Max);
        float far = Mathf.Max((high - low).magnitude * 0.18f, 1e-4f);
        int at = 0;
        for (int v = 0; v < vertices.Length; v++)
        {
            int count = v < counts.Length ? counts[v] : 0;
            float total = 0f;
            int strongest = -1;
            float strongestWeight = 0f;
            for (int i = 0; i < count; i++)
            {
                BoneWeight1 w = weights[at + i];
                total += w.weight;
                if (w.weight > strongestWeight)
                {
                    strongestWeight = w.weight;
                    strongest = w.boneIndex;
                }
            }

            at += count;
            if (total < 0.98f || strongest < 0 || strongest >= bones.Length || bones[strongest] == null)
            {
                body.Unweighted++;
                continue;
            }

            if (DistanceToBone(vertices[v], strongest, origins, children) > far)
            {
                body.FarWeighted++;
            }
        }
    }

    private static float DistanceToBone(Vector3 point, int bone, Vector3[] origins, List<int>[] children)
    {
        Vector3 origin = origins[bone];
        float best = (point - origin).magnitude;
        foreach (int child in children[bone])
        {
            Vector3 along = origins[child] - origin;
            float t = along.sqrMagnitude > 1e-10f ? Mathf.Clamp01(Vector3.Dot(point - origin, along) / along.sqrMagnitude) : 0f;
            best = Mathf.Min(best, (point - (origin + along * t)).magnitude);
        }

        return best;
    }

    // Bones scaled unevenly (a stretched skeleton) or the rig's root far from 1.
    private static string OddScales(Transform root, SkinnedMeshRenderer skin)
    {
        var odd = new List<string>();
        foreach (Transform bone in skin.bones.Where(b => b != null))
        {
            Vector3 s = bone.localScale;
            float most = Mathf.Max(s.x, s.y, s.z), least = Mathf.Min(s.x, s.y, s.z);
            if (least <= 1e-4f || most / least > 1.05f)
            {
                odd.Add($"{bone.name} {s.x:0.##},{s.y:0.##},{s.z:0.##}");
            }
        }

        return odd.Count == 0 ? "" : string.Join("; ", odd.Take(3)) + (odd.Count > 3 ? $" (+{odd.Count - 3})" : "");
    }

    private static void Edges(Mesh mesh, Vector3[] rest, out int[] edges, out float[] restLength)
    {
        var unique = new HashSet<long>();
        var list = new List<int>();
        int[] triangles = mesh.triangles;
        for (int t = 0; t < triangles.Length; t += 3)
        {
            for (int e = 0; e < 3; e++)
            {
                int a = triangles[t + e], b = triangles[t + (e + 1) % 3];
                long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (unique.Add(key))
                {
                    list.Add(a);
                    list.Add(b);
                }
            }
        }

        edges = list.ToArray();
        restLength = new float[edges.Length / 2];
        for (int i = 0; i < restLength.Length; i++)
        {
            restLength[i] = (rest[edges[2 * i]] - rest[edges[2 * i + 1]]).magnitude;
        }
    }

    // Vertices that share a place in the model (UV and normal seams split a vertex): in a pose they should stay together.
    private static int[][] Seams(Vector3[] rest)
    {
        var groups = new Dictionary<(int, int, int), List<int>>();
        for (int v = 0; v < rest.Length; v++)
        {
            var key = (Mathf.RoundToInt(rest[v].x * 10000f), Mathf.RoundToInt(rest[v].y * 10000f), Mathf.RoundToInt(rest[v].z * 10000f));
            if (!groups.TryGetValue(key, out List<int> list))
            {
                groups[key] = list = new List<int>();
            }

            list.Add(v);
        }

        return groups.Values.Where(g => g.Count > 1).Select(g => g.ToArray()).ToArray();
    }

    private static void Measure(Body body, Vector3[] posed, Vector3[] rest, int[] edges, float[] restLength, int[][] seams, SkinnedMeshRenderer skin, string at)
    {
        int count = restLength.Length;
        if (count == 0 || posed.Length != rest.Length)
        {
            return;
        }

        var ratios = new float[count];
        for (int i = 0; i < count; i++)
        {
            float length = restLength[i];
            ratios[i] = length > 1e-6f ? (posed[edges[2 * i]] - posed[edges[2 * i + 1]]).magnitude / length : 1f;
        }

        float[] sorted = (float[])ratios.Clone();
        System.Array.Sort(sorted);
        float median = Mathf.Max(sorted[count / 2], 1e-6f);
        int stretched = 0;
        float worst = 1f;
        int worstEdge = 0;
        for (int i = 0; i < count; i++)
        {
            float r = ratios[i] / median;
            // Stretching only: a bent joint crushes the edges in its fold, which linear skinning always does.
            float bad = r;
            if (restLength[i] < 0.002f / median)
            {
                continue; // edges a couple of millimetres long: rounding, not tearing
            }

            if (r > Stretched)
            {
                stretched++;
            }

            if (bad > worst)
            {
                worst = bad;
                worstEdge = i;
            }
        }

        float share = (float)stretched / count;
        if (worst > body.WorstStretch)
        {
            body.WorstStretch = worst;
            body.WorstStretchAt = $"{at}, {BoneOf(skin, edges[2 * worstEdge])}";
        }

        body.StretchedShare = Mathf.Max(body.StretchedShare, share);
        foreach (int[] seam in seams)
        {
            Vector3 first = posed[seam[0]];
            for (int k = 1; k < seam.Length; k++)
            {
                // In world metres: a mesh baked without the renderer's scale is already at the world's size (the pony's renderer is scaled
                // 106, the Draug's 0.111).
                float apart = (posed[seam[k]] - first).magnitude;
                if (apart > body.WorstSeam)
                {
                    body.WorstSeam = apart;
                    body.WorstSeamAt = $"{at}, {BoneOf(skin, seam[0])}";
                }
            }
        }
    }

    private static string BoneOf(SkinnedMeshRenderer skin, int vertex)
    {
        Mesh mesh = skin.sharedMesh;
        var counts = mesh.GetBonesPerVertex();
        var weights = mesh.GetAllBoneWeights();
        int at = 0;
        for (int v = 0; v < vertex; v++)
        {
            at += counts[v];
        }

        int best = -1;
        float bestWeight = 0f;
        for (int i = 0; i < counts[vertex]; i++)
        {
            if (weights[at + i].weight > bestWeight)
            {
                bestWeight = weights[at + i].weight;
                best = weights[at + i].boneIndex;
            }
        }

        Transform[] bones = skin.bones;
        return best >= 0 && best < bones.Length && bones[best] != null ? bones[best].name : "?";
    }

    public static string Report(List<Body> bodies)
    {
        var text = new StringBuilder();
        text.AppendLine("prefab | renderer | avatar (valid, human) | height m | verts | unweighted | far-weighted | odd scales | clips | worst stretch (where) | >2x share | worst seam m (where) | problems");
        foreach (Body b in bodies.OrderByDescending(b => Severity(b)))
        {
            text.Append(Path.GetFileNameWithoutExtension(b.Prefab)).Append(" | ").Append(b.Renderer).Append(" | ")
                .Append(b.Avatar).Append(b.AvatarValid ? " ok" : " INVALID").Append(b.Human ? " human" : " generic").Append(" | ")
                .Append(b.Height.ToString("0.00", CultureInfo.InvariantCulture)).Append(" | ").Append(b.Vertices).Append(" | ")
                .Append(b.Unweighted).Append(" | ").Append(b.FarWeighted).Append(" | ").Append(b.OddScale).Append(" | ").Append(b.Clips).Append(" | ")
                .Append(b.WorstStretch.ToString("0.0", CultureInfo.InvariantCulture)).Append(" (").Append(b.WorstStretchAt).Append(") | ")
                .Append((b.StretchedShare * 100f).ToString("0.00", CultureInfo.InvariantCulture)).Append("% | ")
                .Append(b.WorstSeam.ToString("0.000", CultureInfo.InvariantCulture)).Append(" (").Append(b.WorstSeamAt).Append(") | ")
                .AppendLine(b.Problem);
        }

        return text.ToString();
    }

    public static float Severity(Body b) =>
        (b.AvatarValid ? 0f : 100f) + b.Unweighted * 0.1f + b.FarWeighted * 0.05f + Mathf.Max(0f, b.WorstStretch - 2f) * 5f
        + b.StretchedShare * 500f + b.WorstSeam * 200f + (string.IsNullOrEmpty(b.OddScale) ? 0f : 5f);
}
