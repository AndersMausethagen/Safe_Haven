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
            var spark = new GameObject("Spark", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)spark.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            float grain = Random.Range(0.6f, 1.3f);
            rect.sizeDelta = Vector2.one * size * grain;
            rect.anchoredPosition = at;
            rect.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var image = spark.GetComponent<Image>();
            image.color = colour;
            image.raycastTarget = false;
            float angle = (i + Random.Range(-0.35f, 0.35f)) / count * Mathf.PI * 2f;
            var velocity = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * reach * Random.Range(0.55f, 1.15f);
            Add(new Motion
            {
                Kind = Kind.Spark, Target = rect, Graphic = image, Seconds = seconds * Random.Range(0.75f, 1.15f), From = at, Velocity = velocity,
                Colour = colour, Spin = Random.Range(-540f, 540f),
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
        if (motion.Kind != Kind.Spark)
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
                ((RectTransform)motion.Target).anchoredPosition = (Vector2)(motion.From + motion.Velocity * out_);
                motion.Target.localRotation = Quaternion.Euler(0f, 0f, 45f + motion.Spin * t);
                motion.Target.localScale = Vector3.one * (1f - t * 0.6f);
                Color colour = motion.Colour;
                colour.a *= 1f - t * t;
                motion.Graphic.color = colour;
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
                Destroy(motion.Target.gameObject);
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
