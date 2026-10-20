using UnityEngine;

/// <summary>
/// The sound this object makes when it is killed or destroyed, overriding the sound from its monster or building data
/// and the game-wide default in Audio Settings. Leave Sound empty and tick Silent for things that should die quietly.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Damageable))]
public sealed class DeathSound : MonoBehaviour
{
    [SerializeField] private SoundDefinition sound;
    [Tooltip("Plays nothing at all, not even the defaults.")]
    [SerializeField] private bool silent;

    public SoundDefinition Sound => sound;
    public bool Silent => silent;
}
