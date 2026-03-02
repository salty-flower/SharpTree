using System;
using System.IO;

namespace SharpTree;

public sealed class ManagedTreeWalker : ITreeWalker
{
    private int dirCount;
    private int fileCount;
    private int maxDepth;
    private bool includeFiles;
    private int currentDepth;
    private TreeRenderer renderer = null!;

    public (int dirs, int files) Walk(
        string rootPath,
        bool includeFiles,
        int maxDepth,
        TreeRenderer renderer
    )
    {
        this.includeFiles = includeFiles;
        this.maxDepth = maxDepth;
        this.renderer = renderer;
        dirCount = 0;
        fileCount = 0;
        currentDepth = 0;

        DisplayTree(rootPath);
        return (dirCount, fileCount);
    }

    private void DisplayTree(string path)
    {
        if (maxDepth != -1 && currentDepth > maxDepth)
            return;

        renderer.MaybeFlush();

        try
        {
            if (includeFiles)
                DisplayTreeWithFiles(path);
            else
                DisplayTreeDirsOnly(path);
        }
        catch (DirectoryNotFoundException)
        {
            renderer.WriteIndent();
            renderer.WriteUtf8("....[Invalid Directory]\n"u8);
        }
        catch (IOException)
        {
            renderer.WriteIndent();
            renderer.WriteUtf8("....[Inaccessible]\n"u8);
        }
    }

    private void DisplayTreeDirsOnly(string path)
    {
        var dirs = Directory.GetDirectories(path, "*", Constants.EnumOptions);
        if (dirs.Length == 0)
            return;

        var color =
            currentDepth % 2 == 1 ? TreeRenderer.OddColor : TreeRenderer.EvenColor;

        for (int i = 0; i < dirs.Length; i++)
        {
            bool isLast = i == dirs.Length - 1;

            renderer.WriteIndent();
            renderer.WriteUtf8(isLast ? TreeRenderer.LastBranch : TreeRenderer.Branch);
            renderer.WriteUtf8(color);
            renderer.WriteChars(Path.GetFileName(dirs[i].AsSpan()));
            renderer.WriteUtf8(TreeRenderer.ResetColor);
            renderer.WriteNewline();

            renderer.PushIndent(isLast);
            currentDepth++;
            DisplayTree(dirs[i]);
            currentDepth--;
            renderer.PopIndent();
        }

        dirCount += dirs.Length;
    }

    private void DisplayTreeWithFiles(string path)
    {
        var entries = new DirectoryInfo(path).GetFileSystemInfos("*", Constants.EnumOptions);
        if (entries.Length == 0)
            return;

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
            var color =
                currentDepth % 2 == 1 ? TreeRenderer.OddColor : TreeRenderer.EvenColor;

            int dirIndex = 0;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i] is not DirectoryInfo)
                    continue;

                bool isLastDir = dirIndex == numDirs - 1;
                bool isLastEntry = isLastDir && !hasFiles;

                renderer.WriteIndent();
                renderer.WriteUtf8(
                    isLastEntry ? TreeRenderer.LastBranch : TreeRenderer.Branch
                );
                renderer.WriteUtf8(color);
                renderer.WriteChars(entries[i].Name.AsSpan());
                renderer.WriteUtf8(TreeRenderer.ResetColor);
                renderer.WriteNewline();

                renderer.PushIndent(isLastEntry);
                currentDepth++;
                DisplayTree(entries[i].FullName);
                currentDepth--;
                renderer.PopIndent();

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

                renderer.WriteIndent();
                renderer.WriteUtf8(isLast ? TreeRenderer.LastBranch : TreeRenderer.Branch);
                renderer.WriteChars(entries[i].Name.AsSpan());
                renderer.WriteNewline();

                fileIndex++;
            }

            fileCount += numFiles;
        }
    }
}
