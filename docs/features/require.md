# Require

`require(...)` is the single module-loading surface in Darp.Luau. It can load host-provided C# modules registered with `RegisterModule(...)`, and file-backed script modules after `EnableScriptModules()`.

## Host Modules

Register a host module when C# owns the module table:

```csharp
using Darp.Luau;

using var lua = new LuauState();

lua.RegisterModule("game", static (state, in LuauTable module) =>
{
    module.Set("answer", 42);

    using LuauFunction add = state.CreateFunction((int a, int b) => a + b);
    module.Set("add", add);
});
```

Luau loads the module by name:

```lua
local game = require("game")

result = game.add(game.answer, 8)
```

The module callback runs lazily the first time the module is required. The resulting table is cached and returned for later `require("game")` calls.

Strongly typed modules can implement `ILuauModule<TModule>`:

```csharp
public sealed class GameModule : ILuauModule<GameModule>
{
    public static string ModuleName => "game";

    public void OnLoad(LuauState lua, in LuauTable module)
    {
        module.Set("answer", 42);
    }
}

lua.RegisterModule(new GameModule());
```

## File-Backed Script Modules

Call `EnableScriptModules()` when Luau scripts should load other `.luau` or `.lua` files from disk:

```csharp
using var lua = new LuauState();
lua.EnableScriptModules();

string path = Path.GetFullPath("scripts/main.luau");
lua.Load(File.ReadAllBytes(path)).WithName("@" + path).Execute();
```

File-backed script module paths must start with `./`, `../`, or `@alias`:

```lua
local x = require("./x")
local parent = require("../parent")
local aliased = require("@shared/tools")
```

## Entry Chunk Names

File-backed script modules need a recognized requiring chunk name:

- `=stdin` for managed source that acts like a stdin entrypoint,
- `@<absolute-or-relative-path>` for source that should resolve modules from a file location.

For file-backed entry scripts, pass a chunk name that starts with `@` and points at the script path.

## Resolution Rules

`EnableScriptModules()` follows Luau's require-by-string rules covered by this package:

- relative paths resolve from the requiring file,
- module lookup checks `.luau`, `.lua`, `init.luau`, and `init.lua`,
- `.luaurc` and `.config.luau` aliases are supported,
- module results are cached by absolute file path.

Host module names are bare names such as `game`; names that look like script-module paths are reserved for file-backed modules.

## Module Behavior

- each file-backed module must return exactly one value,
- file-backed modules cannot yield while loading,
- returned values can be any Luau value that Darp.Luau can surface,
- host module callbacks populate a table and are cached after the first successful load.

## Error Reporting

If file-backed module loading fails, the Lua-visible error may be generic in some cases, such as `module must return a single value`.

Check `lua.RequireContext?.LoadError` for the loader's detailed message after a failed file-backed `require(...)`.
