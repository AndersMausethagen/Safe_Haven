using UnityEngine;

/// <summary>
/// The ribbon a blade leaves through the air: it follows the held weapon's real edge from just before a swing lands until just
/// after, so it curves the way the animation curves. Hilt and tip are found from the held model's own bounds (its longest axis, the
/// tip being the end further from the hand), so any weapon with a model gets one. Drawn with the slash material (uv.x along the
/// ribbon, uv.y from hilt to tip) and Graphics.RenderMesh, for the reason given in SlashEffect. Fists have no model and draw none.
/// Tune it in Combat Feel › Weapon trail, and per attack with its Slash Color.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerCombat))]
[RequireComponent(typeof(HeldItemView))]
public sealed class WeaponTrail : MonoBehaviour
{
    private const int MaxSamples = 32;
    // Each pair of recorded samples is rounded into this many pieces, so a fast swing curves instead of showing corners.
    private const int Subdivisions = 3;
    private const int MaxPoints = (MaxSamples - 1) * Subdivisions + 1;
    // Seconds before the hit it starts when the swing has no strike of its own, and after the hit it goes on.
    private const float MinimumLead = 0.1f;
    private const float FollowSeconds = 0.1f;

    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int ProgressId = Shader.PropertyToID("_Progress");
    private static readonly int FadeId = Shader.PropertyToID("_Fade");

    private readonly Vector3[] hilts = new Vector3[MaxSamples];
    private readonly Vector3[] tips = new Vector3[MaxSamples];
    private readonly float[] times = new float[MaxSamples];
    private readonly Vector3[] vertices = new Vector3[MaxPoints * 2];
    private readonly Vector2[] uvs = new Vector2[MaxPoints * 2];
    private int[] triangles;

    private PlayerCombat combat;
    private HeldItemView held;
    private Mesh mesh;
    private MaterialPropertyBlock block;
    private GameObject measuredModel;
    private Vector3 localHilt;
    private Vector3 localTip;
    private bool hasBlade;
    private int count;
    private float emitUntil = -1f;
    private Color color;

    private void Awake()
    {
        combat = GetComponent<PlayerCombat>();
        held = GetComponent<HeldItemView>();
        block = new MaterialPropertyBlock();
        mesh = new Mesh { name = "Weapon Trail", hideFlags = HideFlags.DontSave };
        mesh.MarkDynamic();
        triangles = new int[(MaxPoints - 1) * 6];
    }

    private void OnEnable()
    {
        combat.SwingLanded += OnSwingLanded;
    }

    private void OnDisable()
    {
        combat.SwingLanded -= OnSwingLanded;
        count = 0;
        emitUntil = -1f;
    }

    private void OnDestroy()
    {
        if (mesh != null)
        {
            Destroy(mesh);
        }
    }

    private void OnSwingLanded(SwingInfo swing, int hits)
    {
        emitUntil = Time.time + FollowSeconds;
    }

    // After the animation and after HeldItemView, so the blade is where it is drawn this frame.
    private void LateUpdate()
    {
        CombatFeelSettings feel = CombatFeelSettings.Current;
        if (feel.SlashMaterial == null || feel.TrailSeconds <= 0f)
        {
            count = 0;
            return;
        }

        if (IsEmitting(out AttackDefinition attack))
        {
            if (attack != null)
            {
                float brightness = feel.TrailBrightness;
                Color slash = attack.SlashColor;
                color = new Color(slash.r * brightness, slash.g * brightness, slash.b * brightness, slash.a);
            }

            Record();
        }

        DropOld(feel.TrailSeconds);
        if (count >= 2)
        {
            Draw(feel);
        }
    }

    private bool IsEmitting(out AttackDefinition attack)
    {
        attack = combat.ActiveAttack;
        bool visible = attack != null && attack.SlashColor.a > 0f && !combat.IsCharging;
        bool striking = visible && combat.IsWindingUp && combat.TimeToHit <= Mathf.Max(attack.StrikeLead, MinimumLead);
        return striking || (visible && Time.time < emitUntil);
    }

