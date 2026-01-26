# Darp.Luau

Luau bindings and a higher level wrapper

## Why an other library
- Fully Native AOT compatible
- Execution of sync scripts
- Generation of type definitions (TODO)
- Support for UserData (TODO)
- Clean, safe and performant API with ref structs for Lifetime management of references
- Managed and typed function callbacks by using interceptors (TODO)
- Confidence through high unit test coverage (TODO)
- Proper error management (TODO)
- Module support (TODO)
- Async (Thread and Coroutines) support (MAYBE)

## Usage

```csharp
using var state = new LuaState();

// Set a callback
state.Globals.Set("log", (string s) => Console.WriteLine(s));

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

// Call a lua function
double result = state.Globals.GetFunction("add").Call<double, double, double>(1, 2);

// Set values
state.Globals.Set("my_string", "value");
state.Globals.Set("my_number", 1);
var myTable = state.CreateTable("my_table");
myTable.Set("my_number", 1);
myTable.Set("my_boolean", true);
myTable.Set("my_sequence", [1, 2, 3]);

TODO:

// Set values with dict syntax
state.Globals["my_string"] = "value";
state.Globals["my_number"] = 1;

// Get values safely
_ = state.Globals.TryGetTable("my_table", out var myTable)
    && myTable.TryGetInteger("my_number", out long myNumber)
    && myTable.TryGetBoolean("my_boolean", out bool myBool)
    && myTable.TryGetIntegerSequence("my_sequence", out long[] mySequence);
if (myTable.TryGetPairs(out var parisIterator))
{
    // Do something
}

// Get values unsafely
var myNumber = state.Globals["my_table"]["my_number"].GetInteger();
```
