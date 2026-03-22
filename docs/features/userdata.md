# Userdata

Userdata lets Luau hold onto a real managed object instead of flattening it into a table.

Use it when the script should interact with identity, mutable state, or host-defined behavior.

Typical examples include game entities, domain objects, service handles, and other values that should round-trip back to the same managed instance.

## The three userdata surfaces

In Darp.Luau, userdata usually shows up in three forms:

- `ILuauUserData<T>` defines the script-facing behavior for your managed type.
- `LuauUserdata` is an owned userdata reference that you can keep and dispose.
- `LuauUserdataView` is a borrowed callback-scoped userdata view.

That split matters:

- use the managed type when you want direct access to your own object,
- use `LuauUserdata` when you want an owned Luau wrapper,
- use `LuauUserdataView` only inside the current callback frame, or promote it with `ToOwned()` first.

## Define userdata behavior

Implement the static members on `ILuauUserData<T>` to decide what Luau can do with your object:

| Hook | Luau syntax | Receives | Unknown member behavior |
| --- | --- | --- | --- |
| `OnIndex` | `player.name` | `self`, `LuauState`, member name | return `LuauReturnSingle.NotHandled` and Luau sees `nil` |
| `OnSetIndex` | `player.score = 10` | `self`, `LuauArgsSingle`, member name | return `LuauOutcome.NotHandledError` and Luau gets an unknown-member error |
| `OnMethodCall` | `player:add(1)` | `self`, `LuauArgs`, method name | return `LuauReturn.NotHandledError` and Luau gets an unknown-method error |

These hooks are manual callback surfaces, closer to `CreateFunctionBuilder(...)` than to `CreateFunction(...)`: you read arguments yourself and return `LuauReturn*` or `LuauOutcome` values explicitly.

For `player:add(1)`, `self` is already passed separately, so `functionArgs` contains only the actual method arguments.

```csharp
internal sealed class PlayerUserdata : ILuauUserData<PlayerUserdata>
{
    public required string Name { get; init; }
    public int Score { get; private set; }

    public static LuauReturnSingle OnIndex(PlayerUserdata self, in LuauState state, in ReadOnlySpan<char> fieldName) =>
        fieldName switch
        {
            "name" => LuauReturnSingle.Ok(self.Name),
            "score" => LuauReturnSingle.Ok(self.Score),
            _ => LuauReturnSingle.NotHandled,
        };

    public static LuauOutcome OnSetIndex(PlayerUserdata self, LuauArgsSingle args, in ReadOnlySpan<char> fieldName)
    {
        switch (fieldName)
        {
            case "score":
                if (!args.TryReadNumber(out int score, out string? error))
                    return LuauOutcome.Error(error);

                self.Score = score;
                return LuauOutcome.Ok();
            default:
                return LuauOutcome.NotHandledError;
        }
    }

    public static LuauReturn OnMethodCall(
        PlayerUserdata self,
        LuauArgs functionArgs,
        in ReadOnlySpan<char> methodName
    )
    {
        switch (methodName)
        {
            case "add":
                if (!functionArgs.TryValidateArgumentCount(1, out string? error))
                    return LuauReturn.Error(error);
                if (functionArgs.ArgumentCount != 1)
                    return LuauReturn.Error("Expected exactly 1 argument.");
                if (!functionArgs.TryReadNumber(1, out int amount, out error))
                    return LuauReturn.Error(error);

                self.Score += amount;
                return LuauReturn.Ok(self.Score);
            default:
                return LuauReturn.NotHandledError;
        }
    }

    public static implicit operator IntoLuau(PlayerUserdata value) => IntoLuau.FromUserdata(value);
}
```

## Expose and retrieve userdata

```csharp
var player = new PlayerUserdata { Name = "Ada" };

lua.Globals.Set("player", player);

lua.Load(
    """
    player.score = 41
    result = player:add(1)
    """
).Execute();

PlayerUserdata samePlayer = lua.Globals.GetUserdata<PlayerUserdata>("player");

using LuauUserdata playerRef = lua.Globals.GetLuauUserdata("player");
_ = playerRef.TryGetManaged(out PlayerUserdata? resolvedPlayer, out string? error);
```

The implicit conversion operator is optional but convenient. If you do not define it on your managed type, use `IntoLuau.FromUserdata(player)` at the call site instead.

If you want to keep the Lua userdata wrapper itself, call `lua.GetOrCreateUserdata(player)` and hold the resulting `LuauUserdata` in a `using` block.

Choose the read API that matches what you need:

| Need | API |
| --- | --- |
| Resolve directly to a managed userdata instance | `GetUserdata<T>`, `TryGetUserdata<T>` |
| Accept missing or `nil` | `GetUserdataOrNil<T>`, `TryGetUserdataOrNil<T>` |
| Keep a generic owned userdata wrapper | `GetLuauUserdata`, `TryGetLuauUserdata` |
| Resolve an owned or borrowed userdata wrapper back to a managed instance | `LuauUserdata.TryGetManaged<T>`, `LuauUserdataView.TryGetManaged<T>` |

`GetUserdata<T>` and `TryGetManaged<T>` only succeed for managed userdata created by this library and matching `T`. Generic `LuauUserdata` wrappers can still represent other userdata values, but they will not resolve back to your managed type.

The same split exists inside callbacks:

- `args.TryReadUserdata<T>(...)` reads the managed instance directly.
- `args.TryReadLuauUserdata(...)` reads a borrowed `LuauUserdataView`.

## Error behavior

Userdata hooks participate in normal Luau error handling:

- return `LuauReturn.Error(...)` or `LuauOutcome.Error(...)` for expected user-facing failures,
- return `NotHandled` or `NotHandledError` for unknown members,
- let exceptions bubble only for truly exceptional failures.

Thrown exceptions become Luau errors too, including inside `pcall(...)`.

Methods can return zero, one, or many values through `LuauReturn.Ok(...)`.

## Identity and lifetime

Managed userdata keeps object identity:

- pushing the same managed instance into the same `LuauState` again reuses the same Lua userdata identity while that userdata is still alive,
- pushing two different managed instances creates two different Lua userdata values even if their contents match.

Lifetime rules follow the normal owned-vs-borrowed model:

- `LuauUserdata` is an owned reference; keep it in a `using` block.
- `LuauUserdataView` is callback-scoped and temporary.
- `LuauArgs`, `LuauArgsSingle`, and other `*View` values in userdata hooks are also callback-scoped.
- call `ToOwned()` before storing or reusing a borrowed userdata value outside the current callback.

See [Lifetimes and ownership](../concepts/lifetimes.md) for the broader ownership model.

## Design guidance

- Expose a small, stable script-facing surface instead of mirroring your full managed type.
- Keep validation and error messages intentional inside the hooks.
- Prefer tables for plain data and userdata for identity or behavior.
