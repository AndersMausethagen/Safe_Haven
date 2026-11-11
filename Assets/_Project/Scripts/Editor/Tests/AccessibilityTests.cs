using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class AccessibilityTests
{
    [Test]
    public void TextWithoutARebindStaysAsWritten()
    {
        const string line = "Open your bag (B), then [E] Interact";
        Assert.AreSame(line, GameInput.WithRebinds(line));
    }

    [Test]
    public void AKeyNamedByItsActionShowsTheKeyBoundNow()
    {
        Assert.AreEqual($"Get down ({GameInput.KeyLabel(GameInput.Whistle)})", GameInput.WithRebinds("Get down ({key:Dismount})"));
        InputAction interact = GameInput.Interact;
        int binding = FirstKeyboardBinding(interact);
        interact.ApplyBindingOverride(binding, "<Keyboard>/g");
        try
        {
            Assert.AreEqual("Hold G to build", GameInput.WithRebinds("Hold {key:Interact} to build"));
        }
        finally
        {
            interact.RemoveBindingOverride(binding);
        }
    }

    [Test]
    public void EveryKeyNamedInTheGamesTextIsARealAction()
    {
        var unknown = new System.Collections.Generic.List<string>();
        foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/_Project/Data", "Assets/_Project/Resources" }))
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".asset") || System.IO.File.ReadAllText(path).IndexOf("{key:", System.StringComparison.Ordinal) < 0)
            {
                continue;
            }

            var so = new UnityEditor.SerializedObject(UnityEditor.AssetDatabase.LoadMainAssetAtPath(path));
            UnityEditor.SerializedProperty property = so.GetIterator();
            while (property.Next(true))
            {
                if (property.propertyType == UnityEditor.SerializedPropertyType.String && GameInput.UnknownKey(property.stringValue) is string name)
                {
                    unknown.Add($"{path} {property.propertyPath}: {name}");
                }
            }
        }

        // Code writes them too, as "{key:Build}" or, inside an interpolated string, "{{key:Talents}}".
        foreach (string file in System.IO.Directory.GetFiles("Assets/_Project/Scripts", "*.cs", System.IO.SearchOption.AllDirectories))
        {
            string code = System.IO.File.ReadAllText(file).Replace("{{key:", "{key:").Replace("}}", "}");
            foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(code, @"\{key:[A-Za-z0-9/]+\}"))
            {
                if (GameInput.UnknownKey(match.Value) is string name)
                {
                    unknown.Add($"{file}: {name}");
                }
            }
        }

        Assert.IsEmpty(unknown, "Key tokens that name no action or verb (see GameInput.Verbs):\n" + string.Join("\n", unknown));
    }

    [Test]
    public void AReboundKeyShowsAsTheNewOneInText()
    {
        InputAction interact = GameInput.Interact;
        int binding = FirstKeyboardBinding(interact);
        interact.ApplyBindingOverride(binding, "<Keyboard>/g");
        try
        {
            StringAssert.Contains("(G)", GameInput.WithRebinds("Listen to the portal (E)"));
            StringAssert.Contains("[G]", GameInput.WithRebinds("[E] Interact"));
        }
        finally
        {
            interact.RemoveBindingOverride(binding);
        }
    }

    [Test]
    public void TwoSwappedKeysDontRunTogether()
    {
        InputAction interact = GameInput.Interact;
        InputAction dodge = GameInput.Dodge;
        int a = FirstKeyboardBinding(interact);
        int b = FirstKeyboardBinding(dodge);
        string interactWas = Written(interact, a);
        string dodgeWas = Written(dodge, b);
        interact.ApplyBindingOverride(a, dodge.bindings[b].path);
        dodge.ApplyBindingOverride(b, interact.bindings[a].path);
        try
        {
            string text = GameInput.WithRebinds($"({interactWas}) and ({dodgeWas})");
            Assert.AreEqual($"({KeyOf(interact, a)}) and ({KeyOf(dodge, b)})", text);
        }
        finally
        {
            interact.RemoveBindingOverride(a);
            dodge.RemoveBindingOverride(b);
        }
    }

    [TestCase(0f, 10f, "ahead")]
    [TestCase(10f, 0f, "to the right")]
    [TestCase(-10f, 0f, "to the left")]
    [TestCase(0f, -10f, "behind")]
    [TestCase(1f, 0f, "")]
    public void ACaptionSaysWhichWayTheSoundIs(float x, float z, string side)
    {
        Assert.AreEqual(side, SoundCaptions.SideOf(new Vector3(x, 0f, z), Vector3.forward));
    }

    [Test]
    public void AssistsAreOffByDefaultAndSayWhatTheyDo()
    {
        float damage = GameOptions.DamageTaken.Value;
        float timing = GameOptions.TimingHelp.Value;
        try
        {
            GameOptions.DamageTaken.Value = 0f;
            GameOptions.TimingHelp.Value = 0f;
            Assert.AreEqual(1f, GameOptions.DamageTakenShare);
            Assert.AreEqual(1f, GameOptions.TimingShare);
            Assert.AreEqual("", GameOptions.AssistsInUse);

            GameOptions.DamageTaken.Value = 2f;
            GameOptions.TimingHelp.Value = 1f;
            Assert.AreEqual(0.5f, GameOptions.DamageTakenShare);
            Assert.AreEqual(1.5f, GameOptions.TimingShare);
            StringAssert.Contains("damage taken", GameOptions.AssistsInUse);
        }
        finally
        {
            GameOptions.DamageTaken.Value = damage;
            GameOptions.TimingHelp.Value = timing;
        }
    }

    private static int FirstKeyboardBinding(InputAction action)
    {
        InputBinding group = InputBinding.MaskByGroup("Keyboard&Mouse");
        for (int i = 0; i < action.bindings.Count; i++)
        {
            InputBinding binding = action.bindings[i];
            if (!binding.isComposite && !binding.isPartOfComposite && group.Matches(binding))
            {
                return i;
            }
        }

        throw new AssertionException("No keyboard binding on " + action.name);
    }

    // How a key is written in the game's text: its default name, whatever the keyboard's language calls it.
    private static string Written(InputAction action, int binding) =>
        InputControlPath.ToHumanReadableString(action.bindings[binding].path, InputControlPath.HumanReadableStringOptions.OmitDevice);

    private static string KeyOf(InputAction action, int binding) =>
        action.GetBindingDisplayString(binding, InputBinding.DisplayStringOptions.DontIncludeInteractions);
}
