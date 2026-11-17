using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.Universal;

/// <summary>
/// The world-look check (Docs/WorldLookPlan_2026-11-17.md B13, step 1 of its order of work): shots of a launched raid taken from the
/// game's own screen with ScreenCapture, so the atmospheric fog and the terrain mist show (a camera rendered into a texture skips the
/// atmosphere pass, which is why the earlier probes showed none), and the plan's colour table written beside them: mean RGB, luminance
/// p5/p50/p95, mean saturation, fine detail (mean difference from a 9 px box blur) and the median hue of the saturated pixels, on the
/// centre 90% of each image, measured exactly as scratchpad/wlp_stats.py measures (Pillow's grey, HSV and box blur).
/// <para>
/// Run in Play, one call per step (the raid must be launched through Play Haven (Isolated Saves): a launch autosaves):
/// <list type="bullet">
/// <item><c>--entry CaptureRaidLook.Launch --args '["TrollValley"]'</c>: launches the raid at its look seed, the first seed from 1 up
/// whose hour is Day, so every before and after is the same run at the same hour (<c>'["MossMarsh", "Dusk"]'</c> for another hour).</item>
/// <item><c>--entry CaptureRaidLook.Shoot --args '["D:/look/valley_before"]'</c>, once the map is made: the start, two sites, the top
/// through the screen (with the air) and the top as a plain render (no air, for counting canopy), then <c>colours.md</c>; <c>done.txt</c>
/// appears when it has finished (about 20 s). The Game view is set to Full HD for the shots and put back after.</item>
/// <item><c>--entry CaptureRaidLook.Table --args '["Docs/ConceptArt"]'</c>: the same table for any folder of PNGs (the targets).</item>
/// </list>
/// The shots run as a coroutine on the raid's RaidMap, as the CLI's calls take seconds. The editor must tick while unfocused
/// (<c>editor set_auto_tick</c>). The player is made invulnerable and the raid's clock gets ten more minutes.
/// </para>
/// </summary>
public static class CaptureRaidLook
{
    private const string Invulnerable = "CaptureRaidLook";
    private const float SiteStandOff = 12f;
    private const float SettleSeconds = 3f;
    private const float FileWaitSeconds = 8f;
    private const int MaxSeedSearch = 1000;

    /// <summary>Launches the raid (by asset name, id or display name) at its look seed for <paramref name="hour"/>.</summary>
    public static string Launch(string raidName, string hour = "Day")
    {
        if (!Application.isPlaying || Player.Current == null)
        {
            return "needs Play mode with the player in the Haven";
        }

        RaidDefinition raid = FindRaid(raidName);
        if (raid == null)
        {
            return $"no raid called {raidName}";
        }

        int seed = LookSeed(raid, hour);
        RaidTimeOfDay time = RaidTimeOfDay.For(raid, seed);
        bool launched = RaidLauncher.TryLaunch(raid, Player.Current.gameObject, null, out string problem, 0, seed);
        return launched
            ? $"launching {raid.DisplayName} at seed {seed} ({time?.DisplayName ?? "the scene's own hour"}); wait for 'Raid layout from seed {seed}' in the log"
            : $"refused: {problem}";
    }

    /// <summary>The first seed from 1 up whose hour is <paramref name="hour"/> (by name); 1 when the raid has no hours or none matches.</summary>
    public static int LookSeed(RaidDefinition raid, string hour = "Day")
    {
        for (int seed = 1; seed <= MaxSeedSearch; seed++)
        {
            RaidTimeOfDay time = RaidTimeOfDay.For(raid, seed);
            if (time == null)
            {
                return 1;
            }

            if (string.Equals(time.name, hour, StringComparison.OrdinalIgnoreCase) || string.Equals(time.DisplayName, hour, StringComparison.OrdinalIgnoreCase))
            {
                return seed;
            }
        }

        return 1;
    }

