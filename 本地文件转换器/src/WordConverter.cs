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
    public sealed class WordConverter : IConverter
    {
        public string Id { get { return "word-pdf"; } }
        public string Title { get { return "Word 转 PDF"; } }
        public string Category { get { return "转换为 PDF"; } }
        public string Description { get { return "批量转换 DOC / DOCX，保留文档排版。需要本机安装并可使用 Microsoft Word。"; } }
        public string[] Extensions { get { return new[] { ".doc", ".docx" }; } }
        public static bool Available { get { return Type.GetTypeFromProgID("Word.Application") != null; } }
        static string Quote(string s) { return "\"" + s + "\""; }
        public void Convert(IList<InputFile> files, ConversionOptions o, CancellationToken token, Action<int, string> progress)
        {
            if (!Available) throw new IOException("未检测到 Microsoft Word。图片转换不受影响；Word 转换需要安装并激活桌面版 Microsoft Word。");
            if (files.Count == 0) throw new ArgumentException("请先添加 Word 文档。");
            Directory.CreateDirectory(o.OutputFolder);
            var errors = new List<string>();
            for (int i = 0; i < files.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                progress(i * 100 / files.Count, "正在转换 " + Path.GetFileName(files[i].Path));
                string temp = OutputFiles.Temporary(o.OutputFolder);
                string job = Path.Combine(Path.GetTempPath(), "LocalConverter-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(job);
                try
                {
                    using (var worker = Process.Start(new ProcessStartInfo(Application.ExecutablePath,
                        "--word-worker " + Quote(files[i].Path) + " " + Quote(temp) + " " + Quote(job)) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = job }))
                    {
                        try
                        {
                            var clock = Stopwatch.StartNew();
                            while (!worker.WaitForExit(100))
                            {
                                token.ThrowIfCancellationRequested();
                                if (clock.Elapsed.TotalSeconds > 120) throw new IOException("转换超过 120 秒，已终止。请检查文档大小、密码保护或 Word 授权状态。");
                            }
                            token.ThrowIfCancellationRequested();
                            if (worker.ExitCode != 0 || !File.Exists(temp))
                                throw new IOException(File.Exists(Path.Combine(job, "error.txt")) ? File.ReadAllText(Path.Combine(job, "error.txt")) : "Word 转换未完成（进程退出码 " + worker.ExitCode + "），请先确认 Word 能正常打开此文档。");
                        }
                        finally
                        {
                            if (!worker.HasExited) { try { worker.Kill(); worker.WaitForExit(5000); } catch (InvalidOperationException) { } }
                            StopOwnedWord(job);
                        }
                    }
                    string dest = OutputFiles.Commit(temp, o.OutputFolder, Path.GetFileNameWithoutExtension(files[i].Path));
                    progress((i + 1) * 100 / files.Count, "已生成 " + Path.GetFileName(dest));
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception e) { errors.Add(Path.GetFileName(files[i].Path) + "：" + e.Message); }
                finally
                {
                    OutputFiles.DeleteTemporary(temp);
                    try { Directory.Delete(job, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                }
            }
            if (errors.Count > 0) throw new IOException("部分文件未能转换，已完成的 PDF 已保留。\r\n" + String.Join("\r\n", errors));
        }
        static void StopOwnedWord(string job)
        {
            try
            {
                string[] identity = File.ReadAllLines(Path.Combine(job, "word.pid"));
                using (var p = Process.GetProcessById(Int32.Parse(identity[0])))
                {
                    // PID 可复用，同时核对启动时间；绝不按进程名批量终止用户 Word。
                    if (p.ProcessName.Equals("WINWORD", StringComparison.OrdinalIgnoreCase) && p.StartTime.ToUniversalTime().Ticks == Int64.Parse(identity[1]))
                    { if (!p.WaitForExit(1500)) { p.Kill(); p.WaitForExit(5000); } }
                }
            }
            catch (ArgumentException) { } catch (IOException) { } catch (InvalidOperationException) { }
        }
        public static string ReadPreview(string input, CancellationToken token)
        {
            string job = Path.Combine(Path.GetTempPath(), "LocalConverter-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(job);
            string output = Path.Combine(job, "preview.txt");
            try
            {
                using (var worker = Process.Start(new ProcessStartInfo(Application.ExecutablePath, "--word-worker " + Quote(input) + " " + Quote(output) + " " + Quote(job)) { UseShellExecute = false, CreateNoWindow = true }))
                {
                    try
                    {
                        var clock = Stopwatch.StartNew();
                        while (!worker.WaitForExit(100)) { token.ThrowIfCancellationRequested(); if (clock.Elapsed.TotalSeconds > 45) throw new IOException("Word 预览超时。"); }
                        token.ThrowIfCancellationRequested();
                        if (worker.ExitCode != 0 || !File.Exists(output)) throw new IOException("Word 无法读取正文，请确认文件可正常打开。");
                        using (var reader = new StreamReader(output)) { char[] chars = new char[16000]; int n = reader.ReadBlock(chars, 0, chars.Length); return new string(chars, 0, n); }
                    }
                    finally { if (!worker.HasExited) { worker.Kill(); worker.WaitForExit(5000); } StopOwnedWord(job); }
                }
            }
            finally { try { Directory.Delete(job, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
        }
        public static void ExportFile(string input, string output, CancellationToken token)
        {
            if (!Available) throw new IOException("需要已安装并可使用的 Microsoft Word 桌面版。");
            string job=Path.Combine(Path.GetTempPath(),"LocalConverter-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(job);
            try
            {
                using(var worker=Process.Start(new ProcessStartInfo(Application.ExecutablePath,"--word-worker "+Quote(input)+" "+Quote(output)+" "+Quote(job)){UseShellExecute=false,CreateNoWindow=true}))
                {
                    try
                    {
                        var clock=Stopwatch.StartNew();while(!worker.WaitForExit(100)){token.ThrowIfCancellationRequested();if(clock.Elapsed.TotalSeconds>150)throw new IOException("Word 转换超时。请确认文档可打开、Word 已激活。");}
                        token.ThrowIfCancellationRequested();if(worker.ExitCode!=0||!File.Exists(output))throw new IOException(File.Exists(Path.Combine(job,"error.txt"))?File.ReadAllText(Path.Combine(job,"error.txt")):"Word 转换失败。");
                    }
                    finally{if(!worker.HasExited){worker.Kill();worker.WaitForExit(5000);}StopOwnedWord(job);}
                }
            }
            finally{try{Directory.Delete(job,true);}catch(IOException){}catch(UnauthorizedAccessException){}}
        }
        public static int RunWorker(string input, string output, string job)
        {
            try { return RunWorkerCore(input, output, job); }
            catch (Exception e) { File.WriteAllText(Path.Combine(job, "error.txt"), "Word 进程收尾异常：" + e.ToString()); return 1; }
        }
        static int RunWorkerCore(string input, string output, string job)
        {
            dynamic app = null, documents = null, document = null;
            bool owned = false;
            string stage = "启动 Word";
            try
            {
                var existing = new HashSet<int>(Process.GetProcessesByName("WINWORD").Select(p => { int id = p.Id; p.Dispose(); return id; }));
                DateTime activationStart = DateTime.UtcNow;
                app = Activator.CreateInstance(Type.GetTypeFromProgID("Word.Application", true));
                // 隐藏文档没有 Window/HWND。只认 COM 激活前不存在且刚创建的唯一进程。
                var created = Process.GetProcessesByName("WINWORD");
                try
                {
                    var candidates = created.Where(p => !existing.Contains(p.Id) && p.StartTime.ToUniversalTime() >= activationStart.AddSeconds(-1)).ToArray();
                    if (candidates.Length != 1) throw new IOException("未能确认独立 Word 转换进程，请保存工作后重试。");
                    var ownedProcess = candidates[0];
                    File.WriteAllLines(Path.Combine(job, "word.pid"), new[] { ownedProcess.Id.ToString(), ownedProcess.StartTime.ToUniversalTime().Ticks.ToString() });
                    owned = true;
                }
                finally { foreach (var p in created) p.Dispose(); }
                app.Visible = false;
                app.DisplayAlerts = 0;
                app.AutomationSecurity = 3; // 转换不执行文档宏。
                app.Options.UpdateLinksAtOpen = false;
                app.Options.SaveNormalPrompt = false;
                documents = app.Documents;
                stage = "只读打开文档";
                document = documents.Open(FileName: input, ConfirmConversions: false, ReadOnly: true,
                    AddToRecentFiles: false, PasswordDocument: Guid.NewGuid().ToString(), PasswordTemplate: Guid.NewGuid().ToString(),
                    Revert: false, WritePasswordDocument: Guid.NewGuid().ToString(), WritePasswordTemplate: Guid.NewGuid().ToString(),
                    Visible: false, OpenAndRepair: false, NoEncodingDialog: true);
                stage = "导出 PDF";
                if (output.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                {
                    dynamic range = document.Content;
                    try { File.WriteAllText(output, (string)range.Text); } finally { Release((object)range); }
                }
                else if(output.EndsWith(".docx",StringComparison.OrdinalIgnoreCase))document.SaveAs2(FileName:output,FileFormat:16,AddToRecentFiles:false);
                else if(output.EndsWith(".doc",StringComparison.OrdinalIgnoreCase))document.SaveAs2(FileName:output,FileFormat:0,AddToRecentFiles:false);
                else document.ExportAsFixedFormat(OutputFileName: output, ExportFormat: 17, OpenAfterExport: false, OptimizeFor: 0, KeepIRM: false);
                return 0;
            }
            catch (Exception e)
            {
                File.WriteAllText(Path.Combine(job, "error.txt"), "Word 无法转换此文件（" + stage + "）。请检查文件是否损坏、加密、受保护，以及 Word 是否已激活。\r\n" + e.Message);
                return 1;
            }
            finally
            {
                if ((object)document != null) { try { document.Close(0); } catch { } Release((object)document); }
                if ((object)documents != null) Release((object)documents);
                if ((object)app != null) { if (owned) { try { app.Quit(0); } catch { } } Release((object)app); }
            }
        }
        static void Release(object value)
        {
            // Word 退出时可能已断开 RCW。清理失败由父进程的 PID 收尾保证，不覆盖转换结果。
            try { if (Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value); }
            catch (InvalidComObjectException) { } catch (ArgumentException) { }
        }
    }
}
