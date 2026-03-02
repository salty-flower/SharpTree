using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SharpTree;

/// <summary>
/// P/Invoke for POSIX opendir/readdir/closedir with platform-aware
/// struct dirent field accessors. macOS and Linux only.
/// </summary>
internal static unsafe class NativeDir
{
    [DllImport("libc", EntryPoint = "opendir")]
    public static extern nint OpenDir(byte* name);

    [DllImport("libc", EntryPoint = "readdir")]
    public static extern nint ReadDir(nint dirp);

    [DllImport("libc", EntryPoint = "closedir")]
    public static extern int CloseDir(nint dirp);

    public const byte DT_UNKNOWN = 0;
    public const byte DT_DIR = 4;

    // struct dirent layout:
    // macOS:  d_namlen=18(u16), d_type=20(u8), d_name=21
    // Linux:  d_type=18(u8), d_name=19

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte GetDType(nint entry)
    {
        if (OperatingSystem.IsMacOS())
            return *(byte*)(entry + 20);
        return *(byte*)(entry + 18);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte* NamePtr(nint entry)
    {
        if (OperatingSystem.IsMacOS())
            return (byte*)(entry + 21);
        return (byte*)(entry + 19);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<byte> GetName(nint entry)
    {
        byte* name = NamePtr(entry);
        int len;
        if (OperatingSystem.IsMacOS())
            len = *(ushort*)(entry + 18); // d_namlen
        else
        {
            len = 0;
            while (name[len] != 0) len++;
        }
        return new ReadOnlySpan<byte>(name, len);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsDotOrDotDot(nint entry)
    {
        byte* n = NamePtr(entry);
        if (n[0] != (byte)'.') return false;
        if (n[1] == 0) return true;
        return n[1] == (byte)'.' && n[2] == 0;
    }
}
