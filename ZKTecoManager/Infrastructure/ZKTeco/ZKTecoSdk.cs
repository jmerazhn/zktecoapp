using System.Runtime.InteropServices;

namespace ZKTecoManager.Infrastructure.ZKTeco;

/// <summary>
/// P/Invoke wrapper for plcommpro.dll (Pull SDK x64).
/// All calls are thread-safe via the lock in ZKTecoDevice.
/// </summary>
internal static class ZKTecoSdk
{
    private const string DllName = "plcommpro.dll";

    [DllImport(DllName, CharSet = CharSet.Ansi)]
    public static extern IntPtr Connect(string parameters);

    [DllImport(DllName)]
    public static extern void Disconnect(IntPtr handle);

    [DllImport(DllName, CharSet = CharSet.Ansi)]
    public static extern int GetDeviceParam(IntPtr handle, ref byte buffer, int bufferSize, string paramName);

    [DllImport(DllName, CharSet = CharSet.Ansi)]
    public static extern int SetDeviceParam(IntPtr handle, string parameters);

    [DllImport(DllName, CharSet = CharSet.Ansi)]
    public static extern int ReadAllUserID(IntPtr handle, ref byte buffer, int bufferSize, ref int size, int option);

    [DllImport(DllName, CharSet = CharSet.Ansi)]
    public static extern int SSR_SetUserInfo(IntPtr handle, string enrollNumber, string name, string password,
        int privilege, bool enabled);

    [DllImport(DllName, CharSet = CharSet.Ansi)]
    public static extern int DeleteUser(IntPtr handle, int machineNumber, string enrollNumber, int backupNumber);

    [DllImport(DllName, CharSet = CharSet.Ansi)]
    public static extern int ReadGeneralLogData(IntPtr handle, ref byte buffer, int bufferSize, ref int size);

    [DllImport(DllName, CharSet = CharSet.Ansi)]
    public static extern int ClearGLog(IntPtr handle);

    [DllImport(DllName, CharSet = CharSet.Ansi)]
    public static extern int GetRTLog(IntPtr handle, ref byte buffer, int bufferSize);

    [DllImport(DllName, CharSet = CharSet.Ansi)]
    public static extern int BeginBatchWrite(IntPtr handle);

    [DllImport(DllName, CharSet = CharSet.Ansi)]
    public static extern int BatchWriteCard(IntPtr handle, string enrollNumber, string cardNumber, int machineNumber);

    [DllImport(DllName, CharSet = CharSet.Ansi)]
    public static extern int EndBatchWrite(IntPtr handle, ref int errorCount);

    [DllImport(DllName, CharSet = CharSet.Ansi)]
    public static extern int GetDeviceTime(IntPtr handle, ref int year, ref int month, ref int day,
        ref int hour, ref int minute, ref int second);

    [DllImport(DllName, CharSet = CharSet.Ansi)]
    public static extern int SetDeviceTime2(IntPtr handle, int year, int month, int day,
        int hour, int minute, int second);

    [DllImport(DllName)]
    public static extern int RestartDevice(IntPtr handle);
}
