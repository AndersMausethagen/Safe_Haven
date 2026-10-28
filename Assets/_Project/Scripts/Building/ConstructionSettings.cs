using UnityEngine;

/// <summary>
/// How putting up and upgrading buildings looks and sounds (Construction): the scaffold's wood, the dressing that each
/// level adds (stone plinth and lanterns, then banners and a flower box), the dust of each hammer blow and the pop when
/// it is done. Lives at Resources/ConstructionSettings; built-in defaults apply when the asset is missing.
/// </summary>
[CreateAssetMenu(menuName = "Safe Haven/Settings/Construction Settings", fileName = "ConstructionSettings")]
public sealed class ConstructionSettings : GameSettings<ConstructionSettings>
{
    [Header("Look")]
    [SerializeField] private Material scaffoldMaterial;
    [SerializeField] private Material stoneMaterial;
    [SerializeField] private Material clothMaterial;
    [SerializeField] private Material lanternMaterial;
    [SerializeField] private Material dustMaterial;
    [SerializeField] private Color lanternLight = new Color(1f, 0.72f, 0.4f);
    [Tooltip("A ruin (or a building still being raised) shows this much darker.")]
    [SerializeField, Range(0f, 1f)] private float ruinShade = 0.45f;

    [Header("Feel")]
    [SerializeField] private SoundDefinition hammerSound;
    [SerializeField] private SoundDefinition finishSound;
    [Tooltip("Seconds between hammer blows while the player works.")]
    [SerializeField, Min(0.1f)] private float blowInterval = 0.55f;
    [SerializeField, Range(0f, 1f)] private float blowShake = 0.05f;
    [SerializeField, Range(0f, 1f)] private float finishShake = 0.18f;
    [Tooltip("How much bigger it pops when it is finished, before settling.")]
    [SerializeField, Range(1f, 1.5f)] private float finishPop = 1.12f;
    [Tooltip("Work for repairing a ruin when its kind stands at once.")]
    [SerializeField, Min(0.5f)] private float repairWork = 8f;

    public Material ScaffoldMaterial => scaffoldMaterial;
    public Material StoneMaterial => stoneMaterial;
    public Material ClothMaterial => clothMaterial;
    public Material LanternMaterial => lanternMaterial;
    public Material DustMaterial => dustMaterial;
    public Color LanternLight => lanternLight;
    public float RuinShade => ruinShade;
    public SoundDefinition HammerSound => hammerSound;
    public SoundDefinition FinishSound => finishSound;
    public float BlowInterval => blowInterval;
    public float BlowShake => blowShake;
    public float FinishShake => finishShake;
    public float FinishPop => finishPop;
    public float RepairWork => repairWork;
}
