using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace LocalConverter
{
    public static class OfficeConverter
    {
        public static bool Supports(string path){return new[]{".xls",".xlsx",".ppt",".pptx"}.Contains(Path.GetExtension(path).ToLowerInvariant());}
        static string Quote(string value){return "\""+value+"\"";}
        public static void ExportFile(string input,string output,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            string kind=Path.GetExtension(input).StartsWith(".xls",StringComparison.OrdinalIgnoreCase)?"excel":"powerpoint";
            if(Type.GetTypeFromProgID(kind=="excel"?"Excel.Application":"PowerPoint.Application")==null)throw new IOException("此文件转 PDF 需要已安装并激活的 Microsoft "+(kind=="excel"?"Excel":"PowerPoint")+" 桌面版。");
            string job=Path.Combine(Path.GetTempPath(),"LocalConverterOffice-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(job);
            try
            {
                using(var worker=Process.Start(new ProcessStartInfo(Application.ExecutablePath,"--office-worker "+kind+" "+Quote(input)+" "+Quote(output)+" "+Quote(job)){UseShellExecute=false,CreateNoWindow=true}))
                {
                    try
                    {
                        var watch=Stopwatch.StartNew();while(!worker.WaitForExit(100)){token.ThrowIfCancellationRequested();if(watch.Elapsed.TotalSeconds>150)throw new IOException("Office 转换超时，请确认文件可打开且 Office 已激活。");}
                        token.ThrowIfCancellationRequested();if(worker.ExitCode!=0||!File.Exists(output))throw new IOException(File.Exists(Path.Combine(job,"error.txt"))?File.ReadAllText(Path.Combine(job,"error.txt")):"Office 转换失败。");
                    }
                    finally{if(!worker.HasExited){worker.Kill();worker.WaitForExit(5000);}StopOwned(job);}
                }
            }
            finally{try{Directory.Delete(job,true);}catch(IOException){}catch(UnauthorizedAccessException){}}
        }
        static void StopOwned(string job)
        {
            try
            {
                var identity=File.ReadAllLines(Path.Combine(job,"office.pid"));using(var process=Process.GetProcessById(Int32.Parse(identity[0])))
                if(process.ProcessName==identity[2]&&process.StartTime.ToUniversalTime().Ticks==Int64.Parse(identity[1])){if(!process.WaitForExit(1500)){process.Kill();process.WaitForExit(5000);}}
            }
            catch(ArgumentException){}catch(IOException){}catch(InvalidOperationException){}
        }
        static HashSet<int> ProcessIds(string name){var processes=Process.GetProcessesByName(name);try{return new HashSet<int>(processes.Select(p=>p.Id));}finally{foreach(var p in processes)p.Dispose();}}
        static void Release(object value){try{if(value!=null&&Marshal.IsComObject(value))Marshal.FinalReleaseComObject(value);}catch(InvalidComObjectException){}}
        public static int RunWorker(string kind,string input,string output,string job)
        {
            dynamic app=null,books=null,book=null;bool owned=false;bool excel=kind=="excel";string name=excel?"EXCEL":"POWERPNT";
            try
            {
                var before=ProcessIds(name);DateTime began=DateTime.UtcNow;app=Activator.CreateInstance(Type.GetTypeFromProgID(excel?"Excel.Application":"PowerPoint.Application",true));
                var processes=Process.GetProcessesByName(name);
                try
                {
                    var created=processes.Where(p=>!before.Contains(p.Id)&&p.StartTime.ToUniversalTime()>=began.AddSeconds(-1)).ToArray();
                    if(created.Length!=1)throw new IOException("未能确认独立的 "+(excel?"Excel":"PowerPoint")+" 进程。请保存该程序中的工作并关闭它后重试。");
                    File.WriteAllLines(Path.Combine(job,"office.pid"),new[]{created[0].Id.ToString(),created[0].StartTime.ToUniversalTime().Ticks.ToString(),name});owned=true;
                }
                finally{foreach(var process in processes)process.Dispose();}
                app.AutomationSecurity=3;
                if(excel)
                {
                    app.Visible=false;app.DisplayAlerts=false;app.AskToUpdateLinks=false;books=app.Workbooks;
                    string unavailablePassword=Guid.NewGuid().ToString("N").Substring(0,12);
                    book=books.Open(Filename:input,UpdateLinks:0,ReadOnly:true,Password:unavailablePassword,WriteResPassword:unavailablePassword,IgnoreReadOnlyRecommended:true,Notify:false,AddToMru:false);
                    book.ExportAsFixedFormat(Type:0,Filename:output,Quality:0,IncludeDocProperties:true,IgnorePrintAreas:false,OpenAfterPublish:false);
                }
                else
                {
                    app.DisplayAlerts=1;books=app.Presentations;book=books.Open(FileName:input,ReadOnly:-1,Untitled:0,WithWindow:0);
                    book.SaveAs(output,32,0);
                }
                return 0;
            }
            catch(Exception error){File.WriteAllText(Path.Combine(job,"error.txt"),(excel?"Excel":"PowerPoint")+" 无法转换："+error.Message);return 1;}
            finally
            {
                if((object)book!=null){try{if(excel)book.Close(false);else book.Close();}catch{}Release((object)book);}
                Release((object)books);if((object)app!=null){if(owned){try{app.Quit();}catch{}}Release((object)app);}
            }
        }
    }
}
