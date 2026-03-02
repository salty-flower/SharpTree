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
        int maxBytes = Encoding.UTF8.GetMaxByteCount(chars.Length);
        if (outputPos + maxBytes > outputBuf.Length)
            DrainBuffer();
        outputPos += Encoding.UTF8.GetBytes(chars, outputBuf.AsSpan(outputPos));
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
        segment.CopyTo(indentBuf.AsSpan(indentByteLen));
        indentLevelSize[depth] = segment.Length;
        indentByteLen += segment.Length;
        depth++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void PopIndent()
    {
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
            output.Write(outputBuf, 0, outputPos);
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
        WriteChars($"Folder PATH listing for volume {Path.GetPathRoot(path)}");
        WriteNewline();
        WriteChars(
            $"Volume serial number is {new DriveInfo(Path.GetPathRoot(path) ?? "")?.VolumeLabel ?? "Unknown"}"
        );
        WriteNewline();
        WriteChars(path);
        WriteNewline();
    }

    public void WriteFooter(int dirs, int files)
    {
        WriteNewline();
        WriteChars(
            $"{dirs} Dir{(dirs > 1 ? "(s)" : "")}, {files} File{(files > 1 ? "(s)" : "")}"
        );
        WriteNewline();
    }
}
