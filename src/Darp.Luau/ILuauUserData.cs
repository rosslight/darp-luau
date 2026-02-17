namespace Darp.Luau;

/// <summary> A generic interface which provides callbacks for a userdata </summary>
/// <typeparam name="T"> The C# Type of the userdata </typeparam>
public interface ILuauUserData<in T>
    where T : allows ref struct
{
    static abstract IntoLuau OnIndex(T self, in LuauState view, in ReadOnlySpan<char> fieldName);

    static abstract bool OnSetIndex(T self, in LuauView view, in ReadOnlySpan<char> fieldName);

    /// <summary> Called when Luau tries to call a member as a function </summary>
    /// <param name="self"> The luau UserData </param>
    /// <param name="view"> </param>
    /// <param name="methodName"> The name of the member </param>
    /// <returns> True, if the method call was handled; False, otherwise </returns>
    static abstract bool OnMethodCall(T self, LuauFunctions view, in ReadOnlySpan<char> methodName);
}
