# Generator and analyzer support

The repository includes `Darp.Luau.Generator`, a Roslyn component that supports the higher-level callback experience.

## What it does

The generator and analyzer layer exists to validate and optimize `CreateFunction` usage.

In particular, the analyzer checks for unsupported usage patterns around `CreateFunction<TDelegate>(...)`, and the generator emits interceptor-related code to avoid problematic runtime paths.

## Why this matters

Callback-heavy APIs are easy to make convenient but hard to keep safe and AOT-friendly. The generator layer helps move some of that correctness work to compile time.

## Practical expectation

As a library consumer, you should treat diagnostics from this package as guidance that your delegate shape or invocation pattern needs to be adjusted.

If you are working on the library itself, this part of the codebase is where callback signature mapping and compile-time validation live.

## Scope today

This area is still evolving. Keep an eye on diagnostics and release notes before depending on advanced generator behavior as part of your public API guarantees.
