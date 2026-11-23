using NUnit.Framework;

/// <summary>
/// Finished work goes to the chests by itself (the user, 2026-11-23: "NPC's should automatically move items from crafting stations output
/// to nearby storage", both: workers as soon as it is worth a trip, and residents in their free time for stations nobody works).
/// </summary>
public sealed class StationHaulingTests
{
    [Test]
    public void AWorker_GoesWhenALoadIsReady_OrTheStationHasStopped()
    {
        Assert.IsTrue(WorkerProduction.WorthATrip(20, 20, false, false), "a full load waits: off they go while it keeps working");
        Assert.IsFalse(WorkerProduction.WorthATrip(5, 20, false, false), "a handful while it works: not yet");
        Assert.IsTrue(WorkerProduction.WorthATrip(5, 20, true, false), "the station done: whatever there is");
        Assert.IsTrue(WorkerProduction.WorthATrip(5, 20, false, true), "the station blocked by its full output");
        Assert.IsFalse(WorkerProduction.WorthATrip(0, 20, true, true), "nothing to carry");
    }
}
