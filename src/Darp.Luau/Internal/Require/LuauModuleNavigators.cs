using Darp.Luau.Native;

namespace Darp.Luau.Internal.Require;

internal class LuauModuleNavigators
{
    private readonly Lock _dictLock = new();
    private readonly Dictionary<nint, LuauModuleNavigator> _dict = [];

    public unsafe LuauModuleNavigator this[lua_State* L]
    {
        get
        {
            lock (_dictLock)
            {
                nint nKey = (nint)L;
                if (!_dict.TryGetValue(nKey, out LuauModuleNavigator? nav))
                {
                    nav = new LuauModuleNavigator();
                    _dict.Add(nKey, nav);
                }
                return nav;
            }
        }
    }
}
