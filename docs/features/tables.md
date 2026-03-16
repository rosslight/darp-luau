# Tables

Tables are the main general-purpose data structure in Luau, and Darp.Luau gives you both convenient accessors and direct wrappers for them.

## Create a table

```csharp
using LuauTable table = state.CreateTable();
table.Set("name", "Ada");
table.Set("score", 42);
```

You can then store that table in globals or inside other tables.

## Read values

The API provides both throwing and non-throwing access patterns.

```csharp
string name = table.GetUtf8String("name");
_ = table.TryGetNumber("score", out int score);

if (table.TryGetBoolean("enabled", out bool enabled))
{
    // use enabled
}
```

Use `Get*` methods when a value is required and missing data should fail fast. Use `TryGet*` methods when the data shape is optional or external.

## Global table

`LuauState.Globals` is just a table wrapper over the Lua global environment, so the same table patterns apply there too.

## List-like behavior

Some table APIs treat a table as a list. That can be useful, but remember that sparse tables and tables with holes can behave differently than dense arrays.

## Borrowed table views

`LuauTableView` is a borrowed wrapper used inside callback scenarios. It follows the same lifetime rules as other `*View` types.

## Recommendation

Use tables for script-facing configuration and state, but keep your public managed API more structured than your raw Lua table layout. That makes versioning and validation easier.
