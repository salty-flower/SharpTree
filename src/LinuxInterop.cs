using System;
using System.Runtime.InteropServices;

namespace SharpTree;

/// <summary>
/// P/Invoke bindings for POSIX opendir/readdir/closedir — Linux only.
/// </summary>
internal static unsafe class LinuxDir
{
    private const string Lib = "libc";

    // d_type constants
    public const byte DT_UNKNOWN = 0;
    public const byte DT_DIR = 4;
    public const byte DT_REG = 8;
    public const byte DT_LNK = 10;

    // Linux struct dirent offsets (same on x86_64 and aarch64):
    //   d_ino:    0  (8 bytes)
    //   d_off:    8  (8 bytes)
    //   d_reclen: 16 (2 bytes)
    //   d_type:   18 (1 byte)
    //   d_name:   19 (256 bytes)
    private const int OFF_D_TYPE = 18;
    private const int OFF_D_NAME = 19;

    [DllImport(Lib, EntryPoint = "opendir")]
    public static extern nint Open(byte* name);

    [DllImport(Lib, EntryPoint = "readdir")]
    public static extern nint Read(nint dirp);

    [DllImport(Lib, EntryPoint = "closedir")]
    public static extern int Close(nint dirp);

    public static byte GetDType(nint entry) => *(byte*)(entry + OFF_D_TYPE);

    public static byte* GetName(nint entry) => (byte*)(entry + OFF_D_NAME);

    public static ReadOnlySpan<byte> GetNameSpan(nint entry)
    {
        byte* p = GetName(entry);
        int len = 0;
        while (p[len] != 0) len++;
        return new ReadOnlySpan<byte>(p, len);
    }

    public static bool IsDotOrDotDot(nint entry)
    {
        byte* p = GetName(entry);
        if (p[0] != (byte)'.') return false;
        if (p[1] == 0) return true;           // "."
        return p[1] == (byte)'.' && p[2] == 0; // ".."
    }
}
