namespace Darp.Luau;

public readonly ref struct UserDataMethods<T>
    where T : allows ref struct
{
    public void AddMethod(ReadOnlySpan<char> name, Action<T> onCall) => throw new NotImplementedException();

    public void AddMethod(ReadOnlySpan<char> name, Delegate onCall) => throw new NotImplementedException();
}
