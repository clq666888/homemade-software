# 模块与边界

界面继续使用原生 WinForms，新增专用离线 PDF 引擎，而非浏览器壳或后台服务。GUI 与 PDF 引擎通过单次私有 JSON 作业连接，密码不在进程命令行。后端使用 pypdf（结构、页面、AES、压缩）、PDFium/pypdfium2（页面渲染）、Pillow（图片）、olefile（DOC 容器）。所有原生运行库随 PyInstaller 目录发行。

## 界面与页面模型

- `MainForm.cs`：左侧 PDF 主分类，顶部功能选择；文件队列、工具参数、右侧预览、先处理后下载状态。处理在 Task.Run，进度回 UI；页面载入、预览、缩略图不在 UI 线程执行。
- `UiControls.cs`：FunctionTab 用文字+底线表达导航，不画普通操作按钮边框；ModernButton.Toolbar 用轻量悬停表达操作，添加文件使用描边，流程主动作使用实色。键盘焦点仅在 ShowFocusCues 时显示。FileCards 绘制卡片与红色垃圾桶；PageCanvas 自绘所有页的位置，只为可见页请求缩略图，支持框选、Ctrl/Shift、拖动排序、红叉。手动统一系统 DPI 的布局尺寸，自绘按逻辑坐标换算。
- `PageDocument.cs`：页来源索引与旋转，独立快照撤销栈（100 步），删除至少留一页；覆盖保存检查大小/时间，File.Replace 建立原件备份。
- `SplitSettings.cs`：每页/指定页码/比例切割动态面板；每份以两个空数字文本框和固定 ~ 标记输入起止页，键入限制 ASCII 数字，提交再次验证粘贴值/正整数/起止顺序。内部生成后端规范的范围字符串，用户不输入范围符号；实际页数越界由后端验证。保留份数调整时未删除行的输入，互斥方向复选框和比例；只有当前方式的参数参与结果摘要与作业。MainForm 在 UI 线程读取参数，后台不读控件。

导航项和文件/页面操作项用短竖线分隔，预览与左侧有独立竖线；处理选项白底标题、上下横线与执行区分开。LayoutOptions 按当前字体/DPI 测量组合框完整文本，说明独立换行，面板随动态选项调整高度并允许滚动。单行/单列的 TableLayout 显式使用百分比，避免默认 AutoSize 在缩小时保留旧尺寸；WrappingOptionsPanel 使用视口宽度换行，避免旧虚拟宽度造成横向溢出。动态按钮按 DPI 设置尺寸；FileCards 紧凑空状态显示一行完整提示。

## 处理路径

文件导入由 AddFilesAsync 在后台逐个 probe，PdfPasswordDialog 对需要密码的 PDF 提供本地验证、同窗错误重试和取消；通过后才进入 FileCards。取消当前弹窗跳过当前文件，主界面取消可停止剩余导入。PdfAccessStore 按完整路径、长度、修改时间保存本实例已验证密码；重新添加未变化文件不再提示，版本变化先移除旧验证，删除输入/关闭释放关联。处理前检查版本并生成独立密码字典，密码不进入 ResultContext/设置/日志；只有各功能输入版本参与摘要。页面编辑模型在原件版本变化时也清空重载。

预览、页面载入/缩略图使用当前源文件密码。处理 JSON 中的 passwords 逐文件映射由 pdf_access.password_for 统一取值，split、watermark、merge、edit、encrypt/decrypt、compress 和所有导出都使用对应输入的密码；生成的 Office 中间 PDF 默认为空密码。单个 password 参数保留用于旧检查与单源调用兼容。空用户密码且仅有所有者限制的文档可自动验证；不要求输入未知所有者密码。取消弹窗会等待当前验证退出后关闭，私有 JSON 作业照常清理。

GUI 的 `Execute` 只协调输入准备和操作路由。Word/图片先分别生成临时 PDF，再按用户选择合并或分别输出。PDF 结构操作、文本/图片/DOCX 导出调用后端。PDF → DOCX 通过 OOXML 重建可编辑正文；无文字页嵌入渲染图，不调用 Word PDF 重排、不伪装 OCR。PDF 表单操作使用 append 导入规范字段树，合并按输入文档命名空间避开字段同名冲突。

