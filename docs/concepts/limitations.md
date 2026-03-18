# Limitations and current boundaries

Darp.Luau already covers a useful embedding core, but some parts of the surface are still intentionally narrow.

## Current boundaries

- The main package currently targets `net10.0`.
- `LuauState` executes source through `DoString(...)`. There is no `DoFile(...)` helper at the moment.
- `LuauState` is not thread-safe.
- Owned references and borrowed views are bound to a single `LuauState`; cross-state usage is invalid.
- `CreateFunction(...)` depends on generator interception, must be called directly, and has no runtime fallback.
- `LuauFunction.Invoke(...)` currently accepts up to 4 arguments per call through `RefEnumerable<IntoLuau>`.
- Typed function returns currently have explicit overloads for 1, 2, or 3 values; use `InvokeMulti(...)` for dynamic multi-return access.
- Generator-backed `CreateFunction(...)` supports top-level tuple returns, but currently rejects nested tuples and only supports tuple arities that fit the current `LuauReturn.Ok(...)` overload set.
- File-backed `require(...)` is available through `EnableRequire()`, but it requires explicit setup and a matching chunk-name convention for file entrypoints.
- `EnableRequire()` currently expects modules to return exactly one value and not yield while loading.
- Detailed loader failures may surface through `LuauState.RequireContext.LoadError` even when the Lua-visible error is generic.
- Managed interop is documented for strings, numbers, booleans, tables, functions, userdata, and buffers. Vector and thread values are not documented as managed interop surfaces yet.
- Higher-level async, coroutine orchestration, and thread-based host APIs are not documented as finished features.

## What this means in practice

- If you want file-based script loading, read the file in managed code and pass the contents to `DoString(...)`.
- If you want file-backed modules, call `EnableRequire()` and use an `@`-prefixed chunk name for the entry script.
- If you want callback signatures outside the supported `CreateFunction(...)` subset, use `CreateFunctionBuilder(...)`.
- If you need more than the current typed `Invoke(...)` overload set, either compose around `InvokeMulti(...)` or add an explicit overload.
- If a `require(...)` call fails with a generic Lua error, inspect `LuauState.RequireContext.LoadError` for the detailed loader message.
- If you need long-lived access to callback values, promote borrowed `*View` values to owned references before the callback returns.

## Expect change

These boundaries are not promises that the library will stay narrow forever. They are the parts that are documented and supported today.
