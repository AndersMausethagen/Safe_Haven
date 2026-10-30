using Object = UnityEngine.Object;

/// <summary>
/// The definition game events about this object report as their Subject: a dead monster's MonsterDefinition, a fallen
/// building's BuildingDefinition. Put it on the object with the Damageable; without one, its death reports no subject.
/// </summary>
public interface IEventSubject
{
    Object EventSubject { get; }
}