0.4 增加 Excel、PPT 和本地 HTML 输入；PreparePdf 统一中间 PDF 路径，非 PDF 输入到其他输出也经过此路径。OfficeConverter 通过 --office-worker 隔离 Excel/PPT COM，宏关闭、只读，精确记录新 PID/启动时间，父进程取消/超时回收自身组件；不取得独立进程就报错，不接管用户会话。Excel 占位打开密码限制为 12 字符，过长会使 Open 失败。

HtmlConverter 先调用后端 prepare_html：HTMLParser 清除脚本、事件属性、框架、刷新跳转，添加 CSP 禁止脚本和网络，base 指向源目录以保留本地资源。再用独立 profile 的 Edge/Chrome headless 打印，代理和 DNS 参数补充阻止远程加载。不能全局 --disable-javascript/scriptEnabled=false，会连带破坏 CLI 打印。ProcessJob 在作用域结束时收尾新浏览器及子进程；等待继承的 stderr 管道关闭，不能把启动器先退出当成打印结束。

office_formats.py 延迟导入 openpyxl/python-pptx/pdfplumber，避免普通 PDF 预览承担新库冷启动成本。XLSX 用 pdfplumber 检测表格，未检出时逐行正文，扫描页嵌图，所有提取单元格设文本类型防止被当公式；PPTX 每页渲染为一张图片；HTML 以流式单文件输出内嵌 PNG，或转义后的可编辑正文。新库版本/许可证随构建锁定和收集。旧 XLS/PPT 仅输入，输出为 XLSX/PPTX。

后端结果以 `.tmp` 写入，完成后原子改名，取消不会将半成品显示为可下载。压缩优先保留原结构；有损档位只改变图片，无法缩小时原样复制。PDF 重写可能使数字签名失效，复杂 XFA/签名不保证兼容。

`engine/pdf_split.py` 先验证全部输入/页码。每页与指定页数组用 append 保留表单树，流式写入一个 ZIP。比例模式独立走整文档裁剪：append 全部页，逐页归一化显示旋转，按 keep（first/second）和保留比例裁剪 cropbox/mediabox 等边界；每个输入原子输出一份多页 PDF，页数/顺序不变，无 ZIP、无另一半。下方/右侧保留比例直接表示该侧保留量，不是上方/左侧的切割点。加密输入的输出 PDF 保留原密码；裁剪改变可见边界，不能用于脱敏。测试覆盖整个文档页数/顺序、四侧尺寸、旋转/非零框、批量以及 UI 输出格式。

`PdfEngine.cs` 启动打包进程，读取独立进度文件，取消/超时只终止对应工作进程，finally 删除私有作业。`ResultSession.cs` 只枚举完成结果，下载原子复制并保留扩展名。同名自动编号。根目录 EXE 使用相对路径定位 `dist/文件万能处理器/engine/PdfEngine.exe`，发行目录 EXE 使用自身 engine；不依赖开发机绝对路径。

`DownloadJob.cs` 在后台统计总长度、顺序保存，并按已写字节汇总百分比/平均速度/文件份数。ResultSession.SaveCopy 使用 128KiB 缓冲，首块及约 100ms 报告一次进度，区分 Copying/Flushing/Saved；Flush(true) 与同目录原子改名完成后才算保存成功。UI 的准备和落盘阶段使用仅忙碌时开启的动画，未全部提交时百分比上限 99%。同步记录已提交份数，Progress 将展示通知派发回 UI，结束/取消后忽略排队通知；取消不删除已提交文件，不清空可重试的处理结果。下载按钮保留可见但禁用以展示进度，上方主按钮用于取消。

`ToolResultStore.cs` 按功能分开管理最后一批结果及输入/参数摘要，修复旧全局 results 导致切工具仍暴露另一功能下载按钮的问题。切换恢复当前功能状态，未处理功能隐藏下载并清空进度；同功能参数变化明确标“上次结果”。所有工具的未下载结果都参与关闭提示和清理，不能因切页丢结果。新的加密密码只使摘要失效，不存入摘要；原文件密码通过导入验证关联。

## 预览与生命周期

