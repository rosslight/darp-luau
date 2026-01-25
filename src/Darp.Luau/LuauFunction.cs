namespace Darp.Luau;

public readonly ref struct LuauFunction
{
    public LuauState? State { get; }
    internal int Reference { get; }

    [Obsolete("Do not initialize the LuauFunction. Create using the LuauState instead", true)]
    public LuauFunction() => State = null;

    internal LuauFunction(LuauState? state, int reference) => (State, Reference) = (state, reference);

    public bool TryAs<T1, T2, TR>(out LuauFunction<T1, T2, TR> value)
        where T1 : allows ref struct
        where T2 : allows ref struct
        where TR : allows ref struct
    {
        value = new LuauFunction<T1, T2, TR>();
        return true;
    }
}

public readonly ref struct LuauFunction<TR>
    where TR : allows ref struct
{
    public LuauState? State { get; }
    internal int Reference { get; }

    public TR Call() => throw new NotImplementedException();

    public static implicit operator LuauFunction(LuauFunction<TR> f) => new(f.State, f.Reference);
}

public readonly ref struct LuauFunction<T1, TR>
    where T1 : allows ref struct
    where TR : allows ref struct
{
    public LuauState? State { get; }
    internal int Reference { get; }

    public TR Call(T1 p1) => throw new NotImplementedException();

    public static implicit operator LuauFunction(LuauFunction<T1, TR> f) => new(f.State, f.Reference);
}

public readonly ref struct LuauFunction<T1, T2, TR>
    where T1 : allows ref struct
    where T2 : allows ref struct
    where TR : allows ref struct
{
    public LuauState? State { get; }
    internal int Reference { get; }

    public TR Call(T1 p1, T2 p2) => throw new NotImplementedException();

    public static implicit operator LuauFunction(LuauFunction<T1, T2, TR> f) => new(f.State, f.Reference);
}
