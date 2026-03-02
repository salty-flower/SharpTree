using System;
using System.IO;
using System.Text;

namespace SharpTree;

/// <summary>
/// Tree walker using raw opendir/readdir/closedir. macOS + Linux.
/// Zero string allocation: names stay as UTF-8 bytes throughout.
/// </summary>
public sealed class NativeTreeWalker
{
    private byte[] namePool = new byte[512 * 1024];
    private int namePoolPos;

    private (int offset, int length)[] dirRefs = new (int, int)[4096];
    private int dirRefCount;

    private (int offset, int length)[] fileRefs = new (int, int)[16384];
    private int fileRefCount;

    // Reusable path buffer (null-terminated UTF-8)
    private readonly byte[] pathBuf = new byte[4096 + 256];
    private int pathLen;

    private int dirCount;
    private int fileCount;
    private bool includeFiles;
    private int maxDepth;
    private TreeRenderer renderer = null!;

    public unsafe (int dirs, int files) Walk(
        string rootPath,
        bool includeFiles,
        int maxDepth,
        TreeRenderer renderer)
    {
        this.includeFiles = includeFiles;
        this.maxDepth = maxDepth;
        this.renderer = renderer;
        dirCount = 0;
        fileCount = 0;
        namePoolPos = 0;
        dirRefCount = 0;
        fileRefCount = 0;

        pathLen = Encoding.UTF8.GetBytes(rootPath, pathBuf);
        pathBuf[pathLen] = 0;

        WalkDir(0);
        return (dirCount, fileCount);
    }

    private unsafe void WalkDir(int depth)
    {
        if (maxDepth != -1 && depth > maxDepth)
            return;

        renderer.MaybeFlush();

        nint dirp;
        fixed (byte* p = pathBuf)
            dirp = NativeDir.OpenDir(p);

        if (dirp == 0)
        {
            renderer.WriteIndent();
            renderer.WriteUtf8("....[Inaccessible]\n"u8);
            return;
        }

        // Save pool state for stack-like reclamation
        int savedDirRef = dirRefCount;
        int savedFileRef = fileRefCount;
        int savedPool = namePoolPos;

        // Phase 1: readdir all entries, classify into dir/file pools
        try
        {
            nint entry;
            while ((entry = NativeDir.ReadDir(dirp)) != 0)
            {
                if (NativeDir.IsDotOrDotDot(entry))
                    continue;

                byte dtype = NativeDir.GetDType(entry);

                if (dtype == NativeDir.DT_DIR)
                {
                    BufferName(NativeDir.GetName(entry), ref dirRefs, ref dirRefCount);
                }
                else if (dtype == NativeDir.DT_UNKNOWN)
                {
                    var name = NativeDir.GetName(entry);
                    if (CheckIsDirectory(name))
                        BufferName(name, ref dirRefs, ref dirRefCount);
                    else if (includeFiles)
                        BufferName(name, ref fileRefs, ref fileRefCount);
                }
                else if (includeFiles)
                {
                    BufferName(NativeDir.GetName(entry), ref fileRefs, ref fileRefCount);
                }
            }
        }
        finally
        {
            NativeDir.CloseDir(dirp);
        }

        int localDirEnd = dirRefCount;
        int localFileEnd = fileRefCount;
        int numDirs = localDirEnd - savedDirRef;
        int numFiles = localFileEnd - savedFileRef;
        bool hasFiles = includeFiles && numFiles > 0;

        // Phase 2: Display dirs + recurse
        if (numDirs > 0)
        {
            var color = depth % 2 == 1 ? TreeRenderer.OddColor : TreeRenderer.EvenColor;

            for (int i = savedDirRef; i < localDirEnd; i++)
            {
                bool isLastDir = i == localDirEnd - 1;
                bool isLastEntry = isLastDir && !hasFiles;
                var dirName = namePool.AsSpan(dirRefs[i].offset, dirRefs[i].length);

                renderer.WriteIndent();
                renderer.WriteUtf8(isLastEntry ? TreeRenderer.LastBranch : TreeRenderer.Branch);
                renderer.WriteUtf8(color);
                renderer.WriteUtf8(dirName);
                renderer.WriteUtf8(TreeRenderer.ResetColor);
                renderer.WriteNewline();

                // Build child path in-place; skip recursion if it would overflow pathBuf
                if (pathLen + 1 + dirName.Length >= pathBuf.Length)
                    continue;

                int savedPathLen = pathLen;
                pathBuf[pathLen] = (byte)'/';
                dirName.CopyTo(pathBuf.AsSpan(pathLen + 1));
                pathLen += 1 + dirName.Length;
                pathBuf[pathLen] = 0;

                renderer.PushIndent(isLastEntry);
                WalkDir(depth + 1);
                renderer.PopIndent();

                pathLen = savedPathLen;
                pathBuf[pathLen] = 0;
            }

            dirCount += numDirs;
        }

        // Phase 3: Display files
        if (hasFiles)
        {
            for (int i = savedFileRef; i < localFileEnd; i++)
            {
                bool isLast = i == localFileEnd - 1;

                renderer.WriteIndent();
                renderer.WriteUtf8(isLast ? TreeRenderer.LastBranch : TreeRenderer.Branch);
                renderer.WriteUtf8(namePool.AsSpan(fileRefs[i].offset, fileRefs[i].length));
                renderer.WriteNewline();
            }

            fileCount += numFiles;
        }

        // Phase 4: Reclaim pool space
        dirRefCount = savedDirRef;
        fileRefCount = savedFileRef;
        namePoolPos = savedPool;
    }

    private void BufferName(ReadOnlySpan<byte> name,
        ref (int offset, int length)[] refs, ref int refCount)
    {
        if (namePoolPos + name.Length > namePool.Length)
            Array.Resize(ref namePool, namePool.Length * 2);
        name.CopyTo(namePool.AsSpan(namePoolPos));
        if (refCount >= refs.Length)
            Array.Resize(ref refs, refs.Length * 2);
        refs[refCount++] = (namePoolPos, name.Length);
        namePoolPos += name.Length;
    }

    private bool CheckIsDirectory(ReadOnlySpan<byte> name)
    {
        int tempLen = pathLen + 1 + name.Length;
        if (tempLen >= pathBuf.Length)
            return false;

        pathBuf[pathLen] = (byte)'/';
        name.CopyTo(pathBuf.AsSpan(pathLen + 1));
        pathBuf[tempLen] = 0;

        string fullPath = Encoding.UTF8.GetString(pathBuf, 0, tempLen);
        pathBuf[pathLen] = 0;

        return Directory.Exists(fullPath);
    }
}