水印由 WatermarkSettings 产生独立 WatermarkSpec 快照，UI 直接调用 Paint 在当前预览的可见页面矩形上重画，不触发 PDF 渲染。七种布局、九宫格位置、字体/描边、角度、间距、不透明度、颜色和页范围统一进入结果摘要。导出由 WatermarkJob 先请求 watermark_info 验证全部页范围并获取去重后的可见页面尺寸，再用同一 Paint 以 144dpi（最长 3200 像素上限）生成透明 PNG。生成只在后台进行，一张处理完释放位图。

VisualPickers.PositionGrid 用直接命中的 3×3 格与蓝色选中态替代九项文字下拉，支持方向键；ColorSwatch 显示实际颜色方块，点击仍使用原生颜色选择窗口。九宫格出现时增加所在行高度，其余选项随纵向滚动，不挤压为一行小字。

pdf_watermark 使用原 PDF 的 clone 保留正文/表单，嵌入压缩 RGB 图像 + 灰度 SMask，并按 cropbox 与 mediabox 交集、原旋转的逆变换追加图层；不改变页面旋转或原注释坐标。共享资源字典修改前复制，重复水印命名避碰，同文档相同尺寸复用图像对象。加密输入重新按原密码加密。页面物理尺寸含 UserUnit，preview_worker 的轻量 pypdf 元数据 reader 跟随私有快照打开/关闭，与 PDFium 绘制尺寸相互换算。图层不是可搜索文字，原页面不整页栅格化。所有图层存本次 engine.Workspace，finally 清理。

PDF 右侧由 PreviewRenderer 通过继承的 stdin/stdout 管道连接 --preview-server。每个窗口复用一份进程，worker 常驻一份 PDF 私有快照，文件路径/修改时间/长度/密码变化时关闭文档并清缓存；不持续占用用户原文件。PDFium 只在 worker 主线程串行执行，读管道线程只入队 JSON。当前页立即回复，随后在无前台请求时预取前后页，PNG LRU 至多 20 个。普通操作仍走 PdfEngine 单次 JSON 进程；缩略图保持可见区域批量加载（映射最多 80 个、位图约 72 个）。

原来每次未缓存翻页需要启动 inspect 和 render 两个进程；现页数与图像一次返回，无重复冷启动。取消优先等短请求结束以保留预热进程，超过约 250ms 取消宽限则结束本窗口 worker，20 秒超时也回收。ProcessJob 确保 GUI 强制结束时 worker 一同退出；正常关闭还删除本实例私有快照/PNG。PyInstaller windowed 的 sys.stdin/stdout 为 None，preview_worker 恢复父进程提供的标准管道句柄，不开控制台。

PdfPagePreview 在 PictureBox.Zoom 的实际页面矩形上绘半透明灰遮罩与蓝色虚线，不覆盖两侧留白。0.4.2 起选择器改为保留上/下/左/右，灰色表示裁去范围；KeepSecond 存入 SplitSettings.JobValues 与 Context，影响实际输出及上次结果判断。下/右保留时显示切割点为 1-保留比例。调整只重绘 UI、不重渲染 PDF；左侧参数区域随容器缩放重新计算，给完整文件卡片留出空间。

`FilePreview.cs` 读取 DOCX XML 和标准 DOC piece table 正文，正文缓存 16 项，以路径/修改时间/长度识别版本。快速 DOC 不启动 Word，解决旧版每次选择都冷启动 COM 的延迟。需要完整排版时用户显式点击排版预览，由 Word 隔离导出 PDF。`WordConverter.cs` 保留宏禁用、只读打开、超时、PID/启动时间精确收尾。独立正文重建代替了本机测试时会卡住的 PDFREFLOW。

正常关闭等待预览与缩略图退出，提示未下载结果/未保存页面，清理本实例随机工作目录；其他实例、用户备份与已下载文件不受影响。当前只记上次下载目录，不保留密码和文件列表。

## 构建

`build-v3.ps1` 构建 `.work/release`；`collect-licenses.py` 收集 Python、PDFium 与各包许可证。`publish.ps1` 确认版本后停止本项目旧 GUI，替换唯一 dist 当前目录，并同步根 EXE；不把测试运行器发给用户。入口 `build.ps1` 顺序调用暂存和发布。

