using System;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using LocalConverter;

static class SpeechChecks
{
    static int checks;
    static void Check(bool value,string name){if(!value)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
    static T Field<T>(object value,string name){return (T)value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(value);}
    static void Context(){SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());}
    static object Call(object value,string name,params object[] args){Context();return value.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(value,args);}
    static void Pump(Task task){var clock=System.Diagnostics.Stopwatch.StartNew();while(!task.IsCompleted){Application.DoEvents();Thread.Sleep(4);if(clock.Elapsed.TotalSeconds>240)throw new Exception("speech UI timeout");}task.GetAwaiter().GetResult();Application.DoEvents();}
    static void Wait(SpeechWorkspace pane){Pump(Field<Task>(pane,"operation"));}
    static void Shot(Form form,string file){Application.DoEvents();using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(file);}}
    static void Rejected(Action action,string label){try{action();throw new Exception("unexpected success: "+label);}catch(IOException){Check(true,label);}}
    static void Zip(string path,params string[] entries){using(var z=ZipFile.Open(path,ZipArchiveMode.Create))foreach(string entry in entries){var e=z.CreateEntry(entry);using(var w=new StreamWriter(e.Open()))w.Write("data");}}
    [STAThread]static int Main(string[] args)
    {
        try
        {
            string root=Path.GetFullPath(args[0]),artifacts=Path.GetFullPath(args[1]);Directory.CreateDirectory(root);var token=CancellationToken.None;var store=new EngineStore(Path.Combine(root,"中文测试外置引擎"));
            Check(!store.Installed(SpeechCatalog.Tiny)&&!store.Installed(SpeechCatalog.Sherpa),"fresh external-engine folder is not treated as installed");
            Rejected(()=>EngineStore.Inside(store.Root,"../escape"),"resource traversal is rejected");Rejected(()=>EngineStore.Inside(store.Root,"C:\\escape"),"absolute resource paths are rejected");
            string bad=Path.Combine(root,"bad.zip");Zip(bad,"../escape.txt");Rejected(()=>store.ImportVoicePack(bad,token),"ZIP traversal never escapes the import folder");Check(!File.Exists(Path.Combine(store.Root,"escape.txt")),"failed archive leaves no escaped file");
            string executable=Path.Combine(root,"executable.zip");Zip(executable,"suspicious.exe");Rejected(()=>store.ImportVoicePack(executable,token),"voice imports reject executable payloads");
            string marker=Path.Combine(root,"marker.zip");Zip(marker,"installed.json");Rejected(()=>store.ImportVoicePack(marker,token),"a voice pack cannot provide its own installed marker");
            var forged=new EnginePackage{Id="test",Folder="models/test",Bytes=4,Sha256=new string('0',64),Archive="file",FileName="model.bin"};string fake=Path.Combine(root,"fake.bin");File.WriteAllText(fake,"test");Rejected(()=>store.InstallArchive(forged,fake,token,s=>{}),"incorrect SHA256 cannot install or enable an engine");Check(!store.Installed(forged),"failed hash check never leaves a ready engine");
            foreach(var package in new[]{SpeechCatalog.Whisper,SpeechCatalog.Sherpa,SpeechCatalog.Tiny,SpeechCatalog.Kokoro11})store.InstallArchive(package,Path.Combine(artifacts,package.Id),token,s=>{});
            Check(new[]{SpeechCatalog.Whisper,SpeechCatalog.Sherpa,SpeechCatalog.Tiny,SpeechCatalog.Kokoro11}.All(store.Installed),"real official engine and model archives install and verify");
            store.Verify(SpeechCatalog.Whisper,token);store.Verify(SpeechCatalog.Sherpa,token);Check(true,"installed CPU libraries pass post-install integrity checks");
            string model=Path.Combine(store.PackageRoot(SpeechCatalog.Tiny),"model.bin");File.Move(model,model+".gone");Check(!store.Installed(SpeechCatalog.Tiny),"missing model immediately disables readiness");File.Move(model+".gone",model);Check(store.Installed(SpeechCatalog.Tiny),"restoring an unchanged model restores readiness");
            var pack=SpeechCatalog.Pack(SpeechCatalog.Kokoro11,store);Check(pack.Voices.Length==103&&pack.Voices.Count(v=>v.Gender=="男")==45,"base voice pack exposes its real 103-speaker catalog");
            var extended=SpeechCatalog.Pack(SpeechCatalog.Kokoro10,store);Check(extended.Voices.Any(v=>v.Language=="en"&&v.Gender=="男")&&extended.Voices.Any(v=>v.Language=="zh"&&v.Gender=="女"),"additional voice pack supplies English male and Chinese voices");
            string custom=Path.Combine(root,"custom.zip");var manifest=new LocalVoiceManifest{Name="导入数据验证",Adapter="kokoro",Model="model.onnx",VoicesFile="voices.bin",Tokens="tokens.txt",Lexicons="lexicon.txt",License="test fixture",Voices=new[]{new SpeechVoice{Id=0,Name="测试音色",Language="zh",Gender="女"}}};
            using(var z=ZipFile.Open(custom,ZipArchiveMode.Create)){foreach(string name in new[]{"model.onnx","voices.bin","tokens.txt","lexicon.txt"})using(var w=new StreamWriter(z.CreateEntry(name).Open()))w.Write("fixture");using(var w=new StreamWriter(z.CreateEntry("voice-pack.json").Open()))w.Write(new JavaScriptSerializer().Serialize(manifest));}
            var imported=store.ImportVoicePack(custom,token);Check(store.VoicePacks().Any(p=>p.Id==imported.Id)&&store.VoiceReady(imported),"compatible data pack registers without changing application code");store.VerifyVoice(imported,token);File.AppendAllText(imported.File(imported.Tokens),"changed");Check(!store.VoiceReady(imported),"changed imported model metadata immediately disables its readiness");Rejected(()=>store.VerifyVoice(imported,token),"changed imported voice data fails integrity validation");
            var jobs=new SpeechJobs(store);string generated=Path.Combine(root,"中文生成结果");Directory.CreateDirectory(generated);string wave=jobs.Generate(pack,3,"欢迎使用文件万能处理器。我们正在测试离线语音识别功能，这段声音将转换成中文文字。",1,generated,token,(p,s)=>{});var audio=new AudioEngine();var info=audio.Inspect(wave,token);Check(info.Duration>3&&info.Rate==24000,"real Chinese TTS writes a nonempty 24 kHz waveform in a Chinese path");
            string english=Path.Combine(root,"English");Directory.CreateDirectory(english);string en=jobs.Generate(pack,0,"Hello. Welcome to our local file processing application. This is a test of offline speech recognition.",1,english,token,(p,s)=>{});Check(audio.Inspect(en,token).Duration>3,"real English voice generates a complete preview waveform");
            string transcribed=Path.Combine(root,"中文转写结果");Directory.CreateDirectory(transcribed);var result=jobs.Transcribe(audio.Inspect(en,token),SpeechCatalog.Tiny,"en",transcribed,token,s=>{});Check(result.Text.Length>15&&(result.Text.ToLowerInvariant().Contains("test")||result.Text.ToLowerInvariant().Contains("welcome")),"real Whisper CPU transcription recognizes generated English speech");Check(File.ReadAllText(result.Srt).Contains("-->"),"transcription exports real subtitle timestamps");
            var cn=jobs.Transcribe(info,SpeechCatalog.Tiny,"zh",generated,token,s=>{});Check(cn.Text.Any(c=>c>='\u3400'&&c<='\u9fff'),"real Whisper can transcribe Chinese speech without path corruption");
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            var emptyStore=new EngineStore(Path.Combine(root,"未下载引擎"));
            using(var form=new Form{Size=new Size(1280,850)})using(var pane=new SpeechWorkspace(emptyStore))
            {form.Controls.Add(pane);form.Show();Application.DoEvents();Check(!Field<ModernButton>(pane,"start").Enabled&&Field<ModernButton>(pane,"install").Enabled,"missing ASR engines disable processing while keeping download visible");pane.SetMode("语音生成");Check(!Field<ModernButton>(pane,"start").Enabled&&!Field<ModernButton>(pane,"sample").Enabled,"missing TTS engines disable generation and audition");Check(Field<Label>(pane,"resourceState").Text.Contains("联网"),"uninstalled resource card explicitly explains online download");Shot(form,Path.Combine(root,"speech-not-installed.png"));form.Close();}
            using(var form=new Form{Size=new Size(1280,900)})using(var pane=new SpeechWorkspace(store))
            {
                form.Controls.Add(pane);form.Show();Application.DoEvents();pane.SetMode("语音生成");Check(Field<ModernButton>(pane,"start").Enabled,"downloaded engines enable real synthesis");Context();Field<ComboBox>(pane,"gender").SelectedIndex=2;Check(Field<ComboBox>(pane,"voice").Items.Count==45,"gender filter selects only actual male speaker IDs");Field<ComboBox>(pane,"gender").SelectedIndex=0;Field<ComboBox>(pane,"voiceLanguage").SelectedIndex=1;Check(Field<ComboBox>(pane,"voice").Items.Count==3,"language filter lists available English voices");Field<TextBox>(pane,"text").Text="Hello, welcome to the offline audio tool.";Field<ComboBox>(pane,"outputFormat").SelectedItem="MP3";Call(pane,"Run","process",new Func<CancellationToken,Task>(t=>(Task)Call(pane,"Process",t)));Wait(pane);var outcomes=Field<ToolResultStore>(pane,"outcomes");Check(outcomes.Get("语音生成").Files.Length==1&&Path.GetExtension(outcomes.Get("语音生成").Files[0])==".mp3","synthesis UI exports actual MP3 through bundled audio engine");outcomes.Get("语音生成").Downloaded=true;Shot(form,Path.Combine(root,"speech-generation.png"));
                pane.SetMode("语音转文字");Check(!Field<ModernButton>(pane,"download").Visible,"switching to unprocessed transcription hides synthesis download");pane.AddAudio(en);Wait(pane);Context();Field<ComboBox>(pane,"language").SelectedIndex=2;Field<ModernButton>(pane,"start").PerformClick();Wait(pane);Check(Field<TextBox>(pane,"transcript").Text.Length>15&&Field<ModernButton>(pane,"download").Visible,"ASR UI displays actual transcription and reveals its own download");Field<TextBox>(pane,"transcript").Text="Corrected transcript";Check(!outcomes.Get("语音转文字").Downloaded,"manual transcript correction is tracked as an unsaved result");Field<ComboBox>(pane,"textFormat").SelectedIndex=1;Check(Field<TextBox>(pane,"transcript").ReadOnly&&Field<TextBox>(pane,"transcript").Text.Contains("-->"),"SRT mode displays its real immutable time-coded subtitle");Field<ComboBox>(pane,"textFormat").SelectedIndex=0;Check(Field<TextBox>(pane,"transcript").Text=="Corrected transcript","switching subtitle view retains corrected TXT content");outcomes.Get("语音转文字").Downloaded=true;Shot(form,Path.Combine(root,"speech-transcription.png"));Pump(pane.StopAsync());form.Close();
            }
            Console.WriteLine("ALL "+checks+" SPEECH CHECKS PASSED");return 0;
        }catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
