using System;
using System.Text;

namespace SharpTree;

/// <summary>
/// Tree walker using BSD fts(3) P/Invoke. macOS only.
/// Displays directories first, then files (files buffered until FTS_DP).
/// </summary>
public sealed class FtsTreeWalker : ITreeWalker
{
    // Per-level sibling tracking
    private readonly int[] levelTotalDirs = new int[1024];
    private readonly bool[] levelHasFiles = new bool[1024];
    private readonly int[] levelDirsSeen = new int[1024];

    // File name pool — stack-like buffer for deferred file display
    private byte[] namePool = new byte[512 * 1024];
    private int namePoolPos;
    private (int offset, int length)[] fileRefs = new (int, int)[16384];
    private int fileRefCount;
    private readonly int[] levelFileRefStart = new int[1024];
    private readonly int[] levelFileRefEnd = new int[1024];
    private readonly int[] levelNamePoolStart = new int[1024];

    private int dirCount;
    private int fileCount;

    public unsafe (int dirs, int files) Walk(
        string rootPath,
        bool includeFiles,
        int maxDepth,
        TreeRenderer renderer
    )
    {
        dirCount = 0;
        fileCount = 0;

        byte[] pathBytes = Encoding.UTF8.GetBytes(rootPath + "\0");
        fixed (byte* pPath = pathBytes)
        {
            nint* argv = stackalloc nint[2];
            argv[0] = (nint)pPath;
            argv[1] = 0;

            nint ftsp = Fts.Open(argv, Fts.FTS_PHYSICAL | Fts.FTS_NOSTAT_TYPE, 0);
            if (ftsp == 0)
                return (0, 0);

            try
            {
                WalkLoop(ftsp, includeFiles, maxDepth, renderer);
            }
            finally
            {
                Fts.Close(ftsp);
            }
        }

        return (dirCount, fileCount);
    }

    private unsafe void WalkLoop(
        nint ftsp,
        bool includeFiles,
        int maxDepth,
        TreeRenderer renderer
    )
    {
        nint entry;
        while ((entry = Fts.Read(ftsp)) != 0)
        {
            ushort info = Fts.GetInfo(entry);
            short level = Fts.GetLevel(entry);

            switch (info)
            {
                case Fts.FTS_D: // entering directory (pre-order)
                    HandlePreOrder(ftsp, entry, level, includeFiles, maxDepth, renderer);
                    break;

                case Fts.FTS_DP: // leaving directory (post-order)
                    if (level > 0)
                    {
                        FlushBufferedFiles(level, renderer);
                        renderer.PopIndent();
                    }
                    break;

                case Fts.FTS_DNR: // unreadable directory
                    if (level > 0)
                        HandleUnreadableDir(entry, level, renderer);
                    break;

                // FTS_F, FTS_NSOK, FTS_SL, etc. — already buffered, skip
            }
        }
    }

    private unsafe void HandlePreOrder(
        nint ftsp,
        nint entry,
        short level,
        bool includeFiles,
        int maxDepth,
        TreeRenderer renderer
    )
    {
        // Display this directory entry (unless root)
        if (level > 0)
        {
            int parentLevel = level - 1;
            levelDirsSeen[parentLevel]++;
            bool isLastDir =
                levelDirsSeen[parentLevel] == levelTotalDirs[parentLevel];
            bool isLastEntry = isLastDir && !levelHasFiles[parentLevel];

            var color =
                parentLevel % 2 == 1 ? TreeRenderer.OddColor : TreeRenderer.EvenColor;

            renderer.WriteIndent();
            renderer.WriteUtf8(isLastEntry ? TreeRenderer.LastBranch : TreeRenderer.Branch);
            renderer.WriteUtf8(color);
            renderer.WriteUtf8(Fts.GetNameSpan(entry));
            renderer.WriteUtf8(TreeRenderer.ResetColor);
            renderer.WriteNewline();

            renderer.PushIndent(isLastEntry);
            dirCount++;
        }

        renderer.MaybeFlush();

        // At max depth: display all children immediately, skip recursion
        if (maxDepth != -1 && level >= maxDepth)
        {
            Fts.Set(ftsp, entry, Fts.FTS_SKIP);
            DisplayChildrenImmediate(ftsp, level, includeFiles, renderer);
            return;
        }

        // Normal: scan children for counting, buffer file names
        ScanChildren(ftsp, level, includeFiles);
    }

    private unsafe void ScanChildren(nint ftsp, int level, bool includeFiles)
    {
        nint child = Fts.Children(ftsp, 0);
        int numDirs = 0;
        int numFiles = 0;
        int poolStart = namePoolPos;
        int refStart = fileRefCount;

        while (child != 0)
        {
            ushort ci = Fts.GetInfo(child);
            if (ci == Fts.FTS_D)
            {
                numDirs++;
            }
            else
            {
                numFiles++;
                if (includeFiles)
                {
                    var name = Fts.GetNameSpan(child);
                    EnsurePoolCapacity(name.Length);
                    name.CopyTo(namePool.AsSpan(namePoolPos));
                    EnsureRefCapacity();
                    fileRefs[fileRefCount++] = (namePoolPos, name.Length);
                    namePoolPos += name.Length;
                }
            }
            child = Fts.GetLink(child);
        }

        levelTotalDirs[level] = numDirs;
        levelHasFiles[level] = includeFiles && numFiles > 0;
        levelDirsSeen[level] = 0;
        levelFileRefStart[level] = refStart;
        levelFileRefEnd[level] = fileRefCount;
        levelNamePoolStart[level] = poolStart;
    }

