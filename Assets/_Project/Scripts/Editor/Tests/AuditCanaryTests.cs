using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The content and quest audits are seen to report a fault (Docs/BugPatterns.md, sixth round; the test audit of 2026-11-18): their tests
/// only ask for an empty list, so an audit that stopped finding anything would stay green, as a guard nobody ran did. Faulty assets are
/// planted in a folder of their own for the length of the test, and each audit must name them.
/// </summary>
public sealed class AuditCanaryTests
{
    private const string Parent = "Assets/_Project";
    private const string FolderName = "AuditCanary (planted, removed after)";
    private const string Folder = Parent + "/" + FolderName;

    [Test]
    public void EveryAudit_ReportsAFaultPlantedForIt()
    {
        if (AssetDatabase.IsValidFolder(Folder))
        {
            AssetDatabase.DeleteAsset(Folder);
        }

        AssetDatabase.CreateFolder(Parent, FolderName);
        try
        {
            // A recipe not known from the start that nothing teaches, with no id of its own; a quest outside the content database.
            var recipe = ScriptableObject.CreateInstance<RecipeDefinition>();
            AssetDatabase.CreateAsset(recipe, Folder + "/Planted Recipe.asset");
            var notKnown = new SerializedObject(recipe);
            notKnown.FindProperty("knownFromStart").boolValue = false; // a recipe is known from the start unless set otherwise
            notKnown.ApplyModifiedPropertiesWithoutUndo();
            var quest = ScriptableObject.CreateInstance<QuestDefinition>();
            AssetDatabase.CreateAsset(quest, Folder + "/Planted Quest.asset");
            ContentAudit.Invalidate();
            QuestSceneIndex.Invalidate();

            var blind = new List<string>();
            if (!ContentAudit.UnlearnableRecipes().Any(line => line.Contains("Planted Recipe")))
            {
                blind.Add("recipes nothing teaches (ContentAudit.UnlearnableRecipes)");
            }

            if (!ContentAudit.ImplicitIds().Any(line => line.Contains("Planted Recipe")))
            {
                blind.Add("definitions with no id of their own (ContentAudit.ImplicitIds)");
            }

            if (!QuestAudit.CantStart().Keys.Contains(quest))
            {
                blind.Add("quests that can't start (QuestAudit.CantStart)");
            }

            CollectionAssert.IsEmpty(blind, "these audits didn't report the fault planted for them: they can't see what they are for");
        }
        finally
        {
            AssetDatabase.DeleteAsset(Folder);
            ContentAudit.Invalidate();
            QuestSceneIndex.Invalidate();
        }
    }
}
