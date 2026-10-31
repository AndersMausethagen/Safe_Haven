using System;
using UnityEngine;

/// <summary>
/// A standing tree taking a knock: it leans over about its foot, swings back past upright and settles, and a hard enough
/// blow leaves it standing a little crooked for good. Added to a tree when it is first hit, and asleep the rest of the time.
/// </summary>
[DisallowMultipleComponent]
public sealed class TreeSway : MonoBehaviour
{
    private TreeSpring spring;
    private Quaternion upright;
    private Action settled;

    /// <summary>Knocks a tree towards a direction, by about this many degrees, leaving it this share of them out of true.</summary>
    public static TreeSway Knock(GameObject tree, Vector3 direction, float degrees, float keeps, Action settled = null)
    {
        if (!tree.TryGetComponent(out TreeSway sway))
        {
            sway = tree.AddComponent<TreeSway>();
            sway.upright = tree.transform.rotation;
        }

        sway.settled = settled ?? sway.settled;
        Vector3 flat = direction;
        flat.y = 0f;
        var towards = new Vector2(flat.x, flat.z);
        towards = towards.sqrMagnitude > 0.0001f ? towards.normalized : Vector2.right;
        sway.spring.Knock(towards, degrees);
        sway.spring.Rest += towards * (degrees * keeps);
        sway.enabled = true;
        return sway;
    }

    private void Update()
    {
        // Short steps, as a hit-stop or a slow frame must not make the spring blow up.
        float seconds = Mathf.Min(Time.deltaTime, 0.05f);
        spring.Step(seconds * 0.5f);
        spring.Step(seconds * 0.5f);
        Apply();
        if (spring.IsStill)
        {
            spring.Lean = spring.Rest;
            Apply();
            enabled = false;
            Action done = settled;
            settled = null;
            done?.Invoke();
        }
    }

    private void Apply()
    {
        float degrees = spring.Lean.magnitude;
        if (degrees < 0.001f)
        {
            transform.rotation = upright;
            return;
        }

        // Lean towards a direction is a turn about the level line across it.
        var towards = new Vector3(spring.Lean.x, 0f, spring.Lean.y) / degrees;
        transform.rotation = Quaternion.AngleAxis(degrees, Vector3.Cross(Vector3.up, towards)) * upright;
    }
}
