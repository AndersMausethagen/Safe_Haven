using UnityEngine;

/// <summary>
/// How the Purification Altar feels when it works (Docs/AltarUXResearch_2026-11-28.md): the sounds of charging, letting go and the
/// three sizes of commit, the shapes the stage draws (glyph ring, glow, spark, shockwave), and how long each part takes. Read by
/// AltarStage. Lives at Resources/AltarFeel; the sounds are Tools/Audio/altar.py's, the shapes Tools/UI/altar_art.py's.
/// </summary>
[CreateAssetMenu(menuName = "Safe Haven/Settings/Altar Feel", fileName = "AltarFeel")]
public sealed class AltarFeel : GameSettings<AltarFeel>
{
    [Header("Sounds")]
    [SerializeField] private SoundDefinition charge;
    [SerializeField] private SoundDefinition cancel;
    [SerializeField] private SoundDefinition commitSmall;
    [SerializeField] private SoundDefinition commitLarge;
    [SerializeField] private SoundDefinition commitMilestone;
    [SerializeField] private SoundDefinition tick;
    [SerializeField] private SoundDefinition select;
    [SerializeField] private SoundDefinition purify;

    [Header("Shapes")]
    [SerializeField] private Sprite glyphRing;
    [SerializeField] private Sprite glow;
    [SerializeField] private Sprite spark;
    [SerializeField] private Sprite shock;

    [Header("Timing")]
    [Tooltip("Seconds a working is held before it is done (a step is held for half of it).")]
    [SerializeField, Min(0.1f)] private float holdSeconds = 0.7f;
    [Tooltip("Degrees a second the glyph ring turns at rest; charging turns it up to five times as fast.")]
    [SerializeField, Min(0f)] private float ringTurn = 8f;
    [Tooltip("Sparks in a step's burst; a working that matters has twice as many, a great one four times.")]
    [SerializeField, Min(0)] private int sparks = 12;

    public SoundDefinition Charge => charge;
    public SoundDefinition Cancel => cancel;
    public SoundDefinition Tick => tick;
    public SoundDefinition Select => select;
    public SoundDefinition Purify => purify;
    public Sprite GlyphRing => glyphRing;
    public Sprite Glow => glow;
    public Sprite Spark => spark;
    public Sprite Shock => shock;
    public float RingTurn => ringTurn;

    public SoundDefinition CommitFor(AltarMoment moment) =>
        moment == AltarMoment.Milestone ? commitMilestone : moment == AltarMoment.Large ? commitLarge : commitSmall;

    public float HoldFor(AltarMoment moment) => moment == AltarMoment.Small ? holdSeconds * 0.5f : holdSeconds;

    public int SparksFor(AltarMoment moment) => moment == AltarMoment.Milestone ? sparks * 4 : moment == AltarMoment.Large ? sparks * 2 : sparks;
}
