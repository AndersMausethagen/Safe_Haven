using UnityEngine;

/// <summary>
/// Footsteps a big creature can be heard and felt by before it is seen: a sound every stride it walks, and a camera
/// shake that grows as it comes closer to the player. Presentation only (monsters don't hear each other). Put it on
/// heavy monsters such as trolls.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MonsterAI))]
public sealed class CreatureFootfalls : MonoBehaviour
{
    [Tooltip("Played each stride. Make it positional with a long audible distance.")]
    [SerializeField] private SoundDefinition stepSound;
    [Tooltip("Metres walked per step sound.")]
    [SerializeField, Min(0.2f)] private float strideMeters = 2.4f;
    [Tooltip("Camera shake per step when the player is right beside it; it fades to nothing at Felt Distance.")]
    [SerializeField, Min(0f)] private float shake = 0.25f;
    [SerializeField, Min(1f)] private float feltDistance = 28f;

    private MonsterAI monster;
    private float travelled;

    private void Awake()
    {
        monster = GetComponent<MonsterAI>();
    }

    private void Update()
    {
        if (!monster.enabled || monster.CurrentState == MonsterAI.State.Dead)
        {
            return;
        }

        travelled += monster.Speed * Time.deltaTime;
        if (travelled < strideMeters)
        {
            return;
        }

        travelled = 0f;
        AudioManager.Play(stepSound, transform.position);
        Player player = Player.Current;
        if (player == null || shake <= 0f)
        {
            return;
        }

        float distance = Vector3.Distance(player.transform.position, transform.position);
        float felt = 1f - Mathf.Clamp01(distance / feltDistance);
        if (felt > 0f)
        {
            CameraShake.Add(shake * felt * felt);
        }
    }
}
