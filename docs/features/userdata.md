# Userdata

Userdata is the bridge for exposing managed objects to Luau without flattening everything into tables.

If a managed type implements `ILuauUserData<T>`, Darp.Luau can expose selected fields and methods through Luau callbacks.

## Typical use case

Use userdata when the script should interact with a real managed object that has identity, state, or behavior.

Examples:

- game entities,
- domain models,
- handles to services or subsystems,
- objects that should not be copied into plain tables.

## Define userdata behavior

Implement the static interface members on `ILuauUserData<T>`:

- `OnIndex` for member reads,
- `OnSetIndex` for member assignment,
- `OnMethodCall` for callable members.

```csharp
internal sealed class PlayerUserdata : ILuauUserData<PlayerUserdata>
{
    public required string Name { get; init; }
    public int Score { get; set; }

    public static LuauReturnSingle OnIndex(PlayerUserdata self, in LuauState state, in ReadOnlySpan<char> fieldName)
    {
        return fieldName switch
        {
            "name" => LuauReturnSingle.Ok(self.Name),
            "score" => LuauReturnSingle.Ok(self.Score),
            _ => LuauReturnSingle.NotHandled,
        };
    }

    public static LuauOutcome OnSetIndex(PlayerUserdata self, LuauArgsSingle args, in ReadOnlySpan<char> fieldName)
    {
        return LuauOutcome.NotHandledError;
    }

    public static LuauReturn OnMethodCall(PlayerUserdata self, LuauArgs functionArgs, in ReadOnlySpan<char> methodName) =>
        LuauReturn.NotHandledError;
}
```

## Store and retrieve userdata

```csharp
var player = new PlayerUserdata { Name = "Ada", Score = 42 };
table.Set("player", player);

PlayerUserdata samePlayer = table.GetUserdata<PlayerUserdata>("player");
```

## Design guidance

- Expose a small script-facing surface.
- Return `NotHandled` or `NotHandledError` for unknown members instead of silently inventing behavior.
- Keep userdata contracts stable even if the managed implementation evolves.

## Borrowed vs owned userdata wrappers

`LuauUserdata` is an owned reference.

`LuauUserdataView` is callback-scoped and should be treated as temporary.
