using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace LocalConverter
{
    public sealed class VideoInfo
    {
        public string Path,Codec;public double Duration;public int Width,Height;public bool Audio;public long Bytes,Ticks;
        public string Identity{get{return Path+"|"+Bytes+"|"+Ticks;}}
        public bool Unchanged{get{var f=new FileInfo(Path);return f.Exists&&f.Length==Bytes&&f.LastWriteTimeUtc.Ticks==Ticks;}}
    }
    public sealed class VideoOptions
    {
        public bool Gif,KeepAudio=true,Loop=true;public int Format,Quality=1,MaxEdge,GifWidth=480,Fps=12;public double Start,End=5;
        public string Extension{get{return Gif?".gif":new[]{".mp4",".mkv",".mov",".webm",".avi",".wmv"}[Format];}}
        public string Context{get{return Gif?String.Join("|","gif",Start.ToString(CultureInfo.InvariantCulture),End.ToString(CultureInfo.InvariantCulture),GifWidth,Fps,Loop):String.Join("|","video",Format,Quality,MaxEdge,KeepAudio);}}
        public void Validate(VideoInfo info)
        {
            if(!info.Unchanged)throw new IOException("原视频已变化，请移除后重新添加。");
            if(Format<0||Format>5||Quality<0||Quality>2)throw new IOException("视频输出设置无效。");
            if(Gif&&(Double.IsNaN(Start)||Double.IsNaN(End)||Start<0||End<=Start||End>info.Duration+.01||End-Start>120||GifWidth<80||GifWidth>1280||Fps<1||Fps>30))throw new IOException("GIF 起点须早于终点，且不超出视频；每段最长 120 秒。");
        }
    }
    public sealed class VideoEngine
    {
        readonly EngineStore store;
        const string InputGuard=" -protocol_whitelist file,pipe -format_whitelist mov,matroska,webm,avi,asf,flv,mpeg,mpegts,ogg,gif ";
        static readonly CultureInfo Inv=CultureInfo.InvariantCulture;
        public VideoEngine(EngineStore store){this.store=store;}
        public bool Ready{get{return store.Installed(VideoCatalog.Runtime);}}
        public string Executable(string name){if(!Ready)throw new IOException("请先下载或导入视频引擎。");return EngineStore.Find(store.PackageRoot(VideoCatalog.Runtime),name);}
        public void Verify(CancellationToken token){store.Verify(VideoCatalog.Runtime,token);}
        public static string Number(double value){return value.ToString("0.###",Inv);}
        public static string TimeText(double seconds){var t=TimeSpan.FromSeconds(Math.Max(0,seconds));return ((int)t.TotalHours).ToString("00")+":"+t.Minutes.ToString("00")+":"+t.Seconds.ToString("00")+"."+(t.Milliseconds/100).ToString();}
        public static string Run(string executable,string arguments,CancellationToken token,Action<string> line=null,int timeout=14400)
        {
            var stdout=new StringBuilder();var stderr=new StringBuilder();object gate=new object();
            using(var process=new Process{StartInfo=new ProcessStartInfo(executable,arguments){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,RedirectStandardInput=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8}})
            using(var job=new ProcessJob())
            {
                process.OutputDataReceived+=(s,e)=>{if(e.Data==null)return;lock(gate){stdout.AppendLine(e.Data);if(stdout.Length>1000000)stdout.Remove(0,stdout.Length-1000000);}if(line!=null)line(e.Data);};process.ErrorDataReceived+=(s,e)=>{if(e.Data==null)return;lock(gate){stderr.AppendLine(e.Data);if(stderr.Length>12000)stderr.Remove(0,stderr.Length-12000);}};
                token.ThrowIfCancellationRequested();process.Start();job.Add(process);process.StandardInput.Close();process.BeginOutputReadLine();process.BeginErrorReadLine();var clock=Stopwatch.StartNew();
                try{while(!process.WaitForExit(80)){token.ThrowIfCancellationRequested();if(clock.Elapsed.TotalSeconds>timeout)throw new IOException("视频处理超时，请缩短片段或降低画面大小后重试。");}process.WaitForExit();token.ThrowIfCancellationRequested();if(process.ExitCode!=0)throw new IOException("视频引擎未能完成处理，请检查文件和输出设置。\r\n"+stderr);return stdout.ToString();}
                finally{if(!process.HasExited){process.Kill();process.WaitForExit(5000);}}
            }
        }
        static double Value(object value){double result;return Double.TryParse(Convert.ToString(value,Inv),NumberStyles.Float,Inv,out result)&&!Double.IsInfinity(result)&&!Double.IsNaN(result)?result:0;}
        public VideoInfo Inspect(string path,CancellationToken token)
        {
            var file=new FileInfo(path);if(!file.Exists)throw new IOException("找不到视频文件。");
            string json=Run(Executable("ffprobe.exe"),"-v error"+InputGuard+" -show_entries format=duration:stream=codec_type,codec_name,width,height,duration:stream_side_data=rotation -of json "+SpeechProcess.Quote(file.FullName),token,timeout:45);
            var data=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(json);object list;if(!data.TryGetValue("streams",out list))throw new IOException("没有可读取的视频画面。");var streams=((System.Collections.IEnumerable)list).Cast<Dictionary<string,object>>().ToArray();var video=streams.FirstOrDefault(s=>s.ContainsKey("codec_type")&&(string)s["codec_type"]=="video");if(video==null)throw new IOException("该文件没有视频画面，请使用音频处理。");
            double duration=0;object f;if(data.TryGetValue("format",out f)){object d;if(((Dictionary<string,object>)f).TryGetValue("duration",out d))duration=Value(d);}if(duration<=0&&video.ContainsKey("duration"))duration=Value(video["duration"]);
            int width=video.ContainsKey("width")?(int)Value(video["width"]):0,height=video.ContainsKey("height")?(int)Value(video["height"]):0;if(width<=0||height<=0||(long)width*height>40000000||duration<=0)throw new IOException("无法读取时长，或画面超过 4000 万像素，暂不支持此视频。");
            if(video.ContainsKey("side_data_list"))foreach(Dictionary<string,object> side in (System.Collections.IEnumerable)video["side_data_list"]){object r;if(side.TryGetValue("rotation",out r)&&Math.Abs(Value(r))%180>45){int swap=width;width=height;height=swap;break;}}
            return new VideoInfo{Path=file.FullName,Codec=(string)video["codec_name"],Duration=duration,Width=width,Height=height,Audio=streams.Any(s=>s.ContainsKey("codec_type")&&(string)s["codec_type"]=="audio"),Bytes=file.Length,Ticks=file.LastWriteTimeUtc.Ticks};
        }
        public string Frame(VideoInfo info,double position,string folder,CancellationToken token)
        {
            if(!info.Unchanged)throw new IOException("原视频已变化，请重新添加。");Directory.CreateDirectory(folder);string target=Path.Combine(folder,Guid.NewGuid().ToString("N")+".png");
            try{Run(Executable("ffmpeg.exe"),"-hide_banner -loglevel error -nostdin -threads 2 -ss "+Number(Math.Min(Math.Max(0,position),Math.Max(0,info.Duration-.1)))+InputGuard+" -i "+SpeechProcess.Quote(info.Path)+" -map 0:v:0 -an -sn -dn -vf \"scale=960:540:force_original_aspect_ratio=decrease:reset_sar=1\" -frames:v 1 -update 1 -y "+SpeechProcess.Quote(target),token,timeout:45);return target;}catch{if(File.Exists(target))File.Delete(target);throw;}
        }
        string Encode(string arguments,double duration,CancellationToken token,Action<int> progress)
        {
            return Run(Executable("ffmpeg.exe"),"-hide_banner -loglevel error -nostdin -threads 4 -filter_threads 2 -filter_complex_threads 2 -progress pipe:1 -nostats "+arguments,token,line=>{if(line.StartsWith("out_time_us=")){double t=Value(line.Substring(12))/1000000;if(progress!=null)progress((int)Math.Min(99,Math.Max(0,t/Math.Max(.01,duration)*100)));}});
        }
        public string ConvertFile(VideoInfo info,string folder,VideoOptions options,CancellationToken token,Action<int> progress)
        {
            options.Validate(info);Directory.CreateDirectory(folder);string stem=Path.GetFileNameWithoutExtension(info.Path)+(options.Gif?"_动图":"_转换"),temporary=Path.Combine(folder,Guid.NewGuid().ToString("N")+".tmp"),palette=Path.Combine(folder,Guid.NewGuid().ToString("N")+".tmp");
            try
            {
                string input=InputGuard+" -i "+SpeechProcess.Quote(info.Path);
                if(options.Gif)
                {
                    double duration=options.End-options.Start;string clip="-ss "+Number(options.Start)+input+" -t "+Number(duration);string filter="fps="+options.Fps+",scale=w='min("+options.GifWidth+",iw)':h='min(1280,ih)':force_original_aspect_ratio=decrease:flags=lanczos:reset_sar=1";
                    Encode(clip+" -an -sn -dn -vf "+SpeechProcess.Quote(filter+",palettegen=stats_mode=diff")+" -frames:v 1 -c:v png -f image2 -update 1 -y "+SpeechProcess.Quote(palette),duration,token,p=>{if(progress!=null)progress(p/3);});
                    Encode("-ss "+Number(options.Start)+input+" -i "+SpeechProcess.Quote(palette)+" -t "+Number(duration)+" -an -sn -dn -filter_complex "+SpeechProcess.Quote("[0:v:0]"+filter+"[v];[v][1:v:0]paletteuse=dither=sierra2_4a")+" -loop "+(options.Loop?"0":"-1")+" -f gif -y "+SpeechProcess.Quote(temporary),duration,token,p=>{if(progress!=null)progress(33+p*66/100);});
                }
                else
                {
                    int edge=options.MaxEdge==0?16384:options.MaxEdge;string scale="scale=w='min("+edge+",iw)':h='min("+edge+",ih)':force_original_aspect_ratio=decrease:force_divisible_by=2:reset_sar=1";string video=options.Format<=2?"-c:v libx264 -preset medium -crf "+new[]{20,23,28}[options.Quality]:options.Format==3?"-c:v libvpx-vp9 -b:v 0 -crf "+new[]{27,32,38}[options.Quality]+" -deadline good -cpu-used 3":options.Format==4?"-c:v mpeg4 -q:v "+new[]{3,5,9}[options.Quality]:"-c:v wmv2 -b:v "+new[]{4000,2000,1000}[options.Quality]+"k";
                    string audio=!options.KeepAudio||!info.Audio?"-an":options.Format<=2?"-c:a aac -b:a 192k -ac 2":options.Format==3?"-c:a libopus -b:a 128k -ac 2":options.Format==4?"-c:a libmp3lame -b:a 192k -ac 2":"-c:a wmav2 -b:a 192k -ar 44100 -ac 2";
                    string muxer=new[]{"mp4","matroska","mov","webm","avi","asf"}[options.Format];Encode(input+" -map 0:v:0 "+(options.KeepAudio?"-map 0:a:0? ":"")+" -sn -dn -map_metadata -1 -vf "+SpeechProcess.Quote(scale)+" "+video+" -pix_fmt yuv420p "+audio+(options.Format==0||options.Format==2?" -movflags +faststart":"")+" -f "+muxer+" -y "+SpeechProcess.Quote(temporary),info.Duration,token,progress);
                }
                token.ThrowIfCancellationRequested();if(!info.Unchanged)throw new IOException("处理期间原视频发生变化，请重新添加后处理。");if(!File.Exists(temporary)||new FileInfo(temporary).Length==0)throw new IOException("视频引擎没有生成完整结果。");string output=OutputFiles.Commit(temporary,folder,stem,options.Extension);if(progress!=null)progress(100);return output;
            }
            finally{if(File.Exists(temporary))File.Delete(temporary);if(File.Exists(palette))File.Delete(palette);}
        }
    }
}
