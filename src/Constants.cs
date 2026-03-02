using System.IO;

namespace SharpTree;

public static class Constants
{
    public const int BufferSize = 1024 * 1024;
    public static readonly EnumerationOptions EnumOptions =
        new() { IgnoreInaccessible = true, RecurseSubdirectories = false, BufferSize = 16384 };
}