扩展非 PDF 工具时增加左侧分类及其工作区，复用 ResultSession 与引擎任务边界；不把新业务塞进 PageDocument。Python 引擎目前作为本地处理宿主，图片动作在单独模块中分派，PDF 业务保持原接口，不建立跨项目依赖。

## 图片工作区（0.8.0）

ImageWorkspace 独立持有输入、设置、ToolResultStore、取消与关闭状态。MainForm 仅协调 PDF/音频/图片三工作区；输入准备和长处理放后台。共用 PdfEngine 的私有 JSON 作业与子进程取消边界，`image_*` 分派到 image_tools.py，不将图片业务写进 PDF 流程。导出与下载分离，继续复用原子保存/字节进度。

图片先由既有Pillow13解码、校正EXIF、尝试ICC→sRGB，生成私有PNG；C#不依赖系统WebP/AVIF扩展。0.8.1按最新要求允许放大溢出画布后的右键平移，只改变origin，限制边界避免整图拖丢，不生成编辑/不改OCR选择。SetImage同尺寸刷新保留zoom/origin，Fit/ActualSize复位居中；手动平移后的滚轮保留视图中心，默认仍居中。左键编辑，中键/空格不平移。

BrushSizePicker滑块选择屏幕逻辑大小，乘DeviceDpi/96再除zoom得到图像直径；光标和Draft共用BrushDiameter。光标中心按实际图像采样点对齐，去除旧的Math.Max(4,...)虚假下限。滑块调节时显示居中参考圈，回到图上则跟随实际笔刷；线条粗细也用相同换算。画笔/马赛克/橡皮为圆形路径，DraftFactory与输出共用绘制，裁剪/OCR才用选框。叠图解码按路径缓存，马赛克每手势一份缩小纹理并仅重画路径，松开/取消释放；橡皮模式额外持干净底图用于恢复预览。

ImageAsset最多100步命令，坐标属于每步输入尺寸，撤销/重做重放而不存每步全图。ImageEditing.RenderLayers维护照片底图和透明标注层；裁剪/旋转/翻转/尺寸同时变换两层，调色针对底图，eraser仅用SourceCopy清标注层，恢复原透明度，不擦原照片。渲染结束合成；普通处理不额外保留底图，只有橡皮预览保留。文字有稳定Id，retext替换原位置的文字命令，removeText可撤销，Effective解析后再重放，避免新字叠旧字；TextTargets通过后续几何变换计算命中区域。ImageWorkspaceText在输入/样式变化时立即更新命令，连续输入替换同一个末尾retext，离开输入框/重选/普通操作结束分组，保留一次撤销。独立串行后台预览只消费最新快照，通过代次/版本/源文件身份丢弃旧帧，不禁用或重建输入框、不回写文字/光标，保留缩放视口；一般任务先等待该预览收尾，关闭确认后同样等待再清理。重选从最新命令解析，避免命中过期画布后回退内容；画布Delete删除选中文字，输入框Delete保持正常删字。其他图形尚未提供通用对象移动/图层工程。

ImageFilters为16个固定色彩矩阵预设与强度混合，ImageFilterPicker从当前图生成小缩略图，无模型、素材或外部库。滤镜为独立工具/结果会话，180ms合并预览请求，预览与批量输出同一函数；编辑菜单不再放滤镜。图片原件不写回，静态处理多帧须显式取第一帧。

OCR在已打包Python子进程中通过Tesseract C API调用锁定DLL及两个原有LSTM模型。默认PSM3整页排版，可选PSM11零散标签；RIL_TEXTLINE=2获取行，再由ocr_lines.py合并相近基线/字高、小间距的片段。0.8.3将水平距离改为双向，并合并同时匹配的分组，修复按中心高度排序后右片段先到导致负gap漏合并，以及中间片段迟到未连接两组。不跨远列/不同行盲合并，过滤极小背景框。JSON键仍叫words；OcrTextFlow为右侧TextBox和画布SelectedText共用拼接，默认不强制框间换行，中文直接拼、英文空格、标点前不加空格，可选KeepTextLineBreaks恢复原行。ocr_preprocess.py提供自动/原图/浅文档/深背景/票证底纹，透明先填白、小图最多放大2倍，最长边4800，框反算回原图坐标。增强只给识别，不改原图，不保证所有碎片/艺术字体/标注截图正确，未验证所有排版。

