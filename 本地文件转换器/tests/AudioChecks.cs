using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using LocalConverter;

static class AudioChecks
{
    static int checks;static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
    static T Field<T>(object value,string name){return (T)value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(value);}
    static object Call(object value,string name,params object[] args){SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());return value.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(value,args);}
    static void Pump(Task task){var watch=System.Diagnostics.Stopwatch.StartNew();while(!task.IsCompleted){Application.DoEvents();Thread.Sleep(5);if(watch.Elapsed.TotalSeconds>60)throw new Exception("UI timeout");}task.GetAwaiter().GetResult();Application.DoEvents();}
    static void Shot(Form form,string path){Application.DoEvents();using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,form.Size));image.Save(path);}}
    static string Fixture(string folder){string path=Path.Combine(folder,"中文 噪声样本.wav");int samples=48000*8;var random=new Random(14);using(var stream=new BinaryWriter(File.Create(path))){stream.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));stream.Write(36+samples*2);stream.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));stream.Write(16);stream.Write((short)1);stream.Write((short)1);stream.Write(48000);stream.Write(96000);stream.Write((short)2);stream.Write((short)16);stream.Write(System.Text.Encoding.ASCII.GetBytes("data"));stream.Write(samples*2);for(int i=0;i<samples;i++){double noise=(random.NextDouble()*2-1)*0.035,tone=i<48000?0:Math.Sin(2*Math.PI*440*i/48000)*0.25;stream.Write((short)((tone+noise)*32767));}}return path;}
    [STAThread]static int Main(string[] args)
    {
        try
        {
            string root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);string source=Fixture(root);byte[] original=File.ReadAllBytes(source);var engine=new AudioEngine();var formats=engine.Formats(CancellationToken.None);Check(formats.Length==10,"ten common encoder/container choices available");var info=engine.Inspect(source,CancellationToken.None);Check(info.Rate==48000&&info.Channels==1&&Math.Abs(info.Duration-8)<0.01,"metadata recognizes PCM duration rate and channels");
            foreach(var format in formats)
            {
                string folder=Path.Combine(root,"format-"+formats.ToList().IndexOf(format));string converted=engine.ConvertFile(info,folder,new AudioOptions{Format=format},CancellationToken.None,(p,s)=>{});var convertedInfo=engine.Inspect(converted,CancellationToken.None);Check(Math.Abs(convertedInfo.Duration-8)<0.4&&convertedInfo.Channels>0,"encode and reopen "+format.Name);
                string back=engine.ConvertFile(convertedInfo,Path.Combine(folder,"roundtrip"),new AudioOptions{Format=formats.First(f=>f.Extension=="wav")},CancellationToken.None,(p,s)=>{});Check(engine.Inspect(back,CancellationToken.None).Duration>7.5,"decode back to WAV "+format.Name);
            }
            var wav=formats.First(f=>f.Extension=="wav");string denoised=engine.ConvertFile(info,Path.Combine(root,"denoised"),new AudioOptions{Format=wav,Denoise=true,Strength=1},CancellationToken.None,(p,s)=>{});Check(engine.Inspect(denoised,CancellationToken.None).Duration>7.99,"denoise retains full duration");
            using(var cancel=new CancellationTokenSource()){string folder=Path.Combine(root,"cancelled");bool cancelled=false;try{engine.ConvertFile(info,folder,new AudioOptions{Format=wav,Denoise=true},cancel.Token,(p,s)=>cancel.Cancel());}catch(OperationCanceledException){cancelled=true;}Check(cancelled&&!Directory.GetFiles(folder).Any(),"cancellation removes unfinished audio and helper exits");}
            Check(original.SequenceEqual(File.ReadAllBytes(source)),"all conversions preserve input bytes");
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            using(var form=new MainForm(new string[0]))
            {
                form.Show();Application.DoEvents();Call(form,"SwitchWorkspace",true);var audio=Field<AudioWorkspace>(form,"audioWorkspace");Pump(Field<Task>(audio,"operation"));Call(audio,"QueueAdd",(object)new[]{source});Pump(Field<Task>(audio,"operation"));Check(Field<FileCards>(audio,"cards").Items.Count==1,"real audio workspace accepts local file");
                Call(audio,"SetMode","背景降噪");Field<ComboBox>(audio,"format").SelectedItem=formats.FirstOrDefault();var formatBox=Field<ComboBox>(audio,"format");formatBox.SelectedIndex=1;Call(audio,"Queue",new Func<CancellationToken,Task>(token=>(Task)Call(audio,"GenerateSample",token)));Pump(Field<Task>(audio,"operation"));Check(File.Exists(Field<string>(audio,"sourceClip"))&&File.Exists(Field<string>(audio,"resultClip"))&&Field<AudioWaveform>(audio,"wave").Result.Length==500,"A/B preview generates real encoded result and bounded waveforms");Shot(form,Path.Combine(root,"audio-denoise.png"));
                Call(audio,"Queue",new Func<CancellationToken,Task>(token=>(Task)Call(audio,"Process",token)));Pump(Field<Task>(audio,"operation"));var store=Field<ToolResultStore>(audio,"outcomes");var result=store.Get("背景降噪");Check(result.Files.Length==1&&Field<ModernButton>(audio,"download").Visible,"real denoise dispatch produces downloadable result");result.Downloaded=true;
                Call(audio,"SetMode","格式转换");Check(!Field<ModernButton>(audio,"download").Visible&&Field<SmoothProgress>(audio,"progress").Value==0,"audio tools isolate result download and progress");form.Size=form.MinimumSize;Application.DoEvents();Shot(form,Path.Combine(root,"audio-minimum.png"));Check(Field<ModernButton>(audio,"start").Bottom<=Field<ModernButton>(audio,"start").Parent.ClientSize.Height,"minimum window keeps processing controls inside footer");Call(form,"SwitchWorkspace",false);Check(!audio.Visible,"PDF workspace remains available");form.Close();while(!form.IsDisposed){Application.DoEvents();Thread.Sleep(5);}
            }
            Console.WriteLine("ALL "+checks+" AUDIO CHECKS PASSED");return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
