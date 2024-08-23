using System;
using System.IO;
using System.Linq;
using System.Text;
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
        var sb = new StringBuilder();
        sb.AppendLine($"Folder PATH listing for volume {Path.GetPathRoot(path)}");
        sb.AppendLine($"Volume serial number is {GetVolumeSerial(path)}");
        sb.AppendLine(path);
        sb.AppendLine();

        var (dirCount, fileCount) = DisplayTree(ref sb, path, "", directoriesOnly, maxDepth);

        sb.AppendLine();
        sb.AppendLine($"{dirCount} Dir(s), {fileCount} File(s)");
        Console.Write(sb.ToString());
    }

    private static (int dirCount, int fileCount) DisplayTree(
        ref StringBuilder sb,
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
            sb.AppendLine($"{indent}{connector}{item.Name}");
            switch (item)
            {
                case DirectoryInfo dir:
                {
                    var subIndent = indent + (isLast ? "    " : "│   ");
                    var (subDirCount, subFileCount) = DisplayTree(
                        ref sb,
                        dir.FullName,
                        subIndent,
                        directoriesOnly,
                        maxDepth,
                        currentDepth + 1
                    );
                    dirCount += subDirCount + 1;
                    fileCount += subFileCount;
                    break;
                }

                default:
                    fileCount++;
                    break;
            }
        }

        return (dirCount, fileCount);
    }

    private static string GetVolumeSerial(string path) =>
        DriveInfo.GetDrives().FirstOrDefault(d => d.Name == Path.GetPathRoot(path))?.VolumeLabel
        ?? "Unknown";
}
