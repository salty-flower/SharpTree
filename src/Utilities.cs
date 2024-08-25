#define ENABLE_MANUAL_CAPACITY_UPGRADE

using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace SharpTree;

public partial class TreeCommand
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void InitializeOutput(string path) =>
        sb.AppendLine(
            $"Folder PATH listing for volume {Path.GetPathRoot(path)}\n"
                + $"Volume serial number is {GetVolumeSerial(path)}\n{path}"
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void FinalizeOutput() =>
        sb.AppendLine(
            $"\n{dirCount} Dir{(dirCount > 1 ? "(s)" : "")}, "
                + $"{fileCount} File{(fileCount > 1 ? "(s)" : "")}"
        );

#if (ENABLE_MANUAL_CAPACITY_UPGRADE)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ManualCapacityUpgrade(int itemCount, int indentLength) =>
        sb.Capacity = Math.Max(
            sb.Capacity,
            sb.Length + itemCount * (Constants.AverageNameLength + indentLength)
        );
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string GetVolumeSerial(string path) =>
        new DriveInfo(Path.GetPathRoot(path) ?? "")?.VolumeLabel ?? "Unknown";
}
