using UnityEngine;

/// <summary>
/// What falls from a tree's crown when it is struck or felled, when the standard leaf fall in a guessed colour doesn't
/// suit it: needle sprigs from a cedar, broad leaves from a maple, in the colour of its own foliage. Read by TreeParts.
/// </summary>
[DisallowMultipleComponent]
public sealed class TreeLeafLook : MonoBehaviour
{
    [Tooltip("The particle effect played from the crown. Empty: Tree Chopping Settings › Leaf Fall.")]
    [SerializeField] private GameObject leafFall;
    [Tooltip("Colour of what falls: the average of the tree's foliage texture.")]
    [SerializeField] private Color colour = new Color(0.31f, 0.32f, 0.2f);

    public GameObject LeafFall => leafFall;
    public Color Colour => colour;

    public void Configure(GameObject fall, Color tint)
    {
        leafFall = fall;
        colour = tint;
    }
}
