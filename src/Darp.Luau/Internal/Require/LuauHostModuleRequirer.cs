namespace Darp.Luau.Internal.Require;

internal sealed class LuauHostModuleRequirer : IDisposable
{
    private readonly LuauState _state;
    private readonly Dictionary<string, HostModule> _hostModules = new(StringComparer.Ordinal);

    public LuauHostModuleRequirer(LuauState state)
    {
        _state = state;
    }

    public void RegisterModule(string name, LuauState.OnModuleLoad onLoad)
    {
        if (!_hostModules.TryAdd(name, new HostModule(onLoad)))
            throw new InvalidOperationException($"Module '{name}' is already registered.");
    }

    public RequireResolution Resolve(string name)
    {
        if (!_hostModules.TryGetValue(name, out HostModule? registration))
            return RequireResolution.NotFound;

        if (registration.CachedValue is { } cached)
            return RequireResolution.LoadedHostModule(cached);

        if (registration.IsLoading)
            return RequireResolution.LoadError($"module '{name}' is already loading");

        registration.IsLoading = true;
        try
        {
            LuauTable loaded = _state.CreateTable();
            try
            {
                registration.OnLoad(_state, loaded);
            }
            catch
            {
                loaded.Dispose();
                throw;
            }

            registration.CachedValue = loaded;
            return RequireResolution.LoadedHostModule(loaded);
        }
        catch (Exception exception)
        {
            return RequireResolution.LoadError(
                $"failed to load module '{name}': {exception.GetType().Name}: {exception.Message}"
            );
        }
        finally
        {
            registration.IsLoading = false;
        }
    }

    public void Dispose()
    {
        foreach (HostModule module in _hostModules.Values)
        {
            module.CachedValue?.Dispose();
            module.CachedValue = null;
        }

        _hostModules.Clear();
    }

    private sealed class HostModule(LuauState.OnModuleLoad onLoad)
    {
        public LuauState.OnModuleLoad OnLoad { get; } = onLoad;
        public LuauTable? CachedValue { get; set; }
        public bool IsLoading { get; set; }
    }
}
