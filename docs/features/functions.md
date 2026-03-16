# Functions

Functions are one of the core interaction points between C# and Luau.

You will usually work with functions in two directions:

- call an existing Luau function from managed code,
- expose a managed callback so Luau can call into your code.

## Call Luau functions

Get a function from globals or a table, keep the owned reference for as long as needed, and invoke it with typed arguments:

```csharp
using LuauFunction add = state.Globals.GetLuauFunction("add");

double result = add.Invoke<double>(1, 2);
```

The generic return type controls how the Luau return value is converted.

## Register managed callbacks

You can place delegates into globals or tables:

```csharp
state.Globals.Set("log", (string message) => Console.WriteLine(message));
state.Globals.Set("sum", (int a, int b) => a + b);
```

This lets Luau scripts call managed logic while staying inside the same state.

## Error behavior

Function calls can fail for two broad reasons:

- Luau reported an execution error.
- The returned value could not be converted to the managed return type you requested.

Treat both as part of the contract of your host API.

## Borrowed function views

`LuauFunctionView` represents a callback-scoped borrowed function value. Use it immediately and do not cache it after the callback returns.

## Signature design

Keep delegate signatures simple and explicit.

- Prefer concrete managed types over overly generic callback surfaces.
- Make nullable behavior intentional.
- Avoid exposing signatures that hide lossy numeric conversion.
