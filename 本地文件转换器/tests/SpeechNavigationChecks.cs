using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using LocalConverter;
static class SpeechNavigationChecks
{
    static int count;static void Check(bool value,string name){if(!value)throw new Exception(name);count++;Console.WriteLine("PASS "+name);}
    static T Field<T>(object value,string name){return (T)value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(value);}
    static object Call(object value,string name,params object[] args){SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());return value.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(value,args);}
    static void Wait(Task task){var watch=System.Diagnostics.Stopwatch.StartNew();while(!task.IsCompleted){Application.DoEvents();Thread.Sleep(4);if(watch.Elapsed.TotalSeconds>30)throw new Exception("UI timeout");}task.GetAwaiter().GetResult();Application.DoEvents();}
    static void Shot(Form f,string p){using(var b=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(b,new Rectangle(Point.Empty,f.Size));b.Save(p);}}
    [STAThread]static int Main(string[] args)
    {
        try
        {
            string root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            using(var f=new MainForm(new string[0]))
            {
                f.Show();Application.DoEvents();Call(f,"ChangeWorkspace",1);var audio=Field<AudioWorkspace>(f,"audioWorkspace");Wait(Field<Task>(audio,"operation"));Call(audio,"SetMode","语音转文字");var speech=Field<SpeechWorkspace>(audio,"speech");
                Check(speech.Visible&&!Field<Control>(audio,"normalBody").Visible&&!Field<Control>(audio,"normalFooter").Visible,"new speech tab owns the visible audio body and actions");Check(Field<FunctionTab>(audio,"recognizeTab").Active&&!Field<FunctionTab>(audio,"denoiseTab").Active,"four audio tabs have distinct navigation state");
                f.Size=f.MinimumSize;Application.DoEvents();var start=Field<ModernButton>(speech,"start");var install=Field<ModernButton>(speech,"install");var offline=Field<ModernButton>(speech,"offline");Shot(f,Path.Combine(root,"audio-speech-asr-minimum.png"));Check(start.Visible&&start.Bottom<=start.Parent.ClientSize.Height,"minimum window keeps speech action inside the footer");Check(install.Right<=install.Parent.ClientSize.Width&&install.Bottom<=install.Parent.ClientSize.Height,"resource download control is fully visible at minimum size: "+install.Bounds+" parent "+install.Parent.ClientSize);Check(offline.Bottom<=offline.Parent.ClientSize.Height,"offline import row does not clip its buttons");
                Call(audio,"SetMode","语音生成");Check(speech.Visible&&Field<FunctionTab>(audio,"synthesisTab").Active,"synthesis navigation opens the complete speech panel");Check(!Field<ModernButton>(speech,"download").Visible,"an unprocessed synthesis tab has no previous download action");Shot(f,Path.Combine(root,"audio-speech-tts-minimum.png"));
                Call(audio,"SetMode","格式转换");Check(!speech.Visible&&Field<Control>(audio,"normalBody").Visible&&Field<Control>(audio,"normalFooter").Visible,"returning to conversion restores original file and action panels");Call(audio,"SetMode","背景降噪");Check(Field<FunctionTab>(audio,"denoiseTab").Active&&!Field<FunctionTab>(audio,"recognizeTab").Active,"denoise retains independent navigation state");Call(f,"ChangeWorkspace",0);Check(!audio.Visible,"PDF workspace still switches normally");f.Close();while(!f.IsDisposed){Application.DoEvents();Thread.Sleep(4);}
            }
            Console.WriteLine("ALL "+count+" SPEECH NAVIGATION CHECKS PASSED");return 0;
        }catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
