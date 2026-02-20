namespace Darp.Luau;

/// <summary> A generic interface which provides callbacks for a userdata </summary>
/// <typeparam name="T"> The C# Type of the userdata </typeparam>
public interface ILuauUserData<in T>
    where T : class
{
    /// <summary> Called when Luau tries to read a member on userdata (index access) </summary>
    /// <param name="self"> The managed userdata instance </param>
    /// <param name="state"> The current Luau state </param>
    /// <param name="fieldName"> The requested member name </param>
    /// <returns>
    /// The callback result.
    /// Use <see cref="LuauIndexResult.NotHandled"/> to signal unknown members.
    /// </returns>
    static abstract LuauIndexResult OnIndex(T self, in LuauState state, in ReadOnlySpan<char> fieldName);

    /// <summary> Called when Luau tries to assign a member on userdata (newindex access) </summary>
    /// <param name="self"> The managed userdata instance </param>
    /// <param name="setArgs"> View of the assigned Lua value </param>
    /// <param name="fieldName"> The assigned member name </param>
    /// <returns>
    /// The callback result.
    /// Use <see cref="LuauSetIndexResult.NotHandled"/> to signal unknown members.
    /// </returns>
    static abstract LuauSetIndexResult OnSetIndex(T self, LuauSetIndexArgs setArgs, in ReadOnlySpan<char> fieldName);

    /// <summary> Called when Luau tries to call a member as a function </summary>
    /// <param name="self"> The luau UserData </param>
    /// <param name="functionArgs"> The method call arguments </param>
    /// <param name="methodName"> The name of the member </param>
    /// <returns> The callback result. Use <see cref="LuauReturn.NotHandledError"/> to signal unknown methods </returns>
    static abstract LuauReturn OnMethodCall(T self, LuauArgs functionArgs, in ReadOnlySpan<char> methodName);
}
