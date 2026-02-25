# Darp.Luau

Luau bindings and a higher level wrapper

## Why an other library
- Fully Native AOT compatible
- Execution of sync scripts
- Generation of type definitions (TODO)
- Support for UserData
- Clean, safe and performant API with ref structs for Lifetime management of references
- Managed and typed function callbacks by using interceptors (TODO)
- Confidence through high unit test coverage (TODO)
- Proper error management (TODO)
- Module support (TODO)
- Async (Thread and Coroutines) support (MAYBE)

## Mapping of data types

### `nil`

- `LuauNil`
- `null` for parameters
- `void` for return types

### `string`

- `ReadOnlySpan<byte>`
- `ReadOnlySpan<char>`, `string` using UTF8 Encoding

### `number`

- `double`
- `byte, sbyte, ushort, short, uint, int, ulong, long, UInt128, Int128` are converted (cut off)
- `Half`, `float`, `decimal` are converted (loss of precision)
- Any user defined enum

### `boolean`

- `bool`

### `table`

- `LuauTable`

### `function`

- `LuauFunction`
- Delegates that with parameters of the primitives listed here

### `thread`

Unsupported for now

### `userdata`

Any `class` that implements `ILuauUserData<T>`

### `buffer`

- `ReadOnlySpan<byte>`
- `byte[]`

### `vector`

Unsupported for now. Planned:

- `System.Numerics.Vector4`
- `System.Numerics.Vector3` (loss of fourth dimension)

## Usage

### Script execution

Create a `LuauState`, optionally configure built-in libraries and custom libraries, then run Luau code from file or inline strings.

```csharp
using var state = new LuauState();

// Execute a script
state.DoFile("path/to_my_file.lua");
state.DoString(
    """
    function add(a, b)
      return a + b
    end
    log("hello from lua")
    """
);
```

### Functions

Get a Luau function reference from globals and call it with typed arguments and return values.

```csharp
// Add typed callbacks
state.Globals.Set("log", (string s) => Console.WriteLine(s));

// Get existing lua functions
using LuauFunction add = state.Globals.GetLuauFunction("add");

// Call
double result = add.Call<double>(1, 2);
```

### Tables

Create and populate tables, then read values via `Get*` (throwing) or `TryGet*` (non-throwing).

```csharp
// Create a new table
LuauTable myTable = state.CreateTable();

// Set values on the table
myTable.Set("my_number", 1);
myTable.Set("my_boolean", true);
myTable.Set("my_buffer", new byte[] { 1, 2, 3 });

// Access the global table
state.Globals.Set("my_string", "value");
state.Globals.Set("my_number", 1);
state.Globals.Set("my_table", myTable);

using LuauTable table = state.Globals.GetLuauTable("my_table");

// Get values from the table
double myNumber = table.GetNumber("my_number");
bool myBool = table.GetBoolean("my_boolean");
byte[] myBuffer = table.GetBuffer("my_buffer");
double? maybeNumber = table.GetNumberOrNil("maybe_number");

// Get values savely
_ = table.TryGetNumber("my_number", out int numberAsInt);
_ = table.TryGetUtf8StringOrNil("optional_name", out string? optionalName);
_ = table.TryGetBoolean("missing_flag", out _);
```

### Userdata

Store managed objects as userdata, expose selected fields to Luau, and resolve them back in C#.
Lua will interact with the managed object through the callbacks overwritten on the `ILuauUserData<T>` interface.

```csharp
// Create new userdata and link it to lua
var player = new PlayerUserdata { Name = "Ada", Score = 42 };
table.Set("player", player);

// Retrieve userdata from lua
PlayerUserdata samePlayer = table.GetUserdata<PlayerUserdata>("player");
_ = table.TryGetUserdataOrNil("maybe_player", out PlayerUserdata? maybePlayer);

using LuauUserdata playerRef = table.GetLuauUserdata("player");
_ = playerRef.TryGetManaged(out PlayerUserdata? resolvedPlayer, out string? error);

// Example definition of a userdata
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
        switch (fieldName)
        {
            case "score":
            {
                if (!args.TryReadNumber(out int score, out string? error))
                    return LuauOutcome.Error(error);
                self.Score = score;
                return LuauOutcome.Ok();
            }
            default:
                return LuauOutcome.NotHandledError;
        }
    }

    public static LuauReturn OnMethodCall(PlayerUserdata self, LuauArgs functionArgs, in ReadOnlySpan<char> methodName) =>
        LuauReturn.NotHandledError;

    public static implicit operator IntoLuau(PlayerUserdata value) => IntoLuau.FromUserdata(value);
}
```

### Library configuration

`LuauState` supports both built-in and custom libraries.

- Configure built-in Luau libraries via `LuauLibraries` in the constructor.
- Register custom libraries via `RegisterLibrary(...)`.
- `LuauLibraries.Minimal` (`Base | Table`) is always enabled automatically.

```csharp
using var state = new LuauState(LuauLibraries.Math | LuauLibraries.String);

state.OpenLibrary("game", static (_, in lib) =>
{
    lib.Set("answer", 42);
    lib.Set("add", (int a, int b) => a + b);
});
```
