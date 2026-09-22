/// <summary>
/// Single-use, main-thread transaction state. Independent of Unity so the real
/// consumption rules can be executed in CI, not just checked as source strings.
/// A failed attempt can retry. A committed card can never credit another lock.
/// This is visit-local state, not saved inventory or a network authority.
/// </summary>
internal sealed class CardConsumptionState
{
    private object pendingTarget;
    public bool IsConsumed { get; private set; }

    public bool TryBegin(object target)
    {
        if (target == null || IsConsumed || pendingTarget != null)
            return false;
        pendingTarget = target;
        return true;
    }

    public bool IsPendingFor(object target)
    {
        return pendingTarget != null && object.ReferenceEquals(pendingTarget, target);
    }

    public bool Commit(object target)
    {
        if (IsConsumed || !IsPendingFor(target))
            return false;
        IsConsumed = true;
        return true;
    }

    public void End(object target)
    {
        if (IsPendingFor(target))
            pendingTarget = null;
    }
}
