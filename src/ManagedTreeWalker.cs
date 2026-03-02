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
        var dirs = Directory.GetDirectories(path, "*", Constants.EnumOptions);
        var files = Directory.GetFiles(path, "*", Constants.EnumOptions);
        if (dirs.Length == 0 && files.Length == 0)
            return;

        bool hasFiles = files.Length > 0;

        if (dirs.Length > 0)
        {
            var color =
                currentDepth % 2 == 1 ? TreeRenderer.OddColor : TreeRenderer.EvenColor;

            for (int i = 0; i < dirs.Length; i++)
            {
                bool isLastDir = i == dirs.Length - 1;
                bool isLastEntry = isLastDir && !hasFiles;

                renderer.WriteIndent();
                renderer.WriteUtf8(
                    isLastEntry ? TreeRenderer.LastBranch : TreeRenderer.Branch
                );
                renderer.WriteUtf8(color);
                renderer.WriteChars(Path.GetFileName(dirs[i].AsSpan()));
                renderer.WriteUtf8(TreeRenderer.ResetColor);
                renderer.WriteNewline();

                renderer.PushIndent(isLastEntry);
                currentDepth++;
                DisplayTree(dirs[i]);
                currentDepth--;
                renderer.PopIndent();
            }

            dirCount += dirs.Length;
        }

        if (hasFiles)
        {
            for (int i = 0; i < files.Length; i++)
            {
                bool isLast = i == files.Length - 1;

                renderer.WriteIndent();
                renderer.WriteUtf8(isLast ? TreeRenderer.LastBranch : TreeRenderer.Branch);
                renderer.WriteChars(Path.GetFileName(files[i].AsSpan()));
                renderer.WriteNewline();
            }

            fileCount += files.Length;
        }
    }
}
