using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace ChilePrimeRepair.Core;

[SupportedOSPlatform("windows")]
public static class WindowsDeviceProbe
{
    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        public uint Length, Load;
        public ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile, TotalVirtual, AvailableVirtual, AvailableExtendedVirtual;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct AdapterDescription
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Description;
        public uint Vendor, Device, Subsystem, Revision;
        public UIntPtr DedicatedVideo, DedicatedSystem, SharedSystem;
        public uint LuidLow; public int LuidHigh; public uint Flags;
    }
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
    [DllImport("dxgi.dll")] private static extern int CreateDXGIFactory1(ref Guid iid, out IntPtr factory);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int EnumerateAdapter(IntPtr factory, uint index, out IntPtr adapter);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int DescribeAdapter(IntPtr adapter, out AdapterDescription description);
    private static T Method<T>(IntPtr instance, int slot) where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance), slot * IntPtr.Size));

    public static DeviceSnapshot Read(string installation)
    {
        ulong? total = null, available = null; long? disk = null;
        var memory = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        if (GlobalMemoryStatusEx(ref memory)) { total = memory.TotalPhysical; available = memory.AvailablePhysical; }
        try { disk = new DriveInfo(Path.GetPathRoot(installation)!).AvailableFreeSpace; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { }
        var graphics = new List<GraphicsDevice>(); bool known = false;
        IntPtr factory = IntPtr.Zero;
        try
        {
            var iid = new Guid("770aae78-f26f-4dba-a829-253c83d1b387");
            if (CreateDXGIFactory1(ref iid, out factory) >= 0)
            {
                var enumerate = Method<EnumerateAdapter>(factory, 12);
                for (uint index = 0; index < 16; index++)
                {
                    if (enumerate(factory, index, out var adapter) < 0) break;
                    try
                    {
                        if (Method<DescribeAdapter>(adapter, 10)(adapter, out var description) >= 0 && (description.Flags & 2) == 0)
                            graphics.Add(new(description.Description, description.DedicatedVideo.ToUInt64(), description.SharedSystem.ToUInt64()));
                    }
                    finally { if (adapter != IntPtr.Zero) Marshal.Release(adapter); }
                }
                known = true;
            }
        }
        catch (Exception e) when (e is COMException or DllNotFoundException or EntryPointNotFoundException) { }
        finally { if (factory != IntPtr.Zero) Marshal.Release(factory); }
        int? preference = null;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\DirectX\UserGpuPreferences", false);
            // Only inspect preferences for actual local game processes, never other applications.
            foreach (var executable in Directory.EnumerateFiles(installation, "FiveM*GTAProcess.exe").Take(16))
            {
                Safety.EnsureNoLinks(executable);
                if (key?.GetValue(Path.GetFullPath(executable)) is not string value) continue;
                if (value.Split(';').Contains("GpuPreference=1", StringComparer.OrdinalIgnoreCase)) { preference = 1; break; }
                if (value.Split(';').Contains("GpuPreference=2", StringComparer.OrdinalIgnoreCase)) preference = 2;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
        return new(total, available, disk, graphics.ToArray(), known, preference);
    }
}
