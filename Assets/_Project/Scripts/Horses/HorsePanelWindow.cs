using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A horse's panel, from its menu: its picture (a small camera beside it, live while the panel is open), its name, sex,
/// nature and coat, bars for its health, stamina, bond, speed and strength against the best of its kind, what it has
/// learned, what it wears and carries, and what it is doing. Hearthline: a solid window, Cinzel title, thin bars. Built in
/// code the first time it is opened.
/// </summary>
public sealed class HorsePanelWindow : MonoBehaviour
{
    private const float Width = 860f;
    private const float Height = 520f;
    private const float Margin = 36f;
    private const float Picture = 400f;
    private const float BarWidth = 300f;

    private static HorsePanelWindow current;

    private UIWindow window;
    private TMP_Text title;
    private TMP_Text line;
    private TMP_Text details;
    private RawImage picture;
    private RenderTexture texture;
    private Camera portrait;
    private readonly Image[] fills = new Image[5];
    private readonly TMP_Text[] labels = new TMP_Text[5];
    private Horse horse;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => current = null;

    /// <summary>Opens the panel on this horse.</summary>
    public static void Show(Horse horse)
    {
        if (horse == null)
        {
            return;
        }

        if (current == null)
        {
            var root = new GameObject("Horse Panel");
            root.SetActive(false);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 205;
            UIRoot.ConfigureScaler(root.AddComponent<CanvasScaler>());
            root.AddComponent<GraphicRaycaster>();
            current = root.AddComponent<HorsePanelWindow>();
            current.Build(root.transform);
            root.SetActive(true);
        }

        current.horse = horse;
        current.Refresh();
        current.window.Open();
    }

    private void Build(Transform root)
    {
        UIStyle style = UIStyle.Current;
        RectTransform content = CodeUI.Stretch("Content", root);
        content.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);
        RectTransform panel = CodeUI.Box("Panel", content, new Vector2(0.5f, 0.5f), new Vector2(Width, Height), Vector2.zero);
        panel.gameObject.AddComponent<Image>();
        panel.gameObject.AddComponent<UIRole>().Set(UIRole.Kind.Window);

        RectTransform frame = CodeUI.At("Picture", panel, Margin, Margin, Picture, Picture);
        frame.gameObject.AddComponent<Image>();
        frame.gameObject.AddComponent<UIRole>().Set(UIRole.Kind.Slot);
        picture = CodeUI.Stretch("Image", frame).gameObject.AddComponent<RawImage>();
        texture = new RenderTexture(512, 512, 24) { name = "Horse Portrait" };
        picture.texture = texture;

        float x = Margin * 2f + Picture;
        float width = Width - x - Margin;
        TMP_Text kicker = CodeUI.Label("Kicker", panel, x, Margin, width, 22f, 16f, style.Accent);
        kicker.text = "YOUR HORSE";
        kicker.characterSpacing = 12f;
        title = CodeUI.Label("Name", panel, x, Margin + 22f, width, 46f, 34f, style.Text);
        style.Title(title);
        line = CodeUI.Paragraph("Line", panel, x, Margin + 72f, width, 48f, 18f, style.Muted);
        string[] names = { "Health", "Stamina", "Bond", "Speed", "Strength" };
        for (int i = 0; i < names.Length; i++)
        {
            float y = Margin + 128f + i * 40f;
            labels[i] = CodeUI.Label(names[i], panel, x, y, width, 20f, 16f, style.Text);
            labels[i].font = style.BodyFont;
            RectTransform track = CodeUI.At("Track", panel, x, y + 22f, BarWidth, 6f);
            track.gameObject.AddComponent<Image>().color = style.Track;
            RectTransform fill = CodeUI.Stretch("Fill", track);
            fills[i] = fill.gameObject.AddComponent<Image>();
        }

