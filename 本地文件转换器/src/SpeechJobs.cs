using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace LocalConverter
{
    public sealed class Transcription{public string Text,Txt,Srt;}
    public sealed class SpeechJobs
    {
        readonly EngineStore store;readonly AudioEngine audio=new AudioEngine();
        public SpeechJobs(EngineStore store){this.store=store;}
        public Transcription Transcribe(AudioInfo input,EnginePackage model,string language,string folder,CancellationToken token,Action<string> status)
        {
            store.Verify(SpeechCatalog.Whisper,token);store.Verify(model,token);if(!input.Unchanged)throw new IOException("原音频已变化，请重新添加。");
            string work=Path.Combine(folder,"job-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(work);
            try
            {
                string wav=Path.Combine(work,"input.wav"),prefix=Path.Combine(work,"text");status("正在准备本地音频…");AudioEngine.Run("ffmpeg.exe","-nostdin -hide_banner -loglevel error -protocol_whitelist file,pipe -i "+SpeechProcess.Quote(input.Path)+" -map 0:a:0 -vn -ar 16000 -ac 1 -c:a pcm_s16le "+SpeechProcess.Quote(wav),token);
                status("正在本地转写，较长音频需要等待，可随时取消…");string exe=EngineStore.Find(store.PackageRoot(SpeechCatalog.Whisper),"whisper-cli.exe");
                SpeechProcess.Run(exe,"-m "+SpeechProcess.Quote(Path.Combine(store.PackageRoot(model),"model.bin"))+" -f "+SpeechProcess.Quote(wav)+" -l "+language+" -otxt -osrt -of "+SpeechProcess.Quote(prefix)+" -t "+Math.Max(1,Math.Min(4,Environment.ProcessorCount))+" -pp",token,line=>{if(line.Contains("progress"))status("本地转写中 · "+line.Trim());},7200);
                if(!File.Exists(prefix+".txt")||!File.Exists(prefix+".srt"))throw new IOException("识别引擎没有产生完整结果。");string text=File.ReadAllText(prefix+".txt",Encoding.UTF8).Trim();if(String.IsNullOrWhiteSpace(text))throw new IOException("没有识别到文字。请检查音频中是否有清晰语音。");
                string txt=Path.Combine(folder,"语音转文字.txt"),srt=Path.Combine(folder,"语音转文字.srt");File.Copy(prefix+".txt",txt,true);File.Copy(prefix+".srt",srt,true);return new Transcription{Text=text,Txt=txt,Srt=srt};
            }
            finally{Directory.Delete(work,true);}
        }
        public string Generate(VoicePack pack,int voice,string text,float speed,string folder,CancellationToken token,Action<int,string> status)
        {
            store.Verify(SpeechCatalog.Sherpa,token);store.VerifyVoice(pack,token);string work=Path.Combine(folder,"tts-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(work);string output=Path.Combine(folder,"语音生成.wav");
            try
            {
                string modelDirectory=Path.GetDirectoryName(pack.File(pack.Model));var rules=new[]{"date-zh.fst","number-zh.fst","phone-zh.fst"}.Select(p=>Path.Combine(modelDirectory,p)).Where(File.Exists).ToArray();
                // Kaldi's rule-file reader uses ANSI fopen on Windows. The isolated
                // worker sets its Unicode working directory to the model folder,
                // then opens ASCII rule basenames instead of a lossy full path.
                var job=new TtsJob{Library=EngineStore.Find(store.PackageRoot(SpeechCatalog.Sherpa),"sherpa-onnx-c-api.dll"),Model=pack.File(pack.Model),Voices=pack.File(pack.VoicesFile),Tokens=pack.File(pack.Tokens),Lexicons=String.Join(",",pack.Lexicons.Split(',').Select(pack.File)),Rules=String.Join(",",rules.Select(Path.GetFileName)),Text=text,Voice=voice,Speed=speed,Output=output,Reply=Path.Combine(work,"reply.json"),Progress=Path.Combine(work,"progress.txt")};
                string path=Path.Combine(work,"job.json");File.WriteAllText(path,new JavaScriptSerializer().Serialize(job),Encoding.UTF8);status(0,"正在载入音色并生成语音，首次载入需要稍等…");
                int last=-1;SpeechProcess.Run(typeof(MainForm).Assembly.Location,"--tts-worker "+SpeechProcess.Quote(path),token,timeout:3600,pulse:()=>{try{int p;if(File.Exists(job.Progress)&&Int32.TryParse(File.ReadAllText(job.Progress),out p)&&p!=last){last=p;status(p,"正在生成语音 · "+p+"%");}}catch(IOException){}});
                if(!File.Exists(job.Reply))throw new IOException("语音生成工作进程异常退出，请修复引擎/音色包后重试。");var result=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(job.Reply));if(!(bool)result["ok"])throw new IOException(Convert.ToString(result["error"]));if(!File.Exists(output)||new FileInfo(output).Length<=44)throw new IOException("生成的音频为空。");status(100,"语音已生成。");return output;
            }
            finally{Directory.Delete(work,true);}
        }
    }
}
