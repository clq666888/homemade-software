using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace LocalConverter
{
    public sealed class PdfAccessStore : IDisposable
    {
        sealed class Entry{public string Password;public long Length,Ticks;}
        readonly Dictionary<string,Entry> entries=new Dictionary<string,Entry>(StringComparer.OrdinalIgnoreCase);
        public bool IsCurrent(string path){Entry value;if(!entries.TryGetValue(Path.GetFullPath(path),out value))return false;var info=new FileInfo(path);return info.Exists&&info.Length==value.Length&&info.LastWriteTimeUtc.Ticks==value.Ticks;}
        public string PasswordFor(string path){Entry value;if(!entries.TryGetValue(Path.GetFullPath(path),out value))return "";if(!IsCurrent(path))throw new IOException("文件已被修改，请重新添加并验证："+Path.GetFileName(path));return value.Password;}
        public void Forget(string path){entries.Remove(Path.GetFullPath(path));}
        public void Retain(IEnumerable<InputFile> files){var keep=new HashSet<string>(StringComparer.OrdinalIgnoreCase);foreach(var file in files)keep.Add(file.Path);foreach(var path in new List<string>(entries.Keys))if(!keep.Contains(path))entries.Remove(path);}
        public Dictionary<string,string> Snapshot(IEnumerable<InputFile> files){var result=new Dictionary<string,string>();foreach(var file in files)if(Path.GetExtension(file.Path).Equals(".pdf",StringComparison.OrdinalIgnoreCase))result[file.Path]=PasswordFor(file.Path);return result;}
        public async Task<bool> AdmitAsync(string path,PdfEngine engine,CancellationToken token,Func<string,Func<string,CancellationToken,Task<bool>>,Task<string>> ask)
        {
            path=Path.GetFullPath(path);if(IsCurrent(path))return true;Forget(path);
            var info=new FileInfo(path);long length=info.Length,ticks=info.LastWriteTimeUtc.Ticks;string password="";
            Func<string,CancellationToken,Task<bool>> verify=async(candidate,cancel)=>{var answer=await Task.Run(()=>engine.Run("probe",new Dictionary<string,object>{{"files",new[]{path}},{"password",candidate}},cancel));return !(bool)answer["needsPassword"];};
            if(!await verify("",token))
            {
                string verified=null;
                password=await ask(path,async(candidate,cancel)=>{bool accepted=await verify(candidate,cancel);if(accepted)verified=candidate;return accepted;});
                token.ThrowIfCancellationRequested();if(password==null)return false;
                if(verified==null||password!=verified)throw new IOException("密码尚未验证，文件未添加。");
            }
            token.ThrowIfCancellationRequested();info.Refresh();if(info.Length!=length||info.LastWriteTimeUtc.Ticks!=ticks)throw new IOException("验证期间文件发生变化，请重新添加。");
            entries[path]=new Entry{Password=password,Length=length,Ticks=ticks};return true;
        }
        public void Dispose(){entries.Clear();}
    }
}
