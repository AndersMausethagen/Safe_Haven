using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Safe Haven's renown and ranks (HavenRenown): what each part of the settlement is worth and the ranks it climbs
/// through, from a camp in an overgrown clearing to a haven. Each rank reached widens the Heart Tree's ward (more room
/// to build, the moss spreading out to the new edge) and is celebrated once. Lives at Resources/HavenSettings;
/// built-in defaults apply when the asset is missing.
/// </summary>
[CreateAssetMenu(menuName = "Safe Haven/Settings/Haven Settings", fileName = "HavenSettings")]
public sealed class HavenSettings : GameSettings<HavenSettings>
{
    [Serializable]
    public sealed class Rank
    {
        public string name = "Camp";
        [Tooltip("Renown needed.")]
        [Min(0)] public int renown;
        [Tooltip("Metres the ward reaches beyond its level's radius once this rank is reached.")]
        [Min(0f)] public float wardBonus;
        [TextArea(1, 3)] public string line;
        [Tooltip("Raised once when Safe Haven reaches this rank, for quests (a Quest Event objective with Count Already Owned) and stories. Optional.")]
        public GameEventDefinition reachedEvent;
    }

    [Tooltip("Each more building of a kind already standing brings this share of its renown: variety is worth more.")]
    [SerializeField, Range(0f, 1f)] private float repeatShare = 0.5f;
    [Tooltip("Renown for clearing all the overgrowth (less for part of it).")]
    [SerializeField, Min(0)] private int clearingRenown = 40;
    [Tooltip("Renown for each resident living in Safe Haven.")]
    [SerializeField, Min(0)] private int residentRenown = 8;
    [Tooltip("Experience for each new rank.")]
    [SerializeField, Min(0)] private int rankExperience = 150;
    [Tooltip("Experience for each quarter of the overgrowth cleared, times the quarters cleared (25, 50, 75, 100).")]
    [SerializeField, Min(0)] private int clearingExperiencePerQuarter = 25;
    [SerializeField] private SoundDefinition rankSound;
    [Tooltip("The people lost with the player. The Haven window shows who is home and who is still out there.")]
    [SerializeField] private List<ResidentDefinition> expedition = new List<ResidentDefinition>();
    [SerializeField] private List<Rank> ranks = new List<Rank>
    {
        new Rank { name = "Overgrown Camp", renown = 0, line = "A fire and a tree, and the forest pressing in." },
        new Rank { name = "Homestead", renown = 25, wardBonus = 3f, line = "Someone lives here now. The ward reaches further." },
        new Rank { name = "Hamlet", renown = 60, wardBonus = 6f, line = "Smoke from more than one chimney." },
        new Rank { name = "Village", renown = 110, wardBonus = 10f, line = "Travellers speak of a light in the old wood." },
        new Rank { name = "Haven", renown = 180, wardBonus = 15f, line = "A true haven. The forest itself seems to breathe easier." },
    };

    public float RepeatShare => repeatShare;
    public int ClearingRenown => clearingRenown;
    public int ResidentRenown => residentRenown;
    public int RankExperience => rankExperience;
    public int ClearingExperiencePerQuarter => clearingExperiencePerQuarter;
    public SoundDefinition RankSound => rankSound;
    public IReadOnlyList<Rank> Ranks => ranks;
    public IReadOnlyList<ResidentDefinition> Expedition => expedition;

    /// <summary>The highest rank this much renown reaches (0 is the first).</summary>
    public int RankFor(int renown)
    {
        int rank = 0;
        for (int i = 0; i < ranks.Count; i++)
        {
            if (renown >= ranks[i].renown)
            {
                rank = i;
            }
        }

        return rank;
    }

    public float WardBonusAt(int rank) => ranks.Count == 0 ? 0f : ranks[Mathf.Clamp(rank, 0, ranks.Count - 1)].wardBonus;
}
