#define ENABLE_MANUAL_CAPACITY_UPGRADE

using System.IO;

namespace SharpTree;

public static class Constants
{
#if (ENABLE_MANUAL_CAPACITY_UPGRADE)
    public const int AverageNameLength = 8;
#endif
    public const int BufferSize = 1024 * 1024;
    public static readonly EnumerationOptions EnumOptions =
        new() { IgnoreInaccessible = true, RecurseSubdirectories = false };
    public const string OddLayerDirectoryColorString = "\u001b[0;36m";
    public const string EvenLayerDirectoryColorString = "\u001b[0;35m";
    public const string ColorResetString = "\u001b[0m";
}
