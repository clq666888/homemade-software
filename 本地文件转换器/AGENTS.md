# 项目开发入口

## 范围与交付
- 唯一项目：`E:\小软件开发\本地文件转换器`，遵循主目录已采用的公共规则；不扫描兄弟项目。
- 当前交付0.9.1；Windows x64 原生 WinForms / C#5 / Framework4.8，Python/Pillow图片与PDF后端，音频精简FFmpeg，OCR原生Tesseract与中英文模型。可选语音引擎独立后台下载，用户不需开发环境。
- 根入口 `文件万能处理器.exe`，必须保留 `dist/文件万能处理器/engine`。dist 仅当前版。用户明确授权更新时强制关闭本项目旧 EXE，无需再询问；不按名称结束用户 Word。

## 阅读顺序
1. 本文件。
2. `docs/HANDOFF.md` 当前状态、验证边界。
   同时核对根目录 `开发日志.md` 的当前版本及最近交付条目。
3. `docs/ARCHITECTURE.md` 与本次相关源码；不全量读取依赖/构建输出。

## 主要入口
- `src/MainForm.cs` PDF 工作区/状态；`src/UiControls.cs` 卡片和页面画布。
- `src/PdfEngine.cs` 私有 JSON 进程调用；`engine/pdf_engine.py` PDF/DOC 快速读取与格式导出。
- `src/PageDocument.cs` 页面顺序/撤销/安全覆盖；`src/WordConverter.cs` Word COM 隔离工作进程。
- `src/ResultSession.cs` 结果/原子保存；`src/DownloadJob.cs` 批量字节进度/速度；`src/FilePreview.cs` 快速预览和缓存。
- `src/SplitSettings.cs` 拆分/裁剪参数；`engine/pdf_split.py` 每页/页组 ZIP 与整份 PDF 比例裁剪。
- `src/WatermarkSettings.cs` 文字水印设置；`src/Watermark.cs` 预览/导出共用绘制、后台图层准备；`engine/pdf_watermark.py` 透明图层写入与页面范围验证。
- `src/VisualPickers.cs` 九宫格/实际色块；`src/PdfAccessStore.cs` 按文件版本保存本次已验证密码；`src/PdfPasswordDialog.cs` 导入验证弹窗；`engine/pdf_access.py` probe 和每输入密码路由。
- `src/AudioWorkspace.cs` 独立音频 UI/试听/各功能结果；`src/AudioEngine.cs` 格式识别/转换/精简滤镜进程；`assets/audio-runtime.lock.json` 来源/校验；`scripts/prepare-audio.ps1` 恢复及验证约 11.85MiB 音频组件。
- `src/SpeechWorkspace.cs` 语音转写/生成UI及结果；`SpeechCatalog.cs` 固定下载清单/音色；`EngineStore.cs` 续传/校验/安装/导入；`SpeechJobs.cs` FFmpeg/Whisper/私有TTS作业；`NativeTtsWorker.cs` sherpa2.0.3 x64 C ABI隔离worker；`SpeechProcess.cs` 进程组/取消/UTF8兼容。音色导入规范见`docs/VOICE_PACKS.md`。
- `EngineDownloadManager.cs` 独立并发2/排队/按ID去重/取消重试，`EngineDownloadsForm.cs`无模式逐任务窗口；不能将资源后台任务重新合入Audio/Speech Busy锁导航。MainForm退出必须检查后台HasActive并等待取消收尾；普通页面切换不停止下载。tests/EngineBackgroundChecks.cs无模型验证调度和导航，EngineDownloadChecks实际验证HTTPS续传及两个资源安装。
- `src/ImageWorkspace.cs` 图片导航/任务/结果及OCR右侧文本；`ImageCanvas.cs` 缩放/右键视口/真实草稿/整行OCR；`BrushSizePicker.cs` 可见大小滑块；`ImageBrushes.cs` 圆形路径/马赛克纹理/橡皮；`ImageEditing.cs` 底图与标注层/文字Id重编辑/几何命中；`ImageFilters.cs` 16预设与缩略图；`ImageFileStrip.cs` 底部缩略图；`ImageWatermarkSettings.cs` 水印设置；`engine/image_tools.py` 编解码/离线OCR，`ocr_lines.py` 行合并、`ocr_preprocess.py` 有界增强；`scripts/prepare-image.py` / `assets/image-runtime.lock.json` 校验恢复原有14.30MB组件。
- `src/OfficeConverter.cs` Excel/PPT 独立 COM worker；`src/HtmlConverter.cs` 隔离 Edge/Chrome 打印；`engine/office_formats.py` 新导出、HTML 清理与快速预览。
- `src/PreviewRenderer.cs` 持久管道预览；`engine/preview_worker.py` PDFium 串行渲染/邻页预取；`src/PdfPagePreview.cs` 切割线和半透明灰色遮罩。

