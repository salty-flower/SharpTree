#define ENABLE_MANUAL_CAPACITY_UPGRADE

using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace SharpTree;

public partial class TreeCommand
{
    private async Task DisplayTreeAsync(string path, string indent)
    {
        if (maxDepth != -1 && currentDepth > maxDepth)
            return;

        await MaybeFlushOutputAsync();

        try
        {
            var thisLayerDirectories = Directory.GetDirectories(path, "*", Constants.EnumOptions);
            var thisLayerFiles = Directory.GetFiles(path, "*", Constants.EnumOptions);

#if (ENABLE_MANUAL_CAPACITY_UPGRADE)
            ManualCapacityUpgrade(
                thisLayerDirectories.Length + (includeFiles ? thisLayerFiles.Length : 0),
                indent.Length
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
        string[] thisLayerFiles,
        string indent
    )
    {
        var directoryColorString =
            currentDepth % 2 == 1
                ? Constants.OddLayerDirectoryColorString
                : Constants.EvenLayerDirectoryColorString;
        // Handle all but the last item
        for (int i = 0; i < thisLayerDirectories.Length - 1; i++)
        {
            var dir = thisLayerDirectories[i];
            sb.Append(indent);
            sb.Append("├─── ");
            sb.Append(directoryColorString);
            sb.Append(Path.GetFileName(dir));
            sb.AppendLine(Constants.ColorResetString);

            var subIndent = indent + "│   ";
            currentDepth++;
            await DisplayTreeAsync(dir, subIndent);
            currentDepth--;
        }

        // Handle the last item separately
        if (thisLayerDirectories.Length > 0)
        {
            var lastDir = thisLayerDirectories[^1];
            bool v = thisLayerFiles.Length > 0 && includeFiles;
            sb.Append(indent);
            sb.Append(v ? "├" : "└");
            sb.Append("─── ");
            sb.Append(directoryColorString);
            sb.Append(Path.GetFileName(lastDir));
            sb.AppendLine(Constants.ColorResetString);

            var subIndent = indent + (v ? "│" : " ") + "   ";
            currentDepth++;
            await DisplayTreeAsync(lastDir, subIndent);
            currentDepth--;
        }

        dirCount += thisLayerDirectories.Length;
    }

    private void DisplayFiles(string[] thisLayerFiles, string indent)
    {
        for (int i = 0; i < thisLayerFiles.Length - 1; i++)
        {
            var file = thisLayerFiles[i];
            sb.Append(indent);
            sb.Append("├─── ");
            sb.AppendLine(Path.GetFileName(file));
        }

        sb.Append(indent);
        sb.Append("└─── ");
        sb.AppendLine(Path.GetFileName(thisLayerFiles[^1]));
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
}
