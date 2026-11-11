using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The icons beside the vitals for boons that come from no item: a meal or tonic shows its own item's icon, and the rest (Well Rested, a
/// festival, an offering, a place, a raid event, a technique) are found here by how their id starts (PlayerBoons). Painted with LocalSpawn
/// (Tools/LocalSpawn/boon_icons.json) and filled in by AgentScripts/BuildUIArt.cs (Boons); lives at Resources/ConditionIconSettings.
/// </summary>
[CreateAssetMenu(menuName = "Safe Haven/Settings/Condition Icon Settings", fileName = "ConditionIconSettings")]
public sealed class ConditionIconSettings : GameSettings<ConditionIconSettings>
{
    [Serializable]
    public sealed class BoonIcon
    {
        [Tooltip("The start of the boon's id: haven_rested, festival, offering, place, event, technique...")]
        public string idStartsWith;
        public Sprite icon;
    }

    [SerializeField] private List<BoonIcon> boonIcons = new List<BoonIcon>();
    [Tooltip("For a boon nothing else matches.")]
    [SerializeField] private Sprite otherBoon;

    private ContentDatabase content;

    /// <summary>The icon for a boon by its id: its item's when it is a meal or tonic, else the longest matching start here, else the fallback.</summary>
    public Sprite ForBoon(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return otherBoon;
        }

        if (content == null)
        {
            content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        }

        ItemDefinition item = content != null ? content.FindItem(id) : null;
        if (item != null && item.Icon != null)
        {
            return item.Icon;
        }

        BoonIcon best = null;
        foreach (BoonIcon candidate in boonIcons)
        {
            if (candidate != null && candidate.icon != null && !string.IsNullOrEmpty(candidate.idStartsWith)
                && id.StartsWith(candidate.idStartsWith, StringComparison.OrdinalIgnoreCase)
                && (best == null || candidate.idStartsWith.Length > best.idStartsWith.Length))
            {
                best = candidate;
            }
        }

        return best != null ? best.icon : otherBoon;
    }
}
