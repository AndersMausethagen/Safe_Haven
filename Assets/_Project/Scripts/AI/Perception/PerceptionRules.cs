using UnityEngine;

/// <summary>A monster's own senses, read from its MonsterDefinition.</summary>
public readonly struct SenseProfile
{
    public readonly float SightRange;
    /// <summary>Half the sight cone, in degrees from straight ahead.</summary>
    public readonly float HalfAngle;
    /// <summary>Within this distance it notices a target from any direction, even through cover.</summary>
    public readonly float CloseRange;
    public readonly float Hearing;
    public readonly float Alertness;

    public SenseProfile(float sightRange, float sightAngle, float closeRange, float hearing, float alertness)
    {
        SightRange = Mathf.Max(0f, sightRange);
        HalfAngle = Mathf.Clamp(sightAngle, 0f, 360f) * 0.5f;
        CloseRange = Mathf.Max(0f, closeRange);
        Hearing = Mathf.Max(0f, hearing);
        Alertness = Mathf.Max(0f, alertness);
    }

    public static SenseProfile Of(MonsterDefinition definition) => new SenseProfile(definition.DetectionRange,
        definition.SightAngle, definition.CloseSenseRange, definition.Hearing, definition.Alertness);
}

/// <summary>
/// The pure arithmetic of noticing (tested in PerceptionTests): how fast a glimpse fills awareness and how much a sound
/// raises it. MonsterSenses supplies distances, angles and line of sight; nothing here touches the scene.
/// </summary>
public static class PerceptionRules
{
    /// <summary>
    /// How far a monster sees a target with this visibility: its sight range × √visibility, kept between the settings'
    /// bounds, so sneaking lets you come closer and fighting is seen from further away.
    /// </summary>
    public static float EffectiveSightRange(SenseProfile senses, float visibility, PerceptionSettings rules, float environment = 1f)
    {
        float scale = Mathf.Clamp(Mathf.Sqrt(Mathf.Max(0f, visibility)), rules.MinRangeScale, rules.MaxRangeScale);
        return senses.SightRange * scale * Mathf.Clamp01(environment);
    }

    /// <summary>
    /// Share of their sight range monsters keep in fog of this density: 1 in clear air, falling linearly to
    /// <paramref name="sightInDenseFog"/> at <paramref name="denseFog"/> and staying there beyond.
    /// </summary>
    public static float FogSightScale(float density, float denseFog, float sightInDenseFog)
    {
        float thickness = denseFog > 0f ? Mathf.Clamp01(density / denseFog) : 0f;
        return Mathf.Lerp(1f, sightInDenseFog, thickness);
    }

    /// <summary>
    /// Awareness gained per second of seeing a target at <paramref name="distance"/> metres and
    /// <paramref name="angle"/> degrees off straight ahead (the caller has checked line of sight). Fog shortens the range
    /// through <paramref name="environment"/> (AtmosphereConditions.SightScale). Inside close range it
    /// notices quickly from any direction; in the cone it takes from Near to Far Notice Seconds, scaled by distance;
    /// outside both, 0. Visibility and the monster's alertness divide the time.
    /// </summary>
    public static float NoticeRate(float distance, float angle, SenseProfile senses, float visibility, PerceptionSettings rules,
        float environment = 1f)
    {
        float strength = Mathf.Max(0f, visibility) * senses.Alertness;
        if (strength <= 0f)
        {
            return 0f;
        }

        if (distance <= senses.CloseRange)
        {
            return strength / rules.CloseNoticeSeconds;
        }

        float range = EffectiveSightRange(senses, visibility, rules, environment);
        if (range <= 0f || distance > range || angle > senses.HalfAngle)
        {
            return 0f;
        }

        float seconds = Mathf.Lerp(rules.NearNoticeSeconds, rules.FarNoticeSeconds, distance / range);
        return strength / seconds;
    }

    /// <summary>
    /// The awareness a sound of <paramref name="radius"/> metres gives a monster <paramref name="distance"/> metres away:
    /// 0 beyond the radius (× its hearing), from Heard Awareness Far at the edge up to Heard Awareness Near at the source.
    /// </summary>
    public static float HeardLevel(float distance, float radius, float hearing, PerceptionSettings rules)
    {
        float reach = radius * Mathf.Max(0f, hearing);
        if (reach <= 0f || distance > reach)
        {
            return 0f;
        }

        float closeness = 1f - distance / reach;
        return Mathf.Lerp(rules.HeardAwarenessFar, rules.HeardAwarenessNear, closeness);
    }
}
