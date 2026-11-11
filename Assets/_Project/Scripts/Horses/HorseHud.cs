using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// What the player needs to see of a horse, kept small and over the horse itself rather than across the screen: two thin
/// bars above its head and, while taming, one short line of what to press. In the struggle, the strain on the rope and
/// the strength it has left; worn out, how long it will stand; on the breaking ride, the rider's balance and, while it asks
/// for barley, its patience; riding a horse of your own, its stamina and its health. Frameless in the Hearthline way.
/// Built in code the first time a horse needs it; reads HorseTaming.Active and PlayerRiding.
/// </summary>
public sealed class HorseHud : MonoBehaviour
{
    private const float Width = 96f;
    private const float BarHeight = 5f;
    private const float Gap = 4f;
    // Metres above the horse's feet the bars float: over its head.
    private const float Above = 2.7f;

    private static HorseHud current;

    private RectTransform canvasRect;
    private RectTransform holder;
    private CanvasGroup group;
    private TMP_Text hint;
    private Bar first;
    private Bar second;

    private sealed class Bar
    {
        public RectTransform Track;
        public Image Fill;

        public void Show(bool shown) => Track.gameObject.SetActive(shown);

        public void Set(float fraction, Color colour)
        {
            Fill.color = colour;
            Fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => current = null;

    /// <summary>Makes the HUD for this scene, unless it has one.</summary>
    public static void Ensure()
    {
        if (current != null)
        {
            return;
        }

        var root = new GameObject("Horse HUD");
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 72;
        UIRoot.ConfigureScaler(root.AddComponent<CanvasScaler>());
        current = root.AddComponent<HorseHud>();
        current.canvasRect = (RectTransform)root.transform;
        current.group = root.AddComponent<CanvasGroup>();
        current.group.blocksRaycasts = false;
        current.group.alpha = 0f;
        current.Build(root.transform);
    }

    private void Build(Transform root)
    {
        UIStyle style = UIStyle.Current;
        // Placed each frame over the horse; the bars hang from its top middle.
        holder = CodeUI.Box("Over the horse", root, Vector2.zero, new Vector2(Width, BarHeight * 2f + Gap), Vector2.zero);
        holder.pivot = new Vector2(0.5f, 0f);
        first = MakeBar(holder, 0f, style);
        second = MakeBar(holder, BarHeight + Gap, style);
        hint = CodeUI.Label("Hint", holder, -77f, -24f, Width + 154f, 20f, 15f, style.Accent, TextAlignmentOptions.Center);
        hint.font = style.BodyFont;
    }

    private static Bar MakeBar(RectTransform holder, float y, UIStyle style)
    {
        var bar = new Bar { Track = CodeUI.At("Track", holder, 0f, y, Width, BarHeight) };
        bar.Track.gameObject.AddComponent<Image>().color = style.Track;
        RectTransform fill = CodeUI.Stretch("Fill", bar.Track);
        bar.Fill = fill.gameObject.AddComponent<Image>();
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = Vector2.one;
        fill.offsetMin = fill.offsetMax = Vector2.zero;
        return bar;
    }

    private void LateUpdate()
    {
        UIStyle style = UIStyle.Current;
        HorseTaming taming = HorseTaming.Active;
        Player player = Player.Current;
        PlayerRiding riding = player != null && player.TryGetComponent(out PlayerRiding found) ? found : null;
        bool windows = UIStateManager.Current != null && UIStateManager.Current.IsAnyWindowOpen;
        string key = GameInput.KeyLabel(GameInput.Interact);
        Horse horse = null;
        if (taming != null && taming.Current == HorseTaming.Phase.Struggle)
        {
            horse = taming.Horse;
            first.Show(true);
            second.Show(true);
            first.Set(taming.Strain, taming.Strain > 0.75f ? style.Danger : style.Warning);
            second.Set(taming.Strength, style.Accent);
            hint.text = taming.IsDragging ? $"It drags you! Tap {key}, lean away" : taming.Lean > 0.05f ? $"Tap {key}" : $"Tap {key}, lean away";
        }
        else if (taming != null && taming.Current == HorseTaming.Phase.GivenIn)
        {
            horse = taming.Horse;
            first.Show(true);
            second.Show(false);
            first.Set(taming.MountLeft / Mathf.Max(1f, taming.Horse.Definition.MountSeconds), style.Good);
            bool near = player != null && Vector3.Distance(player.transform.position, taming.Horse.transform.position) <= HorseSettings.Current.MountReach;
            hint.text = near ? $"{key}: get on" : "Walk up beside it";
        }
        else if (taming != null && taming.Current == HorseTaming.Phase.Breaking)
        {
            horse = taming.Horse;
            first.Show(true);
            second.Show(taming.IsAsking);
            first.Set(taming.Balance, taming.Balance < 0.35f ? style.Danger : style.Good);
            if (taming.IsAsking)
            {
                second.Set(taming.PatienceLeft, taming.PatienceLeft < 0.35f ? style.Danger : style.Warning);
            }

            ItemDefinition barley = taming.Horse.Definition.Barley;
            hint.text = taming.IsAsking ? $"{key}: {(barley != null ? barley.DisplayName.ToLowerInvariant() : "food")} ({taming.Fed}/{taming.Needed})"
                : taming.Veer.sqrMagnitude > 0.01f ? "Steer against it!" : "";
        }
        else if (riding != null && riding.IsRiding)
        {
            horse = riding.Riding;
            first.Show(true);
            second.Show(true);
            float stamina = horse.MaxStamina > 0f ? horse.Stamina / horse.MaxStamina : 0f;
            first.Set(stamina, stamina < 0.2f ? style.Warning : style.Good);
            second.Set(horse.Health.HealthFraction, horse.Health.HealthFraction < 0.3f ? style.Danger : style.Text);
            hint.text = "";
        }

        bool shown = horse != null && !windows && !GamePause.IsPaused && Place(horse);
        group.alpha = shown ? 1f : 0f;
    }

    // Over the horse's head, where the camera sees it; hidden when it is behind the camera.
    private bool Place(Horse horse)
    {
        Camera view = Camera.main;
        if (view == null)
        {
            return false;
        }

        Vector3 screen = view.WorldToScreenPoint(horse.transform.position + Vector3.up * Above);
        if (screen.z <= 0f || !RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local))
        {
            return false;
        }

        holder.anchoredPosition = local + canvasRect.rect.size * canvasRect.pivot;
        return true;
    }
}
