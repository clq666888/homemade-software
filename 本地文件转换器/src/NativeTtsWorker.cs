using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;

namespace LocalConverter
{
    public sealed class TtsJob{public string Library,Model,Voices,Tokens,Lexicons,Rules,Text,Output,Reply,Progress;public int Voice;public float Speed=1;}
    public static class NativeTtsWorker
    {
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr LoadLibrary(string path);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern bool SetDllDirectory(string path);
        [DllImport("kernel32.dll")]static extern IntPtr GetProcAddress(IntPtr library,string name);
        [DllImport("kernel32.dll")]static extern bool FreeLibrary(IntPtr library);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate IntPtr Create(IntPtr config);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate void Release(IntPtr value);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate int Speakers(IntPtr tts);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate int Progress(IntPtr samples,int n,float progress);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate IntPtr Generate(IntPtr tts,IntPtr text,int voice,float speed,Progress callback);
        [StructLayout(LayoutKind.Sequential)]struct Audio{public IntPtr Samples;public int Count,Rate;}
        static T Export<T>(IntPtr library,string name){IntPtr p=GetProcAddress(library,name);if(p==IntPtr.Zero)throw new IOException("语音引擎版本不匹配："+name);return (T)(object)Marshal.GetDelegateForFunctionPointer(p,typeof(T));}
        public static int Run(string jobPath)
        {
            TtsJob job=null;var json=new JavaScriptSerializer{MaxJsonLength=1000000};
            try{job=json.Deserialize<TtsJob>(File.ReadAllText(jobPath,Encoding.UTF8));Synthesize(job);File.WriteAllText(job.Reply,json.Serialize(new{ok=true}),Encoding.UTF8);return 0;}
            catch(Exception e){if(job!=null)File.WriteAllText(job.Reply,json.Serialize(new{ok=false,error=e.Message}),Encoding.UTF8);return 1;}
        }
        static void Synthesize(TtsJob job)
        {
            if(String.IsNullOrWhiteSpace(job.Text)||job.Text.Length>5000||job.Speed<.5||job.Speed>2)throw new IOException("请输入1–5000字，语速需在0.5–2倍之间。");
            var strings=new List<IntPtr>();IntPtr config=IntPtr.Zero,library=IntPtr.Zero,tts=IntPtr.Zero,audio=IntPtr.Zero;Release destroy=null,freeAudio=null;
            Func<string,IntPtr> utf8=value=>{byte[] bytes=Encoding.UTF8.GetBytes((value??"")+"\0");IntPtr p=Marshal.AllocHGlobal(bytes.Length);Marshal.Copy(bytes,0,p,bytes.Length);strings.Add(p);return p;};
            try
            {
                Directory.SetCurrentDirectory(Path.GetDirectoryName(job.Model));SetDllDirectory(Path.GetDirectoryName(job.Library));library=LoadLibrary(job.Library);if(library==IntPtr.Zero)throw new IOException("无法载入语音生成引擎，请重新下载完整CPU引擎。");
                // Pinned sherpa-onnx v2.0.3 x64 C ABI, c-api.h: sizeof(config)=456.
                // Explicit offsets keep all unused model families zero-initialized.
                config=Marshal.AllocHGlobal(456);Marshal.Copy(new byte[456],0,config,456);Marshal.WriteInt32(config,56,Math.Max(1,Math.Min(4,Environment.ProcessorCount)));Marshal.WriteIntPtr(config,64,utf8("cpu"));
                Marshal.WriteIntPtr(config,128,utf8(job.Model));Marshal.WriteIntPtr(config,136,utf8(job.Voices));Marshal.WriteIntPtr(config,144,utf8(job.Tokens));Marshal.WriteInt32(config,160,BitConverter.ToInt32(BitConverter.GetBytes(1F),0));Marshal.WriteIntPtr(config,176,utf8(job.Lexicons));
                if(!String.IsNullOrEmpty(job.Rules))Marshal.WriteIntPtr(config,424,utf8(job.Rules));Marshal.WriteInt32(config,432,1);Marshal.WriteInt32(config,448,BitConverter.ToInt32(BitConverter.GetBytes(.2F),0));
                destroy=Export<Release>(library,"SherpaOnnxDestroyOfflineTts");freeAudio=Export<Release>(library,"SherpaOnnxDestroyOfflineTtsGeneratedAudio");tts=Export<Create>(library,"SherpaOnnxCreateOfflineTts")(config);if(tts==IntPtr.Zero)throw new IOException("音色包无法加载，请检查模型、词典和音色是否属于同一兼容版本。");
                int count=Export<Speakers>(library,"SherpaOnnxOfflineTtsNumSpeakers")(tts);if(job.Voice<0||job.Voice>=count)throw new IOException("该音色编号不存在于当前音色包。");
                Progress report=(samples,n,p)=>{try{File.WriteAllText(job.Progress,Math.Max(0,Math.Min(99,(int)(p*100))).ToString());}catch(IOException){}return 1;};
                audio=Export<Generate>(library,"SherpaOnnxOfflineTtsGenerateWithProgressCallback")(tts,utf8(job.Text),job.Voice,job.Speed,report);GC.KeepAlive(report);if(audio==IntPtr.Zero)throw new IOException("未生成语音，请检查文字语言是否受当前音色包支持。");var data=(Audio)Marshal.PtrToStructure(audio,typeof(Audio));
                if(data.Count<=0||data.Count>24000*60*60||data.Rate<8000||data.Rate>96000)throw new IOException("生成音频为空或超出一小时预算。");
                using(var output=new BinaryWriter(File.Create(job.Output))){int bytes=checked(data.Count*2);output.Write(Encoding.ASCII.GetBytes("RIFF"));output.Write(36+bytes);output.Write(Encoding.ASCII.GetBytes("WAVEfmt "));output.Write(16);output.Write((short)1);output.Write((short)1);output.Write(data.Rate);output.Write(data.Rate*2);output.Write((short)2);output.Write((short)16);output.Write(Encoding.ASCII.GetBytes("data"));output.Write(bytes);float[] buffer=new float[16384];for(int i=0;i<data.Count;i+=buffer.Length){int n=Math.Min(buffer.Length,data.Count-i);Marshal.Copy(IntPtr.Add(data.Samples,i*4),buffer,0,n);for(int j=0;j<n;j++)output.Write((short)Math.Round(Math.Max(-1,Math.Min(1,buffer[j]))*32767));}}
            }
            finally{if(audio!=IntPtr.Zero&&freeAudio!=null)freeAudio(audio);if(tts!=IntPtr.Zero&&destroy!=null)destroy(tts);if(config!=IntPtr.Zero)Marshal.FreeHGlobal(config);foreach(var p in strings)Marshal.FreeHGlobal(p);if(library!=IntPtr.Zero)FreeLibrary(library);SetDllDirectory(null);}
        }
    }
}
