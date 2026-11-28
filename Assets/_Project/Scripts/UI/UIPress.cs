using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Makes a button feel pressable: it lifts a little under the pointer, sinks while held and springs back when let go.
/// Added to every button UIRole paints, at runtime. Only runs while it is moving.
/// </summary>
[DisallowMultipleComponent]
public sealed class UIPress : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler
{
    private const float Hover = 1.03f;
    private const float Held = 0.95f;
    private const float Speed = 18f;

    private Selectable selectable;
    private Vector3 rest;
    private float target = 1f;
    private float now = 1f;
    private bool over;

    private void Awake()
    {
        selectable = GetComponent<Selectable>();
        rest = transform.localScale;
        enabled = false;
    }

    // Only when the button itself goes away (a window closing): switching off once settled keeps its size.
    private void OnDisable()
    {
        if (gameObject.activeInHierarchy)
        {
            return;
        }

        now = target = 1f;
        over = false;
        if (rest != Vector3.zero)
        {
            transform.localScale = rest;
        }
    }

    private bool Live => selectable == null || selectable.IsInteractable();

    private static float lastHover;

    // No sound for the mouse: the button's lift says it is under the pointer (the user, 2026-11-28: "remove hover for the mouse,
    // keep it for the controller"). On a controller (the stick's pointer in windows, PadCursor, or a selection moved in a
    // conversation or a menu) each step onto a button ticks.
    public void OnPointerEnter(PointerEventData eventData)
    {
        Tick();
        over = true;
        Aim(Live ? Hover : 1f);
    }

    public void OnSelect(BaseEventData eventData) => Tick();

    private void Tick()
    {
        // A soft tick, never more than one a moment, so a sweep down a list murmurs instead of rattling.
        if (GameInput.UsingGamepad && Time.unscaledTime - lastHover > 0.06f && Live && AudioManager.Sounds != null && AudioManager.Sounds.Hover != null)
        {
            lastHover = Time.unscaledTime;
            Vector3 at = Player.Current != null ? Player.Current.transform.position : Camera.main != null ? Camera.main.transform.position : Vector3.zero;
            AudioManager.Play(AudioManager.Sounds.Hover, at);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        over = false;
        Aim(1f);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (Live)
        {
            Aim(Held);
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (Live)
        {
            // Springs back past its size, so the press lands.
            now = 1.06f;
        }

        Aim(over && Live ? Hover : 1f);
    }

    private void Aim(float scale)
    {
        if (!enabled)
        {
            rest = transform.localScale / Mathf.Max(0.01f, now);
        }

        target = scale;
        enabled = true;
    }

    private void Update()
    {
        now = Mathf.Lerp(now, target, 1f - Mathf.Exp(-Speed * Time.unscaledDeltaTime));
        transform.localScale = rest * now;
        if (Mathf.Abs(now - target) < 0.002f)
        {
            now = target;
            transform.localScale = rest * now;
            enabled = false;
        }
    }

    // Entering Play doesn't reload scripts in this project: what a session left in the statics goes (EngineHazardTests).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        lastHover = 0f;
    }
}
