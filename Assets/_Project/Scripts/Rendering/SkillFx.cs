using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// Plays a skill's look (SkillLook): its cues placed over the caster, the attack's reach and arc, or the creatures struck, each after its
/// delay and stepped one after another, from a pool of effect objects (one pool a prefab, reused rather than made and destroyed each
/// cast); a light flash and a camera shake at the release; and the area mark while it winds up (SkillAreaMark). Placed on the ground by
/// the terrain or what lies under the point. One runner per scene, made on first use.
/// </summary>
public sealed class SkillFx : MonoBehaviour
{
    private static SkillFx runner;
    private readonly Dictionary<GameObject, Stack<GameObject>> pools = new Dictionary<GameObject, Stack<GameObject>>();
    private readonly Dictionary<GameObject, GameObject> prefabOf = new Dictionary<GameObject, GameObject>();
    private Light flash;
    private float flashEnds;
    private float flashFrom;
    private float flashSeconds = 0.35f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => runner = null;

    private static SkillFx Runner
    {
        get
        {
            if (runner == null)
            {
                runner = new GameObject("Skill Effects").AddComponent<SkillFx>();
            }

            return runner;
        }
    }

    /// <summary>The cast: its cues round the caster, and its area while it winds up. <paramref name="lunge"/>: how far a lunge or a
    /// leap carries the caster first (negative: its whole Lunge Distance).</summary>
    public static void Cast(SkillDefinition skill, Transform caster, Vector3 aim, float windUp, float lunge = -1f)
    {
        if (skill == null || skill.Look == null)
        {
            return;
        }

        SkillLook look = skill.Look;
        if (look.showArea && skill.Attack != null && windUp > 0.05f)
        {
            // Where it will land: past the lunge for a skill that carries the caster there first (Mountain Leap, Wolf's Lunge).
            Vector3 flat = new Vector3(aim.x, 0f, aim.z);
            Vector3 lands = caster.position + (flat.sqrMagnitude > 0.0001f ? flat.normalized : Vector3.zero) * (lunge >= 0f ? lunge : skill.LungeDistance);
            SkillAreaMark.Show(lands, aim, skill.Attack.Range, skill.Attack.Arc, SkillSchools.ColorOf(skill.School), windUp);
        }

        Runner.PlayAll(look.cast, caster.position, aim, Reach(skill.Attack), Arc(skill.Attack), null, caster.position);
        if (look.castSound != null)
        {
            AudioManager.Play(look.castSound, caster.position);
        }

        Runner.PlaySounds(look, SkillSoundPhase.Cast, caster.position);
        // Its big moment's wind-up: the camera pushes in until it lands (or, with no blow, until its moment comes).
        if (look.moment != null)
        {
            SkillMoments.WindUp(look.moment, windUp > 0.05f ? windUp : look.moment.impactDelay);
        }
    }

    // The look's extra sound layers of a phase, each after its delay in real time (slow motion doesn't stretch a riser or a tail).
    private void PlaySounds(SkillLook look, SkillSoundPhase phase, Vector3 at)
    {
        if (look.sounds == null)
        {
            return;
        }

        foreach (SkillSoundCue cue in look.sounds)
        {
            if (cue == null || cue.sound == null || cue.phase != phase)
            {
                continue;
            }

            if (cue.delay > 0f)
            {
                StartCoroutine(SoundLaterRealtime(cue.sound, at, cue.delay));
            }
            else
            {
                AudioManager.Play(cue.sound, at);
            }
        }
    }

    private static IEnumerator SoundLaterRealtime(SoundDefinition sound, Vector3 at, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        AudioManager.Play(sound, at);
    }

    /// <summary>A weapon art begins (its attack's own look, AttackDefinition.Look): its cast cues round the striker.</summary>
    public static void ArtBegin(AttackDefinition attack, Vector3 origin, Vector3 aim)
    {
        if (attack != null && attack.Look != null && !attack.Look.IsEmpty)
        {
            Runner.PlayAll(attack.Look.cast, origin, aim, attack.Range, attack.Arc, null, origin);
            if (attack.Look.castSound != null)
            {
                AudioManager.Play(attack.Look.castSound, origin);
            }
        }
    }

    /// <summary>A weapon art lands: its release cues over its reach and arc (and along the path a dash came, from <paramref name="from"/>),
    /// on what it struck, a flash and a shake.</summary>
    public static void ArtLand(AttackDefinition attack, Vector3 origin, Vector3 aim, IReadOnlyList<Vector3> struck, Vector3 from)
    {
        if (attack != null && attack.Look != null && !attack.Look.IsEmpty)
        {
            Runner.Land(attack.Look, origin, aim, attack.Range, attack.Arc, struck, from);
        }
    }

