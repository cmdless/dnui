using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Dnui.Hosting;

/// <summary>
/// Ties every runtime the library starts to this process (spec 003 research R3).
/// <list type="bullet">
/// <item>Windows: children join a kill-on-close job object, so they die with this process however it ends.</item>
/// <item>Unix: SIGINT/SIGTERM/SIGHUP/SIGQUIT received here are forwarded to the children.</item>
/// <item>A child tracked with <c>holdParent</c> (the tool's passthrough) keeps this process alive on those
/// signals until the child exits (killed after <see cref="GracePeriod"/>), so its exit code is returned.</item>
/// </list>
/// Gap: on Unix a SIGKILL of this process cannot be forwarded.
/// </summary>
internal static class ChildProcessLifetime
{
    public static readonly TimeSpan GracePeriod = TimeSpan.FromSeconds(5);

    private static readonly object Gate = new();
    private static readonly Dictionary<Process, bool> Children = [];
    private static List<PosixSignalRegistration>? _registrations;
    private static IntPtr _job;

    /// <summary>Tracks a started child until the returned handle is disposed.</summary>
    public static IDisposable Track(Process child, bool holdParent)
    {
        lock (Gate)
        {
            EnsureRegistered();
            Children[child] = holdParent;
        }

        if (OperatingSystem.IsWindows())
        {
            AssignToJob(child);
        }

        return new Tracking(child);
    }

    private static void EnsureRegistered()
    {
        if (_registrations is not null)
        {
            return;
        }

        _registrations = [];
        foreach (var signal in new[] { PosixSignal.SIGINT, PosixSignal.SIGTERM, PosixSignal.SIGHUP, PosixSignal.SIGQUIT })
        {
            try
            {
                _registrations.Add(PosixSignalRegistration.Create(signal, OnSignal));
            }
            catch (PlatformNotSupportedException)
            {
                // Not every signal exists everywhere (for example SIGHUP on some Windows hosts).
            }
        }
    }

    private static void OnSignal(PosixSignalContext context)
    {
        List<KeyValuePair<Process, bool>> snapshot;
        lock (Gate)
        {
            snapshot = [.. Children];
        }

        var hold = false;
        foreach (var (child, holdParent) in snapshot)
        {
            hold |= holdParent;
            if (!OperatingSystem.IsWindows())
            {
                // Windows consoles deliver Ctrl+C/Break to the child themselves; the job object covers the rest.
                Forward(child, context.Signal);
            }

            if (holdParent)
            {
                _ = KillAfterGraceAsync(child);
            }
        }

        if (hold)
        {
            context.Cancel = true;
        }
    }

    private static void Forward(Process child, PosixSignal signal)
    {
        var number = signal switch
        {
            PosixSignal.SIGHUP => 1,
            PosixSignal.SIGINT => 2,
            PosixSignal.SIGQUIT => 3,
            PosixSignal.SIGTERM => 15,
            _ => 0,
        };
        try
        {
            if (number != 0 && !child.HasExited)
            {
                _ = SysKill(child.Id, number);
            }
        }
        catch (InvalidOperationException)
        {
            // Exited in the meantime.
        }
    }

    private static async Task KillAfterGraceAsync(Process child)
    {
        try
        {
            await child.WaitForExitAsync().WaitAsync(GracePeriod).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            try { child.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static void AssignToJob(Process child)
    {
        try
        {
            lock (Gate)
            {
                if (_job == IntPtr.Zero)
                {
                    var job = CreateJobObject(IntPtr.Zero, null);
                    if (job == IntPtr.Zero)
                    {
                        return;
                    }

                    var info = new JobObjectExtendedLimitInformation { BasicLimitInformation = { LimitFlags = JobObjectLimitKillOnJobClose } };
                    var size = Marshal.SizeOf<JobObjectExtendedLimitInformation>();
                    var buffer = Marshal.AllocHGlobal(size);
                    try
                    {
                        Marshal.StructureToPtr(info, buffer, false);
                        if (!SetInformationJobObject(job, JobObjectExtendedLimitInformationClass, buffer, (uint)size))
                        {
                            CloseHandle(job);
                            return;
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(buffer);
                    }

                    // Never closed: the handle closes when this process ends, which is what kills the children.
                    _job = job;
                }
            }

            _ = AssignProcessToJobObject(_job, child.Handle);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or DllNotFoundException)
        {
            // Best effort: without the job, children still end on catchable signals.
        }
    }

    private sealed class Tracking(Process child) : IDisposable
    {
        public void Dispose()
        {
            lock (Gate)
            {
                Children.Remove(child);
            }
        }
    }

    private const uint JobObjectLimitKillOnJobClose = 0x2000;
    private const int JobObjectExtendedLimitInformationClass = 9;

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectBasicLimitInformation
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectExtendedLimitInformation
    {
        public JobObjectBasicLimitInformation BasicLimitInformation;
        public IoCounters IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateJobObject(IntPtr attributes, string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(IntPtr job, int infoClass, IntPtr info, uint length);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static extern int SysKill(int pid, int signal);
}
