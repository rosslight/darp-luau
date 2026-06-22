# Libraries

`LuauState` supports three kinds of libraries:

- built-in Luau standard libraries selected through `LuauLibraries`,
- source-generated host libraries declared with `[LuauLibrary]`,
- manually registered host libraries built with `OpenLibrary(...)`.

## Built-in libraries

Choose built-in Luau libraries when you create the state:

```csharp
using var lua = new LuauState(LuauLibraries.Math | LuauLibraries.String | LuauLibraries.Buffer);
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
- Built-in libraries are loaded when the state is created, not later.
- `LuauLibraries.Buffer` enables Luau's script-side `buffer` library. Host-side buffer interop such as `CreateBuffer(...)`, `GetBuffer(...)`, or passing `byte[]` does not depend on that flag.

## Generate a host library with `[LuauLibrary]`

The recommended way to expose a host library is to mark a partial type with `[LuauLibrary]` and mark the script-facing members with `[LuauMember]`.

```csharp
using Darp.Luau;

[LuauLibrary("game")]
public static partial class GameLibrary
{
    [LuauMember("answer")]
    public static int Answer => 42;

    [LuauMember("add")]
    public static int Add(int left, int right) => left + right;
}
```

The source generator emits a `Register(LuauState)` method. Call it once for each state that should receive the library:

```csharp
using var lua = new LuauState();

GameLibrary.Register(lua);

lua.Load("result = game.add(game.answer, 8)").Execute();
```

The generated registration path creates a global table named by `[LuauLibrary("...")]`, populates it, and preserves the same duplicate-global check as manual `OpenLibrary(...)`.

### Static and instance libraries

Static library types generate a static `Register(...)` method. Instance library types generate an instance `Register(...)` method, which is useful when the library needs managed state:

```csharp
[LuauLibrary("scoreboard")]
public sealed partial class ScoreboardLibrary
{
    private readonly List<string> _names = [];

    [LuauMember("add")]
    public void Add(string name) => _names.Add(name);

    [LuauMember("count")]
    public int Count => _names.Count;
}

var scoreboard = new ScoreboardLibrary();
scoreboard.Register(lua);
```

Generated library properties are snapshot values written to the library table during registration. Instance library properties are not supported because they would need live table accessors.

### Nested library paths

Library member names can contain dots to create nested tables:

```csharp
[LuauLibrary("workshop")]
public sealed partial class WorkshopLibrary
{
    [LuauMember("tools.hammer")]
    public int MakeHammer(int size) => size * 2;
}
```

Luau sees this as `workshop.tools.hammer(...)`.

### Generated library member rules

Generated libraries support:

- methods with fixed supported signatures,
- static read-only properties,
- instance methods on class libraries,
- generated or manual managed userdata as supported parameter and return types.

The generator reports diagnostics for unsupported shapes instead of emitting weak runtime fallbacks. Current boundaries include:

- exported library types must be partial, top-level, and non-generic,
- instance structs are not supported,
- fields are not exported,
- instance properties are not supported,
- write-capable library properties are not supported,
- optional, `params`, `ref`, `in`, `out`, generic methods, and by-ref returns are not supported,
- member paths cannot conflict, such as using both `Field` and `Field.u8` as exports.

Use stable Luau-facing names in `[LuauMember("...")]` instead of mirroring managed names by default.

## Register a custom library with `OpenLibrary(...)`

Use manual `OpenLibrary(...)` when the generated library model cannot express the shape you need, such as dynamic table construction, custom validation, or unsupported callback signatures.

`OpenLibrary(...)` creates a new table, lets you populate it from managed code, and then stores that table in globals under the chosen name.

```csharp
lua.OpenLibrary("game", static (state, in LuauTable lib) =>
{
    lib.Set("answer", 42);

    using LuauFunction add = state.CreateFunction((int a, int b) => a + b);
    lib.Set("add", add);
});

lua.Load("result = game.add(game.answer, 8)").Execute();
```

This is the lower-level fallback for script-facing utility packages and host APIs that you want to group under a stable namespace-like table.

Populate the library inside the registration callback. The temporary `lib` wrapper passed to `OpenLibrary(...)` is only meant for that setup step.

## Registration behavior

`OpenLibrary(...)` is intentionally simple:

- it throws if the global name already exists,
- it wraps build failures in an `InvalidOperationException`,
- it registers a plain global table, not a module loader,
- it can be called after state creation for runtime registration.

If you want file-backed `require(...)`, use `EnableRequire()` instead. `OpenLibrary(...)` exposes host APIs; it does not load script modules.

## Guidance

- Keep the global surface small and deliberate.
- Group related functionality under one named table instead of scattering globals.
- Prefer generated `[LuauLibrary]` declarations for regular fixed host APIs.
- Prefer stable script-facing names over mirroring internal managed type names.
- Fall back to manual `OpenLibrary(...)` only when the generated model is too narrow.
- Register only what the script truly needs.

See [Require](require.md) for filesystem-backed module loading.
