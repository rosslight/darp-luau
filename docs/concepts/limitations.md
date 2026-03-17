# Limitations and current boundaries

Darp.Luau already covers a useful embedding core, but some parts of the surface are still intentionally narrow.

## Current boundaries

- The main package currently targets `net10.0`.
- `LuauState` executes source through `DoString(...)`. There is no `DoFile(...)` helper at the moment.
- `LuauState` is not thread-safe.
- Owned references and borrowed views are bound to a single `LuauState`; cross-state usage is invalid.
- `CreateFunction(...)` depends on generator interception, must be called directly, and has no runtime fallback.
- `LuauFunction.Invoke(...)` currently provides overloads for zero, one, or two arguments.
- File-backed `require(...)` is available through `EnableRequire()`, but it requires explicit setup and a matching chunk-name convention for file entrypoints.
- `EnableRequire()` currently expects modules to return exactly one value and not yield while loading.
- Detailed loader failures may surface through `LuauRequireByString.Context.LoadError` even when the Lua-visible error is generic.
- Managed interop is documented for strings, numbers, booleans, tables, functions, userdata, and buffers. Vector and thread values are not documented as managed interop surfaces yet.
- Higher-level async, coroutine orchestration, and thread-based host APIs are not documented as finished features.

## What this means in practice

- If you want file-based script loading, read the file in managed code and pass the contents to `DoString(...)`.
- If you want file-backed modules, call `EnableRequire()` and use an `@`-prefixed chunk name for the entry script.
- If you want callback signatures outside the supported `CreateFunction(...)` subset, use `CreateFunctionBuilder(...)`.
- If a `require(...)` call fails with a generic Lua error, inspect `LuauRequireByString.Context.LoadError` for the detailed loader message.
- If you need long-lived access to callback values, promote borrowed `*View` values to owned references before the callback returns.

## Expect change

These boundaries are not promises that the library will stay narrow forever. They are the parts that are documented and supported today.
