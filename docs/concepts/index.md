# Concepts

These pages cover the mental models that matter most when using Darp.Luau.

The library is not just a thin set of P/Invoke calls. It makes opinionated choices about ownership, borrowed values, typed conversion, and error reporting. Those choices make the API safer, but they also shape how you should design your embedding surface.

Start with these three pages:

- [Lifetimes and ownership](lifetimes.md)
- [Type mapping](type-mapping.md)
- [Limitations and current boundaries](limitations.md)

Then use the feature pages for the concrete APIs built on top of those rules, especially [Strings](../features/strings.md), [Buffers](../features/buffers.md), callbacks, tables, userdata, libraries, and [Require](../features/require.md).
