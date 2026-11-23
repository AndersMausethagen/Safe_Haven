using UnityEngine;

/// <summary>
/// What makes residents people (Docs/PeopleResearch_2026-11-23.md, the user's "implement as recommended" of 2026-11-23): their
/// talents in the player's own skills and their passions, their spirits and inspired days, friends and rivals, the marks raids leave
/// and the kit they carry. The numbers here; who has which talents and traits is each ResidentDefinition's.
/// Lives at Resources/PeopleSettings.
/// </summary>
[CreateAssetMenu(fileName = "PeopleSettings", menuName = "Safe Haven/Settings/People")]
public sealed class PeopleSettings : GameSettings<PeopleSettings>
{
    [Header("Talents")]
    [Tooltip("The level of a skill a written resident's Talents leave out (the player starts most at 1; 10 is ordinary).")]
    [SerializeField, Range(1, 50)] private int unlistedLevel = 5;
    [Tooltip("How much faster their station works for each level of their job's skill above 10 (level 30: x1.3, 40: x1.45).")]
    [SerializeField, Range(0f, 0.05f)] private float speedPerLevel = 0.015f;
    [Tooltip("Work never falls below this speed for a low skill (1: talents only ever help, so nobody works slower than before talents).")]
    [SerializeField, Range(0.3f, 1f)] private float leastSpeed = 1f;
    [Tooltip("Chance of one more of what they make, for each level of their job's skill above 10 (level 40: 12%).")]
    [SerializeField, Range(0f, 0.02f)] private float extraPerLevel = 0.004f;
    [SerializeField, Range(0f, 0.5f)] private float mostExtra = 0.2f;

    [Header("Learning")]
    [Tooltip("What their practice counts for, as the player's would for the same work (crafts by their tier and kind, LevelingSettings).")]
    [SerializeField, Range(0f, 3f)] private float workShare = 1f;
    [Tooltip("Practice counted in full in one skill in one Haven day; beyond it, only Beyond The Day's share (RimWorld's daily cap): a " +
             "few people can't do everything.")]
    [SerializeField, Min(10f)] private float dailyPractice = 400f;
    [SerializeField, Range(0f, 1f)] private float beyondTheDay = 0.2f;
    [Tooltip("How fast they learn a skill they have no passion for, one they are Keen on, and one they burn for.")]
    [SerializeField] private float noPassionLearning = 0.5f;
    [SerializeField] private float keenLearning = 1f;
    [SerializeField] private float burningLearning = 1.5f;
    [Tooltip("Work done for the job that isn't a craft (an inscription carved, a quest's reward of work): tier experience for each.")]
    [SerializeField, Min(0f)] private float workUnitPractice = 2f;
    [Tooltip("A raid gone along on: tier experience of the raid's tier in their fighting skill (Melee, or Archery for an archer), and " +
             "for each foe felled.")]
    [SerializeField, Min(0f)] private float raidPractice = 4f;
    [SerializeField, Min(0f)] private float killPractice = 0.5f;
    [Tooltip("A foray come home: tier experience of its tier in the skill of the land's work (ForaySettings › Destination › Skill).")]
    [SerializeField, Min(0f)] private float forayPractice = 6f;
    [Tooltip("A land party come home: tier experience in its leader's job skill.")]
    [SerializeField, Min(0f)] private float partyPractice = 4f;
    [Tooltip("A tree felled or rock broken in a ring: tier experience in Woodcutting or Mining.")]
    [SerializeField, Min(0f)] private float ringPractice = 1f;
    [Tooltip("Experience in Construction for each second of hammering at a scaffold.")]
    [SerializeField, Min(0f)] private float buildPracticePerSecond = 0.4f;
    [SerializeField] private ProficiencyDefinition fellingSkill;
    [SerializeField] private ProficiencyDefinition quarrySkill;
    [SerializeField] private ProficiencyDefinition buildingSkill;
    [SerializeField] private ProficiencyDefinition meleeSkill;
    [SerializeField] private ProficiencyDefinition archerySkill;

    [Header("Passions")]
    [Tooltip("A little quicker at work in a skill they love (a share of their work speed): Keen, Burning. Never slower elsewhere.")]
    [SerializeField, Range(0f, 0.5f)] private float keenJoy = 0.05f;
    [SerializeField, Range(0f, 0.5f)] private float burningJoy = 0.1f;
    [Tooltip("A bond rank reached with the player (Trusted, Sworn) lets the player choose one of two passions for them to grow.")]
    [SerializeField] private bool picksAtBondRanks = true;

