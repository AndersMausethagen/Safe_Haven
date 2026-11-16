using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Now and then, far enough from home and at the right hour, puts a Glimpse where the player will see it: a little ahead of
/// them, at the edge of what the camera shows. One at a time and rarely, so each is a moment. Entries are data: what, how far
/// from the portal, at what hours, how likely, and whether it wants to stand by a dead tree or rock.
/// <para>Where it belongs: once in a scene with woods (the Haven world, a raid map).</para>
/// </summary>
public sealed class GlimpseWatcher : MonoBehaviour
{
    [Serializable]
    public sealed class Entry
    {
        public string name = "Hollowed Deer";
        public Glimpse prefab;
        [Tooltip("Metres from the portal it is seen beyond (the deep woods).")]
        [Min(0f)] public float beyondPortal = 280f;
        [Tooltip("How likely at each hour (0-24 across, 0-1 up).")]
        public AnimationCurve byHour = AnimationCurve.Constant(0f, 24f, 1f);
        [Tooltip("Metres from the player it appears at, least and most.")]
        public Vector2 distance = new Vector2(18f, 24f);
        [Tooltip("Relative chance among the entries allowed now.")]
        [Min(0f)] public float weight = 1f;
        [Tooltip("How much more often it comes as the blight spreads round home (SettlementState.Blight): its weight is " +
                 "multiplied by 1 + this at full blight, and it comes up to 60% nearer the portal.")]
        [Min(0f)] public float moreWithBlight;

        public float WeightNow(float blight) => weight * (1f + moreWithBlight * blight);

        public float BeyondNow(float blight) => moreWithBlight > 0f ? beyondPortal * (1f - 0.6f * blight) : beyondPortal;
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();
    [Tooltip("Seconds between chances, least and most.")]
    [SerializeField] private Vector2 every = new Vector2(70f, 150f);
    [Tooltip("Chance at each, of anything at all.")]
    [SerializeField, Range(0f, 1f)] private float chance = 0.35f;

    private float due;
    private Glimpse showing;

    public void Configure(List<Entry> glimpses, Vector2 seconds, float odds)
    {
        entries = glimpses;
        every = seconds;
        chance = odds;
    }

    private void OnEnable() => due = Time.time + UnityEngine.Random.Range(every.x, every.y);

    private void Update()
    {
        if (Time.time < due || showing != null)
        {
            return;
        }

        due = Time.time + UnityEngine.Random.Range(every.x, every.y);
        Player player = Player.Current;
        Camera view = Camera.main;
        if (player == null || view == null || UnityEngine.Random.value > chance || MonsterAI.AnyHuntingPlayerSide())
        {
            return;
        }

        Vector3 at = player.transform.position;
        float fromHome = HomePortal.Current != null ? Vector3.Distance(at, HomePortal.Current.transform.position) : float.MaxValue;
        float hour = AtmosphereConditions.Hour;
        float blight = GameSession.Here.Blight;
        Entry pick = Pick(entry => entry.prefab != null && fromHome >= entry.BeyondNow(blight) && SpiritBonds.Welcome(entry.prefab)
            && UnityEngine.Random.value < entry.byHour.Evaluate(hour), blight);
        if (pick == null)
        {
            return;
        }

        // Ahead of the player as the camera looks (the top of the screen), a little to one side.
        Vector3 forward = view.transform.forward;
        forward.y = 0f;
        forward = forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward;
        for (int tries = 0; tries < 6; tries++)
        {
            Vector3 heading = Quaternion.Euler(0f, UnityEngine.Random.Range(-40f, 40f), 0f) * forward;
            Vector3 wanted = at + heading * UnityEngine.Random.Range(pick.distance.x, pick.distance.y);
            if (NavMesh.SamplePosition(wanted, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            {
                showing = Instantiate(pick.prefab, hit.position, Quaternion.LookRotation(-heading));
                return;
            }
        }
    }

    // The allowed entries are settled first (the hour is a roll each), then one is drawn by weight.
    private Entry Pick(Predicate<Entry> allowed, float blight)
    {
        var now = entries.FindAll(allowed);
        float total = 0f;
        foreach (Entry entry in now)
        {
            total += entry.WeightNow(blight);
        }

        float roll = UnityEngine.Random.value * total;
        foreach (Entry entry in now)
        {
            if ((roll -= entry.WeightNow(blight)) <= 0f)
            {
                return entry;
            }
        }

        return null;
    }
}
