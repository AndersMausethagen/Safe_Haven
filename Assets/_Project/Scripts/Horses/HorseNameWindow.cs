using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Naming a horse: asked once it is tamed, and again from its menu. A name typed, or one from the dice (old northern
/// horse names); left empty it keeps "Horse". Built in code the first time it is asked for.
/// </summary>
public sealed class HorseNameWindow : MonoBehaviour
{
    private const float Width = 620f;
    private const float Height = 290f;
    private const float Margin = 40f;

    private static readonly string[] Names =
    {
        "Frost", "Ember", "Ash", "Bracken", "Rune", "Sleet", "Thistle", "Hearth", "Gale", "Birch", "Moss", "Cinder",
        "Hilde", "Skadi", "Grani", "Hrimfaxi", "Svala", "Toki", "Dagny", "Brisa", "Ulf", "Sigur", "Vidar", "Rinda",
    };

    private static HorseNameWindow current;

    private UIWindow window;
    private TMP_InputField field;
    private TMP_Text question;
    private Horse horse;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => current = null;

    /// <summary>True while it asks for this horse's name.</summary>
    public static bool IsNaming(Horse horse) => current != null && horse != null && current.horse == horse && current.window != null && current.window.IsOpen;

    /// <summary>Asks for this horse's name.</summary>
    public static void Ask(Horse horse)
    {
        if (horse == null)
        {
            return;
        }

        if (current == null)
        {
            var root = new GameObject("Horse Name Window");
            root.SetActive(false);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 210;
            UIRoot.ConfigureScaler(root.AddComponent<CanvasScaler>());
            root.AddComponent<GraphicRaycaster>();
            current = root.AddComponent<HorseNameWindow>();
            current.Build(root.transform);
            root.SetActive(true);
        }

        current.horse = horse;
        current.question.text = horse.HasName ? $"A new name for {horse.HorseName}?" : "What will you call it?";
        current.field.text = horse.HasName ? horse.HorseName : "";
        current.window.Open();
        current.field.ActivateInputField();
    }

    private void Build(Transform root)
    {
        UIStyle style = UIStyle.Current;
        RectTransform content = CodeUI.Stretch("Content", root);
        content.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);
        RectTransform panel = CodeUI.Box("Panel", content, new Vector2(0.5f, 0.5f), new Vector2(Width, Height), Vector2.zero);
        panel.gameObject.AddComponent<Image>();
        panel.gameObject.AddComponent<UIRole>().Set(UIRole.Kind.Window);
        TMP_Text kicker = CodeUI.Label("Kicker", panel, Margin, 30f, 400f, 24f, 17f, style.Accent);
        kicker.text = "A HORSE OF YOUR OWN";
        kicker.characterSpacing = 12f;
        question = CodeUI.Label("Question", panel, Margin, 56f, Width - Margin * 2f, 46f, 32f, style.Text);
        style.Title(question);

        RectTransform box = CodeUI.At("Name", panel, Margin, 120f, Width - Margin * 2f - 150f, 52f);
        box.gameObject.AddComponent<Image>();
        box.gameObject.AddComponent<UIRole>().Set(UIRole.Kind.Slot);
        RectTransform area = CodeUI.Stretch("Text Area", box);
        area.offsetMin = new Vector2(16f, 6f);
        area.offsetMax = new Vector2(-16f, -6f);
        area.gameObject.AddComponent<RectMask2D>();
        TMP_Text text = CodeUI.Label("Text", area, 0f, 0f, 300f, 40f, 24f, style.Text);
        Fill((RectTransform)text.transform);
        TMP_Text hint = CodeUI.Label("Hint", area, 0f, 0f, 300f, 40f, 24f, style.Muted);
        Fill((RectTransform)hint.transform);
        hint.text = "Its name...";
        field = box.gameObject.AddComponent<TMP_InputField>();
        field.textViewport = area;
        field.textComponent = text;
        field.placeholder = hint;
        field.lineType = TMP_InputField.LineType.SingleLine;
        field.characterLimit = CharacterAppearance.MaximumNameLength;
        field.onSubmit.AddListener(_ => Done());

        CodeUI.AddButton("Dice", panel, Width - Margin - 136f, 123f, 136f, UIRole.Kind.Button, () => field.text = Names[Random.Range(0, Names.Length)], out _).text = "Roll one";
        CodeUI.AddButton("Done", panel, Width - Margin - 200f, Height - 82f, 200f, UIRole.Kind.PrimaryButton, Done, out _).text = "Name it";

        window = root.gameObject.AddComponent<UIWindow>();
        window.Configure(UIWindowId.HorseName, content.gameObject);
        // While a name is typed its letters are letters: no key opens a window, sneaks or whistles behind it.
        window.OpenChanged += open =>
        {
            if (open)
            {
                GameInput.SuspendGameplay();
            }
            else
            {
                GameInput.ResumeGameplay();
            }
        };
    }

    private void OnDestroy()
    {
        if (window != null && window.IsOpen)
        {
            GameInput.ResumeGameplay();
        }
    }

    private static void Fill(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private void Done()
    {
        if (horse != null)
        {
            horse.Rename(field.text);
            GameMessages.Post(horse.HasName ? $"You call it {horse.HorseName}." : "It stays nameless for now.", MessageKind.Gain);
        }

        window.Close();
    }
}
