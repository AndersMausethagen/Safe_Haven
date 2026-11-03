using System;
using UnityEngine;

/// <summary>One stage of a boss fight, past a share of its health.</summary>
[Serializable]
public sealed class BossPhase
{
    [Tooltip("It begins when the boss's health falls below this share.")]
    [Range(0f, 1f)] public float belowHealth = 0.66f;
    [Tooltip("Written on the screen as it begins.")]
    [TextArea] public string line;
    [Tooltip("It reels for this long as it changes: a moment to get a blow in, or to get clear.")]
    [Min(0f)] public float reelSeconds = 1.6f;
    [Tooltip("Its blows hit this much harder from now on: 0.15 is 15%.")]
    [Min(0f)] public float damageBonus;
    [SerializeField] private SoundDefinition sound;

    public SoundDefinition Sound => sound;
}

/// <summary>
/// A boss (GDD §16.5): who it is, how its fight changes, and what beating it gives. The stage of the fight opens more of its attacks
/// (MonsterAttack › From Phase). What it gives is not loot: it is known at once and kept, even if the player falls before getting home.
/// Its drops are ordinary drops, and have to be carried out like anything else.
/// </summary>
[CreateAssetMenu(fileName = "Boss", menuName = "Safe Haven/Monsters/Boss")]
public sealed class BossDefinition : ScriptableObject
{
    [Tooltip("Stable unique identifier, used for saves.")]
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [Tooltip("Under its name on the bar.")]
    [SerializeField] private string title;
    [SerializeField] private BossPhase[] phases = new BossPhase[0];
    [Tooltip("Learned for good on beating it, the first time.")]
    [SerializeField] private SkillDefinition rewardSkill;
    [Tooltip("Written on the screen when it falls.")]
    [SerializeField, TextArea] private string defeatLine;

    public string Id => string.IsNullOrWhiteSpace(id) ? name : id;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public string Title => title;
    public BossPhase[] Phases => phases;
    public SkillDefinition RewardSkill => rewardSkill;
    public string DefeatLine => defeatLine;

    /// <summary>The stage of the fight at this share of health: 0 at the start, one more past each phase's mark.</summary>
    public static int PhaseAt(float healthShare, BossPhase[] phases)
    {
        int phase = 0;
        for (int i = 0; i < phases.Length; i++)
        {
            if (healthShare < phases[i].belowHealth)
            {
                phase = i + 1;
            }
        }

        return phase;
    }
}
