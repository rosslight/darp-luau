# Contributing

This section is for people working on the repository itself rather than only consuming the package.

It mirrors the contributor navigation in the docs and is meant to answer a simple question: how do I make a change here without breaking the core guarantees of the library?

The project currently consists of two main areas:

- `src/Darp.Luau` for the runtime wrapper and public API.
- `src/Darp.Luau.Generator` for analyzer and generator support around callback creation.

## Start here

- [Local development](local-development.md)
- [Testing and validation](testing.md)

## What matters most when contributing

- Preserve lifetime and ownership guarantees.
- Keep examples and docs aligned with the real API surface.
- Treat analyzer and generator diagnostics as part of the user experience, not just implementation details.
