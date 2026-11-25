using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using LocalConverter;

static class PreviewChecks
{
    [DllImport("user32.dll")]static extern bool SetProcessDPIAware();
    static int checks;static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
    static T Field<T>(MainForm f,string name){return (T)typeof(MainForm).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(f);}
    static object Call(MainForm f,string name,params object[] args){SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());var value=typeof(MainForm).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(f,args);if(name=="AddFiles"){var task=Field<Task>(f,"admissionTask");while(!task.IsCompleted){Application.DoEvents();Thread.Sleep(5);}task.GetAwaiter().GetResult();}return value;}
    static void WaitPreview(MainForm f){var clock=Stopwatch.StartNew();while(!Field<Task>(f,"previewTask").IsCompleted){Application.DoEvents();Thread.Sleep(5);if(clock.Elapsed.TotalSeconds>15)throw new Exception("UI preview timeout");}Application.DoEvents();}
    static void Snapshot(MainForm f,string path){Application.DoEvents();using(var bitmap=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(bitmap,new Rectangle(Point.Empty,f.Size));bitmap.Save(path);}}
    static double Median(List<double> values){var sorted=values.OrderBy(v=>v).ToArray();return sorted[sorted.Length/2];}
    [STAThread]static int Main(string[] args)
    {
        try
        {
            SetProcessDPIAware();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            string folder=Path.GetFullPath(args[0]),source=Path.Combine(folder,"a.pdf");var oldTimes=new List<double>();var times=new List<double>();int[] sequence={0,1,2,1,0,2};byte[] before=File.ReadAllBytes(source);
            using(var old=new PdfEngine())foreach(int index in sequence)
            {
                var clock=Stopwatch.StartNew();old.Run("inspect",new Dictionary<string,object>{{"files",new[]{source}}},CancellationToken.None);old.Run("render",new Dictionary<string,object>{{"files",new[]{source}},{"folder",Path.Combine(folder,"old-previews")},{"indices",new[]{index}},{"edge",1300}},CancellationToken.None);oldTimes.Add(clock.Elapsed.TotalMilliseconds);
            }
            int pid;string privateFolder;
            using(var renderer=new PreviewRenderer())
            {
                var clock=Stopwatch.StartNew();var first=renderer.Render(source,0,0,1300,"",CancellationToken.None);Console.WriteLine("FIRST_PREVIEW_MS="+clock.Elapsed.TotalMilliseconds.ToString("F1"));pid=renderer.WorkerId;privateFolder=Path.GetDirectoryName(first.Path);
                Check(first.Count==3&&File.Exists(first.Path),"preview gets page image and count in one request");
                foreach(int index in sequence){clock.Restart();var page=renderer.Render(source,index,0,1300,"",CancellationToken.None);using(var image=Image.FromFile(page.Path))Check(Math.Max(image.Width,image.Height)<=1301,"preview image is bounded");times.Add(clock.Elapsed.TotalMilliseconds);Check(renderer.WorkerId==pid,"flipping pages reuses the same render process");}
                var cached=renderer.Render(source,1,0,1300,"",CancellationToken.None);Check(cached.Cached,"previously visited page comes from worker cache");
                Console.WriteLine("OLD_FLIP_MEDIAN_MS="+Median(oldTimes).ToString("F1"));Console.WriteLine("NEW_FLIP_MEDIAN_MS="+Median(times).ToString("F1"));using(var process=Process.GetProcessById(pid))Console.WriteLine("PREVIEW_WORKING_MIB="+(process.WorkingSet64/1048576.0).ToString("F1"));
                string copy=Path.Combine(folder,"preview-overwrite.pdf");File.Copy(source,copy,true);renderer.Render(copy,0,0,600,"",CancellationToken.None);var info=new FileInfo(copy);PageDocument.Overwrite(Path.Combine(folder,"b.pdf"),copy,info.Length,info.LastWriteTimeUtc);Check(renderer.Render(copy,0,0,600,"",CancellationToken.None).Count==2,"open preview does not lock source and detects replacement");
                var other=renderer.Render(Path.Combine(folder,"b.pdf"),0,0,600,"",CancellationToken.None);Check(other.Count==2&&other.Path!=cached.Path,"switching source clears document-specific cached pages");
                var rotated=renderer.Render(source,0,90,600,"",CancellationToken.None);using(var image=Image.FromFile(rotated.Path))Check(image.Width>image.Height,"preview respects edited page rotation");
                foreach(int edge in new[]{400,500})foreach(int angle in new[]{0,90,180,270})foreach(int index in new[]{0,1,2})renderer.Render(source,index,angle,edge,"",CancellationToken.None);
                Check(Directory.GetFiles(privateFolder,"*.png").Length<=20,"preview disk cache is bounded to twenty pages");
                var cancelled=new CancellationTokenSource();cancelled.Cancel();bool rejected=false;try{renderer.Render(source,0,0,600,"",cancelled.Token);}catch(OperationCanceledException){rejected=true;}Check(rejected,"cancelled preview is not sent to worker");
                var encrypted=Directory.GetDirectories(folder,"job-*").SelectMany(dir=>Directory.GetFiles(dir,"*_已加密.pdf",SearchOption.AllDirectories)).First();bool wrong=false;try{renderer.Render(encrypted,0,0,600,"wrong",CancellationToken.None);}catch(IOException){wrong=true;}Check(wrong,"incorrect preview password does not show a previous document");Check(renderer.Render(encrypted,0,0,600,"本地测试-123",CancellationToken.None).Count==3,"correct password works after failed attempt");
            }
            Check(!Directory.Exists(privateFolder),"preview renderer removes its private snapshots and images");bool exited;try{using(var process=Process.GetProcessById(pid))exited=process.WaitForExit(2000);}catch(ArgumentException){exited=true;}Check(exited,"preview process exits on renderer disposal");Check(before.SequenceEqual(File.ReadAllBytes(source)),"preview and prefetch leave source bytes unchanged");
            using(var form=new MainForm(new string[0]))
            {
                form.Show();Call(form,"SetMode","拆分");Call(form,"AddFiles",(object)new[]{source});WaitPreview(form);var settings=Field<SplitSettings>(form,"splitSettings");var image=Field<PdfPagePreview>(form,"pagePicture");var parts=Field<ComboBox>(form,"previewPart");
                settings.Method.SelectedIndex=2;settings.RatioPicker.Value=30;Application.DoEvents();Check(image.CropEnabled&&parts.Visible,"ratio mode exposes shaded preview and part selector");
                var bounds=PdfPagePreview.ImageBounds(image.ClientSize,image.Image.Size);Check(Math.Abs(image.ShadedBounds.Height-bounds.Height*0.7)<1,"top thirty percent preview shades the lower seventy percent");
                int uiPid=Field<PreviewRenderer>(form,"previewRenderer").WorkerId;Image original=image.Image;string context=settings.Context;parts.SelectedIndex=1;Check(Math.Abs(image.ShadedBounds.Height-bounds.Height*0.7)<1,"keeping bottom thirty percent shades upper seventy percent");Check(settings.Context!=context&&(string)settings.JobValues()["keep"]=="second","keep selector changes actual output parameters and result context");
                settings.LeftRight.Checked=true;Check(Math.Abs(image.ShadedBounds.Width-bounds.Width*0.7)<1,"keeping right thirty percent shades left seventy percent");
                parts.SelectedIndex=0;Check(Math.Abs(image.ShadedBounds.Width-bounds.Width*0.7)<1,"left part preview shades the right remainder");
                Check(Object.ReferenceEquals(image.Image,original)&&Field<PreviewRenderer>(form,"previewRenderer").WorkerId==uiPid,"changing split ratio and part only repaints overlay");
                Snapshot(form,Path.Combine(folder,"preview-left30.png"));settings.TopBottom.Checked=true;settings.RatioPicker.Value=50;Snapshot(form,Path.Combine(folder,"preview-top50.png"));parts.SelectedIndex=1;Snapshot(form,Path.Combine(folder,"preview-bottom50.png"));
                form.Size=form.MinimumSize;Application.DoEvents();Check(parts.Right<=parts.Parent.ClientSize.Width,"part selector fits the minimum window");Check(Field<FileCards>(form,"cards").ClientSize.Height>=image.DeviceDpi*88/96,"minimum window keeps a complete file card visible");Snapshot(form,Path.Combine(folder,"preview-minimum.png"));
                Call(form,"TurnPreview",1);Call(form,"TurnPreview",1);WaitPreview(form);Check(Field<int>(form,"currentPage")==2&&Field<Label>(form,"pageNumber").Text.StartsWith("3 /"),"rapid navigation displays the latest selected page");
                var cropTask=(Task)Call(form,"ProcessFiles");while(!cropTask.IsCompleted){Application.DoEvents();Thread.Sleep(5);}cropTask.GetAwaiter().GetResult();var result=(ResultSession)typeof(MainForm).GetProperty("results",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form,null);
                Check(result.Files.Length==1&&Path.GetExtension(result.Files[0])==".pdf","ratio mode in UI outputs a PDF rather than ZIP");using(var engine=new PdfEngine()){var data=engine.Run("inspect",new Dictionary<string,object>{{"files",result.Files}},CancellationToken.None);Check(System.Convert.ToInt32(data["count"])==3,"UI crop preserves all pages in the same PDF");}result.Downloaded=true;
                Call(form,"SetMode","转换");Check(!image.CropEnabled&&!parts.Visible,"leaving split mode removes overlay and part selector");WaitPreview(form);form.Close();while(!form.IsDisposed){Application.DoEvents();Thread.Sleep(5);}
                bool gone;try{using(var process=Process.GetProcessById(uiPid))gone=process.WaitForExit(2000);}catch(ArgumentException){gone=true;}Check(gone,"closing window stops its persistent preview process");
            }
            Console.WriteLine("ALL "+checks+" PREVIEW CHECKS PASSED");return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
