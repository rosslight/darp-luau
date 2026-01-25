namespace Darp.Luau;

public interface ILuauUserData<T>
    where T : allows ref struct
{
    public static abstract void AddFields(in UserDataFields<T> fields);
    public static abstract void AddMethods(in UserDataMethods<T> fields);
}
