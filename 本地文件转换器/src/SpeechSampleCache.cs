using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
namespace LocalConverter
{
    public sealed class SpeechSampleCache : IDisposable
    {
        sealed class Entry{public string Key,Folder,File;public long Bytes;}
        readonly List<Entry> entries=new List<Entry>();readonly string root;readonly int capacity;readonly long budget;
        public bool Reused{get;private set;}
        public SpeechSampleCache(string root=null,int capacity=12,long budget=16*1024*1024){if(capacity<1||budget<45)throw new ArgumentOutOfRangeException("capacity");this.root=root??Path.Combine(Path.GetTempPath(),"FileToolsSpeechSamples-"+Guid.NewGuid().ToString("N"));this.capacity=capacity;this.budget=budget;}
        public async Task<string> GetAsync(string key,Func<string,Task<string>> generate,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();Reused=false;var cached=entries.FirstOrDefault(e=>e.Key==key);if(cached!=null){var info=new FileInfo(cached.File);if(info.Exists&&info.Length==cached.Bytes){entries.Remove(cached);entries.Add(cached);Reused=true;return cached.File;}Remove(cached);}
            string folder=Path.Combine(root,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);bool committed=false;
            try{string file=await generate(folder);token.ThrowIfCancellationRequested();if(!Path.GetFullPath(file).StartsWith(Path.GetFullPath(folder)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new IOException("试听文件路径无效。");var info=new FileInfo(file);if(!info.Exists||info.Length<=44||info.Length>budget)throw new IOException("试听文件为空或超出缓存大小。");while(entries.Count>=capacity||entries.Sum(e=>e.Bytes)+info.Length>budget)Remove(entries[0]);entries.Add(new Entry{Key=key,Folder=folder,File=file,Bytes=info.Length});committed=true;return file;}finally{if(!committed)Delete(folder);}
        }
        void Remove(Entry e){entries.Remove(e);Delete(e.Folder);}
        static void Delete(string folder){try{if(Directory.Exists(folder))Directory.Delete(folder,true);}catch(IOException){}catch(UnauthorizedAccessException){}}
        public void Dispose(){foreach(var e in entries.ToArray())Remove(e);Delete(root);}
    }
}
