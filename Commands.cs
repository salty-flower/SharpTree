//#define ENABLE_MANUAL_CAPACITY_UPGRADE

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using ConsoleAppFramework;

namespace SharpTree;

public class Commands
{
#if (ENABLE_MANUAL_CAPACITY_UPGRADE)
    private const int averageNameLength = 8;
#endif
    private readonly StringBuilder sb = new();
    private static readonly EnumerationOptions enumOptions =
        new() { IgnoreInaccessible = true, RecurseSubdirectories = false };
    private int dirCount = 0;
    private int fileCount = 0;
    private int maxDepth = -1;
    private int currentDepth = 0;
    private bool includeFiles = false;
    private readonly Stopwatch stopwatch = new();

    /// <summary>
    /// Display a tree of the specified directory.
    /// </summary>
    /// <param name="path">-p, Root path of the tree</param>
    /// <param name="includeFiles">-f</param>
    /// <param name="maxDepth">-m</param>
    [Command("")]
    public void Tree(string path = ".", bool includeFiles = false, int maxDepth = -1)
    {
        this.maxDepth = maxDepth;
        this.includeFiles = includeFiles;
        path = Path.GetFullPath(path);
        InitializeOutput(path);
        stopwatch.Start();

        DisplayTree(path, "");

        FinalizeOutput();
        Console.Write(sb.ToString());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void InitializeOutput(string path) =>
        sb.AppendLine(
            $"Folder PATH listing for volume {Path.GetPathRoot(path)}\n"
                + $"Volume serial number is {GetVolumeSerial(path)}\n"
                + $"{path}\n"
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void FinalizeOutput() =>
        sb.AppendLine(
            $"\n{dirCount} Dir{(dirCount > 1 ? "(s)" : "")}, "
                + $"{fileCount} File{(fileCount > 1 ? "(s)" : "")}"
        );

    private void DisplayTree(string path, string indent)
    {
        if (maxDepth != -1 && currentDepth > maxDepth)
            return;

        MaybeFlushOutput();

        try
        {
            var thisLayerDirectories = Directory.GetDirectories(path, "*", enumOptions);
            var thisLayerFiles = Directory.GetFiles(path, "*", enumOptions);
            if (thisLayerDirectories.Length > 0)
            {
                DisplayDirectories(ref thisLayerDirectories, ref thisLayerFiles, indent);
            }
            if (includeFiles && thisLayerFiles.Length > 0)
            {
                DisplayFiles(ref thisLayerDirectories, ref thisLayerFiles, indent);
            }
        }
        catch (DirectoryNotFoundException)
        {
            sb.AppendLine($"{indent}....[Invalid Directory]");
        }
        catch (IOException)
        {
            sb.AppendLine($"{indent}....[Inaccessible]");
        }
    }

    private void DisplayDirectories(
        ref string[] thisLayerDirectories,
        ref string[] thisLayerFiles,
        string indent
    )
    {
#if (ENABLE_MANUAL_CAPACITY_UPGRADE)
        ManualCapacityUpgrade(
            thisLayerDirectories.Length + (includeFiles ? thisLayerFiles.Length : 0)
        );
#endif
        // Handle all but the last item
        for (int i = 0; i < thisLayerDirectories.Length - 1; i++)
        {
            var dir = thisLayerDirectories[i];
            sb.AppendLine($"{indent}├───{Path.GetFileName(dir)}");

            var subIndent = indent + "│   ";
            currentDepth++;
            DisplayTree(dir, subIndent);
            currentDepth--;
            dirCount++;
        }

        // Handle the last item separately
        if (thisLayerDirectories.Length > 0)
        {
            var lastDir = thisLayerDirectories[^1];
            sb.AppendLine($"{indent}└───{Path.GetFileName(lastDir)}");

            var subIndent = indent + "    ";
            currentDepth++;
            DisplayTree(lastDir, subIndent);
            currentDepth--;
            dirCount++;
        }
    }

#if (ENABLE_MANUAL_CAPACITY_UPGRADE)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ManualCapacityUpgrade(int itemCount) =>
        sb.EnsureCapacity(sb.Length + itemCount * averageNameLength);
#endif

    private void DisplayFiles(
        ref string[] thisLayerDirectories,
        ref string[] thisLayerFiles,
        string indent
    )
    {
#if (ENABLE_MANUAL_CAPACITY_UPGRADE)
        ManualCapacityUpgrade(
            thisLayerFiles.Length + (includeFiles ? thisLayerDirectories.Length : 0)
        );
#endif

        for (int i = 0; i < thisLayerFiles.Length - 1; i++)
        {
            var file = thisLayerFiles[i];
            sb.AppendLine($"{indent}├───{Path.GetFileName(file)}");
        }

        sb.AppendLine($"{indent}└───{Path.GetFileName(thisLayerFiles[^1])}");
        fileCount += thisLayerFiles.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void MaybeFlushOutput()
    {
        if (stopwatch.Elapsed.TotalSeconds > 1)
        {
            Console.Write(sb.ToString());
            sb.Clear();
            stopwatch.Restart();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string GetVolumeSerial(string path) =>
        new DriveInfo(Path.GetPathRoot(path) ?? "")?.VolumeLabel ?? "Unknown";
}
