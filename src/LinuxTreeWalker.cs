using System;
using System.IO;
using System.Text;

namespace SharpTree;

/// <summary>
/// Tree walker using opendir/readdir P/Invoke. Linux only.
/// Closes directory FD before recursing to prevent FD exhaustion.
/// </summary>
public sealed class LinuxTreeWalker : ITreeWalker
{
    // Pool buffers for directory and file names
    private byte[] namePool = new byte[512 * 1024];
    private int namePoolPos;

    private (int offset, int length)[] dirRefs = new (int, int)[4096];
    private int dirRefCount;

    private (int offset, int length)[] fileRefs = new (int, int)[16384];
    private int fileRefCount;

    // Reusable path buffer: PATH_MAX (4096) + NAME_MAX (255) + 1 (slash)
    private readonly byte[] pathBuf = new byte[4352];

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
        namePoolPos = 0;
        dirRefCount = 0;
        fileRefCount = 0;

        WalkDirectory(rootPath);
        return (dirCount, fileCount);
    }

    private unsafe void WalkDirectory(string path)
    {
        if (maxDepth != -1 && currentDepth > maxDepth)
            return;

        renderer.MaybeFlush();

        // Build null-terminated UTF-8 path for opendir
        int pathByteCount = Encoding.UTF8.GetByteCount(path);
        Span<byte> pathNative = pathByteCount < 4096
            ? pathBuf.AsSpan(0, pathByteCount + 1)
            : new byte[pathByteCount + 1];
        Encoding.UTF8.GetBytes(path, pathNative);
        pathNative[pathByteCount] = 0;

        nint dirp;
        fixed (byte* pPath = pathNative)
        {
            dirp = LinuxDir.Open(pPath);
        }

        if (dirp == 0)
        {
            renderer.WriteIndent();
            renderer.WriteUtf8("....[Inaccessible]\n"u8);
            return;
        }

        // Save pool positions so we can reclaim after this level
        int poolStart = namePoolPos;
        int dirRefStart = dirRefCount;
        int fileRefStart = fileRefCount;

        // Read all entries
        nint entry;
        while ((entry = LinuxDir.Read(dirp)) != 0)
        {
            if (LinuxDir.IsDotOrDotDot(entry))
                continue;

            byte dtype = LinuxDir.GetDType(entry);
            var name = LinuxDir.GetNameSpan(entry);

            bool isDir;
            if (dtype == LinuxDir.DT_DIR)
                isDir = true;
            else if (dtype == LinuxDir.DT_UNKNOWN)
            {
                // Filesystem doesn't support d_type — fall back to stat
                string childPath = Path.Join(path, Encoding.UTF8.GetString(name));
                isDir = Directory.Exists(childPath);
            }
            else
                isDir = false; // DT_REG, DT_LNK, etc. — treat as file

            // Buffer the name
            EnsurePoolCapacity(name.Length);
            name.CopyTo(namePool.AsSpan(namePoolPos));
            int offset = namePoolPos;
            namePoolPos += name.Length;

            if (isDir)
            {
                EnsureDirRefCapacity();
                dirRefs[dirRefCount++] = (offset, name.Length);
            }
            else if (includeFiles)
            {
                EnsureFileRefCapacity();
                fileRefs[fileRefCount++] = (offset, name.Length);
            }
        }

        // Close directory BEFORE recursing to avoid FD exhaustion
        LinuxDir.Close(dirp);

        int numDirs = dirRefCount - dirRefStart;
        int numFiles = fileRefCount - fileRefStart;
        bool hasFiles = includeFiles && numFiles > 0;

        // Display and recurse into directories
        if (numDirs > 0)
        {
            var color =
                currentDepth % 2 == 1 ? TreeRenderer.OddColor : TreeRenderer.EvenColor;

            for (int i = dirRefStart; i < dirRefCount; i++)
            {
                bool isLastDir = i == dirRefCount - 1;
                bool isLastEntry = isLastDir && !hasFiles;
                var dirName = namePool.AsSpan(dirRefs[i].offset, dirRefs[i].length);

                renderer.WriteIndent();
                renderer.WriteUtf8(
                    isLastEntry ? TreeRenderer.LastBranch : TreeRenderer.Branch
                );
                renderer.WriteUtf8(color);
                renderer.WriteUtf8(dirName);
                renderer.WriteUtf8(TreeRenderer.ResetColor);
                renderer.WriteNewline();

                renderer.PushIndent(isLastEntry);
                currentDepth++;

                // Build child path for recursion
                string childPath = Path.Join(path, Encoding.UTF8.GetString(dirName));
                WalkDirectory(childPath);

                currentDepth--;
                renderer.PopIndent();
            }

            dirCount += numDirs;
        }

        // Display files
        if (hasFiles)
        {
            for (int i = fileRefStart; i < fileRefCount; i++)
            {
                bool isLast = i == fileRefCount - 1;

                renderer.WriteIndent();
                renderer.WriteUtf8(isLast ? TreeRenderer.LastBranch : TreeRenderer.Branch);
                renderer.WriteUtf8(
                    namePool.AsSpan(fileRefs[i].offset, fileRefs[i].length)
                );
                renderer.WriteNewline();
            }

            fileCount += numFiles;
        }

        // Reclaim pool space (stack-like)
        dirRefCount = dirRefStart;
        fileRefCount = fileRefStart;
        namePoolPos = poolStart;
    }

    private void EnsurePoolCapacity(int needed)
    {
        if (namePoolPos + needed > namePool.Length)
            Array.Resize(ref namePool, namePool.Length * 2);
    }

    private void EnsureDirRefCapacity()
    {
        if (dirRefCount >= dirRefs.Length)
            Array.Resize(ref dirRefs, dirRefs.Length * 2);
    }

    private void EnsureFileRefCapacity()
    {
        if (fileRefCount >= fileRefs.Length)
            Array.Resize(ref fileRefs, fileRefs.Length * 2);
    }
}