    private static float Reach(AttackDefinition attack) => attack != null ? attack.Range : 2f;

    private static float Arc(AttackDefinition attack) => attack != null ? attack.Arc : 60f;

    /// <summary>The release: its cues over the attack's reach and arc, on what it struck, a flash and a shake.</summary>
    public static void Release(SkillDefinition skill, Vector3 origin, Vector3 aim, IReadOnlyList<Vector3> struck)
    {
        if (skill == null || skill.Look == null)
        {
            return;
        }

        Runner.Land(skill.Look, origin, aim, Reach(skill.Attack), Arc(skill.Attack), struck, origin);
    }

    private void Land(SkillLook look, Vector3 origin, Vector3 aim, float reach, float arc, IReadOnlyList<Vector3> struck, Vector3 from)
    {
        PlayAll(look.release, origin, aim, reach, arc, null, from);
        PlayAll(look.struck, origin, aim, reach, arc, struck, from);
        if (look.releaseSound != null)
        {
            if (look.releaseSoundDelay > 0f)
            {
                StartCoroutine(SoundLater(look.releaseSound, origin, look.releaseSoundDelay));
            }
            else
            {
                AudioManager.Play(look.releaseSound, origin);
            }
        }
        if (look.light.a > 0f && look.lightIntensity > 0f)
        {
            Flash(origin + Vector3.up * 1.2f, look.light, look.lightIntensity, look.lightRange, look.lightSeconds);
        }

        if (look.shake > 0f)
        {
            CameraShake.Add(look.shake);
        }

        PlaySounds(look, SkillSoundPhase.Release, origin);
        // Its weight by size (Docs/SkillsImprovementResearch_2026-11-24.md 6): a freeze when it struck something (an ultimate's always:
        // its moment is the blow), and the frame pushed toward the blow.
        bool moment = look.moment != null && !look.moment.IsEmpty;
        if (look.hitStop > 0f && (moment || struck != null && struck.Count > 0) && CombatFeedback.Current != null)
        {
            CombatFeedback.Current.HitStop(look.hitStop);
        }

        if (moment)
        {
            SkillMoments.Impact(look.moment);
        }

        if (look.kick > 0f)
        {
            CameraShake.Kick(aim, look.kick);
        }
    }

    private void PlayAll(List<SkillFxCue> cues, Vector3 origin, Vector3 aim, float reach, float arc, IReadOnlyList<Vector3> struck, Vector3 from)
    {
        aim.y = 0f;
        aim = aim.sqrMagnitude > 0.0001f ? aim.normalized : Vector3.forward;
        foreach (SkillFxCue cue in cues)
        {
            if (cue == null || cue.prefab == null)
            {
                continue;
            }

            List<Vector3> points = Points(cue, origin, aim, reach, arc, struck, from);
            for (int i = 0; i < points.Count; i++)
            {
                StartCoroutine(PlayLater(cue, points[i], aim, cue.delay + cue.stepDelay * i));
            }
        }
    }

    /// <summary>Where a cue's effects go (public for tests): its place over the caster, the reach, the arc or what was struck.</summary>
    public static List<Vector3> Points(SkillFxCue cue, Vector3 origin, Vector3 aim, float reach, float arc, IReadOnlyList<Vector3> struck,
        Vector3? from = null)
    {
        var points = new List<Vector3>();
        int count = Mathf.Max(1, cue.count);
        switch (cue.place)
        {
            case SkillFxPlace.Caster:
                points.Add(origin + Vector3.up * 1.1f);
                break;
            case SkillFxPlace.CasterGround:
                points.Add(Grounded(origin));
                break;
            case SkillFxPlace.AlongReach:
                for (int i = 0; i < count; i++)
                {
                    float share = count == 1 ? 1f : Mathf.Lerp(0.18f, 1f, i / (float)(count - 1));
                    points.Add(Grounded(origin + aim * reach * share));
                }

                break;
            case SkillFxPlace.AcrossArc:
                for (int i = 0; i < count; i++)
                {
                    float angle = count == 1 ? 0f : Mathf.Lerp(-arc * 0.5f, arc * 0.5f, i / (float)(count - 1));
                    points.Add(Grounded(origin + Quaternion.Euler(0f, angle, 0f) * aim * reach * 0.85f));
                }

                break;
            case SkillFxPlace.Ring:
                for (int i = 0; i < count; i++)
                {
                    float angle = i * 360f / count;
                    points.Add(Grounded(origin + Quaternion.Euler(0f, angle, 0f) * aim * reach));
                }

                break;
            case SkillFxPlace.Trail:
                // From where a dash began to where it ended, evenly (one at the end when it didn't move).
                Vector3 start = from ?? origin;
                for (int i = 0; i < count; i++)
                {
                    points.Add(Grounded(Vector3.Lerp(start, origin, count == 1 ? 1f : i / (float)(count - 1))));
                }

                break;
            case SkillFxPlace.Struck:
                if (struck != null)
                {
                    foreach (Vector3 point in struck)
                    {
                        points.Add(point + Vector3.up * 0.9f);
                    }
                }

                break;
        }

        return points;
    }

