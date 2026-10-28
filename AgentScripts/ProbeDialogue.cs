using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Shows the dialogue box in a playing scene (`unity command run_script --file AgentScripts/ProbeDialogue.cs --entry ProbeDialogue.<Step>`).</summary>
public static class ProbeDialogue
{
    public static string AskAboutWolf()
    {
        MonsterAI wolf = Object.FindObjectsByType<MonsterAI>().FirstOrDefault();
        var answers = new List<DialogueChoice> { new DialogueChoice("I'll deal with it.", "The Hungry Pack"), new DialogueChoice("Not now.") };
        DialogueBox.Current.Ask(new DialogueSpeaker("Runa", wolf != null ? wolf.transform : null), "There's a pack in the hollow. If they find the camp we're done for. Will you thin them out?", answers,
            index => Debug.Log("Probe dialogue chose " + index));
        return $"asked, open={DialogueBox.IsOpen}, any window open={UIStateManager.Instance != null && UIStateManager.Instance.IsAnyWindowOpen}";
    }

    public static string PlayerSays()
    {
        DialogueBox.Current.Say(DialogueSpeaker.Player(), "At least it's not raining.", () => DialogueBox.Current.Say(DialogueSpeaker.Player(), "...I should not have said that."));
        return "said, open=" + DialogueBox.IsOpen;
    }

    public static string Close()
    {
        DialogueBox.Current.Close();
        return "closed, open=" + DialogueBox.IsOpen;
    }
}
