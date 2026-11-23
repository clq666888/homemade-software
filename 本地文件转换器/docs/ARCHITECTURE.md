# 模块与边界

界面继续使用原生 WinForms，新增专用离线 PDF 引擎，而非浏览器壳或后台服务。GUI 与 PDF 引擎通过单次私有 JSON 作业连接，密码不在进程命令行。后端使用 pypdf（结构、页面、AES、压缩）、PDFium/pypdfium2（页面渲染）、Pillow（图片）、olefile（DOC 容器）。所有原生运行库随 PyInstaller 目录发行。

## 界面与页面模型

- `MainForm.cs`：左侧 PDF 主分类，顶部功能选择；文件队列、工具参数、右侧预览、先处理后下载状态。处理在 Task.Run，进度回 UI；页面载入、预览、缩略图不在 UI 线程执行。
- `UiControls.cs`：FunctionTab 用文字+底线表达导航，不画普通操作按钮边框；ModernButton.Toolbar 用轻量悬停表达操作，添加文件使用描边，流程主动作使用实色。键盘焦点仅在 ShowFocusCues 时显示。FileCards 绘制卡片与红色垃圾桶；PageCanvas 自绘所有页的位置，只为可见页请求缩略图，支持框选、Ctrl/Shift、拖动排序、红叉。手动统一系统 DPI 的布局尺寸，自绘按逻辑坐标换算。
- `PageDocument.cs`：页来源索引与旋转，独立快照撤销栈（100 步），删除至少留一页；覆盖保存检查大小/时间，File.Replace 建立原件备份。
- `SplitSettings.cs`：每页/指定页码/比例切割动态面板；每份以两个空数字文本框和固定 ~ 标记输入起止页，键入限制 ASCII 数字，提交再次验证粘贴值/正整数/起止顺序。内部生成后端规范的范围字符串，用户不输入范围符号；实际页数越界由后端验证。保留份数调整时未删除行的输入，互斥方向复选框和比例；只有当前方式的参数参与结果摘要与作业。MainForm 在 UI 线程读取参数，后台不读控件。

导航项和文件/页面操作项用短竖线分隔，预览与左侧有独立竖线；处理选项白底标题、上下横线与执行区分开。LayoutOptions 按当前字体/DPI 测量组合框完整文本，说明独立换行，面板随动态选项调整高度并允许滚动。单行/单列的 TableLayout 显式使用百分比，避免默认 AutoSize 在缩小时保留旧尺寸；WrappingOptionsPanel 使用视口宽度换行，避免旧虚拟宽度造成横向溢出。动态按钮按 DPI 设置尺寸；FileCards 紧凑空状态显示一行完整提示。

## 处理路径

GUI 的 `Execute` 只协调输入准备和操作路由。Word/图片先分别生成临时 PDF，再按用户选择合并或分别输出。PDF 结构操作、文本/图片/DOCX 导出调用后端。PDF → DOCX 通过 OOXML 重建可编辑正文；无文字页嵌入渲染图，不调用 Word PDF 重排、不伪装 OCR。PDF 表单操作使用 append 导入规范字段树，合并按输入文档命名空间避开字段同名冲突。

0.4 增加 Excel、PPT 和本地 HTML 输入；PreparePdf 统一中间 PDF 路径，非 PDF 输入到其他输出也经过此路径。OfficeConverter 通过 --office-worker 隔离 Excel/PPT COM，宏关闭、只读，精确记录新 PID/启动时间，父进程取消/超时回收自身组件；不取得独立进程就报错，不接管用户会话。Excel 占位打开密码限制为 12 字符，过长会使 Open 失败。

HtmlConverter 先调用后端 prepare_html：HTMLParser 清除脚本、事件属性、框架、刷新跳转，添加 CSP 禁止脚本和网络，base 指向源目录以保留本地资源。再用独立 profile 的 Edge/Chrome headless 打印，代理和 DNS 参数补充阻止远程加载。不能全局 --disable-javascript/scriptEnabled=false，会连带破坏 CLI 打印。ProcessJob 在作用域结束时收尾新浏览器及子进程；等待继承的 stderr 管道关闭，不能把启动器先退出当成打印结束。

office_formats.py 延迟导入 openpyxl/python-pptx/pdfplumber，避免普通 PDF 预览承担新库冷启动成本。XLSX 用 pdfplumber 检测表格，未检出时逐行正文，扫描页嵌图，所有提取单元格设文本类型防止被当公式；PPTX 每页渲染为一张图片；HTML 以流式单文件输出内嵌 PNG，或转义后的可编辑正文。新库版本/许可证随构建锁定和收集。旧 XLS/PPT 仅输入，输出为 XLSX/PPTX。

后端结果以 `.tmp` 写入，完成后原子改名，取消不会将半成品显示为可下载。压缩优先保留原结构；有损档位只改变图片，无法缩小时原样复制。PDF 重写可能使数字签名失效，复杂 XFA/签名不保证兼容。

`engine/pdf_split.py` 先验证全部输入/页码。每页与指定页数组用 append 保留表单树，流式写入一个 ZIP。比例模式独立走整文档裁剪：append 全部页，逐页归一化显示旋转，按 keep（first/second）和保留比例裁剪 cropbox/mediabox 等边界；每个输入原子输出一份多页 PDF，页数/顺序不变，无 ZIP、无另一半。下方/右侧保留比例直接表示该侧保留量，不是上方/左侧的切割点。加密输入的输出 PDF 保留原密码；裁剪改变可见边界，不能用于脱敏。测试覆盖整个文档页数/顺序、四侧尺寸、旋转/非零框、批量以及 UI 输出格式。

