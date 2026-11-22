using UnityEngine;

/// <summary>
/// How a creature's lasso holds the player (WebLasso: the Giant Black Widow's web, the user 2026-11-22: "lasso the player with web,
/// that grabs a hold of him, pulling him towards it. The player must escape it by spamming SPACE, like the Nøkken grab"). How long
/// a lasso holds is its attack's (AttackDefinition › Lasso Seconds); the rest is here.
/// </summary>
[CreateAssetMenu(fileName = "GrabSettings", menuName = "Safe Haven/Settings/Grabs")]
public sealed class GrabSettings : GameSettings<GrabSettings>
{
    [Tooltip("Presses of Dodge (Space) that tear the player free.")]
    [SerializeField, Min(1)] private int pressesToEscape = 8;
    [Tooltip("Presses lost each second the player doesn't press: hammering, not tapping.")]
    [SerializeField, Min(0f)] private float pressesFadePerSecond = 1f;
    [Tooltip("Metres a second the line reels the player in.")]
    [SerializeField, Min(0.1f)] private float reelSpeed = 4.5f;
    [Tooltip("The line stops reeling this far from the holder (metres, grown with its size): within its bite.")]
    [SerializeField, Min(0.5f)] private float holdDistance = 2.2f;
    [Tooltip("The line's look.")]
    [SerializeField] private Material lineMaterial;
    [SerializeField, Min(0.005f)] private float lineWidth = 0.06f;
    [Tooltip("The camera's shake while held.")]
    [SerializeField, Min(0f)] private float holdShake = 0.08f;
    [Tooltip("Heard as the line strikes, and as it snaps.")]
    [SerializeField] private SoundDefinition grabSound;
    [SerializeField] private SoundDefinition snapSound;

    public int PressesToEscape => pressesToEscape;
    public float PressesFadePerSecond => pressesFadePerSecond;
    public float ReelSpeed => reelSpeed;
    public float HoldDistance => holdDistance;
    public Material LineMaterial => lineMaterial;
    public float LineWidth => lineWidth;
    public float HoldShake => holdShake;
    public SoundDefinition GrabSound => grabSound;
    public SoundDefinition SnapSound => snapSound;
}
