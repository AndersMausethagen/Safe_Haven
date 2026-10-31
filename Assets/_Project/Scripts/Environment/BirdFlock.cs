using UnityEngine;

/// <summary>What one bird is doing.</summary>
public enum BirdState
{
    /// <summary>Flying in to its spot on the ground.</summary>
    Arriving,
    /// <summary>On the ground, pecking and hopping about.</summary>
    Perched,
    /// <summary>Flying away for good, up and out of the picture.</summary>
    Leaving,
}

/// <summary>One bird. A struct in an array, as there are only ever a few dozen and they move every frame.</summary>
public struct Bird
{
    public const int PerchedFrame = 0;
    public const int PeckingFrame = 1;
    public const int FirstFlapFrame = 2;
    public const int FlapFrames = 6;
    public const int HeldOutFrame = FirstFlapFrame + 1;

    public BirdState State;
    public Vector3 Position;
    public Vector3 Velocity;
    public Vector3 Spot;
    public Color Colour;
    public float Size;
    public float Facing;
    public float Timer;
    public float Age;
    public float Flap;
    public float Hop;
    public bool Pecking;
    public bool Gliding;
}

/// <summary>
/// A few birds that arrive together, settle, and go up together when something comes too close. Plain data and rules, no Unity
/// objects, so <see cref="Birds"/> only has to draw the result. Each leaves a moment after the last, so a flock bursts up
/// rather than lifting like a sheet.
/// </summary>
public sealed class BirdFlock
{
    private const float ArrivalStagger = 1.2f;
    private const float TakeOffStagger = 0.3f;
    private const float FleeSpread = 55f;
    private const float SettleDistance = 0.15f;
    private const float BrakingDistance = 4f;
    private const float FadeSeconds = 1.2f;
    private const float HopSeconds = 0.22f;
    private const float HopReach = 0.35f;
    private const float HopHeight = 0.12f;
    private const float PeckSeconds = 0.35f;
    private const float GustSeconds = 1.3f;

    private readonly BirdProfile profile;
    private bool passing;

    private BirdFlock(BirdProfile profile, int count, Vector3 centre)
    {
        this.profile = profile;
        Centre = centre;
        Birds = new Bird[count];
        NextCall = profile.RandomCall;
    }

    public Bird[] Birds { get; }

    /// <summary>The middle of where it is, or was going to land.</summary>
    public Vector3 Centre { get; }

    /// <summary>True once every bird has flown off the picture.</summary>
    public bool IsGone { get; private set; }

    /// <summary>True once it has been frightened, or was only ever passing.</summary>
    public bool IsLeaving { get; private set; }

    /// <summary>Seconds until a settled flock calls again. <see cref="global::Birds"/> plays it.</summary>
    public float NextCall { get; set; }

    /// <summary>True while most of it is on the ground, so it can be startled.</summary>
    public bool IsSettled
    {
        get
        {
            if (IsLeaving)
            {
                return false;
            }

            int down = 0;
            foreach (Bird bird in Birds)
            {
                if (bird.State == BirdState.Perched)
                {
                    down++;
                }
            }

            return down * 2 >= Birds.Length;
        }
    }

    /// <summary>A flock flying in from <paramref name="from"/> to land on <paramref name="spots"/>.</summary>
    public static BirdFlock Landing(BirdProfile profile, Vector3 from, Vector3 centre, Vector3[] spots)
    {
        var flock = new BirdFlock(profile, spots.Length, centre);
        for (int i = 0; i < spots.Length; i++)
        {
            flock.Birds[i] = new Bird
            {
                State = BirdState.Arriving,
                Position = from + Random.insideUnitSphere * 2f,
                Spot = spots[i],
                Colour = profile.RandomColour,
                Size = profile.RandomSize,
                Timer = Random.Range(0f, ArrivalStagger),
                Velocity = (spots[i] - from).normalized * profile.RandomSpeed,
                Flap = Random.value,
            };
        }

        return flock;
    }

    /// <summary>A flock crossing the sky: it never lands, so nothing can startle it.</summary>
    public static BirdFlock Passing(BirdProfile profile, Vector3 from, Vector3 velocity, int count)
    {
        var flock = new BirdFlock(profile, count, from) { IsLeaving = true, passing = true };
        for (int i = 0; i < count; i++)
        {
            flock.Birds[i] = new Bird
            {
                State = BirdState.Leaving,
                Position = from + Vector3.Scale(Random.insideUnitSphere, new Vector3(3.5f, 0.6f, 3.5f)),
                Colour = profile.RandomColour,
                Size = profile.RandomSize,
                Velocity = velocity,
                Facing = Mathf.Atan2(velocity.x, velocity.z),
                Flap = Random.value,
            };
        }

        return flock;
    }

