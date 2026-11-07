using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Outlines nearby enemies through foliage: white while they haven't noticed the player's side, red while they chase or
/// attack it. The bow's own target outline (ArcheryTargetOutline) replaces this one on the monster it aims at.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyVisibility : MonoBehaviour
{
    private const float Range = 35f;
    private static readonly Color HostileColor = new Color(0.95f, 0.16f, 0.1f, 1f);

    private sealed class Tracked
    {
        public List<OutlineCopies.Entry> Copies;
        public bool Hostile;
    }

    private readonly Dictionary<Damageable, Tracked> outlines = new Dictionary<Damageable, Tracked>();
    private readonly List<Damageable> gone = new List<Damageable>();
    private Material outlineMaterial;
    private Material hostileMaterial;
    private Material maskMaterial;
    private Player archer;
    private PlayerArchery archery;

    private void Awake()
    {
        Shader outline = Shader.Find("Safe Haven/Interaction Outline");
        Shader mask = Shader.Find("Safe Haven/Interaction Outline Mask");
        if (outline == null || mask == null)
        {
            Debug.LogError("EnemyVisibility requires the interaction outline shaders.", this);
            enabled = false;
            return;
        }

        outlineMaterial = new Material(outline) { name = "Enemy Visibility Outline" };
        outlineMaterial.SetColor("_OutlineColor", Color.white);
        outlineMaterial.SetFloat("_ZTest", (float)CompareFunction.Always);
        hostileMaterial = new Material(outlineMaterial) { name = "Enemy Visibility Outline (Hostile)" };
        hostileMaterial.SetColor("_OutlineColor", HostileColor);
        maskMaterial = new Material(mask) { name = "Enemy Visibility Mask" };
        maskMaterial.SetFloat("_ZTest", (float)CompareFunction.Always);
    }

    private void LateUpdate()
    {
        Player player = Player.Current;
        bool show = player != null && !GamePause.IsPaused
            && (UIStateManager.Instance == null || !UIStateManager.Instance.IsAnyWindowOpen);
        float rangeSquared = Range * Range;
        Damageable bowTarget = null;
        if (player != archer)
        {
            archer = player;
            archery = player != null ? player.GetComponent<PlayerArchery>() : null;
        }

        if (archery != null)
        {
            bowTarget = archery.Target;
        }

        foreach (MonsterAI monster in MonsterAI.Active)
        {
            if (monster == null || !monster.TryGetComponent(out Damageable health))
            {
                continue;
            }

            if (!outlines.TryGetValue(health, out Tracked tracked))
            {
                tracked = new Tracked
                {
                    Copies = OutlineCopies.Create(health.transform, outlineMaterial, maskMaterial, "Enemy Visibility Outline", "Enemy Visibility Mask"),
                };
                outlines.Add(health, tracked);
            }

            bool hostile = monster.IsHuntingPlayerSide;
            if (hostile != tracked.Hostile)
            {
                tracked.Hostile = hostile;
                OutlineCopies.SetOutlineMaterial(tracked.Copies, hostile ? hostileMaterial : outlineMaterial);
            }

            bool inRange = player != null && (health.transform.position - player.transform.position).sqrMagnitude <= rangeSquared;
            OutlineCopies.SetShown(tracked.Copies, show && inRange && health.IsAlive && health != bowTarget);
        }

        ForgetGone();
    }

    private void OnDisable()
    {
        foreach (Tracked tracked in outlines.Values)
        {
            OutlineCopies.SetShown(tracked.Copies, false);
        }
    }

    private void OnDestroy()
    {
        foreach (Tracked tracked in outlines.Values)
        {
            OutlineCopies.Destroy(tracked.Copies);
        }

        outlines.Clear();
        if (outlineMaterial != null)
        {
            Destroy(outlineMaterial);
        }

        if (hostileMaterial != null)
        {
            Destroy(hostileMaterial);
        }

        if (maskMaterial != null)
        {
            Destroy(maskMaterial);
        }
    }

    private void ForgetGone()
    {
        gone.Clear();
        foreach (Damageable key in outlines.Keys)
        {
            if (key == null)
            {
                gone.Add(key);
            }
        }

        foreach (Damageable key in gone)
        {
            outlines.Remove(key);
        }
    }
}
