using System;

/// <summary>
/// Marks a Quest Event (GameEventDefinition) field that game code raises, such as a traveler's "freed" event or a job's
/// "assigned" event. The Content Editor finds these fields automatically, so it knows the event is raised and lists the
/// scene object or data asset as a "Raises" use. Put it on every new Quest Event field your code raises.
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class RaisedByGameAttribute : Attribute
{
}
