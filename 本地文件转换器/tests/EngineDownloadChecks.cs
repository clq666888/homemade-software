using System;
using System.IO;
using System.Linq;
using System.Threading;
using LocalConverter;
static class EngineDownloadChecks
{
    static int checks;
    static void Check(bool value,string name){if(!value)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
    static int Main(string[] args)
    {
        try
        {
            string root=Path.GetFullPath(args[0]);var store=new EngineStore(root);
            foreach(var package in new[]{SpeechCatalog.Whisper,SpeechCatalog.Tiny})
            {
                using(var cancel=new CancellationTokenSource())
                {
                    try{store.Install(new[]{package},cancel.Token,p=>{if(p.Written>0)cancel.Cancel();});throw new Exception("download was not cancelled");}catch(OperationCanceledException){}
                }
                string partial=Path.Combine(root,"downloads",package.Id+".part");Check(File.Exists(partial)&&new FileInfo(partial).Length>0&&!store.Installed(package),package.Id+" cancellation retains only a partial download and stays disabled");
                store.Install(new[]{package},CancellationToken.None,p=>{});Check(store.Installed(package),package.Id+" real HTTPS range retry completes a verified installation");Check(!File.Exists(partial),package.Id+" installed package removes its download fragment");store.Verify(package,CancellationToken.None);Check(true,package.Id+" network-installed files pass SHA256 checks");
            }
            var parallelStore=new EngineStore(Path.Combine(root,"parallel"));using(var manager=new EngineDownloadManager(parallelStore))
            {
                manager.Enqueue(new[]{SpeechCatalog.Whisper,SpeechCatalog.Tiny,SpeechCatalog.Whisper});var watch=System.Diagnostics.Stopwatch.StartNew();while(manager.HasActive){Thread.Sleep(20);if(watch.Elapsed.TotalSeconds>180)throw new Exception("parallel HTTPS timeout");}
                Check(manager.Snapshots.Length==2&&manager.Snapshots.All(s=>s.State==EngineTaskState.Completed),"real background queue downloads and installs two deduplicated official resources");parallelStore.Verify(SpeechCatalog.Whisper,CancellationToken.None);parallelStore.Verify(SpeechCatalog.Tiny,CancellationToken.None);Check(true,"parallel installations preserve both engine receipts and model integrity");
            }
            Console.WriteLine("ALL "+checks+" ENGINE DOWNLOAD CHECKS PASSED");return 0;
        }catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
