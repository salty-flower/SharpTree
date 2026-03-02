using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using ConsoleAppFramework;

namespace SharpTree;

public partial class TreeCommand
{
    private int dirCount;
    private int fileCount;
    private int maxDepth = -1;
    private int currentDepth;
    private bool includeFiles;
    private int flushInterval;
    private readonly Stopwatch stopwatch = new();
    private Stream outputStream = null!;

    // Direct UTF-8 output buffer — no StringBuilder, no transcoding for constants
    private byte[] outputBuf = new byte[Constants.BufferSize];
    private int outputPos;

    // UTF-8 indent buffer (│ is 3 bytes in UTF-8, so levels are variable-width)
    private readonly byte[] indentBuf = new byte[8192];
    private int indentByteLen;
    private readonly int[] indentLevelSize = new int[1024];

    /// <summary>
    /// Display a tree of the specified directory.
    /// </summary>
    /// <param name="path">-p, Root path of the tree</param>
    /// <param name="includeFiles">-f</param>
    /// <param name="maxDepth">-m</param>
    /// <param name="flushInterval">-t, Flush output to stdout every x ms. -1 to disable.</param>
    [Command("")]
    public void Tree(
        string path = ".",
        bool includeFiles = false,
        int maxDepth = -1,
        int flushInterval = 1
    )
    {
        this.maxDepth = maxDepth;
        this.includeFiles = includeFiles;
        this.flushInterval = flushInterval;
        path = Path.GetFullPath(path);

        using (
            outputStream = new BufferedStream(Console.OpenStandardOutput(), Constants.BufferSize)
        )
        {
            WriteHeader(path);
            stopwatch.Start();

            DisplayTree(path);

            WriteFooter();
            Flush();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureCapacity(int needed)
    {
        if (outputPos + needed > outputBuf.Length)
            Flush();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteUtf8(ReadOnlySpan<byte> bytes)
    {
        EnsureCapacity(bytes.Length);
        bytes.CopyTo(outputBuf.AsSpan(outputPos));
        outputPos += bytes.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteChars(ReadOnlySpan<char> chars)
    {
        int maxBytes = Encoding.UTF8.GetMaxByteCount(chars.Length);
        EnsureCapacity(maxBytes);
        outputPos += Encoding.UTF8.GetBytes(chars, outputBuf.AsSpan(outputPos));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteNewline()
    {
        EnsureCapacity(1);
        outputBuf[outputPos++] = (byte)'\n';
    }

    private void Flush()
    {
        if (outputPos > 0)
        {
            outputStream.Write(outputBuf, 0, outputPos);
            outputStream.Flush();
            outputPos = 0;
        }
    }
}
