# Require

`EnableRequire()` installs Luau's file-backed `require(...)` support into a `LuauState`.

Use it when you want Luau scripts to load other `.luau` or `.lua` modules from disk.

## Enable `require(...)`

```csharp
using System.Text;
using Darp.Luau;

using var lua = new LuauState();
lua.EnableRequire();

string path = Path.GetFullPath("scripts/main.luau");
lua.DoString(File.ReadAllBytes(path), "@" + path);
```

The require context is owned by `LuauState` and remains available while the state is alive.

## Entry chunk names

`require(...)` only works when the requiring chunk has a recognized chunk name:

- `=stdin` for managed source that acts like a stdin entrypoint,
- `@<absolute-or-relative-path>` for source that should resolve modules from a file location.

For file-backed entry scripts, pass a chunk name that starts with `@` and points at the script path.

## Resolution rules

`EnableRequire()` follows Luau's require-by-string rules covered by this package:

- require paths must start with `./`, `../`, or `@alias`,
- relative paths resolve from the requiring file,
- module lookup checks `.luau`, `.lua`, `init.luau`, and `init.lua`,
- `.luaurc` and `.config.luau` aliases are supported,
- module results are cached by absolute file path.

## Module behavior

- each module must return exactly one value,
- modules cannot yield while loading,
- returned values can be any Luau value that Darp.Luau can surface.

## Error reporting

If module loading fails, the Lua-visible error may be generic in some cases, such as `module must return a single value`.

Check `lua.RequireContext?.LoadError` for the loader's detailed message after a failed `require(...)`.

## Relationship to `OpenLibrary(...)`

`OpenLibrary(...)` and `EnableRequire()` solve different problems:

- `OpenLibrary(...)` registers a host-provided global table,
- `EnableRequire()` enables filesystem-backed module loading for Luau scripts.

Use `OpenLibrary(...)` for managed APIs and `EnableRequire()` for script modules.
