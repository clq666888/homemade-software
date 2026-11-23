using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Collections.Generic;

namespace LocalConverter
{
    public static class HtmlConverter
    {
        public static bool Supports(string path){return new[]{".html",".htm"}.Contains(Path.GetExtension(path).ToLowerInvariant());}
        static string Quote(string value){return "\""+value+"\"";}
        public static void ExportFile(string input,string output,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            string browser=new[]{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Microsoft","Edge","Application","msedge.exe"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Microsoft","Edge","Application","msedge.exe"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Google","Chrome","Application","chrome.exe"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Google","Chrome","Application","chrome.exe")}.FirstOrDefault(File.Exists);
            if(browser==null)throw new IOException("本地 HTML 转 PDF 需要 Chrome 或 Edge。请保留 HTML 同目录的图片、样式和字体资源。");
            string profile=Path.Combine(Path.GetTempPath(),"LocalConverterHtml-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(profile);
            try
            {
            using(var engine=new PdfEngine()){var safe=engine.Run("prepare_html",new Dictionary<string,object>{{"files",new[]{input}},{"folder",profile}},token);input=PdfEngine.Outputs(safe)[0];}
            // An isolated profile never touches the user's browser. Block remote content through
            // a non-listening proxy, including loopback; scripts and extensions are disabled.
            string args="--headless=new --disable-gpu --no-first-run --disable-extensions --disable-background-networking --disable-component-update --disable-sync --proxy-server=http://127.0.0.1:9 --proxy-bypass-list=\"<-loopback>\" --host-resolver-rules=\"MAP * ~NOTFOUND\" --no-pdf-header-footer --timeout=12000 --user-data-dir="+Quote(Path.Combine(profile,"browser"))+" --print-to-pdf="+Quote(output)+" "+Quote(new Uri(Path.GetFullPath(input)).AbsoluteUri);
                using(var group=new ProcessJob())
                using(var child=Process.Start(new ProcessStartInfo(browser,args){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true}))
                {
                    group.Add(child);
                    var errors=child.StandardError.ReadToEndAsync();
                    // Edge may finish its launcher before its print process. The inherited stderr
                    // pipe closes only after those descendants finish; do not delete their profile early.
                    try{var clock=Stopwatch.StartNew();while(!child.HasExited||!errors.IsCompleted){token.ThrowIfCancellationRequested();if(clock.Elapsed.TotalSeconds>60)throw new IOException("HTML 打印超时，请检查本地文件及资源。");Thread.Sleep(100);}token.ThrowIfCancellationRequested();if(!File.Exists(output)||new FileInfo(output).Length<100)throw new IOException("HTML 打印未生成 PDF（退出码 "+child.ExitCode+"）。"+errors.Result);}
                    finally{if(!child.HasExited){child.Kill();child.WaitForExit(5000);}}
                }
            }
            finally{try{Directory.Delete(profile,true);}catch(IOException){}catch(UnauthorizedAccessException){}}
        }
    }
}
