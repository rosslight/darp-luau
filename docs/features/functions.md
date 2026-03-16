# Functions

Functions are one of the main ways managed code and Luau call into each other.

You usually work in two directions:

- call an existing Luau function from C#,
- expose a managed callback so Luau can call into your code.

## Call Luau functions from C#

Get an owned `LuauFunction` from globals or a table, keep it for as long as needed, and invoke it with typed arguments:

```csharp
using LuauFunction add = lua.Globals.GetLuauFunction("add");

double result = add.Invoke<double>(1, 2);
```

The generic return type controls how the Luau return value is converted.

- Use `Invoke<TR>()`, `Invoke<TR>(p1)`, or `Invoke<TR>(p1, p2)` depending on how many arguments you want to pass.
- Use `LuauNil` when you expect no return value.
- Argument values go through the normal `IntoLuau` conversion rules.

If Luau raises an error, `Invoke<TR>(...)` throws `LuaException`. If the return value cannot be converted to `TR`, it throws `InvalidCastException`.

## Expose managed callbacks with `CreateFunction(...)`

Use `CreateFunction(...)` for supported fixed delegate signatures:

```csharp
using LuauFunction log = lua.CreateFunction((string message) => Console.WriteLine(message));
lua.Globals.Set("log", log);

using LuauFunction add = lua.CreateFunction((int a, int b) => a + b);
lua.Globals.Set("add", add);
```

This is the normal callback API when your callback shape is simple and static.

### Direct-call requirement

`CreateFunction(...)` is generator-backed. The direct method call is intercepted at compile time and replaced with a generated marshalling adapter.

- Call `CreateFunction(...)` directly at the call site.
- Do not store `lua.CreateFunction` in another delegate and invoke it indirectly.
- There is no runtime fallback. If interception does not happen, the stub throws.

### What fits this API well

`CreateFunction(...)` is best for fixed signatures with:

- primitive numeric and boolean types,
- supported nullable value types,
- enums,
- `string` and span-based string or buffer parameters,
- `LuauValue`,
- borrowed callback views such as `LuauTableView` or `LuauFunctionView`,
- `void` or one managed return value.

The supported signature set is narrower than the library's overall type-conversion surface. If a delegate shape is not supported there, use `CreateFunctionBuilder(...)` instead. See [Type mapping](../concepts/type-mapping.md) for the broader conversion model.

## Choose between callback APIs

| Capability | `CreateFunction(...)` | `CreateFunctionBuilder(...)` |
| --- | --- | --- |
| Input shape | typed delegate | `LuauArgs` |
| Output shape | `void` or one managed return value | `LuauReturn.Ok(...)` / `LuauReturn.Error(...)` |
| Requirements | direct call, generator-backed | plain runtime API |
| Best for | simple fixed signatures | manual validation, multiple return values, unsupported signatures |

Prefer `CreateFunction(...)` unless you specifically need the extra control from `CreateFunctionBuilder(...)`.

## Use `CreateFunctionBuilder(...)` for manual callbacks

Use `CreateFunctionBuilder(...)` when you want to parse callback arguments yourself, return more than one value, or shape the user-facing error contract explicitly:

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

lua.Globals.Set("pair", pair);
```

`TryValidateArgumentCount(...)` checks minimum arity, not exact arity. If your callback requires an exact argument count, compare `args.ArgumentCount` yourself as well.

## Borrowed callback values

`LuauArgs` can return borrowed callback-scoped views such as `LuauFunctionView`, `LuauTableView`, `LuauStringView`, `LuauBufferView`, and `LuauUserdataView`.

Use them immediately, or promote them to owned references before the callback returns:

```csharp
using LuauFunction invokeCallback = lua.CreateFunctionBuilder(static args =>
{
    if (!args.TryReadLuauFunction(1, out LuauFunctionView callback, out string? error))
        return LuauReturn.Error(error);

    using LuauFunction owned = callback.ToOwned();
    owned.Invoke<LuauNil>();
    return LuauReturn.Ok();
});
```

Borrowed views are valid only while the current callback frame is active. See [Lifetimes and ownership](../concepts/lifetimes.md).

## Error behavior

Callback failures become normal Luau errors:

- argument conversion failures become Luau errors,
- `LuauReturn.Error(...)` produces a Luau error with your message,
- thrown exceptions are caught and surfaced as managed callback failures,
- `pcall(...)` can catch those errors on the Luau side.

## Signature guidance

- Keep callback signatures narrow and explicit.
- Make nullable behavior intentional.
- Prefer plain managed values for stable contracts.
- Use `CreateFunctionBuilder(...)` when you need fine-grained validation instead of hiding it behind a wide delegate signature.
