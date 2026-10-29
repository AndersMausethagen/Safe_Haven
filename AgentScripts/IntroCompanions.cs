using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The expedition becomes three (`unity command run_script --file AgentScripts/IntroCompanions.cs --entry IntroCompanions.Apply`):
/// Sigrid (the leader, a runemaster: mysterious, maybe not to be trusted; she takes over Liv's waystone knowledge and
/// Ingrid's healing), Eirik (the soldier: protective, kind-hearted) and Runa (the tracker: sneaky, deadly with a bow).
/// Bjarne, Tor, Liv and Ingrid leave the prologue (switched off in the scene) to be found and rescued later. Rewrites
/// their lines and the chapter texts that named Liv. Reopens the scene that was open. How they fight (Eirik's sword,
/// Runa's bow) is set up by AgentScripts/BuildCompanions.cs.
/// </summary>
public static class IntroCompanions
{
    private const string PrologueScene = "Assets/_Project/Scenes/Prologue_MistyWoods.unity";
    private static readonly string[] Leaving = { "Bjarne", "Tor", "Liv", "Ingrid" };

    public static string Apply()
    {
        string previous = SceneManager.GetActiveScene().path;
        if (SceneManager.GetActiveScene().isDirty)
        {
            return "the open scene has unsaved changes";
        }

        string data = Data();
        Scene scene = EditorSceneManager.OpenScene(PrologueScene);
        PrologueCompanion[] companions = Object.FindObjectsByType<PrologueCompanion>(FindObjectsInactive.Include);
        string Name(PrologueCompanion c) => c.Resident != null ? c.Resident.DisplayName : c.name;
        foreach (PrologueCompanion companion in companions)
        {
            string name = Name(companion);
            var so = new SerializedObject(companion);
            switch (name)
            {
                case "Sigrid":
                    Lines(so, "Keep the torch high and the road under your feet. The forest is listening tonight. Let it hear only footsteps.",
                        "The villages think we came for wolves and witches. Let them. The stones out here are older than any village, and someone has been waking them.",
                        "Stand inside the ring. Whatever you see, do not let go of the stone until I say.");
                    so.FindProperty("alchemist").boolValue = true;
                    break;
                case "Eirik":
                    Lines(so, "Stay between me and the trees. If anything comes out of the dark, it meets my shield first.",
                        "Eat something, even if you are not hungry. I have buried enough friends who thought they could fight on an empty stomach.",
                        "Go! I will hold them. Nobody gets left behind, not while I am standing.");
                    break;
                case "Runa":
                    Lines(so, "Tracks everywhere, all running the same way. Whatever they are fleeing, we are walking towards it. Lovely.",
                        "I counted three wolves circling and one shape that was not a wolf. Keep your voice down. I would rather see them before they see us.",
                        "I will take the ones on the stones. You just keep breathing.");
                    break;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            if (Leaving.Contains(name))
            {
                companion.gameObject.SetActive(false);
            }
        }

        // The leader's offers repeat the chapter texts.
        foreach (QuestGiver giver in Object.FindObjectsByType<QuestGiver>(FindObjectsInactive.Include))
        {
            var giverData = new SerializedObject(giver);
            SerializedProperty offers = giverData.FindProperty("offers");
            for (int i = 0; i < offers.arraySize; i++)
            {
                SerializedProperty text = offers.GetArrayElementAtIndex(i).FindPropertyRelative("text");
                text.stringValue = text.stringValue
                    .Replace("Liv thinks they may be a way out.", "Sigrid says they may be a way out, and does not say how she knows.")
                    .Replace("Liv: The stones are drawing power!", "Sigrid: The stones are drawing power!");
            }

            giverData.ApplyModifiedPropertiesWithoutUndo();
        }

        PrologueDirector director = Object.FindAnyObjectByType<PrologueDirector>(FindObjectsInactive.Include);
        var directorData = new SerializedObject(director);
        SerializedProperty list = directorData.FindProperty("companions");
        for (int i = list.arraySize - 1; i >= 0; i--)
        {
            var companion = list.GetArrayElementAtIndex(i).objectReferenceValue as PrologueCompanion;
            if (companion == null || Leaving.Contains(Name(companion)))
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = null;
                list.DeleteArrayElementAtIndex(i);
            }
        }

        directorData.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        string kept = string.Join(", ", companions.Where(c => c.gameObject.activeSelf).Select(Name));
        if (!string.IsNullOrEmpty(previous) && previous != PrologueScene)
        {
            EditorSceneManager.OpenScene(previous);
        }

        return $"{data}; expedition: {kept}; director lists {list.arraySize}";
    }

    private static void Lines(SerializedObject so, string forest, string camp, string portal)
    {
        so.FindProperty("forestLine").stringValue = forest;
        so.FindProperty("campLine").stringValue = camp;
        so.FindProperty("portalLine").stringValue = portal;
    }

    private static string Data()
    {
        var prologue = AssetDatabase.LoadAssetAtPath<PrologueDefinition>("Assets/_Project/Data/Prologue/ForestPrologue.asset");
        var so = new SerializedObject(prologue);
        so.FindProperty("leader").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ResidentDefinition>("Assets/_Project/Data/Prologue/Residents/Sigrid.asset");
        SerializedProperty chapters = so.FindProperty("chapters");
        for (int i = 0; i < chapters.arraySize; i++)
        {
            SerializedProperty text = chapters.GetArrayElementAtIndex(i).FindPropertyRelative("arrivalText");
            text.stringValue = text.stringValue
                .Replace("Liv thinks they may be a way out.", "Sigrid says they may be a way out, and does not say how she knows.")
                .Replace("Liv: The stones are drawing power!", "Sigrid: The stones are drawing power!");
        }

        so.FindProperty("survivedText").stringValue = "Sigrid: It is open! Everyone, through! Something vast strikes the stones. You reach for the others... and the forest disappears.";
        so.FindProperty("overwhelmedText").stringValue = "Sigrid: I cannot hold it! Take my hand! A white light tears through the dark...";
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(prologue);

        var activate = AssetDatabase.LoadAssetAtPath<QuestDefinition>("Assets/_Project/Data/Prologue/Quests/ActivatePortal.asset");
        var quest = new SerializedObject(activate);
        quest.FindProperty("description").stringValue = "Sigrid recognises a passage in the ancient waystones. Touch the centre stone to wake the connection.";
        quest.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(activate);
        AssetDatabase.SaveAssets();
        return "prologue data: Sigrid leads";
    }
}
