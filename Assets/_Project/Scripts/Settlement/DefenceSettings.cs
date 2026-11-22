using UnityEngine;

/// <summary>
/// The Haven's defence by its people (GuardPosts; the Keeper's view, phase 7): how many guards may stand at once (capped by the
/// defences built, so guards who can't die never set the balance), whether they and the Watch Post archer turn out on corruption nights
/// as well as ward breaches, and where they stand on those nights when no post is set. Resources/DefenceSettings.
/// </summary>
[CreateAssetMenu(menuName = "Safe Haven/Settings/Defence Settings", fileName = "DefenceSettings")]
public sealed class DefenceSettings : GameSettings<DefenceSettings>
{
    [Tooltip("Guards who may stand at once with no defences built.")]
    [SerializeField, Min(0)] private int basePosts = 2;
    [Tooltip("More guards for each tier of each standing Watch Post.")]
    [SerializeField, Min(0)] private int postsPerWatchPostTier = 1;
    [Tooltip("Guards at most, whatever is built.")]
    [SerializeField, Min(1)] private int mostPosts = 8;
    [Tooltip("Guards turn out on corruption nights too (from the warning until the band is gone), and the Watch Post archer shoots.")]
    [SerializeField] private bool corruptionNights = true;
    [Tooltip("Metres from the portal toward the night's side where guards stand when no post is set.")]
    [SerializeField, Min(2f)] private float nightPostDistance = 14f;
    [Tooltip("Degrees between guards standing toward the night's side.")]
    [SerializeField, Range(0f, 90f)] private float nightPostSpread = 25f;
    [Tooltip("On a corruption night, posts within this many degrees of its side (seen from the portal) are taken; the rest wait.")]
    [SerializeField, Range(10f, 180f)] private float nightPostSide = 60f;

    [Header("The horn")]
    [Tooltip("Who brings the horn (HavenHorn): it can be sounded once they live in the Haven. Empty: from the start.")]
    [SerializeField] private ResidentDefinition hornKeeper;
    [Tooltip("Seconds a sounding holds: the guards at their posts, everyone else at the portal. Sounding it again stands them down.")]
    [SerializeField, Min(10f)] private float hornSeconds = 150f;
    [Tooltip("What the horn sounds like, at the portal.")]
    [SerializeField] private SoundDefinition hornSound;

    [Header("Ward breaches")]
    [Tooltip("In a ward breach everyone at home turns out to fight, not only the guards (the user, 2026-11-22: \"all NPCs should help " +
             "fight\"): guards at their posts, everyone else round the portal. Off: only the guards, as many as the posts hold.")]
    [SerializeField] private bool everyoneFightsBreaches = true;
    [Tooltip("How someone without a guard's job fights in a breach, when they have no way of fighting on a raid of their own.")]
    [SerializeField] private CompanionDefinition breachFighter;

    public bool EveryoneFightsBreaches => everyoneFightsBreaches;
    public CompanionDefinition BreachFighter => breachFighter;

    public ResidentDefinition HornKeeper => hornKeeper;
    public float HornSeconds => hornSeconds;
    public SoundDefinition HornSound => hornSound;

    public int BasePosts => basePosts;
    public int PostsPerWatchPostTier => postsPerWatchPostTier;
    public int MostPosts => mostPosts;
    public bool CorruptionNights => corruptionNights;
    public float NightPostDistance => nightPostDistance;
    public float NightPostSpread => nightPostSpread;
    public float NightPostSide => nightPostSide;
}