    private unsafe void DisplayChildrenImmediate(
        nint ftsp,
        int level,
        bool includeFiles,
        TreeRenderer renderer
    )
    {
        nint child = Fts.Children(ftsp, 0);

        // Count
        int numDirs = 0,
            numFiles = 0;
        nint c = child;
        while (c != 0)
        {
            if (Fts.GetInfo(c) == Fts.FTS_D)
                numDirs++;
            else
                numFiles++;
            c = Fts.GetLink(c);
        }

        bool hasFiles = includeFiles && numFiles > 0;

        // Display directories
        if (numDirs > 0)
        {
            var color = level % 2 == 1 ? TreeRenderer.OddColor : TreeRenderer.EvenColor;
            int dirIdx = 0;
            c = child;
            while (c != 0)
            {
                if (Fts.GetInfo(c) == Fts.FTS_D)
                {
                    bool isLastDir = dirIdx == numDirs - 1;
                    bool isLastEntry = isLastDir && !hasFiles;

                    renderer.WriteIndent();
                    renderer.WriteUtf8(
                        isLastEntry ? TreeRenderer.LastBranch : TreeRenderer.Branch
                    );
                    renderer.WriteUtf8(color);
                    renderer.WriteUtf8(Fts.GetNameSpan(c));
                    renderer.WriteUtf8(TreeRenderer.ResetColor);
                    renderer.WriteNewline();

                    dirIdx++;
                }
                c = Fts.GetLink(c);
            }
            dirCount += numDirs;
        }

        // Display files
        if (hasFiles)
        {
            int fileIdx = 0;
            c = child;
            while (c != 0)
            {
                if (Fts.GetInfo(c) != Fts.FTS_D)
                {
                    bool isLast = fileIdx == numFiles - 1;

                    renderer.WriteIndent();
                    renderer.WriteUtf8(
                        isLast ? TreeRenderer.LastBranch : TreeRenderer.Branch
                    );
                    renderer.WriteUtf8(Fts.GetNameSpan(c));
                    renderer.WriteNewline();

                    fileIdx++;
                }
                c = Fts.GetLink(c);
            }
            fileCount += numFiles;
        }

        // Mark level as fully displayed (no deferred files)
        levelTotalDirs[level] = 0;
        levelHasFiles[level] = false;
        levelFileRefStart[level] = fileRefCount;
        levelFileRefEnd[level] = fileRefCount;
        levelNamePoolStart[level] = namePoolPos;
    }

    private unsafe void HandleUnreadableDir(nint entry, short level, TreeRenderer renderer)
    {
        int parentLevel = level - 1;
        levelDirsSeen[parentLevel]++;
        bool isLastDir = levelDirsSeen[parentLevel] == levelTotalDirs[parentLevel];
        bool isLastEntry = isLastDir && !levelHasFiles[parentLevel];

        var color =
            parentLevel % 2 == 1 ? TreeRenderer.OddColor : TreeRenderer.EvenColor;

        renderer.WriteIndent();
        renderer.WriteUtf8(isLastEntry ? TreeRenderer.LastBranch : TreeRenderer.Branch);
        renderer.WriteUtf8(color);
        renderer.WriteUtf8(Fts.GetNameSpan(entry));
        renderer.WriteUtf8(TreeRenderer.ResetColor);
        renderer.WriteNewline();

        renderer.PushIndent(isLastEntry);
        renderer.WriteIndent();
        renderer.WriteUtf8("....[Inaccessible]\n"u8);
        renderer.PopIndent();

        dirCount++;
    }

    private void FlushBufferedFiles(int level, TreeRenderer renderer)
    {
        int start = levelFileRefStart[level];
        int end = levelFileRefEnd[level];

        if (end > start)
        {
            for (int i = start; i < end; i++)
            {
                bool isLast = i == end - 1;

                renderer.WriteIndent();
                renderer.WriteUtf8(isLast ? TreeRenderer.LastBranch : TreeRenderer.Branch);
                renderer.WriteUtf8(
                    namePool.AsSpan(fileRefs[i].offset, fileRefs[i].length)
                );
                renderer.WriteNewline();
            }
            fileCount += end - start;
        }

        // Reclaim pool space (stack-like: child levels already reclaimed)
        fileRefCount = start;
        namePoolPos = levelNamePoolStart[level];
    }

    private void EnsurePoolCapacity(int needed)
    {
        if (namePoolPos + needed > namePool.Length)
            Array.Resize(ref namePool, namePool.Length * 2);
    }

    private void EnsureRefCapacity()
    {
        if (fileRefCount >= fileRefs.Length)
            Array.Resize(ref fileRefs, fileRefs.Length * 2);
    }
}
