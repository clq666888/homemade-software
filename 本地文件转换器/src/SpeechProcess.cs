using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace LocalConverter
{
    public static class SpeechProcess
    {
        public static string Quote(string value){var b=new StringBuilder("\"");int slashes=0;foreach(char c in value){if(c=='\\'){slashes++;continue;}if(c=='\"'){b.Append('\\',slashes*2+1);b.Append(c);}else{b.Append('\\',slashes);b.Append(c);}slashes=0;}b.Append('\\',slashes*2);b.Append('"');return b.ToString();}
        public static string Run(string exe,string args,CancellationToken token,Action<string> report=null,int timeout=3600,Action pulse=null)
        {
            var output=new StringBuilder();object gate=new object();
            using(var p=new Process{StartInfo=new ProcessStartInfo(exe,args){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true,StandardErrorEncoding=Encoding.UTF8,StandardOutputEncoding=Encoding.UTF8}})
            using(var group=new ProcessJob())
            {
                DataReceivedEventHandler received=(s,e)=>{if(e.Data==null)return;lock(gate){output.AppendLine(e.Data);if(output.Length>16000)output.Remove(0,output.Length-16000);}if(report!=null)report(e.Data);};p.OutputDataReceived+=received;p.ErrorDataReceived+=received;
                token.ThrowIfCancellationRequested();p.Start();group.Add(p);p.BeginOutputReadLine();p.BeginErrorReadLine();var watch=Stopwatch.StartNew();
                try{while(!p.WaitForExit(80)){token.ThrowIfCancellationRequested();if(pulse!=null)pulse();if(watch.Elapsed.TotalSeconds>timeout)throw new IOException("语音处理超时，请缩短输入后重试。");}p.WaitForExit();token.ThrowIfCancellationRequested();if(p.ExitCode!=0)throw new IOException("语音引擎未能完成处理（退出码 "+p.ExitCode+"）。请检查资源是否完整，或重新下载修复。\r\n"+output);return output.ToString();}
                finally{if(!p.HasExited){p.Kill();p.WaitForExit(5000);}}
            }
        }
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr BeginUpdateResource(string file,bool delete);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool UpdateResource(IntPtr update,IntPtr type,IntPtr name,ushort lang,byte[] data,uint size);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool EndUpdateResource(IntPtr update,bool discard);
        public static void EnableWhisperUtf8(string exe)
        {
            // Official Windows CLI uses the process ANSI code page for argv. Declare
            // UTF-8 in its local manifest so Chinese paths work on Windows 10 1903+.
            byte[] manifest=Encoding.UTF8.GetBytes("<?xml version=\"1.0\" encoding=\"utf-8\"?><assembly xmlns=\"urn:schemas-microsoft-com:asm.v1\" manifestVersion=\"1.0\"><assemblyIdentity type=\"win32\" name=\"WhisperUtf8Compatibility\" version=\"1.0.0.0\"/><application xmlns=\"urn:schemas-microsoft-com:asm.v3\"><windowsSettings><activeCodePage xmlns=\"http://schemas.microsoft.com/SMI/2019/WindowsSettings\">UTF-8</activeCodePage></windowsSettings></application></assembly>");
            IntPtr update=BeginUpdateResource(exe,false);if(update==IntPtr.Zero)throw new IOException("无法准备引擎的中文路径兼容设置。");bool saved=false;try{if(!UpdateResource(update,new IntPtr(24),new IntPtr(1),1033,manifest,(uint)manifest.Length))throw new IOException("无法写入中文路径兼容设置。");saved=EndUpdateResource(update,false);update=IntPtr.Zero;if(!saved)throw new IOException("无法保存引擎兼容设置。");}finally{if(update!=IntPtr.Zero)EndUpdateResource(update,true);}
        }
    }
}
