# Testing and validation

Contributions should be validated at the level they affect.

## Runtime changes

If you change the runtime wrapper in `src/Darp.Luau`, focus on:

- value conversion behavior,
- lifetime and disposal behavior,
- table, function, buffer, and userdata interactions,
- failure paths and exceptions.

The main runtime tests live in `tests/Darp.Luau.Tests`.

## Generator and analyzer changes

If you change `src/Darp.Luau.Generator`, validate both diagnostics and generated behavior.

The main generator tests live in `tests/Darp.Luau.Generator.Tests`.

## Main validation commands

Build:

```bash
dotnet build
```

Test:

```bash
dotnet test --verbosity normal
```

The CI workflow in `.github/workflows/build-test.yml` also collects Cobertura coverage output during test runs.

## Documentation validation

If you change docs or site configuration, build the docs site locally when possible:

```bash
zensical build --clean
```

This catches broken links, configuration mistakes, and stale template leftovers before they land in Pages.

## Review checklist

- Does the change preserve the public API contract?
- Do examples still match the actual method names and types?
- Are lifetime-sensitive values documented and tested correctly?
- If diagnostics changed, do tests cover the expected compiler feedback?
