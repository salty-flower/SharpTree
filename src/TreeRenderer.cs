using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace SharpTree;

public sealed class TreeRenderer
{
    public static ReadOnlySpan<byte> Branch => "├─── "u8;
    public static ReadOnlySpan<byte> LastBranch => "└─── "u8;
    public static ReadOnlySpan<byte> OddColor => "\x1b[0;36m"u8;
    public static ReadOnlySpan<byte> EvenColor => "\x1b[0;35m"u8;
    public static ReadOnlySpan<byte> ResetColor => "\x1b[0m"u8;
    private static ReadOnlySpan<byte> PipeIndent => "│   "u8;
    private static ReadOnlySpan<byte> SpaceIndent => "    "u8;

    private readonly Stream output;
    private readonly int flushInterval;
    private readonly Stopwatch stopwatch = new();
    private readonly byte[] outputBuf = new byte[Constants.BufferSize];
    private int outputPos;
    private readonly byte[] indentBuf = new byte[8192];
    private int indentByteLen;
    private readonly int[] indentLevelSize = new int[1024];
    private int depth;

    public TreeRenderer(Stream output, int flushInterval)
    {
        this.output = output;
        this.flushInterval = flushInterval;
    }

    public void Start() => stopwatch.Start();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteUtf8(ReadOnlySpan<byte> bytes)
    {
        if (outputPos + bytes.Length > outputBuf.Length)
            DrainBuffer();
        bytes.CopyTo(outputBuf.AsSpan(outputPos));
        outputPos += bytes.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteChars(ReadOnlySpan<char> chars)
    {
        if (outputPos + chars.Length * 3 > outputBuf.Length)
            DrainBuffer();
        // Fast ASCII path: most filenames are pure ASCII
        bool allAscii = true;
        for (int i = 0; i < chars.Length; i++)
        {
            if (chars[i] > 127) { allAscii = false; break; }
        }
        if (allAscii)
        {
            for (int i = 0; i < chars.Length; i++)
                outputBuf[outputPos++] = (byte)chars[i];
        }
        else
        {
            outputPos += Encoding.UTF8.GetBytes(chars, outputBuf.AsSpan(outputPos));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteNewline()
    {
        if (outputPos + 1 > outputBuf.Length)
            DrainBuffer();
        outputBuf[outputPos++] = (byte)'\n';
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteIndent() => WriteUtf8(indentBuf.AsSpan(0, indentByteLen));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void PushIndent(bool isLastEntry)
    {
        ReadOnlySpan<byte> segment = isLastEntry ? SpaceIndent : PipeIndent;
        if (depth >= indentLevelSize.Length || indentByteLen + segment.Length > indentBuf.Length)
            return;
        segment.CopyTo(indentBuf.AsSpan(indentByteLen));
        indentLevelSize[depth] = segment.Length;
        indentByteLen += segment.Length;
        depth++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void PopIndent()
    {
        if (depth <= 0)
            return;
        depth--;
        indentByteLen -= indentLevelSize[depth];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void MaybeFlush()
    {
        if (flushInterval != -1 && stopwatch.Elapsed.TotalMilliseconds > flushInterval)
        {
            DrainBuffer();
            stopwatch.Restart();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DrainBuffer()
    {
        if (outputPos > 0)
        {
            output.Write(outputBuf.AsSpan(0, outputPos));
            outputPos = 0;
        }
    }

    public void Flush()
    {
        DrainBuffer();
        output.Flush();
    }

    public void WriteHeader(string path)
    {
        var root = Path.GetPathRoot(path) ?? string.Empty;
        WriteUtf8("Folder PATH listing for volume "u8);
        WriteChars(root.AsSpan());
        WriteNewline();

        string volumeLabel = "Unknown";
        if (root.Length > 0)
        {
            var driveInfo = new DriveInfo(root);
            if (!string.IsNullOrEmpty(driveInfo.VolumeLabel))
                volumeLabel = driveInfo.VolumeLabel;
        }
        WriteUtf8("Volume serial number is "u8);
        WriteChars(volumeLabel.AsSpan());
        WriteNewline();

        WriteChars(path.AsSpan());
        WriteNewline();
    }

    public void WriteFooter(int dirs, int files)
    {
        WriteNewline();
        Span<char> numBuf = stackalloc char[11];
        dirs.TryFormat(numBuf, out int written);
        WriteChars(numBuf[..written]);
        WriteUtf8(dirs > 1 ? " Dir(s), "u8 : " Dir, "u8);
        files.TryFormat(numBuf, out written);
        WriteChars(numBuf[..written]);
        WriteUtf8(files > 1 ? " File(s)"u8 : " File"u8);
        WriteNewline();
    }
}
