using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Warns the player that a monster's attack is coming: during the wind-up it draws the attack's area (range and arc from
/// its AttackDefinition) on the ground and fills it from the monster outward; when the fill reaches the edge, the hit
/// lands. Players learn to read it and dodge out. Colors and material come from the CombatFeel settings.
/// <para>
/// What the player can do about it shows too (Docs/EnemyCombatResearch.md §4.2): a strike any blow breaks off fills in its own colour,
/// a heavy one gets a dark edge and a low swell as it starts, and once a blow has spent itself a ring at the creature's feet shrinks
/// over the time it can't act: the opening to punish it in, the "after" half of readability.
/// </para>
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
    // One unit-radius fan per arc, shared by every monster; and one band (an arc's outer edge, or a ring) per arc and width.
    private static readonly Dictionary<int, Mesh> Fans = new Dictionary<int, Mesh>();
    private static readonly Dictionary<(int, int), Mesh> Bands = new Dictionary<(int, int), Mesh>();

    // Entering Play doesn't reload scripts in this project, so statics are cleared when it starts.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        Fans.Clear();
        Bands.Clear();
    }

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
    private bool interruptible;
    private bool cuesParry = true;
    private static bool warnedNoMaterial;
    private MeshFilter edgeFilter;
    private Transform opening;
    private MeshRenderer openingRenderer;
    private MaterialPropertyBlock openingBlock;
    private float bodyRadius = 0.5f;

    private void Awake()
    {
        ai = GetComponent<MonsterAI>();
        if (TryGetComponent(out UnityEngine.AI.NavMeshAgent agent))
        {
            bodyRadius = agent.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);
        }
    }

    /// <summary>A heavy strike: marked as its great blow (Monster Attack › Heavy), or hard-hitting (Impact at least
    /// <paramref name="heavyImpact"/>). It gets a dark edge and a low swell as it starts.</summary>
    public static bool IsHeavy(bool marked, float impact, float heavyImpact) => marked || impact >= heavyImpact;

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
        if (opening != null)
        {
            opening.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (marker != null)
        {
            Destroy(marker.gameObject);
        }

        if (opening != null)
        {
            Destroy(opening.gameObject);
        }
    }

    private void OnAttackStarted()
    {
        attack = ai.CurrentAttack;
        // A lob's warning is the ring where it will land (LobStrike), not an area around the thrower.
        if (ai.ActiveAction != null && ai.ActiveAction.kind == MonsterAttackKind.Lob)
        {
            attack = null;
            Hide();
            return;
        }

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
        // The AI's own rule (an Unyielding elite's strikes break off for nothing until its layer breaks, whatever the strike allows).
        interruptible = ai.AnyBlowBreaksStrike;
        // A shot leaves the hand and a charge starts its rush at Hit Delay: contact comes later, by the flight or the run, so the
        // moment before Hit Delay is no time to parry them (a guard raised on that cue blocks instead).
        cuesParry = ai.ActiveAction == null || (ai.ActiveAction.kind != MonsterAttackKind.Charge && !MonsterAttacks.IsRanged(ai.ActiveAction.kind));
        ShowHeavy();
        Place();
        UpdateFill(0f);
    }

    // A heavy strike's dark edge round its area, and the low swell under its wind-up.
    private void ShowHeavy()
    {
        CombatFeelSettings feel = CombatFeelSettings.Current;
        // The first blow of the action only: a follow-up is its everyday strike again.
        bool heavy = IsHeavy(ai.ActiveAction != null && ai.ActiveAction.heavy && ai.ActiveAction.attack == attack, attack.Impact, feel.HeavyImpact);
        edgeFilter.gameObject.SetActive(heavy);
        if (!heavy)
        {
            return;
        }

        edgeFilter.sharedMesh = BandFor(attack.Arc, feel.HeavyEdgeWidth);
        if (feel.HeavyWindupSound != null)
        {
            AudioManager.Play(feel.HeavyWindupSound, transform.position);
        }
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
        UpdateOpening();
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
        if (cued || !cuesParry || attack.Unblockable || window <= 0f || attack.HitDelay <= window || elapsed < attack.HitDelay - window || elapsed >= attack.HitDelay)
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
        // A blow no guard stops shows in its own colour, and has no parry moment to cue; one any blow breaks off shows in another.
        bool unblockable = attack != null && attack.Unblockable;
        CombatFeelSettings feel = CombatFeelSettings.Current;
        Color color = unblockable ? feel.UnblockableColor : interruptible ? feel.InterruptibleColor : feel.TelegraphFillColor;
        float window = CombatTechniqueSettings.Current.ParryWindow * GameOptions.TimingShare;
        if (!unblockable && cuesParry && attack != null && attack.HitDelay > window && progress >= 1f - window / attack.HitDelay && progress < 1f)
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
        edgeFilter = CreatePart("Heavy Edge", marker, feel, feel.HeavyEdgeColor, out _);
        edgeFilter.transform.localPosition = Vector3.up * 0.02f;
        edgeFilter.gameObject.SetActive(false);
        marker.gameObject.SetActive(false);
        return true;
    }

    // The ring at its feet while its blow is spent: from the body's edge out by Opening Reach, shrinking in as the opening closes.
    private void UpdateOpening()
    {
        bool open = ai.TryGetOpening(out float left, out float length);
        if (!open)
        {
            if (opening != null && opening.gameObject.activeSelf)
            {
                opening.gameObject.SetActive(false);
            }

            return;
        }

        CombatFeelSettings feel = CombatFeelSettings.Current;
        if (opening == null)
        {
            if (feel.TelegraphMaterial == null)
            {
                return;
            }

            opening = new GameObject($"{name} Opening").transform;
            var filter = opening.gameObject.AddComponent<MeshFilter>();
            openingRenderer = opening.gameObject.AddComponent<MeshRenderer>();
            openingRenderer.sharedMaterial = feel.TelegraphMaterial;
            openingRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            openingRenderer.receiveShadows = false;
            openingBlock = new MaterialPropertyBlock();
            // Opening Width wide at its widest; it narrows a little with the ring as it closes.
            filter.sharedMesh = BandFor(360f, feel.OpeningWidth / (bodyRadius + feel.OpeningWidth + feel.OpeningReach));
        }

        float share = Mathf.Clamp01(left / length);
        float radius = bodyRadius + feel.OpeningWidth + feel.OpeningReach * share;
        opening.SetPositionAndRotation(transform.position + Vector3.up * (groundOffset + 0.005f), Quaternion.identity);
        opening.localScale = Vector3.one * radius;
        Color color = feel.OpeningColor;
        color.a *= Mathf.Lerp(0.35f, 1f, share);
        openingBlock.SetColor(BaseColorId, color);
        openingBlock.SetColor(ColorId, color);
        openingRenderer.SetPropertyBlock(openingBlock);
        if (!opening.gameObject.activeSelf)
        {
            opening.gameObject.SetActive(true);
        }
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

    /// <summary>A flat band of outer radius 1 along an arc in degrees pointing along +Z (360: a ring), <paramref name="width"/> of the
    /// radius wide.</summary>
    public static Mesh BandFor(float arc, float width)
    {
        int key = Mathf.RoundToInt(Mathf.Clamp(arc, 1f, 360f));
        int thick = Mathf.RoundToInt(Mathf.Clamp(width, 0.01f, 1f) * 100f);
        if (Bands.TryGetValue((key, thick), out Mesh cached) && cached != null)
        {
            return cached;
        }

        int segments = Mathf.Max(4, Mathf.CeilToInt(key / DegreesPerSegment));
        segments += segments % 2;
        float inner = 1f - thick / 100f;
        var vertices = new Vector3[(segments + 1) * 2];
        var triangles = new int[segments * 6];
        for (int i = 0; i <= segments; i++)
        {
            float angle = (-key * 0.5f + key * i / (float)segments) * Mathf.Deg2Rad;
            var along = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            vertices[i * 2] = along * inner;
            vertices[i * 2 + 1] = along;
        }

        for (int i = 0; i < segments; i++)
        {
            int a = i * 2;
            triangles[i * 6] = a;
            triangles[i * 6 + 1] = a + 1;
            triangles[i * 6 + 2] = a + 3;
            triangles[i * 6 + 3] = a;
            triangles[i * 6 + 4] = a + 3;
            triangles[i * 6 + 5] = a + 2;
        }

        var mesh = new Mesh { name = $"Telegraph Band {key} {thick}", vertices = vertices, triangles = triangles };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        Bands[(key, thick)] = mesh;
        return mesh;
    }
}
