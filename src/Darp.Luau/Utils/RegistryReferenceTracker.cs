using Darp.Luau.Native;
using static Darp.Luau.Native.LuauNative;

namespace Darp.Luau.Utils;

/// <summary> Snapshot of reference/callback tracking counters for a <see cref="LuauState"/>. </summary>
internal readonly record struct LuauMemoryStatistics(
    int ActiveRegistryReferences,
    int CreatedRegistryReferences,
    int ReleasedRegistryReferences,
    int ActiveManagedCallbacks
);

internal sealed class RegistryReferenceTracker(LuauState state)
{
    private readonly LuauState _state = state;
    private readonly Dictionary<int, TrackedReference> _trackedReferences = [];
    private readonly Dictionary<int, List<int>> _callbackFrameOwnedHandles = [];
    private int _nextTrackedReferenceHandle = 1;
    private int _createdRegistryReferenceCount;
    private int _releasedRegistryReferenceCount;

    public LuauMemoryStatistics GetStatistics(int activeManagedCallbacks) =>
        new(
            ActiveRegistryReferences: _trackedReferences.Count,
            CreatedRegistryReferences: _createdRegistryReferenceCount,
            ReleasedRegistryReferences: _releasedRegistryReferenceCount,
            ActiveManagedCallbacks: activeManagedCallbacks
        );

    /// <summary>
    /// Retrieves a reference for a value at the specified stack index in the Lua state and tracks it for future use.
    /// </summary>
    /// <param name="L">A pointer to the Lua state.</param>
    /// <param name="stackIndex">The index of the value on the Lua stack to be referenced and tracked.</param>
    /// <param name="pinned">Indicates whether the reference should be pinned to prevent garbage collection. Defaults to false.</param>
    /// <returns>An integer representing the tracked reference handle.</returns>
    /// <exception cref="InvalidOperationException"> Thrown if the maximum number of tracked registry references has been exceeded. </exception>
    public unsafe int TrackRef(lua_State* L, int stackIndex, bool pinned = false)
    {
        ArgumentNullException.ThrowIfNull(L);
        if ((nint)L != (nint)_state.L)
            throw new InvalidOperationException("Cross-state reference tracking is not allowed.");
        if (_nextTrackedReferenceHandle == int.MaxValue)
            throw new InvalidOperationException("Too many tracked registry references were created for this state.");
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        int luaReference = lua_ref(L, stackIndex);

        int handle = _nextTrackedReferenceHandle;
        _nextTrackedReferenceHandle++;
        int callbackFrameToken = pinned ? 0 : _state.GetCurrentCallbackFrameToken();
        _trackedReferences[handle] = new TrackedReference(luaReference, pinned, callbackFrameToken);
        if (callbackFrameToken != 0)
        {
            if (!_callbackFrameOwnedHandles.TryGetValue(callbackFrameToken, out List<int>? callbackOwnedHandles))
            {
                callbackOwnedHandles = [];
                _callbackFrameOwnedHandles.Add(callbackFrameToken, callbackOwnedHandles);
            }

            callbackOwnedHandles.Add(handle);
        }
        _createdRegistryReferenceCount++;
        return handle;
    }

    public unsafe int TrackAndPopRef(lua_State* L, int stackIndex, bool pinned = false)
    {
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: -1);
#endif
        int handle = TrackRef(L, stackIndex, pinned);
        lua_pop(L, 1);
        return handle;
    }

    public bool HasRegistryReference(int handle) => _trackedReferences.ContainsKey(handle);

    public bool TryResolveLuaRef(int handle, out int luaReference)
    {
        luaReference = 0;
        if (handle is 0)
            return false;
        if (!_trackedReferences.TryGetValue(handle, out TrackedReference trackedReference))
            return false;

        luaReference = trackedReference.LuaReference;
        return true;
    }

    public int ResolveLuaRef(int handle, ReadOnlySpan<char> ownerTypeName)
    {
        if (TryResolveLuaRef(handle, out int luaReference))
            return luaReference;
        throw new ObjectDisposedException(ownerTypeName.ToString(), $"The reference to '{ownerTypeName}' is invalid.");
    }

    public unsafe int CloneTrackedReference(lua_State* L, int handle, string ownerTypeName)
    {
        ArgumentNullException.ThrowIfNull(L);
        if ((nint)L != (nint)_state.L)
            throw new InvalidOperationException("Cross-state reference cloning is not allowed.");
        int oldRef = ResolveLuaRef(handle, ownerTypeName);
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        lua_getref(L, oldRef);
        int newHandle = TrackRef(L, -1);
        lua_pop(L, 1);
        return newHandle;
    }

    public unsafe int CloneTrackedReference(int handle, string ownerTypeName) =>
        CloneTrackedReference(_state.L, handle, ownerTypeName);

    public unsafe void ReleaseRef(int handle)
    {
        if (_state.IsDisposed)
            return;
        if (!_trackedReferences.TryGetValue(handle, out TrackedReference trackedReference))
            return;
        if (trackedReference.IsPinned)
            return;

        _trackedReferences.Remove(handle);
        if (
            trackedReference.CallbackFrameToken != 0
            && _callbackFrameOwnedHandles.TryGetValue(
                trackedReference.CallbackFrameToken,
                out List<int>? callbackOwnedHandles
            )
        )
        {
            callbackOwnedHandles.Remove(handle);
            if (callbackOwnedHandles.Count == 0)
                _callbackFrameOwnedHandles.Remove(trackedReference.CallbackFrameToken);
        }

        _releasedRegistryReferenceCount++;
        int luaReference = trackedReference.LuaReference;

        lua_unref(_state.L, luaReference);
    }

    public void ReleaseCallbackFrameReferences(int callbackFrameToken)
    {
        if (callbackFrameToken == 0)
            return;
        if (!_callbackFrameOwnedHandles.Remove(callbackFrameToken, out List<int>? callbackOwnedHandles))
            return;

        foreach (int handle in callbackOwnedHandles)
            ReleaseRef(handle);
    }

    public void ReleaseAll()
    {
        if (_trackedReferences.Count is 0)
            return;

        _releasedRegistryReferenceCount += _trackedReferences.Count;
        _trackedReferences.Clear();
        _callbackFrameOwnedHandles.Clear();
    }
}

internal readonly record struct TrackedReference(int LuaReference, bool IsPinned, int CallbackFrameToken);
