using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace LocalConverter
{
    public sealed class AudioFormat
    {
        public string Name,Extension,Codec,Muxer;public bool Lossless;public int RequiredRate,RequiredChannels;
        public override string ToString(){return Name;}
    }
    public sealed class AudioInfo
    {
        public string Path,Codec;public double Duration;public int Rate,Channels;public long Length,Ticks;
        public string Identity{get{return Path+":"+Length+":"+Ticks;}}
        public bool Unchanged{get{var file=new FileInfo(Path);return file.Exists&&file.Length==Length&&file.LastWriteTimeUtc.Ticks==Ticks;}}
    }
    public sealed class AudioOptions
    {
        public AudioFormat Format;public int Bitrate=192,Rate,Channels,Strength=1,Hum;public bool Denoise;
        public string Context{get{return Format.Name+"|"+Bitrate+"|"+Rate+"|"+Channels+"|"+Denoise+"|"+Strength+"|"+Hum;}}
        public string Filter{get{if(!Denoise)return "";string filter="afftdn=nr="+(Strength==0?8:Strength==2?24:14)+":nf="+(Strength==0?-45:Strength==2?-30:-38)+":tn=0:gs=8";if(Hum>0)filter="bandreject=f="+(Hum==1?50:60)+":width_type=h:width=3,"+filter;return filter;}}
    }
    public sealed class AudioEngine
    {
        public static string Runtime
        {
            get{string direct=System.IO.Path.Combine(Application.StartupPath,"audio");if(File.Exists(System.IO.Path.Combine(direct,"ffmpeg.exe")))return direct;string nested=System.IO.Path.Combine(Application.StartupPath,"dist","文件万能处理器","audio");if(File.Exists(System.IO.Path.Combine(nested,"ffmpeg.exe")))return nested;throw new IOException("找不到音频引擎，请保留完整 dist 发行文件夹。");}
        }
        public static string Quote(string value){return "\""+value.Replace("\"","\\\"")+"\"";}
        public static string Run(string executable,string arguments,CancellationToken token,Action<string> line=null,int timeoutSeconds=7200)
        {
            token.ThrowIfCancellationRequested();var stdout=new StringBuilder();var errors=new StringBuilder();object gate=new object();
            using(var process=new Process{StartInfo=new ProcessStartInfo(System.IO.Path.Combine(Runtime,executable),arguments){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8}})
            using(var group=new ProcessJob())
            {
                process.OutputDataReceived+=(s,e)=>{if(e.Data==null)return;if(line!=null)line(e.Data);else lock(gate){if(stdout.Length<1000000)stdout.AppendLine(e.Data);}};
                process.ErrorDataReceived+=(s,e)=>{if(e.Data!=null)lock(gate){errors.AppendLine(e.Data);if(errors.Length>12000)errors.Remove(0,errors.Length-12000);}};
                process.Start();group.Add(process);process.BeginOutputReadLine();process.BeginErrorReadLine();var watch=Stopwatch.StartNew();
                try{while(!process.WaitForExit(50)){token.ThrowIfCancellationRequested();if(watch.Elapsed.TotalSeconds>timeoutSeconds)throw new IOException("音频处理超时，可缩短文件或重试。");}process.WaitForExit();token.ThrowIfCancellationRequested();if(process.ExitCode!=0)throw new IOException("音频引擎未能处理该文件。可能是格式不支持、文件损坏或编码参数不兼容。\r\n"+errors.ToString());return stdout.ToString();}
                finally{if(!process.HasExited){process.Kill();process.WaitForExit(5000);}}
            }
        }
        static AudioFormat F(string name,string ext,string codec,string muxer,bool lossless=false,int rate=0,int channels=0){return new AudioFormat{Name=name,Extension=ext,Codec=codec,Muxer=muxer,Lossless=lossless,RequiredRate=rate,RequiredChannels=channels};}
        public AudioFormat[] Formats(CancellationToken token)
        {
            string output=Run("ffmpeg.exe","-hide_banner -encoders",token,timeoutSeconds:15);var encoders=new HashSet<string>(output.Split('\n').Select(x=>x.Trim().Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries)).Where(x=>x.Length>1&&x[0].StartsWith("A")).Select(x=>x[1]));
            var all=new[]{F("MP3 · 通用兼容","mp3","libmp3lame","mp3"),F("WAV · 16-bit PCM","wav","pcm_s16le","wav",true),F("FLAC · 无损压缩","flac","flac","flac",true),F("AAC","aac","aac","adts"),F("M4A · AAC","m4a","aac","mp4"),F("M4A · ALAC 无损","m4a","alac","mp4",true),F("OGG · Vorbis","ogg","libvorbis","ogg"),F("Opus","opus","libopus","opus",rate:48000),F("AIFF · 16-bit PCM","aiff","pcm_s16be","aiff",true),F("WMA","wma","wmav2","asf")};
            return all.Where(f=>encoders.Contains(f.Codec)).ToArray();
        }
        public AudioInfo Inspect(string path,CancellationToken token)
        {
            path=System.IO.Path.GetFullPath(path);if(!File.Exists(path))throw new IOException("文件不存在。");string ext=System.IO.Path.GetExtension(path).ToLowerInvariant();if(new[]{".m3u",".m3u8",".pls",".sdp",".ffconcat"}.Contains(ext))throw new IOException("请添加音频文件，不接受播放列表或网络地址。");
            var before=new FileInfo(path);long length=before.Length,ticks=before.LastWriteTimeUtc.Ticks;
            string json=Run("ffprobe.exe","-v error -protocol_whitelist file,pipe -select_streams a:0 -show_entries stream=codec_name,sample_rate,channels,duration:format=duration -of json "+Quote(path),token,timeoutSeconds:30);
            var data=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(json);var streams=((System.Collections.IEnumerable)data["streams"]).Cast<Dictionary<string,object>>().ToArray();if(streams.Length==0)throw new IOException("该文件没有可识别的音轨。");var stream=streams[0];double duration=0;var format=(Dictionary<string,object>)data["format"];object raw;if(format.TryGetValue("duration",out raw)||stream.TryGetValue("duration",out raw))Double.TryParse(raw.ToString(),NumberStyles.Float,CultureInfo.InvariantCulture,out duration);
            before.Refresh();if(before.Length!=length||before.LastWriteTimeUtc.Ticks!=ticks)throw new IOException("检查期间文件发生变化，请重新添加。");return new AudioInfo{Path=path,Codec=stream["codec_name"].ToString(),Rate=Convert.ToInt32(stream["sample_rate"]),Channels=Convert.ToInt32(stream["channels"]),Duration=duration,Length=length,Ticks=ticks};
        }
        public string ConvertFile(AudioInfo info,string folder,AudioOptions options,CancellationToken token,Action<int,string> report,double start=0,double seconds=0,bool preview=false,string outputName=null)
        {
            if(!info.Unchanged)throw new IOException("文件已发生变化，请重新添加："+System.IO.Path.GetFileName(info.Path));Directory.CreateDirectory(folder);var format=options.Format;string temp=OutputFiles.Temporary(folder);double duration=seconds>0?Math.Min(seconds,Math.Max(0,info.Duration-start)):info.Duration;
            if(options.Denoise||(!preview&&format.Muxer=="asf"))return Supplement(info,folder,options,token,report,start,seconds,preview);
            try
            {
                var args=new StringBuilder("-hide_banner -loglevel error -nostdin -y -protocol_whitelist file,pipe ");if(start>0)args.Append("-ss "+start.ToString(CultureInfo.InvariantCulture)+" ");args.Append("-i "+Quote(info.Path)+" -map 0:a:0 -vn -sn -dn -map_metadata -1 -threads 2 ");if(seconds>0)args.Append("-t "+seconds.ToString(CultureInfo.InvariantCulture)+" ");if(options.Filter!="")args.Append("-af "+Quote(options.Filter)+" ");
                string codec=preview?"pcm_s16le":format.Codec,muxer=preview?"wav":format.Muxer;args.Append("-c:a "+codec+" ");
                int rate=preview?44100:format.RequiredRate>0?format.RequiredRate:options.Rate,channels=preview?2:format.RequiredChannels>0?format.RequiredChannels:options.Channels>0?options.Channels:Math.Min(info.Channels,2);
                if(rate==0&&!format.Lossless)rate=48000;
                if(rate>0)args.Append("-ar "+rate+" ");args.Append("-ac "+channels+" ");
                if(!preview&&!format.Lossless)args.Append("-b:a "+(codec=="libopencore_amrnb"?"12.2k":options.Bitrate+"k")+" ");args.Append("-progress pipe:1 -nostats -f "+muxer+" "+Quote(temp));
                report(0,"正在处理 "+System.IO.Path.GetFileName(info.Path));Run("ffmpeg.exe",args.ToString(),token,line=>{if(line.StartsWith("out_time_us=")){double value;if(Double.TryParse(line.Substring(12),NumberStyles.Float,CultureInfo.InvariantCulture,out value))report(duration>0?Math.Min(99,(int)(value/1000000/duration*100)):0,"正在处理 "+System.IO.Path.GetFileName(info.Path));}});
                token.ThrowIfCancellationRequested();if(!File.Exists(temp)||new FileInfo(temp).Length==0)throw new IOException("未生成有效音频。");string saved=OutputFiles.Commit(temp,folder,outputName??System.IO.Path.GetFileNameWithoutExtension(info.Path)+"_转换","."+(preview?"wav":format.Extension));report(100,"处理完成");return saved;
            }
            finally{OutputFiles.DeleteTemporary(temp);}
        }
        string Supplement(AudioInfo info,string folder,AudioOptions options,CancellationToken token,Action<int,string> report,double start,double seconds,bool preview)
        {
            string work=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"LocalConverterAudioProcess-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(work);string temp=OutputFiles.Temporary(folder);
            try
            {
                var pcm=F("PCM","wav","pcm_s16le","wav",true);var plain=new AudioOptions{Format=pcm,Rate=options.Rate,Channels=options.Channels};
                string decoded=ConvertFile(info,work,plain,token,(p,s)=>report(p/3,"正在解码 · "+s),start,seconds);string filtered=System.IO.Path.Combine(work,"processed.wav");bool wma=!preview&&options.Format.Muxer=="asf";
                string destination=wma?temp:filtered;string args="-hide_banner -v error -nostdin -y -i "+Quote(decoded)+" -map 0:a:0 -threads 2 "+(options.Denoise?"-af "+Quote(options.Filter)+" ":"")+(wma?"-c:a wmav2 -b:a "+options.Bitrate+"k -ar "+(options.Rate>0?options.Rate:48000)+" -f asf ":"-c:a pcm_s16le -rf64 auto -f wav ")+"-progress pipe:1 -nostats "+Quote(destination);
                double duration=seconds>0?Math.Min(seconds,Math.Max(0,info.Duration-start)):info.Duration;Run("AudioDenoise.exe",args,token,line=>{if(line.StartsWith("out_time_us=")){double value;if(Double.TryParse(line.Substring(12),NumberStyles.Float,CultureInfo.InvariantCulture,out value))report(33+(duration>0?Math.Min(32,(int)(32*value/1000000/duration)):0),"正在滤除背景底噪…");}});
                token.ThrowIfCancellationRequested();string name=System.IO.Path.GetFileNameWithoutExtension(info.Path)+(options.Denoise?"_降噪":"_转换");
                if(wma){string saved=OutputFiles.Commit(temp,folder,name,".wma");report(100,"处理完成");return saved;}
                var processed=Inspect(filtered,token);var encoding=new AudioOptions{Format=options.Format,Rate=options.Rate,Channels=options.Channels,Bitrate=options.Bitrate};return ConvertFile(processed,folder,encoding,token,(p,s)=>report(66+p/3,s),0,0,preview,name);
            }
            finally{OutputFiles.DeleteTemporary(temp);try{Directory.Delete(work,true);}catch(IOException){}catch(UnauthorizedAccessException){}}
        }
        public static float[] Wave(string path)
        {
            // Preview files are bounded PCM16 stereo clips, never a whole long recording.
            using(var reader=new BinaryReader(File.OpenRead(path)))
            {
                if(new string(reader.ReadChars(4))!="RIFF")return new float[0];reader.ReadInt32();reader.ReadChars(4);int channels=2,bits=16;long data=0;int length=0;
                while(reader.BaseStream.Position+8<reader.BaseStream.Length){string name=new string(reader.ReadChars(4));int size=reader.ReadInt32();long next=reader.BaseStream.Position+size+(size&1);if(name=="fmt "){reader.ReadInt16();channels=reader.ReadInt16();reader.ReadInt32();reader.ReadInt32();reader.ReadInt16();bits=reader.ReadInt16();}if(name=="data"){data=reader.BaseStream.Position;length=size;break;}reader.BaseStream.Position=next;}
                if(data==0||bits!=16)return new float[0];var peaks=new float[500];int samples=Math.Min(length/2,44100*2*31);for(int i=0;i<samples&&reader.BaseStream.Position+2<=reader.BaseStream.Length;i++){float value=Math.Abs((float)reader.ReadInt16()/32768);int bin=(int)((long)i*peaks.Length/Math.Max(1,samples));if(value>peaks[bin])peaks[bin]=value;}return peaks;
            }
        }
    }
}
