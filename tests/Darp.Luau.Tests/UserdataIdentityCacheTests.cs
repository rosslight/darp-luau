using Darp.Luau.Tests.Fixtures;
using Shouldly;

namespace Darp.Luau.Tests;

public sealed class UserdataIdentityCacheTests
{
    [Fact]
    public void Cache_SameManagedInstance_ShouldReuseIdentity()
    {
        using var state = new LuauState();
        var value = new ValueUserdata { Value = 11 };

        using LuauUserdata first = state.GetOrCreateUserdata(value);
        using LuauUserdata second = state.GetOrCreateUserdata(value);

        state.Globals.Set("first", first);
        state.Globals.Set("second", second);
        state.DoString("sameIdentity = first == second");

        state.Globals.TryGet("sameIdentity", out bool sameIdentity).ShouldBeTrue();
        sameIdentity.ShouldBeTrue();
    }

    [Fact]
    public void Cache_DifferentManagedInstances_ShouldNotReuseIdentity()
    {
        using var state = new LuauState();
        var firstValue = new ValueUserdata();
        var secondValue = new ValueUserdata();

        using LuauUserdata first = state.GetOrCreateUserdata(firstValue);
        using LuauUserdata second = state.GetOrCreateUserdata(secondValue);

        state.Globals.Set("first", first);
        state.Globals.Set("second", second);
        state.DoString("sameIdentity = first == second");

        state.Globals.TryGet("sameIdentity", out bool sameIdentity).ShouldBeTrue();
        sameIdentity.ShouldBeFalse();
    }

    [Fact]
    public void Cache_DisposingOneWrapper_ShouldNotInvalidateOtherWrapper()
    {
        using var state = new LuauState();
        var value = new ValueUserdata { Value = 44 };

        using LuauUserdata first = state.GetOrCreateUserdata(value);
        using LuauUserdata second = state.GetOrCreateUserdata(value);

        first.Dispose();
        second.TryGetManaged(out ValueUserdata? resolved, out string? error).ShouldBeTrue(error);
        ReferenceEquals(value, resolved).ShouldBeTrue();
    }

    [Fact]
    public void Cache_StrongLuaReference_ShouldKeepIdentityReusable()
    {
        using var state = new LuauState();
        var value = new ValueUserdata();

        using LuauUserdata keepAlive = state.GetOrCreateUserdata(value);
        state.Globals.Set("keepAlive", keepAlive);

        keepAlive.Dispose();

        using LuauUserdata recreated = state.GetOrCreateUserdata(value);
        state.Globals.Set("recreated", recreated);
        state.DoString("sameIdentity = keepAlive == recreated");

        state.Globals.TryGet("sameIdentity", out bool sameIdentity).ShouldBeTrue();
        sameIdentity.ShouldBeTrue();
    }

    [Fact]
    public void Cache_WhenUserdataCollected_ShouldCreateNewIdentity()
    {
        using var state = new LuauState();
        var value = new ValueUserdata();

        LuauUserdata oldUserdata = state.GetOrCreateUserdata(value);
        state.Globals.Set("oldUserdata", oldUserdata);
        oldUserdata.Dispose();

        state.DoString(
            """
            weakKeys = setmetatable({}, { __mode = "k" })
            weakKeys[oldUserdata] = true
            oldUserdata = nil
            hasCollectGarbage = collectgarbage ~= nil
            if hasCollectGarbage then
              collectgarbage("collect")
            end
            oldCollected = next(weakKeys) == nil
            """
        );

        state.Globals.TryGet("hasCollectGarbage", out bool hasCollectGarbage).ShouldBeTrue();
        if (!hasCollectGarbage)
            return;

        state.Globals.TryGet("oldCollected", out bool oldCollected).ShouldBeTrue();
        oldCollected.ShouldBeTrue();

        using LuauUserdata newUserdata = state.GetOrCreateUserdata(value);
        state.Globals.Set("newUserdata", newUserdata);
        state.DoString("reusedAfterCollect = weakKeys[newUserdata] == true");

        state.Globals.TryGet("reusedAfterCollect", out bool reusedAfterCollect).ShouldBeTrue();
        reusedAfterCollect.ShouldBeFalse();
    }

    [Fact]
    public void Cache_ShouldStayStableUnderRepeatedLookups()
    {
        using var state = new LuauState();
        var value = new ValueUserdata();

        using LuauUserdata baseline = state.GetOrCreateUserdata(value);
        state.Globals.Set("baseline", baseline);

        for (int i = 0; i < 1000; i++)
        {
            using LuauUserdata current = state.GetOrCreateUserdata(value);
            state.Globals.Set("current", current);
            state.DoString("sameIdentity = baseline == current");
            state.Globals.TryGet("sameIdentity", out bool sameIdentity).ShouldBeTrue();
            sameIdentity.ShouldBeTrue();
        }
    }
}
