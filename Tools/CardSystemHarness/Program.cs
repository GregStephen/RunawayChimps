using System;

// Executes the actual engine-independent production class, not a translated model
// or a substring check. Does NOT simulate Unity, PhysX, XRI or Photon.
internal static class Program
{
    private static int assertions;
    private static void Check(bool result, string description)
    {
        assertions++;
        if (!result) throw new InvalidOperationException(description);
    }

    private sealed class EqualLookingLock
    {
        public override bool Equals(object value) => value is EqualLookingLock;
        public override int GetHashCode() => 1;
    }

    private static void Main()
    {
        object a = new EqualLookingLock(), b = new EqualLookingLock();
        var state = new CardConsumptionState();
        Check(!state.Commit(a), "Unreserved card committed");
        Check(!state.TryBegin(null), "Null objective acquired the transaction");
        Check(!state.IsPendingFor(null), "Null objective matched no reservation");
        Check(state.TryBegin(a), "First reservation rejected");
        Check(!state.TryBegin(a) && !state.TryBegin(b), "Reentrant submission entered");
        Check(state.IsPendingFor(a) && !state.IsPendingFor(b), "Value-equal locks shared identity");
        Check(!state.Commit(b), "Wrong lock committed");
        state.End(b);
        Check(state.IsPendingFor(a), "Wrong lock released a reservation");
        state.End(a);
        Check(!state.IsConsumed && !state.IsPendingFor(a), "Rejected attempt consumed a card");
        Check(state.TryBegin(b), "Rejected card could not retry elsewhere");
        Check(state.Commit(b), "Valid reserved card could not commit");
        Check(state.IsConsumed, "Consumption was not visible before progress notification");
        Check(!state.TryBegin(a) && !state.Commit(a) && !state.Commit(b), "Committed card credited twice");
        state.End(b);
        Check(state.IsConsumed && !state.TryBegin(a), "Ending a transaction unconsumed a card");
        var second = new CardConsumptionState();
        Check(second.TryBegin(a) && second.Commit(a), "Independent card shared consumed state");

        for (int card = 0; card < 1000; card++)
        {
            state = new CardConsumptionState();
            // Model calls surrounding failed guards and listener re-entry, executing
            // production state at every step. Each card succeeds exactly once.
            for (int retry = 0; retry < 4; retry++)
            {
                Check(state.TryBegin(a), "Failed guard prevented another reservation");
                Check(!state.TryBegin(b), "Second lock entered during an attempt");
                state.End(a);
                Check(!state.IsConsumed, "Failed guard consumed the card");
            }
            Check(state.TryBegin(a) && state.Commit(a), "Final accepted attempt failed");
            for (int listener = 0; listener < 4; listener++)
                Check(state.IsConsumed && !state.TryBegin(b) && !state.Commit(b), "Listener double-credit");
            state.End(a);
            Check(state.IsConsumed && !state.TryBegin(b), "Delayed duplicate credited");
        }
        Console.WriteLine($"PASS: {assertions} assertions against production CardConsumptionState (1000 repeated retry/consume sequences).");
        Console.WriteLine("MANAGED STATE ONLY: Unity, XRI, PhysX, Photon and headset tests remain separate.");
    }
}
