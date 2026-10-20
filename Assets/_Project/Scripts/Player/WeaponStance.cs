/// <summary>
/// What the player is holding, which selects the combat idle, the attack animation and the arm hold pose.
/// The numbers are written to the Animator's "WeaponStance" parameter, so never renumber a value.
/// </summary>
public enum WeaponStance
{
    Unarmed = 0,
    OneHanded = 1,
    TwoHanded = 2,
    Polearm = 3,
}
