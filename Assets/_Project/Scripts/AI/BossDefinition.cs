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
    [Tooltip("How its body glows from now on, as far as its material has an emission map. Black: no change.")]
    [ColorUsage(false, true)] public Color glow = Color.black;
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

    [Header("Topples")]
    [Tooltip("Written on the screen each time it topples (its Monster Definition › Topple Seconds), so the opening is read: \"The " +
             "Heartroot topples! Its heart lies open!\". Empty: none.")]
    [SerializeField] private string toppleLine;
    [Tooltip("How its body glows while it lies toppled, pulsing like a heartbeat, so the opening shows in the dark; its stage's glow comes " +
             "back as it rises. Only where its material has an emission map. Black: no change.")]
    [SerializeField, ColorUsage(false, true)] private Color toppleGlow = Color.black;

    [Header("Waking")]
    [Tooltip("Metres within which a player (or a friend) wakes it; anything that hurts it wakes it too. 0: it wakes as soon as it is " +
             "placed, for a boss brought into its fight (the Heartroot). One standing on the map from the start (the Brukam on its bridge) " +
             "needs a range, or its bar, music and roar would start the raid wherever the player is.")]
    [SerializeField, Min(0f)] private float wakeRange;
    [Tooltip("Seconds it takes to rise, while its name is shown. Nothing can hurt it, and it hurts nothing.")]
    [SerializeField, Min(0f)] private float wakeSeconds = 3.2f;
    [SerializeField] private SoundDefinition wakeSound;
    [Tooltip("Plays from its waking until it falls, in place of the scene's music.")]
    [SerializeField] private MusicPlaylist music = new MusicPlaylist();

    [Header("At Home")]
    [Tooltip("Stands near the portal once it has been beaten.")]
    [SerializeField] private BossTrophy trophy;
    [Tooltip("What those at home say of it for a while. One is kept per person.")]
    [SerializeField, TextArea] private string[] homeLines = new string[0];

    public string Id => string.IsNullOrWhiteSpace(id) ? name : id;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public string Title => title;
    public BossPhase[] Phases => phases;
    public SkillDefinition RewardSkill => rewardSkill;
    public string DefeatLine => defeatLine;
    public string ToppleLine => toppleLine;
    public Color ToppleGlow => toppleGlow;
    public float WakeSeconds => wakeSeconds;
    public float WakeRange => wakeRange;
    public SoundDefinition WakeSound => wakeSound;
    public MusicPlaylist Music => music;
    public BossTrophy Trophy => trophy;
    public string[] HomeLines => homeLines;

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