`PdfEngine.cs` 启动打包进程，读取独立进度文件，取消/超时只终止对应工作进程，finally 删除私有作业。`ResultSession.cs` 只枚举完成结果，下载原子复制并保留扩展名。同名自动编号。根目录 EXE 使用相对路径定位 `dist/文件万能处理器/engine/PdfEngine.exe`，发行目录 EXE 使用自身 engine；不依赖开发机绝对路径。

`DownloadJob.cs` 在后台统计总长度、顺序保存，并按已写字节汇总百分比/平均速度/文件份数。ResultSession.SaveCopy 使用 128KiB 缓冲，首块及约 100ms 报告一次进度，区分 Copying/Flushing/Saved；Flush(true) 与同目录原子改名完成后才算保存成功。UI 的准备和落盘阶段使用仅忙碌时开启的动画，未全部提交时百分比上限 99%。同步记录已提交份数，Progress 将展示通知派发回 UI，结束/取消后忽略排队通知；取消不删除已提交文件，不清空可重试的处理结果。下载按钮保留可见但禁用以展示进度，上方主按钮用于取消。

`ToolResultStore.cs` 按功能分开管理最后一批结果及输入/参数摘要，修复旧全局 results 导致切工具仍暴露另一功能下载按钮的问题。切换恢复当前功能状态，未处理功能隐藏下载并清空进度；同功能参数变化明确标“上次结果”。所有工具的未下载结果都参与关闭提示和清理，不能因切页丢结果。密码只使摘要失效，不存入摘要。

## 预览与生命周期

PDF 右侧由 PreviewRenderer 通过继承的 stdin/stdout 管道连接 --preview-server。每个窗口复用一份进程，worker 常驻一份 PDF 私有快照，文件路径/修改时间/长度/密码变化时关闭文档并清缓存；不持续占用用户原文件。PDFium 只在 worker 主线程串行执行，读管道线程只入队 JSON。当前页立即回复，随后在无前台请求时预取前后页，PNG LRU 至多 20 个。普通操作仍走 PdfEngine 单次 JSON 进程；缩略图保持可见区域批量加载（映射最多 80 个、位图约 72 个）。

原来每次未缓存翻页需要启动 inspect 和 render 两个进程；现页数与图像一次返回，无重复冷启动。取消优先等短请求结束以保留预热进程，超过约 250ms 取消宽限则结束本窗口 worker，20 秒超时也回收。ProcessJob 确保 GUI 强制结束时 worker 一同退出；正常关闭还删除本实例私有快照/PNG。PyInstaller windowed 的 sys.stdin/stdout 为 None，preview_worker 恢复父进程提供的标准管道句柄，不开控制台。

PdfPagePreview 在 PictureBox.Zoom 的实际页面矩形上绘半透明灰遮罩与蓝色虚线，不覆盖两侧留白。0.4.2 起选择器改为保留上/下/左/右，灰色表示裁去范围；KeepSecond 存入 SplitSettings.JobValues 与 Context，影响实际输出及上次结果判断。下/右保留时显示切割点为 1-保留比例。调整只重绘 UI、不重渲染 PDF；左侧参数区域随容器缩放重新计算，给完整文件卡片留出空间。

`FilePreview.cs` 读取 DOCX XML 和标准 DOC piece table 正文，正文缓存 16 项，以路径/修改时间/长度识别版本。快速 DOC 不启动 Word，解决旧版每次选择都冷启动 COM 的延迟。需要完整排版时用户显式点击排版预览，由 Word 隔离导出 PDF。`WordConverter.cs` 保留宏禁用、只读打开、超时、PID/启动时间精确收尾。独立正文重建代替了本机测试时会卡住的 PDFREFLOW。

正常关闭等待预览与缩略图退出，提示未下载结果/未保存页面，清理本实例随机工作目录；其他实例、用户备份与已下载文件不受影响。当前只记上次下载目录，不保留密码和文件列表。

## 构建

`build-v3.ps1` 构建 `.work/release`；`collect-licenses.py` 收集 Python、PDFium 与各包许可证。`publish.ps1` 确认版本后停止本项目旧 GUI，替换唯一 dist 当前目录，并同步根 EXE；不把测试运行器发给用户。入口 `build.ps1` 顺序调用暂存和发布。

扩展非 PDF 工具时增加左侧分类及其工作区，复用 ResultSession 与引擎任务边界；不把音频等新格式的业务塞进 PageDocument 或 PDF worker，不建立跨项目依赖。

官方资料：[pypdf 页面合并/表单](https://pypdf.readthedocs.io/en/stable/user/merging-pdfs.html)、[压缩](https://pypdf.readthedocs.io/en/stable/user/file-size.html)、[AES](https://pypdf.readthedocs.io/en/stable/user/encryption-decryption.html)、[PDFium 线程与释放](https://pypdfium2.readthedocs.io/en/stable/python_api.html)、[MS-DOC 正文算法](https://learn.microsoft.com/en-us/openspecs/office_file_formats/ms-doc/01d5d8c4-cf9c-4ef9-80fd-439e763cfe01)。

0.3.2 的导航/操作分层参考 [PDFgear 官方用户指南的界面示例](https://www.pdfgear.com/windows-user-guide/navigate-pdf.htm)，只借鉴结构，未复制品牌素材或代码。MainForm.RefreshCommands 集中约束空队列排序、图片旋转和页面选择操作的可用性。