## 实际命令
- 开发环境：Python 3.12 创建 `.venv`，`.venv\Scripts\python.exe -m pip install -r engine/requirements.lock`。本轮依赖已安装，保留该环境用于复现，不需重复安装。
- 构建发布：`powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build.ps1`；仅 UI：同命令加 `-GuiOnly`。暂存：`scripts/build-v3.ps1`，发布：`scripts/publish.ps1`。编译器为系统 Framework64 csc，无 NuGet。
- 检查：`powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test.ps1`，正常桌面权限验证 Word；沙箱会拒绝 COM。PDF 与界面流程检查可加 `-SkipWord`；优先测试 .work/release，不存在时从 dist 暂存。
- 图片专项：`.venv\Scripts\python.exe tests/image_regression.py .work/<新目录> .work/release/engine/PdfEngine.exe`；`tests/ImageChecks.cs` 由总测试入口编译运行。手动单跑 Layout/Preview 须传已有 PDF fixture 根目录，不能传空目录；无 Application.Run 的测试每次按钮操作先恢复 WindowsFormsSynchronizationContext。下载检查恢复上次保存目录需正常桌面文件权限，不能把沙箱配置写入失败当成业务断言失败。
- PDF 检查直接运行：`.venv\Scripts\python.exe tests/pdf_regression.py .work/<检查目录> dist/文件万能处理器/engine/PdfEngine.exe`。
- 可再生内容在 `.work`；清理只对逐项解析、已确认属于本轮的绝对目录操作，不删除 `.venv`、源码、锁、用户备份或其他项目。
- 语音专项：`scripts/test-speech.ps1` 读取已校验.work测试资源；首次加`-DownloadFixtures`才下载约200MB测试资源，`-NetworkDownloads`另做真实HTTPS取消/续传。模型仅临时测试，不在构建中自动下载、不安装到正式外置目录。普通总测试只跑不需模型的SpeechNavigationChecks。

