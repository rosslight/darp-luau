# Lifetimes and ownership

This is the most important concept in Darp.Luau.

Some values are owned references that stay valid until you dispose them. Others are borrowed views that are only valid while a callback frame is still active.

## Owned references

Types such as `LuauTable`, `LuauFunction`, and `LuauUserdata` represent tracked references owned by your code.

These values:

- can outlive a single callback,
- usually wrap a registry-tracked reference inside the state,
- should be disposed when you no longer need them.

Typical pattern:

```csharp
using LuauFunction add = state.Globals.GetLuauFunction("add");
double value = add.Invoke<double>(1, 2);
```

## Borrowed values

Types ending in `View`, such as `LuauTableView`, `LuauFunctionView`, `LuauBufferView`, `LuauStringView`, and `LuauUserdataView`, are borrowed values.

These values are tied to the active callback frame. They do not own a registry reference and should not be cached beyond the callback that produced them.

If you use them after the callback frame ends, the library throws `ObjectDisposedException`.

## Why this matters

Luau values can live on the stack, in the registry, or in managed wrappers. Darp.Luau makes that distinction visible so that lifetime bugs fail early instead of silently corrupting behavior.

## Practical rules

- Use `using` with owned references when possible.
- Treat `*View` types as temporary.
- Avoid storing borrowed values on fields, in long-lived collections, or across async boundaries.
- If an API returns a non-view wrapper like `LuauTable`, you own that reference and should eventually dispose it.

## Callback guidance

When writing callbacks that receive `LuauArgs`, `LuauArgsSingle`, or borrowed views:

- read what you need inside the callback,
- convert to managed values quickly,
- return owned values only when the API explicitly creates them for you.

## Common mistake

Do not assume a callback-scoped value can be reused later just because it is a struct. In this library, the wrapper shape does not mean the underlying Luau value is long-lived.
