using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Small motions that make the interface answer: a punch of scale, a shake, a flash of colour, a fade, a pulse that runs
/// until stopped, and sparks bursting from a point. All in unscaled time (they play while paused) on one hidden runner, with
/// no allocations per frame once running; a motion whose target is destroyed simply ends. Plain functions to call from any
/// view: <c>UIMotion.Punch(button.transform)</c>.
/// </summary>
public sealed class UIMotion : MonoBehaviour
{
    private enum Kind
    {
        Scale,
        Shake,
        Colour,
        Fade,
        Spark,
        Ray,
        Fly,
    }

    private struct Motion
    {
        public Kind Kind;
        public Transform Target;
        public Graphic Graphic;
        public CanvasGroup Group;
        public float Start;
        public float Seconds;
        public float Amount;
        public Vector3 From;
        public Vector3 Velocity;
        public Color Colour;
        public Color Rest;
        public float Spin;
        public float Gravity;
        public Vector3 Size;
        public Vector3 To;
        public System.Action Arrived;
    }

    private static UIMotion runner;
    private static readonly List<Motion> motions = new List<Motion>(64);
    private static readonly Dictionary<Transform, Vector3> restScale = new Dictionary<Transform, Vector3>();
    private static readonly Dictionary<Transform, Vector3> restPosition = new Dictionary<Transform, Vector3>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        runner = null;
        motions.Clear();
        restScale.Clear();
        restPosition.Clear();
    }

    /// <summary>Scales up by <paramref name="amount"/> (0.15 = 15%) and springs back, overshooting a little.</summary>
    public static void Punch(Transform target, float amount = 0.12f, float seconds = 0.35f)
    {
        if (target == null)
        {
            return;
        }

        Add(new Motion { Kind = Kind.Scale, Target = target, Seconds = seconds, Amount = amount, From = RestScale(target) });
    }

    /// <summary>Grows from <paramref name="from"/> of its size to full, with a little overshoot: something arriving.</summary>
    public static void Pop(Transform target, float from = 0.6f, float seconds = 0.4f)
    {
        if (target == null)
        {
            return;
        }

        Add(new Motion { Kind = Kind.Scale, Target = target, Seconds = seconds, Amount = -(1f - from), From = RestScale(target) });
    }

    /// <summary>Shakes sideways by up to <paramref name="pixels"/>: a refusal.</summary>
    public static void Shake(Transform target, float pixels = 8f, float seconds = 0.3f)
    {
        if (target == null)
        {
            return;
        }

        if (!restPosition.ContainsKey(target) || !Moving(target, Kind.Shake))
        {
            restPosition[target] = target.localPosition;
        }

        Add(new Motion { Kind = Kind.Shake, Target = target, Seconds = seconds, Amount = pixels, From = restPosition[target] });
    }

    /// <summary>Flashes a graphic to <paramref name="flash"/> and back to the colour it has now.</summary>
    public static void Flash(Graphic graphic, Color flash, float seconds = 0.4f)
    {
        if (graphic == null)
        {
            return;
        }

        Color rest = graphic.color;
        for (int i = 0; i < motions.Count; i++)
        {
            if (motions[i].Graphic == graphic && motions[i].Kind == Kind.Colour)
            {
                rest = motions[i].Rest; // flashed again before the last one ended: go back to the true colour
            }
        }

        Add(new Motion { Kind = Kind.Colour, Graphic = graphic, Target = graphic.transform, Seconds = seconds, Colour = flash, Rest = rest });
    }

    /// <summary>Fades a group from <paramref name="from"/> to <paramref name="to"/>.</summary>
    public static void Fade(CanvasGroup group, float from, float to, float seconds = 0.25f)
    {
        if (group == null)
        {
            return;
        }

        group.alpha = from;
        Add(new Motion { Kind = Kind.Fade, Group = group, Target = group.transform, Seconds = seconds, From = new Vector3(from, to, 0f) });
    }

    /// <summary>
    /// Sparks: <paramref name="count"/> small diamonds of <paramref name="colour"/> flying out from a point under
    /// <paramref name="parent"/>, spinning and fading. <paramref name="reach"/> is how far they fly, in pixels.
    /// </summary>
    public static void Burst(RectTransform parent, Vector2 at, int count, Color colour, float reach = 220f, float seconds = 0.9f, float size = 10f)
    {
        if (parent == null)
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            // Soft motes, a few of them white-hot, that drift down a little as they fade: embers rather than confetti.
            var spark = new GameObject("Spark", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)spark.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            float grain = Random.Range(0.5f, 1.6f);
            rect.sizeDelta = Vector2.one * size * 2.6f * grain;
            rect.anchoredPosition = at;
            var image = spark.GetComponent<Image>();
            image.sprite = UISprites.Glow;
            Color tint = Random.value < 0.25f ? Color.Lerp(colour, Color.white, 0.7f) : colour;
            image.color = tint;
            image.raycastTarget = false;
            float angle = (i + Random.Range(-0.35f, 0.35f)) / count * Mathf.PI * 2f;
            var velocity = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * reach * Random.Range(0.45f, 1.15f);
            Add(new Motion
            {
                Kind = Kind.Spark, Target = rect, Graphic = image, Seconds = seconds * Random.Range(0.75f, 1.25f), From = at, Velocity = velocity,
                Colour = tint, Spin = Random.Range(0f, 6f), Gravity = reach * Random.Range(0.25f, 0.6f), Size = Vector3.one,
            });
        }
    }

    /// <summary>Plays an interface sound where the player hears it.</summary>
    public static void Sound(SoundDefinition sound)
    {
        if (sound == null)
        {
            return;
        }

        Vector3 ear = Player.Current != null ? Player.Current.transform.position : Camera.main != null ? Camera.main.transform.position : Vector3.zero;
        AudioManager.Play(sound, ear);
    }

    /// <summary>
    /// A glowing mote flying along a curve from one point to another under <paramref name="parent"/>, growing brighter as it
    /// goes; <paramref name="arrived"/> is called as it lands (the experience bar takes it in).
    /// </summary>
    public static void Fly(RectTransform parent, Vector2 from, Vector2 to, Color colour, float seconds, System.Action arrived, float size = 40f)
    {
        if (parent == null)
        {
            return;
        }

        var mote = new GameObject("Mote", typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)mote.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = Vector2.one * size;
        rect.anchoredPosition = from;
        var image = mote.GetComponent<Image>();
        image.sprite = UISprites.Glow;
        image.color = colour;
        image.raycastTarget = false;
        // The curve bows out sideways and up, so motes from the same spot don't all fly the same line.
        Vector2 middle = (from + to) * 0.5f + new Vector2(Random.Range(-160f, 160f), Random.Range(60f, 180f));
        Add(new Motion { Kind = Kind.Fly, Target = rect, Graphic = image, Seconds = seconds, From = from, Velocity = middle, To = to, Colour = colour, Arrived = arrived });
    }

    /// <summary>Rays of light thrown out from a point, turning slowly as they fade: the moment a milestone lands.</summary>
    public static void Rays(RectTransform parent, Vector2 at, int count, Color colour, float length = 520f, float seconds = 1.3f)
    {
        if (parent == null)
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            var shaft = new GameObject("Ray", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)shaft.transform;
            rect.SetParent(parent, false);
            rect.SetAsFirstSibling();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(Random.Range(18f, 34f), length * Random.Range(0.6f, 1.1f));
            rect.anchoredPosition = at;
            float angle = (i + Random.Range(-0.2f, 0.2f)) / count * 360f;
            rect.localRotation = Quaternion.Euler(0f, 0f, angle);
            var image = shaft.GetComponent<Image>();
            image.sprite = UISprites.Ray;
            image.color = colour;
            image.raycastTarget = false;
            Add(new Motion
            {
                Kind = Kind.Ray, Target = rect, Graphic = image, Seconds = seconds * Random.Range(0.85f, 1.15f), Colour = colour,
                Spin = angle, Gravity = Random.Range(-14f, 14f),
            });
        }
    }

    /// <summary>A gentle breathing between 0 and 1, for things that wait for the player: 0.5 + 0.5 sin, at <paramref name="perSecond"/>.</summary>
    public static float Breath(float perSecond = 1.2f) => 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * perSecond * Mathf.PI * 2f);

    // Its scale at rest: what it has now, unless a motion is still moving it (then what it had before that one).
    private static Vector3 RestScale(Transform target)
    {
        if (!restScale.TryGetValue(target, out Vector3 rest) || !Moving(target, Kind.Scale))
        {
            rest = target.localScale;
            restScale[target] = rest;
        }

        return rest;
    }

    private static bool Moving(Transform target, Kind kind)
    {
        for (int i = 0; i < motions.Count; i++)
        {
            if (motions[i].Target == target && motions[i].Kind == kind)
            {
                return true;
            }
        }

        return false;
    }

    private static void Add(Motion motion)
    {
        if (runner == null)
        {
            var holder = new GameObject("UI Motion");
            holder.hideFlags = HideFlags.HideInHierarchy;
            DontDestroyOnLoad(holder);
            runner = holder.AddComponent<UIMotion>();
        }

        motion.Start = Time.unscaledTime;
        // One motion of a kind per target: a new punch replaces the one still running.
        if (motion.Kind != Kind.Spark && motion.Kind != Kind.Ray && motion.Kind != Kind.Fly)
        {
            for (int i = motions.Count - 1; i >= 0; i--)
            {
                if (motions[i].Target == motion.Target && motions[i].Kind == motion.Kind && motions[i].Graphic == motion.Graphic)
                {
                    motions.RemoveAt(i);
                }
            }
        }

        motions.Add(motion);
    }

    private void Update()
    {
        float now = Time.unscaledTime;
        for (int i = motions.Count - 1; i >= 0; i--)
        {
            Motion motion = motions[i];
            if (motion.Target == null)
            {
                motions.RemoveAt(i);
                continue;
            }

            float t = Mathf.Clamp01((now - motion.Start) / Mathf.Max(0.01f, motion.Seconds));
            Step(motion, t);
            if (t >= 1f)
            {
                End(motion);
                motions.RemoveAt(i);
            }
        }
    }

    private static void Step(in Motion motion, float t)
    {
        switch (motion.Kind)
        {
            case Kind.Scale:
                // A punch rises fast and springs back past rest; a pop grows from small with the same spring.
                float spring = Mathf.Sin(t * Mathf.PI * 2.5f) * (1f - t) * (1f - t);
                float scale = motion.Amount >= 0f ? 1f + motion.Amount * spring : 1f + motion.Amount * (1f - EaseOutBack(t));
                motion.Target.localScale = motion.From * scale;
                break;
            case Kind.Shake:
                motion.Target.localPosition = motion.From + Vector3.right * Mathf.Sin(t * Mathf.PI * 7f) * motion.Amount * (1f - t);
                break;
            case Kind.Colour:
                motion.Graphic.color = Color.Lerp(motion.Colour, motion.Rest, t * t);
                break;
            case Kind.Fade:
                motion.Group.alpha = Mathf.Lerp(motion.From.x, motion.From.y, 1f - (1f - t) * (1f - t));
                break;
            case Kind.Spark:
                float out_ = 1f - (1f - t) * (1f - t) * (1f - t);
                ((RectTransform)motion.Target).anchoredPosition = (Vector2)(motion.From + motion.Velocity * out_) + Vector2.down * motion.Gravity * t * t;
                motion.Target.localScale = motion.Size * (1f - t * 0.55f);
                Color colour = motion.Colour;
                colour.a *= (1f - t * t) * (0.85f + 0.15f * Mathf.Sin(t * 40f + motion.Spin));
                motion.Graphic.color = colour;
                break;
            case Kind.Fly:
                float ease = t * t * (3f - 2f * t);
                Vector3 a = Vector3.Lerp(motion.From, motion.Velocity, ease);
                Vector3 b = Vector3.Lerp(motion.Velocity, motion.To, ease);
                ((RectTransform)motion.Target).anchoredPosition = Vector3.Lerp(a, b, ease);
                motion.Target.localScale = Vector3.one * (0.6f + 0.6f * Mathf.Sin(t * Mathf.PI));
                break;
            case Kind.Ray:
                // Grows out fast, then fades as it turns a little.
                motion.Target.localScale = new Vector3(1f, Mathf.Min(1f, t * 5f), 1f);
                motion.Target.localRotation = Quaternion.Euler(0f, 0f, motion.Spin + motion.Gravity * t);
                Color light = motion.Colour;
                light.a *= Mathf.Min(1f, t * 6f) * (1f - t) * (1f - t);
                motion.Graphic.color = light;
                break;
        }
    }

    private static void End(in Motion motion)
    {
        switch (motion.Kind)
        {
            case Kind.Scale:
                motion.Target.localScale = motion.From;
                break;
            case Kind.Shake:
                motion.Target.localPosition = motion.From;
                break;
            case Kind.Colour:
                motion.Graphic.color = motion.Rest;
                break;
            case Kind.Spark:
            case Kind.Ray:
                Destroy(motion.Target.gameObject);
                break;
            case Kind.Fly:
                Destroy(motion.Target.gameObject);
                motion.Arrived?.Invoke();
                break;
        }
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
}
