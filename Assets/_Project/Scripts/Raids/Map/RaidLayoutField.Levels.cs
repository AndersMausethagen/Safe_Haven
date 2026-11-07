using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The levels clearings and trails stand on: each stamped along its trail and room, blended where they meet, and the
/// open land filled from the nearest two. Part of RaidLayoutField, in its own file.
/// </summary>
public sealed partial class RaidLayoutField
{
    // Stamped this far; beyond, the nearest two places spread outward cell by cell (much cheaper than stamping the whole map).
    private const float LevelReach = 26f;

    private const float LevelBlend = 9f;

    private const float CliffFrom = 2.5f;

    private const float CliffFull = 4.5f;

    // Every trail, spur, clearing and pocket has a height: a clearing its level's, a trail climbing from one end's to the other's (a ramp
    // that eases in and out between the clearings' edges), a spur or pocket the height where it starts. The wild ground takes the height
    // of the nearest. Between two nearby places of about the same height it blends, so ramps leave no ledges; where they differ by a level
    // it doesn't, and that edge is a cliff.
    private void BuildLevels(float halfWidth)
    {
        nearDistance = Filled(Far);
        otherDistance = Filled(Far);
        nearHeight = new float[resolution * resolution];
        otherHeight = new float[resolution * resolution];
        nearId = new int[resolution * resolution];
        otherId = new int[resolution * resolution];
        for (int i = 0; i < nearId.Length; i++)
        {
            nearId[i] = -1;
            otherId[i] = -1;
        }

        float step = plan.TerraceHeight;
        int id = 0;
        for (int i = 0; i < plan.Trails.Count && i < plan.Links.Count; i++, id++)
        {
            List<Vector2> trail = plan.Trails[i];
            RaidLayoutRoom from = plan.Rooms[plan.Links[i].a];
            RaidLayoutRoom to = plan.Rooms[plan.Links[i].b];
            float length = 0f;
            for (int k = 0; k + 1 < trail.Count; k++)
            {
                length += Vector2.Distance(trail[k], trail[k + 1]);
            }

            float low = from.Level * step, high = to.Level * step;
            float walk = Mathf.Max(0.01f, length - from.Radius - to.Radius);
            float start = from.Radius;
            float Ramp(float at) => Mathf.Lerp(low, high, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((at - start) / walk)));
            float run = 0f;
            for (int k = 0; k + 1 < trail.Count; k++)
            {
                float next = run + Vector2.Distance(trail[k], trail[k + 1]);
                StampLevel(trail[k], trail[k + 1], halfWidth, run, next, Ramp, id);
                run = next;
            }
        }

        foreach (List<Vector2> spur in plan.Spurs)
        {
            float height = NearestHeight(spur[0]);
            for (int k = 0; k + 1 < spur.Count; k++)
            {
                StampLevel(spur[k], spur[k + 1], halfWidth * SpurWidth, 0f, 0f, _ => height, id);
            }

            id++;
        }

        foreach (RaidLayoutRoom room in plan.Rooms)
        {
            float height = room.Level * step;
            StampLevel(room.Position, room.Position, room.Radius, 0f, 0f, _ => height, id++);
        }

        foreach ((Vector2 centre, float radius) in plan.Pockets)
        {
            float height = NearestHeight(centre);
            StampLevel(centre, centre, radius, 0f, 0f, _ => height, id++);
        }

        FillLevels();
        levelHeight = new float[resolution * resolution];
        for (int i = 0; i < levelHeight.Length; i++)
        {
            levelHeight[i] = BlendedLevel(i);
        }

