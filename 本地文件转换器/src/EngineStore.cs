using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using System.Web.Script.Serialization;

namespace LocalConverter
{
    public sealed class EngineProgress
    {public long Written,Total;public string Message;public int Percent{get{return Total<=0?0:(int)Math.Min(99,Written*100/Total);}}}
    public sealed class EngineFileReceipt{public string Path,Hash;public long Bytes,Ticks;}
    public sealed class EngineReceipt{public string Id,SourceHash;public EngineFileReceipt[] Files;}
    public sealed class LocalVoiceManifest
    {public int Schema=1;public string Name,Adapter,Model,VoicesFile,Tokens,Lexicons,License;public SpeechVoice[] Voices;}
    public sealed class EngineStore
    {
        public readonly string Root;readonly JavaScriptSerializer json=new JavaScriptSerializer{MaxJsonLength=4000000};readonly System.Collections.Generic.Dictionary<string,Tuple<long,long,EngineReceipt>> receipts=new System.Collections.Generic.Dictionary<string,Tuple<long,long,EngineReceipt>>();readonly object gate=new object();
        public EngineStore(string root=null){Root=Path.GetFullPath(root??SpeechCatalog.DefaultRoot);Directory.CreateDirectory(Root);RejectLinks(Root);}
        readonly Dictionary<string,object> packageGates=new Dictionary<string,object>();
        sealed class Lease : IDisposable{readonly object value;public Lease(object value){this.value=value;}public void Dispose(){Monitor.Exit(value);}}
        IDisposable EnterPackage(EnginePackage package,CancellationToken token)
        {
            object value;lock(gate){if(!packageGates.TryGetValue(package.Id,out value)){value=new object();packageGates.Add(package.Id,value);}}
            while(!Monitor.TryEnter(value,100))token.ThrowIfCancellationRequested();try{token.ThrowIfCancellationRequested();return new Lease(value);}catch{Monitor.Exit(value);throw;}
        }
        public static string Inside(string root,string relative)
        {
            if(String.IsNullOrWhiteSpace(relative)||Path.IsPathRooted(relative)||relative.Contains(":"))throw new IOException("资源路径必须是包内相对路径。");string full=Path.GetFullPath(Path.Combine(root,relative.Replace('/',Path.DirectorySeparatorChar)));string prefix=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;if(!full.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new IOException("资源路径越出引擎目录。");return full;
        }
        static void RejectLinks(string path){for(var d=new DirectoryInfo(path);d!=null;d=d.Parent)if(d.Exists&&(d.Attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("引擎目录不支持符号链接或目录联接。");}
        public string PackageRoot(EnginePackage p){return Inside(Root,p.Folder);}
        public static string Find(string root,string name){if(!Directory.Exists(root))throw new IOException("引擎尚未安装。");var found=Directory.GetFiles(root,name,SearchOption.AllDirectories);if(found.Length!=1)throw new IOException("引擎资源缺失或重复："+name);return found[0];}
        EngineReceipt Receipt(string folder){try{string path=Inside(folder,"installed.json");var f=new FileInfo(path);if(!f.Exists)return null;lock(gate){Tuple<long,long,EngineReceipt> cached;if(receipts.TryGetValue(path,out cached)&&cached.Item1==f.Length&&cached.Item2==f.LastWriteTimeUtc.Ticks)return cached.Item3;var value=new JavaScriptSerializer{MaxJsonLength=4000000}.Deserialize<EngineReceipt>(File.ReadAllText(path));receipts[path]=Tuple.Create(f.Length,f.LastWriteTimeUtc.Ticks,value);return value;}}catch(IOException){return null;}catch(ArgumentException){return null;}}
        static bool Critical(string name){string ext=Path.GetExtension(name).ToLowerInvariant();return ext==".exe"||ext==".dll"||ext==".onnx"||name.EndsWith("model.bin",StringComparison.OrdinalIgnoreCase)||name.EndsWith("voices.bin",StringComparison.OrdinalIgnoreCase)||name.EndsWith("tokens.txt",StringComparison.OrdinalIgnoreCase);}
        public bool Installed(EnginePackage package)
        {
            var folder=PackageRoot(package);var receipt=Receipt(folder);if(receipt==null||receipt.Id!=package.Id||receipt.SourceHash!=package.Sha256||receipt.Files==null||receipt.Files.Length==0)return false;
            try{return receipt.Files.Where(f=>Critical(f.Path)).All(f=>{var file=new FileInfo(Inside(folder,f.Path));return file.Exists&&file.Length==f.Bytes&&file.LastWriteTimeUtc.Ticks==f.Ticks;});}catch(IOException){return false;}
        }
        public void Verify(EnginePackage package,CancellationToken token)
        {
            if(!Installed(package))throw new IOException("引擎未安装或文件已变化，请联网下载/修复。");var folder=PackageRoot(package);foreach(var file in Receipt(folder).Files.Where(f=>Critical(f.Path))){token.ThrowIfCancellationRequested();if(Hash(Inside(folder,file.Path),token)!=file.Hash)throw new IOException("引擎完整性校验失败，请重新下载："+file.Path);}
        }
        public static string Hash(string path,CancellationToken token){using(var s=File.OpenRead(path))using(var sha=SHA256.Create()){byte[] buffer=new byte[1024*1024];int n;while((n=s.Read(buffer,0,buffer.Length))>0){token.ThrowIfCancellationRequested();sha.TransformBlock(buffer,0,n,buffer,0);}sha.TransformFinalBlock(buffer,0,0);return BitConverter.ToString(sha.Hash).Replace("-","").ToLowerInvariant();}}
        public void Install(IEnumerable<EnginePackage> packages,CancellationToken token,Action<EngineProgress> progress)
        {
            var pending=packages.Where(p=>!Installed(p)).Distinct().ToArray();long total=pending.Sum(p=>p.Bytes),done=0;
            foreach(var package in pending)
            {
                using(EnterPackage(package,token))
                {
                if(Installed(package)){done+=package.Bytes;continue;}
                string archive=Inside(Root,"downloads/"+package.Id+".part");Directory.CreateDirectory(Path.GetDirectoryName(archive));Download(package,archive,token,(n,message)=>progress(new EngineProgress{Written=done+n,Total=total,Message=message}));
                InstallArchiveCore(package,archive,token,message=>progress(new EngineProgress{Written=done+package.Bytes,Total=total,Message=message}));done+=package.Bytes;File.Delete(archive);
                }
            }
            progress(new EngineProgress{Written=total,Total=total,Message="资源已安装，可以离线使用。"});
        }
        static void Download(EnginePackage package,string target,CancellationToken token,Action<long,string> progress)
        {
            var uri=new Uri(package.Url);if(uri.Scheme!="https")throw new IOException("下载源必须使用 HTTPS。");long offset=File.Exists(target)?new FileInfo(target).Length:0;if(offset>package.Bytes){File.Delete(target);offset=0;}if(offset==package.Bytes)return;
            ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;var request=(HttpWebRequest)WebRequest.Create(uri);request.UserAgent="FileTools/0.9";request.Timeout=30000;request.ReadWriteTimeout=30000;if(offset>0)request.AddRange(offset);
            using(token.Register(()=>request.Abort()))
            try
            {
                using(var response=(HttpWebResponse)request.GetResponse())
                {
                    if(response.StatusCode==HttpStatusCode.PartialContent){string range=response.Headers["Content-Range"]??"";if(!range.StartsWith("bytes "+offset+"-",StringComparison.Ordinal))throw new IOException("下载服务器续传位置不匹配，请重试。");}
                    else if(response.StatusCode==HttpStatusCode.OK)offset=0;else throw new IOException("下载服务返回异常状态。");
                    using(var input=response.GetResponseStream())using(var output=new FileStream(target,offset==0?FileMode.Create:FileMode.Append,FileAccess.Write,FileShare.None))
                    {byte[] buffer=new byte[128*1024];int n;long resumed=offset;var watch=Stopwatch.StartNew();long last=-1000;while((n=input.Read(buffer,0,buffer.Length))>0){token.ThrowIfCancellationRequested();if(offset+n>package.Bytes)throw new IOException("下载文件大于已核实的大小。");output.Write(buffer,0,n);offset+=n;if(watch.ElapsedMilliseconds-last>=100){last=watch.ElapsedMilliseconds;progress(offset,"正在下载 "+package.Name+" · "+DownloadJob.SizeText(offset)+" / "+DownloadJob.SizeText(package.Bytes)+" · "+DownloadJob.SizeText((long)((offset-resumed)/Math.Max(.1,watch.Elapsed.TotalSeconds)))+"/s");}}output.Flush(true);}
                }
                if(offset!=package.Bytes)throw new IOException("下载未完整，重试将继续下载。");
            }
            catch(WebException){token.ThrowIfCancellationRequested();throw new IOException("无法连接资源下载站点，请检查网络。已下载部分会保留，可重试续传，也可导入离线资源。");}
            catch(IOException){token.ThrowIfCancellationRequested();throw;}
        }
        public void InstallArchive(EnginePackage package,string archive,CancellationToken token,Action<string> progress)
        {using(EnterPackage(package,token))InstallArchiveCore(package,archive,token,progress);}
        void InstallArchiveCore(EnginePackage package,string archive,CancellationToken token,Action<string> progress)
        {
            progress("正在校验下载文件…");if(new FileInfo(archive).Length!=package.Bytes||Hash(archive,token)!=package.Sha256){if(Path.GetFullPath(archive).StartsWith(Inside(Root,"downloads")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))File.Delete(archive);throw new IOException("资源大小或 SHA256 校验不符，未启用此资源，请重新下载。");}
            string stage=Inside(Root,".install-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);
            try
            {
                progress("正在解压安装，请稍等…");if(package.Archive=="file")File.Copy(archive,Inside(stage,package.FileName));else if(package.Archive=="zip")ExtractZip(archive,stage,token);else if(package.Archive=="tar")ExtractTar(archive,stage,token);else throw new IOException("未知资源包格式。");
                token.ThrowIfCancellationRequested();if(package.Id=="whisper-runtime")SpeechProcess.EnableWhisperUtf8(Find(stage,"whisper-cli.exe"));
                if(package.Id=="sherpa-runtime"){string library=Find(stage,"sherpa-onnx-c-api.dll");if(!File.Exists(Path.Combine(Path.GetDirectoryName(library),"onnxruntime.dll")))throw new IOException("语音生成引擎缺少配套CPU运行库。");}if(package.Id=="kokoro-11"||package.Id=="kokoro-10"){Find(stage,"model.int8.onnx");Find(stage,"voices.bin");Find(stage,"tokens.txt");}
                WriteReceipt(stage,package.Id,package.Sha256,token);Commit(stage,PackageRoot(package));stage=null;
            }
            finally{if(stage!=null)RemoveGenerated(stage);}
        }
        static void ExtractZip(string archive,string target,CancellationToken token)
        {
            using(var zip=ZipFile.OpenRead(archive)){if(zip.Entries.Count>15000||zip.Entries.Sum(e=>e.Length)>4L*1024*1024*1024)throw new IOException("资源包超过安装上限。");foreach(var entry in zip.Entries){token.ThrowIfCancellationRequested();if(entry.FullName.Equals("installed.json",StringComparison.OrdinalIgnoreCase))throw new IOException("资源包不能自带安装完成标记。");if((entry.ExternalAttributes>>16&0xf000)==0xa000)throw new IOException("资源包不允许符号链接。");string path=Inside(target,entry.FullName);if(entry.FullName.EndsWith("/")){Directory.CreateDirectory(path);continue;}Directory.CreateDirectory(Path.GetDirectoryName(path));using(var input=entry.Open())using(var output=File.Create(path)){var buffer=new byte[128*1024];int n;while((n=input.Read(buffer,0,buffer.Length))>0){token.ThrowIfCancellationRequested();output.Write(buffer,0,n);}}}}
        }
        static void ExtractTar(string archive,string target,CancellationToken token)
        {
            string tar=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"tar.exe");if(!File.Exists(tar))throw new IOException("当前 Windows 缺少 tar 解包工具。请在支持的 Windows 10/11 上安装资源，或导入已解压的兼容音色包。");bool invalid=false;int count=0;
            SpeechProcess.Run(tar,"-tf "+SpeechProcess.Quote(archive),token,line=>{lock(target){count++;try{Inside(target,line.TrimEnd('/'));if(count>15000)invalid=true;}catch(IOException){invalid=true;}}},120);if(invalid)throw new IOException("资源包目录结构不安全或文件过多。");
            SpeechProcess.Run(tar,"-tvf "+SpeechProcess.Quote(archive),token,line=>{if(line.Length>0&&line[0]!='-'&&line[0]!='d')invalid=true;},120);if(invalid)throw new IOException("资源包包含不支持的链接/设备文件。");
            SpeechProcess.Run(tar,"-xf "+SpeechProcess.Quote(archive)+" -C "+SpeechProcess.Quote(target),token,timeout:600);
            if(Directory.GetFiles(target,"*",SearchOption.AllDirectories).Sum(p=>new FileInfo(p).Length)>4L*1024*1024*1024)throw new IOException("解压结果超过安装预算。");
        }
        void WriteReceipt(string folder,string id,string source,CancellationToken token)
        {
            var files=Directory.GetFiles(folder,"*",SearchOption.AllDirectories).Select(path=>{RejectLinks(path);var f=new FileInfo(path);return new EngineFileReceipt{Path=path.Substring(folder.Length+1),Bytes=f.Length,Ticks=f.LastWriteTimeUtc.Ticks,Hash=Hash(path,token)};}).ToArray();
            File.WriteAllText(Inside(folder,"installed.json"),new JavaScriptSerializer{MaxJsonLength=4000000}.Serialize(new EngineReceipt{Id=id,SourceHash=source,Files=files}));
        }
        void Commit(string stage,string destination)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination));string previous=null;if(Directory.Exists(destination)){if(!File.Exists(Inside(destination,"installed.json")))throw new IOException("目标目录已有未知内容，未覆盖。请先检查外置引擎目录。");previous=Inside(Root,".previous-"+Guid.NewGuid().ToString("N"));Directory.Move(destination,previous);}
            try{Directory.Move(stage,destination);}catch{if(previous!=null)Directory.Move(previous,destination);throw;}if(previous!=null)RemoveGenerated(previous);
        }
        void RemoveGenerated(string folder){if(!Path.GetFullPath(folder).StartsWith(Root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new IOException("清理目标超出引擎目录。");RejectLinks(folder);foreach(var p in Directory.GetFileSystemEntries(folder,"*",SearchOption.AllDirectories))if((File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0)throw new IOException("拒绝清理链接目录。");Directory.Delete(folder,true);}
        public VoicePack[] VoicePacks()
        {
            var list=new List<VoicePack>{SpeechCatalog.Pack(SpeechCatalog.Kokoro11,this),SpeechCatalog.Pack(SpeechCatalog.Kokoro10,this)};string directory=Inside(Root,"voice-packs");if(Directory.Exists(directory))foreach(string folder in Directory.GetDirectories(directory,"custom-*")){try{var m=json.Deserialize<LocalVoiceManifest>(File.ReadAllText(Inside(folder,"voice-pack.json")));list.Add(ValidatePack(folder,m));}catch(IOException){}catch(ArgumentException){}}return list.ToArray();
        }
        static VoicePack ValidatePack(string folder,LocalVoiceManifest m)
        {
            if(m.Schema!=1||m.Adapter!="kokoro"||String.IsNullOrWhiteSpace(m.Name)||m.Name.Length>80||m.Voices==null||m.Voices.Length==0||m.Voices.Length>1000||m.Voices.Any(v=>v.Id<0||v.Id>999||String.IsNullOrWhiteSpace(v.Name)||v.Name.Length>80||v.Language!="zh"&&v.Language!="en")||m.Voices.Select(v=>v.Id).Distinct().Count()!=m.Voices.Length)throw new IOException("音色包清单不符合 Kokoro 格式。");
            foreach(string relative in new[]{m.Model,m.VoicesFile,m.Tokens}.Concat((m.Lexicons??"").Split(','))){string path=Inside(folder,relative);if(!File.Exists(path))throw new IOException("音色包缺少文件："+relative);}
            return new VoicePack{Id=Path.GetFileName(folder),Name=m.Name,Root=folder,Model=m.Model,VoicesFile=m.VoicesFile,Tokens=m.Tokens,Lexicons=m.Lexicons,License=m.License,Voices=m.Voices};
        }
        public VoicePack ImportVoicePack(string archive,CancellationToken token)
        {
            string stage=Inside(Root,".import-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);try{ExtractZip(archive,stage,token);foreach(string file in Directory.GetFiles(stage,"*",SearchOption.AllDirectories))if(new[]{".exe",".dll",".bat",".cmd",".ps1",".js",".py",".lnk"}.Contains(Path.GetExtension(file).ToLowerInvariant()))throw new IOException("音色包只能包含模型数据，不能包含可执行程序。");var m=json.Deserialize<LocalVoiceManifest>(File.ReadAllText(Inside(stage,"voice-pack.json")));ValidatePack(stage,m);string id="custom-"+Guid.NewGuid().ToString("N");WriteReceipt(stage,id,Hash(archive,token),token);string final=Inside(Root,"voice-packs/"+id);Commit(stage,final);stage=null;return ValidatePack(final,m);}finally{if(stage!=null)RemoveGenerated(stage);}
        }
        public bool VoiceReady(VoicePack pack){if(pack.Package!=null)return Installed(pack.Package);try{var receipt=Receipt(pack.Root);return receipt!=null&&receipt.Files!=null&&File.Exists(pack.File(pack.Model))&&File.Exists(pack.File(pack.VoicesFile))&&File.Exists(pack.File(pack.Tokens))&&receipt.Files.Where(f=>Critical(f.Path)).All(f=>{var file=new FileInfo(Inside(pack.Root,f.Path));return file.Exists&&file.Length==f.Bytes&&file.LastWriteTimeUtc.Ticks==f.Ticks;});}catch(IOException){return false;}}
        public void VerifyVoice(VoicePack pack,CancellationToken token){if(pack.Package!=null){Verify(pack.Package,token);return;}var receipt=Receipt(pack.Root);if(receipt==null)throw new IOException("音色包尚未安装。");foreach(var f in receipt.Files){token.ThrowIfCancellationRequested();if(!File.Exists(Inside(pack.Root,f.Path))||Hash(Inside(pack.Root,f.Path),token)!=f.Hash)throw new IOException("导入的音色包已变化，请重新导入。");}}
    }
}
