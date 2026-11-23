using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace LocalConverter
{
    public sealed class PdfEngine : IDisposable
    {
        public readonly string Workspace = Path.Combine(Path.GetTempPath(), "LocalConverterV3-" + Guid.NewGuid().ToString("N"));
        public PdfEngine() { Directory.CreateDirectory(Workspace); }
        internal static string Executable
        {
            get
            {
                string direct = Path.Combine(Application.StartupPath, "engine", "PdfEngine.exe");
                if (File.Exists(direct)) return direct;
                string nested = Path.Combine(Application.StartupPath, "dist", "文件万能处理器", "engine", "PdfEngine.exe");
                if (File.Exists(nested)) return nested;
                throw new IOException("找不到 PDF 处理引擎，请保留项目的 dist 文件夹和根目录 EXE。");
            }
        }
        public Dictionary<string, object> Run(string action, IDictionary<string, object> values, CancellationToken token, Action<int,string> report = null)
        {
            string folder = Path.Combine(Workspace, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
            string jobfile = Path.Combine(folder,"job.json"), reply = Path.Combine(folder,"reply.json");
            var json = new JavaScriptSerializer { MaxJsonLength = 20000000 };
            var job = new Dictionary<string, object>(values); job["action"] = action; job["reply"] = reply;
            File.WriteAllText(jobfile,json.Serialize(job));
            try
            {
                token.ThrowIfCancellationRequested();
                using (var process = Process.Start(new ProcessStartInfo(Executable, "\"" + jobfile + "\"") { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = folder }))
                {
                    try
                    {
                        var watch = Stopwatch.StartNew(); int last = -1;
                        while (!process.WaitForExit(100))
                        {
                            token.ThrowIfCancellationRequested();
                            if (watch.Elapsed.TotalMinutes > 20) throw new IOException("处理超过 20 分钟，已停止；请拆分文件后重试。");
                            if (report != null)
                            {
                                try { string p = Path.ChangeExtension(reply,".progress"); if (File.Exists(p)) { var data=json.Deserialize<Dictionary<string,object>>(File.ReadAllText(p)); int value=System.Convert.ToInt32(data["percent"]); if(value!=last){last=value;report(value,(string)data["message"]);} } }
                                catch(IOException) { } catch(ArgumentException) { }
                            }
                        }
                        token.ThrowIfCancellationRequested();
                        if (!File.Exists(reply)) throw new IOException("PDF 引擎未能完成，请检查文件或重启软件。");
                        var result = json.Deserialize<Dictionary<string,object>>(File.ReadAllText(reply));
                        if (!(bool)result["ok"]) throw new IOException((string)result["error"]);
                        return result;
                    }
                    finally { if(!process.HasExited) { process.Kill(); process.WaitForExit(5000); } }
                }
            }
            finally { try { Directory.Delete(folder,true); } catch(IOException) { } catch(UnauthorizedAccessException) { } }
        }
        public static string[] Outputs(Dictionary<string,object> result)
        { return ((System.Collections.IEnumerable)result["outputs"]).Cast<object>().Select(o=>o.ToString()).ToArray(); }
        public void Dispose() { try { Directory.Delete(Workspace,true); } catch(IOException) { } catch(UnauthorizedAccessException) { } }
    }
}
