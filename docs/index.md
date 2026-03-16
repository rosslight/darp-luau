# Getting started

Darp.Luau is a .NET wrapper around [Luau](https://luau.org/) with a strong focus on native AOT compatibility, typed access to Lua values, and predictable reference lifetimes while introducing minimal overhead.

!!! info

    Darp.Luau is still under active development. Expect breaking API changes and documentation updates as the project evolves.
    See [Limitations and roadmap notes](limitations.md) for the current boundaries and planned expansion areas.


This documentation is organized around the way you use the library in practice:

- Start a `LuauState` and decide which built-in libraries you want.
- Run scripts from strings or files.
- Move data between Luau and C# through functions, tables, buffers, and userdata.
- Understand which values are owned references and which values are borrowed views.

## What makes this library different

- Native AOT compatibility is a first-class goal.
- The API is typed and tries to surface conversion failures clearly.
- Lifetime-sensitive values are modeled explicitly instead of hiding ownership rules.
- Custom userdata and custom libraries let you expose managed behavior to Luau.
- Source generators improve the developer experience.

## Add the package

Add the package to your project with the normal .NET workflow:

```bash
dotnet add package Darp.Luau
```

## Create a state

`LuauState` owns the underlying Luau VM and acts as the main entry point.

```csharp
using Darp.Luau;

using var lua = new LuauState();
```

## Execute a script

You can execute Luau from a file or from an inline string:

```csharp
lua.DoString("result = 1");

var result = lua.Globals.GetNumber("result");
```

## Create and use a table

```csharp
using LuauTable config = lua.CreateTable();
config.Set("name", "Ada");
config.Set("score", 42);
config.Set("enabled", true);

lua.Globals.Set("config", config);

using LuauTable roundTripped = lua.Globals.GetLuauTable("config");
string name = roundTripped.GetUtf8String("name");
_ = roundTripped.TryGetNumber("score", out int score);
bool? enabled = roundTripped.GetBooleanOrNil("enabled");
```

See [Tables](features/tables.md) for `*OrNil` reads, list helpers, raw Luau wrappers, and borrowed table views.

## Expose a callback

Register managed callbacks with `CreateFunction(...)` and store the resulting `LuauFunction` in globals or tables:

```csharp
using var log = lua.CreateFunction((string message) => Console.WriteLine(message));
lua.Globals.Set("log", log);

lua.DoString("""log("hello from lua")""");
```

For supported fixed signatures, `CreateFunction(...)` is the normal choice. If you need manual argument parsing, explicit error shaping, or multiple return values, use `CreateFunctionBuilder(...)`. See [Functions](features/functions.md).

## Expose userdata

If a class implements `ILuauUserData<T>`, it can be exposed to Luau as userdata:

```csharp
var player = new PlayerUserdata { Name = "Ada", Score = 42 };

lua.Globals.Set("player", IntoLuau.FromUserdata(player));

lua.DoString(
    """
    log(player.name)
    player.score = 100
    """
);
```

See [Userdata](features/userdata.md) for hook behavior, retrieval APIs, and lifetime rules.

## Register a custom library



```csharp
lua.OpenLibrary("game", static (state, in LuauTable lib) =>
{
    lib.Set("answer", 42);
    using var addFunc = state.CreateFunction((int a, int b) => a + b);
    lib.Set("add", addFunc);
});

lua.DoString(
    """
    result = game.add(game.answer, 8)
    log(result)
    """
);
```
