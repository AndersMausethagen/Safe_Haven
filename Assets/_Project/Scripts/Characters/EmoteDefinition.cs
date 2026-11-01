using UnityEngine;

/// <summary>
/// A gesture a character can make: a one-shot (wave, laugh) or a pose held until it is ended (sit, sleep), played on the
/// "Emote" layer of the character's Animator. EmoteLayerBuilder makes the states from these clips, named after the
/// asset's StateName; CharacterEmotes plays them.
/// </summary>
[CreateAssetMenu(menuName = "Safe Haven/Characters/Emote", fileName = "Emote")]
public sealed class EmoteDefinition : ScriptableObject
{
    [SerializeField] private string displayName;
    [Tooltip("The clip of a one-shot; for a held pose, the loop.")]
    [SerializeField] private AnimationClip clip;
    [Tooltip("Held poses: played before the loop. Optional.")]
    [SerializeField] private AnimationClip begin;
    [Tooltip("Held poses: played when the pose ends. Optional.")]
    [SerializeField] private AnimationClip end;
    [Tooltip("Held until ended (moving ends it) instead of playing once.")]
    [SerializeField] private bool held;
    [Tooltip("Offered on the command wheel.")]
    [SerializeField] private bool onWheel = true;

    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public AnimationClip Clip => clip;
    public AnimationClip Begin => begin;
    public AnimationClip End => end;
    public bool IsHeld => held;
    public bool OnWheel => onWheel;

    /// <summary>The Animator state of the one-shot or the held loop.</summary>
    public string StateName => "Emote " + name;
    public string BeginState => StateName + " Begin";
    public string EndState => StateName + " End";
    public bool HasBegin => held && begin != null;
    public bool HasEnd => held && end != null;

#if UNITY_EDITOR
    public void EditorSet(string displayName, AnimationClip clip, AnimationClip begin, AnimationClip end, bool held, bool onWheel)
    {
        this.displayName = displayName;
        this.clip = clip;
        this.begin = begin;
        this.end = end;
        this.held = held;
        this.onWheel = onWheel;
    }
#endif
}
