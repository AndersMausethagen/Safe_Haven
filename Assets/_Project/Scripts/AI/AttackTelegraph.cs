using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Warns the player that a monster's attack is coming: during the wind-up it draws the attack's area (range and arc from
/// its AttackDefinition) on the ground and fills it from the monster outward; when the fill reaches the edge, the hit
/// lands. Players learn to read it and dodge out. Colors and material come from the CombatFeel settings.
/// <para>
/// Where it belongs: AI, on monster prefabs next to MonsterAI (the monster prefab builder adds it). It only listens to
/// MonsterAI's AttackStarted and StateChanged events. A longer HitDelay on the attack gives a longer warning.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MonsterAI))]
public sealed class AttackTelegraph : MonoBehaviour
{
    private const float DegreesPerSegment = 6f;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    // One unit-radius fan per arc, shared by every monster.
    private static readonly Dictionary<int, Mesh> Fans = new Dictionary<int, Mesh>();

    // Entering Play doesn't reload scripts in this project, so statics are cleared when it starts.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Fans.Clear();

    [Tooltip("Height of the marker above the monster's feet, so it doesn't flicker into the ground.")]
    [SerializeField, Min(0f)] private float groundOffset = 0.06f;

    private MonsterAI ai;
    private Transform marker;
    private Transform fill;
    private MeshFilter areaFilter;
    private MeshFilter fillFilter;
    private MeshRenderer fillRenderer;
    private MaterialPropertyBlock block;
    private AttackDefinition attack;
    private float startTime;
    private float hideAt = -1f;
    private bool cued;
    private static bool warnedNoMaterial;

    private void Awake()
    {
        ai = GetComponent<MonsterAI>();
    }

    private void OnEnable()
    {
        ai.AttackStarted += OnAttackStarted;
        ai.StateChanged += OnStateChanged;
    }

    private void OnDisable()
    {
        ai.AttackStarted -= OnAttackStarted;
        ai.StateChanged -= OnStateChanged;
        Hide();
    }

    private void OnDestroy()
    {
        if (marker != null)
        {
            Destroy(marker.gameObject);
        }
    }

    private void OnAttackStarted()
    {
        attack = ai.CurrentAttack;
        if (attack == null || !EnsureMarker())
        {
            return;
        }

        startTime = Time.time;
        hideAt = -1f;
        cued = false;
        areaFilter.sharedMesh = fillFilter.sharedMesh = FanFor(attack.Arc);
        float range = ai.ActiveAction != null && ai.ActiveAction.kind == MonsterAttackKind.Charge
            ? ai.ActiveAction.chargeDistance : attack.Range;
        marker.localScale = Vector3.one * range;
        marker.gameObject.SetActive(true);
        Place();
        UpdateFill(0f);
    }

    // A monster that dies or is interrupted mid-swing never lands the hit, so its warning disappears with it.
    private void OnStateChanged(MonsterAI.State previous, MonsterAI.State current)
    {
        if (previous == MonsterAI.State.Attacking && attack != null && Time.time - startTime < attack.HitDelay)
        {
            Hide();
        }
    }

    private void LateUpdate()
    {
        if (marker == null || !marker.gameObject.activeSelf || attack == null)
        {
            return;
        }

        Place();
        if (ai.IsHoldingStrike)
        {
            startTime += Time.deltaTime;
        }

        float elapsed = Time.time - startTime;
        if (hideAt < 0f && elapsed >= attack.HitDelay)
        {
            hideAt = Time.time + CombatFeelSettings.Current.TelegraphLinger;
        }

        if (hideAt >= 0f && Time.time >= hideAt)
        {
            Hide();
            return;
        }

        UpdateFill(attack.HitDelay > 0f ? elapsed / attack.HitDelay : 1f);
        UpdateParryCue(elapsed);
    }

    // The last moment before the hit is the time to raise the guard: the fill turns the parry color and the attacker glints.
    private void UpdateParryCue(float elapsed)
    {
        float window = CombatTechniqueSettings.Current.ParryWindow * GameOptions.TimingShare;
        if (cued || window <= 0f || attack.HitDelay <= window || elapsed < attack.HitDelay - window || elapsed >= attack.HitDelay)
        {
            return;
        }

        cued = true;
        GameObject glint = CombatFeelSettings.Current.ParryCueEffect;
        if (glint != null)
        {
            PrefabPool.Spawn(glint, transform.position + Vector3.up * 1.6f, Quaternion.identity);
        }
    }

    private void Place()
    {
        marker.position = transform.position + Vector3.up * groundOffset;
        Vector3 direction = ai.AttackDirection;
        direction.y = 0f;
        marker.rotation = Quaternion.LookRotation(direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward, Vector3.up);
    }

    private void UpdateFill(float progress)
    {
        progress = Mathf.Clamp01(progress);
        fill.localScale = new Vector3(progress, 1f, progress);
        Color color = CombatFeelSettings.Current.TelegraphFillColor;
        float window = CombatTechniqueSettings.Current.ParryWindow * GameOptions.TimingShare;
        if (attack != null && attack.HitDelay > window && progress >= 1f - window / attack.HitDelay && progress < 1f)
        {
            color = CombatFeelSettings.Current.ParryCueColor;
        }
        else if (progress >= 1f)
        {
            color.a = Mathf.Min(1f, color.a * 1.8f);
        }

        block.SetColor(BaseColorId, color);
        block.SetColor(ColorId, color);
        fillRenderer.SetPropertyBlock(block);
    }

    private void Hide()
    {
        hideAt = -1f;
        if (marker != null)
        {
            marker.gameObject.SetActive(false);
        }
    }

    private bool EnsureMarker()
    {
        if (marker != null)
        {
            return true;
        }

        CombatFeelSettings feel = CombatFeelSettings.Current;
        if (feel.TelegraphMaterial == null)
        {
            if (!warnedNoMaterial)
            {
                warnedNoMaterial = true;
                Debug.LogWarning("CombatFeel has no telegraph material, so monster attacks show no warning.", feel);
            }

            return false;
        }

        // Kept out of the monster's hierarchy so the monster's scale can't change the marker's size.
        marker = new GameObject($"{name} Attack Telegraph").transform;
        block = new MaterialPropertyBlock();
        areaFilter = CreatePart("Area", marker, feel, feel.TelegraphAreaColor, out _);
        fillFilter = CreatePart("Fill", marker, feel, feel.TelegraphFillColor, out fillRenderer);
        fill = fillFilter.transform;
        // Slightly above the area so the two never fight over the same pixels.
        fill.localPosition = Vector3.up * 0.01f;
        marker.gameObject.SetActive(false);
        return true;
    }

    private MeshFilter CreatePart(string partName, Transform parent, CombatFeelSettings feel, Color color, out MeshRenderer meshRenderer)
    {
        var part = new GameObject(partName);
        part.transform.SetParent(parent, false);
        var filter = part.AddComponent<MeshFilter>();
        meshRenderer = part.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = feel.TelegraphMaterial;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        var colorBlock = new MaterialPropertyBlock();
        colorBlock.SetColor(BaseColorId, color);
        colorBlock.SetColor(ColorId, color);
        meshRenderer.SetPropertyBlock(colorBlock);
        return filter;
    }

    /// <summary>A flat fan of radius 1 pointing along +Z, covering the arc in degrees (360 is a full disc).</summary>
    public static Mesh FanFor(float arc)
    {
        int key = Mathf.RoundToInt(Mathf.Clamp(arc, 1f, 360f));
        if (Fans.TryGetValue(key, out Mesh cached) && cached != null)
        {
            return cached;
        }

        // An even count puts a vertex straight ahead, so the marker reaches its full range where the monster aims.
        int segments = Mathf.Max(4, Mathf.CeilToInt(key / DegreesPerSegment));
        segments += segments % 2;
        var vertices = new Vector3[segments + 2];
        var triangles = new int[segments * 3];
        vertices[0] = Vector3.zero;
        for (int i = 0; i <= segments; i++)
        {
            float angle = (-key * 0.5f + key * i / (float)segments) * Mathf.Deg2Rad;
            vertices[i + 1] = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
        }

        for (int i = 0; i < segments; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = i + 2;
        }

        var mesh = new Mesh { name = $"Telegraph Fan {key}", vertices = vertices, triangles = triangles };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        Fans[key] = mesh;
        return mesh;
    }
}
