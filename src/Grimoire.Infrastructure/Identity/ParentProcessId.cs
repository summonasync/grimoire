using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Grimoire.Infrastructure.Identity;

public static class ParentProcessId
{
    private const int ProcessBasicInformationClass = 0;

    public static int? Resolve() => Resolve(Environment.ProcessId);

    public static int? Resolve(int processId)
    {
        try
        {
            if (OperatingSystem.IsLinux())
            {
                return ParseLinuxStat(File.ReadAllText($"/proc/{processId}/stat"));
            }

            return OperatingSystem.IsWindows() ? Windows(processId) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static int? ParseLinuxStat(string stat)
    {
        var commEnd = stat.LastIndexOf(')');
        if (commEnd < 0)
        {
            return null;
        }

        var fields = stat[(commEnd + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return fields.Length >= 2 && int.TryParse(fields[1], out var parent) ? parent : null;
    }

    private static int? Windows(int processId)
    {
        using var owner = processId == Environment.ProcessId
            ? Process.GetCurrentProcess()
            : Process.GetProcessById(processId);
        var info = new ProcessBasicInformation();
        var status = NtQueryInformationProcess(
            owner.Handle,
            ProcessBasicInformationClass,
            ref info,
            Marshal.SizeOf<ProcessBasicInformation>(),
            IntPtr.Zero);
        var raw = info.InheritedFromUniqueProcessId.ToInt64();
        return status == 0 && raw is > 0 and <= int.MaxValue ? (int)raw : null;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessBasicInformation
    {
        public IntPtr Reserved1;

        public IntPtr PebBaseAddress;

        public IntPtr Reserved2a;

        public IntPtr Reserved2b;

        public IntPtr UniqueProcessId;

        public IntPtr InheritedFromUniqueProcessId;
    }

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(
        IntPtr processHandle,
        int processInformationClass,
        ref ProcessBasicInformation processInformation,
        int processInformationLength,
        IntPtr returnLength);
}
