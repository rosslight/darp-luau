# Type mapping

Darp.Luau supports typed conversion between Luau values and managed types in both directions. Read-side conversions happen when you fetch values from tables, globals, or callback arguments. Write-side conversions happen when managed code pushes values into Luau, usually through `IntoLuau`.

## Core mappings

| Lua type | Common managed mappings | Notes |
| --- | --- | --- |
| `nil` | `LuauNil`, `null` in supported nullable cases, `void` returns | `nil` support depends on the target API surface |
| `string` | `string`, `ReadOnlySpan<char>`, `ReadOnlySpan<byte>`, `LuauString`, `LuauStringView` | `ReadOnlySpan<byte>` and borrowed views point at Luau memory; copy if you need an independent lifetime |
| `number` | `double`, integer types, floating-point types, enums | narrowing and truncation rules still apply |
| `boolean` | `bool` | straightforward mapping |
| `table` | `LuauTable`, `LuauTableView` | owned vs borrowed distinction matters |
| `function` | `LuauFunction`, `LuauFunctionView`, delegates | delegate support goes through generator-backed `CreateFunction(...)`; borrowed views are callback-scoped |
| `userdata` | `LuauUserdata`, `LuauUserdataView`, managed classes implementing `ILuauUserData<T>` | managed userdata is a core extensibility point |
| `buffer` | `byte[]`, `ReadOnlySpan<byte>`, `LuauBufferView` | `ReadOnlySpan<byte>` and borrowed views point at Luau memory; copy if you need an independent lifetime |

## Pushing values with `IntoLuau`

`IntoLuau` is the temporary carrier used by APIs that push managed values into Luau. Most of the time you do not construct it yourself; you rely on implicit conversions at the call site.

You encounter it when:

- setting table or global values,
- passing arguments to `LuauFunction.Invoke(...)`,
- returning values from `LuauReturn.Ok(...)` or `LuauReturnSingle.Ok(...)`.

```csharp
table.Set("name", "Ada");

double result = add.Invoke<double>(1, 2);

return LuauReturn.Ok("ok", 42);
```

`IntoLuau` is a `ref struct` and intentionally temporary. Treat it as a call-site conversion type, not something to cache or store.

### Custom conversions

Your own types can participate by defining `implicit operator IntoLuau`.

For primitive-style wrappers, forward to an existing supported value:

```csharp
public readonly record struct UserId(int Value)
{
    public static implicit operator IntoLuau(UserId value) => value.Value;
}
```

For managed userdata, forward to `IntoLuau.FromUserdata(...)`.

### Ownership and lifetime when pushing

- Plain managed values such as `string`, `bool`, `int`, `double`, and `byte[]` push copied data.
- Passing `LuauTable`, `LuauFunction`, `LuauString`, `LuauBuffer`, `LuauUserdata`, or `LuauValue` reuses the existing Luau-backed value instead of creating another tracked reference.
- Passing borrowed `*View` values into `IntoLuau` keeps the same callback-frame lifetime constraints. See [lifetimes and ownership](lifetimes.md).
- Reference-backed values still belong to one `LuauState`; cross-state usage is invalid.

### String and nil behavior

- `string` uses `null` to mean `nil`.
- `string.Empty` pushes an empty Luau string.
- `ReadOnlySpan<char>` currently treats an empty span as `nil`, so prefer `string` when empty string and `nil` need to stay distinct.

## Numeric conversions

Luau numbers are flexible, but your target managed type may not be.

- Converting to integral types can truncate values.
- Converting to smaller floating-point types can lose precision.
- Enum conversion is supported through the underlying numeric value.

If numeric range and precision matter in your application, document that in your own higher-level API instead of relying on implicit assumptions.

## Nullability

Some nullable managed types map naturally to `nil`, but support is not universal across every type shape or callback surface.

For delegate-based callbacks, the generator validates direct `CreateFunction(...)` usages and reports unsupported type combinations at compile time where possible. If you need a shape it does not support, use `CreateFunctionBuilder(...)` and read `LuauArgs` manually.

On the write side, supported nullable `IntoLuau` conversions also use `nil` when the managed value is `null`.

## Choosing between rich wrappers and plain values

Use plain managed values when you want convenience and stability:

- `string`
- `int`
- `double`
- `bool`
- `byte[]`

Use Luau wrapper types when you want control over lifetime, delayed reads, or direct interaction with Luau values:

- `LuauTable`
- `LuauFunction`
- `LuauUserdata`
- `LuauString`
- `LuauValue`

## Failure modes

Conversions can fail when:

- the Lua value has the wrong runtime type,
- the target managed type is unsupported,
- a borrowed value is used after its frame ends,
- a reference-backed value is pushed into the wrong `LuauState`.

Prefer explicit, narrow APIs in your own code instead of exposing many implicit conversion paths at once.
