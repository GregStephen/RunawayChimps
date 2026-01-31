using System;
using System.Collections.Generic;

public static class EconomyState
{
    public static int Coconuts { get; private set; }
    public static IReadOnlyCollection<string> OwnedItemIds => _ownedItemIds;
    private static HashSet<string> _ownedItemIds = new HashSet<string>();

    public static event Action<int, IReadOnlyCollection<string>>? OnChanged;

    public static void Set(int coconuts, HashSet<string> ownedItemIds)
    {
        Coconuts = coconuts;
        _ownedItemIds = ownedItemIds ?? new HashSet<string>();
        OnChanged?.Invoke(Coconuts, _ownedItemIds);
    }
}
