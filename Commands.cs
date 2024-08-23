#define ENABLE_MANUAL_CAPACITY_UPGRADE

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using ConsoleAppFramework;

namespace SharpTree;

public class Commands
{
#if (ENABLE_MANUAL_CAPACITY_UPGRADE)
    private const int averageNameLength = 8;
#endif
    private const int bufferSize = 1024 * 1024;
    private readonly StringBuilder sb = new();
    private static readonly EnumerationOptions enumOptions =
        new() { IgnoreInaccessible = true, RecurseSubdirectories = false };
    private int dirCount = 0;
    private int fileCount = 0;
    private int maxDepth = -1;
    private int currentDepth = 0;
    private bool includeFiles = false;
    private int flushInterval = 500;
    private readonly Stopwatch stopwatch = new();
    private BufferedStream bufferedOutput = null!;

    /// <summary>
    /// Display a tree of the specified directory.
    /// </summary>
    /// <param name="path">-p, Root path of the tree</param>
    /// <param name="includeFiles">-f</param>
    /// <param name="maxDepth">-m</param>
    /// <param name="flushInterval">-t, Flush output to stdout every x ms. -1 to disable.</param>
    [Command("")]
    public async Task Tree(
        string path = ".",
        bool includeFiles = false,
        int maxDepth = -1,
        int flushInterval = 1
    )
    {
        this.maxDepth = maxDepth;
        this.includeFiles = includeFiles;
        this.flushInterval = flushInterval;
        path = Path.GetFullPath(path);

        using (bufferedOutput = new BufferedStream(Console.OpenStandardOutput(), bufferSize))
        {
            InitializeOutput(path);
            stopwatch.Start();

            await DisplayTreeAsync(path, "");

            FinalizeOutput();
            await WriteBufferAsync();
        }
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

    private async Task DisplayTreeAsync(string path, string indent)
    {
        if (maxDepth != -1 && currentDepth > maxDepth)
            return;

        await MaybeFlushOutputAsync();

        try
        {
            var thisLayerDirectories = Directory.GetDirectories(path, "*", enumOptions);
            var thisLayerFiles = Directory.GetFiles(path, "*", enumOptions);

#if (ENABLE_MANUAL_CAPACITY_UPGRADE)
            ManualCapacityUpgrade(
                thisLayerDirectories.Length + (includeFiles ? thisLayerFiles.Length : 0)
            );
#endif

            if (thisLayerDirectories.Length > 0)
            {
                await DisplayDirectoriesAsync(thisLayerDirectories, thisLayerFiles, indent);
            }
            if (includeFiles && thisLayerFiles.Length > 0)
            {
                DisplayFiles(thisLayerFiles, indent);
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

    private async Task DisplayDirectoriesAsync(
        string[] thisLayerDirectories,
        string[] _,
        string indent
    )
    {
        // Handle all but the last item
        for (int i = 0; i < thisLayerDirectories.Length - 1; i++)
        {
            var dir = thisLayerDirectories[i];
            sb.AppendLine($"{indent}├───{Path.GetFileName(dir)}");

            var subIndent = indent + "│   ";
            currentDepth++;
            await DisplayTreeAsync(dir, subIndent);
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
            await DisplayTreeAsync(lastDir, subIndent);
            currentDepth--;
            dirCount++;
        }
    }

#if (ENABLE_MANUAL_CAPACITY_UPGRADE)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ManualCapacityUpgrade(int itemCount) =>
        sb.EnsureCapacity(sb.Length + itemCount * averageNameLength);
#endif

    private void DisplayFiles(string[] thisLayerFiles, string indent)
    {
        for (int i = 0; i < thisLayerFiles.Length - 1; i++)
        {
            var file = thisLayerFiles[i];
            sb.AppendLine($"{indent}├───{Path.GetFileName(file)}");
        }

        sb.AppendLine($"{indent}└───{Path.GetFileName(thisLayerFiles[^1])}");
        fileCount += thisLayerFiles.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async Task MaybeFlushOutputAsync()
    {
        if (flushInterval != -1 && stopwatch.Elapsed.TotalMilliseconds > flushInterval)
        {
            await WriteBufferAsync();
            sb.Clear();
            stopwatch.Restart();
        }
    }

    private async Task WriteBufferAsync()
    {
        var buffer = sb.ToString();
        await bufferedOutput.WriteAsync(Encoding.UTF8.GetBytes(buffer));
        await bufferedOutput.FlushAsync();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string GetVolumeSerial(string path) =>
        new DriveInfo(Path.GetPathRoot(path) ?? "")?.VolumeLabel ?? "Unknown";
}