    [Header("Fighting beside the player")]
    [Tooltip("Extra damage for each level of their fighting skill above 10 (Melee, or Archery for an archer).")]
    [SerializeField, Range(0f, 0.05f)] private float damagePerLevel = 0.015f;
    [Tooltip("More chance of reading a blow and stepping aside, for each level of Melee above 10 (kept under Most Dodge).")]
    [SerializeField, Range(0f, 0.02f)] private float dodgePerLevel = 0.005f;
    [SerializeField, Range(0f, 0.95f)] private float mostDodge = 0.8f;

    [Header("Spirits")]
    [Tooltip("Steps (lifts less lowers: ResidentSpirits) at which they are Bright; and how many more lowers than lifts make them Low.")]
    [SerializeField] private int brightAt = 2;
    [SerializeField, Min(1)] private int lowSteps = 1;
    [Tooltip("What Low spirits cost at work (a share of their speed). Nothing else: no breaks, no refusals.")]
    [SerializeField, Range(0f, 0.5f)] private float lowWork = 0.1f;

    [Header("Inspired days")]
    [Tooltip("Chance each dawn that someone in Bright spirits has an inspired day (about one in 12 Bright days).")]
    [SerializeField, Range(0f, 1f)] private float inspirationChance = 0.08f;
    [Tooltip("Days an inspiration that waits for its moment (the next piece, foray or raid) keeps.")]
    [SerializeField, Min(1)] private int inspirationWaits = 3;
    [Tooltip("A working fever: the share faster at work for the day.")]
    [SerializeField, Range(0f, 2f)] private float feverWork = 0.5f;
    [Tooltip("Spoiling for a fight: the share more damage along on the next raid.")]
    [SerializeField, Range(0f, 1f)] private float fightingHeart = 0.2f;
    [Tooltip("A feeling about the woods: the share more their next foray brings home.")]
    [SerializeField, Range(0f, 2f)] private float goodFind = 0.5f;
    [Tooltip("An idea at the bench that doesn't make gear: how many more of their next craft come out (gear comes out a step finer).")]
    [SerializeField, Min(0)] private int inspiredExtra = 2;
    [Tooltip("A story told: how long everyone at home is cheered, in seconds of play (SmallScenes' cheer).")]
    [SerializeField, Min(0f)] private float storyCheerSeconds = 600f;

    [Header("Friends and rivals")]
    [Tooltip("Where a written friend starts (Friendly: 1), and how far below Neutral a written friction starts (Cool: 1).")]
    [SerializeField] private float writtenFriend = 1f;
    [SerializeField, Min(0f)] private float frictionCool = 1f;
    [Tooltip("What a small scene shared, a foray walked together and a day working near each other add.")]
    [SerializeField, Min(0f)] private float sceneGain = 0.35f;
    [SerializeField, Min(0f)] private float forayGain = 0.25f;
    [SerializeField, Min(0f)] private float nearGain = 0.1f;
    [Tooltip("Metres between two workplaces that count as working near each other, and between two homes that are next door.")]
    [SerializeField, Min(1f)] private float nearReach = 25f;
    [SerializeField, Min(1f)] private float nextDoor = 15f;
    [Tooltip("A rivals' argument at the fire: the chance it clears the air, what that adds, and what it takes when it hardens instead.")]
    [SerializeField, Range(0f, 1f)] private float clearedAirChance = 0.5f;
    [SerializeField, Min(0f)] private float clearedAir = 2f;
    [SerializeField, Min(0f)] private float hardened = 1f;
    [Tooltip("Days a friend the forest takes is missed (their spirits).")]
    [SerializeField, Min(0)] private int missedDays = 5;
    [Tooltip("A friend (Friendly or better) working near: the share quicker at work for each, up to Most Near Friends.")]
    [SerializeField, Range(0f, 0.3f)] private float friendNearWork = 0.05f;
    [SerializeField, Min(0)] private int mostNearFriends = 2;
    [Tooltip("Friends going on a foray together: the share of the party's power for each pair, up to Most Foray Friends.")]
    [SerializeField, Range(0f, 0.5f)] private float forayFriends = 0.1f;
    [SerializeField, Min(0)] private int mostForayFriends = 3;

    [Header("Marks")]
    [Tooltip("Every mark a raid may leave (MarkDefinition); a person holds at most Most Marks good and Most Marks hard.")]
    [SerializeField] private System.Collections.Generic.List<MarkDefinition> marks = new System.Collections.Generic.List<MarkDefinition>();
    [SerializeField, Min(1)] private int mostMarks = 3;
    [Tooltip("The mark being found again leaves (held until every memory is back).")]
    [SerializeField] private MarkDefinition forestMark;

