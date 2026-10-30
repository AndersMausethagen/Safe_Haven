/// <summary>
/// The sounds a kind of fighter or structure makes when hit and when it falls (MonsterDefinition, BuildingDefinition).
/// Reached through the object's <see cref="IEventSubject"/>, so audio and hit feedback needn't know what it is. Null
/// leaves the default for its kind.
/// </summary>
public interface ICombatSounds
{
    SoundDefinition HurtSound { get; }
    SoundDefinition DeathSound { get; }
}
