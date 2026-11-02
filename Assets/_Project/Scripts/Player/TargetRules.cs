/// <summary>
/// How the player's focus chooses among enemies, kept pure so it can be tested. A target is scored by how far it is from the
/// aim and from the player (lower is better); the current one is kept until another is clearly better, so the focus doesn't
/// flicker in a crowd, and it stands aside while the aim points well away from it, so the player can always swing elsewhere.
/// </summary>
public static class TargetRules
{
    /// <summary>Lower is better: an enemy near the aim and near the player.</summary>
    public static float Score(float angleFromAim, float distance, float range) => angleFromAim / 90f + distance / UnityEngine.Mathf.Max(0.01f, range);

    /// <summary>True when a new enemy may be taken up: within the acquiring cone and range.</summary>
    public static bool CanAcquire(float angleFromAim, float distance, float cone, float range) => angleFromAim <= cone && distance <= range;

    /// <summary>True when a challenger's score beats the current target's by the margin.</summary>
    public static bool ShouldSwitch(float currentScore, float otherScore, float margin) => otherScore + margin < currentScore;

    /// <summary>True while the aim points so far from the target that the player means to look elsewhere.</summary>
    public static bool IsAimingAway(float angleFromAim, float freeAimAngle) => angleFromAim > freeAimAngle;
}
