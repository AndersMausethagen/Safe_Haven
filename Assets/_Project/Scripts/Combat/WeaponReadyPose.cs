/// <summary>
/// The pose a weapon stands ready and guards in (PlayerAnimator.controller's ReadyPose parameter: Combat Idle and the guard trees).
/// Stance uses the stance's own (the value is the stance); the others are a weapon's own pose.
/// </summary>
public enum WeaponReadyPose
{
    /// <summary>The stance's own ready pose.</summary>
    Stance = 0,
    /// <summary>A staff held upright (Spell_Staff_Ready), where its spells start.</summary>
    Staff = 1,
}
