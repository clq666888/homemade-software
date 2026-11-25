# 扩展音色包

内置下载目录提供 Kokoro v1.1 中英文包（103音色）和 v1.0 扩展包（本版展示其中中文、英文音色）。同一包的多个音色共用模型；不是每个声音都下载一份大模型。额外模型全部存于项目根目录 `外置引擎`，不提交Git，不放进dist。资源齐全后处理全程离线。

“导入离线资源”用于导入本版锁定的官方 ZIP、TAR.BZ2 或 BIN 下载原件，SHA256须匹配当前所选版本；可同时选引擎与模型。“导入音色包”用于下述自定义兼容数据包，两种入口不同。

## 自定义 Kokoro 音色包

ZIP根目录放 `voice-pack.json`，及其引用的模型、音色向量、token和词典。示例：

```json
{
  "Schema": 1,
  "Name": "我的兼容音色包",
  "Adapter": "kokoro",
  "Model": "model.int8.onnx",
  "VoicesFile": "voices.bin",
  "Tokens": "tokens.txt",
  "Lexicons": "lexicon-us-en.txt,lexicon-zh.txt",
  "License": "请填真实许可证和来源说明",
  "Voices": [
    {"Id": 0, "Name": "中文女声一", "Language": "zh", "Gender": "女"},
    {"Id": 1, "Name": "英语男声一", "Language": "en", "Gender": "男"}
  ]
}
```

编号、语言和名称必须符合实际模型，示例不代表任意模型都有这些音色。`Model`须为当前 sherpa-onnx 2.0.3 Kokoro 适配器支持的ONNX模型；音色向量、tokens和词典必须来自配套版本。路径全为包内相对路径，词典用英文逗号分隔；路径内不要包含逗号。可把日期/数字/电话规则 `date-zh.fst`、`number-zh.fst`、`phone-zh.fst` 放到模型同目录。不要包含 `installed.json`、EXE、DLL或脚本，不接受路径越界和符号链接。ZIP最多15000项、解压最多4GiB。

导入会复制并登记到 `外置引擎/voice-packs/custom-*`，保存文件SHA256并在使用前核对；模型是否能实际合成还由隔离工作进程验证。导入包不会安装或执行包里的程序。兼容数据包可不改软件代码直接增加音色，其他模型家族需要另写对应适配器；WAV/MP3录音不能直接当作该格式的音色向量。通用音色克隆本版未实现。

## 来源与版本

- Whisper CPU 1.8.2、Tiny/Base Q5_1多语言模型：来源[whisper.cpp](https://github.com/ggml-org/whisper.cpp)及[维护者模型](https://huggingface.co/ggerganov/whisper.cpp)，MIT。
- sherpa-onnx CPU 2.0.3：来源[官方发布](https://github.com/k2-fsa/sherpa-onnx/releases/tag/v2.0.3)，Apache-2.0，随包ONNX Runtime为MIT。
- Kokoro v1.1/v1.0 INT8：来源[维护者TTS资源](https://github.com/k2-fsa/sherpa-onnx/releases/tag/tts-models)及[原模型](https://huggingface.co/hexgrad/Kokoro-82M-v1.1-zh)，保留资源包中的许可证与来源说明。

精确下载地址、字节数和SHA256以 `src/SpeechCatalog.cs` 为准。下载只在用户点击后发起，不访问在线推理API；音频和输入文字不上传。模型效果受语言、音色、原始录音与模型大小影响，不能按“男低音/萝莉音”等名称未经试听就保证效果。
