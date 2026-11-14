#error Retired 2026-11-14: this script rebuilt content from its own stored copy and could silently overwrite newer work (AgentScripts/RETIRED.md). The scenes and assets are the source of truth: make a small targeted edit with a before/after diff, and restore earlier states from git.
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Keys in the game's text named by their actions (`unity command script run --file AgentScripts/KeyTokens.cs --entry KeyTokens.Apply`):
/// every string in the data (journal pages, quests, tips, dialogue, voice lines...) that writes a default key - "(G)", "[E]",
/// "hold E" - gets the action's token instead ("({key:Build})", "[{key:Interact}]", "hold {key:Interact}"), which GameInput.Keys
/// turns back into the key bound now. Which key is which action is read from the Input Actions' keyboard bindings. Safe to run
/// again: tokens are left as they are.
/// </summary>
public static class KeyTokens
{
    private static readonly string[] Folders = { "Assets/_Project/Data", "Assets/_Project/Resources" };

    public static string Apply()
    {
        Dictionary<string, string> keys = DefaultKeys();
        string letters = string.Join("|", keys.Keys.OrderByDescending(k => k.Length).Select(Regex.Escape));
        var bracketed = new Regex($@"([\(\[])({letters})([\)\]])");
        var spoken = new Regex($@"\b(press|tap|hold|Press|Tap|Hold)( the)? ({letters})\b(?! key)");
        // "(build menu: G)": a key named after a colon in brackets.
        var named = new Regex($@"(menu: |key: )({letters})([\)\]])");
        int changed = 0, assets = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", Folders))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Object asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset == null || asset is TerrainData || asset is TerrainLayer)
            {
                continue;
            }

            var so = new SerializedObject(asset);
            SerializedProperty property = so.GetIterator();
            int before = changed;
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.String || string.IsNullOrEmpty(property.stringValue))
                {
                    continue;
                }

                string text = property.stringValue;
                string done = bracketed.Replace(text, m => $"{m.Groups[1].Value}{{key:{keys[m.Groups[2].Value]}}}{m.Groups[3].Value}");
                done = spoken.Replace(done, m => $"{m.Groups[1].Value}{m.Groups[2].Value} {{key:{keys[m.Groups[3].Value]}}}");
                done = named.Replace(done, m => $"{m.Groups[1].Value}{{key:{keys[m.Groups[2].Value]}}}{m.Groups[3].Value}");
                if (done != text)
                {
                    property.stringValue = done;
                    changed++;
                }
            }

            if (changed > before)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
                assets++;
            }
        }

        AssetDatabase.SaveAssets();
        return $"{changed} texts in {assets} assets now name their keys by action ({keys.Count} keys known: {string.Join(", ", keys.Select(k => k.Key + "=" + k.Value))})";
    }

    // The default keyboard key of each player action, as text writes it ("E", "Space", "G"), when only one action has it.
    private static Dictionary<string, string> DefaultKeys()
    {
        var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/_Project/Input/InputSystem_Actions.inputactions");
        var keys = new Dictionary<string, string>();
        var taken = new HashSet<string>();
        InputActionMap player = asset.FindActionMap("Player");
        foreach (InputAction action in player.actions)
        {
            foreach (InputBinding binding in action.bindings)
            {
                if (binding.isComposite || binding.isPartOfComposite || !binding.path.StartsWith("<Keyboard>/"))
                {
                    continue;
                }

                string key = binding.path.Substring("<Keyboard>/".Length);
                key = key == "space" ? "Space" : key == "tab" ? "Tab" : key == "leftCtrl" ? "Ctrl" : key == "leftAlt" ? "Alt" : key == "leftShift" ? "Shift"
                    : key.Length == 1 ? key.ToUpperInvariant() : null;
                if (key == null)
                {
                    continue;
                }

                if (!taken.Add(key))
                {
                    keys.Remove(key);
                    continue;
                }

                keys[key] = action.name;
            }
        }

        // The skill bar's number keys aren't named in text; the one-letter keys and the few named ones are.
        foreach (string number in new[] { "1", "2", "3", "4", "5" })
        {
            keys.Remove(number);
        }

        return keys;
    }
}
