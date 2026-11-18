/// <summary>
/// The owner of a lease held for a static class or a moment rather than an object (invulnerability, a hold on the player's controls, a
/// speed): a key with a name. LeaseWatch tells a lease held too long by its holder's name, and a plain <c>new object()</c> was told as
/// "Object", so 80 of the playtests' Stuck rows could not say what held them (SharedStateTests).
/// </summary>
public sealed class LeaseKey
{
    private readonly string name;

    public LeaseKey(string name)
    {
        this.name = string.IsNullOrWhiteSpace(name) ? "a lease" : name;
    }

    public override string ToString() => name;
}
