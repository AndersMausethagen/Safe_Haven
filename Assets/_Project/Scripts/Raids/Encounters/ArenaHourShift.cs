using System.Collections;
using UnityEngine;

/// <summary>
/// A boss arena's light that changes with the fight: at one stage the hour slides to dusk (the Heartroot's 33%: "the old heart
/// splits", the canopy closes and the hollow falls into deep dusk, Docs/HeartrootBossReview_2026-11-17.md C8), so the player's own
/// glow (PlayerGlow) and the boss's heart are what light the floor; when the boss falls the day comes back. It moves the scene's
/// DayNightCycle hour, so fog, sky, sun and night volume all follow as they do for any hour.
/// <para>
/// Where it belongs: on an object in the arena scene, with the boss placed there. A friend's game (co-op) runs its own from its copy of
/// the boss's stages, as RisingGround does: the boss is known by its BossDefinition.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[OnAFriendsCopy(CopyRule.Same, "each game darkens its own arena from its copy of the boss's stages")]
public sealed class ArenaHourShift : MonoBehaviour
{
    [Tooltip("The boss whose stage brings the dusk. Known by its Boss Definition, so a friend's copy does it too.")]
    [SerializeField] private BossEncounter boss;
    [Tooltip("The stage that darkens the arena (the Heartroot's 33%: 2).")]
    [SerializeField, Min(1)] private int atPhase = 2;
    [Tooltip("The hour it slides to (deep dusk, before full night).")]
    [SerializeField, Range(0f, 24f)] private float hour = 20.4f;
    [Tooltip("Seconds the slide takes.")]
    [SerializeField, Min(0.1f)] private float seconds = 4f;
    [Tooltip("Seconds the day takes to come back once the boss has fallen. 0: it stays dark.")]
    [SerializeField, Min(0f)] private float backSeconds = 6f;
    [Tooltip("A light at the boss's chest from the dusk on, so the topple's opening shows in the dark. Black: none.")]
    [SerializeField] private Color heartColour = new Color(0.55f, 1f, 0.45f);
    [SerializeField, Min(0f)] private float heartIntensity = 3f;
    [SerializeField, Min(1f)] private float heartRange = 10f;
    [SerializeField, Min(0f)] private float heartHeight = 2.6f;

    private BossDefinition watching;
    private DayNightCycle cycle;
    private float dayHour;
    private bool dark;
    private Light heart;
    private Coroutine sliding;

    private void OnEnable()
    {
        BossDefinition placed = BossEncounter.DefinitionOf(boss);
        if (placed != null)
        {
            watching = placed;
        }

        BossEncounter.AnyPhaseEntered += OnPhase;
        BossEncounter.AnyFell += OnFell;
    }

    private void OnDisable()
    {
        BossEncounter.AnyPhaseEntered -= OnPhase;
        BossEncounter.AnyFell -= OnFell;
    }

    private void OnPhase(BossEncounter from, int phase)
    {
        if (dark || watching == null || from == null || from.Definition != watching || phase < atPhase)
        {
            return;
        }

        cycle = cycle != null ? cycle : FindAnyObjectByType<DayNightCycle>();
        if (cycle == null)
        {
            return;
        }

        dark = true;
        dayHour = cycle.Hour;
        Slide(dayHour, hour, seconds);
        if (heartColour.maxColorComponent > 0f && heartIntensity > 0f)
        {
            var holder = new GameObject("Heart Light");
            holder.transform.SetParent(from.transform, false);
            holder.transform.localPosition = Vector3.up * heartHeight;
            heart = holder.AddComponent<Light>();
            heart.type = LightType.Point;
            heart.color = heartColour;
            heart.intensity = heartIntensity;
            heart.range = heartRange;
            heart.shadows = LightShadows.None;
        }
    }

    private void OnFell(BossEncounter from)
    {
        if (!dark || from == null || from.Definition != watching)
        {
            return;
        }

        if (heart != null)
        {
            Destroy(heart.gameObject);
        }

        if (backSeconds > 0f && cycle != null)
        {
            Slide(cycle.Hour, dayHour, backSeconds);
        }
    }

    private void Slide(float from, float to, float over)
    {
        if (sliding != null)
        {
            StopCoroutine(sliding);
        }

        sliding = StartCoroutine(Sliding(from, to, over));
    }

    private IEnumerator Sliding(float from, float to, float over)
    {
        for (float t = 0f; t < over; t += Time.deltaTime)
        {
            if (cycle == null)
            {
                yield break;
            }

            cycle.Hour = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / over));
            yield return null;
        }

        if (cycle != null)
        {
            cycle.Hour = to;
        }

        sliding = null;
    }
}
