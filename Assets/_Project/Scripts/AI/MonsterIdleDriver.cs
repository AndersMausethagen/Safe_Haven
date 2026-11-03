using System;
using UnityEngine;

/// <summary>
/// Carries out a calm creature's idle life (MonsterIdleLife decides, this moves and shows it): walking off to a spot, grazing, sleeping, and how much it
/// notices meanwhile. A plain C# part of MonsterAI, which says when it is calm.
/// </summary>
public sealed class MonsterIdleDriver
{
    private readonly Transform self;
    private readonly MonsterDefinition definition;
    private readonly MonsterIdleLife life;
    private readonly MonsterMovement movement;
    private readonly MonsterSenses senses;
    private readonly MonsterPose pose;
    private readonly Action changed;

    public MonsterIdleDriver(Transform self, MonsterDefinition definition, MonsterIdleLife life, MonsterMovement movement, MonsterSenses senses, MonsterPose pose, Action changed)
    {
        this.self = self;
        this.definition = definition;
        this.life = life;
        this.movement = movement;
        this.senses = senses;
        this.pose = pose;
        this.changed = changed;
    }

    /// <summary>One frame of it, round its home.</summary>
    public void Tick(Vector3 home)
    {
        MonsterIdleLife.Doing before = life.Now;
        if (life.Now == MonsterIdleLife.Doing.Walking)
        {
            if (life.HasArrived(self.position))
            {
                movement.Stop();
                life.Settle();
            }
        }
        else if (Time.time >= life.Until)
        {
            Vector3 point = MonsterIdleLife.WanderPoint(home, definition.IdleWanderRadius, UnityEngine.Random.value, UnityEngine.Random.value);
            if (definition.IdleWanderRadius > 0f && movement.TryFindGround(point, 2f, out Vector3 ground))
            {
                life.Walk(ground);
                movement.RepathNow();
                movement.MoveTo(ground);
            }
            else
            {
                life.Settle();
            }
        }

        if (life.Now != before)
        {
            Show();
        }
    }

    /// <summary>Puts what it is doing on the body and in its senses, as when it is shaken out of it.</summary>
    public void Show()
    {
        senses.Attention = life.Attention;
        if (pose != null)
        {
            pose.Rest = life.Now == MonsterIdleLife.Doing.Sleeping ? 1f : life.Now == MonsterIdleLife.Doing.Grazing ? 0.5f : 0f;
        }

        changed?.Invoke();
    }
}