UpdateOcrText根据画布选择投影到右侧只读可选TextBox，未选时显示全部，多选默认连段，可显式保留原换行；CopyText优先右侧部分选择，其次画布选择/全部显示内容。识别前/切文件/切工具清理旧文本，右键平移不触发SelectionChanged。结果预览入口无输出隐藏，有输出明确命名；viewingResult使结果只读，返回工作预览后恢复控件，参数变化标上次，避免草稿与已编码文件混淆。

ImageCompressionSettings独立管理压缩参数，不复用其他工具不可见的format/quality/firstOnly。按每图原格式与当前批次显示适用控件：JPG/WebP/AVIF三档92/80/55，手动画质滑块按需展开；PNG不展示无效画质，WebP无损开关只在适用时显示，动画取第一张仅多帧显示且默认关闭。百分比尺寸按每图自身宽高换算后端edge并保持比例；UI线程先按资产生成ImageCompressionSpec数组，后台不读控件。独立Context确保结果失效条件与实际参数一致。颜色标签和46×46按钮在ImageWatermarkSettings同一横排，Logo模式隐藏整行。

## 按需语音引擎（0.9.0）

按用户新指令，项目根目录 `外置引擎` 专存非自带引擎：runtimes、models、voice-packs、downloads；除README外全被Git忽略。SpeechCatalog.DefaultRoot从根入口或dist入口定位相同项目目录。自带PDF/图片、音频转换/降噪、OCR组件仍在dist；发布脚本不删除外置目录。没有资源也显示完整语音界面，禁止生成/试听，不自动联网。

SpeechCatalog是下载清单的单一来源：固定CPU版本、HTTPS地址、字节数、SHA256、许可证与音色映射。Whisper1.8.2+Tiny/Base多语言Q5_1，sherpa-onnx2.0.3+Kokoro v1.1/v1.0 INT8；相同引擎跨音色包复用。EngineStore流式下载到.part，HTTP Range续传、校验大小/SHA，ZIP路径及链接检查、TAR清单验证，随机临时目录解压并写逐文件散列/长度/时间收据，目录原子替换后才算安装；失败保留原可用版本，未完成下载不启用。收据缓存避免每次输入重新解析大量文件，运行前核对关键文件SHA；自定义包核对所有数据。TAR用系统tar.exe，目标Win10/11 x64。

SpeechWorkspace独立持有资源选择、输入、生成任务、试听、结果和保存，AudioWorkspace统一四个顶部导航并汇总处理Busy/Pending。0.9.1将引擎资源移入独立EngineDownloadManager，不计入页面处理Busy，下载/校验/解压时允许任意工作区和语音资源选择切换。SemaphoreSlim限制同时2个资源，其余等待；按package.Id去重并复用音色包共用引擎，终止任务可重试，同一Store对同ID的下载/离线安装加可取消等待锁。各线程用独立序列化器写安装收据。后台异常写任务状态而非在其他页面弹模态窗，UI按版本号轮询快照，避免旧包回调覆盖当前页。

EngineDownloadsForm为无模式逐项进度窗口，关闭只隐藏查看，不停下载；语音页按钮和所有工作区可见的侧栏链接均能打开。MainForm只有退出时单独检查HasActive并等待取消收尾；Manager保留包括被重试替换的未完成worker，Dispose在全部worker结束后释放令牌与信号量。不是跨进程下载服务，多个程序窗口不共享队列。引擎是否可处理仍由完整安装收据决定，不能用任务下载100%冒充安装成功。

语音工具结果隔离，结果保存仍使用DownloadJob原子保存与进度。TXT正文校对和原始SRT分开，SRT显示实际时间码、TXT改动标未保存。TTS可选WAV/MP3/FLAC，试听短句不覆盖正式结果；引擎/音色缺失或数据变化则禁用。音色导入只允许配套Kokoro数据及受限voice-pack.json，不执行包内程序；规范见[VOICE_PACKS](VOICE_PACKS.md)。新模型家族需实现适配器，不把录音当音色包。

SpeechJobs以私有作业目录调用原生计算，音频通过自带FFmpeg只读解码到16k单声道PCM；Whisper输出TXT/SRT。其原生CLI原有ANSI argv无法处理中文路径，安装时仅为本地whisper-cli.exe写UTF-8 activeCodePage manifest（Win10 1903+），再记录修改后散列；原下载原件校验依旧固定上游SHA。不修改系统区域/注册表。

