using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LocalConverter
{
    // The job contains only a newly launched helper and its descendants.
    sealed class ProcessJob : IDisposable
    {
        IntPtr handle;
        [StructLayout(LayoutKind.Sequential)]struct BasicLimits{public long PerProcess,PerJob;public uint Flags;public UIntPtr MinWorking,MaxWorking;public uint Active;public UIntPtr Affinity;public uint Priority,Scheduling;}
        [StructLayout(LayoutKind.Sequential)]struct IoCounters{public ulong ReadOperations,WriteOperations,OtherOperations,ReadBytes,WriteBytes,OtherBytes;}
        [StructLayout(LayoutKind.Sequential)]struct ExtendedLimits{public BasicLimits Basic;public IoCounters Io;public UIntPtr ProcessMemory,JobMemory,PeakProcess,PeakJob;}
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr CreateJobObject(IntPtr attributes,string name);
        [DllImport("kernel32.dll",SetLastError=true)]static extern bool SetInformationJobObject(IntPtr job,int type,ref ExtendedLimits info,uint size);
        [DllImport("kernel32.dll",SetLastError=true)]static extern bool AssignProcessToJobObject(IntPtr job,IntPtr process);
        [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr value);
        public ProcessJob(){handle=CreateJobObject(IntPtr.Zero,null);var info=new ExtendedLimits();info.Basic.Flags=0x2000;if(handle==IntPtr.Zero||!SetInformationJobObject(handle,9,ref info,(uint)Marshal.SizeOf(info))){int error=Marshal.GetLastWin32Error();Dispose();throw new Win32Exception(error);}}
        public void Add(Process process){if(!AssignProcessToJobObject(handle,process.Handle)&&!process.HasExited)throw new Win32Exception(Marshal.GetLastWin32Error());}
        public void Dispose(){if(handle!=IntPtr.Zero){CloseHandle(handle);handle=IntPtr.Zero;}}
    }
}
