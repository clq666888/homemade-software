using System;
using System.Collections.Generic;
using System.Linq;

namespace LocalConverter
{
    public sealed class EngineResource
    {
        public EnginePackage Package;public string Group,Title,Usage,RemovalHint;
        public bool Downloadable{get{return !String.IsNullOrWhiteSpace(Package.Url)&&Package.Archive!="local";}}
    }
    public static class EngineResources
    {
        public static string SizeText(long bytes){return bytes>=1000000000?(bytes/1000000000D).ToString("0.#")+" GB":bytes>=1000000?(bytes/1000000D).ToString("0.#")+" MB":bytes>=1000?(bytes/1000D).ToString("0.#")+" KB":bytes+" B";}
        static EngineResource R(EnginePackage package,string group,string title,string usage,string removal){return new EngineResource{Package=package,Group=group,Title=title,Usage=usage,RemovalHint=removal};}
        public static EngineResource[] All(EngineStore store)
        {
            var list=new List<EngineResource>{
                R(SpeechCatalog.Whisper,"语音转文字","Whisper 语音识别引擎","用于“音频处理 → 语音转文字”。轻量和均衡识别模型共用此引擎，还需下载其中一种识别模型。","语音转文字暂不可用；已下载的识别模型会保留。"),
                R(SpeechCatalog.Tiny,"语音转文字","轻量中英文识别模型","用于语音转文字的“轻量中英文”选项。需要 Whisper 引擎，体积较小，识别结果需核对。","轻量识别模型暂不可用；可以切换到已安装的均衡模型。"),
                R(SpeechCatalog.Base,"语音转文字","均衡中英文识别模型","用于语音转文字的“均衡中英文”选项。需要 Whisper 引擎；与轻量模型任选一种或同时安装。","均衡识别模型暂不可用；可以切换到已安装的轻量模型。"),
                R(SpeechCatalog.Sherpa,"语音生成","语音生成共用引擎（必需）","用于“音频处理 → 语音生成”。所有 Kokoro 音色包共用此引擎，还需安装一个音色包。","语音生成和试听暂不可用；已下载或导入的音色包会保留。"),
                R(SpeechCatalog.Kokoro11,"语音生成","Kokoro v1.1 · 中英103音色（推荐）","100个中文男女声、3个英语女声。与v1.0任选一套即可，不必都装；共用语音生成引擎。","这个音色包的音色将不可用，其他音色包和引擎会保留。"),
                R(SpeechCatalog.Kokoro10,"语音生成","Kokoro v1.0 · 经典中英音色（可选）","本软件提供28个英语男女声、8个中文男女声。与v1.1是不同音色集合，任选一套即可；共用语音生成引擎。","这个音色包的音色将不可用，其他音色包和引擎会保留。"),
                R(VideoCatalog.Runtime,"视频处理","视频转换 / GIF 引擎","用于“视频处理 → 格式转换”和“视频转 GIF”，两项功能共用一个引擎，不影响原有音频转换或图片/PDF功能。","视频格式转换和视频转 GIF 暂不可用；已生成的结果会保留。")};
            list.AddRange(store.ImportedPackages().Select(p=>R(p,"导入的音色包",p.Name,"本地导入的兼容 Kokoro 音色包，用于语音生成，需要语音生成引擎。没有固定下载源；卸载后需要重新导入原 ZIP。","此导入包的音色将不可用；需要原 ZIP 才能重新导入。")));return list.ToArray();
        }
    }
}