    private void Record()
    {
        if (!FindBlade())
        {
            return;
        }

        Transform model = held.ShownModel.transform;
        if (count == MaxSamples)
        {
            ShiftDown(1);
        }

        hilts[count] = model.TransformPoint(localHilt);
        tips[count] = model.TransformPoint(localTip);
        times[count] = Time.time;
        count++;
    }

    private void DropOld(float seconds)
    {
        int old = 0;
        while (old < count && Time.time - times[old] > seconds)
        {
            old++;
        }

        if (old > 0)
        {
            ShiftDown(old);
        }
    }

    private void ShiftDown(int by)
    {
        count -= by;
        System.Array.Copy(hilts, by, hilts, 0, count);
        System.Array.Copy(tips, by, tips, 0, count);
        System.Array.Copy(times, by, times, 0, count);
    }

    // The ends of the held model along its longest axis, in its own space; the tip is the end further from the hand.
    private bool FindBlade()
    {
        GameObject model = held.ShownModel;
        if (model == null)
        {
            measuredModel = null;
            hasBlade = false;
            return false;
        }

        if (model == measuredModel)
        {
            return hasBlade;
        }

        measuredModel = model;
        hasBlade = HeldWeaponShape.Measure(model, held.Grip, out localTip, out Vector3 butt);
        // The ribbon runs along the blade, not down the hilt in the fist.
        localHilt = Vector3.Lerp(localTip, butt, CombatFeelSettings.Current.TrailBladeShare);
        return hasBlade;
    }

    private void Draw(CombatFeelSettings feel)
    {
        int points = 0;
        var bounds = new Bounds(tips[count - 1], Vector3.zero);
        for (int i = 0; i < count - 1; i++)
        {
            for (int step = 0; step < Subdivisions; step++)
            {
                float t = step / (float)Subdivisions;
                AddPoint(ref points, ref bounds, Smooth(hilts, i, t), Smooth(tips, i, t), Mathf.Lerp(times[i], times[i + 1], t), feel.TrailSeconds);
            }
        }

        AddPoint(ref points, ref bounds, hilts[count - 1], tips[count - 1], times[count - 1], feel.TrailSeconds);
        for (int i = 0; i < points - 1; i++)
        {
            int v = i * 2, t = i * 6;
            triangles[t] = v;
            triangles[t + 1] = v + 1;
            triangles[t + 2] = v + 3;
            triangles[t + 3] = v;
            triangles[t + 4] = v + 3;
            triangles[t + 5] = v + 2;
        }

        mesh.Clear();
        mesh.SetVertices(vertices, 0, points * 2);
        mesh.SetUVs(0, uvs, 0, points * 2);
        mesh.SetTriangles(triangles, 0, (points - 1) * 6, 0, false);
        mesh.bounds = bounds;
        block.SetColor(ColorId, color);
        block.SetFloat(ProgressId, 1f);
        block.SetFloat(FadeId, 1f);
        var draw = new RenderParams(feel.SlashMaterial)
        {
            matProps = block,
            layer = gameObject.layer,
            shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
            receiveShadows = false,
            worldBounds = bounds,
        };
        Graphics.RenderMesh(draw, mesh, 0, Matrix4x4.identity);
    }

    private void AddPoint(ref int points, ref Bounds bounds, Vector3 hilt, Vector3 tip, float time, float seconds)
    {
        // uv.x: 1 at the blade now, 0 at the oldest, so the shader's tail fades it out behind.
        float age = Mathf.Clamp01(1f - (Time.time - time) / seconds);
        vertices[points * 2] = hilt;
        vertices[points * 2 + 1] = tip;
        uvs[points * 2] = new Vector2(age, 0f);
        uvs[points * 2 + 1] = new Vector2(age, 1f);
        bounds.Encapsulate(hilt);
        bounds.Encapsulate(tip);
        points++;
    }

    // Catmull-Rom between sample i and the next, using its neighbours (repeated at the ends).
    private Vector3 Smooth(Vector3[] samples, int i, float t)
    {
        Vector3 before = samples[Mathf.Max(0, i - 1)], from = samples[i], to = samples[i + 1], after = samples[Mathf.Min(count - 1, i + 2)];
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * (2f * from + (to - before) * t + (2f * before - 5f * from + 4f * to - after) * t2 + (3f * from - before - 3f * to + after) * t3);
    }
}
