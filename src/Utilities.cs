using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace SharpTree;

public partial class TreeCommand
{
    private void WriteHeader(string path)
    {
        WriteChars($"Folder PATH listing for volume {Path.GetPathRoot(path)}");
        WriteNewline();
        WriteChars($"Volume serial number is {GetVolumeSerial(path)}");
        WriteNewline();
        WriteChars(path);
        WriteNewline();
    }

    private void WriteFooter()
    {
        WriteNewline();
        WriteChars(
            $"{dirCount} Dir{(dirCount > 1 ? "(s)" : "")}, {fileCount} File{(fileCount > 1 ? "(s)" : "")}"
        );
        WriteNewline();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string GetVolumeSerial(string path) =>
        new DriveInfo(Path.GetPathRoot(path) ?? "")?.VolumeLabel ?? "Unknown";
}
