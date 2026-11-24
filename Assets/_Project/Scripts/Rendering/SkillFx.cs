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

    /// <summary>The cast: its cues round the caster, and its area while it winds up.</summary>
    public static void Cast(SkillDefinition skill, Transform caster, Vector3 aim, float windUp)
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
            Vector3 lands = caster.position + (flat.sqrMagnitude > 0.0001f ? flat.normalized : Vector3.zero) * skill.LungeDistance;
            SkillAreaMark.Show(lands, aim, skill.Attack.Range, skill.Attack.Arc, SkillSchools.ColorOf(skill.School), windUp);
        }

        Runner.PlayAll(look.cast, caster.position, aim, skill, null);
    }

    /// <summary>The release: its cues over the attack's reach and arc, on what it struck, a flash and a shake.</summary>
    public static void Release(SkillDefinition skill, Vector3 origin, Vector3 aim, IReadOnlyList<Vector3> struck)
    {
        if (skill == null || skill.Look == null)
        {
            return;
        }

        SkillLook look = skill.Look;
        SkillFx fx = Runner;
        fx.PlayAll(look.release, origin, aim, skill, null);
        fx.PlayAll(look.struck, origin, aim, skill, struck);
        if (look.light.a > 0f && look.lightIntensity > 0f)
        {
            fx.Flash(origin + Vector3.up * 1.2f, look.light, look.lightIntensity, look.lightRange, look.lightSeconds);
        }

        if (look.shake > 0f)
        {
            CameraShake.Add(look.shake);
        }
    }

    private void PlayAll(List<SkillFxCue> cues, Vector3 origin, Vector3 aim, SkillDefinition skill, IReadOnlyList<Vector3> struck)
    {
        aim.y = 0f;
        aim = aim.sqrMagnitude > 0.0001f ? aim.normalized : Vector3.forward;
        float reach = skill.Attack != null ? skill.Attack.Range : 2f;
        float arc = skill.Attack != null ? skill.Attack.Arc : 60f;
        foreach (SkillFxCue cue in cues)
        {
            if (cue == null || cue.prefab == null)
            {
                continue;
            }

            List<Vector3> points = Points(cue, origin, aim, reach, arc, struck);
            for (int i = 0; i < points.Count; i++)
            {
                StartCoroutine(PlayLater(cue, points[i], aim, cue.delay + cue.stepDelay * i));
            }
        }
    }

    /// <summary>Where a cue's effects go (public for tests): its place over the caster, the reach, the arc or what was struck.</summary>
    public static List<Vector3> Points(SkillFxCue cue, Vector3 origin, Vector3 aim, float reach, float arc, IReadOnlyList<Vector3> struck)
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

    /// <summary>The ground under <paramref name="point"/>: what a downward ray meets, else the terrain.</summary>
    public static Vector3 Grounded(Vector3 point)
    {
        if (Physics.Raycast(point + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 8f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            return hit.point;
        }

        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null)
        {
            point.y = terrain.SampleHeight(point) + terrain.transform.position.y;
        }

        return point;
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