## 不可破坏的约束
- 默认只读原件；结果先暂存、后下载，同名自动编号。未完成 `.tmp` 不当结果。
- 下载全程后台执行，准备/落盘阶段保持动画，按总字节报告进度；只有全部原子提交才显示 100%。取消清理未完成文件，保留完整输出和暂存结果；UI 消息完成后必须忽略迟到进度。`tests/DownloadChecks.cs` 验证完整性、取消、批量和界面状态。
- ToolResultStore 按功能隔离结果；未处理功能无下载/旧进度，参数变化必须标明“上次结果”。关闭检查并清理所有功能会话，不能只检查当前页。
- 直接修改仅页面编辑显式选择后确认，检查原件未变化，原子替换并保留同目录 `.bak` 原件；备份属于用户数据，不自动删除。
- 密码不放命令行/设置/正常日志，私有作业目录正常收尾删除。移除加密需正确密码。
- PDF 必须先异步 probe，必要时弹窗验证后才加入；错误可重试、取消跳过，空用户密码可直接验证。输入关联密码仅留内存，移除/关闭释放，文件长度/修改时间变化后失效。预览/页面/全部处理按输入取密码；后台 job.passwords 保留逐文件映射，单 password 仅兼容旧引擎检查，不恢复全局 UI 密码框。加密功能只保留“新的打开密码”。
- 音频以常见格式为范围，新增Git/自带交付文件控制在50MB内，不能只删菜单却留大视频库。自带约10.5MiB专用引擎+1.35MiB降噪/WMA组件，DLL依赖不可盲删；转换/降噪试听最多15秒、经过实际编码，不预装语音大模型。额外语音引擎/模型按用户点击外置下载，不进Git；ProcessJob精确取消自身进程，原件只读，关闭检查所有音频/语音结果。
- PDFium 只在独立进程内串行使用；预览/缩略图请求可取消，关闭等待收尾。缓存数量有上限。
- 预览进程按窗口复用；不要退回每翻页分别启动 inspect 和 render。worker 用私有文档快照避免锁原件，切文档/密码/文件版本清缓存，最多 20 PNG。比例裁剪灰色为裁去区域，选择保留侧会改变输出参数/结果摘要，但只重画遮罩、不重新渲染。窗口化打包无 sys.stdin/stdout，需要恢复继承的管道句柄。
- Word 宏强制关闭、不更新打开链接，按新建 PID 与启动时间收尾，不批量结束其他 Word。PDF → DOCX 不走易阻塞的 Word PDFREFLOW。
- DOCX 输出为正文重建，扫描页为图片，不承诺 OCR 或原排版。不要把样例通过写成所有格式/机器通过。
- 顶部 FunctionTab 是导航，下面 Toolbar 外观是操作；不要重新混用同一组选中按钮。主目录统一使用 Git，仅查看当前项目状态；不新建嵌套仓库，不擅自提交/推送。开发日志、根 EXE 和完整当前发行包须可跟踪，临时目录和开发依赖仍忽略。
- 图片原件只读、编辑单图/其他工具批量、各功能结果隔离。OCR 仅原图框选复制，不自动输出图片。4000万像素上限，动画转静态需显式取第一帧；不要悄悄丢帧。编辑预览与导出共用 ImageEditing；水印与 ImageStamp 共用绘制，Logo 缓存释放。OCR 放隔离进程中以便取消，禁止在线识别；不打包 HEIC 大组件、AI 框架或素材库。新增最终Git预算50MB，临时文件可大但收尾清理。
- 最新用户覆盖旧要求：图片默认居中，放大超出画布后允许且仅右键平移，保留编辑后的视口，“适应画布”复位；左键编辑，中键/空格不平移。BrushSizePicker滑块控制屏幕可见大小，经DPI/zoom换成统一BrushDiameter，圆圈和实际笔刷范围/中心一致，不能画假最小圈。图形拖动显示真实形状，橡皮仅清新增标注/保留底图透明度，文字Id可重编/撤销，滤镜独立。OCR按行并在右侧可选只读文本框显示选中行/多行，支持部分复制、切工具清理旧文。生成文件预览按钮无结果隐藏，有结果后可返回只读/工作视图。侧栏三项尺寸间距统一。
- 导航顺序：转换、页面编辑、合并、拆分、水印、加密、移除加密、压缩；导航和操作项均保留分隔线。每页/页组拆分输出 ZIP；比例裁剪每个输入输出一份 PDF，保留全部页数与顺序，不导出另一半/单页，不打包 ZIP。参数快照在 UI 线程读取；比例裁剪不是脱敏。
- 水印默认新文件、加密保留密码；所有参数进入结果摘要。预览与导出共用 WatermarkSpec.Paint，字体转透明图层，不整页栅格化。PdfPagePreview 的 PageWidth/Height 使用物理点数（含 UserUnit）；后台按可见裁剪框和旋转逆变换定位，不修改原页面旋转或表单坐标。页范围先验证所有输入；每文档相同尺寸共用图像对象。临时图层 finally 清理。
- 指定页数拆分每份用两个空数字框“起始页 ~ 结束页”；不要重新要求用户手写范围表达式。UI 校验正整数及起止顺序后生成后端标准范围，实际页数上限仍由后端验证。
- PDF页面编辑空总览提示为“页面总览”。图片文字实时输入/样式由ImageWorkspaceText串行后台预览，不禁用/重建输入框或回写光标；连续输入合并末尾retext，普通任务/关闭须等待预览收尾，迟到帧不可覆盖新状态。没有“更新选中文字”按钮；画布Delete删除对象，输入框Delete正常删字。tests/ImageTextChecks.cs覆盖该流程，仍仅编辑后加文字。
- OCR默认整页排版，零散模式可选；ocr_lines按双向水平距离合并近基线碎片，不因右块先到漏合并，也不跨远列乱拼。OcrTextFlow供右侧显示/画布复制共用，默认多框连段无强制换行，勾选保留原图换行才逐行；中文不加人工空格、英文空格与标点衔接。图片压缩仅用ImageCompressionSettings的独立快照/Context，按每图格式决定有效画质选项、每图尺寸计算比例，后台不读取控件；不得恢复PNG下无效的质量框。水印颜色标签右侧46×46色块同排。tests/ImageUsabilityChecks.cs验证控件实际有效性、布局和输出。
- 水印色块右侧浅灰“←（点击选择颜色）”，侧栏双行品牌居中、列宽百分比。0.9.0用户已授权实施外置下载语音能力：项目根`外置引擎`只存额外下载/导入资源，除README外Git忽略，不混进dist、不在发布/清理时删用户资源。无资源显示完整UI并禁用处理/试听，只有点击才联网；SHA和字节匹配、临时解压、原子安装后启用。不得随意升级sherpa C ABI456字节/固定偏移；Whisper本地manifest UTF8改动后记录散列，Kaldi规则用worker模型目录中的ASCII文件名解决中文路径。新版中英实际生成/转写已测试，不能扩大为全部音色/录音准确。
- 根EXE依赖完整dist，自带组件必须允许主Git收录，外置资源须忽略；不能把“未被忽略”描述成“已提交/上传”。用户本轮询问已核实dist与根EXE尚未跟踪，未授权提交/推送，仍不擅自操作Git提交。
- Office 输入仅只读独立进程，不结束用户已有 Excel/PPT；Excel 打开密码占位不得超过 15 字符。HTML 输入仅本地文件，先清理脚本并加 CSP；不要用全局禁用 JavaScript 的浏览器参数（会破坏 CLI 打印）。浏览器独立 profile 与 ProcessJob 收尾，禁止加载远程资源。
- XLSX 提取表格/正文，PPTX 为页面图片，HTML 可选页面图片/正文；必须保留界面与 README 的还原范围说明，不描述为无损/可编辑 PPT。

## 版本与日志
- 当前版本标记见 `src/Program.cs`；同步检查界面、manifest、发布脚本、日志和 EXE 元数据，避免不一致。开发/已交付状态以开发日志及 HANDOFF 为准。
- 按主目录新规范：新增兼容能力升次版本，纯修复/小幅界面优化升修订号，不兼容换代须授权后升主版本；仅文档维护不升版。保留历史实际编号，不追改 0.5.1。
- 根目录 `开发日志.md` 保存长期历史：大版本标题、次版本列表、修订子条目，日期/变化/分级原因/验证结论齐全；不确定历史明确待核实。`HANDOFF.md` 仅当前状态，不替代或清理历史日志。
