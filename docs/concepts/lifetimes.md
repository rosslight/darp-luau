# Lifetimes and ownership

Darp.Luau makes lifetime rules explicit. Every Luau-backed value belongs to exactly one `LuauState`, and the API distinguishes between owned references, borrowed views, borrowed spans, and managed copies.

## The mental model

| Kind | Examples | Backing storage | Valid until |
| --- | --- | --- | --- |
| Owned references | `LuauTable`, `LuauFunction`, `LuauString`, `LuauBuffer`, `LuauUserdata`, reference-backed `LuauValue` | tracked registry reference | you dispose it, or the state is disposed |
| Borrowed views | `LuauTableView`, `LuauFunctionView`, `LuauStringView`, `LuauBufferView`, `LuauUserdataView`, `LuauArgs`, `LuauArgsSingle` | current callback stack frame | the callback returns |
| Borrowed spans | `ReadOnlySpan<byte>` from string or buffer reads | Luau-owned memory | only while the aliased memory stays valid |
| Managed copies | `string`, `byte[]`, numbers, booleans | managed memory | normal .NET lifetime |

`LuauState` is the outer lifetime boundary. Dispose the state and every wrapper from that state becomes invalid. You also cannot use a reference from one state in another state.

## Owned references

Owned references are the values you can keep after the current operation finishes.

- They are backed by registry references tracked by the state.
- They can outlive a callback.
- They should normally be wrapped in `using`.

Typical pattern:

```csharp
using LuauFunction add = lua.Globals.GetLuauFunction("add");
double value = add.Invoke<double>(1, 2);
```

`LuauValue` also participates in this model. If it represents `table`, `function`, `string`, `userdata`, or `buffer`, it owns a tracked reference and should be disposed.

That also applies to values returned from `InvokeMulti(...)` or `DoStringMulti(...)`: dispose each returned `LuauValue` when it may be reference-backed.

## Borrowed values

Types ending in `View`, plus `LuauArgs` and `LuauArgsSingle`, are callback-scoped.

That rule applies equally to manual callback surfaces such as `CreateFunctionBuilder(...)`, userdata hooks, and generated adapters behind `CreateFunction(...)`.

- Use them immediately.
- Do not store them in fields, collections, or across async boundaries.
- If you need to keep one, promote it with `ToOwned()` before the callback returns.

```csharp
using LuauFunction capture = lua.CreateFunctionBuilder(static args =>
{
    if (!args.TryReadLuauTable(1, out LuauTableView table, out string? error))
        return LuauReturn.Error(error);

    using LuauTable owned = table.ToOwned();
    return LuauReturn.Ok(owned.GetNumber("value"));
});
```

This example uses `CreateFunctionBuilder(...)` because it exposes `LuauArgs` directly, but the same ownership rule applies whenever a callback receives borrowed views.

If you use a borrowed view after the callback frame ends, the library throws `ObjectDisposedException`.

## Borrowed spans are still borrowed

Not every temporary value has a `View` suffix. `ReadOnlySpan<byte>` returned from APIs such as `TryGetUtf8String`, `TryGetBuffer`, `TryReadUtf8String`, `TryReadBuffer`, `LuauString.TryGet(out ReadOnlySpan<byte>)`, or `LuauBuffer.TryGet(out ReadOnlySpan<byte>)` aliases Luau memory.

Consume those spans immediately. If you need an independent lifetime, copy into a managed `string` or `byte[]`.

For the string- and buffer-specific API shapes that produce those spans, see [Strings](../features/strings.md) and [Buffers](../features/buffers.md).

## Promotion and move semantics

- `ToOwned()` creates a new owned registry reference from a borrowed view.
- `DisposeAndToLuauValue()` transfers ownership from an owned wrapper into a `LuauValue`.

```csharp
using LuauTable table = lua.CreateTable();
LuauValue value = table.DisposeAndToLuauValue();
```

After that call, `value` owns the reference. The original wrapper has been consumed and should not be used again.

If you later do `value.TryGet(out LuauTable tableCopy)`, you now have another owned wrapper and both `value` and `tableCopy` need to be disposed.

## Special cases

- `LuauState.Globals` is backed by a pinned global-table reference. Disposing one `Globals` wrapper does not destroy the global environment; `lua.Globals` can produce another wrapper later.
- The library rejects cross-state reference usage with `InvalidOperationException`.
- `LuauState` itself is not thread-safe.

## Practical rules

- Keep owned references in `using` blocks.
- Treat `*View` types and callback args from `CreateFunctionBuilder(...)`, userdata hooks, and other callback surfaces as immediate-use values.
- Copy spans if you need managed ownership.
- Promote with `ToOwned()` before caching or reusing a borrowed value outside the current callback.
- Dispose `LuauValue` when it may contain a reference-backed value.
