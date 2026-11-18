using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace BalatroTrainer.Gui;

internal static class Injector
{
    private const uint PROCESS_CREATE_THREAD = 0x0002;
    private const uint PROCESS_VM_OPERATION = 0x0008;
    private const uint PROCESS_VM_READ = 0x0010;
    private const uint PROCESS_VM_WRITE = 0x0020;
    private const uint PROCESS_QUERY_INFORMATION = 0x0400;

    private const uint MEM_COMMIT = 0x1000;
    private const uint MEM_RESERVE = 0x2000;
    private const uint MEM_RELEASE = 0x8000;
    private const uint PAGE_READWRITE = 0x04;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inherit, int pid);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr VirtualAllocEx(IntPtr process, IntPtr address, nuint size, uint type, uint protect);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualFreeEx(IntPtr process, IntPtr address, nuint size, uint type);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool WriteProcessMemory(IntPtr process, IntPtr address, byte[] buffer, nuint size, out nuint written);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateRemoteThread(IntPtr process, IntPtr attrs, nuint stack, IntPtr start, IntPtr param, uint flags, out uint threadId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetExitCodeThread(IntPtr thread, out uint exitCode);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
    private static extern IntPtr GetProcAddress(IntPtr module, string name);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr GetModuleHandleW(string name);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool IsWow64Process(IntPtr process, out bool wow64);

    public static bool IsLoaded(Process process, string dllFileName)
    {
        try
        {
            foreach (ProcessModule m in process.Modules)
            {
                if (string.Equals(m.ModuleName, dllFileName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        catch (Win32Exception)
        {
            // Module list not readable (process starting up or exiting) - treat as not loaded.
        }
        catch (InvalidOperationException)
        {
        }
        return false;
    }

    public static void Inject(int pid, string dllPath)
    {
        if (!File.Exists(dllPath))
            throw new FileNotFoundException("BalatroTrainer.dll was not found next to the trainer.", dllPath);

        IntPtr proc = OpenProcess(
            PROCESS_CREATE_THREAD | PROCESS_VM_OPERATION | PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_QUERY_INFORMATION,
            false, pid);
        if (proc == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not open the game process");

        try
        {
            if (IsWow64Process(proc, out bool wow) && wow)
                throw new InvalidOperationException("The game is a 32-bit process; this trainer is 64-bit only.");

            byte[] pathBytes = Encoding.Unicode.GetBytes(dllPath + "\0");
            nuint size = (nuint)pathBytes.Length;

            IntPtr remote = VirtualAllocEx(proc, IntPtr.Zero, size, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
            if (remote == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "VirtualAllocEx failed");

            try
            {
                if (!WriteProcessMemory(proc, remote, pathBytes, size, out _))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "WriteProcessMemory failed");

                IntPtr loadLibrary = GetProcAddress(GetModuleHandleW("kernel32.dll"), "LoadLibraryW");
                if (loadLibrary == IntPtr.Zero)
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not locate LoadLibraryW");

                IntPtr thread = CreateRemoteThread(proc, IntPtr.Zero, 0, loadLibrary, remote, 0, out _);
                if (thread == IntPtr.Zero)
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateRemoteThread failed");

                try
                {
                    if (WaitForSingleObject(thread, 10_000) != 0)
                        throw new TimeoutException("The game did not finish loading the backend DLL in time.");

                    GetExitCodeThread(thread, out uint code);
                    if (code == 0)
                        throw new InvalidOperationException(
                            "LoadLibrary failed inside the game (is the DLL 64-bit and in a readable folder?).");
                }
                finally
                {
                    CloseHandle(thread);
                }
            }
            finally
            {
                VirtualFreeEx(proc, remote, 0, MEM_RELEASE);
            }
        }
        finally
        {
            CloseHandle(proc);
        }
    }
}