        details = CodeUI.Paragraph("Details", panel, x, Margin + 340f, width, 110f, 16f, style.Text);
        CodeUI.AddButton("Close", panel, Width - Margin - 180f, Height - 70f, 180f, UIRole.Kind.PrimaryButton, () => window.Close(), out _).text = "Close";
        window = root.gameObject.AddComponent<UIWindow>();
        window.Configure(UIWindowId.HorsePanel, content.gameObject);
    }

    private void Refresh()
    {
        if (horse == null)
        {
            return;
        }

        UIStyle style = UIStyle.Current;
        HorseDefinition kind = horse.Definition;
        HorseStats stats = horse.Stats;
        RareCoat? rare = kind.RareCoatOf(stats.rareCoat);
        string coat = rare.HasValue ? rare.Value.name.ToLowerInvariant() + " " : "";
        string age = stats.IsGrown ? "" : $", a foal {stats.growth * 100f:0}% grown";
        title.text = horse.HorseName;
        line.text = $"A {coat}{stats.SexName} of the {kind.DisplayName.ToLowerInvariant()}{age}, {HorseTemperamentRules.NameOf(stats.temperament)} by nature."
                    + (stats.generation > 0 ? $" Bred in your stable, generation {stats.generation}." : "");
        float Share(float value, Vector2 range) => Mathf.InverseLerp(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y), value);
        Set(0, $"Health {horse.Health.CurrentHealth:0} / {horse.Health.MaxHealth:0}", horse.Health.HealthFraction, style.Danger);
        Set(1, $"Stamina {horse.Stamina:0} / {horse.MaxStamina:0}", horse.MaxStamina > 0f ? horse.Stamina / horse.MaxStamina : 0f, style.Good);
        Set(2, $"Bond {horse.CurrentBond():0} / 100", horse.Bond / HorseBondRules.Most, style.Accent);
        Set(3, $"Gallop {stats.gallopSpeed:0.0} m/s", Share(stats.gallopSpeed, kind.GallopSpeed), style.Text);
        Set(4, $"Carries {stats.carryWeight:0}", Share(stats.carryWeight, kind.CarryWeight), style.Text);
        string traits = horse.TraitNames();
        string doing = horse.IsStabled ? "In the stable." : horse.Order == Horse.Orders.Follow ? "Following you." : horse.Order == Horse.Orders.Stay ? "Staying where it is."
            : horse.Order == Horse.Orders.Work ? "At work." : "Grazing.";
        details.text = (traits.Length > 0 ? $"Learned: {traits}\n" : "Learned nothing yet: ride it far, over rough ground, through water.\n")
                       + (horse.Saddle != null ? $"Saddle: {horse.Saddle.DisplayName}, {horse.Weight:0} / {horse.Capacity:0} weight in its bags\n" : "No saddle\n")
                       + (horse.Armour != null ? $"Barding: {horse.Armour.DisplayName}\n" : "")
                       + doing;
    }

    private void Set(int bar, string label, float fraction, Color colour)
    {
        labels[bar].text = label;
        fills[bar].color = colour;
        fills[bar].rectTransform.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
    }

    // The picture: a camera beside it, three-quarters on, looking at its shoulder; only while the panel is open.
    private void LateUpdate()
    {
        bool open = window != null && window.IsOpen && horse != null;
        if (!open)
        {
            if (portrait != null)
            {
                portrait.enabled = false;
            }

            return;
        }

        if (portrait == null)
        {
            var holder = new GameObject("Horse Portrait Camera");
            holder.transform.SetParent(transform, false);
            portrait = holder.AddComponent<Camera>();
            portrait.targetTexture = texture;
            portrait.fieldOfView = 32f;
            portrait.nearClipPlane = 0.2f;
            portrait.farClipPlane = 120f;
        }

        portrait.enabled = true;
        Transform body = horse.transform;
        Vector3 look = body.position + Vector3.up * 1.25f;
        portrait.transform.position = look + body.right * 4.6f + body.forward * 2.2f + Vector3.up * 0.4f;
        portrait.transform.LookAt(look);
        if (Time.frameCount % 15 == 0)
        {
            Refresh();
        }
    }

    private void OnDestroy()
    {
        if (texture != null)
        {
            texture.Release();
            Destroy(texture);
        }
    }
}