    [Header("Bond with the player")]
    [Tooltip("What grows a resident's bond with the player (PeopleBond); the bond ranks (Settlement Settings › Bond Ranks) read it, a raid " +
             "along being Bond Raid.")]
    [SerializeField, Min(0f)] private float bondRaid = 1f;
    [SerializeField, Min(0f)] private float bondLesson = 0.25f;
    [SerializeField, Min(0f)] private float bondAsk = 0.5f;
    [SerializeField, Min(0f)] private float bondWish = 0.5f;
    [SerializeField, Min(0f)] private float bondKit = 0.25f;
    [SerializeField, Min(0f)] private float bondGift = 0.35f;
    [SerializeField, Min(0f)] private float bondMarkHealed = 0.5f;
    [SerializeField, Min(0f)] private float bondHelpedUp = 0.5f;
    [SerializeField, Min(0f)] private float bondFriendHome = 1f;

    [Header("Gifts")]
    [Tooltip("Days a gift they love lifts their spirits; one gift a day counts.")]
    [SerializeField, Min(1)] private int giftDays = 3;

    [Header("Their own kit")]
    [Tooltip("Raids gone along on with a weapon or body piece the player gave them, after which it is theirs, by name.")]
    [SerializeField, Min(1)] private int kitNamedAfter = 8;
    [Tooltip("Wear on their kit for each raid along (a share of its durability), and more when the raid failed.")]
    [SerializeField, Range(0f, 1f)] private float kitWearPerRaid = 0.06f;
    [SerializeField, Range(0f, 1f)] private float kitWearFailed = 0.2f;

    public int UnlistedLevel => unlistedLevel;
    public int BrightAt => brightAt;
    /// <summary>At or below these steps they are Low (a negative number).</summary>
    public int LowAt => -lowSteps;
    public float LowWork => lowWork;
    public float InspirationChance => inspirationChance;
    public int InspirationWaits => inspirationWaits;
    public float FeverWork => feverWork;
    public float FightingHeart => fightingHeart;
    public float GoodFind => goodFind;
    public float StoryCheerSeconds => storyCheerSeconds;
    public int InspiredExtra => inspiredExtra;
    public float WrittenFriend => writtenFriend;
    /// <summary>Where a written friction starts (a negative standing).</summary>
    public float WrittenFriction => -frictionCool;
    public float SceneGain => sceneGain;
    public float ForayGain => forayGain;
    public float NearGain => nearGain;
    public float NearReach => nearReach;
    public float NextDoor => nextDoor;
    public float ClearedAirChance => clearedAirChance;
    public float ClearedAir => clearedAir;
    public float Hardened => hardened;
    public int MissedDays => missedDays;
    public float FriendNearWork => friendNearWork;
    public int MostNearFriends => mostNearFriends;
    public float ForayFriends => forayFriends;
    public int MostForayFriends => mostForayFriends;
    public System.Collections.Generic.IReadOnlyList<MarkDefinition> Marks => marks;
    public int MostMarks => mostMarks;
    public MarkDefinition ForestMark => forestMark;
    public int KitNamedAfter => kitNamedAfter;
    public float BondRaid => bondRaid;
    public float BondLesson => bondLesson;
    public float BondAsk => bondAsk;
    public float BondWish => bondWish;
    public float BondKit => bondKit;
    public float BondGift => bondGift;
    public float BondMarkHealed => bondMarkHealed;
    public float BondHelpedUp => bondHelpedUp;
    public float BondFriendHome => bondFriendHome;
    public int GiftDays => giftDays;
    public float KitWearPerRaid => kitWearPerRaid;
    public float KitWearFailed => kitWearFailed;
    public float SpeedPerLevel => speedPerLevel;
    public float LeastSpeed => leastSpeed;
    public float ExtraPerLevel => extraPerLevel;
    public float MostExtra => mostExtra;
    public float WorkShare => workShare;
    public float DailyPractice => dailyPractice;
    public float BeyondTheDay => beyondTheDay;
    public float WorkUnitPractice => workUnitPractice;
    public float RaidPractice => raidPractice;
    public float KillPractice => killPractice;
    public float ForayPractice => forayPractice;
    public float PartyPractice => partyPractice;
    public float RingPractice => ringPractice;
    public float BuildPracticePerSecond => buildPracticePerSecond;
    public ProficiencyDefinition FellingSkill => fellingSkill;
    public ProficiencyDefinition QuarrySkill => quarrySkill;
    public ProficiencyDefinition BuildingSkill => buildingSkill;
    public ProficiencyDefinition MeleeSkill => meleeSkill;
    public ProficiencyDefinition ArcherySkill => archerySkill;
    public bool PicksAtBondRanks => picksAtBondRanks;
    public float DamagePerLevel => damagePerLevel;
    public float DodgePerLevel => dodgePerLevel;
    public float MostDodge => mostDodge;

    public float LearningFor(Passion passion) => passion == Passion.Burning ? burningLearning : passion == Passion.Keen ? keenLearning : noPassionLearning;

    public float JoyOf(Passion passion) => passion == Passion.Burning ? burningJoy : passion == Passion.Keen ? keenJoy : 0f;
}
