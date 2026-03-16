# Libraries

`LuauState` supports two kinds of libraries:

- built-in Luau standard libraries selected through `LuauLibraries`,
- custom host-provided libraries registered as global tables with `OpenLibrary(...)`.

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

## Register a custom library with `OpenLibrary(...)`

`OpenLibrary(...)` creates a new table, lets you populate it from managed code, and then stores that table in globals under the chosen name.

```csharp
lua.OpenLibrary("game", static (state, in LuauTable lib) =>
{
    lib.Set("answer", 42);

    using LuauFunction add = state.CreateFunction((int a, int b) => a + b);
    lib.Set("add", add);
});

lua.DoString("result = game.add(game.answer, 8)");
```

This is a good fit for script-facing utility packages and host APIs that you want to group under a stable namespace-like table.

Populate the library inside the registration callback. The temporary `lib` wrapper passed to `OpenLibrary(...)` is only meant for that setup step.

## Registration behavior

`OpenLibrary(...)` is intentionally simple:

- it throws if the global name already exists,
- it wraps build failures in an `InvalidOperationException`,
- it registers a plain global table, not a module loader,
- it can be called after state creation for runtime registration.

If you want `require(...)` semantics, caching, or a more elaborate loading model, build that on top of this primitive.

## Guidance

- Keep the global surface small and deliberate.
- Group related functionality under one named table instead of scattering globals.
- Prefer stable script-facing names over mirroring internal managed type names.
- Register only what the script truly needs.
