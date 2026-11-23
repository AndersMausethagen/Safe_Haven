using UnityEngine;

/// <summary>
/// A word in passing: when the player walks by a resident who is about (not asleep, not talking, nothing hunting), the
/// resident says something aside, as a subtitle that leaves the controls alone. News first, once per homecoming (someone
/// lost, a boss beaten, someone home who doesn't know them yet: ResidentNpc.News), otherwise now and then a line for the
/// hour. Added by ResidentNpc; the lines are in Settlement Settings › In passing.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ResidentNpc))]
public sealed class ResidentAsides : MonoBehaviour
{
    private const float Within = 3.5f;
    private static float lastSpokenAt = -999f;
    private ResidentNpc npc;
    private ResidentLife life;
    private float nextAside;
    private int newsToldAt = -1;
    private float nextCheck;

    private void Awake()
    {
        npc = GetComponent<ResidentNpc>();
        life = GetComponent<ResidentLife>();
        nextAside = Time.time + Random.Range(20f, 60f);
    }

    private void Update()
    {
        if (Time.time < nextCheck)
        {
            return;
        }

        nextCheck = Time.time + 0.5f;
        Player player = Player.Current;
        DialogueBox box = DialogueBox.Current;
        if (player == null || box == null || DialogueBox.IsOpen || box.IsSubtitling || Time.time - lastSpokenAt < 8f
            || (life != null && life.Activity == ResidentActivity.Sleep) || MonsterAI.AnyHuntingPlayerSide()
            || (player.transform.position - transform.position).sqrMagnitude > Within * Within)
        {
            return;
        }

        SettlementSettings settings = SettlementSettings.Current;
        int homecoming = GameSession.Here.SuccessfulExtractions;
        string line = null;
        if (newsToldAt != homecoming)
        {
            line = npc.News();
            newsToldAt = homecoming;
        }

        if (line == null && Time.time >= nextAside)
        {
            FestivalDefinition festival = Festivals.Now;
            HavenRequestDefinition asked = HavenRequestBoard.AskedBy(npc.Resident);
            if (asked != null && festival == null && Random.value < 0.5f)
            {
                line = asked.IsOrder
                    ? $"{asked.Ask} A {asked.WantedName}, when your hands are up to it."
                    : $"{asked.Ask} {asked.Count} {asked.WantedName}, if you come across it.";
            }
            else if (festival != null && festival.Lines.Count > 0)
            {
                line = festival.Lines[Random.Range(0, festival.Lines.Count)];
            }
            else if (PersonalLine() is string own)
            {
                line = own;
            }
            else
            {
                string[] lines = settings.AsidesAt(AtmosphereConditions.Hour);
                line = lines.Length > 0 ? lines[Random.Range(0, lines.Length)] : null;
            }
        }

        if (line == null)
        {
            return;
        }

        nextAside = Time.time + settings.AsideEvery * Random.Range(0.8f, 1.3f);
        lastSpokenAt = Time.time;
        string who = npc.Resident != null ? npc.Resident.DisplayName : name;
        box.Bark(new DialogueSpeaker(who, transform, null, 0f, null, transform), line);
    }

    // Who they are, now and then (Docs/PeopleResearch_2026-11-23.md): a friend the forest has, asked after; else, a third of the time, a
    // line of one of their traits (their tell). Null for the hour's line instead. In one's own Haven: a friend's people say the hour's.
    private string PersonalLine()
    {
        ResidentRecord record = npc.Resident != null && !HavenVisit.InHostsHaven ? GameSession.Here.Find(npc.Resident) : null;
        if (record == null)
        {
            return null;
        }

        ResidentDefinition missed = PeopleStandings.Misses(GameSession.Here, record);
        if (missed != null && Random.value < 0.6f)
        {
            return $"Bring {missed.DisplayName} home. The forest has no right to them.";
        }

        return Random.value < 0.35f ? ResidentTraits.LineOf(record, Random.Range(0, 1000)) : null;
    }

    // Entering Play doesn't reload scripts in this project, and Time.time starts again at 0: an old moment would silence residents.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => lastSpokenAt = -999f;
}
