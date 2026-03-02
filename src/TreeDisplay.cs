using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace SharpTree;

public partial class TreeCommand
{
    // Pre-encoded UTF-8 constants — zero transcoding at runtime
    private static ReadOnlySpan<byte> Branch => "├─── "u8;
    private static ReadOnlySpan<byte> LastBranch => "└─── "u8;
    private static ReadOnlySpan<byte> PipeIndent => "│   "u8;
    private static ReadOnlySpan<byte> SpaceIndent => "    "u8;
    private static ReadOnlySpan<byte> OddColor => "\x1b[0;36m"u8;
    private static ReadOnlySpan<byte> EvenColor => "\x1b[0;35m"u8;
    private static ReadOnlySpan<byte> ResetColor => "\x1b[0m"u8;

    private void DisplayTree(string path)
    {
        if (maxDepth != -1 && currentDepth > maxDepth)
            return;

        MaybeFlush();

        try
        {
            if (includeFiles)
                DisplayTreeWithFiles(path);
            else
                DisplayTreeDirsOnly(path);
        }
        catch (DirectoryNotFoundException)
        {
            WriteIndent();
            WriteUtf8("....[Invalid Directory]\n"u8);
        }
        catch (IOException)
        {
            WriteIndent();
            WriteUtf8("....[Inaccessible]\n"u8);
        }
    }

    private void DisplayTreeDirsOnly(string path)
    {
        var dirs = Directory.GetDirectories(path, "*", Constants.EnumOptions);
        if (dirs.Length == 0) return;

        var color = currentDepth % 2 == 1 ? OddColor : EvenColor;

        for (int i = 0; i < dirs.Length; i++)
        {
            bool isLast = i == dirs.Length - 1;

            WriteIndent();
            WriteUtf8(isLast ? LastBranch : Branch);
            WriteUtf8(color);
            WriteChars(Path.GetFileName(dirs[i].AsSpan()));
            WriteUtf8(ResetColor);
            WriteNewline();

            PushIndent(isLast);
            currentDepth++;
            DisplayTree(dirs[i]);
            currentDepth--;
            PopIndent();
        }

        dirCount += dirs.Length;
    }

    private void DisplayTreeWithFiles(string path)
    {
        var entries = new DirectoryInfo(path).GetFileSystemInfos("*", Constants.EnumOptions);
        if (entries.Length == 0) return;

        int numDirs = 0;
        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i] is DirectoryInfo)
                numDirs++;
        }
        int numFiles = entries.Length - numDirs;
        bool hasFiles = numFiles > 0;

        if (numDirs > 0)
        {
            var color = currentDepth % 2 == 1 ? OddColor : EvenColor;

            int dirIndex = 0;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i] is not DirectoryInfo)
                    continue;

                bool isLastDir = dirIndex == numDirs - 1;
                bool isLastEntry = isLastDir && !hasFiles;

                WriteIndent();
                WriteUtf8(isLastEntry ? LastBranch : Branch);
                WriteUtf8(color);
                WriteChars(entries[i].Name.AsSpan());
                WriteUtf8(ResetColor);
                WriteNewline();

                PushIndent(isLastEntry);
                currentDepth++;
                DisplayTree(entries[i].FullName);
                currentDepth--;
                PopIndent();

                dirIndex++;
            }

            dirCount += numDirs;
        }

        if (hasFiles)
        {
            int fileIndex = 0;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i] is DirectoryInfo)
                    continue;

                bool isLast = fileIndex == numFiles - 1;

                WriteIndent();
                WriteUtf8(isLast ? LastBranch : Branch);
                WriteChars(entries[i].Name.AsSpan());
                WriteNewline();

                fileIndex++;
            }

            fileCount += numFiles;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteIndent()
    {
        WriteUtf8(indentBuf.AsSpan(0, indentByteLen));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PushIndent(bool isLastEntry)
    {
        ReadOnlySpan<byte> segment = isLastEntry ? SpaceIndent : PipeIndent;
        segment.CopyTo(indentBuf.AsSpan(indentByteLen));
        indentLevelSize[currentDepth] = segment.Length;
        indentByteLen += segment.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PopIndent()
    {
        indentByteLen -= indentLevelSize[currentDepth];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void MaybeFlush()
    {
        if (flushInterval != -1 && stopwatch.Elapsed.TotalMilliseconds > flushInterval)
        {
            Flush();
            stopwatch.Restart();
        }
    }
}
