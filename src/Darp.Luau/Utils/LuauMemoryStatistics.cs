namespace Darp.Luau.Utils;

/// <summary> Snapshot of reference/callback tracking counters for a <see cref="LuauState"/>. </summary>
internal readonly record struct LuauMemoryStatistics(
    ulong ActiveRegistryReferences,
    ulong CreatedRegistryReferences,
    ulong ReleasedRegistryReferences,
    int ActiveManagedCallbacks
);