    /// <summary>In a launched raid: takes the shots into <paramref name="folder"/> and writes colours.md and done.txt beside them.</summary>
    public static string Shoot(string folder)
    {
        var map = UnityEngine.Object.FindAnyObjectByType<RaidMap>();
        if (!Application.isPlaying || map == null || Player.Current == null || Camera.main == null)
        {
            return "needs a launched raid in Play mode (a RaidMap, the player and the main camera)";
        }

        Directory.CreateDirectory(folder);
        string done = Path.Combine(folder, "done.txt");
        if (File.Exists(done))
        {
            File.Delete(done);
        }

        Player.Current.Health.AddInvulnerability(Invulnerable);
        RaidDirector.Current?.Session.AddTime(600f);
        List<(string name, Vector3 at)> spots = Spots(map);
        map.StartCoroutine(Run(map, folder, spots));
        return $"shooting {string.Join(", ", spots.ConvertAll(spot => spot.name))}, top and top_plain into {folder}; wait for done.txt";
    }

    /// <summary>Writes colours.md for every PNG in <paramref name="folder"/> and returns the table.</summary>
    public static string Table(string folder)
    {
        string[] images = Directory.GetFiles(folder, "*.png");
        Array.Sort(images, StringComparer.OrdinalIgnoreCase);
        var table = new StringBuilder();
        table.AppendLine("| Image | Mean RGB | Lum p5/p50/p95 | Sat | Detail | Hue |");
        table.AppendLine("|---|---|---|---|---|---|");
        foreach (string image in images)
        {
            table.AppendLine(Row(image));
        }

        File.WriteAllText(Path.Combine(folder, "colours.md"), table.ToString());
        return table.ToString();
    }

    // The start, then two sites in the plan's order (the same for the same seed), each stood off to the south so the camera looks at it.
    private static List<(string name, Vector3 at)> Spots(RaidMap map)
    {
        var spots = new List<(string, Vector3)>
        {
            ("start", map.PlayerStart != null ? map.PlayerStart.position : Player.Current.transform.position),
        };

        if (map.Layout != null)
        {
            Vector3 origin = map.Terrain.transform.position;
            int sites = 0;
            foreach (RaidSite site in map.Layout.Sites)
            {
                if (sites == 2)
                {
                    break;
                }

                if (site.Kind == RaidSiteKind.PlayerStart || site.Kind == RaidSiteKind.Extraction || site.Kind == RaidSiteKind.Patrol)
                {
                    continue;
                }

                Vector3 at = origin + new Vector3(site.Position.x * map.Layout.Size, 0f, site.Position.y * map.Layout.Size - SiteStandOff);
                spots.Add(("site" + ++sites, at));
            }
        }

        return spots;
    }

    private static IEnumerator Run(RaidMap map, string folder, List<(string name, Vector3 at)> spots)
    {
        int previousSize = SelectGameViewSize(1920, 1080);
        for (int frame = 0; frame < 5; frame++)
        {
            yield return null;
        }

        var log = new StringBuilder();
        log.AppendLine($"Raid {RaidDirector.Current?.Session.Definition?.DisplayName}, seed {RaidDirector.Current?.Session.Seed}, " +
                       $"{Screen.width}x{Screen.height}, {DateTime.Now:yyyy-MM-dd HH:mm}");
        foreach ((string name, Vector3 at) in spots)
        {
            MovePlayer(at);
            yield return new WaitForSecondsRealtime(SettleSeconds);
            yield return CaptureScreen(Path.Combine(folder, name + ".png"));
        }

        Camera main = Camera.main;
        Vector3 centre = map.Center;
        float size = map.Terrain != null ? map.Terrain.terrainData.size.x : 200f;
        float height = size * 0.5f / Mathf.Tan(30f * Mathf.Deg2Rad) + 10f;
        GameObject top = TopCamera(main, centre, height);
        yield return null;
        yield return null;
        yield return CaptureScreen(Path.Combine(folder, "top.png"));
        PlainRender(top.GetComponent<Camera>(), Path.Combine(folder, "top_plain.png"));
        UnityEngine.Object.Destroy(top);

        if (previousSize >= 0)
        {
            SelectGameViewSize(previousSize);
        }

        string table = Table(folder);
        File.WriteAllText(Path.Combine(folder, "colours.md"), log + Environment.NewLine + table);
        File.WriteAllText(Path.Combine(folder, "done.txt"), "done");
        Debug.Log($"CaptureRaidLook: shots and colours.md in {folder}.");
    }

