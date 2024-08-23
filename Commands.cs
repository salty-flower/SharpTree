using System;
using System.IO;
using System.Linq;
using ConsoleAppFramework;

namespace SharpTree;

public static class Commands
{
    /// <summary>
    /// Display a tree of the specified directory.
    /// </summary>
    /// <param name="path">-p, Root path of the tree</param>
    /// <param name="directoriesOnly">-d</param>
    /// <param name="maxDepth">-m</param>
    [Command("tree")]
    public static void Tree(string path = ".", bool directoriesOnly = false, int maxDepth = -1)
    {
        path = Path.GetFullPath(path);
        Console.WriteLine($"Folder PATH listing for volume {Path.GetPathRoot(path)}");
        Console.WriteLine($"Volume serial number is {GetVolumeSerial(path)}");
        Console.WriteLine(path);
        Console.WriteLine();

        var (dirCount, fileCount) = DisplayTree(path, "", directoriesOnly, maxDepth);

        Console.WriteLine();
        Console.WriteLine($"{dirCount} Dir(s), {fileCount} File(s)");
    }

    private static (int dirCount, int fileCount) DisplayTree(
        string path,
        string indent,
        bool directoriesOnly,
        int maxDepth,
        int currentDepth = 0
    )
    {
        if (maxDepth != -1 && currentDepth > maxDepth)
            return (0, 0);

        var dirCount = 0;
        var fileCount = 0;

        var items = new DirectoryInfo(path)
            .GetFileSystemInfos()
            .Where(item => !directoriesOnly || item is DirectoryInfo)
            .OrderBy(item => item is DirectoryInfo ? 0 : 1)
            .ThenBy(item => item.Name);

        foreach (
            var (item, isLast) in items.Select((item, index) => (item, index == items.Count() - 1))
        )
        {
            var connector = isLast ? "└───" : "├───";
            Console.WriteLine($"{indent}{connector}{item.Name}");

            if (item is DirectoryInfo dir)
            {
                var subIndent = indent + (isLast ? "    " : "│   ");
                var (subDirCount, subFileCount) = DisplayTree(
                    dir.FullName,
                    subIndent,
                    directoriesOnly,
                    maxDepth,
                    currentDepth + 1
                );
                dirCount += subDirCount + 1;
                fileCount += subFileCount;
            }
            else
            {
                fileCount++;
            }
        }

        return (dirCount, fileCount);
    }

    private static string GetVolumeSerial(string path) =>
        DriveInfo.GetDrives().FirstOrDefault(d => d.Name == Path.GetPathRoot(path))?.VolumeLabel
        ?? "Unknown";
}
