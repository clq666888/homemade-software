# 扩展音色包

内置下载目录提供 Kokoro v1.1 中英文包（103音色）和 v1.0 扩展包（本版展示其中中文、英文音色）。同一包的多个音色共用模型；不是每个声音都下载一份大模型。额外模型全部存于项目根目录 `外置引擎`，不提交Git，不放进dist。资源齐全后处理全程离线。

“离线安装官方包”（原“导入离线资源”）用于导入本版锁定的官方 ZIP、TAR.BZ2 或 BIN 下载原件，SHA256须匹配当前所选版本；可同时选引擎与模型。“导入自定义音色”（原“导入音色包”）用于下述自定义兼容数据包，两种入口不同。

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

导入会复制并登记到 `外置引擎/voice-packs/custom-*`，保存文件SHA256并在使用前核对；导入阶段仅验证结构与文件完整性，不能保证实际合成成功；请先试听，实际兼容性由隔离工作进程验证。导入包不会安装或执行包里的程序。兼容数据包可不改软件代码直接增加音色，其他模型家族需要另写对应适配器；WAV/MP3录音不能直接当作该格式的音色向量。通用音色克隆本版未实现。

## 音色名称、分类与试听

v1.1中文使用官方zf_001、zm_009等非连续编号，不是预先分类的萝莉音/男低音/角色音库。界面不根据性别或编号杜撰风格，个人备注与收藏只帮助再次查找，不替代现成具名资源的适配。v1.0当前提供晓贝、晓妮、晓晓、晓伊、云健、云希、云夏、云扬等具名中文音色。官方映射及试听：https://k2-fsa.github.io/sherpa/onnx/tts/all/Chinese-English/kokoro-multi-lang-v1_1.html 。

其他具名模型可增加新适配器后接入；sherpa-onnx VITS目录已有具名模型，但本版仅Kokoro，不宣称已接入VITS。网上声音页面/在线API不等于可下载的离线模型，是否能适配须核实模型数据、许可及实际输出。当前未新增具名角色包。

0.12.0重复试听使用会话缓存（最多12段/16MiB），相同参数无需再载入模型；未缓存音色首次载入仍慢。允许备注/收藏，配置只在本机，缓存关闭清理。

## 来源与版本

- Whisper CPU 1.8.2、Tiny/Base Q5_1多语言模型：来源[whisper.cpp](https://github.com/ggml-org/whisper.cpp)及[维护者模型](https://huggingface.co/ggerganov/whisper.cpp)，MIT。
- sherpa-onnx CPU 2.0.3：来源[官方发布](https://github.com/k2-fsa/sherpa-onnx/releases/tag/v2.0.3)，Apache-2.0，随包ONNX Runtime为MIT。
- Kokoro v1.1/v1.0 INT8：来源[维护者TTS资源](https://github.com/k2-fsa/sherpa-onnx/releases/tag/tts-models)及[原模型](https://huggingface.co/hexgrad/Kokoro-82M-v1.1-zh)，保留资源包中的许可证与来源说明。

精确下载地址、字节数和SHA256以 `src/SpeechCatalog.cs` 为准。下载只在用户点击后发起，不访问在线推理API；音频和输入文字不上传。模型效果受语言、音色、原始录音与模型大小影响，不能按“男低音/萝莉音”等名称未经试听就保证效果。