        nearDistance = nearHeight = otherDistance = otherHeight = null;
        nearId = otherId = null;
    }

    private float NearestHeight(Vector2 point)
    {
        int x = Mathf.Clamp(Mathf.RoundToInt(point.x / cell), 0, resolution - 1);
        int y = Mathf.Clamp(Mathf.RoundToInt(point.y / cell), 0, resolution - 1);
        int index = y * resolution + x;
        return nearId[index] >= 0 ? BlendedLevel(index) : 0f;
    }

    // Like Stamp, but keeps the nearest two different places at every cell, with the height the place has at its nearest point.
    private void StampLevel(Vector2 from, Vector2 to, float radius, float runFrom, float runTo, System.Func<float, float> heightAt, int id)
    {
        float extent = radius + LevelReach;
        int xMin = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(from.x, to.x) - extent) / cell));
        int xMax = Mathf.Min(resolution - 1, Mathf.CeilToInt((Mathf.Max(from.x, to.x) + extent) / cell));
        int yMin = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(from.y, to.y) - extent) / cell));
        int yMax = Mathf.Min(resolution - 1, Mathf.CeilToInt((Mathf.Max(from.y, to.y) + extent) / cell));
        Vector2 along = to - from;
        float lengthSquared = Mathf.Max(0.0001f, along.sqrMagnitude);
        for (int y = yMin; y <= yMax; y++)
        {
            for (int x = xMin; x <= xMax; x++)
            {
                var point = new Vector2(x * cell, y * cell);
                float t = Mathf.Clamp01(Vector2.Dot(point - from, along) / lengthSquared);
                float distance = Vector2.Distance(point, from + along * t) - radius;
                int index = y * resolution + x;
                if (id == nearId[index])
                {
                    if (distance < nearDistance[index])
                    {
                        nearDistance[index] = distance;
                        nearHeight[index] = heightAt(Mathf.Lerp(runFrom, runTo, t));
                    }
                }
                else if (distance < nearDistance[index])
                {
                    otherDistance[index] = nearDistance[index];
                    otherHeight[index] = nearHeight[index];
                    otherId[index] = nearId[index];
                    nearDistance[index] = distance;
                    nearHeight[index] = heightAt(Mathf.Lerp(runFrom, runTo, t));
                    nearId[index] = id;
                }
                else if (distance < otherDistance[index])
                {
                    otherDistance[index] = distance;
                    otherHeight[index] = heightAt(Mathf.Lerp(runFrom, runTo, t));
                    otherId[index] = id;
                }
            }
        }
    }

    private float BlendedLevel(int index)
    {
        if (otherId[index] < 0 || otherDistance[index] >= Far)
        {
            return nearHeight[index];
        }

        float rise = Mathf.Abs(nearHeight[index] - otherHeight[index]);
        float width = Mathf.Lerp(LevelBlend, 0.05f, Mathf.InverseLerp(CliffFrom, CliffFull, rise));
        float near = Mathf.SmoothStep(0f, 1f, 0.5f + (otherDistance[index] - nearDistance[index]) / width);
        return Mathf.Lerp(otherHeight[index], nearHeight[index], near);
    }

    // Beyond the stamps' reach, the nearest two places spread outward cell by cell, a step's length further each time (diagonals 1.41),
    // so deep wild ground still knows both and blends between them. Only shorter distances replace longer, so it settles.
    private void FillLevels()
    {
        var open = new Queue<int>();
        for (int i = 0; i < nearId.Length; i++)
        {
            if (nearId[i] >= 0)
            {
                open.Enqueue(i);
            }
        }

        float diagonal = cell * 1.41421f;
        while (open.Count > 0)
        {
            int index = open.Dequeue();
            int x = index % resolution, y = index / resolution;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if ((dx == 0 && dy == 0) || nx < 0 || ny < 0 || nx >= resolution || ny >= resolution)
                    {
                        continue;
                    }

                    int next = ny * resolution + nx;
                    float step = dx != 0 && dy != 0 ? diagonal : cell;
                    bool changed = Offer(next, nearDistance[index] + step, nearHeight[index], nearId[index]);
                    if (otherId[index] >= 0)
                    {
                        changed |= Offer(next, otherDistance[index] + step, otherHeight[index], otherId[index]);
                    }

                    if (changed)
                    {
                        open.Enqueue(next);
                    }
                }
            }
        }
    }

    // Keeps the nearest two different places at a cell; true when either changed.
    private bool Offer(int index, float distance, float height, int id)
    {
        if (id == nearId[index])
        {
            if (distance < nearDistance[index] - 0.001f)
            {
                nearDistance[index] = distance;
                nearHeight[index] = height;
                return true;
            }

            return false;
        }

        if (distance < nearDistance[index])
        {
            otherDistance[index] = nearDistance[index];
            otherHeight[index] = nearHeight[index];
            otherId[index] = nearId[index];
            nearDistance[index] = distance;
            nearHeight[index] = height;
            nearId[index] = id;
            return true;
        }

        if (id == otherId[index])
        {
            if (distance < otherDistance[index] - 0.001f)
            {
                otherDistance[index] = distance;
                otherHeight[index] = height;
                return true;
            }

            return false;
        }

        if (distance < otherDistance[index])
        {
            otherDistance[index] = distance;
            otherHeight[index] = height;
            otherId[index] = id;
            return true;
        }

        return false;
    }
}
