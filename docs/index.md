# Darp.Luau

Darp.Luau is a .NET wrapper around Luau with a strong focus on native AOT compatibility, typed access to Lua values, and predictable reference lifetimes.

This documentation is organized around the way you use the library in practice:

- Start a `LuauState` and decide which built-in libraries you want.
- Run scripts from strings or files.
- Move data between Luau and C# through functions, tables, buffers, and userdata.
- Understand which values are owned references and which values are borrowed views.

## What makes this library different

- Native AOT compatibility is a first-class goal.
- The API is typed and tries to surface conversion failures clearly.
- Lifetime-sensitive values are modeled explicitly instead of hiding ownership rules.
- Custom userdata and custom libraries let you expose managed behavior to Luau.

## Read this first

- [Getting started](getting-started.md) for the shortest path to a working script.
- [Concepts](concepts/index.md) for the mental models behind ownership, conversion, and current boundaries.
- [Functions](features/functions.md) for the first major API area.

## Sections

### Concepts

- [Concepts overview](concepts/index.md)
- [Lifetimes and ownership](concepts/lifetimes.md)
- [Type mapping](concepts/type-mapping.md)
- [Limitations and roadmap notes](limitations.md)

### Features

- [Functions](features/functions.md)
- [Tables](features/tables.md)
- [Userdata](features/userdata.md)
- [Libraries](features/libraries.md)
- [Generator and analyzer support](features/generator.md)

### Contributing

- [Contributing overview](contributing/index.md)
- [Local development](contributing/local-development.md)
- [Testing and validation](contributing/testing.md)

## Status

The repository is active and already has a broad test suite, but some capabilities are still clearly marked as planned or incomplete in `README.md`.

See [Limitations and roadmap notes](limitations.md) for the current boundaries and planned expansion areas.
