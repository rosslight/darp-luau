Darp.Luau
======

[![Darp.Results](https://img.shields.io/nuget/v/Darp.Luau.svg)](https://www.nuget.org/packages/Darp.Luau)
[![Downloads](https://img.shields.io/nuget/dt/Darp.Luau)](https://www.nuget.org/packages/Darp.Luau)
![License](https://img.shields.io/github/license/rosslight/darp-luau)
![.NET](https://img.shields.io/badge/version-.NET10-blue)

`Darp.Luau` is a .NET wrapper around [Luau](https://luau.org/) focused on native AOT compatibility, typed value access, and explicit ownership for Luau-backed references.

## What it gives you today

- `LuauState` with configurable built-in libraries
- `DoString(...)` for running Luau source from managed code
- typed reads and writes for tables, functions, userdata, strings, and buffers
- explicit owned wrappers such as `LuauTable` and `LuauFunction`
- callback-scoped borrowed views such as `LuauTableView` and `LuauFunctionView`
- managed callbacks through `CreateFunction(...)` and `CreateFunctionBuilder(...)`
- custom libraries and managed userdata hooks through `ILuauUserData<T>`

## Quick start

```csharp
using Darp.Luau;

using var lua = new LuauState();

using LuauFunction log = lua.CreateFunction((string message) => Console.WriteLine(message));
lua.Globals.Set("log", log);

using LuauTable config = lua.CreateTable();
config.Set("name", "Ada");
config.Set("enabled", true);
lua.Globals.Set("config", config);

lua.DoString(
    """
    function add(a, b)
      return a + b
    end

    log(config.name)
    result = add(20, 22)
    """
);

double result = lua.Globals.GetNumber("result");
```

## Call Lua functions from C#

```csharp
using LuauFunction add = lua.Globals.GetLuauFunction("add");

double sum = add.Invoke<double>(1, 2);
```

`Invoke<TR>(...)` converts the Luau return value to the managed type you ask for. The current API provides overloads for zero, one, or two arguments.

## Expose managed callbacks

Use `CreateFunction(...)` for supported fixed delegate signatures:

```csharp
using LuauFunction sum = lua.CreateFunction((int a, int b) => a + b);
lua.Globals.Set("sum", sum);
```

Use `CreateFunctionBuilder(...)` when you need manual argument parsing, multiple return values, or explicit user-facing errors:

```csharp
using LuauFunction pair = lua.CreateFunctionBuilder(static args =>
{
    if (!args.TryValidateArgumentCount(2, out string? error))
        return LuauReturn.Error(error);
    if (args.ArgumentCount != 2)
        return LuauReturn.Error("Expected exactly 2 arguments.");
    if (!args.TryReadNumber(1, out int a, out error) || !args.TryReadNumber(2, out int b, out error))
        return LuauReturn.Error(error);

    return LuauReturn.Ok(a + b, a - b);
});
```

`CreateFunction(...)` must be called directly at the call site so the generator can intercept it. If you need a shape that is not supported there, use `CreateFunctionBuilder(...)`.

## Work with tables

```csharp
using LuauTable settings = lua.CreateTable();
settings.Set("volume", 0.8);
settings.Set("muted", false);
settings.Set("blob", new byte[] { 1, 2, 3 });

lua.Globals.Set("settings", settings);

using LuauTable roundTripped = lua.Globals.GetLuauTable("settings");
double volume = roundTripped.GetNumber("volume");
bool muted = roundTripped.GetBoolean("muted");
byte[] blob = roundTripped.GetBuffer("blob");
```

Use `Get*` for required values, `TryGet*` for optional or external data, and `*OrNil` when `nil` is part of the contract.

## Work with userdata

```csharp
var player = new PlayerUserdata { Name = "Ada" };

lua.Globals.Set("player", IntoLuau.FromUserdata(player));

PlayerUserdata samePlayer = lua.Globals.GetUserdata<PlayerUserdata>("player");

using LuauUserdata playerRef = lua.Globals.GetLuauUserdata("player");
_ = playerRef.TryGetManaged(out PlayerUserdata? resolvedPlayer, out string? error);
```

Managed userdata types implement `ILuauUserData<T>` to expose script-facing fields, setters, and methods. See [Userdata](docs/features/userdata.md) for the full hook model.

## Register custom libraries

```csharp
lua.OpenLibrary("game", static (state, in LuauTable lib) =>
{
    lib.Set("answer", 42);

    using LuauFunction add = state.CreateFunction((int a, int b) => a + b);
    lib.Set("add", add);
});
```

`OpenLibrary(...)` registers a global table. It is a convenient way to expose host-provided APIs, but it is not a `require(...)`-style module loader by itself.

## Ownership and lifetime

- `LuauTable`, `LuauFunction`, `LuauString`, `LuauBuffer`, `LuauUserdata`, and reference-backed `LuauValue` are owned references and should be disposed.
- `LuauTableView`, `LuauFunctionView`, `LuauStringView`, `LuauBufferView`, `LuauUserdataView`, and `LuauArgs` are borrowed callback-scoped values.
- Reference-backed values belong to one `LuauState`; cross-state usage is invalid.

## Current boundaries

- `DoString(...)` is the script execution API today. If you want file-based execution, read the file yourself and pass its contents in.
- `CreateFunction(...)` is generator-backed and has no runtime fallback.
- `LuauState` is not thread-safe.
- A documented module system and higher-level async/thread orchestration are not part of the current surface yet.
