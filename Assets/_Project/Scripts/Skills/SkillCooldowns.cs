using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// When each skill can be cast again. Cooldowns belong to the skill, not the slot, so moving a skill to another slot
/// doesn't reset it. Plain C#; the caller passes the time, so it can be tested without a scene.
/// </summary>
public sealed class SkillCooldowns
{
    private readonly struct Window
    {
        public readonly float Start;
        public readonly float End;

        public Window(float start, float end)
        {
            Start = start;
            End = end;
        }
    }

    private readonly Dictionary<SkillDefinition, Window> windows = new Dictionary<SkillDefinition, Window>();

    public void Start(SkillDefinition skill, float now, float duration)
    {
        if (skill == null)
        {
            return;
        }

        if (duration <= 0f)
        {
            windows.Remove(skill);
            return;
        }

        windows[skill] = new Window(now, now + duration);
    }

    public bool IsReady(SkillDefinition skill, float now) => Remaining(skill, now) <= 0f;

    /// <summary>Seconds left, or 0 when ready.</summary>
    public float Remaining(SkillDefinition skill, float now)
    {
        return skill != null && windows.TryGetValue(skill, out Window window) ? Mathf.Max(0f, window.End - now) : 0f;
    }

    /// <summary>Share of the cooldown still to go, from 1 (just cast) to 0 (ready).</summary>
    public float Fraction(SkillDefinition skill, float now)
    {
        if (skill == null || !windows.TryGetValue(skill, out Window window))
        {
            return 0f;
        }

        float length = window.End - window.Start;
        return length > 0f ? Mathf.Clamp01((window.End - now) / length) : 0f;
    }

    public void Clear() => windows.Clear();
}
