using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace LocalConverter
{
    public sealed class EnginePackage
    {
        public string Id,Name,Folder,Url,Sha256,Archive,FileName,License;
        public long Bytes;
        public override string ToString(){return Name;}
    }
    public sealed class SpeechVoice
    {
        public int Id;public string Name,Language,Gender;
        public override string ToString(){return Name;}
    }
    public sealed class VoicePack
    {
        public string Id,Name,Adapter="kokoro",Root,Model,VoicesFile,Tokens,Lexicons,License;
        public SpeechVoice[] Voices;public EnginePackage Package;
        public override string ToString(){return Name;}
        public string File(string relative){return EngineStore.Inside(Root,relative);}
    }
    public static class SpeechCatalog
    {
        static EnginePackage P(string id,string name,string folder,string url,long bytes,string hash,string archive,string file,string license){return new EnginePackage{Id=id,Name=name,Folder=folder,Url=url,Bytes=bytes,Sha256=hash,Archive=archive,FileName=file,License=license};}
        public static readonly EnginePackage Whisper=P("whisper-runtime","Whisper CPU 引擎 1.8.2","runtimes/whisper-1.8.2","https://github.com/ggml-org/whisper.cpp/releases/download/v1.8.2/whisper-bin-x64.zip",3832432,"b1514ebc099765e39fa37eb780b92a140a94c86bb0b3b3d98226b38825979732","zip",null,"MIT · whisper.cpp");
        public static readonly EnginePackage Sherpa=P("sherpa-runtime","语音生成 CPU 引擎 2.0.3","runtimes/sherpa-2.0.3","https://github.com/k2-fsa/sherpa-onnx/releases/download/v2.0.3/sherpa-onnx-v2.0.3-win-x64-shared-MT-MinSizeRel.tar.bz2",28557357,"5e2619053f877c6cd4927320bc79fcd310dc96c17050ac208febeb4df00277f4","tar",null,"Apache-2.0 · sherpa-onnx；MIT · ONNX Runtime");
        public static readonly EnginePackage Tiny=P("whisper-tiny","轻量中英文 · 下载约 32.2MB","models/whisper-tiny-q5","https://huggingface.co/ggerganov/whisper.cpp/resolve/5359861c739e955e79d9a303bcbc70fb988958b1/ggml-tiny-q5_1.bin",32152673,"818710568da3ca15689e31a743197b520007872ff9576237bda97bd1b469c3d7","file","model.bin","MIT · Whisper");
        public static readonly EnginePackage Base=P("whisper-base","均衡中英文 · 下载约 59.7MB","models/whisper-base-q5","https://huggingface.co/ggerganov/whisper.cpp/resolve/5359861c739e955e79d9a303bcbc70fb988958b1/ggml-base-q5_1.bin",59707625,"422f1ae452ade6f30a004d7e5c6a43195e4433bc370bf23fac9cc591f01a8898","file","model.bin","MIT · Whisper");
        public static readonly EnginePackage Kokoro11=P("kokoro-11","中英文音色包 · 103 个音色","voice-packs/kokoro-11","https://github.com/k2-fsa/sherpa-onnx/releases/download/tts-models/kokoro-int8-multi-lang-v1_1.tar.bz2",147052523,"259c9c8bc906d6078e4e0570421885dabb68b539c2f970fcbb890a58eeaf3e9c","tar",null,"Apache-2.0 · Kokoro");
        public static readonly EnginePackage Kokoro10=P("kokoro-10","扩展音色包 · 含英文男声与中文男女声","voice-packs/kokoro-10","https://github.com/k2-fsa/sherpa-onnx/releases/download/tts-models/kokoro-int8-multi-lang-v1_0.tar.bz2",132353410,"72c347230107ef8a31098ec4d468a605244beef5f1ed00db97efa13518800653","tar",null,"Apache-2.0 · Kokoro");
        public static readonly EnginePackage[] Packages={Whisper,Sherpa,Tiny,Base,Kokoro11,Kokoro10};
        static SpeechVoice V(int id,string name,string lang,string gender){return new SpeechVoice{Id=id,Name=name,Language=lang,Gender=gender};}
        public static VoicePack Pack(EnginePackage package,EngineStore store)
        {
            var voices=new List<SpeechVoice>();string prefix;
            if(package==Kokoro11){prefix="kokoro-int8-multi-lang-v1_1";voices.Add(V(0,"英语女声 · Maple","en","女"));voices.Add(V(1,"英语女声 · Sol","en","女"));voices.Add(V(2,"英式女声 · Vale","en","女"));for(int i=3;i<=57;i++)voices.Add(V(i,"中文女声 "+(i-2).ToString("00"),"zh","女"));for(int i=58;i<=102;i++)voices.Add(V(i,"中文男声 "+(i-57).ToString("00"),"zh","男"));}
            else{prefix="kokoro-int8-multi-lang-v1_0";string[] names={"alloy","aoede","bella","heart","jessica","kore","nicole","nova","river","sarah","sky","adam","echo","eric","fenrir","liam","michael","onyx","puck","santa","alice","emma","isabella","lily","daniel","fable","george","lewis"};for(int i=0;i<names.Length;i++)voices.Add(V(i,"英语"+(i<11||i>=20&&i<=23?"女":"男")+"声 · "+names[i],"en",i<11||i>=20&&i<=23?"女":"男"));string[] cn={"晓贝","晓妮","晓晓","晓伊","云健","云希","云夏","云扬"};for(int i=0;i<cn.Length;i++)voices.Add(V(45+i,"中文"+(i<4?"女":"男")+"声 · "+cn[i],"zh",i<4?"女":"男"));}
            return new VoicePack{Id=package.Id,Name=package.Name,Root=store.PackageRoot(package),Model=prefix+"/model.int8.onnx",VoicesFile=prefix+"/voices.bin",Tokens=prefix+"/tokens.txt",Lexicons=prefix+"/lexicon-us-en.txt,"+prefix+"/lexicon-zh.txt",License=package.License,Voices=voices.ToArray(),Package=package};
        }
        public static string DefaultRoot
        {
            get{var current=new DirectoryInfo(Application.StartupPath);for(int i=0;i<4&&current!=null;i++,current=current.Parent)if(Directory.Exists(Path.Combine(current.FullName,"dist","文件万能处理器")))return Path.Combine(current.FullName,"外置引擎");return Path.Combine(Application.StartupPath,"外置引擎");}
        }
    }
}