    private static void MovePlayer(Vector3 at)
    {
        Transform player = Player.Current.transform;
        Vector3 to = NavMesh.SamplePosition(at, out NavMeshHit hit, 15f, NavMesh.AllAreas) ? hit.position : at;
        var body = player.GetComponent<CharacterController>();
        bool wasEnabled = body != null && body.enabled;
        if (wasEnabled)
        {
            body.enabled = false;
        }

        Vector3 from = player.position;
        player.position = to + Vector3.up * 0.1f;
        if (wasEnabled)
        {
            body.enabled = true;
        }

        foreach (Unity.Cinemachine.CinemachineCamera follow in UnityEngine.Object.FindObjectsByType<Unity.Cinemachine.CinemachineCamera>())
        {
            if (follow.Follow != null && follow.Follow.IsChildOf(player))
            {
                follow.OnTargetObjectWarped(follow.Follow, player.position - from);
            }
        }
    }

    // The screen as the player sees it, HUD hidden, then waits until the file is written (ScreenCapture writes at the frame's end).
    private static IEnumerator CaptureScreen(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        var hidden = new List<Canvas>();
        foreach (Canvas canvas in UnityEngine.Object.FindObjectsByType<Canvas>())
        {
            if (canvas.enabled && canvas.isRootCanvas && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                canvas.enabled = false;
                hidden.Add(canvas);
            }
        }

        yield return null;
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(path);
        float waited = 0f;
        while (!File.Exists(path) && waited < FileWaitSeconds)
        {
            yield return null;
            waited += Time.unscaledDeltaTime;
        }

        // A moment more, so the file is whole before anything reads it.
        yield return new WaitForSecondsRealtime(0.3f);
        foreach (Canvas canvas in hidden)
        {
            if (canvas != null)
            {
                canvas.enabled = true;
            }
        }
    }

    // A camera like the game's, high over the map's middle looking straight down, drawn after it so the screen shows it.
    private static GameObject TopCamera(Camera main, Vector3 centre, float height)
    {
        var holder = new GameObject("Raid Look Top Camera");
        Camera top = holder.AddComponent<Camera>();
        top.CopyFrom(main);
        // The game camera's renderer, post-processing and volume settings too, so the grade is the one the player sees.
        if (main.TryGetComponent(out UniversalAdditionalCameraData source))
        {
            if (!holder.TryGetComponent(out UniversalAdditionalCameraData copy))
            {
                copy = holder.AddComponent<UniversalAdditionalCameraData>();
            }

            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(source), copy);
            copy.cameraStack?.Clear();
        }

