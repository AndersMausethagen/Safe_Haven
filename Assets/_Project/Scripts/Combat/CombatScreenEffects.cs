using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Screen-edge feedback for the player: a red flash when hurt (stronger for bigger hits), a slow pulse while health is
/// low, and a cool flash on a parry or perfect dodge. One overlay canvas with a generated vignette, made by
/// CombatFeedback in every scene with a player; colors and strengths are in Combat Feel › Hurt vignette.
/// </summary>
public sealed class CombatScreenEffects : MonoBehaviour
{
    private const int Size = 128;

    private RawImage image;
    private Damageable player;
    private float flash;
    private Color flashColor;

    public static CombatScreenEffects Create()
    {
        var go = new GameObject("Combat Screen Effects");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 400;
        var effects = go.AddComponent<CombatScreenEffects>();
        var imageObject = new GameObject("Vignette", typeof(RectTransform));
        imageObject.transform.SetParent(go.transform, false);
        var rect = (RectTransform)imageObject.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        effects.image = imageObject.AddComponent<RawImage>();
        effects.image.texture = VignetteTexture();
        effects.image.raycastTarget = false;
        effects.image.color = Color.clear;
        return effects;
    }

    public void Watch(Damageable target)
    {
        if (player != null)
        {
            player.Damaged -= OnDamaged;
        }

        player = target;
        if (player != null)
        {
            player.Damaged += OnDamaged;
        }
    }

    private void OnDestroy() => Watch(null);

    private void OnDamaged(DamageInfo damage, DamageResult result)
    {
        if (result.Damage <= 0f || player == null)
        {
            return;
        }

        // A quarter of max health or more flashes at full strength; blocked hits barely.
        float share = Mathf.Clamp01(result.Damage / (player.MaxHealth * 0.25f));
        Flash(CombatFeelSettings.Current.HurtColor, Mathf.Lerp(0.35f, 1f, share) * (result.Blocked ? 0.4f : 1f));
    }

    /// <summary>Flashes the edges in <paramref name="color"/> at <paramref name="strength"/> (0-1), fading out.</summary>
    public void Flash(Color color, float strength)
    {
        flashColor = color;
        flash = Mathf.Max(flash, Mathf.Clamp01(strength) * GameOptions.ScreenFlash.Value);
    }

    private void Update()
    {
        CombatFeelSettings feel = CombatFeelSettings.Current;
        flash = Mathf.Max(0f, flash - Time.unscaledDeltaTime / Mathf.Max(0.05f, feel.HurtSeconds));
        Color color = flashColor;
        float alpha = flash * flash * color.a;

        if (player != null && player.IsAlive && feel.LowHealthBelow > 0f && player.HealthFraction < feel.LowHealthBelow)
        {
            float depth = 1f - player.HealthFraction / feel.LowHealthBelow;
            float pulse = (0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * Mathf.Lerp(3f, 6f, depth))) * feel.LowHealthStrength * depth * GameOptions.ScreenFlash.Value;
            if (pulse > alpha)
            {
                color = feel.HurtColor;
                alpha = pulse;
            }
        }

        color.a = alpha;
        image.color = color;
        image.enabled = alpha > 0.001f;
    }

    private static Texture2D VignetteTexture()
    {
        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Combat Vignette" };
        var pixels = new Color32[Size * Size];
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                float u = (x + 0.5f) / Size * 2f - 1f, v = (y + 0.5f) / Size * 2f - 1f;
                float d = Mathf.Sqrt(u * u * 0.8f + v * v);
                byte a = (byte)(Mathf.SmoothStep(0.55f, 1.35f, d) * 255f);
                pixels[y * Size + x] = new Color32(255, 255, 255, a);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }
}
