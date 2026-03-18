# Execute Luau source

`LuauState` runs source code through the `DoString(...)` family.

Use these overloads depending on how you want to handle chunk return values:

- `DoString(...)` runs the chunk and ignores any return values.
- `DoString<TR>(...)` converts the first Luau return value to `TR`. Extra return values are ignored.
- `DoString<TR1, TR2>(...)`, `DoString<TR1, TR2, TR3>(...)`, and `DoString<TR1, TR2, TR3, TR4>(...)` convert the first 2, 3, or 4 return values to a tuple. Extra return values are ignored.
- `DoStringMulti(...)` returns every Luau return value as raw `LuauValue` instances.

Each shape has both `ReadOnlySpan<char>` and `ReadOnlySpan<byte>` overloads. Use the `char` overloads for normal .NET text and the `byte` overloads when you already have UTF-8 bytes and want to avoid an extra encoding step.

## Run a chunk without reading returns

```csharp
lua.DoString(
    """
    function add(a, b)
      return a + b
    end

    result = add(20, 22)
    """
);

double result = lua.Globals.GetNumber("result");
```

## Read typed chunk returns directly

```csharp
int answer = lua.DoString<int>("return 42");

(int total, int delta) = lua.DoString<int, int>("return 20 + 4, 20 - 4");
```

Typed chunk returns use the same conversion rules as other typed APIs.

- If the chunk returns more values than requested, extra values are ignored.
- If the chunk returns fewer values than requested, the call throws.
- If a return value cannot be converted to the requested managed type, the call throws `InvalidCastException`.

## Read all returns dynamically

Use `DoStringMulti(...)` when you want every return value without fixing the shape up front:

```csharp
LuauValue[] values = lua.DoStringMulti("return 10, 'hello', true");

using LuauValue numberValue = values[0];
using LuauValue textValue = values[1];
using LuauValue flagValue = values[2];

numberValue.TryGet(out int number, acceptNil: false);
textValue.TryGet(out string? text, acceptNil: false);
flagValue.TryGet(out bool flag, acceptNil: false);
```

Each `LuauValue` may own a registry reference. Dispose returned values when they may be reference-backed. See [Lifetimes and ownership](../concepts/lifetimes.md).

## Error behavior

- Luau runtime failures throw `LuaException`.
- Typed overloads throw if the requested return values cannot be read or converted.
- There is no file-based helper today; if you want `DoFile(...)` behavior, read the file in managed code and pass the contents to `DoString(...)`.
