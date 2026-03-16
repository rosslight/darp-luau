# Type mapping

Darp.Luau supports typed conversion between Luau values and managed types. The goal is to make common cases easy while still making conversion boundaries explicit.

## Core mappings

| Lua type | Common managed mappings | Notes |
| --- | --- | --- |
| `nil` | `LuauNil`, `null` in supported nullable cases, `void` returns | `nil` support depends on the target API surface |
| `string` | `string`, `ReadOnlySpan<char>`, `ReadOnlySpan<byte>`, `LuauString`, `LuauStringView` | borrowed string views are callback-scoped |
| `number` | `double`, integer types, floating-point types, enums | narrowing and truncation rules still apply |
| `boolean` | `bool` | straightforward mapping |
| `table` | `LuauTable`, `LuauTableView` | owned vs borrowed distinction matters |
| `function` | `LuauFunction`, `LuauFunctionView`, delegates | borrowed views are callback-scoped |
| `userdata` | `LuauUserdata`, `LuauUserdataView`, managed classes implementing `ILuauUserData<T>` | managed userdata is a core extensibility point |
| `buffer` | `byte[]`, `ReadOnlySpan<byte>`, `LuauBufferView` | borrowed buffer views are callback-scoped |

## Numeric conversions

Luau numbers are flexible, but your target managed type may not be.

- Converting to integral types can truncate values.
- Converting to smaller floating-point types can lose precision.
- Enum conversion is supported through the underlying numeric value.

If numeric range and precision matter in your application, document that in your own higher-level API instead of relying on implicit assumptions.

## Nullability

Some nullable managed types map naturally to `nil`, but support is not universal across every type shape.

The generator and analyzer layer also validates delegate signatures and reports unsupported type combinations at compile time where possible.

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
- `LuauStringView`

## Failure modes

Conversions can fail when:

- the Lua value has the wrong runtime type,
- the target managed type is unsupported,
- a borrowed value is used after its frame ends.

Prefer explicit, narrow APIs in your own code instead of exposing many implicit conversion paths at once.
