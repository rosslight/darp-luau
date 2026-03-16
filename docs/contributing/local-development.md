# Local development

This page covers the basics for working on the repository locally.

## Prerequisites

- .NET SDK `10.0.101` or a compatible rolled-forward SDK, as pinned in `global.json`.
- Python plus `zensical` if you want to preview or build the docs site.
- Git.

## Repository layout

- `src/Darp.Luau` contains the main runtime library.
- `src/Darp.Luau.Generator` contains Roslyn analyzer and generator code.
- `tests/Darp.Luau.Tests` contains runtime-focused tests.
- `tests/Darp.Luau.Generator.Tests` contains generator-focused tests.
- `docs/` contains the documentation source published through GitHub Pages.

## Basic commands

Restore and build:

```bash
dotnet restore
dotnet build --no-restore
```

Run tests:

```bash
dotnet test --no-build --verbosity normal
```

Build docs:

```bash
zensical build --clean
```

## Formatting

The repo has a local tool manifest in `.config/dotnet-tools.json` with `csharpier` configured.

If you need it locally:

```bash
dotnet tool restore
dotnet csharpier .
```

## Working habits

- Prefer small, testable changes.
- Update docs when changing examples or behavior visible to consumers.
- Be careful with changes that affect disposal, reference tracking, borrowed views, or callback boundaries.

## Docs workflow

The docs site is built from `docs/` and configured through `zensical.toml`. GitHub Pages deployment is handled by `.github/workflows/docs.yml`.
