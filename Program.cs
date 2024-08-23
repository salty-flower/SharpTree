using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace SharpTree;

class Program : ConsoleAppBase
{
    static async Task Main(string[] args) =>
        await Host.CreateDefaultBuilder().RunConsoleAppFrameworkAsync<Program>(args);

    [Command("tree")]
    public void Tree(
        [Option("p", "The directory path to display")] string path = ".",
        [Option("d", "Display directories only")] bool directoriesOnly = false,
        [Option("L", "Limit the depth of recursion")] int maxDepth = -1
    )
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

    private (int dirCount, int fileCount) DisplayTree(
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

    private string GetVolumeSerial(string path) =>
        DriveInfo.GetDrives().FirstOrDefault(d => d.Name == Path.GetPathRoot(path))?.VolumeLabel
        ?? "Unknown";
}
