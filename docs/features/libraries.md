# Libraries

`LuauState` supports both built-in Luau libraries and custom managed libraries.

## Built-in libraries

Use `LuauLibraries` flags to choose which standard libraries to load:

```csharp
using var lua = new LuauState(LuauLibraries.Math | LuauLibraries.String);
```

The available flags include:

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

`LuauLibraries.Minimal` is always enabled automatically, even if you pass a narrower set.

## Custom libraries

Use `OpenLibrary` to create a table in globals and populate it from managed code:

```csharp
lua.OpenLibrary("game", static (state, in LuauTable lib) =>
{
    lib.Set("answer", 42);

    using LuauFunction add = state.CreateFunction((int a, int b) => a + b);
    lib.Set("add", add);
});
```

This is a good fit for script-facing utility modules and host-provided APIs.

## Guidance

- Keep the global surface small and deliberate.
- Group related functionality into a named library table.
- Avoid leaking internal managed implementation details directly into script code.
- Throw early if your host cannot construct the library safely.
