using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LocalConverter;
static class SpeechSampleChecks
{
    static int checks;static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
    static void Main(string[] args){string root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);Run(root,args.Length>1?args[1]:null).GetAwaiter().GetResult();Console.WriteLine("ALL "+checks+" SAMPLE CHECKS PASSED");}
    static async Task Run(string root,string real)
    {
        var token=CancellationToken.None;int generated=0;Func<string,Task<string>> create=folder=>{generated++;string path=Path.Combine(folder,"sample.wav");File.WriteAllBytes(path,new byte[60]);return Task.FromResult(path);};string cachedRoot=Path.Combine(root,"cache");
        using(var cache=new SpeechSampleCache(cachedRoot,2,130)){
            string first=await cache.GetAsync("voice1|zh|1|version1",create,token);string repeat=await cache.GetAsync("voice1|zh|1|version1",create,token);Check(generated==1&&cache.Reused&&first==repeat,"same sample replays without running synthesis twice");
            await cache.GetAsync("voice2|zh|1|version1",create,token);Check(generated==2&&!cache.Reused,"a different voice generates a distinct sample");await cache.GetAsync("voice1|zh|1|version1",create,token);Check(generated==2&&cache.Reused,"switching voices and returning reuses the cached audio");
            await cache.GetAsync("voice1|zh|1.2|version1",create,token);Check(generated==3&&!cache.Reused,"a different speed requires its own sample");await cache.GetAsync("voice2|zh|1|version1",create,token);Check(generated==4,"least recently used audio is evicted within the cache limit");
            await cache.GetAsync("voice2|en|1|version1",create,token);Check(generated==5,"language participates in the sample identity");await cache.GetAsync("voice2|en|1|version2",create,token);Check(generated==6,"changed engine or model version never replays old audio");
            var cancelled=new CancellationTokenSource();try{await cache.GetAsync("cancel",folder=>{cancelled.Cancel();return create(folder);},cancelled.Token);throw new Exception("not cancelled");}catch(OperationCanceledException){Check(Directory.GetDirectories(cachedRoot).Length==2,"cancelled synthesis never commits a partial sample");}
            try{await cache.GetAsync("error",folder=>{throw new IOException("test failure");},token);}catch(IOException){}Check(Directory.GetDirectories(cachedRoot).Length==2,"failed synthesis cleans its temporary directory");
        }Check(!Directory.Exists(cachedRoot),"closing removes the owned sample cache");
        string prefs=Path.Combine(root,"preferences.json");var preferences=new VoicePreferences(prefs);preferences.Set("pack",3,"低沉男声",true);var loaded=new VoicePreferences(prefs);Check(loaded.Get("pack",3).Alias=="低沉男声"&&loaded.Get("pack",3).Favorite,"voice notes and favorites survive reopening");Check(!loaded.Get("other",3).Favorite,"same speaker id in a different pack is independent");
        var store=new EngineStore(Path.Combine(root,"engines"));var pack=SpeechCatalog.Pack(SpeechCatalog.Kokoro11,store);Check(pack.Voices.Single(v=>v.Id==11).Name.Contains("zf_017")&&pack.Voices.Single(v=>v.Id==58).Name.Contains("zm_009"),"display names match the nonsequential official speaker mapping");
        if(real!=null){var actual=new EngineStore(real);var actualPack=SpeechCatalog.Pack(SpeechCatalog.Kokoro11,actual);if(!actual.Installed(SpeechCatalog.Sherpa)||!actual.VoiceReady(actualPack))throw new Exception("real resources not installed");using(actual.Use(SpeechCatalog.Sherpa.Id,actualPack.Id))using(var cache=new SpeechSampleCache(Path.Combine(root,"real-cache"))){var jobs=new SpeechJobs(actual);int calls=0;Func<string,Task<string>> generate=folder=>Task.Run(()=>{calls++;return jobs.Generate(actualPack,3,"你好，很高兴认识你。",1,folder,token,(p,s)=>{});});string key=actual.VersionStamp(actual.PackageRoot(SpeechCatalog.Sherpa))+actual.VersionStamp(actualPack.Root)+"|3|zh|1";var clock=System.Diagnostics.Stopwatch.StartNew();string wave=await cache.GetAsync(key,generate,token);long firstMs=clock.ElapsedMilliseconds;clock.Restart();await cache.GetAsync(key,generate,token);long replayMs=clock.ElapsedMilliseconds;Check(calls==1&&cache.Reused&&new FileInfo(wave).Length>44,"real Kokoro output is reused with no second worker generation");Console.WriteLine("REAL_FIRST_MS="+firstMs+" REAL_CACHE_REPLAY_MS="+replayMs);}}
    }
}
