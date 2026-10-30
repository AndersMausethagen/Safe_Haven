/// <summary>
/// Something that can be knocked off balance: it breaks off what it was doing and reels for a while (a parried attack,
/// broken poise). MonsterAI is one; put it on the object with the Combatant.
/// </summary>
public interface IStaggerable
{
    void Stagger(float seconds);
}
