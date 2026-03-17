namespace Darp.Luau.Utils;

internal interface IReferenceSource
{
    LuauState ValidateInternal();
    PopDisposable PushToStack(out int stackIndex);
    PopDisposable PushToTop();
}
