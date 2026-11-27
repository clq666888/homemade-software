using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LocalConverter
{
    public enum EngineTaskState{Queued,Downloading,Installing,Completed,Paused,Cancelled,Failed,Removing,Clearing}
    public sealed class EngineTaskSnapshot
    {
        public EnginePackage Package;public EngineTaskState State;public long Written,Total;public string Message;public bool Cancelling;
        public bool Active{get{return EngineDownloadManager.Active(State);}}
        public int Percent{get{return State==EngineTaskState.Completed?100:Total<=0?0:(int)Math.Min(99,Written*100/Total);}}
    }
    public sealed class EngineDownloadManager : IDisposable
    {
        sealed class Job{public EnginePackage Package;public CancellationTokenSource Cancel=new CancellationTokenSource();public Task Work;public EngineTaskState State=EngineTaskState.Queued;public long Written;public int StopMode;public string Message="排队等待";}
        readonly EngineStore store;readonly Action<EnginePackage,CancellationToken,Action<EngineProgress>> installer;readonly SemaphoreSlim slots;readonly object gate=new object();readonly Dictionary<string,Job> jobs=new Dictionary<string,Job>();readonly List<Task> workers=new List<Task>();long revision;bool disposed;
        public const int DefaultConcurrency=2;public EngineStore Store{get{return store;}}
        public EngineDownloadManager(EngineStore store,int concurrency=DefaultConcurrency,Action<EnginePackage,CancellationToken,Action<EngineProgress>> install=null){if(concurrency<1||concurrency>4)throw new ArgumentOutOfRangeException("concurrency");this.store=store;slots=new SemaphoreSlim(concurrency,concurrency);installer=install??((package,token,report)=>store.Install(new[]{package},token,report));}
        public long Revision{get{return Interlocked.Read(ref revision);}}
        public EngineTaskSnapshot[] Snapshots{get{lock(gate)return jobs.Values.Select(j=>new EngineTaskSnapshot{Package=j.Package,State=j.State,Written=j.Written,Total=j.Package.Bytes,Message=j.Message,Cancelling=j.StopMode!=0}).ToArray();}}
        public bool HasActive{get{return Snapshots.Any(s=>s.Active);}}
        public bool IsActive(string id){return Snapshots.Any(s=>s.Package.Id==id&&s.Active);}
        public static bool Active(EngineTaskState state){return state==EngineTaskState.Queued||state==EngineTaskState.Downloading||state==EngineTaskState.Installing||state==EngineTaskState.Removing||state==EngineTaskState.Clearing;}
        Job Create(EnginePackage package,EngineTaskState state)
        {
            if(disposed)throw new ObjectDisposedException("EngineDownloadManager");Job old;if(jobs.TryGetValue(package.Id,out old)){var retired=old;retired.Work.ContinueWith(t=>retired.Cancel.Dispose(),TaskScheduler.Default);}workers.RemoveAll(t=>t.IsCompleted);var job=new Job{Package=package,State=state,Written=store.PartialBytes(package)};jobs[package.Id]=job;Interlocked.Increment(ref revision);return job;
        }
        public void Enqueue(IEnumerable<EnginePackage> packages)
        {
            lock(gate){if(disposed)throw new ObjectDisposedException("EngineDownloadManager");foreach(var package in packages.Where(p=>p!=null).GroupBy(p=>p.Id).Select(g=>g.First())){if(store.Installed(package))continue;Job old;if(jobs.TryGetValue(package.Id,out old)&&Active(old.State))continue;if(package.Archive=="local")continue;var job=Create(package,EngineTaskState.Queued);job.Work=Run(job);workers.Add(job.Work);}}
        }
        void Update(Job job,EngineTaskState state,string message,long written=-1){lock(gate){job.State=state;job.Message=message;if(written>=0)job.Written=written;Interlocked.Increment(ref revision);}}
        async Task FinishStopped(Job job)
        {
            if(store.Installed(job.Package)){Update(job,EngineTaskState.Completed,"安装已完成，可离线使用。",job.Package.Bytes);return;}
            if(job.StopMode==2){Update(job,EngineTaskState.Clearing,"正在清理下载片段…");try{await Task.Run(()=>store.DiscardDownload(job.Package)).ConfigureAwait(false);Update(job,EngineTaskState.Cancelled,"已取消，未完成的下载已删除。",0);}catch(Exception error){Update(job,EngineTaskState.Failed,"清理失败，请重试取消："+error.Message,store.PartialBytes(job.Package));}}
            else Update(job,EngineTaskState.Paused,"已暂停，下载进度已保留。",store.PartialBytes(job.Package));
        }
        async Task Run(Job job)
        {
            bool acquired=false,stopped=false;
            try{
                try{await slots.WaitAsync(job.Cancel.Token).ConfigureAwait(false);acquired=true;job.Cancel.Token.ThrowIfCancellationRequested();Update(job,EngineTaskState.Downloading,"正在连接下载源…");await Task.Run(()=>installer(job.Package,job.Cancel.Token,p=>Update(job,p.Message.Contains("校验")||p.Message.Contains("解压")||p.Message.Contains("安装")?EngineTaskState.Installing:EngineTaskState.Downloading,p.Message,p.Written))).ConfigureAwait(false);if(!store.Installed(job.Package))job.Cancel.Token.ThrowIfCancellationRequested();Update(job,EngineTaskState.Completed,"安装完成，可离线使用。",job.Package.Bytes);}
                catch(OperationCanceledException){stopped=true;}catch(Exception error){if(job.Cancel.IsCancellationRequested)stopped=true;else Update(job,EngineTaskState.Failed,error.Message);}
                if(stopped)await FinishStopped(job).ConfigureAwait(false);
            }
            finally{if(acquired)slots.Release();}
        }
        public void Pause(string id){lock(gate){Job job;if(jobs.TryGetValue(id,out job)&&(job.State==EngineTaskState.Queued||job.State==EngineTaskState.Downloading||job.State==EngineTaskState.Installing)){if(job.StopMode==0)job.StopMode=1;job.Cancel.Cancel();Interlocked.Increment(ref revision);}}}
        public void Cancel(string id){EnginePackage package;lock(gate){Job job;if(!jobs.TryGetValue(id,out job))return;package=job.Package;}Cancel(package);}
        public void Cancel(EnginePackage package)
        {
            lock(gate){Job job;if(jobs.TryGetValue(package.Id,out job)&&Active(job.State)){if(job.State==EngineTaskState.Removing||job.State==EngineTaskState.Clearing)return;job.StopMode=2;job.Cancel.Cancel();Interlocked.Increment(ref revision);return;}if(store.Installed(package))return;job=Create(package,EngineTaskState.Clearing);job.StopMode=2;job.Work=FinishStopped(job);workers.Add(job.Work);}
        }
        public void Uninstall(EnginePackage package)
        {
            lock(gate){if(IsActive(package.Id))throw new InvalidOperationException("请先暂停或取消该资源任务。");if(store.InUse(package.Id))throw new InvalidOperationException("该资源正在处理文件，请稍后卸载。");var job=Create(package,EngineTaskState.Removing);job.Work=Remove(job);workers.Add(job.Work);}
        }
        async Task Remove(Job job){try{Update(job,EngineTaskState.Removing,"正在卸载…");await Task.Run(()=>store.Uninstall(job.Package)).ConfigureAwait(false);Update(job,EngineTaskState.Cancelled,"已卸载，需要时可重新下载。",0);}catch(Exception error){Update(job,EngineTaskState.Failed,"卸载未完成："+error.Message);}}
        public Task StopAsync(){lock(gate){foreach(var job in jobs.Values.Where(j=>j.State==EngineTaskState.Queued||j.State==EngineTaskState.Downloading||j.State==EngineTaskState.Installing)){if(job.StopMode==0)job.StopMode=1;job.Cancel.Cancel();}Interlocked.Increment(ref revision);return Task.WhenAll(workers.ToArray());}}
        public void Dispose(){Task pending;lock(gate){if(disposed)return;disposed=true;pending=StopAsync();}pending.ContinueWith(t=>{lock(gate){foreach(var job in jobs.Values)job.Cancel.Dispose();slots.Dispose();}},TaskScheduler.Default);}
    }
}
