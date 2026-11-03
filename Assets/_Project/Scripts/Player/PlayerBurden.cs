using UnityEngine;

/// <summary>
/// What a heavy bag does (GDD §12.4): past a share of its weight budget the player slows down, tires faster and is heard further,
/// easing in up to a full bag, so "one more log" is a choice and not a wall. A haul sack adds its own rattle and drag on top. The
/// numbers are on Player Movement Settings › Carrying and the backpack's item, the rule is CarryLoad.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ItemInventory))]
public sealed class PlayerBurden : MonoBehaviour
{
    private const float CheckSeconds = 0.25f;

    private ItemInventory items;
    private PlayerMovement movement;
    private PlayerStamina stamina;
    private BackpackCapacity backpack;
    private float nextCheckAt;
    private bool warned;

    /// <summary>How far past the heavy mark the bag is: 0 at or under it, 1 when full.</summary>
    public float Over { get; private set; }

    public bool IsHeavy => Over > 0f;

    /// <summary>How far footsteps and the like carry, times normal.</summary>
    public float NoiseScale { get; private set; } = 1f;

    private void Awake()
    {
        items = GetComponent<ItemInventory>();
        movement = GetComponent<PlayerMovement>();
        stamina = GetComponent<PlayerStamina>();
        backpack = GetComponent<BackpackCapacity>();
    }

    private void OnDisable()
    {
        if (movement != null)
        {
            movement.SetSpeedScale(this, 1f);
        }

        if (stamina != null)
        {
            stamina.CostScale = 1f;
        }
    }

    private void Update()
    {
        if (Time.time < nextCheckAt || movement == null)
        {
            return;
        }

        nextCheckAt = Time.time + CheckSeconds;
        PlayerMovementSettings settings = movement.Settings;
        ItemContainer bag = items.Container;
        Over = bag != null && bag.HasWeightLimit ? CarryLoad.Over(bag.CurrentWeight, bag.WeightCapacity, settings.HeavyFromShare) : 0f;
        ItemDefinition pack = backpack != null ? backpack.Applied : null;
        movement.SetSpeedScale(this, CarryLoad.Scale(Over, settings.SpeedAtFullBag) * (pack != null ? pack.CarrySpeedScale : 1f));
        NoiseScale = CarryLoad.Scale(Over, settings.NoiseAtFullBag);
        if (stamina != null)
        {
            stamina.CostScale = CarryLoad.Scale(Over, settings.StaminaAtFullBag);
        }

        if (IsHeavy && !warned)
        {
            GameMessages.Post("Heavy load: you slow down and tire faster.", MessageKind.Warning);
        }

        warned = IsHeavy;
    }

    /// <summary>What a step adds to the noise it makes, from a pack that rattles. Called for each step, and is heard then too.</summary>
    public float StepNoise(bool sneaking)
    {
        ItemDefinition pack = backpack != null ? backpack.Applied : null;
        if (pack == null || pack.CarryNoise <= 0f)
        {
            return 0f;
        }

        AudioManager.Play(pack.CarrySound, transform.position + Vector3.up);
        return sneaking ? pack.CarryNoise * 0.5f : pack.CarryNoise;
    }
}

/// <summary>The rule of a heavy bag, without a scene.</summary>
public static class CarryLoad
{
    /// <summary>How far past the heavy mark a bag is: 0 at or under it, 1 when full.</summary>
    public static float Over(float weight, float capacity, float heavyFromShare) =>
        capacity <= 0f ? 0f : Mathf.InverseLerp(heavyFromShare, 1f, weight / capacity);

    /// <summary>A number that is 1 under the mark and <paramref name="atFull"/> with a full bag.</summary>
    public static float Scale(float over, float atFull) => Mathf.Lerp(1f, atFull, over);
}
