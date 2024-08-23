using System;
using System.IO;
using System.Linq;
using System.Text;
using ConsoleAppFramework;

namespace SharpTree;

public class Commands
{
    private readonly StringBuilder sb = new();
    private static readonly EnumerationOptions enumOptions = new() { IgnoreInaccessible = true };
    private int dirCount = 0;
    private int fileCount = 0;
    private int maxDepth = -1;
    private int currentDepth = 0;
    private bool includeFiles = false;
    private DateTime lastFlush = DateTime.Now;

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
        sb.AppendLine($"Folder PATH listing for volume {Path.GetPathRoot(path)}");
        sb.AppendLine($"Volume serial number is {GetVolumeSerial(path)}");
        sb.AppendLine(path);
        sb.AppendLine();

        DisplayTree(path, "");

        sb.AppendLine();
        sb.AppendLine($"{dirCount} Dir(s), {fileCount} File(s)");
        Console.Write(sb.ToString());
    }

    private void DisplayTree(string path, string indent)
    {
        if (maxDepth != -1 && currentDepth > maxDepth)
            return;

        if ((DateTime.Now - lastFlush).TotalSeconds > 1)
        {
            Console.Write(sb.ToString());
            sb.Clear();
            lastFlush = DateTime.Now;
        }

        var items = new DirectoryInfo(path)
            .GetFileSystemInfos("*", enumOptions)
            .Where(item => includeFiles || item is DirectoryInfo)
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
                    currentDepth++;
                    DisplayTree(dir.FullName, subIndent);
                    dirCount++;
                    break;
                }

                default:
                    fileCount++;
                    break;
            }
        }
    }

    private static string GetVolumeSerial(string path) =>
        DriveInfo.GetDrives().FirstOrDefault(d => d.Name == Path.GetPathRoot(path))?.VolumeLabel
        ?? "Unknown";
}
