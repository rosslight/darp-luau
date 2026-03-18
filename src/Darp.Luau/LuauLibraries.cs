using System.Diagnostics.CodeAnalysis;

namespace Darp.Luau;

/// <summary> Specifies the standard Luau libraries that can be enabled for a state. </summary>
[Flags]
public enum LuauLibraries
{
    /// <summary> The core base library. </summary>
    Base = 1 << 0,

    /// <summary> Coroutine utilities for cooperative multitasking. </summary>
    Coroutine = 1 << 1,

    /// <summary> Table manipulation helpers. </summary>
    Table = 1 << 2,

    /// <summary> Operating system related helpers. </summary>
    Os = 1 << 3,

    /// <summary> String processing functions. </summary>
    [SuppressMessage(
        "Naming",
        "CA1720:Identifier contains type name",
        Justification = "Matches the canonical Luau library name and preserves public API compatibility."
    )]
    String = 1 << 4,

    /// <summary> Mathematical functions and constants. </summary>
    Math = 1 << 5,

    /// <summary> Debugging helpers. </summary>
    Debug = 1 << 6,

    /// <summary> UTF-8 string utilities. </summary>
    Utf8 = 1 << 7,

    /// <summary> Bitwise operation helpers. </summary>
    Bit32 = 1 << 8,

    /// <summary> Binary buffer operations. </summary>
    Buffer = 1 << 9,

    /// <summary> Vector operations. </summary>
    Vector = 1 << 10,

    /// <summary> The minimal set of required libraries: <see cref="Base"/> and <see cref="Table"/>. </summary>
    Minimal = Base | Table,

    /// <summary> All available standard libraries. </summary>
    All = Base | Coroutine | Table | Os | String | Math | Debug | Utf8 | Bit32 | Buffer | Vector,
}