    // A flat mark sits this far above the ground so the terrain doesn't swallow it.
    private const float GroundLift = 0.06f;
    private static readonly RaycastHit[] GroundHits = new RaycastHit[16];

    /// <summary>
    /// The ground under <paramref name="point"/>: the nearest thing a downward ray meets that isn't a body (the caster, a creature, anything
    /// that can be struck), else the terrain; lifted a little for flat marks. It used to stop on the caster's own capsule, so every mark
    /// cast "on the ground" floated at head height (2026-11-25).
    /// </summary>
    public static Vector3 Grounded(Vector3 point)
    {
        int count = Physics.RaycastNonAlloc(point + Vector3.up * 3f, Vector3.down, GroundHits, 8f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float nearest = float.MaxValue;
        bool found = false;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = GroundHits[i];
            if (hit.distance < nearest && !IsBody(hit.collider))
            {
                nearest = hit.distance;
                point = hit.point;
                found = true;
            }
        }

        Terrain terrain = Terrain.activeTerrain;
        if (!found && terrain != null)
        {
            point.y = terrain.SampleHeight(point) + terrain.transform.position.y;
        }

        point.y += GroundLift;
        return point;
    }

    // Something that walks: the player, a creature, a horse, a resident (all on CharacterControllers or NavMeshAgents), or loose debris.
    // Not every Damageable: buildings have health too, and a dock or a cottage floor is ground.
    private static bool IsBody(Collider collider) =>
        collider is CharacterController || collider.attachedRigidbody != null || collider.GetComponentInParent<Player>() != null
        || collider.GetComponentInParent<UnityEngine.AI.NavMeshAgent>() != null;

    private static IEnumerator SoundLater(SoundDefinition sound, Vector3 at, float delay)
    {
        yield return new WaitForSeconds(delay);
        AudioManager.Play(sound, at);
    }

    private IEnumerator PlayLater(SkillFxCue cue, Vector3 at, Vector3 aim, float delay)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        GameObject effect = Take(cue.prefab);
        effect.transform.SetPositionAndRotation(at, Quaternion.LookRotation(aim, Vector3.up));
        effect.transform.localScale = cue.prefab.transform.localScale * cue.scale;
        effect.SetActive(true);
        foreach (VisualEffect visual in effect.GetComponentsInChildren<VisualEffect>())
        {
            if (visual.TryGetComponent(out VfxValues values))
            {
                values.Apply(visual);
            }

            visual.Reinit();
            visual.Play();
        }

        yield return new WaitForSeconds(cue.seconds);
        Give(effect);
    }

    private GameObject Take(GameObject prefab)
    {
        if (pools.TryGetValue(prefab, out Stack<GameObject> pool))
        {
            while (pool.Count > 0)
            {
                GameObject waiting = pool.Pop();
                if (waiting != null)
                {
                    return waiting;
                }
            }
        }

        GameObject made = Instantiate(prefab, transform);
        made.SetActive(false);
        prefabOf[made] = prefab;
        return made;
    }

    private void Give(GameObject effect)
    {
        if (effect == null || !prefabOf.TryGetValue(effect, out GameObject prefab))
        {
            return;
        }

        effect.SetActive(false);
        if (!pools.TryGetValue(prefab, out Stack<GameObject> pool))
        {
            pools[prefab] = pool = new Stack<GameObject>();
        }

        pool.Push(effect);
    }

    // One light, reused: it flares at the release and fades over its seconds.
    private void Flash(Vector3 at, Color colour, float intensity, float range, float seconds)
    {
        if (flash == null)
        {
            flash = new GameObject("Skill Flash").AddComponent<Light>();
            flash.transform.SetParent(transform, false);
            flash.type = LightType.Point;
            flash.shadows = LightShadows.None;
        }

        flash.transform.position = at;
        flash.color = new Color(colour.r, colour.g, colour.b, 1f);
        flash.range = range;
        flashFrom = intensity;
        flash.intensity = intensity;
        flashSeconds = Mathf.Max(0.05f, seconds);
        flashEnds = Time.time + flashSeconds;
        flash.enabled = true;
    }

    private void Update()
    {
        if (flash != null && flash.enabled)
        {
            float left = flashEnds - Time.time;
            flash.intensity = left > 0f ? flashFrom * Mathf.Clamp01(left / flashSeconds) : 0f;
            flash.enabled = left > 0f;
        }
    }
}
