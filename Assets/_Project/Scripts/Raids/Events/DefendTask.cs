using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hold the site's circle for a while as waves come (Docs/RaidEventsPlan_2026-11-26.md E3, like an exit's hold): the count runs only while
/// the player stands in it, a wave comes every so often, and the reward comes once it is full. Gone far and long, the chance is lost.
/// </summary>
public sealed class DefendTask : RaidEventTask
{
    // Seconds before the end in which no new wave comes.
    private const float NoWaveInLast = 5f;

    /// <summary>True when it stands on an object of its own (an event with no site), which goes with it.</summary>
    public bool OwnObject { get; set; }

    // Metres and seconds away after which the hold is given up.
    private const float GiveUpBeyond = 45f;
    private const float GiveUpAfter = 45f;

    private RaidEventContext context;
    private string label;
    private float radius;
    private float seconds;
    private float waveEvery;
    private List<RaidEventOutcome> eachWave;
    private List<RaidEventOutcome> onHeld;
    private float held;
    private float nextWave;
    private float awayFor;

    /// <summary>Seconds held so far.</summary>
    public float Held => held;

    public bool IsDone { get; private set; }

    public override string HudLine
    {
        get
        {
            int left = Mathf.CeilToInt(Mathf.Max(0f, seconds - held));
            bool inside = context?.Player != null && FlatDistance(context.Player.transform.position, transform.position) <= radius;
            string state = inside ? $"{left / 60}:{left % 60:00}" : "stand in the circle";
            return $"<color={UIStyle.Current.AccentHex}>{label}</color>: {state}";
        }
    }

    public void Begin(RaidEventContext eventContext, string what, float circle, float duration, float wavesEvery,
        List<RaidEventOutcome> waves, List<RaidEventOutcome> reward)
    {
        context = eventContext;
        label = what;
        radius = circle;
        seconds = duration;
        waveEvery = Mathf.Max(5f, wavesEvery);
        eachWave = waves;
        onHeld = reward;
        nextWave = 0f;
        if (context?.Site != null)
        {
            context.Site.ShowMark(true);
        }
    }

    /// <summary>Advances the hold by <paramref name="deltaTime"/> with the player inside or not. Public for tests.</summary>
    public void Step(float deltaTime, bool inside)
    {
        if (IsDone)
        {
            return;
        }

        if (!inside)
        {
            awayFor += deltaTime;
            return;
        }

        awayFor = 0f;
        // No wave in the last seconds: it would arrive with the reward, after the stand is over.
        if (held >= nextWave)
        {
            nextWave += waveEvery;
            if (seconds - held >= NoWaveInLast)
            {
                Apply(eachWave, context, transform.position);
            }
        }

        held += deltaTime;
        if (held >= seconds)
        {
            IsDone = true;
            Apply(onHeld, context, transform.position);
            Finish();
        }
    }

    private void Update()
    {
        if (context?.Player == null || IsDone)
        {
            return;
        }

        // Over with the raid (the report is written): nothing more comes of it.
        if (context.Session != null && context.Session.IsOver)
        {
            Finish();
            return;
        }

        // A fallen player holds nothing, wherever the body lies.
        bool alive = !context.Player.TryGetComponent(out Damageable health) || health.IsAlive;
        float distance = FlatDistance(context.Player.transform.position, transform.position);
        Step(Time.deltaTime, alive && distance <= radius);
        if (!IsDone && distance > GiveUpBeyond && awayFor >= GiveUpAfter)
        {
            GameMessages.Post($"{label}: you were gone too long.", MessageKind.Warning);
            Finish();
        }
    }

    private void Finish()
    {
        if (context?.Site != null)
        {
            context.Site.ShowMark(false);
        }

        // Out of Play (a test stepping it by hand) it only stops: Destroy there is refused. A hold with no site stood on its own object.
        if (Application.isPlaying)
        {
            Destroy(OwnObject ? gameObject : (Object)this);
        }
        else
        {
            enabled = false;
        }
    }
}
