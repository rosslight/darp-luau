# Getting started

This page shows the shortest path from a new project to executing Luau code.

## Add the package

Add the package to your project with the normal .NET workflow:

```bash
dotnet add package Darp.Luau
```

## Create a state

`LuauState` owns the underlying Luau VM and acts as the main entry point.

```csharp
using Darp.Luau;

using var state = new LuauState();
```

By default the state enables all built-in libraries. If you want a more explicit setup, pass `LuauLibraries` flags to the constructor:

```csharp
using var state = new LuauState(LuauLibraries.Math | LuauLibraries.String);
```

`LuauLibraries.Minimal` is always added automatically, so the state still has the required base and table functionality.

## Execute a script

You can execute Luau from a file or from an inline string:

```csharp
state.DoString(
    """
    function add(a, b)
      return a + b
    end
    """
);

using LuauFunction add = state.Globals.GetLuauFunction("add");
double result = add.Invoke<double>(1, 2);
```

## Expose a callback

Managed callbacks can be inserted into globals or tables:

```csharp
state.Globals.Set("log", (string message) => Console.WriteLine(message));

state.DoString(
    """
    log("hello from luau")
    """
);
```

## Create and use a table

```csharp
LuauTable config = state.CreateTable();
config.Set("name", "Ada");
config.Set("score", 42);

state.Globals.Set("config", config);

using LuauTable roundTripped = state.Globals.GetLuauTable("config");
string name = roundTripped.GetUtf8String("name");
_ = roundTripped.TryGetNumber("score", out int score);
```

## What to read next

- [Lifetimes and ownership](concepts/lifetimes.md)
- [Functions](features/functions.md)
- [Tables](features/tables.md)
- [Userdata](features/userdata.md)
