using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Diagnostics;

namespace LocalConverter
{
    public enum SavePhase { Copying, Flushing, Saved }
    public sealed class SaveProgress
    {
        public readonly long Written,Total;
        public readonly SavePhase Phase;
        public SaveProgress(long written,long total,SavePhase phase){Written=written;Total=total;Phase=phase;}
    }
    public sealed class ResultSession : IDisposable
    {
        public string Folder { get; private set; }
        public bool Downloaded { get; set; }
        public ResultSession() { Folder = Path.Combine(Path.GetTempPath(), "LocalConverterResults-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(Folder); }
        public string[] Files { get { return Directory.GetFiles(Folder).Where(p=> !p.EndsWith(".tmp",StringComparison.OrdinalIgnoreCase)).OrderBy(p => p).ToArray(); } }
        public static string SaveCopy(string source, string folder, string name, CancellationToken token,Action<SaveProgress> report=null)
        {
            token.ThrowIfCancellationRequested();long written=0,total=0;
            Directory.CreateDirectory(folder);
            string temp = OutputFiles.Temporary(folder);
            try
            {
                using (var input = File.OpenRead(source))
                using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write))
                {
                    total=input.Length;if(report!=null)report(new SaveProgress(0,total,SavePhase.Copying));var clock=Stopwatch.StartNew();
                    byte[] buffer = new byte[1024 * 128]; int n;
                    while ((n = input.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        token.ThrowIfCancellationRequested();output.Write(buffer,0,n);written+=n;
                        if(report!=null&&(written==n||clock.ElapsedMilliseconds>=100)){report(new SaveProgress(written,total,SavePhase.Copying));clock.Restart();}
                    }
                    token.ThrowIfCancellationRequested();if(report!=null)report(new SaveProgress(written,total,SavePhase.Flushing));
                    output.Flush(true);
                }
                token.ThrowIfCancellationRequested();
                string saved=OutputFiles.Commit(temp, folder, name, Path.GetExtension(source));if(report!=null)report(new SaveProgress(written,total,SavePhase.Saved));return saved;
            }
            finally { OutputFiles.DeleteTemporary(temp); }
        }
        public void Dispose()
        {
            // 仅删除本实例创建的随机目录，不扫描其他会话，也不接收用户目录。
            try { if (Directory.Exists(Folder)) Directory.Delete(Folder, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
