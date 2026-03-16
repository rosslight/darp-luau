# Generator and analyzer support

The repository includes `Darp.Luau.Generator`, a Roslyn component that powers the higher-level `CreateFunction(...)` callback experience.

## What it does

`LuauState.CreateFunction(...)` is a compile-time surface, not a normal runtime implementation. The generator rewrites supported direct invocations into generated adapters that call `CreateFunctionBuilder(...)`.

In particular, the analyzer checks for unsupported usage patterns around `CreateFunction<TDelegate>(...)`, and the generator emits interceptor-related code to avoid problematic runtime paths.

## Why this matters

Callback-heavy APIs are easy to make convenient but hard to keep safe and AOT-friendly. The generator layer helps move some of that correctness work to compile time.

## Practical expectation

As a library consumer:

- call `CreateFunction(...)` directly at the call site,
- use `CreateFunctionBuilder(...)` when you need manual argument parsing, multiple return values, or a shape the generator does not support,
- treat diagnostics from this package as guidance that your delegate shape or invocation pattern needs to be adjusted.

If you are working on the library itself, this part of the codebase is where callback signature mapping and compile-time validation live.

## Scope today

This area is still evolving. Keep an eye on diagnostics and release notes before depending on advanced generator behavior as part of your public API guarantees.
