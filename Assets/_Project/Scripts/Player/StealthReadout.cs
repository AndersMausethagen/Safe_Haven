using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Stealth you can read from above (Docs/RangedStealthResearch.md §3.1), for the local player only: the creature noticing them most has
/// its sight cone and close-sense ring drawn faintly on the ground while its awareness fills (Shadow Tactics; its "?" forming over it is
/// AwarenessIndicator's); how far a sound carries is the HUD's noise meter (NoiseMeterView), not a ring (removed 2026-11-15); sneaking
/// and noticed by nothing, a dark ring sits at their feet; one noticing them from off screen has an arrow at the screen's edge (Hitman).
/// Looks are PerceptionSettings › Presentation; all ground marks use the telegraph material. Added by PlayerStealth.
/// </summary>
[DisallowMultipleComponent]
public sealed class StealthReadout : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    // Creatures further than this aren't looked at; a level below this is a glance that came to nothing.
    private const float LookOut = 40f;
    private const float ShownFrom = 0.03f;
    private const float UnseenRadius = 0.75f;

    private Player player;
    private PlayerStealth stealth;
    private Transform cone;
    private Transform close;
    private Transform unseen;
    private MaterialPropertyBlock block;
    private RectTransform edgeArrow;
    private TextMeshProUGUI edgeGlyph;
    private bool built;

    private void Awake()
    {
        player = GetComponent<Player>();
        stealth = GetComponent<PlayerStealth>();
    }

    private void OnDisable() => HideAll();

    private void OnDestroy()
    {
        foreach (Transform mark in new[] { cone, close, unseen })
        {
            if (mark != null)
            {
                Destroy(mark.gameObject);
            }
        }

        if (edgeArrow != null)
        {
            Destroy(edgeArrow.root.gameObject);
        }
    }

    private bool IsLocal => player != null && Player.Current == player;

    private void LateUpdate()
    {
        PerceptionSettings rules = PerceptionSettings.Current;
        if (!IsLocal || !rules.ShowStealthReadout || GamePause.IsPaused || player.Health == null || !Build())
        {
            HideAll();
            return;
        }

        MonsterAI noticer = null;
        MonsterAI game = null;
        float gameDistance = float.MaxValue;
        float most = ShownFrom;
        bool hunted = false;
        Vector3 here = transform.position;
        foreach (MonsterAI monster in MonsterAI.Active)
        {
            float distance = monster != null ? (monster.transform.position - here).sqrMagnitude : float.MaxValue;
            if (monster == null || distance > LookOut * LookOut)
            {
                continue;
            }

            hunted |= monster.IsEngaging(player.Health);
            float level = monster.NoticeLevelOf(player.Health);
            if (level > most)
            {
                most = level;
                noticer = monster;
            }

            // Game bolts at the first notice (MonsterSenses.CanNotice) and has no awareness to fill: the nearest one in sight
            // range is the one being stalked.
            if (monster.Definition != null && monster.Definition.Timid && !monster.IsBolting && monster.CurrentState != MonsterAI.State.Dead
                && distance < gameDistance)
            {
                float reach = SenseProfile.Of(monster.Definition).SightRange * rules.MaxRangeScale;
                if (distance < reach * reach)
                {
                    game = monster;
                    gameDistance = distance;
                }
            }
        }

        // Stalking game (sneaking, nothing hostile noticing): its cone shows how near they can come, as a hunter's does.
        if (noticer == null && game != null && stealth != null && stealth.IsSneaking)
        {
            UpdateCone(game, rules.SuspiciousAt * 0.5f, rules);
            UpdateUnseen(false, rules);
            UpdateEdge(null, 0f, rules);
            return;
        }

        UpdateCone(noticer, most, rules);
        UpdateUnseen(noticer == null && !hunted && stealth != null && stealth.IsSneaking, rules);
        UpdateEdge(noticer, most, rules);
    }

    // The noticing creature's cone, out to how far it sees the player as they are now, and the ring inside which it notices from any side.
    private void UpdateCone(MonsterAI noticer, float level, PerceptionSettings rules)
    {
        if (noticer == null || noticer.Definition == null)
        {
            cone.gameObject.SetActive(false);
            close.gameObject.SetActive(false);
            return;
        }

        SenseProfile senses = SenseProfile.Of(noticer.Definition);
        float visibility = stealth != null ? stealth.Visibility : 1f;
        float range = PerceptionRules.EffectiveSightRange(senses, visibility, rules, AtmosphereConditions.SightScale(rules));
        Color color = rules.ConeColor;
        color.a *= Mathf.Lerp(0.45f, 1f, Mathf.Clamp01(level / Mathf.Max(0.01f, rules.SuspiciousAt)));
        Vector3 at = Ground(noticer.transform.position);
        Vector3 facing = noticer.transform.forward;
        facing.y = 0f;
        cone.GetComponent<MeshFilter>().sharedMesh = AttackTelegraph.FanFor(Mathf.Clamp(senses.HalfAngle * 2f, 1f, 360f));
        cone.SetPositionAndRotation(at, Quaternion.LookRotation(facing.sqrMagnitude > 1e-4f ? facing : Vector3.forward));
        cone.localScale = Vector3.one * Mathf.Max(0.5f, range);
        Tint(cone, color);
        cone.gameObject.SetActive(true);
        close.position = at + Vector3.up * 0.005f;
        close.localScale = Vector3.one * Mathf.Max(0.3f, senses.CloseRange);
        Tint(close, color * new Color(1f, 1f, 1f, 1.6f));
        close.gameObject.SetActive(true);
    }

    private void UpdateUnseen(bool show, PerceptionSettings rules)
    {
        unseen.gameObject.SetActive(show);
        if (show)
        {
            unseen.position = Ground(transform.position);
            unseen.localScale = Vector3.one * UnseenRadius;
            Tint(unseen, rules.UnseenColor);
        }
    }

    // A noticer off screen: an arrow at the edge, toward it.
    private void UpdateEdge(MonsterAI noticer, float level, PerceptionSettings rules)
    {
        Camera view = Camera.main;
        bool windows = UIStateManager.Current != null && UIStateManager.Current.IsAnyWindowOpen;
        Vector3 point = noticer != null && view != null ? view.WorldToViewportPoint(noticer.transform.position + Vector3.up) : Vector3.zero;
        bool offScreen = noticer != null && view != null && !windows && (point.z < 0f || point.x < 0f || point.x > 1f || point.y < 0f || point.y > 1f);
        edgeArrow.gameObject.SetActive(offScreen);
        if (!offScreen)
        {
            return;
        }

        Vector2 toward = new Vector2(point.x - 0.5f, point.y - 0.5f) * (point.z < 0f ? -1f : 1f);
        toward = toward.sqrMagnitude > 1e-6f ? toward.normalized : Vector2.up;
        float reach = 0.46f / Mathf.Max(Mathf.Abs(toward.x), Mathf.Abs(toward.y));
        Vector2 viewport = new Vector2(0.5f, 0.5f) + toward * Mathf.Min(reach, 0.62f);
        edgeArrow.anchorMin = edgeArrow.anchorMax = viewport;
        edgeArrow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(toward.y, toward.x) * Mathf.Rad2Deg - 90f);
        Color color = level >= rules.SuspiciousAt ? rules.SuspiciousColor : rules.NoticingColor;
        color.a = Mathf.Lerp(0.45f, 1f, Mathf.Clamp01(level / Mathf.Max(0.01f, rules.SuspiciousAt)));
        edgeGlyph.color = color;
    }

    private void HideAll()
    {
        foreach (Transform mark in new[] { cone, close, unseen })
        {
            if (mark != null)
            {
                mark.gameObject.SetActive(false);
            }
        }

        if (edgeArrow != null)
        {
            edgeArrow.gameObject.SetActive(false);
        }
    }

    private static Vector3 Ground(Vector3 position) => position + Vector3.up * 0.05f;

    private void Tint(Transform mark, Color color)
    {
        block.SetColor(BaseColorId, color);
        block.SetColor(ColorId, color);
        mark.GetComponent<MeshRenderer>().SetPropertyBlock(block);
    }

    // The marks, made the first time they are needed (kept out of the player's hierarchy, as the telegraphs are).
    private bool Build()
    {
        if (built)
        {
            return true;
        }

        Material material = CombatFeelSettings.Current.TelegraphMaterial;
        if (material == null)
        {
            return false;
        }

        block = new MaterialPropertyBlock();
        cone = Mark("Stealth Cone", AttackTelegraph.FanFor(120f), material);
        close = Mark("Stealth Close Sense", AttackTelegraph.BandFor(360f, 0.05f), material);
        unseen = Mark("Stealth Unseen", AttackTelegraph.BandFor(360f, 0.3f), material);
        var canvasObject = new GameObject("Stealth Edge Arrow", typeof(Canvas), typeof(CanvasScaler));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;
        var arrowObject = new GameObject("Arrow", typeof(RectTransform));
        edgeArrow = (RectTransform)arrowObject.transform;
        edgeArrow.SetParent(canvasObject.transform, false);
        edgeArrow.sizeDelta = new Vector2(48f, 48f);
        edgeGlyph = arrowObject.AddComponent<TextMeshProUGUI>();
        edgeGlyph.text = "▲";
        edgeGlyph.fontSize = 34f;
        edgeGlyph.alignment = TextAlignmentOptions.Center;
        edgeGlyph.raycastTarget = false;
        edgeArrow.gameObject.SetActive(false);
        built = true;
        HideAll();
        return true;
    }

    private static Transform Mark(string name, Mesh mesh, Material material)
    {
        var mark = new GameObject(name);
        mark.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = mark.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        mark.SetActive(false);
        return mark.transform;
    }
}
