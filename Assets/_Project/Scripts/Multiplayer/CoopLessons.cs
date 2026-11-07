using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Learning by watching: what a friend practises nearby teaches the player a share too (LevelingSettings › Watch Share,
/// within Watch Reach in the same place). Each player's practice is gathered per skill and sent every couple of seconds
/// (<c>SafeHaven.Lesson</c>, passed on by the host); what is learned this way is never sent again, so it can't echo.
/// Plain C#: <see cref="CoopSession"/> ticks it and owns the connection.
/// </summary>
public sealed class CoopLessons : ICoopPart
{
    private const string LessonMessage = "SafeHaven.Lesson";
    private const float SendEvery = 2f;

    private readonly Dictionary<ProficiencyDefinition, float> pending = new Dictionary<ProficiencyDefinition, float>();
    private CoopLink link;
    private float nextSend;

    public void Begin(CoopLink session)
    {
        link = session;
        pending.Clear();
        link.On(LessonMessage, OnLesson);
        ExperienceTracker.Practised += OnPractised;
    }

    public void FriendLeft(ulong clientId)
    {
    }

    public void End()
    {
        ExperienceTracker.Practised -= OnPractised;
        pending.Clear();
        link = null;
    }

    public void Tick()
    {
        if (link == null || pending.Count == 0 || Time.unscaledTime < nextSend || Player.Current == null)
        {
            return;
        }

        nextSend = Time.unscaledTime + SendEvery;
        Vector3 at = Player.Current.transform.position;
        foreach (KeyValuePair<ProficiencyDefinition, float> lesson in pending)
        {
            using var writer = new FastBufferWriter(256, Allocator.Temp);
            Write(writer, CoopPlace.Key, at, lesson.Key.Id, lesson.Value);
            link.ToOthers(LessonMessage, writer);
        }

        pending.Clear();
    }

    private void OnPractised(ProficiencyDefinition skill, float amount)
    {
        pending.TryGetValue(skill, out float sum);
        pending[skill] = sum + amount;
    }

    private void OnLesson(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out FixedString128Bytes place);
        reader.ReadValueSafe(out Vector3 at);
        reader.ReadValueSafe(out FixedString64Bytes skillId);
        reader.ReadValueSafe(out float amount);
        // The host passes a guest's lesson on to the others.
        if (link.IsHost)
        {
            using var writer = new FastBufferWriter(256, Allocator.Temp);
            Write(writer, place, at, skillId.ToString(), amount);
            link.ToGuests(LessonMessage, writer, sender);
        }

        LevelingSettings settings = LevelingSettings.Current;
        ProficiencyDefinition skill = settings.FindSkill(skillId.ToString());
        if (skill != null && Player.Current != null && place.Equals(CoopPlace.Key)
            && (Player.Current.transform.position - at).sqrMagnitude <= settings.WatchReach * settings.WatchReach)
        {
            ExperienceTracker.LearnByWatching(skill, amount * settings.WatchShare);
        }
    }

    private static void Write(FastBufferWriter writer, FixedString128Bytes place, Vector3 at, string skill, float amount)
    {
        writer.WriteValueSafe(place);
        writer.WriteValueSafe(at);
        writer.WriteValueSafe(new FixedString64Bytes(skill));
        writer.WriteValueSafe(amount);
    }
}
