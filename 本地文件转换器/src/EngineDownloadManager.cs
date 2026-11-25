using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LocalConverter
{
    public enum EngineTaskState{Queued,Downloading,Installing,Completed,Cancelled,Failed}
    public sealed class EngineTaskSnapshot
    {
        public EnginePackage Package;public EngineTaskState State;public long Written,Total;public string Message;public bool Cancelling;
        public bool Active{get{return State==EngineTaskState.Queued||State==EngineTaskState.Downloading||State==EngineTaskState.Installing;}}
        public int Percent{get{return State==EngineTaskState.Completed?100:Total<=0?0:(int)Math.Min(99,Written*100/Total);}}
    }
    public sealed class EngineDownloadManager : IDisposable
    {
        sealed class Job
        {public EnginePackage Package;public CancellationTokenSource Cancel=new CancellationTokenSource();public Task Work;public EngineTaskState State=EngineTaskState.Queued;public long Written;public string Message="排队等待";}
        readonly EngineStore store;readonly Action<EnginePackage,CancellationToken,Action<EngineProgress>> installer;readonly SemaphoreSlim slots;readonly object gate=new object();readonly Dictionary<string,Job> jobs=new Dictionary<string,Job>();readonly List<Task> workers=new List<Task>();long revision;bool disposed;
        public const int DefaultConcurrency=2;
        public EngineDownloadManager(EngineStore store,int concurrency=DefaultConcurrency,Action<EnginePackage,CancellationToken,Action<EngineProgress>> install=null)
        {
            if(concurrency<1||concurrency>4)throw new ArgumentOutOfRangeException("concurrency");this.store=store;slots=new SemaphoreSlim(concurrency,concurrency);installer=install??((package,token,report)=>store.Install(new[]{package},token,report));
        }
        public long Revision{get{return Interlocked.Read(ref revision);}}
        public EngineTaskSnapshot[] Snapshots{get{lock(gate)return jobs.Values.Select(j=>new EngineTaskSnapshot{Package=j.Package,State=j.State,Written=j.Written,Total=j.Package.Bytes,Message=j.Message,Cancelling=j.Cancel.IsCancellationRequested}).ToArray();}}
        public bool HasActive{get{return Snapshots.Any(s=>s.Active);}}
        public bool IsActive(string id){return Snapshots.Any(s=>s.Package.Id==id&&s.Active);}
        public void Enqueue(IEnumerable<EnginePackage> packages)
        {
            lock(gate)
            {
                if(disposed)throw new ObjectDisposedException("EngineDownloadManager");
                foreach(var package in packages.Where(p=>p!=null).GroupBy(p=>p.Id).Select(g=>g.First()))
                {
                    if(store.Installed(package))continue;Job previous;if(jobs.TryGetValue(package.Id,out previous)){if(Active(previous.State))continue;var retired=previous;retired.Work.ContinueWith(t=>retired.Cancel.Dispose(),TaskScheduler.Default);}
                    workers.RemoveAll(t=>t.IsCompleted);var job=new Job{Package=package};jobs[package.Id]=job;Interlocked.Increment(ref revision);job.Work=Run(job);workers.Add(job.Work);
                }
            }
        }
        static bool Active(EngineTaskState state){return state==EngineTaskState.Queued||state==EngineTaskState.Downloading||state==EngineTaskState.Installing;}
        void Update(Job job,EngineTaskState state,string message,long written=-1){lock(gate){job.State=state;job.Message=message;if(written>=0)job.Written=written;Interlocked.Increment(ref revision);}}
        async Task Run(Job job)
        {
            bool acquired=false;
            try
            {
                await slots.WaitAsync(job.Cancel.Token).ConfigureAwait(false);acquired=true;job.Cancel.Token.ThrowIfCancellationRequested();Update(job,EngineTaskState.Downloading,"正在连接下载源…");
                await Task.Run(()=>installer(job.Package,job.Cancel.Token,p=>Update(job,p.Message.Contains("校验")||p.Message.Contains("解压")||p.Message.Contains("安装")?EngineTaskState.Installing:EngineTaskState.Downloading,p.Message,p.Written))).ConfigureAwait(false);
                Update(job,EngineTaskState.Completed,"安装完成，可离线使用。",job.Package.Bytes);
            }
            catch(OperationCanceledException){Update(job,EngineTaskState.Cancelled,"已取消，下载片段保留；点击继续可续传。");}
            catch(Exception error){Update(job,EngineTaskState.Failed,error.Message);}
            finally{if(acquired)slots.Release();}
        }
        public void Cancel(string id){lock(gate){Job job;if(jobs.TryGetValue(id,out job)&&Active(job.State)){job.Cancel.Cancel();Interlocked.Increment(ref revision);}}}
        public Task StopAsync(){lock(gate){foreach(var j in jobs.Values.Where(j=>Active(j.State)))j.Cancel.Cancel();Interlocked.Increment(ref revision);return Task.WhenAll(workers.ToArray());}}
        public void Dispose(){Task pending;lock(gate){if(disposed)return;disposed=true;pending=StopAsync();}pending.ContinueWith(t=>{lock(gate){foreach(var j in jobs.Values)j.Cancel.Dispose();slots.Dispose();}},TaskScheduler.Default);}
    }
}
