# Functions

Functions are one of the core interaction points between C# and Luau.

You will usually work with functions in two directions:

- call an existing Luau function from managed code,
- expose a managed callback so Luau can call into your code.

## Call Luau functions

Get a function from globals or a table, keep the owned reference for as long as needed, and invoke it with typed arguments:

```csharp
using LuauFunction add = lua.Globals.GetLuauFunction("add");

double result = add.Invoke<double>(1, 2);
```

The generic return type controls how the Luau return value is converted.

## Register managed callbacks

Use `CreateFunction` for normal typed delegates. This is the default callback API:

```csharp
using LuauFunction log = lua.CreateFunction((string message) => Console.WriteLine(message));
lua.Globals.Set("log", log);

using LuauFunction sum = lua.CreateFunction((int a, int b) => a + b);
lua.Globals.Set("sum", sum);
```

`CreateFunction` is generator-backed. The compiler intercepts the direct call and emits a small adapter that marshals Luau arguments into your delegate shape.

- Call `CreateFunction(...)` directly at the call site.
- Do not store `lua.CreateFunction` in another delegate and invoke it indirectly.
- Supported parameter and return types follow the rules in [type mapping](../concepts/type-mapping.md).
- Unsupported delegate shapes fail closed with diagnostics instead of falling back to a runtime stub.

This lets Luau scripts call managed logic while staying inside the same state.

## Choose between callback APIs

| Capability | `CreateFunction` | `CreateFunctionBuilder` |
| --- | --- | --- |
| Input shape | typed delegate | `LuauArgs` |
| Output shape | delegate `void` or one managed return value | `LuauReturn.Ok(...)` / `LuauReturn.Error(...)` |
| Requirements | direct call, generator-backed | plain runtime API |
| Best for | simple, supported callback signatures | manual validation, multiple return values, unsupported signatures |

Prefer `CreateFunction` unless you specifically need the extra control from `CreateFunctionBuilder`.

## Manual callback builder

Use `CreateFunctionBuilder` when you want to read raw callback arguments yourself, return more than one value, or shape the error contract explicitly:

```csharp
using LuauFunction pair = lua.CreateFunctionBuilder(static args =>
{
    if (!args.TryValidateArgumentCount(2, out string? error))
        return LuauReturn.Error(error);
    if (!args.TryReadNumber(1, out int a, out error) || !args.TryReadNumber(2, out int b, out error))
        return LuauReturn.Error(error);

    return LuauReturn.Ok(a + b, a - b);
});

lua.Globals.Set("pair", pair);
```

Return `LuauReturn.Error(...)` for expected user-facing failures. Unhandled exceptions are caught and surfaced back to Luau as managed callback failures.

## Callback lifetimes

`LuauArgs` and borrowed values such as `LuauFunctionView`, `LuauTableView`, and `LuauStringView` are callback-scoped. Use them immediately, and promote them to owned values before the callback returns if you need to keep them. See [lifetimes and ownership](../concepts/lifetimes.md).

## Error behavior

Function calls can fail for two broad reasons:

- Luau reported an execution error.
- The returned value could not be converted to the managed return type you requested.

Treat both as part of the contract of your host API. When exposing managed callbacks, argument conversion failures and `LuauReturn.Error(...)` values also become normal Luau errors.

## Borrowed function views

`LuauFunctionView` represents a callback-scoped borrowed function value. Use it immediately and do not cache it after the callback returns.

## Signature design

Keep delegate signatures simple and explicit.

- Prefer concrete managed types over overly generic callback surfaces.
- Make nullable behavior intentional.
- Avoid exposing signatures that hide lossy numeric conversion.
