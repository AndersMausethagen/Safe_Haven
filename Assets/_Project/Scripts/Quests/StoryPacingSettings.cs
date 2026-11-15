using UnityEngine;

/// <summary>
/// How much room the game leaves between story beats (Resources/StoryPacingSettings, Systems Editor › World › Story Pacing):
/// a quest that starts by itself never lands on the heels of the last one, on a fight's last blow or over someone talking.
/// A quest can ask for longer with its own Start Delay. Read through StoryPacing.
/// </summary>
[CreateAssetMenu(fileName = "StoryPacingSettings", menuName = "Safe Haven/Settings/Story Pacing Settings")]
public sealed class StoryPacingSettings : GameSettings<StoryPacingSettings>
{
    [Tooltip("Seconds after a quest completes before the next one that starts by itself may begin. A quest's own Start Delay replaces this.")]
    [SerializeField, Min(0f)] private float betweenQuestsSeconds = 3f;
    [Tooltip("Seconds of peace after a fight before a new quest begins or a story line is said, so the player can see they won and catch their breath.")]
    [SerializeField, Min(0f)] private float afterFightSeconds = 5f;
    [Tooltip("Seconds after someone stops talking before a new quest may begin.")]
    [SerializeField, Min(0f)] private float afterDialogueSeconds = 1.5f;
    [Tooltip("A quest ready to start waits at most this long for a fight or a conversation to end, so a monster nobody can reach can't stop the story.")]
    [SerializeField, Min(1f)] private float longestHoldSeconds = 60f;
    [Tooltip("Haven, side and optional quests that start by themselves begin only at home, and at most this many per return from a raid " +
             "(held back rather than a flood: nine started in the eleven minutes after the first raid). When none of a kind is under way, " +
             "one more of it may begin, so there is always something to do at home.")]
    [SerializeField, Min(1)] private int newPerHomecoming = 2;

    public float BetweenQuestsSeconds => betweenQuestsSeconds;
    public int NewPerHomecoming => newPerHomecoming;
    public float AfterFightSeconds => afterFightSeconds;
    public float AfterDialogueSeconds => afterDialogueSeconds;
    public float LongestHoldSeconds => longestHoldSeconds;
}
