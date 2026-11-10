/// <summary>
/// Spoken dialogue from the command line (run with `unity command script run --file AgentScripts/BuildDialogueVoices.cs
/// --entry ...`). The work is DialogueVoices' (Assets/_Project/Scripts/Editor/Audio), which the Voice Lines window
/// (Safe Haven > Voice Lines) uses too:
/// <para><b>BuildDialogueVoices.Harvest</b> finds every line a named character says -> Tools/Audio/Voices/dialogue_lines.json.</para>
/// <para><b>BuildDialogueVoices.Apply</b> writes Resources/VoiceBank.asset from the recordings that exist.</para>
/// Between the two: `python Tools/Audio/dialogue_voices.py new` gives each new line a voice and a mood and has
/// LocalSpawn speak it.
/// </summary>
public static class BuildDialogueVoices
{
    public static string Harvest() => DialogueVoices.Harvest();

    public static string Apply() => DialogueVoices.Apply();
}
