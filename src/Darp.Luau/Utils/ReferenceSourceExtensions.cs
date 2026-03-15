using System.Diagnostics.CodeAnalysis;

namespace Darp.Luau.Utils;

internal static class ReferenceSourceExtensions
{
    public static LuauState Validate<T>([NotNull] this T? source)
        where T : IReferenceSource, allows ref struct
    {
        if (source is null)
            throw new ArgumentNullException(nameof(source));
        return source.ValidateInternal();
    }

    public static bool IsReferenceValid([NotNullWhen(true)] this LuauState? state, ulong handle)
    {
        return state is not null && state.ReferenceTracker.HasRegistryReference(handle);
    }

    public static RegistryReferenceTracker.TrackedReference GetTrackedReferenceOrThrow(
        [NotNull] this LuauState? state,
        ulong handle
    )
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.ReferenceTracker.TryGetTrackedReference(handle, out var trackedReference))
            return trackedReference;
        throw new ObjectDisposedException("Tried to get a reference that was not tracked.");
    }

    public static bool TryGetTrackedReference(
        [NotNullWhen(true)] this LuauState? state,
        ulong handle,
        [NotNullWhen(true)] out RegistryReferenceTracker.TrackedReference? trackedReference
    )
    {
        if (state is not null)
            return state.ReferenceTracker.TryGetTrackedReference(handle, out trackedReference);
        trackedReference = null;
        return false;
    }

    public static unsafe ulong ToOwnedHandle<T>(scoped in T source)
        where T : IReferenceSource, allows ref struct
    {
        LuauState state = source.Validate();
        using PopDisposable _ = source.PushToStack(out int stackIndex);
        return state.ReferenceTracker.TrackRef(state.L, stackIndex);
    }
}
