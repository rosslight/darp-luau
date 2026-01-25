namespace Darp.Luau;

public readonly ref struct UserDataFields<T>
    where T : allows ref struct
{
    public void AddFieldMethodGet<TV>(ReadOnlySpan<char> name, Func<LuauView, T, TV> getter) =>
        throw new NotImplementedException();

    public void AddFieldMethodSet<TV>(ReadOnlySpan<char> name, Action<LuauView, T, TV> setter) =>
        throw new NotImplementedException();
}
