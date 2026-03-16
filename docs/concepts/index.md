# Concepts

These pages cover the mental models that matter most when using Darp.Luau.

The library is not just a thin set of P/Invoke calls. It makes opinionated choices about ownership, borrowed values, typed conversion, and error reporting. Those choices are what make the API safer, but they also define how you should structure your code.

Start with:

- [Lifetimes and ownership](lifetimes.md)
- [Type mapping](type-mapping.md)
- [Limitations and roadmap notes](limitations.md)

Together, these pages define how to think about the library before you design a larger embedding surface around it.
