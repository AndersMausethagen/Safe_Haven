/// <summary>The screens co-op adds while a session runs: the party panel and the downed screen.</summary>
public sealed class CoopHud : ICoopPart
{
    public void Begin(CoopLink link)
    {
        PartyHud.Create();
        DownedHud.Create();
    }

    public void Tick()
    {
    }

    public void FriendLeft(ulong clientId)
    {
    }

    public void End()
    {
        PartyHud.Remove();
        DownedHud.Remove();
    }
}
