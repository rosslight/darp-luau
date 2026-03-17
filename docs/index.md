# Getting started

Darp.Luau is a .NET wrapper around [Luau](https://luau.org/) focused on native AOT compatibility, typed value access, and explicit ownership for Luau-backed references.

!!! info

    Darp.Luau is still under active development. Expect breaking API changes while the library surface settles.
    See [Limitations and current boundaries](concepts/limitations.md) for the parts that are intentionally narrow today.

This documentation is organized around the way you use the library in practice:

- create a `LuauState` and choose built-in libraries,
- run Luau source with `DoString(...)`,
- move values between Luau and C# through strings, tables, functions, buffers, and userdata,
- understand which values are owned references and which values are borrowed callback views.

## Concepts

- `LuauState` owns the underlying Luau VM.
- Owned wrappers such as `LuauTable` and `LuauFunction` can outlive the current call frame, but they need disposal.
- Borrowed `*View` types such as `LuauTableView` and `LuauFunctionView` are callback-scoped.
- `CreateFunction(...)` is the normal typed callback API, but it must be called directly so the generator can intercept it.
- `ILuauUserData<T>` is the manual hook surface for exposing managed objects as userdata.

See [Concepts](concepts/index.md).

## Add the package

```bash
dotnet add package Darp.Luau
```

## Create a state

`LuauState` is the main entry point. It creates the Luau VM, loads the selected built-in libraries, and exposes the global table.

```csharp
using Darp.Luau;

using var lua = new LuauState();
```

`LuauLibraries.Minimal` (`Base | Table`) is always enabled automatically.

## Execute Luau source

`DoString(...)` runs Luau source from managed code:

```csharp
lua.DoString(
    """
    function add(a, b)
      return a + b
    end
    """
);

using LuauFunction add = lua.Globals.GetLuauFunction("add");
double result = add.Invoke<double>(1, 2);
```

If you want file-based execution, load the file contents yourself and pass them to `DoString(...)`.

## Move data with tables

```csharp
using LuauTable config = lua.CreateTable();
config.Set("name", "Ada");
config.Set("score", 42);
config.Set("enabled", true);

lua.Globals.Set("config", config);

using LuauTable roundTripped = lua.Globals.GetLuauTable("config");
string name = roundTripped.GetUtf8String("name");
double score = roundTripped.GetNumber("score");
bool? enabled = roundTripped.GetBooleanOrNil("enabled");
```

See [Tables](features/tables.md) for `Get*`, `TryGet*`, `*OrNil`, dense-array helpers, and raw Luau wrappers.

## Work with strings

```csharp
lua.Globals.Set("name", "Ada");

string roundTripped = lua.Globals.GetUtf8String("name");
```

Use `string` for managed text, `ReadOnlySpan<byte>` for immediate borrowed UTF-8 bytes, `LuauString` for owned Luau-backed string values, and `LuauStringView` inside callbacks. See [Strings](features/strings.md).

## Work with buffers

```csharp
byte[] payload = [0x01, 0x02, 0x03];

lua.Globals.Set("payload", payload);

byte[] roundTripped = lua.Globals.GetBuffer("payload");
```

Use `byte[]` for managed copies, `ReadOnlySpan<byte>` for immediate borrowed reads, `LuauBuffer` for owned Luau-backed values, and `LuauBufferView` inside callbacks. See [Buffers](features/buffers.md).

## Expose a callback

Register managed callbacks with `CreateFunction(...)` and store the resulting `LuauFunction` in globals or tables:

```csharp
using LuauFunction log = lua.CreateFunction((string message) => Console.WriteLine(message));
lua.Globals.Set("log", log);

using LuauFunction pair = lua.CreateFunction((int a, int b) => (a + b, a - b));
lua.Globals.Set("pair", pair);

lua.DoString("""log("hello from luau")""");
```

Use `CreateFunction(...)` for supported fixed signatures, including supported top-level tuple returns. If you need manual argument parsing, unsupported callback shapes, or custom error shaping, use `CreateFunctionBuilder(...)`. See [Functions](features/functions.md).

## Expose userdata

If a managed type implements `ILuauUserData<T>`, you can expose it to Luau as userdata:

```csharp
var player = new PlayerUserdata { Name = "Ada", Score = 42 };

lua.Globals.Set("player", IntoLuau.FromUserdata(player));

lua.DoString(
    """
    currentName = player.name
    player.score = 100
    """
);
```

See [Userdata](features/userdata.md) for hook behavior, retrieval APIs, identity rules, and lifetimes.

## Register a custom library

```csharp
lua.OpenLibrary("game", static (state, in LuauTable lib) =>
{
    lib.Set("answer", 42);

    using LuauFunction add = state.CreateFunction((int a, int b) => a + b);
    lib.Set("add", add);
});

lua.DoString("result = game.add(game.answer, 8)");
```

`OpenLibrary(...)` creates a global table and lets you populate it from managed code. See [Libraries](features/libraries.md).

## Where to next

- [Lifetimes and ownership](concepts/lifetimes.md)
- [Type mapping](concepts/type-mapping.md)
- [Functions](features/functions.md)
- [Strings](features/strings.md)
- [Buffers](features/buffers.md)
- [Tables](features/tables.md)
- [Userdata](features/userdata.md)
- [Libraries](features/libraries.md)
