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
            writer.WriteNetworkSerializable(new PracticeMessage { Place = CoopPlace.Key, At = at, Skill = new FixedString64Bytes(lesson.Key.Id), Amount = lesson.Value });
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
        reader.ReadNetworkSerializable(out PracticeMessage lesson);
        // The host passes a guest's lesson on to the others.
        if (link.IsHost)
        {
            using var writer = new FastBufferWriter(256, Allocator.Temp);
            writer.WriteNetworkSerializable(lesson);
            link.ToGuests(LessonMessage, writer, sender);
        }

        LevelingSettings settings = LevelingSettings.Current;
        ProficiencyDefinition skill = settings.FindSkill(lesson.Skill.ToString());
        if (skill != null && Player.Current != null && lesson.Place.Equals(CoopPlace.Key)
            && (Player.Current.transform.position - lesson.At).sqrMagnitude <= settings.WatchReach * settings.WatchReach)
        {
            ExperienceTracker.LearnByWatching(skill, lesson.Amount * settings.WatchShare);
        }
    }

    // The message: where (CoopPlace.Key) and where they stood, the skill's id, and how much was practised.
    private struct PracticeMessage : INetworkSerializable
    {
        public FixedString128Bytes Place;
        public Vector3 At;
        public FixedString64Bytes Skill;
        public float Amount;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Place);
            serializer.SerializeValue(ref At);
            serializer.SerializeValue(ref Skill);
            serializer.SerializeValue(ref Amount);
        }
    }
}
