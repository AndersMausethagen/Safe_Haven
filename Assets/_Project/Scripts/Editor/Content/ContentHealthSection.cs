using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Every piece of content that needs attention, from all pages at once: the same checks each page runs (missing icons,
/// duplicate ids, broken prefabs, raid scenes missing from the build...), collected so nothing is forgotten. Pick an entry
/// to see what's wrong and jump to the page where it is fixed.
/// </summary>
public sealed class ContentHealthSection : ContentSection
{
    private readonly Dictionary<Object, ContentSection> owners = new Dictionary<Object, ContentSection>();
    private readonly Dictionary<Object, List<string>> found = new Dictionary<Object, List<string>>();

    public override string Title => "Needs Attention";
    public override string Noun => "Problem";
    public override string SidebarGroup => "Overview";
    public override Type AssetType => typeof(Object);
    public override bool UsesDataInspector => false;
    public override bool CanDelete => false;
    public override string EmptyListText => "Nothing needs attention. All content passes its checks.";

    // Its entries belong to other pages, so links never open here.
    public override bool Handles(Object asset) => false;
    public override string Help =>
        "Everything in the game that fails a check, from every page. Pick an entry to see what's wrong, then open it on its " +
        "own page to fix it. The list updates as content changes.";

    public override List<Object> Load()
    {
        owners.Clear();
        found.Clear();
        var flagged = new List<Object>();
        if (Host == null)
        {
            return flagged;
        }

        foreach (ContentSection section in Host.Sections)
        {
            if (section is ContentHealthSection || section is PrefabBrowserSection)
            {
                continue;
            }

            List<Object> all = section.Load();
            foreach (Object asset in all)
            {
                List<string> problems = section.FindProblems(asset, all);
                if (problems.Count > 0 && !owners.ContainsKey(asset))
                {
                    owners[asset] = section;
                    found[asset] = problems;
                    flagged.Add(asset);
                }
            }
        }

        return flagged;
    }

    public override string Label(Object asset) => Owner(asset)?.Label(asset) ?? base.Label(asset);

    public override string ListGroup(Object asset) => Owner(asset)?.Title;

    public override Sprite ListIcon(Object asset) => Owner(asset)?.ListIcon(asset);

    public override int? ListTier(Object asset) => Owner(asset)?.ListTier(asset);

    public override List<string> FindProblems(Object asset, IList<Object> all)
    {
        return found.TryGetValue(asset, out List<string> problems) ? problems : new List<string>();
    }

    public override void DrawExtras(Object asset)
    {
        ContentSection owner = Owner(asset);
        if (owner == null)
        {
            return;
        }

        ContentUtility.BeginPanel("Fix It", $"This {owner.Noun.ToLowerInvariant()} is edited on the {owner.Title} page.");
        if (GUILayout.Button($"Open in {owner.Title}  →", GUILayout.Height(26f)))
        {
            Host.Select(owner, asset);
            GUIUtility.ExitGUI();
        }

        ContentUtility.EndPanel();
    }

    private ContentSection Owner(Object asset)
    {
        return asset != null && owners.TryGetValue(asset, out ContentSection owner) ? owner : null;
    }
}
