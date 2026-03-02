using System;
using System.Runtime.InteropServices;

namespace SharpTree;

/// <summary>
/// P/Invoke bindings for BSD fts(3) — macOS arm64.
/// </summary>
internal static unsafe class Fts
{
    private const string Lib = "libSystem.B.dylib";

    // fts_open options
    public const int FTS_PHYSICAL = 0x010;
    public const int FTS_NOSTAT_TYPE = 0x800; // macOS extension: use d_type, skip stat()

    // fts_info values
    public const ushort FTS_D = 1;    // directory (pre-order)
    public const ushort FTS_DC = 2;   // directory causing cycle
    public const ushort FTS_DNR = 4;  // unreadable directory
    public const ushort FTS_DP = 6;   // directory (post-order)
    public const ushort FTS_ERR = 7;  // error
    public const ushort FTS_F = 8;    // regular file
    public const ushort FTS_NSOK = 11; // no stat requested (non-dir with NOSTAT)

    // fts_set instructions
    public const int FTS_SKIP = 4;

    // FTSENT field offsets for macOS arm64 (verified with offsetof)
    private const int OFF_LINK = 16;
    private const int OFF_NAMELEN = 66;
    private const int OFF_LEVEL = 86;
    private const int OFF_INFO = 88;
    private const int OFF_NAME = 104;

    [DllImport(Lib, EntryPoint = "fts_open")]
    public static extern nint Open(nint* pathArgv, int options, nint compar);

    [DllImport(Lib, EntryPoint = "fts_read")]
    public static extern nint Read(nint ftsp);

    [DllImport(Lib, EntryPoint = "fts_children")]
    public static extern nint Children(nint ftsp, int options);

    [DllImport(Lib, EntryPoint = "fts_set")]
    public static extern int Set(nint ftsp, nint entry, int options);

    [DllImport(Lib, EntryPoint = "fts_close")]
    public static extern int Close(nint ftsp);

    // Accessors for FTSENT fields — read directly from native memory
    public static nint GetLink(nint entry) => *(nint*)(entry + OFF_LINK);
    public static ushort GetNameLen(nint entry) => *(ushort*)(entry + OFF_NAMELEN);
    public static short GetLevel(nint entry) => *(short*)(entry + OFF_LEVEL);
    public static ushort GetInfo(nint entry) => *(ushort*)(entry + OFF_INFO);
    public static byte* GetName(nint entry) => (byte*)(entry + OFF_NAME);

    public static ReadOnlySpan<byte> GetNameSpan(nint entry)
        => new(GetName(entry), GetNameLen(entry));
}