    /// <summary>Sends the whole flock up and away from a point. Does nothing to a flock already leaving.</summary>
    public void Startle(Vector3 from)
    {
        if (IsLeaving)
        {
            return;
        }

        IsLeaving = true;
        for (int i = 0; i < Birds.Length; i++)
        {
            ref Bird bird = ref Birds[i];
            Vector3 away = bird.Position - from;
            away.y = 0f;
            away = away.sqrMagnitude > 0.001f ? away.normalized : Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
            away = Quaternion.Euler(0f, Random.Range(-FleeSpread, FleeSpread), 0f) * away;
            bird.State = BirdState.Leaving;
            bird.Timer = Random.Range(0f, TakeOffStagger);
            bird.Velocity = away * profile.RandomSpeed + Vector3.up * profile.Climb;
            bird.Facing = Mathf.Atan2(away.x, away.z);
            bird.Age = 0f;
        }
    }

    /// <summary>How opaque a bird is: it fades in and out of the sky rather than popping.</summary>
    public float Opacity(in Bird bird)
    {
        float life = profile.FleeSeconds + FadeSeconds;
        if (bird.State != BirdState.Leaving)
        {
            return Mathf.Clamp01(bird.Age / FadeSeconds);
        }

        float fadingOut = Mathf.Clamp01((life - bird.Age) / FadeSeconds);
        return passing ? Mathf.Min(fadingOut, Mathf.Clamp01(bird.Age / FadeSeconds)) : fadingOut;
    }

    /// <summary>Which picture of the sprite sheet shows it now.</summary>
    public static int FrameOf(in Bird bird)
    {
        if (bird.State == BirdState.Perched)
        {
            return bird.Pecking ? Bird.PeckingFrame : Bird.PerchedFrame;
        }

        if (bird.Gliding)
        {
            return Bird.HeldOutFrame;
        }

        return Bird.FirstFlapFrame + Mathf.FloorToInt(Mathf.Repeat(bird.Flap, 1f) * Bird.FlapFrames) % Bird.FlapFrames;
    }

    public void Tick(float seconds)
    {
        bool anyHere = false;
        float life = profile.FleeSeconds + FadeSeconds;
        for (int i = 0; i < Birds.Length; i++)
        {
            ref Bird bird = ref Birds[i];
            switch (bird.State)
            {
                case BirdState.Arriving:
                    TickArriving(ref bird, seconds);
                    anyHere = true;
                    break;
                case BirdState.Perched:
                    TickPerched(ref bird, seconds);
                    anyHere = true;
                    break;
                default:
                    TickLeaving(ref bird, seconds);
                    anyHere |= bird.Age < life;
                    break;
            }
        }

        IsGone = !anyHere;
    }

    private void TickArriving(ref Bird bird, float seconds)
    {
        if (bird.Timer > 0f)
        {
            bird.Timer -= seconds;
            return;
        }

        bird.Age += seconds;
        Vector3 toSpot = bird.Spot - bird.Position;
        float distance = toSpot.magnitude;
        if (distance <= SettleDistance)
        {
            bird.State = BirdState.Perched;
            bird.Position = bird.Spot;
            bird.Timer = Random.Range(0.3f, 1.5f);
            return;
        }

        float speed = bird.Velocity.magnitude * Mathf.Clamp(distance / BrakingDistance, 0.3f, 1f);
        Vector3 step = toSpot / distance * speed;
        bird.Position += step * Mathf.Min(seconds, distance / Mathf.Max(0.01f, speed));
        bird.Facing = Mathf.Atan2(step.x, step.z);
        bird.Flap += seconds * profile.FlapRate;
    }

    // Peck, peck, a hop to one side. The hop is the only time it moves.
    private void TickPerched(ref Bird bird, float seconds)
    {
        bird.Age += seconds;
        bird.Timer -= seconds;
        if (bird.Hop > 0f)
        {
            bird.Hop = Mathf.Max(0f, bird.Hop - seconds);
            float rise = Mathf.Sin(bird.Hop / HopSeconds * Mathf.PI);
            bird.Position = bird.Spot + Vector3.up * (rise * HopHeight);
            bird.Spot += bird.Velocity * seconds;
            return;
        }

        if (bird.Timer > 0f)
        {
            return;
        }

        bird.Pecking = !bird.Pecking && Random.value < 0.7f;
        bird.Timer = bird.Pecking ? PeckSeconds : Random.Range(0.5f, 2.2f);
        if (!bird.Pecking && Random.value < 0.35f)
        {
            float direction = Random.Range(0f, Mathf.PI * 2f);
            var hop = new Vector3(Mathf.Sin(direction), 0f, Mathf.Cos(direction));
            bird.Facing = direction;
            bird.Velocity = hop * (HopReach / HopSeconds);
            bird.Hop = HopSeconds;
        }
    }

    private void TickLeaving(ref Bird bird, float seconds)
    {
        if (bird.Timer > 0f)
        {
            bird.Timer -= seconds;
            return;
        }

        bird.Age += seconds;
        bird.Position += bird.Velocity * seconds;
        // The climb eases off as it gets clear.
        bird.Velocity.y = Mathf.MoveTowards(bird.Velocity.y, profile.Climb * 0.25f, profile.Climb * 0.5f * seconds);
        bird.Gliding = profile.Glide > 0f && Mathf.Repeat((bird.Age + bird.Flap) / GustSeconds, 1f) < profile.Glide;
        if (!bird.Gliding)
        {
            bird.Flap += seconds * profile.FlapRate;
        }
    }
}
