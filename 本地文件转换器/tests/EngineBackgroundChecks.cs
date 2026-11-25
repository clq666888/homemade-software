using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using LocalConverter;

static class EngineBackgroundChecks
{
    static int checks;
    static void Check(bool value,string name){if(!value)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
    static T Field<T>(object value,string name){return (T)value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(value);}
    static object Call(object value,string name,params object[] args){SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());return value.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(value,args);}
    static void Until(Func<bool> ready){var clock=System.Diagnostics.Stopwatch.StartNew();while(!ready()){Application.DoEvents();Thread.Sleep(5);if(clock.Elapsed.TotalSeconds>15)throw new Exception("background task timeout");}}
    static void Wait(Task task){Until(()=>task.IsCompleted);task.GetAwaiter().GetResult();Application.DoEvents();}
    static EnginePackage Package(string id){return new EnginePackage{Id=id,Name="测试资源 "+id,Folder="models/"+id,Bytes=100,Sha256=new string('0',64)};}
    static void Shot(Form form,string path){Application.DoEvents();using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(path);}}
    [STAThread]static int Main(string[] args)
    {
        try
        {
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            string root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);var store=new EngineStore(Path.Combine(root,"external"));var counts=new ConcurrentDictionary<string,int>();int active=0,peak=0;using(var release=new ManualResetEventSlim(false))using(var manager=new EngineDownloadManager(store,2,(package,token,report)=>
            {
                counts.AddOrUpdate(package.Id,1,(k,v)=>v+1);int n=Interlocked.Increment(ref active),old;do{old=peak;if(n<=old)break;}while(Interlocked.CompareExchange(ref peak,n,old)!=old);
                try{report(new EngineProgress{Written=25,Total=100,Message="正在下载测试数据"});while(!release.Wait(10))token.ThrowIfCancellationRequested();token.ThrowIfCancellationRequested();report(new EngineProgress{Written=100,Total=100,Message="正在校验安装"});}
                finally{Interlocked.Decrement(ref active);}
            }))
            {
                var a=Package("a");var b=Package("b");var c=Package("c");manager.Enqueue(new[]{a,b,c,a});Until(()=>counts.Count==2);
                Check(manager.Snapshots.Length==3,"duplicate requests share one package task");Check(manager.Snapshots.Count(s=>s.State==EngineTaskState.Downloading)==2&&manager.Snapshots.Count(s=>s.State==EngineTaskState.Queued)==1,"two resources run concurrently and the third queues");Check(peak==2,"concurrency never exceeds the configured maximum");Check(manager.Snapshots.Where(s=>s.State==EngineTaskState.Downloading).All(s=>s.Percent==25),"each task exposes its own progress snapshot");
                manager.Cancel("a");Until(()=>manager.Snapshots.Single(s=>s.Package.Id=="a").State==EngineTaskState.Cancelled&&counts.ContainsKey("c"));Check(manager.Snapshots.Single(s=>s.Package.Id=="b").Active,"cancelling one resource does not cancel its peer");Check(counts.ContainsKey("c"),"a queued resource starts when a slot becomes free");manager.Enqueue(new[]{a});Until(()=>manager.Snapshots.Single(s=>s.Package.Id=="a").State==EngineTaskState.Queued);Check(manager.Snapshots.Length==3,"retry replaces the terminal row without duplicating tasks");release.Set();Until(()=>!manager.HasActive);Check(manager.Snapshots.All(s=>s.State==EngineTaskState.Completed),"remaining resources and retried task can complete independently");Check(counts["a"]==2&&counts["b"]==1,"retry only reruns the selected resource");
            }
            using(var manager=new EngineDownloadManager(store,2,(p,t,r)=>{t.WaitHandle.WaitOne();t.ThrowIfCancellationRequested();}))
            {manager.Enqueue(new[]{Package("close1"),Package("close2"),Package("close3")});Wait(manager.StopAsync());Check(!manager.HasActive&&manager.Snapshots.All(s=>s.State==EngineTaskState.Cancelled),"shutdown awaits and cancels both active and queued resources");}
            using(var manager=new EngineDownloadManager(store,2,(p,t,r)=>{throw new IOException("connection failed");}))
            {manager.Enqueue(new[]{Package("failed")});Until(()=>!manager.HasActive);Check(manager.Snapshots.Single().State==EngineTaskState.Failed&&manager.Snapshots.Single().Message.Contains("connection failed"),"background errors stay in the task instead of blocking a different page");}
            using(var gate=new ManualResetEventSlim(false))using(var f=new MainForm(new string[0]))
            {
                f.Show();Application.DoEvents();Call(f,"ChangeWorkspace",1);var audio=Field<AudioWorkspace>(f,"audioWorkspace");Wait(Field<Task>(audio,"operation"));var speech=Field<SpeechWorkspace>(audio,"speech");speech.Downloads.Dispose();
                var background=new EngineDownloadManager(store,2,(p,t,r)=>{r(new EngineProgress{Written=p.Bytes/3,Total=p.Bytes,Message="下载中，页面可切换"});while(!gate.Wait(10))t.ThrowIfCancellationRequested();});typeof(SpeechWorkspace).GetField("store",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(speech,store);typeof(SpeechWorkspace).GetField("downloads",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(speech,background);
                Call(audio,"SetMode","语音转文字");Call(speech,"InstallSelected");Until(()=>background.Snapshots.Length==2);Check(!audio.Busy&&!speech.Busy,"engine downloading is not a blocking audio operation");Check(Field<ComboBox>(speech,"resource").Enabled,"resource selection remains usable during downloads");Call(audio,"SetMode","语音生成");Call(speech,"InstallSelected");Field<ComboBox>(speech,"resource").SelectedIndex=1;Call(speech,"InstallSelected");
                Check(background.Snapshots.Length==5,"both speech pages add multiple resource bundles to one queue");Check(background.Snapshots.Count(s=>s.Package.Id==SpeechCatalog.Sherpa.Id)==1,"two voice packs deduplicate their common synthesis engine");Check(Field<ModernButton>(speech,"start").Enabled==false,"uninstalled resources still cannot generate audio while downloading");
                Call(f,"ChangeWorkspace",0);Check(Field<Control>(f,"pdfWorkspace").Visible,"PDF remains navigable while resources download");Call(f,"ChangeWorkspace",2);Check(Field<ImageWorkspace>(f,"imageWorkspace").Visible,"image tools remain navigable while resources download");Until(()=>Field<LinkLabel>(f,"engineDownloadsLink").Visible);Check(Field<LinkLabel>(f,"engineDownloadsLink").Text.Contains("5"),"sidebar exposes global background download status");
                audio.ShowDownloads();var window=Field<EngineDownloadsForm>(speech,"downloadsForm");Check(!window.Modal&&f.Enabled,"download task window is modeless and does not lock the main window");Shot(window,Path.Combine(root,"background-download-tasks.png"));window.Close();Check(background.HasActive,"closing the task viewer keeps background downloads running");
                f.Size=f.MinimumSize;Application.DoEvents();Call(f,"ChangeWorkspace",1);Call(audio,"SetMode","语音生成");var tasks=Field<ModernButton>(speech,"downloadTasks");Check(tasks.Right<=tasks.Parent.ClientSize.Width&&tasks.Bottom<=tasks.Parent.ClientSize.Height,"task controls fit the minimum audio layout");Shot(f,Path.Combine(root,"background-audio-minimum.png"));Call(audio,"SetMode","格式转换");Check(Field<Control>(audio,"normalBody").Enabled,"original conversion controls remain available during downloads");Wait(audio.StopAsync());Check(!background.HasActive,"audio shutdown also awaits the background queue");f.Close();Until(()=>f.IsDisposed);
            }
            Console.WriteLine("ALL "+checks+" ENGINE BACKGROUND CHECKS PASSED");return 0;
        }catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