TTS使用NativeTtsWorker，在相同GUI EXE的`--tts-worker`独立进程内加载固定C API（2.0.3 x64配置456字节，偏移对应官方头文件，不能无验证升级ABI）。job.json私有传文字，UTF-8指针传模型/词典，绕开原生CLI中文argv损坏。Kaldi规则加载仍用ANSI fopen，worker仅在自身进程设Unicode模型工作目录、用ASCII规则文件名，已验证中文路径；PCM由C#写WAV避免原生输出路径问题。SpeechProcess用ProcessJob、取消/超时精确终止自身worker；finally释放DLL/模型/非托管字符串并删私有作业。实际安装包、中文/英文合成、转写、MP3与SRT均由原生测试覆盖；不宣称全部音色、复杂录音、长期大文件或干净机器已通过。

测试：scripts/test-speech.ps1默认需要已校验的.work/speech-artifacts，-DownloadFixtures显式下载测试资源，-NetworkDownloads另测真实HTTPS取消/续传；不会把测试模型装进正式外置目录。prepare-speech-tests.py直接读SpeechCatalog的锁定值。上述资源均不打入dist，不计入Git新增模型；主仓库只收代码/文档和当前GUI，构建后必须同时交付根EXE+完整dist，允许收录不代表已经提交/推送。

体积评估在实施前完成：OCR库/两模型/必要VC运行库共14,304,324字节，加来源说明14,305,619字节。转换复用已发行Pillow；不增加 OpenCV、深度学习框架或字体素材。曾评估 pillow-heif 1.8.1，wheel解压约28.82MB且二进制GPLv2授权，不纳入本版，因此HEIC/HEIF暂不支持。prepare-image.py从当前发行文件验证复制，缺少时仅获取锁定的NuGet、模型及微软重分发包并提取所需DLL，不运行安装器；下载链接或哈希变化会停止。

