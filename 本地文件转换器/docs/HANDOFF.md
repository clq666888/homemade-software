# 当前交接状态

- 更新：2026-11-23，当前 **文件万能处理器 0.4.3**，Windows x64 / Framework 4.8。根入口 `文件万能处理器.exe`；完整运行库仅 `dist/文件万能处理器`。项目文件夹仍为 `本地文件转换器`。根/dist GUI SHA256：`568C99EE6B06560772AE1510F0ABA533970674A6B1A047F6359DEBA557703E4F`。
- 本轮统一窗口、侧栏、EXE 产品信息、入口、发行目录与脚本名称；publish 仅迁移本项目旧名 EXE/发行目录。内部 namespace 与 `%LOCALAPPDATA%/LocalFileConverter/settings.xml` 保留，避免丢失设置。
- 下载：MainForm.SaveResults 立即显示准备动画；DownloadJob 后台统计大小，按总字节展示百分比/已存大小/总量/当前文件/份数/平均速度。ResultSession.SaveCopy 首块及约 100ms 报告进度，Flush(true) 时显示“正在完成保存”并动画；全部原子提交才到 100%。上方按钮取消，当前未完成文件清理，已提交文件及处理结果保留。同步记录已完成份数；UI 结束后忽略排队通知。动画仅忙碌时开启并随控件释放。
- 本轮验证：GUI Release 编译、下载 17 项通过，含 64MiB 哈希、零字节、同名保护、批量进度单调、落盘前不上 100%、复制/落盘取消清理、原件不变、真实窗体下载/取消/迟到通知。自身窗体截图核对进度和落盘展示。测试安装 WinForms 同步上下文模拟 Application.Run，原下载目录设置已恢复。改名时布局/实际 UI 流程 26 项通过，根入口以新标题实际启动。本轮未改后端或 Office，未重复其全套检查。
- 本次用户明确纠正：只有“每页”模式逐页拆分；“指定页数拆分”按各份范围分组；“按比例切割”应裁剪掉灰色范围，每个原 PDF 输出一份裁剪 PDF，保留全部页数和原顺序，不能导出另一半/逐页文件/ZIP。批量时每个输入各一份，不擅自合并。
- 当前交互：上下/左右二选一，右侧选择“保留：上方/下方/左侧/右侧”，参数为“保留比例”。例如保留下方 30%，灰色上方 70% 被裁去。选择保留侧现在是实际输出参数，进入 JobValues/Context；不能再当纯预览切换。只重画遮罩、仍复用原预览图。按钮改“开始裁剪”及“下载裁剪结果”，说明按模式显示 PDF 或 ZIP。
- 主要入口：SplitSettings.KeepSecond 与 ratio/axis 组成裁剪参数；MainForm UpdateCutPreview 将下/右侧的显示切割点换算为 1-保留比例并更新结果有效性；pdf_split 的 ratio 独立走完整文档 append/逐页裁剪/原子写 PDF，each/ranges 仍单 ZIP。裁剪先归一化显示旋转，并按原 cropbox 设置全部页面边界；保留加密密码与页数/顺序。
- 上轮 0.4.2 验证：源码与打包 PDF 回归各 48 项通过，覆盖整份 PDF 输出、无 ZIP/单页、页数/顺序、四侧百分比、批量、旋转/非零框、密码保留。Office/HTML 后端 14 项、布局 26 项、PreviewChecks 38 项通过，含实际 UI 裁剪后仍 3 页及预览生命周期。Poppler 核对裁剪导出，自身窗体截图核对参数与按钮。
- 预览优化保留：每窗口一份 PreviewRenderer/preview_worker，管道复用、私有 PDF 快照、20 PNG LRU、邻页预取，PDFium 仅 worker 主线程串行。文件版本/密码变化清缓存，不锁原件；关闭清理本实例进程与私有目录。windowed PyInstaller 须恢复继承的 stdin/stdout 句柄。此前样本请求中位约 774ms→5ms，本次约 817.3ms→4.8ms、首次 402.4ms、工作集约 48MiB，均为 168.5KiB 3 页样本，不是所有文件/屏幕绘制保证。
- 其他既有功能保留：PDF/Word/Excel/PPT/本地 HTML/图片输入和 PDF/图片/TXT/DOCX/XLSX/PPTX/HTML 输出；Office/浏览器依赖及还原限制见 README。工具结果隔离、上次结果提示、原件默认只读，页面覆盖需确认并保留 .bak。裁剪只改变页面边界，不能当脱敏；无 OCR，不承诺无损还原复杂 Office 排版。指定页码仍两个空数字框以 ~ 分隔。
- 构建/测试：scripts/build.ps1 完整构建；只有 C# 改动可 -GuiOnly，pdf_split/preview_worker 变动必须重打包。scripts/test.ps1 -SkipWord 包括 PDF/Office 后端、布局和预览；不加开关再测 Word/Office 桌面。本轮未改 COM/HTML 引擎，未重复 Office 桌面全套；0.4.0 集成 31 项历史通过。
- 依赖与生命周期约束：PDFium 不跨线程并行；HTML 本地清理/CSP，不使用全局禁 JS 浏览器参数；Office 只读/关闭宏，精确新 PID 收尾，不批量结束用户 Office。Excel 随机占位密码不超过 15 字符。密码不放命令行/配置/正常日志。
- 未验证：全部 DPI/多屏、干净机器、所有复杂 PDF/Office/HTML、网络盘/可移动盘、长期大文件压力及所有保存对话框边界。Flush(true) 期间取消需等磁盘调用返回，窗口仍响应并显示动画。复杂页面首次打开可能较慢，PDF 重写可能使签名失效。
- 范围/清理：唯一项目本目录；用户授权更新强制关本项目旧 EXE，不读兄弟项目。交付清理本轮 .work 源码样本/回归样本/截图/测试运行器/重复引擎构建；保留唯一发行约 75.7MiB、维护 .venv 约 93.7MiB、源码/锁/图标和用户备份。.gitignore 已维护，无 Git，不 init/提交/推送。
- 下一步：用户实测实际大文件保存和整份文档裁剪，根据具体文件与磁盘处理兼容和体验问题。
