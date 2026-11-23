using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using LocalConverter;

static class DownloadChecks
{
    static int checks;
    static void Check(bool condition,string name){if(!condition)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
    static T Field<T>(MainForm form,string name){return (T)typeof(MainForm).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);}
    static object Call(MainForm form,string name,params object[] args){return typeof(MainForm).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(form,args);}
    static string Hash(string path){using(var sha=SHA256.Create())using(var input=File.OpenRead(path))return Convert.ToBase64String(sha.ComputeHash(input));}
    static void Pump(Task task){var clock=System.Diagnostics.Stopwatch.StartNew();while(!task.IsCompleted){Application.DoEvents();Thread.Sleep(2);if(clock.Elapsed.TotalSeconds>30)throw new Exception("UI save timed out");}task.GetAwaiter().GetResult();Application.DoEvents();}
    static void Snapshot(MainForm form,string path){form.PerformLayout();Application.DoEvents();using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(path);}}
    [STAThread]static int Main(string[] args)
    {
        try
        {
            string root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);
            string source=Path.Combine(root,"大文件样本.bin");byte[] block=new byte[1024*1024];new Random(42).NextBytes(block);
            using(var output=File.Create(source))for(int i=0;i<64;i++)output.Write(block,0,block.Length);
            string empty=Path.Combine(root,"空文件.bin");File.WriteAllBytes(empty,new byte[0]);string original=Hash(source);
            var updates=new List<DownloadUpdate>();string destination=Path.Combine(root,"批量保存");Directory.CreateDirectory(destination);
            File.WriteAllText(Path.Combine(destination,Path.GetFileName(source)),"existing");
            string[] saved=DownloadJob.Save(new[]{source,empty},destination,null,CancellationToken.None,u=>updates.Add(u));
            Check(saved.Length==2&&Hash(saved[0])==original&&new FileInfo(saved[1]).Length==0,"batch saves large and empty files without corruption");
            Check(File.ReadAllText(Path.Combine(destination,Path.GetFileName(source)))=="existing"&&saved[0]!=Path.Combine(destination,Path.GetFileName(source)),"same-name file preserved and new result numbered");
            Check(updates.First().Written==0&&updates.First().Percent==0,"initial byte progress starts at zero");
            Check(updates.Any(u=>u.Phase==SavePhase.Copying&&u.Written>0&&u.Written<u.Total),"intermediate byte progress emitted before completion");
            Check(updates.Zip(updates.Skip(1),(a,b)=>a.Written<=b.Written&&a.Percent<=b.Percent).All(v=>v),"aggregate progress remains monotonic across files");
            Check(updates.All(u=>u.Total==new FileInfo(source).Length)&&updates.Where(u=>!u.AllSaved).All(u=>u.Percent<100),"byte-weighted total and no premature 100 percent");
            Check(updates.Count(u=>u.Phase==SavePhase.Flushing)==2&&updates.Last().AllSaved&&updates.Last().Percent==100,"flush phase reported and 100 percent only after both commits");
            Check(!Directory.GetFiles(destination,"*.tmp").Any(),"successful save leaves no temporary file");
            foreach(bool afterFlush in new[]{false,true})
            {
                string folder=Path.Combine(root,afterFlush?"取消完成写入":"取消复制");Directory.CreateDirectory(folder);bool cancelled=false;
                using(var cancel=new CancellationTokenSource())
                {
                    try{ResultSession.SaveCopy(source,folder,"cancelled",cancel.Token,p=>{if(afterFlush?p.Phase==SavePhase.Flushing:p.Phase==SavePhase.Copying&&p.Written>0)cancel.Cancel();});}
                    catch(OperationCanceledException){cancelled=true;}
                }
                Check(cancelled&&!Directory.GetFiles(folder).Any(),"cancel cleans unfinished copy before commit: "+afterFlush);
            }
            Check(Hash(source)==original,"cancellation preserves source bytes");
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            using(var form=new MainForm(new string[0]))using(var session=new ResultSession())
            {
                form.Show();Application.DoEvents();SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());var settings=Field<Settings>(form,"settings");string previousFolder=settings.OutputFolder;
                try
                {
                    typeof(MainForm).GetProperty("results",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(form,session,null);
                    var download=Field<ModernButton>(form,"download");download.Text="下载处理结果";
                    var start=Field<ModernButton>(form,"start");var progress=Field<SmoothProgress>(form,"progress");
                    var task=(Task)Call(form,"SaveResults",new[]{source},Path.Combine(root,"界面保存"),null);
                    Check(download.Visible&&!download.Enabled&&download.Text.Contains("正在准备")&&start.Enabled&&start.Text=="取消下载"&&progress.Indeterminate,"save immediately shows preparation and usable cancel button");
                    Pump(task);string finished=Field<Label>(form,"status").Text;
                    Check(session.Downloaded&&progress.Value==100&&!progress.Indeterminate&&download.Enabled&&finished.StartsWith("已下载 1"),"UI completes save and restores download controls");
                    Application.DoEvents();Check(Field<Label>(form,"status").Text==finished,"late progress cannot replace final completion status");
                    // Exercise the visible presentation deterministically; disk timing is not a reliable screenshot trigger.
                    Call(form,"ShowDownloadProgress",new DownloadUpdate{Written=32L*1024*1024,Total=64L*1024*1024,FileIndex=1,FileCount=1,FileName=Path.GetFileName(source),Phase=SavePhase.Copying,BytesPerSecond=24L*1024*1024});
                    Check(download.Text.Contains("50%")&&download.Text.Contains("32.0 MB")&&Field<Label>(form,"status").Text.Contains("24.0 MB/s"),"visible feedback includes percentage sizes file and speed");
                    Snapshot(form,Path.Combine(root,"download-progress.png"));
                    Call(form,"ShowDownloadProgress",new DownloadUpdate{Written=64L*1024*1024,Total=64L*1024*1024,FileIndex=1,FileCount=1,FileName=Path.GetFileName(source),Phase=SavePhase.Flushing});
                    Check(progress.Indeterminate&&download.Text.Contains("正在完成保存"),"disk flush stays animated and explicitly labelled");
                    Snapshot(form,Path.Combine(root,"download-flush.png"));
                    var cancelTask=(Task)Call(form,"SaveResults",new[]{source},Path.Combine(root,"界面取消"),null);
                    Field<CancellationTokenSource>(form,"running").Cancel();Pump(cancelTask);
                    Check(!progress.Indeterminate&&download.Enabled&&Field<Label>(form,"status").Text.StartsWith("已取消下载"),"UI cancellation stops animation and permits retry");
                }
                finally{settings.OutputFolder=previousFolder;settings.Save();session.Downloaded=true;form.Close();while(!form.IsDisposed){Application.DoEvents();Thread.Sleep(2);}}
            }
            Console.WriteLine("ALL "+checks+" DOWNLOAD CHECKS PASSED");return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