调研依据：[Adobe 图片文字/Logo水印](https://helpx.adobe.com/photoshop-express/mobile/apply-settings/add-watermark.html)、[Microsoft Photos 常见裁剪/调整/标注](https://support.microsoft.com/en-au/windows/apps/photos/edit-photos-and-videos-in-windows)、[Pillow 格式与多帧支持](https://pillow.readthedocs.io/en/stable/handbook/image-file-formats.html)、[Tesseract 模型说明](https://tesseract-ocr.github.io/tessdoc/Data-Files)、[官方C API](https://github.com/tesseract-ocr/tesseract/blob/5.2.0/include/tesseract/capi.h)。只借鉴操作类型与API，不复制品牌素材。

## 音频工作区（0.6.0）

AudioWorkspace 独立管理格式转换与背景降噪的列表、参数、结果和试听，与 PDF 的状态和 ToolResultStore 分开；MainForm 左侧切换两个工作区，关闭时共同检查未下载结果和任务取消。保存复用 ResultSession / DownloadJob 的原子复制、字节进度与落盘反馈。

AudioEngine 使用两套经过校验的轻量组件，总计约 11.85MiB。Xiragi FFmpeg-Audio-Builds 仅作本地常见音频解码/编码与 ffprobe 识别，依赖约10.5MiB；其构建没有 afftdn 和 ASF muxer。为避免恢复151MiB通用引擎，从官方 FFmpeg9.0.2 源码构建约1.35MiB AudioDenoise.exe，只开启 PCM16/WAV 输入、PCM16/WMA输出和 afftdn/bandreject/必要音频滤镜。WMA转码、降噪先解码到本实例私有PCM WAV，再用精简补充组件处理，最后按所选格式编码。临时文件较大不影响 Git 包体，正常结束/取消 finally 清理，不把整段读入内存。

最终只暴露10个常见输出选择：MP3、WAV、FLAC、AAC、M4A(AAC/ALAC)、OGG/Vorbis、Opus、AIFF、WMA。Format列表按实际编码器筛选；M4A用mp4 muxer，WMA走补充组件。自动采样率适配编码器（有损通常48kHz），自动声道最多双声道；Opus固定48kHz，PCM输出为16-bit。所有输入先检查实际音轨与文件版本。禁止网络协议/播放列表，使用私有临时名和显式muxer，不覆盖原件；ProcessJob/取消仅收尾本次进程，错误输出有上限。

试听原声和处理后均为最多15秒的PCM16片段；后者先进行目标格式编码，再解码播放，覆盖有损编码影响。SoundPlayer只负责本地PCM播放，波形压缩为500个峰值；修改文件/参数/片段起点使试听失效，无后台自动播放。未提供整首播放、暂停、剪辑、声源分离或神经网络人声增强。

### 降噪依据与验证

使用 [FFmpeg afftdn](https://ffmpeg.org/ffmpeg-filters.html#afftdn) 频谱降噪：轻度 nr8/nf-45、均衡 nr14/nf-38、强力 nr24/nf-30，关闭自动追踪，gain smoothing为8；可选窄带50/60Hz bandreject。测试中自动追踪版本未充分降低合成白噪声，因此改为固定三档，不把滤镜成功运行当作有效。均衡样本静音段 RMS 0.02019→0.00774，目标正弦段 RMS 比约0.98985，仅说明该合成样本，不承诺所有录音效果。稳定底噪和人声分离不是同一问题，强档可能损伤细节。

调研 [RNNoise论文](https://arxiv.org/abs/1709.08243)、[DeepFilterNet论文](https://arxiv.org/abs/2110.05588) 及项目实现后，首批选择无需模型/大型框架的DSP方案；人声增强模型不应默认用于音乐。参考用户提供的 [Worthsee](https://audio.worthsee.com/convert/flac-to-mp3) 与 [Aconvert](https://www.aconvert.com/cn/audio/) 的格式/参数流程，但不复制联网服务。不能声称所有音频都双向支持。

### 组件复现与体积

assets/audio-runtime.lock.json 锁定音频专用下载包及每个运行文件SHA256，准备脚本优先验证/复制当前发行包，缺少时下载并核验；最新链接内容改变会拒绝，不自动升级。AudioDenoise源码URL/SHA和 scripts/build-audio-denoise.sh 随工程保留，工具链为现有MinGW-w64 GCC8.1.0与Git Bash。源码重建可能受工具链/PE时间戳影响；不同产物须重新验收并更新锁，不冒充位级复现。重建补充组件后可向 prepare-audio.ps1 传 -DenoiseExe，源码目录需保留 COPYING.LGPLv2.1。组件许可证和 SOURCES.txt 随发行包保存，不需用户安装编译环境。

用户最终明确新增上传Git内容控制在50MB内，编译下载中间文件可临时增大但收尾清理。当前方案小于12MiB，已放弃约151MiB通用引擎。不要打包ffplay、开发头文件、源码压缩包、大模型或两套引擎。运行内存另测：10分钟单声道48kHz合成样本（54.93MiB），精简降噪组件50ms采样最大工作集9.63MiB，耗时3.52秒；不是全GUI内存或所有输入保证。

tests/AudioChecks.cs 覆盖10种输出往返、元数据、取消、原件保护、真实工作区、编码后试听/波形、结果隔离与最小窗口；tests/audio_quality.py 独立计算噪声和目标信号。scripts/test.ps1 -SkipWord 同时运行音频与原PDF/密码/水印/下载/布局/预览回归。

官方资料：[pypdf 页面合并/表单](https://pypdf.readthedocs.io/en/stable/user/merging-pdfs.html)、[压缩](https://pypdf.readthedocs.io/en/stable/user/file-size.html)、[AES](https://pypdf.readthedocs.io/en/stable/user/encryption-decryption.html)、[PDFium 线程与释放](https://pypdfium2.readthedocs.io/en/stable/python_api.html)、[MS-DOC 正文算法](https://learn.microsoft.com/en-us/openspecs/office_file_formats/ms-doc/01d5d8c4-cf9c-4ef9-80fd-439e763cfe01)。

0.3.2 的导航/操作分层参考 [PDFgear 官方用户指南的界面示例](https://www.pdfgear.com/windows-user-guide/navigate-pdf.htm)，只借鉴结构，未复制品牌素材或代码。MainForm.RefreshCommands 集中约束空队列排序、图片旋转和页面选择操作的可用性。
