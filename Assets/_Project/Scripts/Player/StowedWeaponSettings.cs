using UnityEngine;

/// <summary>
/// Where a put-away weapon is worn (StowedWeaponView): two-handed weapons, polearms, staffs and bows across the back, a
/// one-handed weapon at the hip. Positions and directions are in the character's own frame (x right, y up, z forward),
/// measured from the bone; the weapon's length runs from its grip along Tip Direction and its flat lies along Flat Normal.
/// On the back the weapon's middle is placed (Back Centre), at the hip its grip (Hip Grip). One asset in Resources.
/// </summary>
[CreateAssetMenu(fileName = "StowedWeaponSettings", menuName = "Safe Haven/Player/Stowed Weapon Settings")]
public sealed class StowedWeaponSettings : ScriptableObject
{
    private const string ResourcePath = "StowedWeaponSettings";

    [Header("On the back (two-handed, polearms, staffs, bows)")]
    [SerializeField] private HumanBodyBones backBone = HumanBodyBones.UpperChest;
    [Tooltip("Where the weapon's middle sits from the bone, in the character's frame: the middle of the back, behind it. The " +
             "middle, not the grip: a bow is held at its middle and a greatsword near its end, and both should lie across the back.")]
    [SerializeField] private Vector3 backCentre = new Vector3(0f, -0.08f, -0.2f);
    [Tooltip("Which way the weapon runs from the grip: down towards the left hip.")]
    [SerializeField] private Vector3 backTipDirection = new Vector3(-0.5f, -0.86f, 0f);
    [Tooltip("The way its flat faces: out from the back.")]
    [SerializeField] private Vector3 backFlatNormal = new Vector3(0f, 0f, -1f);

    [Header("At the hip (one-handed)")]
    [SerializeField] private HumanBodyBones hipBone = HumanBodyBones.Hips;
    [Tooltip("The grip's place from the hips, in the character's frame: at the left hip, a little forward.")]
    [SerializeField] private Vector3 hipGrip = new Vector3(-0.2f, 0.05f, 0.06f);
    [Tooltip("Which way the weapon runs from the grip: down and back.")]
    [SerializeField] private Vector3 hipTipDirection = new Vector3(-0.08f, -0.88f, -0.47f);
    [Tooltip("The way its flat faces: out from the thigh.")]
    [SerializeField] private Vector3 hipFlatNormal = new Vector3(-1f, 0f, 0f);

    private static StowedWeaponSettings cached;

    /// <summary>The asset in Resources, or defaults when there is none.</summary>
    public static StowedWeaponSettings Current
    {
        get
        {
            if (cached == null)
            {
                cached = Resources.Load<StowedWeaponSettings>(ResourcePath);
                if (cached == null)
                {
                    cached = CreateInstance<StowedWeaponSettings>();
                }
            }

            return cached;
        }
    }

    public HumanBodyBones BackBone => backBone;
    public Vector3 BackCentre => backCentre;
    public Vector3 BackTipDirection => backTipDirection;
    public Vector3 BackFlatNormal => backFlatNormal;
    public HumanBodyBones HipBone => hipBone;
    public Vector3 HipGrip => hipGrip;
    public Vector3 HipTipDirection => hipTipDirection;
    public Vector3 HipFlatNormal => hipFlatNormal;

    /// <summary>Whether <paramref name="weapon"/> is worn on the back (true) or at the hip; torches, rods and shields are not worn.</summary>
    public static bool? WornOnBack(WeaponDefinition weapon)
    {
        if (weapon == null || weapon.Kind == WeaponKind.Torch || weapon.Kind == WeaponKind.FishingRod || weapon.IsShield)
        {
            return null;
        }

        // A staff is held in one hand with the other on the shaft (stance One-Handed): its Channel signature marks it.
        return weapon.IsBow || weapon.RequiresBothHands || weapon.Stance == WeaponStance.Polearm || weapon.Signature == WeaponSignature.Channel;
    }
}
