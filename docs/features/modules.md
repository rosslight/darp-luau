# Modules and Standard Libraries

`LuauState` has two separate concepts:

- standard Luau libraries selected through `LuauLibraries`,
- modules loaded from Luau with `require(...)`.

Host modules and file-backed script modules share one require context.

## Standard Libraries

Choose built-in Luau libraries when you create the state, or load additional libraries later:

```csharp
using var lua = new LuauState(LuauLibraries.Math | LuauLibraries.String);

lua.LoadStandardLibraries(LuauLibraries.Buffer);
```

Available flags include:

- `Base`
- `Coroutine`
- `Table`
- `Os`
- `String`
- `Math`
- `Debug`
- `Utf8`
- `Bit32`
- `Buffer`
- `Vector`

Important details:

- `LuauLibraries.Minimal` (`Base | Table`) is always enabled automatically.
- `EnabledLibraries` shows the effective built-in library set for the state.
- `LoadStandardLibraries(...)` is idempotent; already loaded libraries are ignored.
- `LuauLibraries.Buffer` enables Luau's script-side `buffer` library. Host-side buffer interop such as `CreateBuffer(...)`, `GetBuffer(...)`, or passing `byte[]` does not depend on that flag.

## Generate a Host Module With `[LuauModule]`

The recommended way to expose a fixed host API is to mark a partial type with `[LuauModule]` and mark the script-facing members with `[LuauMember]`.

```csharp
using Darp.Luau;

[LuauModule("game")]
public static partial class GameModule
{
    [LuauMember("answer")]
    public static int Answer => 42;

    [LuauMember("add")]
    public static int Add(int left, int right) => left + right;
}
```

The source generator emits `ModuleName` and `OnLoad(...)` members. Register them once for each state that should receive the module:

```csharp
using var lua = new LuauState();

lua.RegisterModule(GameModule.ModuleName, GameModule.OnLoad);

lua.Load(
    """
    local game = require("game")
    result = game.add(game.answer, 8)
    """
).Execute();
```

Generated registration stores a host module registration. The module table is created lazily when Luau first calls `require("game")`, then cached for later calls.

### Static and Instance Modules

Static module types generate a static `OnLoad(...)` method. Instance module types generate an instance `OnLoad(...)` method, which is useful when the module needs managed state:

```csharp
[LuauModule("scoreboard")]
public sealed partial class ScoreboardModule
{
    private readonly List<string> _names = [];

    [LuauMember("add")]
    public void Add(string name) => _names.Add(name);

    [LuauMember("count")]
    public int Count => _names.Count;
}

var scoreboard = new ScoreboardModule();
lua.RegisterModule(ScoreboardModule.ModuleName, scoreboard.OnLoad);
```

Generated module properties are snapshot values written to the module table when it is loaded. Instance module properties are not supported because they would need live table accessors.

### Nested Module Paths

Module member names can contain dots to create nested tables:

```csharp
[LuauModule("workshop")]
public sealed partial class WorkshopModule
{
    [LuauMember("tools.hammer")]
    public int MakeHammer(int size) => size * 2;
}
```

Luau sees this as:

```lua
local workshop = require("workshop")
local bundle = workshop.tools.hammer(5)
```

### Generated Module Member Rules

Generated modules support:

- methods with fixed supported signatures,
- static read-only properties,
- instance methods on class modules,
- generated or manual managed userdata as supported parameter and return types.

The generator reports diagnostics for unsupported shapes instead of emitting weak runtime fallbacks. Current boundaries include:

- exported module types must be partial, top-level, and non-generic,
- instance structs are not supported,
- fields are not exported,
- instance properties are not supported,
- write-capable module properties are not supported,
- optional, `params`, `ref`, `in`, `out`, generic methods, and by-ref returns are not supported,
- member paths cannot conflict, such as using both `Field` and `Field.u8` as exports.

Use stable Luau-facing names in `[LuauMember("...")]` instead of mirroring managed names by default.

## Register a Custom Host Module

Use manual `RegisterModule(...)` when the generated module model cannot express the shape you need, such as dynamic table construction, custom validation, or unsupported callback signatures.

```csharp
lua.RegisterModule("game", static (state, in LuauTable module) =>
{
    module.Set("answer", 42);

    using LuauFunction add = state.CreateFunction((int a, int b) => a + b);
    module.Set("add", add);
});

lua.Load(
    """
    local game = require("game")
    result = game.add(game.answer, 8)
    """
).Execute();
```

Populate the module inside the registration callback. The temporary `module` wrapper passed to `RegisterModule(...)` is only meant for that setup step.

## Registration Behavior

`RegisterModule(...)`:

- throws if the module name is already registered,
- reserves `./`, `../`, `/`, `\`, and `@` prefixes for script-module paths,
- installs the shared require context if it was not installed already,
- loads the module lazily on the first matching `require(...)`,
- caches the module table after a successful load.

See [Require](require.md) for file-backed script module loading.
