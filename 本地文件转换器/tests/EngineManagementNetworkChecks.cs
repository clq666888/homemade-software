using System;
using System.IO;
using System.Linq;
using System.Threading;
using LocalConverter;
static class EngineManagementNetworkChecks
{
    static int checks;static void Check(bool value,string name){if(!value)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
    static void Until(Func<bool> ready){var watch=System.Diagnostics.Stopwatch.StartNew();while(!ready()){Thread.Sleep(5);if(watch.Elapsed.TotalSeconds>120)throw new Exception("HTTPS timeout");}}
    static int Main(string[] args)
    {
        try
        {
            var store=new EngineStore(Path.GetFullPath(args[0]));var package=SpeechCatalog.Whisper;int stop=1;EngineDownloadManager manager=null;
            using(manager=new EngineDownloadManager(store,2,(p,t,r)=>store.Install(new[]{p},t,progress=>{r(progress);if(progress.Written>0&&Interlocked.CompareExchange(ref stop,0,1)==1)manager.Pause(p.Id);else if(progress.Written>0&&Interlocked.CompareExchange(ref stop,0,2)==2)manager.Cancel(p);})))
            {
                manager.Enqueue(new[]{package});Until(()=>!manager.HasActive);Check(manager.Snapshots.Single().State==EngineTaskState.Paused&&store.PartialBytes(package)>0&&!store.Installed(package),"real HTTPS pause retains an incomplete download without enabling the engine");long retained=store.PartialBytes(package);Check(retained<package.Bytes,"real pause occurs before the full archive is downloaded");manager.Enqueue(new[]{package});Until(()=>!manager.HasActive);Check(manager.Snapshots.Single().State==EngineTaskState.Completed&&store.Installed(package),"resume completes the official SHA256-verified engine installation");store.Verify(package,CancellationToken.None);Check(true,"resumed official engine files pass integrity validation");Check(store.PartialBytes(package)==0,"successful resumed installation removes its archive fragment");
                manager.Uninstall(package);Until(()=>!manager.HasActive);Check(!store.Installed(package)&&!Directory.Exists(store.PackageRoot(package)),"uninstall removes a real official engine");stop=2;manager.Enqueue(new[]{package});Until(()=>!manager.HasActive);Check(manager.Snapshots.Single().State==EngineTaskState.Cancelled&&store.PartialBytes(package)==0&&!store.Installed(package),"real HTTPS cancellation removes partial bytes and does not install the engine");
            }
            Console.WriteLine("ALL "+checks+" ENGINE MANAGEMENT NETWORK CHECKS PASSED");return 0;
        }catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
