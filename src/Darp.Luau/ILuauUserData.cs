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
    /// <param name="value"> The resulting value to push if the member is handled </param>
    /// <returns> True if member read was handled; false otherwise </returns>
    static abstract bool OnIndex(T self, in LuauState state, in ReadOnlySpan<char> fieldName, out IntoLuau value);

    /// <summary> Called when Luau tries to assign a member on userdata (newindex access) </summary>
    /// <param name="self"> The managed userdata instance </param>
    /// <param name="valueView"> View of the assigned Lua value </param>
    /// <param name="fieldName"> The assigned member name </param>
    /// <returns> True if member assignment was handled; false otherwise </returns>
    static abstract bool OnSetIndex(T self, in LuauView valueView, in ReadOnlySpan<char> fieldName);

    /// <summary> Called when Luau tries to call a member as a function </summary>
    /// <param name="self"> The luau UserData </param>
    /// <param name="functionArgs"> The method call arguments and return value builder </param>
    /// <param name="methodName"> The name of the member </param>
    /// <returns> True, if the method call was handled; False, otherwise </returns>
    static abstract bool OnMethodCall(T self, LuauFunctions functionArgs, in ReadOnlySpan<char> methodName);
}
