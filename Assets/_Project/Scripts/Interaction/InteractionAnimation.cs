/// <summary>
/// Which animation plays while the player works on an Interactable. The numbers are written to the
/// "Interaction" parameter of PlayerAnimator, so never renumber a value; add new ones at the end
/// and add a matching state in the Animator's Interaction layer.
/// </summary>
public enum InteractionAnimation
{
    None = 0,
    /// <summary>Standing pick at chest height: fruit, berries, leaves.</summary>
    GatherHigh = 1,
    /// <summary>Crouched pick: herbs, mushrooms, items on the ground.</summary>
    GatherLow = 2,
    /// <summary>Crouched pick with an alternate motion.</summary>
    GatherLowAlt = 3,
    /// <summary>One-handed swing at a vertical surface: chopping a tree, mining a cave wall.</summary>
    MineWall = 4,
    /// <summary>Overhead swing down at the ground: ore, rocks.</summary>
    MineGround = 5,
    /// <summary>Hammering at the ground: building, crafting. Plays begin, loop, stop.</summary>
    Hammer = 6,
    /// <summary>Pushing a plow. Plays begin, loop, stop.</summary>
    Farm = 7,
    /// <summary>Cast, wait, stop. PlayerAnimationController.SetInteractionStage drives the fight and catch poses.</summary>
    Fish = 8,
    /// <summary>Conversation gestures.</summary>
    Talk = 9,
}
