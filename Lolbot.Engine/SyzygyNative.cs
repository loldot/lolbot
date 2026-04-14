using System;
using System.Runtime.InteropServices;

internal static class SyzygyNative
{
    private const string DllName = "fathom";

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool fathom_tb_init(string path);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void fathom_tb_free();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern uint fathom_tb_probe_wdl(
        ulong white,
        ulong black,
        ulong kings,
        ulong queens,
        ulong rooks,
        ulong bishops,
        ulong knights,
        ulong pawns,
        uint ep,
        [MarshalAs(UnmanagedType.I1)] bool turn);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern uint fathom_tb_probe_root(
        ulong white,
        ulong black,
        ulong kings,
        ulong queens,
        ulong rooks,
        ulong bishops,
        ulong knights,
        ulong pawns,
        uint rule50,
        uint ep,
        [MarshalAs(UnmanagedType.I1)] bool turn,
        IntPtr results);
}