        top.targetTexture = null;
        top.depth = main.depth + 10f;
        top.fieldOfView = 60f;
        top.nearClipPlane = 1f;
        top.farClipPlane = Mathf.Max(main.farClipPlane, height * 2f);
        top.transform.SetPositionAndRotation(centre + Vector3.up * height, Quaternion.Euler(90f, 0f, 0f));
        return holder;
    }

    // The same view into a texture: the atmosphere pass skips such cameras, so this shows the land itself (canopy, rock, paint).
    private static void PlainRender(Camera camera, string path)
    {
        int width = Mathf.Max(64, Screen.width), height = Mathf.Max(64, Screen.height);
        var target = new RenderTexture(width, height, 24);
        camera.targetTexture = target;
        camera.Render();
        camera.targetTexture = null;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        RenderTexture.active = target;
        image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        image.Apply();
        RenderTexture.active = null;
        File.WriteAllBytes(path, image.EncodeToPNG());
        UnityEngine.Object.Destroy(target);
        UnityEngine.Object.Destroy(image);
    }

    private static RaidDefinition FindRaid(string raidName)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:RaidDefinition"))
        {
            var raid = AssetDatabase.LoadAssetAtPath<RaidDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (raid != null && (string.Equals(raid.name, raidName, StringComparison.OrdinalIgnoreCase)
                                 || string.Equals(raid.Id, raidName, StringComparison.OrdinalIgnoreCase)
                                 || string.Equals(raid.DisplayName, raidName, StringComparison.OrdinalIgnoreCase)))
            {
                return raid;
            }
        }

        return null;
    }

    // ---- The colour table, as wlp_stats.py measures it -------------------------------------------------------------------------------

    /// <summary>One row of the table for a PNG.</summary>
    public static string Row(string png)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!texture.LoadImage(File.ReadAllBytes(png)))
            {
                return $"| {Path.GetFileNameWithoutExtension(png)} | (unreadable) | | | | |";
            }

            Measure(texture.GetPixels32(), texture.width, texture.height, out Vector3 mean, out Vector3 luminance, out float saturation,
                out float detail, out float hue);
            return $"| {Path.GetFileNameWithoutExtension(png)} | {mean.x:0.00} {mean.y:0.00} {mean.z:0.00} | " +
                   $"{luminance.x:0.00}/{luminance.y:0.00}/{luminance.z:0.00} | {saturation:0.00} | {detail:0.0} | {hue:0}° |";
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    /// <summary>
    /// The statistics of an image given bottom row first (Texture2D's order), on its centre 90% as Pillow crops it: mean RGB (0-1),
    /// luminance (Rec. 709) at the 5th, 50th and 95th percentile, mean HSV saturation, detail (mean |grey - 9 px box blur|, 0-255) and
    /// the median hue in degrees of the pixels with saturation over 0.15.
    /// </summary>
    public static void Measure(Color32[] pixels, int width, int height, out Vector3 mean, out Vector3 luminance, out float saturation,
        out float detail, out float hue)
    {
        int left = (int)(width * 0.05), right = (int)(width * 0.95), topRow = (int)(height * 0.05), bottomRow = (int)(height * 0.95);
        int columns = right - left, rows = bottomRow - topRow, count = columns * rows;
        var grey = new byte[count];
        var lum = new float[count];
        var hues = new int[256];
        int saturated = 0;
        double r = 0, g = 0, b = 0, saturationSum = 0;
        for (int row = 0; row < rows; row++)
        {
            // Pillow counts rows from the top; a texture from the bottom.
            int source = (height - 1 - (topRow + row)) * width + left;
            for (int column = 0; column < columns; column++)
            {
                Color32 pixel = pixels[source + column];
                int at = row * columns + column;
                float red = pixel.r / 255f, green = pixel.g / 255f, blue = pixel.b / 255f;
                r += red;
                g += green;
                b += blue;
                lum[at] = 0.2126f * red + 0.7152f * green + 0.0722f * blue;
                grey[at] = (byte)((pixel.r * 19595 + pixel.g * 38470 + pixel.b * 7471 + 0x8000) >> 16);
                Hsv(pixel, out int hueByte, out int saturationByte);
                float s = saturationByte / 255f;
                saturationSum += s;
                if (s > 0.15f)
                {
                    hues[hueByte]++;
                    saturated++;
                }
            }
        }

        mean = new Vector3((float)(r / count), (float)(g / count), (float)(b / count));
        Array.Sort(lum);
        luminance = new Vector3(Percentile(lum, 5f), Percentile(lum, 50f), Percentile(lum, 95f));
        saturation = (float)(saturationSum / count);
        detail = Detail(grey, columns, rows);
        hue = saturated == 0 ? 0f : (Nth(hues, (saturated - 1) / 2) + Nth(hues, saturated / 2)) * 0.5f / 255f * 360f;
    }

    // Pillow's RGB to HSV (Convert.c): hue and saturation as bytes, truncated as it truncates them.
    private static void Hsv(Color32 pixel, out int hue, out int saturation)
    {
        int max = Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b)), min = Mathf.Min(pixel.r, Mathf.Min(pixel.g, pixel.b));
        if (max == min)
        {
            hue = 0;
            saturation = 0;
            return;
        }

        float spread = max - min;
        float s = spread / max;
        float rc = (max - pixel.r) / spread, gc = (max - pixel.g) / spread, bc = (max - pixel.b) / spread;
        double h = pixel.r == max ? bc - gc : pixel.g == max ? 2.0 + rc - bc : 4.0 + gc - rc;
        h = (h / 6.0 + 1.0) % 1.0;
        hue = Mathf.Clamp((int)(h * 255.0), 0, 255);
        saturation = Mathf.Clamp((int)(s * 255.0), 0, 255);
    }

    // numpy's percentile: linear between the two nearest ranks.
    private static float Percentile(float[] sorted, float percent)
    {
        double rank = percent / 100.0 * (sorted.Length - 1);
        int below = (int)Math.Floor(rank);
        int above = Math.Min(sorted.Length - 1, below + 1);
        return (float)(sorted[below] + (sorted[above] - sorted[below]) * (rank - below));
    }

    // The value of the n-th (0-based) smallest entry of a histogram of bytes.
    private static int Nth(int[] histogram, int n)
    {
        int seen = 0;
        for (int value = 0; value < histogram.Length; value++)
        {
            seen += histogram[value];
            if (seen > n)
            {
                return value;
            }
        }

        return histogram.Length - 1;
    }

    // Pillow's BoxBlur(4): a 9-wide box across, rounded to bytes, then down, edges repeated; the mean difference from the grey.
    private static float Detail(byte[] grey, int columns, int rows)
    {
        const int radius = 4;
        var across = new byte[grey.Length];
        var blurred = new byte[grey.Length];
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                int sum = 0;
                for (int k = -radius; k <= radius; k++)
                {
                    sum += grey[row * columns + Mathf.Clamp(column + k, 0, columns - 1)];
                }

                across[row * columns + column] = (byte)((sum * 2 + 2 * radius + 1) / (2 * (2 * radius + 1)));
            }
        }

        for (int column = 0; column < columns; column++)
        {
            for (int row = 0; row < rows; row++)
            {
                int sum = 0;
                for (int k = -radius; k <= radius; k++)
                {
                    sum += across[Mathf.Clamp(row + k, 0, rows - 1) * columns + column];
                }

                blurred[row * columns + column] = (byte)((sum * 2 + 2 * radius + 1) / (2 * (2 * radius + 1)));
            }
        }

        double difference = 0;
        for (int i = 0; i < grey.Length; i++)
        {
            difference += Math.Abs(grey[i] - blurred[i]);
        }

        return (float)(difference / grey.Length);
    }

    // ---- The Game view's size, by reflection (Editor only) ---------------------------------------------------------------------------

    // Picks the Game view's size of exactly this resolution; returns the index it had, or -1 when it can't (the shots then take the
    // view as it is, and colours.md says the size).
    private static int SelectGameViewSize(int width, int height)
    {
        try
        {
            EditorWindow view = GameView(out Type viewType);
            object group = CurrentSizeGroup();
            if (view == null || group == null)
            {
                return -1;
            }

            int total = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null);
            for (int i = 0; i < total; i++)
            {
                object size = group.GetType().GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
                if ((int)size.GetType().GetProperty("width").GetValue(size) == width && (int)size.GetType().GetProperty("height").GetValue(size) == height)
                {
                    int previous = (int)viewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(view);
                    SelectGameViewSize(i);
                    return previous;
                }
            }
        }
        catch (Exception problem)
        {
            Debug.LogWarning($"CaptureRaidLook: couldn't set the Game view to {width}x{height} ({problem.Message}); shooting it as it is.");
        }

        return -1;
    }

    private static void SelectGameViewSize(int index)
    {
        EditorWindow view = GameView(out Type viewType);
        viewType.GetMethod("SizeSelectionCallback", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.Invoke(view, new object[] { index, null });
    }

    private static EditorWindow GameView(out Type viewType)
    {
        viewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        UnityEngine.Object[] views = viewType != null ? Resources.FindObjectsOfTypeAll(viewType) : Array.Empty<UnityEngine.Object>();
        return views.Length > 0 ? (EditorWindow)views[0] : null;
    }

    private static object CurrentSizeGroup()
    {
        Type sizesType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameViewSizes");
        if (sizesType == null)
        {
            return null;
        }

        Type singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
        object sizes = singleton.GetProperty("instance", BindingFlags.Static | BindingFlags.Public).GetValue(null);
        return sizesType.GetProperty("currentGroup", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(sizes);
    }
}
