using System.Runtime.InteropServices;

namespace ZKTecoManager.Infrastructure.ZKTeco;

internal static class ZKTecoSdk
{
    private const string DllName = "plcommpro.dll";

    [DllImport(DllName, EntryPoint = "Connect", CharSet = CharSet.Ansi)]
    public static extern IntPtr Connect(string parameters);

    [DllImport(DllName, EntryPoint = "Disconnect")]
    public static extern void Disconnect(IntPtr handle);

    [DllImport(DllName, EntryPoint = "PullLastError")]
    public static extern int PullLastError();

    [DllImport(DllName, EntryPoint = "GetDeviceParam", CharSet = CharSet.Ansi)]
    public static extern int GetDeviceParam(IntPtr handle, ref byte buffer,
        int bufferSize, string itemNames);

    [DllImport(DllName, EntryPoint = "SetDeviceParam", CharSet = CharSet.Ansi)]
    public static extern int SetDeviceParam(IntPtr handle, string itemValues);

    [DllImport(DllName, EntryPoint = "GetDeviceData", CharSet = CharSet.Ansi)]
    public static extern int GetDeviceData(IntPtr handle, ref byte buffer,
        int bufferSize, string tableName, string fieldNames,
        string filter, string options);

    [DllImport(DllName, EntryPoint = "SetDeviceData", CharSet = CharSet.Ansi)]
    public static extern int SetDeviceData(IntPtr handle, string tableName,
        string data, string options);

    [DllImport(DllName, EntryPoint = "DeleteDeviceData", CharSet = CharSet.Ansi)]
    public static extern int DeleteDeviceData(IntPtr handle, string tableName,
        string data, string options);

}
